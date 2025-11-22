using RALogicEditor.Models;
using RALogicEditor.Parsers;
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

        public string CurrentGameId { get; private set; } = "";
        public string CurrentTitle { get; private set; } = "";

        public void LoadFiles(string primaryPath, out string userFilePath)
        {
            ServerData = null;
            LocalData = null;
            userFilePath = "";
            CurrentGameId = "";

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

            // 2. Determine Paths
            string jsonPath = Path.Combine(dir, $"{gameIdStr}.json");
            string userPath = Path.Combine(dir, $"{gameIdStr}-User.txt");
            string notesPath = Path.Combine(dir, $"{gameIdStr}-Notes.json");

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
                }
            }
            catch (Exception) { /* Log handling could go here */ }
        }

        private void LoadAndMergeNotes(string notesPath)
        {
            List<CodeNote> jsonNotes = new();

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
                    jsonNotes = JsonSerializer.Deserialize<List<CodeNote>>(jsonContent, options) ?? new();
                }
                catch { /* Ignore load errors */ }
            }

            // Merge Logic:
            // 1. Dictionary keyed by Address
            // 2. Add JSON notes first
            // 3. Overwrite/Add Local notes (User.txt)

            var mergedNotesMap = new Dictionary<string, CodeNote>();

            foreach (var n in jsonNotes)
            {
                if (!mergedNotesMap.ContainsKey(n.Address))
                    mergedNotesMap[n.Address] = n;
            }

            if (LocalData != null && LocalData.Notes != null)
            {
                foreach (var n in LocalData.Notes)
                {
                    // Local notes take precedence (edits)
                    mergedNotesMap[n.Address] = n;
                }

                // Update the LocalData list to contain the full merged set
                // This ensures the UI sees everything
                LocalData.Notes = mergedNotesMap.Values.OrderBy(n => n.Address).ToList();
            }
        }

        // Merges Server and Local lists based on IDs to determine Sync status
        public List<T> GetMergedList<T>(bool isLeaderboard) where T : IAssetItem
        {
            var list = new List<T>();
            if (ServerData == null && LocalData == null) return list;

            var serverItems = ServerData?.Sets
                .SelectMany(s => isLeaderboard ? s.Leaderboards.Cast<T>() : s.Achievements.Cast<T>())
                .ToDictionary(i => i.ID) ?? new Dictionary<int, T>();

            var localItems = (isLeaderboard ? LocalData?.Leaderboards.Cast<T>() : LocalData?.Achievements.Cast<T>())
                             ?? Enumerable.Empty<T>();

            var processedIds = new HashSet<int>();

            foreach (var loc in localItems)
            {
                if (serverItems.ContainsKey(loc.ID))
                {
                    var srv = serverItems[loc.ID];
                    if (AreAssetsEqual(loc, srv))
                        loc.State = AssetState.Synced;
                    else
                        loc.State = AssetState.Modified;
                }
                else
                {
                    loc.State = AssetState.Local;
                }
                list.Add(loc);
                processedIds.Add(loc.ID);
            }

            foreach (var srv in serverItems.Values)
            {
                if (!processedIds.Contains(srv.ID))
                {
                    srv.State = AssetState.Synced;
                    list.Add(srv);
                }
            }

            return list.OrderBy(x => x.ID).ToList();
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
            }
            return true;
        }

        public void EnsureLocalDataExists()
        {
            if (LocalData == null) LocalData = new UserTextParser.UserData();
        }
    }
}