#nullable enable
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Services; // Added for Metadata
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RALogicEditor.Services
{
    public class ProjectRepository
    {
        public RaRoot? ServerData { get; private set; }
        public UserTextParser.UserData? LocalData { get; private set; }
        public RichPresenceScript? RichPresence { get; set; }

        public string RichPresenceFilePath { get; private set; } = "";

        // Metadata Service to persist group names
        public ProjectMetadataService Metadata { get; private set; } = new();

        private List<CodeNote> _serverNotes = new(); // Store original server notes for comparison

        public string CurrentGameId { get; private set; } = "";
        public string CurrentTitle { get; private set; } = "";

        public void LoadFiles(string primaryPath, out string userFilePath)
        {
            ServerData = null;
            LocalData = null;
            RichPresence = null;
            _serverNotes.Clear();
            userFilePath = "";
            CurrentGameId = "";
            RichPresenceFilePath = "";

            string dir = Path.GetDirectoryName(primaryPath) ?? "";
            string fileName = Path.GetFileNameWithoutExtension(primaryPath);

            // 1. Extract Game ID
            string gameIdStr = new string(fileName.TakeWhile(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(gameIdStr))
            {
                var digits = fileName.Where(char.IsDigit).ToArray();
                if (digits.Length > 0) gameIdStr = new string(digits);
                else gameIdStr = fileName;
            }
            CurrentGameId = gameIdStr;

            // Load Metadata immediately to have names ready
            Metadata.Load(CurrentGameId);

            // 2. Determine Paths
            string jsonPath = Path.Combine(dir, $"{gameIdStr}.json");
            string userPath = Path.Combine(dir, $"{gameIdStr}-User.txt");
            string notesPath = Path.Combine(dir, $"{gameIdStr}-Notes.json");
            string rpPath = Path.Combine(dir, $"{gameIdStr}-Rich.txt");

            string selectedExt = Path.GetExtension(primaryPath).ToLower();

            if (selectedExt == ".json")
            {
                // If opening Notes file directly, treat it as such, otherwise logic file
                if (primaryPath.EndsWith("-Notes.json", StringComparison.OrdinalIgnoreCase))
                    notesPath = primaryPath;
                else
                    jsonPath = primaryPath;
            }
            else if (selectedExt == ".txt")
            {
                if (primaryPath.EndsWith("-Rich.txt", StringComparison.OrdinalIgnoreCase))
                    rpPath = primaryPath;
                else
                    userPath = primaryPath;
            }

            // 3. Load Logic Data
            if (File.Exists(jsonPath)) LoadServerLogic(jsonPath);

            // 4. Load User Data
            if (File.Exists(userPath))
            {
                LocalData = UserTextParser.ParseUserFile(userPath);
                userFilePath = userPath;
            }
            else
            {
                // Initialize empty local data if no user file exists yet
                LocalData = new UserTextParser.UserData();
            }

            // 5. Load and Merge Notes
            LoadAndMergeNotes(notesPath);

            // 6. Load Rich Presence
            RichPresenceFilePath = rpPath;
            if (File.Exists(rpPath))
            {
                try
                {
                    string rpContent = File.ReadAllText(rpPath);
                    RichPresence = RichPresenceParser.Parse(rpContent);
                }
                catch { /* Ignore RP load errors */ }
            }
        }

        private void LoadServerLogic(string filepath)
        {
            try
            {
                string jsonContent = File.ReadAllText(filepath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                ServerData = JsonSerializer.Deserialize<RaRoot>(jsonContent, options);

                if (ServerData != null)
                {
                    if (ServerData.Sets == null) ServerData.Sets = new List<RaSet>();
                    CurrentTitle = $"{ServerData.Title} ({ServerData.GameId})";

                    // Apply categorization rules
                    foreach (var set in ServerData.Sets)
                    {
                        foreach (var a in set.Achievements)
                        {
                            // Rule: Flags 3 = Core, Flags 5 = Unofficial. Everything else is treated as invalid/fallback.
                            if (a.Flags == 3) a.Category = "Core";
                            else if (a.Flags == 5) a.Category = "Unofficial";
                            else a.Category = "Unofficial"; // Fallback for invalid/unknown flags within a server set
                        }

                        foreach (var l in set.Leaderboards)
                        {
                            // Leaderboards don't usually have Flags in this JSON format, utilize Set Type
                            l.Category = set.Type;
                        }
                    }
                }
            }
            catch (Exception) { /* Log handling could go here */ }
        }

        private void LoadAndMergeNotes(string notesPath)
        {
            if (File.Exists(notesPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(notesPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };
                    _serverNotes = JsonSerializer.Deserialize<List<CodeNote>>(jsonContent, options) ?? new();
                }
                catch { /* Ignore load errors */ }
            }

            // Merge Logic:
            // 1. Start with Server Notes
            // 2. Overlay Local Notes (User.txt)

            var mergedNotesMap = new Dictionary<string, CodeNote>();

            foreach (var n in _serverNotes)
            {
                // Store a copy to ensure we don't modify the reference in _serverNotes list
                if (!mergedNotesMap.ContainsKey(n.Address))
                    mergedNotesMap[n.Address] = new CodeNote { Address = n.Address, Note = n.Note, User = n.User };
            }

            if (LocalData != null)
            {
                if (LocalData.Notes != null)
                {
                    foreach (var n in LocalData.Notes)
                    {
                        // Local notes take precedence (edits)
                        mergedNotesMap[n.Address] = n;
                    }
                }

                // Update the LocalData list to contain the full merged set for the UI
                LocalData.Notes = mergedNotesMap.Values.OrderBy(n => n.Address).ToList();
            }
        }

        // Checks if a note matches exactly what is on the server (Address and Content)
        public bool IsNoteSynced(CodeNote note)
        {
            var serverNote = _serverNotes.FirstOrDefault(n => n.Address == note.Address);
            if (serverNote == null) return false; // New local note

            // Compare content, ignoring User field
            return serverNote.Note == note.Note;
        }

        // Merges Server and Local lists based on IDs to determine Sync status
        // Modified: Now preserves the natural reading order of the JSON files rather than forcing numerical ID sorts
        public List<T> GetMergedList<T>(bool isLeaderboard) where T : IAssetItem
        {
            var list = new List<T>();
            if (ServerData == null && LocalData == null) return list;

            // Preserve JSON order natively
            var serverItemsList = ServerData?.Sets
                .SelectMany(s => isLeaderboard ? s.Leaderboards.Cast<T>() : s.Achievements.Cast<T>())
                .ToList() ?? new List<T>();

            var localItemsList = (isLeaderboard ? LocalData?.Leaderboards.Cast<T>() : LocalData?.Achievements.Cast<T>())?.ToList() ?? new List<T>();

            var localItemsDict = localItemsList.ToDictionary(i => i.ID);
            var processedIds = new HashSet<int>();

            // 1. Map Server Items in their natural JSON order, tagging local modifications if they exist
            foreach (var srv in serverItemsList)
            {
                if (localItemsDict.TryGetValue(srv.ID, out var loc))
                {
                    if (AreAssetsEqual(loc, srv))
                        loc.State = AssetState.Synced;
                    else
                        loc.State = AssetState.Modified;

                    loc.Category = srv.Category;
                    list.Add(loc);
                }
                else
                {
                    srv.State = AssetState.Synced;
                    list.Add(srv);
                }
                processedIds.Add(srv.ID);
            }

            // 2. Append Local-Only (New) items at the bottom in their natural User.txt order
            foreach (var loc in localItemsList)
            {
                if (!processedIds.Contains(loc.ID))
                {
                    loc.State = AssetState.Local;
                    list.Add(loc);
                }
            }

            return list;
        }

        private bool AreAssetsEqual(IAssetItem a, IAssetItem b)
        {
            if (a.Title != b.Title) return false;
            if (a.Description != b.Description) return false;
            if (a.Logic != b.Logic) return false;

            if (a is RaAchievement aa && b is RaAchievement bb)
            {
                if (aa.Points != bb.Points) return false;
                if (aa.Type != bb.Type) return false;
                // Note: We don't compare Flags/Category for sync state, as User.txt doesn't store them.
                // Sync state is strictly about data persisted in User.txt vs Server.
            }
            else if (a is RaLeaderboard la && b is RaLeaderboard lb)
            {
                if (la.Format != lb.Format) return false;
                if (la.LowerIsBetter != lb.LowerIsBetter) return false;
            }
            return true;
        }

        public void EnsureLocalDataExists()
        {
            if (LocalData == null) LocalData = new UserTextParser.UserData();
        }

        public void SaveRichPresence()
        {
            if (RichPresence != null && !string.IsNullOrEmpty(RichPresenceFilePath))
            {
                try
                {
                    File.WriteAllText(RichPresenceFilePath, RichPresence.ToString());
                }
                catch { /* Ignore save errors */ }
            }
        }
    }
}