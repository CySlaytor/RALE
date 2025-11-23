using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Parsers
{
    public static class PointerTreeParser
    {
        public static List<NoteNode> ParseNoteText(string noteText)
        {
            string[] lines = noteText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            bool isPointerNote = noteText.Contains("Pointer", StringComparison.OrdinalIgnoreCase);

            var flatList = new List<NoteNode>();
            // Root node covers the full note text
            var root = new NoteNode { Offset = "(Default)", Description = "Full Note", IndentLevel = -2, Content = noteText };

            // Try parse root size from first line if it's a struct definition
            if (lines.Length > 0) root.Size = ParseSizeFromDescription(lines[0]);

            flatList.Add(root);

            if (isPointerNote)
            {
                var stack = new Stack<NoteNode>();
                var logicalRoot = new NoteNode { IndentLevel = -1 };
                stack.Push(logicalRoot);
                NoteNode? lastAddedNode = null;

                // Regex to capture indentation, offset, and description
                // Added capturing group for separator check if needed, but logic below handles it
                var offsetRegex = new Regex(@"^([.\+]*)\s*([-+]?0x[0-9A-Fa-f]+)\s*(?:[|=:]\s*)?(.*)$");
                var prefixStripRegex = new Regex(@"^([.\+]+)");

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Replace("\r", "").Replace("\n", "");
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var match = offsetRegex.Match(line.Trim());
                    bool isNode = false;

                    // Heuristic: A line is a Pointer Node if it matches the regex AND:
                    // 1. It has explicit indentation (+ or .)
                    // 2. OR it has a standard separator (| or :)
                    // Lines like "0x2f = Intro" have no indent and '=' separator, so they are treated as content.
                    if (match.Success)
                    {
                        bool hasIndent = match.Groups[1].Length > 0;
                        bool hasStandardSeparator = line.Contains("|") || line.Contains(":");

                        if (hasIndent || hasStandardSeparator)
                        {
                            isNode = true;
                        }
                    }

                    if (isNode)
                    {
                        int indent = match.Groups[1].Length;
                        string offset = match.Groups[2].Value;
                        string desc = match.Groups[3].Value;
                        if (!offset.StartsWith("+") && !offset.StartsWith("-")) offset = "+" + offset;

                        long size = ParseSizeFromDescription(desc);

                        var node = new NoteNode { Offset = offset, Description = desc, RawLineIndex = i, IndentLevel = indent, Size = size };

                        // Pop stack until we find the parent (indent level must be less than current)
                        while (stack.Count > 0 && stack.Peek().IndentLevel >= indent)
                            stack.Pop();

                        // Safety check if stack somehow empty (shouldn't happen with logicalRoot)
                        if (stack.Count == 0) stack.Push(logicalRoot);

                        var parent = stack.Peek();
                        node.Parent = parent;
                        parent.Children.Add(node);
                        stack.Push(node);

                        lastAddedNode = node;
                    }
                    else
                    {
                        // Treat as content for the last added node (or root if none)
                        if (lastAddedNode != null)
                        {
                            if (lastAddedNode.Content.Length > 0) lastAddedNode.Content += "\r\n";
                            // Strip indentation markers if they exist (unlikely for content but safe to do)
                            string cleanContentLine = prefixStripRegex.Replace(line.Trim(), "");
                            lastAddedNode.Content += cleanContentLine;
                        }
                    }
                }
                CollectAllNodes(logicalRoot, flatList);
            }
            return flatList;
        }

        private static long ParseSizeFromDescription(string desc)
        {
            if (string.IsNullOrEmpty(desc)) return 1;

            // 1. Explicit [X bytes]
            var bytesMatch = Regex.Match(desc, @"\[(\d+)\s*bytes?\]", RegexOptions.IgnoreCase);
            if (bytesMatch.Success && long.TryParse(bytesMatch.Groups[1].Value, out long bSize))
                return bSize;

            // 2. Standard types [32-bit] etc
            if (desc.Contains("[32-bit") || desc.Contains("[Float")) return 4;
            if (desc.Contains("[24-bit")) return 3;
            if (desc.Contains("[16-bit")) return 2;
            if (desc.Contains("[8-bit")) return 1;

            return 1;
        }

        private static void CollectAllNodes(NoteNode node, List<NoteNode> flatList)
        {
            if (node.IndentLevel != -1) flatList.Add(node);
            foreach (var child in node.Children) CollectAllNodes(child, flatList);
        }

        public static (string Logic, string Type, string Size) GeneratePointerLogic(CodeNote baseNote, NoteNode selectedNode)
        {
            if (baseNote == null) return ("", "", "");
            string baseAddrStr = baseNote.Address.StartsWith("0x") ? baseNote.Address.Substring(2) : baseNote.Address;
            string type = "Mem"; string size = "32-bit";

            string noteHeader = baseNote.Note.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            char ptrPrefix = ' '; bool defaultMask = false;

            if (noteHeader.Contains("32-bit BE Pointer", StringComparison.OrdinalIgnoreCase)) { ptrPrefix = 'G'; defaultMask = true; }
            else if (noteHeader.Contains("32-bit Pointer", StringComparison.OrdinalIgnoreCase)) ptrPrefix = 'X';
            else if (noteHeader.Contains("24-bit", StringComparison.OrdinalIgnoreCase)) ptrPrefix = 'W';
            else if (noteHeader.Contains("16-bit", StringComparison.OrdinalIgnoreCase)) ptrPrefix = ' ';

            long explicitMaskVal = -1;
            var maskMatch = Regex.Match(noteHeader, @"&\s*(?:0x)?([0-9A-Fa-f]+)");
            if (maskMatch.Success) long.TryParse(maskMatch.Groups[1].Value, NumberStyles.HexNumber, null, out explicitMaskVal);

            string prefixStr = ptrPrefix == ' ' ? "" : ptrPrefix.ToString();
            string maskStr = "";
            if (explicitMaskVal != -1) maskStr = "&" + explicitMaskVal.ToString();
            else if (defaultMask) maskStr = "&536870911";

            var sb = new StringBuilder();
            string descriptionForType = "";

            if (selectedNode != null && selectedNode.IndentLevel != -2)
            {
                var chain = new List<string>();
                var current = selectedNode;
                descriptionForType = selectedNode.Description;
                while (current != null && current.IndentLevel != -1)
                {
                    string hex = current.Offset.TrimStart('+', '-').Replace("0x", "");
                    if (hex.Length < 8) hex = hex.PadLeft(8, '0');
                    chain.Insert(0, hex);
                    current = current.Parent;
                }
                sb.Append($"I:0x{prefixStr}{baseAddrStr}{maskStr}");
                for (int i = 0; i < chain.Count - 1; i++) sb.Append($"_I:0x{prefixStr}{chain[i]}{maskStr}");
                if (chain.Count > 0) sb.Append($"_0x{chain[chain.Count - 1]}");
            }
            else { sb.Append($"0x{baseAddrStr}"); descriptionForType = noteHeader; }

            var typeMatch = Regex.Match(descriptionForType, @"\[(.*?)]");
            if (typeMatch.Success)
            {
                string tag = typeMatch.Groups[1].Value;
                if (tag.Equals("Float", StringComparison.OrdinalIgnoreCase) || tag.Equals("Flaot", StringComparison.OrdinalIgnoreCase)) { type = "Mem"; size = "Float"; }
                else { size = tag; type = "Mem"; }
            }
            else size = "8-bit";

            return (sb.ToString(), type, size);
        }

        public static List<long> GetOffsetsFromNode(NoteNode node)
        {
            var nodeChain = new List<long>();
            var current = node;
            while (current != null && current.IndentLevel != -1)
            {
                string raw = current.Offset.TrimStart('+');
                if (long.TryParse(raw.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val)) nodeChain.Insert(0, val);
                else return new List<long>();
                current = current.Parent;
            }
            return nodeChain;
        }
    }
}