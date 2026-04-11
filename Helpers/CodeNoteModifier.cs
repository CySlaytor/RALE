using RALogicEditor.Models;
using RALogicEditor.Parsers;
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
                if (depth < 1) return "";

                // Fully support the Unicode drawing logic
                if (Indentation == IndentStyle.Unicode)
                {
                    if (depth == 1) return "├─";
                    return new string('│', depth - 1) + "├─";
                }

                if (Indentation == IndentStyle.Plus)
                    return new string('+', depth);

                if (Indentation == IndentStyle.Dot)
                    return new string('.', depth);

                // Mixed: Depth 1 is '+', Depth > 1 starts with dots and ends with +
                // Example: +0x10 -> .+0x10 -> ..+0x10
                if (Indentation == IndentStyle.Mixed)
                {
                    if (depth == 1) return "+";
                    return new string('.', depth - 1) + "+";
                }

                return new string('+', depth);
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

        // Detects style based on the current note first, falling back to global stats if needed.
        public static NoteStyle DetectStyle(string currentNote, List<CodeNote> allNotes)
        {
            // 1. Prioritize the current note's style if it has explicit formatting
            var localStats = ScanStats(new[] { currentNote });

            // If we found relevant styling in the current note, use it.
            if (localStats.HasIndent || localStats.HasSeparator)
            {
                var style = ResolveStyle(localStats);

                // If local lacked separator info, try to fill from global
                if (!localStats.HasSeparator)
                {
                    var globalSep = DetectGlobalStyle(allNotes).Separator;
                    // Only apply if it's not Space (default)
                    if (globalSep != SeparatorStyle.Space) style.Separator = globalSep;
                }

                return style;
            }

            // 2. Fallback to global average
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

            // Expanded regex to catch Unicode box drawing characters as well as . and +
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

            // Priority check for Unicode
            if (stats.UnicodeCount > stats.DotCount && stats.UnicodeCount > stats.PlusCount && stats.UnicodeCount > stats.MixedCount) style.Indentation = IndentStyle.Unicode;
            else if (stats.MixedCount > stats.DotCount && stats.MixedCount > stats.PlusCount) style.Indentation = IndentStyle.Mixed;
            else if (stats.DotCount > stats.PlusCount && stats.DotCount > stats.MixedCount) style.Indentation = IndentStyle.Dot;
            else style.Indentation = IndentStyle.Plus;

            if (stats.PipeCount >= stats.EqCount && stats.PipeCount >= stats.ColCount && stats.PipeCount > 0) style.Separator = SeparatorStyle.Pipe;
            else if (stats.EqCount >= stats.PipeCount && stats.EqCount >= stats.ColCount && stats.EqCount > 0) style.Separator = SeparatorStyle.Equals;
            else if (stats.ColCount >= stats.PipeCount && stats.ColCount >= stats.EqCount && stats.ColCount > 0) style.Separator = SeparatorStyle.Colon;
            else style.Separator = SeparatorStyle.Space;

            if (style.Separator == SeparatorStyle.Space && !stats.HasSeparator)
                style.Separator = SeparatorStyle.Pipe; // Default preference

            return style;
        }

        // Helper to infer style from a specific parent node text, overriding defaults
        // This ensures that if we are inserting into a note that uses dots, we use dots, regardless of other settings.
        private static NoteStyle DetectLocalStyle(string parentLine, NoteStyle fallbackStyle)
        {
            var localStyle = new NoteStyle { Separator = fallbackStyle.Separator, Indentation = fallbackStyle.Indentation };

            // Infer Separator from specific line
            if (parentLine.Contains("|")) localStyle.Separator = SeparatorStyle.Pipe;
            else if (parentLine.Contains("=")) localStyle.Separator = SeparatorStyle.Equals;
            else if (parentLine.Contains(":")) localStyle.Separator = SeparatorStyle.Colon;

            // Infer Indentation Pattern from specific line
            // Regex matches start of line like "  +0x" or ".+0x" or "├─0x"
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
                    // Explicit dots
                    localStyle.Indentation = indent.Contains("+") ? IndentStyle.Mixed : IndentStyle.Dot;
                }
                else if (indent.Contains("+"))
                {
                    // If indentation is exactly "+", it is technically compatible with both Plus and Mixed (depth 1).
                    // If fallback is Mixed, preserve Mixed to ensure children get "." (e.g. ".+")
                    if (indent == "+" && fallbackStyle.Indentation == IndentStyle.Mixed)
                    {
                        localStyle.Indentation = IndentStyle.Mixed;
                    }
                    else if (indent.Length > 1 && !indent.Contains("."))
                    {
                        // "++", "+++" -> Definitely Plus
                        localStyle.Indentation = IndentStyle.Plus;
                    }
                    else
                    {
                        // Default to Plus if ambiguous and fallback isn't Mixed
                        localStyle.Indentation = IndentStyle.Plus;
                    }
                }
            }

            return localStyle;
        }

        // --- NEW: INTELLIGENT DELETION ---
        public static string DeleteNodeFamily(string baseNoteText, NoteNode node)
        {
            var lines = baseNoteText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            if (node.RawLineIndex >= lines.Count || node.RawLineIndex < 0) return baseNoteText;

            // To measure indent reliably, we look at the raw string prefix
            string targetPrefix = Regex.Match(lines[node.RawLineIndex], @"^([.\+\s\u2500-\u257F]*)").Groups[1].Value;

            int removeCount = 1;
            int idx = node.RawLineIndex + 1;
            var offsetRegex = new Regex(@"^([.\+\s\u2500-\u257F]*)([-+]?)(0x[0-9A-Fa-f]+)", RegexOptions.IgnoreCase);

            while (idx < lines.Count)
            {
                string line = lines[idx];
                if (string.IsNullOrWhiteSpace(line))
                {
                    removeCount++;
                    idx++;
                    continue;
                }

                var match = offsetRegex.Match(line);
                if (match.Success)
                {
                    string currentPrefix = match.Groups[1].Value;
                    string sign = match.Groups[2].Value;

                    bool isStructural = !string.IsNullOrEmpty(sign) || currentPrefix.Contains("+") || currentPrefix.Contains(".") || currentPrefix.Any(c => c >= '\u2500' && c <= '\u257F');

                    // If current line is a structural node with an indentation level LESS THAN OR EQUAL to our target,
                    // we have reached the next sibling or an uncle node. We stop deleting.
                    if (isStructural && currentPrefix.Length <= targetPrefix.Length)
                    {
                        break;
                    }
                }

                // If it's a child offset or body text, include it in deletion
                removeCount++;
                idx++;
            }

            lines.RemoveRange(node.RawLineIndex, removeCount);

            // Cleanup double blank lines that might result from the gap
            for (int i = lines.Count - 1; i > 0; i--)
            {
                if (string.IsNullOrWhiteSpace(lines[i]) && string.IsNullOrWhiteSpace(lines[i - 1]))
                    lines.RemoveAt(i);
            }

            return string.Join("\r\n", lines).TrimEnd();
        }

        // Updates a specific EXISTING node in the tree with new content
        // Used when editing a real node to prevent data loss in the rest of the file
        public static string UpdateRealNode(string baseNoteText, NoteNode node, string newContent, NoteStyle globalStyle)
        {
            var lines = baseNoteText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();

            if (node.RawLineIndex >= lines.Count) return baseNoteText; // Safety check

            // Detect style from the line itself to preserve indentation format of the node being edited
            var style = DetectLocalStyle(lines[node.RawLineIndex], globalStyle);

            // Calculate depth based on indent level. 
            // Level 0 = Depth 1 (e.g. +), Level 1 = Depth 2 (e.g. ++)
            int depth = node.IndentLevel + 1;

            ReplaceNodeContent(lines, node, newContent, style, depth);

            return string.Join("\r\n", lines);
        }

        public static string InjectNoteContent(string baseNoteText, List<long> chainOffsets, string newContent, NoteStyle globalStyle)
        {
            var parser = new PointerTreeParser();
            var rootNodes = parser.ParseNoteText(baseNoteText);

            // Split into lines for insertion/replacement
            var lines = baseNoteText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();

            // Traversal State
            NoteNode? currentNode = null;
            int currentDepth = 0;

            for (int i = 0; i < chainOffsets.Count; i++)
            {
                long targetOffset = chainOffsets[i];
                currentDepth++;

                // 1. Try to find exact child match first
                NoteNode? child = null;
                if (currentNode == null)
                {
                    // Level 0 nodes (Children of the logical root).
                    child = rootNodes.FirstOrDefault(n => n.IndentLevel == 0 && ParseOffset(n.Offset) == targetOffset);
                }
                else
                {
                    // Standard child lookup
                    child = currentNode.Children.FirstOrDefault(c => ParseOffset(c.Offset) == targetOffset);
                }

                // 2. Range Coverage Check (The fix for Arrays/Structs)
                // If exact match failed, check if a sibling node "covers" this offset range
                if (child == null)
                {
                    IEnumerable<NoteNode> candidates = (currentNode == null)
                        ? rootNodes.Where(n => n.IndentLevel == 0)
                        : currentNode.Children;

                    child = candidates.FirstOrDefault(c => {
                        long start = ParseOffset(c.Offset);
                        return targetOffset >= start && targetOffset < start + c.Size;
                    });
                }

                // Determine Style for this insertion level.
                NoteStyle effectiveStyle = globalStyle;
                bool styleDetected = false;

                // Check Siblings
                IEnumerable<NoteNode> siblings = (currentNode == null)
                    ? rootNodes.Where(n => n.IndentLevel == 0)
                    : currentNode.Children;

                var lastSibling = siblings.LastOrDefault();
                if (lastSibling != null && lastSibling.RawLineIndex < lines.Count)
                {
                    effectiveStyle = DetectLocalStyle(lines[lastSibling.RawLineIndex], globalStyle);
                    styleDetected = true;
                }

                // Check Parent
                if (!styleDetected && currentNode != null && currentNode.RawLineIndex < lines.Count)
                {
                    effectiveStyle = DetectLocalStyle(lines[currentNode.RawLineIndex], globalStyle);
                }

                if (child != null)
                {
                    // Node exists (or is covered), traverse
                    currentNode = child;

                    if (i == chainOffsets.Count - 1)
                    {
                        long childOffset = ParseOffset(child.Offset);
                        if (childOffset == targetOffset)
                        {
                            // Strict Match - Update Existing Node
                            var childStyle = DetectLocalStyle(lines[child.RawLineIndex], effectiveStyle);
                            ReplaceNodeContent(lines, child, newContent, childStyle, child.IndentLevel + 1);
                        }
                        else
                        {
                            // Range Match - Insert as New Child of the Range Coverage Node
                            int insertIndex = GetInsertionIndex(lines, child, rootNodes);
                            int insertionDepth = child.IndentLevel + 2;
                            string indent = effectiveStyle.GetIndentString(insertionDepth);
                            string offsetHex = "0x" + targetOffset.ToString("x");
                            if (targetOffset < 0) offsetHex = "-0x" + Math.Abs(targetOffset).ToString("x");

                            string[] contentParts = newContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            string title = contentParts.Length > 0 ? contentParts[0] : "New Node";
                            string sep = effectiveStyle.GetSeparatorString();
                            string header = $"{indent}{offsetHex}{sep}{title}";

                            lines.Insert(insertIndex, header);

                            for (int k = 1; k < contentParts.Length; k++)
                            {
                                lines.Insert(insertIndex + k, contentParts[k]);
                            }
                        }
                    }
                }
                else
                {
                    // Node missing entirely, create it
                    int insertIndex = GetInsertionIndex(lines, currentNode, rootNodes);

                    string offsetHex = "0x" + targetOffset.ToString("x");
                    if (targetOffset < 0) offsetHex = "-0x" + Math.Abs(targetOffset).ToString("x");

                    int insertionDepth = (currentNode == null) ? 1 : currentNode.IndentLevel + 2;

                    string indent = effectiveStyle.GetIndentString(insertionDepth);

                    if (i == chainOffsets.Count - 1)
                    {
                        // Target Node: Insert Header + Body
                        string[] contentParts = newContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        string title = contentParts.Length > 0 ? contentParts[0] : "New Node";
                        string sep = effectiveStyle.GetSeparatorString();
                        string header = $"{indent}{offsetHex}{sep}{title}";

                        lines.Insert(insertIndex, header);

                        // Insert remaining lines as body
                        for (int k = 1; k < contentParts.Length; k++)
                        {
                            lines.Insert(insertIndex + k, contentParts[k]);
                        }
                    }
                    else
                    {
                        // Intermediate Node: Just Header
                        lines.Insert(insertIndex, $"{indent}{offsetHex}");

                        // Fake node to continue traversal context
                        var newNode = new NoteNode
                        {
                            RawLineIndex = insertIndex,
                            IndentLevel = insertionDepth - 1, // Parser uses 0-based
                            Children = new List<NoteNode>()
                        };

                        if (currentNode != null)
                        {
                            currentNode.Children.Add(newNode);
                        }
                        else
                        {
                            rootNodes.Add(newNode);
                        }
                        currentNode = newNode;
                    }
                }
            }

            return string.Join("\r\n", lines);
        }

        private static void ReplaceNodeContent(List<string> lines, NoteNode node, string newContent, NoteStyle style, int depth)
        {
            // Determine range to replace (Header + Body lines)
            int removeCount = 1;
            int idx = node.RawLineIndex + 1;

            var nodeStructureRegex = new Regex(@"^\s*[.\+\u2500-\u257F]+0x", RegexOptions.IgnoreCase);

            while (idx < lines.Count)
            {
                string line = lines[idx];

                // If blank line, assume part of content
                if (string.IsNullOrWhiteSpace(line))
                {
                    idx++; removeCount++; continue;
                }

                // If we hit a line that starts with indent AND 0x, it's a new structural node -> Stop removing
                if (nodeStructureRegex.IsMatch(line)) break;

                // Safety: If we hit something that looks like a root pointer "+0x...", stop
                if (line.TrimStart().StartsWith("+0x")) break;

                idx++;
                removeCount++;
            }

            lines.RemoveRange(node.RawLineIndex, removeCount);

            // Rebuild Header
            string offsetHex = node.Offset.TrimStart('+', '.');
            if (!offsetHex.StartsWith("-") && !offsetHex.StartsWith("0x")) offsetHex = "0x" + offsetHex;

            string indent = style.GetIndentString(depth);

            string[] contentParts = newContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string title = contentParts.Length > 0 ? contentParts[0] : "";

            string sep = style.GetSeparatorString();
            string header = $"{indent}{offsetHex}{sep}{title}";

            lines.Insert(node.RawLineIndex, header);
            for (int k = 1; k < contentParts.Length; k++)
            {
                lines.Insert(node.RawLineIndex + k, contentParts[k]);
            }
        }

        private static int GetInsertionIndex(List<string> lines, NoteNode? parent, List<NoteNode> allNodes)
        {
            // If parent is null, we are inserting at Top Level (Level 0).
            if (parent == null)
            {
                var lastLevel0 = allNodes.LastOrDefault(n => n.IndentLevel == 0);
                if (lastLevel0 == null)
                {
                    return lines.Count;
                }
                var target = GetLastDescendant(lastLevel0);
                return GetEndOfNodeIndex(lines, target);
            }
            else
            {
                // Insert as last child of parent
                NoteNode target = parent;
                if (parent.Children.Count > 0)
                {
                    var lastChild = parent.Children.Last();
                    target = GetLastDescendant(lastChild);
                }
                return GetEndOfNodeIndex(lines, target);
            }
        }

        private static int GetEndOfNodeIndex(List<string> lines, NoteNode node)
        {
            // Start searching *after* the node's header line
            int idx = node.RawLineIndex + 1;

            var nodeStructureRegex = new Regex(@"^\s*[.\+\u2500-\u257F]+0x", RegexOptions.IgnoreCase);

            while (idx < lines.Count)
            {
                string line = lines[idx];
                if (string.IsNullOrWhiteSpace(line)) { idx++; continue; }

                // If we hit a line that looks like a node, we stop.
                if (nodeStructureRegex.IsMatch(line)) return idx;
                if (line.TrimStart().StartsWith("+0x")) return idx;

                idx++;
            }
            return idx;
        }

        private static NoteNode GetLastDescendant(NoteNode node)
        {
            if (node.Children.Count == 0) return node;
            return GetLastDescendant(node.Children.Last());
        }

        private static long ParseOffset(string s)
        {
            string clean = s.Trim().Replace("+", "").Replace("0x", "");
            if (long.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val))
            {
                if (s.Contains("-")) return -val;
                return val;
            }
            return 0;
        }
    }
}