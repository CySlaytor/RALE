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

            // --- HELPER FOR ESCAPING STRINGS (Fixes the Colon/Backslash Bug) ---
            string EscapeUserString(string input)
            {
                if (string.IsNullOrEmpty(input)) return "";

                // If content has a colon, quote, or backslash, it must be escaped/wrapped
                if (input.Contains(":") || input.Contains("\"") || input.Contains("\\"))
                {
                    // 1. Escape backslashes first ( \ -> \\ ) to prevents paths like "local\img" becoming "local\\img" literals
                    // 2. Escape quotes ( " -> \" )
                    // 3. Wrap the entire string in quotes
                    string escaped = input.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    return "\"" + escaped + "\"";
                }
                return input;
            }

            foreach (var a in mergedAchievements)
            {
                if (a.State == AssetState.Synced) continue; // Don't save synced items to User.txt

                // Sanitize BadgeName to prevent multiline corruption
                string cleanBadge = (a.BadgeName ?? "00000").Replace("\r", "").Replace("\n", "").Trim();

                // APPLY ESCAPE TO TITLE, DESCRIPTION AND BADGE
                sb.AppendLine($"{a.ID}:\"{a.Logic}\":{EscapeUserString(a.Title)}:{EscapeUserString(a.Description)}:::{a.Type}:{a.Author}:{a.Points}:::::{EscapeUserString(cleanBadge)}");
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

                // APPLY ESCAPE TO TITLE AND DESCRIPTION. Convert boolean to 1/0 for integration compatibility.
                sb.AppendLine($"L{l.ID}:\"{GetSeg("Start")}\":\"{GetSeg("Cancel")}\":\"{GetSeg("Submit")}\":\"{GetSeg("Value")}\":{l.Format}:{EscapeUserString(l.Title)}:{EscapeUserString(l.Description)}:{(l.LowerIsBetter ? "1" : "0")}");
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
