using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Logic
{
    // Ported from Pointer Finder 2.0
    // A tool to parse, merge, and clean up complex code notes with hierarchical pointer chains.
    public static class CodeNoteLogic
    {
        // Represents a single line/node in the code note's hierarchy.
        private class CodeNoteNode
        {
            // The content of the line without leading indentation, used as the key for merging.
            public string Content { get; set; } = "";
            // Child nodes in the hierarchy.
            public List<CodeNoteNode> Children { get; } = new List<CodeNoteNode>();
            // The original index, used to maintain order for non-mergeable items.
            public int OriginalIndex { get; set; }
        }

        // Logic: Parses raw code note text, merges duplicate offsets, fixes hierarchy indentation, and sorts results.
        public static string FixHierarchy(string inputText, string indentStylePref, bool indentDescriptions)
        {
            if (string.IsNullOrWhiteSpace(inputText)) return string.Empty;

            var lines = inputText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            string style = indentStylePref;
            if (style.StartsWith("Auto Detect") || string.IsNullOrEmpty(style))
            {
                char detectedIndent = DetectIndentStyle(lines);
                style = detectedIndent == '+' ? "Plusses (+)" : "Dots (.)";
            }

            // 1. Parse the flat text into a tree structure.
            var parsedTree = ParseLinesToTree(lines);

            // 2. Recursively merge AND sort nodes.
            var mergedTree = MergeTree(parsedTree);

            // 3. Rebuild the text from the cleaned tree structure, using the detected style and user preference.
            return RebuildTextFromTree(mergedTree, 0, style, indentDescriptions).Trim();
        }

        private static char DetectIndentStyle(string[] lines)
        {
            // If any line uses multiple '+' for indentation, prefer that style.
            if (lines.Any(line => line.TrimStart().StartsWith("++")))
            {
                return '+';
            }
            // Otherwise, default to dots.
            return '.';
        }

        private static List<CodeNoteNode> ParseLinesToTree(string[] lines)
        {
            var rootNodes = new List<CodeNoteNode>();
            var stack = new Stack<(int indent, List<CodeNoteNode> parentChildrenList)>();
            stack.Push((-1, rootNodes));
            CodeNoteNode? lastNode = null;
            int lineIndex = 0;

            // Regex matching AutoCR's robust structural detection
            var offsetRegex = new Regex(@"^([.\+\s\u2500-\u257F]*)([-+]?)(0x[0-9A-Fa-f]+)\s*([|=:])?\s*(.*)$", RegexOptions.IgnoreCase);
            var prefixStripRegex = new Regex(@"^([.\+\s\u2500-\u257F]+)");

            foreach (var line in lines)
            {
                string untrimmedLine = line.TrimEnd();

                if (string.IsNullOrWhiteSpace(untrimmedLine) || untrimmedLine.TrimStart().StartsWith("---"))
                {
                    lineIndex++;
                    continue;
                }

                int indent = 0;
                bool isNode = false;

                // CRITICAL FIX: Match against untrimmedLine to preserve leading spaces for indentation detection!
                // If we match against trimmedLine, Unicode styles (e.g. "  ├─") lose their spaces and get flattened to indent=2.
                var match = offsetRegex.Match(untrimmedLine);

                if (match.Success)
                {
                    string indentStr = match.Groups[1].Value;
                    string sign = match.Groups[2].Value;
                    string separator = match.Groups[4].Value;

                    bool hasSign = !string.IsNullOrEmpty(sign);
                    bool hasIndent = indentStr.Length > 0;

                    if (hasSign)
                    {
                        isNode = true;
                    }
                    else if (indentStr.Contains("+"))
                    {
                        isNode = true; // Treat any indentation with '+' as a node
                    }
                    else if (hasIndent)
                    {
                        // Indented with dots/spaces/box only, no sign (e.g. "..0x04").
                        if (separator == "|")
                        {
                            isNode = true;
                        }
                    }

                    if (isNode)
                    {
                        // Safely recover sign if the greedy group 1 ate it
                        if (string.IsNullOrEmpty(sign))
                        {
                            if (indentStr.EndsWith("+")) { sign = "+"; indentStr = indentStr.Substring(0, indentStr.Length - 1); }
                            else if (indentStr.EndsWith("-")) { sign = "-"; indentStr = indentStr.Substring(0, indentStr.Length - 1); }
                        }

                        indent = indentStr.Length;

                        string hex = match.Groups[3].Value;
                        string remainder = match.Groups[5].Value;

                        // Overwrite untrimmed line completely to cleanly detach the payload from the visual hierarchy
                        untrimmedLine = $"{sign}{hex} {separator} {remainder}".Trim();
                    }
                }

                if (indent < 0) indent = 0;

                bool isAttachedNote = (indent == 0) && (lastNode != null) &&
                                      !(untrimmedLine.TrimStart().StartsWith("+0x", StringComparison.OrdinalIgnoreCase) ||
                                        untrimmedLine.TrimStart().StartsWith("-0x", StringComparison.OrdinalIgnoreCase) ||
                                        untrimmedLine.TrimStart().StartsWith("["));

                if (isAttachedNote)
                {
                    string cleanContentLine = prefixStripRegex.Replace(untrimmedLine, "").TrimStart();
                    lastNode!.Children.Add(new CodeNoteNode { Content = cleanContentLine, OriginalIndex = lineIndex });
                }
                else
                {
                    while (stack.Peek().indent >= indent)
                    {
                        stack.Pop();
                    }

                    string content = isNode ? untrimmedLine : prefixStripRegex.Replace(untrimmedLine, "").TrimStart();
                    var node = new CodeNoteNode { Content = content, OriginalIndex = lineIndex };
                    stack.Peek().parentChildrenList.Add(node);
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
            var offsetRegex = new Regex(@"^([\+\-]0x[0-9a-fA-F]+)", RegexOptions.IgnoreCase);

            // First pass: Group all mergeable nodes by their normalized offset key.
            foreach (var node in nodes)
            {
                var match = offsetRegex.Match(node.Content);
                if (match.Success)
                {
                    // Normalize the key to handle case (+0xAB vs +0xab) and padding (+0x4 vs +0x04).
                    string rawKey = match.Groups[1].Value;
                    string sign = rawKey.Substring(0, 1);
                    long offsetValue = Convert.ToInt64(rawKey.Substring(3), 16);
                    string key = $"{sign}0x{offsetValue:x}";

                    if (!mergeableGroups.ContainsKey(key))
                    {
                        mergeableGroups[key] = new List<CodeNoteNode>();
                    }
                    mergeableGroups[key].Add(node);
                }
                else
                {
                    nonMergeableNodes.Add(node);
                }
            }

            // Process mergeable groups.
            var contentRegex = new Regex(@"^([\+\-]0x)([0-9a-fA-F]+)(.*)", RegexOptions.IgnoreCase);
            foreach (var group in mergeableGroups.Values)
            {
                var representative = group.OrderBy(n => n.OriginalIndex).First();

                // Select the content with the longest description to preserve information.
                string bestContent = group
                    .Select(n => n.Content)
                    .OrderByDescending(c => c.Length)
                    .FirstOrDefault() ?? representative.Content;

                // Normalize the chosen content for consistent output format (e.g., +0x0, lowercase hex).
                var contentMatch = contentRegex.Match(bestContent);
                if (contentMatch.Success)
                {
                    string signAndPrefix = contentMatch.Groups[1].Value;
                    long offsetVal = Convert.ToInt64(contentMatch.Groups[2].Value, 16);
                    string restOfLine = contentMatch.Groups[3].Value;
                    bestContent = $"{signAndPrefix.ToLower()}{offsetVal:x}{restOfLine}";
                }

                var newNode = new CodeNoteNode { Content = bestContent, OriginalIndex = representative.OriginalIndex };

                // Combine all children from all nodes in the group.
                var allChildren = group.SelectMany(n => n.Children).ToList();
                if (allChildren.Any())
                {
                    // Recursively merge children to properly combine sub-branches
                    newNode.Children.AddRange(MergeTree(allChildren));
                }
                finalNodes.Add(newNode);
            }

            // Process non-mergeable nodes, which just pass through but have their children merged.
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

        // Integrated spacing logic directly into the rebuild function.
        // Now supports robust Unicode box-drawing Tree generations.
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

                // Add a blank line before any top-level node except the very first one.
                if (indentLevel == 0 && sb.Length > 0)
                {
                    sb.AppendLine();
                }

                bool isPointerOffset = node.Content.StartsWith("+0x", StringComparison.OrdinalIgnoreCase) || node.Content.StartsWith("-0x", StringComparison.OrdinalIgnoreCase);
                bool isAttachedNote = !isPointerOffset;

                string prefix = "";

                if (style.StartsWith("Unicode Tree"))
                {
                    if (isAttachedNote)
                    {
                        if (indentDescriptions && indentLevel > 0)
                        {
                            for (int a = 0; a < indentLevel - 1; a++)
                            {
                                prefix += ancestorIsLast[a] ? " " : "│";
                            }
                            prefix += " "; // Fixed: Use a blank space for notes so they align cleanly under the parent text
                        }
                    }
                    else
                    {
                        if (indentLevel > 0)
                        {
                            for (int a = 0; a < indentLevel - 1; a++)
                            {
                                prefix += ancestorIsLast[a] ? " " : "│";
                            }
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
                        if (indentDescriptions)
                        {
                            prefix = indentLevel > 0 ? new string('.', indentLevel) : "";
                        }
                    }
                    else
                    {
                        prefix = indentLevel > 0 ? new string(indentChar, indentLevel) : "";
                    }
                }

                sb.AppendLine($"{prefix}{node.Content}");

                if (node.Children.Any())
                {
                    List<bool> newAncestorIsLast;
                    if (indentLevel == 0)
                    {
                        newAncestorIsLast = new List<bool>();
                    }
                    else
                    {
                        newAncestorIsLast = new List<bool>(ancestorIsLast) { isLastChild };
                    }
                    sb.Append(RebuildTextFromTree(node.Children, indentLevel + 1, style, indentDescriptions, newAncestorIsLast));
                }
            }
            return sb.ToString();
        }
    }
}