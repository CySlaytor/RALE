using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace RALogicEditor.Helpers
{
    public class RecentFilesManager
    {
        private const int MaxFiles = 5;
        // Saves to bin/Debug or bin/Release
        private readonly string FilePath = Path.Combine(Application.StartupPath, "recent.txt");

        public List<string> Files { get; private set; } = new();

        public void Load()
        {
            if (File.Exists(FilePath))
            {
                Files = File.ReadAllLines(FilePath).Where(File.Exists).ToList();
            }
        }

        public void Add(string path)
        {
            // Remove if already exists to move it to top
            Files.RemoveAll(f => f.Equals(path, StringComparison.OrdinalIgnoreCase));

            Files.Insert(0, path);

            if (Files.Count > MaxFiles)
            {
                Files.RemoveAt(Files.Count - 1);
            }

            try
            {
                File.WriteAllLines(FilePath, Files);
            }
            catch { /* Ignore write errors */ }
        }
    }
}