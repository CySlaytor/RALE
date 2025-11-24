using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Services
{
    // Interface to abstract differences between Achievement logic (Core/Alts) 
    // and Leaderboard logic (Start/Cancel/Submit/Value)
    public interface IAssetStrategy
    {
        bool IsLeaderboard { get; }

        // Returns the list of segment names (e.g. "Core" for Ach, "Start", "Cancel"... for LB)
        string[] GetSegmentNames();

        // Parses the raw logic string into a dictionary of segment names -> condition groups
        Dictionary<string, List<AchievementConditionGroup>> ParseLogic(string logic);

        // Rebuilds the full logic string from the segments
        string BuildLogic(Dictionary<string, List<AchievementConditionGroup>> segments);
    }

    public class AchievementStrategy : IAssetStrategy
    {
        public bool IsLeaderboard => false;

        public string[] GetSegmentNames() => new[] { "Core" };

        public Dictionary<string, List<AchievementConditionGroup>> ParseLogic(string logic)
        {
            // Achievements are parsed as a single list of groups (Core + Alts)
            // We map this entire list to the "Core" segment key for consistency in the ViewModel
            var groups = AchievementParser.ParseAchievementTrigger(logic, 'S', false);

            // Ensure at least one group exists
            if (!groups.Any())
                groups.Add(new AchievementConditionGroup { GroupName = "Core" });

            return new Dictionary<string, List<AchievementConditionGroup>>
            {
                { "Core", groups }
            };
        }

        public string BuildLogic(Dictionary<string, List<AchievementConditionGroup>> segments)
        {
            if (!segments.ContainsKey("Core")) return "";
            var groups = segments["Core"];

            // Rebuild string using 'S' separator
            var sb = new StringBuilder();
            for (int i = 0; i < groups.Count; i++)
            {
                if (i > 0) sb.Append('S');
                var group = groups[i];
                for (int c = 0; c < group.Conditions.Count; c++)
                {
                    if (c > 0) sb.Append('_');
                    sb.Append(LogicFormatter.ConditionToString(group.Conditions[c]));
                }
            }
            return sb.ToString();
        }
    }

    public class LeaderboardStrategy : IAssetStrategy
    {
        public bool IsLeaderboard => true;

        public string[] GetSegmentNames() => new[] { "Start", "Cancel", "Submit", "Value" };

        public Dictionary<string, List<AchievementConditionGroup>> ParseLogic(string logic)
        {
            var segments = new Dictionary<string, List<AchievementConditionGroup>>
            {
                { "Start", new() }, { "Cancel", new() }, { "Submit", new() }, { "Value", new() }
            };

            if (string.IsNullOrEmpty(logic))
            {
                // Ensure default empty groups
                foreach (var key in segments.Keys)
                    segments[key].Add(new AchievementConditionGroup { GroupName = "Core" });
                return segments;
            }

            string[] parts = Regex.Split(logic, "::");

            foreach (var part in parts)
            {
                string typeKey = "";
                string cleanLogic = part;

                if (part.StartsWith("STA:")) { typeKey = "Start"; cleanLogic = part.Substring(4); }
                else if (part.StartsWith("CAN:")) { typeKey = "Cancel"; cleanLogic = part.Substring(4); }
                else if (part.StartsWith("SUB:")) { typeKey = "Submit"; cleanLogic = part.Substring(4); }
                else if (part.StartsWith("VAL:")) { typeKey = "Value"; cleanLogic = part.Substring(4); }

                if (!string.IsNullOrEmpty(typeKey))
                {
                    var groups = AchievementParser.ParseAchievementTrigger(cleanLogic, 'S', false);
                    if (!groups.Any()) groups.Add(new AchievementConditionGroup { GroupName = "Core" });
                    segments[typeKey] = groups;
                }
            }

            // Ensure defaults for missing segments
            foreach (var key in segments.Keys)
            {
                if (segments[key].Count == 0)
                    segments[key].Add(new AchievementConditionGroup { GroupName = "Core" });
            }

            return segments;
        }

        public string BuildLogic(Dictionary<string, List<AchievementConditionGroup>> segments)
        {
            var sb = new StringBuilder();

            // Helper to build a segment string
            string BuildSegment(List<AchievementConditionGroup>? groups)
            {
                if (groups == null || groups.Count == 0) return "";
                var segSb = new StringBuilder();
                for (int i = 0; i < groups.Count; i++)
                {
                    if (i > 0) segSb.Append('S');
                    var group = groups[i];
                    for (int c = 0; c < group.Conditions.Count; c++)
                    {
                        if (c > 0) segSb.Append('_');
                        segSb.Append(LogicFormatter.ConditionToString(group.Conditions[c]));
                    }
                }
                return segSb.ToString();
            }

            sb.Append("STA:").Append(BuildSegment(segments.GetValueOrDefault("Start"))).Append("::");
            sb.Append("CAN:").Append(BuildSegment(segments.GetValueOrDefault("Cancel"))).Append("::");
            sb.Append("SUB:").Append(BuildSegment(segments.GetValueOrDefault("Submit"))).Append("::");
            sb.Append("VAL:").Append(BuildSegment(segments.GetValueOrDefault("Value")));

            return sb.ToString();
        }
    }
}