using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Logic;
using RALogicEditor.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Helpers
{
    public static class CodeNoteModifier
    {
        public enum IndentStyle { Plus, Mixed, Dot, Unicode }
        public enum SeparatorStyle { Pipe, Equals, Colon, Space }

        public class NoteStyle
        {
            public IndentStyle Indentation { get; set; } = IndentStyle.Plus;
            public SeparatorStyle Separator { get; set; } = SeparatorStyle.Space;

            public string GetIndentString(int depth)
            {
                if (depth <= 1) return "";

                if (Indentation == IndentStyle.Unicode)
                {
                    if (depth == 2) return "├─";
                    var sb = new StringBuilder();
                    for (int i = 0; i < depth - 2; i++) sb.Append("│ ");
                    sb.Append("├─");
                    return sb.ToString();
                }

                if (Indentation == IndentStyle.Plus)
                    return new string('+', depth - 1);

                // Both Dot and Mixed just output dots as the structural prefix.
                // The mathematical sign (+ or -) is appended separately by InjectNoteContent.
                return new string('.', depth - 1);
            }

            public string GetBodyIndentString(int depth)
            {
                if (depth <= 1) return "";

                if (Indentation == IndentStyle.Unicode)
                    return new string(' ', (depth - 1) * 2); // Pads cleanly under the ├─ structure

                return new string('.', depth - 1);
            }

            public string GetSeparatorString()
            {
                return Separator switch
                {
                    SeparatorStyle.Pipe => " | ",
                    SeparatorStyle.Equals => " = ",
                    SeparatorStyle.Colon => " : ",
                    _ => " "
                };
            }
        }

        private class StyleStats
        {
            public int PlusCount = 0;
            public int MixedCount = 0;
            public int DotCount = 0;
            public int UnicodeCount = 0;
            public int PipeCount = 0;
            public int EqCount = 0;
            public int ColCount = 0;

            public bool HasIndent => PlusCount > 0 || MixedCount > 0 || DotCount > 0 || UnicodeCount > 0;
            public bool HasSeparator => PipeCount > 0 || EqCount > 0 || ColCount > 0;
        }

        private static bool IsNodeBoundary(string line)
        {
            var match = Regex.Match(line, @"^([.\+\s\u2500-\u257F]*)([-+]?)(0x[0-9A-Fa-f]+)\s*([|=:])?\s*(.*)$", RegexOptions.IgnoreCase);
            if (!match.Success) return false;

            string indentStr = match.Groups[1].Value;
            string sign = match.Groups[2].Value;
            string separator = match.Groups[4].Value;

            if (!string.IsNullOrEmpty(sign)) return true;
            if (indentStr.Contains("+")) return true;
            if (indentStr.Length > 0 && separator == "|") return true;

            return false;
        }

        private static string ExtractExactPrefix(string line)
        {
            var match = Regex.Match(line, @"^([.\+\s\u2500-\u257F]*)([-+]?)(0x[0-9A-Fa-f]+)", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;
            return "";
        }

        public static NoteStyle DetectStyle(string currentNote, List<CodeNote> allNotes)
        {
            var localStats = ScanStats(new[] { currentNote });
            if (localStats.HasIndent || localStats.HasSeparator)
            {
                var style = ResolveStyle(localStats);
                if (!localStats.HasSeparator)
                {
                    var globalSep = DetectGlobalStyle(allNotes).Separator;
                    if (globalSep != SeparatorStyle.Space) style.Separator = globalSep;
                }
                return style;
            }
            var globalStats = ScanStats(allNotes.Select(n => n.Note));
            return ResolveStyle(globalStats);
        }

        public static NoteStyle DetectGlobalStyle(List<CodeNote> allNotes)
        {
            var stats = ScanStats(allNotes.Select(n => n.Note));
            return ResolveStyle(stats);
        }

        private static StyleStats ScanStats(IEnumerable<string> notes)
        {
            var stats = new StyleStats();
            var indentRegex = new Regex(@"^([.\+\s\u2500-\u257F]+)0x", RegexOptions.Multiline);
            var sepRegex = new Regex(@"0x[0-9A-Fa-f]+\s*([|=:])\s*", RegexOptions.Multiline);

            foreach (var noteText in notes)
            {
                if (string.IsNullOrEmpty(noteText)) continue;

                var matches = indentRegex.Matches(noteText);
                foreach (Match m in matches)
                {
                    string indent = m.Groups[1].Value;
                    if (indent.Any(c => c == '│' || c == '├' || c == '└' || c == '┬' || c == '─'))
                    {
                        stats.UnicodeCount++;
                    }
                    else if (indent.Contains("."))
                    {
                        if (indent.Contains("+")) stats.MixedCount++;
                        else stats.DotCount++;
                    }
                    else if (indent.Contains("+"))
                    {
                        stats.PlusCount++;
                    }
                }

                var sepMatches = sepRegex.Matches(noteText);
                foreach (Match m in sepMatches)
                {
                    string s = m.Groups[1].Value;
                    if (s == "|") stats.PipeCount++;
                    else if (s == "=") stats.EqCount++;
                    else if (s == ":") stats.ColCount++;
                }
            }
            return stats;
        }

        private static NoteStyle ResolveStyle(StyleStats stats)
        {
            var style = new NoteStyle();

            if (stats.UnicodeCount > stats.DotCount && stats.UnicodeCount > stats.PlusCount && stats.UnicodeCount > stats.MixedCount) style.Indentation = IndentStyle.Unicode;
            else if (stats.MixedCount > stats.DotCount && stats.MixedCount > stats.PlusCount) style.Indentation = IndentStyle.Mixed;
            else if (stats.DotCount > stats.PlusCount && stats.DotCount > stats.MixedCount) style.Indentation = IndentStyle.Dot;
            else style.Indentation = IndentStyle.Plus;

            if (stats.PipeCount >= stats.EqCount && stats.PipeCount >= stats.ColCount && stats.PipeCount > 0) style.Separator = SeparatorStyle.Pipe;
            else if (stats.EqCount >= stats.PipeCount && stats.EqCount >= stats.ColCount && stats.EqCount > 0) style.Separator = SeparatorStyle.Equals;
            else if (stats.ColCount >= stats.PipeCount && stats.ColCount >= stats.EqCount && stats.ColCount > 0) style.Separator = SeparatorStyle.Colon;
            else style.Separator = SeparatorStyle.Space;

            if (style.Separator == SeparatorStyle.Space && !stats.HasSeparator)
                style.Separator = SeparatorStyle.Pipe;

            return style;
        }

        private static NoteStyle DetectLocalStyle(string parentLine, NoteStyle fallbackStyle)
        {
            var localStyle = new NoteStyle { Separator = fallbackStyle.Separator, Indentation = fallbackStyle.Indentation };

            if (parentLine.Contains("|")) localStyle.Separator = SeparatorStyle.Pipe;
            else if (parentLine.Contains("=")) localStyle.Separator = SeparatorStyle.Equals;
            else if (parentLine.Contains(":")) localStyle.Separator = SeparatorStyle.Colon;

            var match = Regex.Match(parentLine.TrimStart(), @"^([.\+\s\u2500-\u257F]+)");
            if (match.Success)
            {
                string indent = match.Groups[1].Value;
                if (indent.Any(c => c == '│' || c == '├' || c == '└' || c == '┬' || c == '─'))
                {
                    localStyle.Indentation = IndentStyle.Unicode;
                }
                else if (indent.Contains("."))
                {
                    localStyle.Indentation = indent.Contains("+") ? IndentStyle.Mixed : IndentStyle.Dot;
                }
                else if (indent.Contains("+"))
                {
                    if (indent == "+" && fallbackStyle.Indentation == IndentStyle.Mixed)
                    {
                        localStyle.Indentation = IndentStyle.Mixed;
                    }
                    else if (indent.Length > 1 && !indent.Contains("."))
                    {
                        localStyle.Indentation = IndentStyle.Plus;
                    }
                    else
                    {
                        localStyle.Indentation = IndentStyle.Plus;
                    }
                }
            }

            return localStyle;
        }

        public static string DeleteNodeFamily(string baseNoteText, NoteNode node)
        {
            var lines = baseNoteText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            if (node.RawLineIndex >= lines.Count || node.RawLineIndex < 0) return baseNoteText;

            string targetPrefix = ExtractExactPrefix(lines[node.RawLineIndex]);

            int removeCount = 1;
            int idx = node.RawLineIndex + 1;

            while (idx < lines.Count)
            {
                string line = lines[idx];
                if (string.IsNullOrWhiteSpace(line))
                {
                    removeCount++;
                    idx++;
                    continue;
                }

                if (IsNodeBoundary(line))
                {
                    string currentPrefix = ExtractExactPrefix(line);
                    if (currentPrefix.Length <= targetPrefix.Length)
                    {
                        break;
                    }
                }

                removeCount++;
                idx++;
            }

            lines.RemoveRange(node.RawLineIndex, removeCount);

            for (int i = lines.Count - 1; i > 0; i--)
            {
                if (string.IsNullOrWhiteSpace(lines[i]) && string.IsNullOrWhiteSpace(lines[i - 1]))
                    lines.RemoveAt(i);
            }

            return string.Join("\r\n", lines).TrimEnd();
        }

        public static string UpdateRealNode(string baseNoteText, NoteNode node, string newContent, NoteStyle globalStyle)
        {
            var lines = baseNoteText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();

            if (node.RawLineIndex >= lines.Count) return baseNoteText;

            var style = DetectLocalStyle(lines[node.RawLineIndex], globalStyle);
            string originalPrefix = ExtractExactPrefix(lines[node.RawLineIndex]);

            int removeCount = 1;
            int idx = node.RawLineIndex + 1;

            while (idx < lines.Count)
            {
                string line = lines[idx];

                if (string.IsNullOrWhiteSpace(line))
                {
                    idx++; removeCount++; continue;
                }

                if (IsNodeBoundary(line)) break;
                if (line.TrimStart().StartsWith("+0x") || line.TrimStart().StartsWith("-0x")) break;

                idx++;
                removeCount++;
            }

            lines.RemoveRange(node.RawLineIndex, removeCount);

            string offsetHex = node.Offset.TrimStart('+', '-');
            if (!offsetHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) offsetHex = "0x" + offsetHex;
            string sign = node.Offset.StartsWith("-") ? "-" : "+";

            string sep = style.GetSeparatorString();

            string[] contentParts = newContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string title = contentParts.Length > 0 ? contentParts[0] : "";

            string header = $"{originalPrefix}{sign}{offsetHex}{sep}{title}";
            lines.Insert(node.RawLineIndex, header);

            string bodyPadding = new string(' ', originalPrefix.Length + 1);

            for (int k = 1; k < contentParts.Length; k++)
            {
                string bodyLine = contentParts[k].TrimStart();
                lines.Insert(node.RawLineIndex + k, bodyPadding + bodyLine);
            }

            return string.Join("\r\n", lines);
        }

        public static string InjectNoteContent(string baseNoteText, List<long> chainOffsets, string newContent, NoteStyle globalStyle)
        {
            var sb = new StringBuilder(baseNoteText);

            // Add spacing to ensure it breaks cleanly from the previous content
            if (sb.Length > 0 && !baseNoteText.EndsWith("\n")) sb.AppendLine();
            sb.AppendLine();

            for (int i = 0; i < chainOffsets.Count; i++)
            {
                long offset = chainOffsets[i];
                string sign = offset < 0 ? "-" : "+";
                string hex = "0x" + Math.Abs(offset).ToString("x");

                // Depth 1 = Root (no indent), Depth 2 = Child (first indent character)
                string indent = globalStyle.GetIndentString(i + 1);

                if (i == chainOffsets.Count - 1)
                {
                    // Final node in chain
                    string[] contentParts = newContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    string title = contentParts.Length > 0 ? contentParts[0] : "New Node";
                    string sep = globalStyle.GetSeparatorString();

                    sb.AppendLine($"{indent}{sign}{hex}{sep}{title}");

                    // Optional body text below the node header
                    string bodyPadding = globalStyle.GetBodyIndentString(i + 1);
                    for (int k = 1; k < contentParts.Length; k++)
                    {
                        sb.AppendLine($"{bodyPadding}{contentParts[k].TrimStart()}");
                    }
                }
                else
                {
                    // Intermediate path nodes
                    sb.AppendLine($"{indent}{sign}{hex}");
                }
            }

            // Return the raw text exactly as it was appended. 
            // The user can visually review it and hit "Fix Hierarchy" to merge it naturally.
            return sb.ToString().TrimEnd();
        }
    }
}