using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace RALogicEditor.Services
{
    public class Snippet
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string LogicCode { get; set; } = "";
    }

    public static class SnippetManager
    {
        private static readonly string SnippetPath = Path.Combine(Application.StartupPath, "snippets.json");
        private static List<Snippet> _cache = new();

        public static List<Snippet> Load()
        {
            if (!File.Exists(SnippetPath)) return new List<Snippet>();
            try
            {
                string json = File.ReadAllText(SnippetPath);
                _cache = JsonSerializer.Deserialize<List<Snippet>>(json) ?? new List<Snippet>();
            }
            catch
            {
                _cache = new List<Snippet>();
            }
            return _cache;
        }

        public static void SaveSnippet(string name, string logic)
        {
            // Ensure loaded
            if (_cache.Count == 0 && File.Exists(SnippetPath)) Load();

            var newSnippet = new Snippet { Name = name, LogicCode = logic };
            _cache.Add(newSnippet);
            SaveDisk();
        }

        public static void DeleteSnippet(Snippet snippet)
        {
            if (_cache.Contains(snippet))
            {
                _cache.Remove(snippet);
                SaveDisk();
            }
        }

        private static void SaveDisk()
        {
            try
            {
                string json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SnippetPath, json);
            }
            catch { }
        }
    }
}