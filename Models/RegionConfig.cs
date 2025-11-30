using RALogicEditor.Models;
using System.Collections.Generic;

namespace RALogicEditor.Models
{
    public class RegionConfig
    {
        public string RegionName { get; set; } = "";
        public bool IsBase { get; set; }
        public Dictionary<string, string> AddressMap { get; set; } = new();
        public List<AddressRangeMap> AddressRanges { get; set; } = new();
        public List<AchievementCondition> LockConditions { get; set; } = new();
    }
}