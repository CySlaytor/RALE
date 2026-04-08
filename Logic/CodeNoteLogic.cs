#nullable enable
using RALogicEditor.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Logic
{
    // Ported from Pointer Finder 2.0
    // This is used for the "Live Preview" and generating new notes based on prefs.
    public static class CodeNoteFormatter
    {
        public static string BuildCodeNote(List<int> offsets, string prefixPref, string suffixPref, bool alignSuffixes, bool lastLineOnly, string? finalMemorySize = null, string? finalDescription = null)
        {
            if (offsets == null || offsets.Count == 0) return string.Empty;

            var lines = new List<string>();

            // 1. Generate the base lines for each offset.
            for (int i = 0; i < offsets.Count; i++)
            {
                int offset = offsets[i];
                string prefix = (i == 0) ? "" : string.Concat(Enumerable.Repeat(prefixPref, i));
                string sign = "+"; // Assuming positive for simple display, logic handles negatives if passed raw
                string offsetHex = $"{Math.Abs(offset):X}";
                if (offset < 0) sign = "-";

                lines.Add($"{prefix}{sign}0x{offsetHex}");
            }

            // 2. Apply alignment and suffixes.
            int maxLength = alignSuffixes ? lines.Max(l => l.Length) : 0;
            var finalLines = new List<string>();

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string currentSuffix = "";

                // Logic: Apply suffix if it's NOT "Last Line Only" OR if it IS the last line
                if (!lastLineOnly || (i == lines.Count - 1))
                {
                    currentSuffix = suffixPref;
                }

                string padding = alignSuffixes ? new string(' ', maxLength - line.Length) : "";
                finalLines.Add($"{line}{padding}{currentSuffix}");
            }

            // 3. Handle the final line description.
            if (finalLines.Count != 0 && (!string.IsNullOrEmpty(finalMemorySize) || !string.IsNullOrEmpty(finalDescription)))
            {
                int lastIndex = finalLines.Count - 1;
                var sb = new StringBuilder(finalLines[lastIndex]);

                if (!string.IsNullOrEmpty(finalMemorySize) && finalMemorySize != "N/A")
                {
                    sb.Append($"[{finalMemorySize}] ");
                }
                if (!string.IsNullOrEmpty(finalDescription))
                {
                    sb.Append(finalDescription);
                }
                finalLines[lastIndex] = sb.ToString().TrimEnd();
            }

            return string.Join(Environment.NewLine, finalLines);
        }
    }

    // Ported from Pointer Finder 2.0
    // This is the "Better" logic that merges duplicate nodes correctly.
    public static class CodeNoteLogic
    {
        private class CodeNoteNode
        {
            public string Content { get; set; } = "";
            public List<CodeNoteNode> Children { get; } = new List<CodeNoteNode>();
            public int OriginalIndex { get; set; }
        }

        // Logic: Parses raw code note text, merges duplicate offsets, fixes hierarchy indentation, and sorts results.
        public static string FixHierarchy(string inputText, string indentCharPref, bool indentValues)
        {
            if (string.IsNullOrWhiteSpace(inputText)) return string.Empty;

            var lines = inputText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Auto-detect style if user pastes mixed content, though usually we pass the pref
            string style = "Dots (.)";
            if (indentCharPref.StartsWith("├")) style = "Unicode Tree (├─)";
            else if (lines.Any(line => line.TrimStart().StartsWith("++")) || indentCharPref.StartsWith("+")) style = "Plusses (+)";

            var parsedTree = ParseLinesToTree(lines);
            var mergedTree = MergeTree(parsedTree);

            // Sort: Non-offsets first, then offsets numerically (Handled inside MergeTree now)

            return RebuildTextFromTree(mergedTree, 0, style, indentValues).Trim();
        }

        private static List<CodeNoteNode> ParseLinesToTree(string[] lines)
        {
            var rootNodes = new List<CodeNoteNode>();
            // Stack keeps track of current hierarchy depth
            var stack = new Stack<(int indent, List<CodeNoteNode> parentChildrenList)>();
            // Initialize stack with root level (indent -1)
            stack.Push((-1, rootNodes));
            CodeNoteNode? lastNode = null;
            int lineIndex = 0;

            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                // Skip empty lines or separators
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("---")) { lineIndex++; continue; }

                int indent = 0;
                string content;
                string untrimmedLine = line.TrimEnd();

                // Count leading characters that define indentation level for traditional styles
                int classicPrefixLength = untrimmedLine.TakeWhile(c => c == '.' || c == '+').Count();

                if (classicPrefixLength > 0)
                {
                    // Handle specific edge case for '+' style (e.g. "++0x...")
                    // For plus-based indents like "++0x...", the indent is one less than the count
                    if (untrimmedLine[classicPrefixLength - 1] == '+' && untrimmedLine.Length > classicPrefixLength && untrimmedLine[classicPrefixLength] != ' ')
                    {
                        indent = classicPrefixLength - 1;
                        content = untrimmedLine.Substring(indent).Trim();
                    }
                    else
                    {
                        indent = classicPrefixLength;
                        content = untrimmedLine.Substring(indent).Trim();
                    }
                }
                else
                {
                    // Check for Unicode tree characters so pasting existing formatted trees still works
                    int unicodePrefixLength = untrimmedLine.TakeWhile(c => c == '│' || c == ' ' || c == '├' || c == '└' || c == '┬' || c == '─').Count();
                    bool hasBoxChar = untrimmedLine.Substring(0, unicodePrefixLength).Any(c => c == '│' || c == '├' || c == '└' || c == '┬' || c == '─');

                    if (unicodePrefixLength > 0 && hasBoxChar)
                    {
                        char lastChar = untrimmedLine[unicodePrefixLength - 1];
                        if (lastChar == '┬' || lastChar == '─')
                        {
                            indent = unicodePrefixLength - 1;
                        }
                        else
                        {
                            indent = 0; // Force 0 so it attaches to lastNode as an Attached Note
                        }
                        content = untrimmedLine.Substring(unicodePrefixLength).Trim();
                    }
                    else
                    {
                        indent = 0;
                        content = untrimmedLine.Trim();
                    }
                }

                if (indent < 0) indent = 0;

                // Heuristic: Is this line a note attached to the previous pointer?
                // Logic: It has 0 indent, there was a previous node, and it DOESN'T look like a new pointer offset.
                bool isAttachedNote = (indent == 0) && (lastNode != null) &&
                                      !(content.StartsWith("+0x", StringComparison.OrdinalIgnoreCase) ||
                                        content.StartsWith("-0x", StringComparison.OrdinalIgnoreCase) ||
                                        content.StartsWith("["));

                if (isAttachedNote)
                {
                    // Add it as a child of the previous node regardless of hierarchy
                    lastNode!.Children.Add(new CodeNoteNode { Content = content, OriginalIndex = lineIndex });
                }
                else
                {
                    // Pop stack until we find the correct parent level
                    while (stack.Peek().indent >= indent) stack.Pop();
                    var node = new CodeNoteNode { Content = content, OriginalIndex = lineIndex };
                    // Add to the current parent's children list
                    stack.Peek().parentChildrenList.Add(node);
                    // Push this node onto stack as the new potential parent for deeper indentation
                    stack.Push((indent, node.Children));
                    lastNode = node;
                }
                lineIndex++;
            }
            return rootNodes;
        }

        private static List<CodeNoteNode> MergeTree(List<CodeNoteNode> nodes)
        {
            var finalNodes = new List<CodeNoteNode>();
            var mergeableGroups = new Dictionary<string, List<CodeNoteNode>>();
            var nonMergeableNodes = new List<CodeNoteNode>();
            // Regex to identify pointer offsets (e.g., +0x1A, -0x4)
            var offsetRegex = new Regex(@"^([\+\-]0x[0-9a-fA-F]+)", RegexOptions.IgnoreCase);

            // 1. Group nodes by their offset value
            foreach (var node in nodes)
            {
                var match = offsetRegex.Match(node.Content);
                if (match.Success)
                {
                    // Normalize Key: "+0x4" and "+0x04" become same key "+0x4"
                    string rawKey = match.Groups[1].Value;
                    string sign = rawKey.Substring(0, 1);
                    long offsetValue = Convert.ToInt64(rawKey.Substring(3), 16);
                    string key = $"{sign}0x{offsetValue:x}";
                    if (!mergeableGroups.ContainsKey(key)) mergeableGroups[key] = new List<CodeNoteNode>();
                    mergeableGroups[key].Add(node);
                }
                else
                {
                    nonMergeableNodes.Add(node);
                }
            }

            // 2. Process the grouped (mergeable) nodes
            var contentRegex = new Regex(@"^([\+\-]0x)([0-9a-fA-F]+)(.*)", RegexOptions.IgnoreCase);
            foreach (var group in mergeableGroups.Values)
            {
                // We pick the node that appeared earliest as the "base" for index
                var representative = group.OrderBy(n => n.OriginalIndex).First();
                // Intelligence: Keep the description text from the node that has the LONGEST string.
                string bestContent = group.Select(n => n.Content).OrderByDescending(c => c.Length).FirstOrDefault() ?? representative.Content;

                // Normalize the text format (ensure lowercase hex)
                var contentMatch = contentRegex.Match(bestContent);
                if (contentMatch.Success)
                {
                    string signAndPrefix = contentMatch.Groups[1].Value;
                    long offsetVal = Convert.ToInt64(contentMatch.Groups[2].Value, 16);
                    string restOfLine = contentMatch.Groups[3].Value;
                    bestContent = $"{signAndPrefix.ToLower()}{offsetVal:x}{restOfLine}";
                }

                var newNode = new CodeNoteNode { Content = bestContent, OriginalIndex = representative.OriginalIndex };

                // Recursively merge all children from all nodes in this group
                var allChildren = group.SelectMany(n => n.Children).ToList();
                if (allChildren.Any())
                {
                    // FIX: Deduplicate child nodes by content before merging to prevent doubling up on enums/descriptions
                    var uniqueChildren = allChildren
                        .GroupBy(c => c.Content.Trim())
                        .Select(g => g.First())
                        .ToList();

                    newNode.Children.AddRange(MergeTree(uniqueChildren));
                }
                finalNodes.Add(newNode);
            }

            // 3. Process non-mergeable nodes (comments, headers, values)
            foreach (var node in nonMergeableNodes)
            {
                if (node.Children.Any())
                {
                    var mergedChildren = MergeTree(node.Children);
                    node.Children.Clear();
                    node.Children.AddRange(mergedChildren);
                }
                finalNodes.Add(node);
            }

            // Finally, sort the processed nodes at this level.
            // This ensures offsets are numerically sorted, while other content stays in place.
            return SortNodes(finalNodes);
        }

        // Helper method to sort nodes intelligently.
        private static List<CodeNoteNode> SortNodes(List<CodeNoteNode> nodes)
        {
            var offsetRegex = new Regex(@"^[\+\-]0x([0-9a-fA-F]+)", RegexOptions.IgnoreCase);

            // Logic:
            // 1. Primary Sort: Non-offsets (Group 0) come before Offsets (Group 1).
            // 2. Secondary Sort:
            //    - If it's an offset (Group 1), sort by the numeric hexadecimal value.
            //    - If it's not an offset (Group 0), sort by OriginalIndex to preserve file order.
            return nodes.OrderBy(node => offsetRegex.IsMatch(node.Content) ? 1 : 0)
                        .ThenBy(node =>
                        {
                            var match = offsetRegex.Match(node.Content);
                            if (match.Success)
                            {
                                return Convert.ToInt64(match.Groups[1].Value, 16);
                            }
                            return (long)node.OriginalIndex;
                        }).ToList();
        }

        private static string RebuildTextFromTree(List<CodeNoteNode> nodes, int indentLevel, string style, bool indentDescriptions, List<bool>? ancestorIsLast = null)
        {
            if (ancestorIsLast == null) ancestorIsLast = new List<bool>();
            var sb = new StringBuilder();

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                bool isLastChild = (i == nodes.Count - 1);

                // Check if any actual pointer descendants exist to know whether to draw a "┬" or "─"
                bool hasPointerChildren = node.Children.Any(c => c.Content.StartsWith("+0x", StringComparison.OrdinalIgnoreCase) || c.Content.StartsWith("-0x", StringComparison.OrdinalIgnoreCase));

                // Formatting: Add a blank line before top-level nodes (except the first) for readability
                if (indentLevel == 0 && sb.Length > 0)
                {
                    sb.AppendLine();
                }

                // Identify if this node is a pointer/struct offset or a value/comment
                bool isPointerOffset = node.Content.StartsWith("+0x", StringComparison.OrdinalIgnoreCase) || node.Content.StartsWith("-0x", StringComparison.OrdinalIgnoreCase);
                bool isAttachedNote = !isPointerOffset;

                string prefix = "";

                if (style.StartsWith("Unicode Tree"))
                {
                    if (isAttachedNote)
                    {
                        if (indentDescriptions && indentLevel > 0)
                        {
                            for (int a = 0; a < indentLevel - 1; a++) prefix += ancestorIsLast[a] ? " " : "│";
                            prefix += " "; // Fixed: Use a blank space for notes so they align cleanly under the parent text
                        }
                    }
                    else
                    {
                        if (indentLevel > 0)
                        {
                            for (int a = 0; a < indentLevel - 1; a++) prefix += ancestorIsLast[a] ? " " : "│";
                            prefix += isLastChild ? "└" : "├";
                            prefix += hasPointerChildren ? "┬" : "─";
                        }
                    }
                }
                else
                {
                    char indentChar = style.StartsWith("Plusses") ? '+' : '.';

                    if (isAttachedNote)
                    {
                        // For values/comments (children that aren't pointers)
                        if (indentDescriptions) prefix = indentLevel > 0 ? new string('.', indentLevel) : "";
                    }
                    else
                    {
                        prefix = indentLevel > 0 ? new string(indentChar, indentLevel) : "";
                    }
                }

                sb.AppendLine($"{prefix}{node.Content}");

                if (node.Children.Any())
                {
                    List<bool> newAncestorIsLast = indentLevel == 0 ? new List<bool>() : new List<bool>(ancestorIsLast) { isLastChild };
                    sb.Append(RebuildTextFromTree(node.Children, indentLevel + 1, style, indentDescriptions, newAncestorIsLast));
                }
            }
            return sb.ToString();
        }
    }
}