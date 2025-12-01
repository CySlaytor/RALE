using RALogicEditor.Models;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace RALogicEditor.Services
{
    // Persists backup metadata for Rich Presence to allow reverting changes
    public class RichPresenceMetadataService
    {
        private string _path = "";

        public class RichPresenceBackup
        {
            public string OriginalScript { get; set; } = "";
            public string Timestamp { get; set; } = "";
        }

        public RichPresenceBackup? CurrentBackup { get; private set; }

        public void Load(string gameId)
        {
            string folder = Path.Combine(Application.StartupPath, "Metadata");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            _path = Path.Combine(folder, $"{gameId}-Rich.json");

            if (File.Exists(_path))
            {
                try
                {
                    string json = File.ReadAllText(_path);
                    CurrentBackup = JsonSerializer.Deserialize<RichPresenceBackup>(json);
                }
                catch { CurrentBackup = null; }
            }
            else
            {
                CurrentBackup = null;
            }
        }

        public void SaveBackup(string script)
        {
            if (string.IsNullOrEmpty(_path)) return;

            var backup = new RichPresenceBackup
            {
                OriginalScript = script,
                Timestamp = System.DateTime.Now.ToString("g")
            };

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(backup, options);
                File.WriteAllText(_path, json);
                CurrentBackup = backup;
            }
            catch { }
        }
    }
}