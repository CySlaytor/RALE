using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RALogicEditor.Helpers
{
    public class RecentFileEntry
    {
        public string Path { get; set; } = "";
        public string GameTitle { get; set; } = "";
    }

    public class RecentFilesManager
    {
        private const int MaxFiles = 10;

        // Save to JSON to store Title + Path structure
        private readonly string FilePathJson = Path.Combine(Application.StartupPath, "recent.json");
        // Legacy support
        private readonly string FilePathTxt = Path.Combine(Application.StartupPath, "recent.txt");

        public List<RecentFileEntry> Files { get; private set; } = new();

        public void Load()
        {
            Files.Clear();

            // 1. Try loading new JSON format
            if (File.Exists(FilePathJson))
            {
                try
                {
                    string json = File.ReadAllText(FilePathJson);
                    var loaded = JsonSerializer.Deserialize<List<RecentFileEntry>>(json);
                    if (loaded != null)
                    {
                        // Filter out files that no longer exist
                        Files = loaded.Where(f => File.Exists(f.Path)).ToList();
                    }
                }
                catch { /* Ignore corrupt file */ }
            }
            // 2. Fallback: Migrate from old TXT format
            else if (File.Exists(FilePathTxt))
            {
                try
                {
                    var lines = File.ReadAllLines(FilePathTxt).Where(File.Exists).ToList();
                    foreach (var path in lines)
                    {
                        string title = ExtractGameTitle(path);
                        Files.Add(new RecentFileEntry { Path = path, GameTitle = title });
                    }
                    // Save immediately to JSON format
                    Save();
                }
                catch { /* Ignore errors */ }
            }
        }

        public void Add(string path)
        {
            // Normalize path
            string fullPath = Path.GetFullPath(path);

            // Remove existing entry for this path to bump it to top
            Files.RemoveAll(f => f.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase));

            // Extract title
            string title = ExtractGameTitle(fullPath);

            // Insert at top
            Files.Insert(0, new RecentFileEntry { Path = fullPath, GameTitle = title });

            // Trim list
            if (Files.Count > MaxFiles)
            {
                Files.RemoveAt(Files.Count - 1);
            }

            Save();
        }

        private void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Files, options);
                File.WriteAllText(FilePathJson, json);
            }
            catch { /* Ignore write errors */ }
        }

        private string ExtractGameTitle(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";

            try
            {
                // Method 1: Check for JSON (Server Data or Notes)
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    // Peek at the file content without full deserialization for speed, or just use regex
                    // JSON format: "Title": "Game Name"
                    string content = File.ReadAllText(path);

                    // Simple Regex to capture Title value
                    var match = Regex.Match(content, "\"Title\"\\s*:\\s*\"(.*?)\"");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
                // Method 2: Check for User Text File (-User.txt)
                else if (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    // Format:
                    // 1.0
                    // Game Title
                    using (var reader = new StreamReader(path))
                    {
                        string? versionLine = reader.ReadLine(); // Skip 1.0
                        string? titleLine = reader.ReadLine();   // Game Title

                        if (!string.IsNullOrWhiteSpace(titleLine))
                        {
                            return titleLine.Trim();
                        }
                    }
                }
            }
            catch
            {
                // Fail silently and return empty
            }

            return "";
        }
    }
}