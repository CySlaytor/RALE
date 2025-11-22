using RALogicEditor.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace RALogicEditor.Parsers
{
    public static class UserTextParser
    {
        public class UserData
        {
            public List<RaAchievement> Achievements { get; set; } = new();
            public List<RaLeaderboard> Leaderboards { get; set; } = new();
            public List<CodeNote> Notes { get; set; } = new();
        }

        public static UserData ParseUserFile(string filepath)
        {
            var data = new UserData();
            string[] lines;
            try { lines = File.ReadAllLines(filepath); } catch { return data; }

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    if (line.StartsWith("N0:"))
                    {
                        var match = Regex.Match(line, "^N0:(0x[0-9A-Fa-f]+):\"(.+)\"$");
                        if (match.Success)
                        {
                            data.Notes.Add(new CodeNote
                            {
                                Address = match.Groups[1].Value,
                                Note = match.Groups[2].Value.Replace("\\r\\n", "\r\n"),
                                User = "Local"
                            });
                        }
                    }
                    else if (line.StartsWith("L"))
                    {
                        int firstColon = line.IndexOf(':');
                        if (firstColon == -1) continue;
                        string idStr = line.Substring(1, firstColon - 1);
                        if (!int.TryParse(idStr, out int id)) continue;

                        string content = line.Substring(firstColon + 1);
                        var parts = ParseColonLine(content);

                        if (parts.Count >= 8)
                        {
                            data.Leaderboards.Add(new RaLeaderboard
                            {
                                ID = id,
                                Logic = $"STA:{parts[0]}::CAN:{parts[1]}::SUB:{parts[2]}::VAL:{parts[3]}",
                                Format = parts[4],
                                Title = parts[5],
                                Description = parts[6],
                                LowerIsBetter = int.TryParse(parts[7], out int lbVal) ? lbVal : 0,
                                State = AssetState.Local
                            });
                        }
                    }
                    else if (char.IsDigit(line[0]))
                    {
                        int firstColon = line.IndexOf(':');
                        if (firstColon == -1) continue;
                        string idStr = line.Substring(0, firstColon);
                        if (!int.TryParse(idStr, out int id)) continue;

                        string content = line.Substring(firstColon + 1);
                        var parts = ParseColonLine(content);

                        if (parts.Count >= 3)
                        {
                            var ach = new RaAchievement
                            {
                                ID = id,
                                Logic = parts[0],
                                Title = parts[1],
                                Description = parts[2],
                                State = AssetState.Local
                            };
                            if (parts.Count > 5) ach.Type = parts[5];
                            if (parts.Count > 6) ach.Author = parts[6];
                            if (parts.Count > 7 && int.TryParse(parts[7], out int pts)) ach.Points = pts;
                            if (parts.Count > 12) ach.BadgeName = parts[12];
                            data.Achievements.Add(ach);
                        }
                    }
                }
                catch { continue; }
            }
            return data;
        }

        private static List<string> ParseColonLine(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(line)) return result;
            bool inQuote = false;
            int start = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"') inQuote = !inQuote;
                else if (line[i] == ':' && !inQuote)
                {
                    result.Add(CleanValue(line.Substring(start, i - start)));
                    start = i + 1;
                }
            }
            if (start <= line.Length) result.Add(CleanValue(line.Substring(start)));
            return result;
        }

        private static string CleanValue(string val)
        {
            if (string.IsNullOrEmpty(val)) return "";
            if (val.StartsWith("\"") && val.EndsWith("\"") && val.Length >= 2)
                return val.Substring(1, val.Length - 2).Replace("\\\"", "\"");
            return val;
        }
    }
}