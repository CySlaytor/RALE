#nullable enable
using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace RALogicEditor.Helpers
{
    public class UserSettings
    {
        public bool ShowLogicReviewer { get; set; } = false; // Default to hidden
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
                string json = JsonSerializer.Serialize(Current);
                File.WriteAllText(SettingsPath, json);
            }
            catch { /* Ignore errors */ }
        }
    }
}