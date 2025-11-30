using System.Collections.Generic;

namespace RALogicEditor.Models
{
    public class ProjectMetadata
    {
        // Key: Asset ID, Value: List of Group Names (Core, Alt 1 (USA)...)
        public Dictionary<int, List<string>> AssetGroupNames { get; set; } = new();
    }
}