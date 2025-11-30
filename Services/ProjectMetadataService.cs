using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace RALogicEditor.Services
{
    public class ProjectMetadataService
    {
        private string _metadataPath = "";
        public ProjectMetadata CurrentData { get; private set; } = new();

        public void Load(string gameId)
        {
            string folder = Path.Combine(Application.StartupPath, "Metadata");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            _metadataPath = Path.Combine(folder, $"{gameId}.json");

            if (File.Exists(_metadataPath))
            {
                try
                {
                    string json = File.ReadAllText(_metadataPath);
                    CurrentData = JsonSerializer.Deserialize<ProjectMetadata>(json) ?? new ProjectMetadata();
                }
                catch { CurrentData = new ProjectMetadata(); }
            }
            else
            {
                CurrentData = new ProjectMetadata();
            }
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(_metadataPath)) return;
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(CurrentData, options);
                File.WriteAllText(_metadataPath, json);
            }
            catch { }
        }

        public void UpdateAssetGroups(int assetId, List<AchievementConditionGroup> groups)
        {
            var names = new List<string>();
            foreach (var g in groups) names.Add(g.GroupName);
            CurrentData.AssetGroupNames[assetId] = names;
        }

        public void ApplyNamesToAsset(int assetId, List<AchievementConditionGroup> groups)
        {
            if (CurrentData.AssetGroupNames.TryGetValue(assetId, out var names))
            {
                // We only apply names if counts match or reasonable heuristic
                for (int i = 0; i < Math.Min(groups.Count, names.Count); i++)
                {
                    groups[i].GroupName = names[i];
                }
            }
        }
    }
}