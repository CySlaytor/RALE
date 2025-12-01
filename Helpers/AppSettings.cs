#nullable enable
using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace RALogicEditor.Helpers
{
    public class UserSettings
    {
        // View State
        public bool ShowLogicReviewer { get; set; } = false;

        // Window Geometry
        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;
        public int WindowWidth { get; set; } = 1200;
        public int WindowHeight { get; set; } = 700;
        public bool IsMaximized { get; set; } = false;

        // Layout (Splitters)
        public int MainSplitterDistance { get; set; } = 260;
        public int EditorSplitterDistance { get; set; } = 700;
        public int VerticalSplitterDistance { get; set; } = 400;

        // Filters
        public int LastAssetTypeIndex { get; set; } = 0; // 0=Ach, 1=LB
        public int LastCategoryIndex { get; set; } = 0;  // 0=Core
        public int LastSourceIndex { get; set; } = 0;    // 0=JSON

        // Trigger Editor State
        public bool Trigger_ShowDecimal { get; set; } = false;
        public bool Trigger_ShowAliases { get; set; } = false;
        public bool Trigger_CollapseChains { get; set; } = false;

        // Code Note Editor State
        public bool Notes_ShowLeafsOnly { get; set; } = false;
        public bool Notes_IndentValues { get; set; } = true;
    }

    public static class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(Application.StartupPath, "settings.json");
        public static UserSettings Current { get; private set; } = new UserSettings();

        public static void Load()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    string json = File.ReadAllText(SettingsPath);
                    var loaded = JsonSerializer.Deserialize<UserSettings>(json);
                    if (loaded != null) Current = loaded;
                }
                catch { /* Ignore errors, use defaults */ }
            }
        }

        public static void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Current, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch { /* Ignore errors */ }
        }
    }
}