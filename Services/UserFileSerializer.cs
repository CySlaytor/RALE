using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Services;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace RALogicEditor.Services
{
    public class UserFileSerializer
    {
        public string Serialize(
            string gameTitle,
            UserTextParser.UserData? localData,
            List<RaAchievement> mergedAchievements,
            List<RaLeaderboard> mergedLeaderboards,
            ProjectRepository repo)
        {
            var sb = new StringBuilder();
            sb.AppendLine("1.0");
            sb.AppendLine(gameTitle);

            var lbStrategy = new LeaderboardStrategy();

            // --- HELPER FOR ESCAPING STRINGS (Fixes the Colon Bug) ---
            string EscapeUserString(string input)
            {
                if (string.IsNullOrEmpty(input)) return "";
                // If content has a colon or starts with a quote, it must be wrapped
                if (input.Contains(":") || input.Contains("\""))
                {
                    // Standard RA escape: replace internal quotes with \" and wrap whole string in quotes
                    return "\"" + input.Replace("\"", "\\\"") + "\"";
                }
                return input;
            }

            foreach (var a in mergedAchievements)
            {
                if (a.State == AssetState.Synced) continue; // Don't save synced items to User.txt

                // APPLY ESCAPE TO TITLE AND DESCRIPTION
                sb.AppendLine($"{a.ID}:\"{a.Logic}\":{EscapeUserString(a.Title)}:{EscapeUserString(a.Description)}:::{a.Type}:{a.Author}:{a.Points}:::::{a.BadgeName}");
            }

            foreach (var l in mergedLeaderboards)
            {
                if (l.State == AssetState.Synced) continue;

                // Use strategy to ensure we get clean segments for the file format
                var segments = lbStrategy.ParseLogic(l.Logic);

                string GetSeg(string key)
                {
                    if (!segments.ContainsKey(key)) return "";
                    var groups = segments[key];
                    StringBuilder segSb = new StringBuilder();
                    for (int i = 0; i < groups.Count; i++)
                    {
                        if (i > 0) segSb.Append('S');
                        for (int c = 0; c < groups[i].Conditions.Count; c++)
                        {
                            if (c > 0) segSb.Append('_');
                            segSb.Append(LogicFormatter.ConditionToString(groups[i].Conditions[c]));
                        }
                    }
                    return segSb.ToString();
                }

                // APPLY ESCAPE TO TITLE AND DESCRIPTION
                sb.AppendLine($"L{l.ID}:\"{GetSeg("Start")}\":\"{GetSeg("Cancel")}\":\"{GetSeg("Submit")}\":\"{GetSeg("Value")}\":{l.Format}:{EscapeUserString(l.Title)}:{EscapeUserString(l.Description)}:{l.LowerIsBetter}");
            }

            // Save notes - Filter out notes that match the server data
            if (localData?.Notes != null)
            {
                foreach (var n in localData.Notes)
                {
                    // Only save notes that are modified or new compared to the server JSON
                    if (repo.IsNoteSynced(n)) continue;

                    sb.AppendLine($"N0:{n.Address}:\"{n.Note.Replace("\r\n", "\\r\\n")}\"");
                }
            }

            return sb.ToString();
        }
    }
}