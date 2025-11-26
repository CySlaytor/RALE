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
        public enum IndentStyle { Plus, Mixed, Dot }
        public enum SeparatorStyle { Pipe, Equals, Colon, Space }

        public class NoteStyle
        {
            public IndentStyle Indentation { get; set; } = IndentStyle.Plus;
            public SeparatorStyle Separator { get; set; } = SeparatorStyle.Space;

            public string GetIndentString(int depth)
            {
                if (depth < 1) return "";

                if (Indentation == IndentStyle.Plus)
                    return new string('+', depth);

                if (Indentation == IndentStyle.Dot)
                    return new string('.', depth);

                // Mixed: Depth 1 is '+', Depth > 1 starts with dots and ends with +
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

        public static NoteStyle DetectGlobalStyle(List<CodeNote> allNotes)
        {
            var style = new NoteStyle();
            int plusCount = 0;
            int mixedCount = 0;
            int dotCount = 0;

            int pipeCount = 0;
            int eqCount = 0;
            int colCount = 0;

            var indentRegex = new Regex(@"^([.+]+)0x", RegexOptions.Multiline);
            var sepRegex = new Regex(@"0x[0-9A-Fa-f]+\s*([|=:])\s*", RegexOptions.Multiline);

            foreach (var note in allNotes)
            {
                var matches = indentRegex.Matches(note.Note);
                foreach (Match m in matches)
                {
                    string indent = m.Groups[1].Value;
                    if (indent.Contains("."))
                    {
                        if (indent.Contains("+")) mixedCount++;
                        else dotCount++;
                    }
                    else if (indent.Contains("+"))
                    {
                        plusCount++;
                    }
                }

                var sepMatches = sepRegex.Matches(note.Note);
                foreach (Match m in sepMatches)
                {
                    string s = m.Groups[1].Value;
                    if (s == "|") pipeCount++;
                    else if (s == "=") eqCount++;
                    else if (s == ":") colCount++;
                }
            }

            if (mixedCount > dotCount && mixedCount > plusCount) style.Indentation = IndentStyle.Mixed;
            else if (dotCount > plusCount && dotCount > mixedCount) style.Indentation = IndentStyle.Dot;
            else style.Indentation = IndentStyle.Plus;

            if (pipeCount >= eqCount && pipeCount >= colCount && pipeCount > 0) style.Separator = SeparatorStyle.Pipe;
            else if (eqCount >= pipeCount && eqCount >= colCount && eqCount > 0) style.Separator = SeparatorStyle.Equals;
            else if (colCount >= pipeCount && colCount >= eqCount && colCount > 0) style.Separator = SeparatorStyle.Colon;
            else style.Separator = SeparatorStyle.Space;

            if (style.Separator == SeparatorStyle.Space && pipeCount == 0 && eqCount == 0 && colCount == 0)
                style.Separator = SeparatorStyle.Pipe;

            return style;
        }

        public static string InjectNoteContent(string baseNoteText, List<long> chainOffsets, string newContent, NoteStyle style)
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

                // FIX: Correctly locate child node depending on if we are at root or inside a node
                NoteNode? child = null;
                if (currentNode == null)
                {
                    // Level 0 nodes (Children of the logical root).
                    // rootNodes[0] is metadata root. Iterate all to find Level 0.
                    child = rootNodes.FirstOrDefault(n => n.IndentLevel == 0 && ParseOffset(n.Offset) == targetOffset);
                }
                else
                {
                    // Standard child lookup
                    child = currentNode.Children.FirstOrDefault(c => ParseOffset(c.Offset) == targetOffset);
                }

                if (child != null)
                {
                    // Node exists, traverse
                    currentNode = child;

                    if (i == chainOffsets.Count - 1)
                    {
                        ReplaceNodeContent(lines, child, newContent, style, currentDepth);
                    }
                }
                else
                {
                    // Node missing, create it
                    // If currentNode is null, we are inserting at Top Level (Level 0)
                    // If currentNode is set, we insert into it

                    int insertIndex = GetInsertionIndex(lines, currentNode, rootNodes);

                    string offsetHex = "0x" + targetOffset.ToString("x");
                    if (targetOffset < 0) offsetHex = "-0x" + Math.Abs(targetOffset).ToString("x");

                    string indent = style.GetIndentString(currentDepth);

                    if (i == chainOffsets.Count - 1)
                    {
                        // Target Node
                        string[] contentParts = newContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        string title = contentParts.Length > 0 ? contentParts[0] : "";

                        string sep = style.GetSeparatorString();
                        string header = $"{indent}{offsetHex}{sep}{title}";

                        lines.Insert(insertIndex, header);
                        for (int k = 1; k < contentParts.Length; k++)
                        {
                            lines.Insert(insertIndex + k, contentParts[k]);
                        }
                    }
                    else
                    {
                        // Intermediate Node
                        lines.Insert(insertIndex, $"{indent}{offsetHex}");

                        // Fake node to continue traversal context
                        var newNode = new NoteNode
                        {
                            RawLineIndex = insertIndex,
                            IndentLevel = CountIndent(indent),
                            Children = new List<NoteNode>()
                        };

                        if (currentNode != null)
                        {
                            currentNode.Children.Add(newNode);
                        }
                        else
                        {
                            // Add to flat list if it's a new root? Not strictly necessary for GetInsertionIndex logic 
                            // as long as we update currentNode. But for correctness:
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

            var nodeRegex = new Regex(@"^[.+\s]*[-+]?0x[0-9A-Fa-f]+");

            while (idx < lines.Count)
            {
                string line = lines[idx];
                if (string.IsNullOrWhiteSpace(line)) { idx++; removeCount++; continue; }

                if (nodeRegex.IsMatch(line)) break;

                idx++;
                removeCount++;
            }

            lines.RemoveRange(node.RawLineIndex, removeCount);

            // Rebuild
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
            // We should find the last Level 0 node and insert after its descendants.
            // If no Level 0 nodes, insert at end.

            if (parent == null)
            {
                var lastLevel0 = allNodes.LastOrDefault(n => n.IndentLevel == 0);
                if (lastLevel0 == null)
                {
                    // No existing nodes, append to end.
                    // Note: allNodes usually contains Root(-2) at 0. We skip that.
                    // If file is empty except header, lines.Count is correct.
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
            // Skip the header and any body lines
            int idx = node.RawLineIndex + 1;
            var nodeRegex = new Regex(@"^[.+\s]*[-+]?0x[0-9A-Fa-f]+");

            while (idx < lines.Count)
            {
                string line = lines[idx];
                // If we hit a line that looks like a node, we stop.
                if (nodeRegex.IsMatch(line)) return idx;
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

        private static int CountIndent(string s)
        {
            int count = 0;
            foreach (char c in s) { if (c == '+' || c == '.') count++; }
            return count;
        }
    }
}