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
            var root = new NoteNode { Offset = "(Default)", Description = "Full Note", IndentLevel = -2, Content = noteText };
            flatList.Add(root);

            if (isPointerNote)
            {
                var stack = new Stack<NoteNode>();
                var logicalRoot = new NoteNode { IndentLevel = -1 };
                stack.Push(logicalRoot);
                NoteNode? lastAddedNode = null;

                var offsetRegex = new Regex(@"^([.\+]*)\s*([-+]?0x[0-9A-Fa-f]+)\s*(?:[|=:]\s*)?(.*)$");
                var prefixStripRegex = new Regex(@"^([.\+]+)");

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Replace("\r", "").Replace("\n", "");
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var match = offsetRegex.Match(line.Trim());
                    if (match.Success)
                    {
                        int indent = match.Groups[1].Length;
                        string offset = match.Groups[2].Value;
                        string desc = match.Groups[3].Value;
                        if (!offset.StartsWith("+") && !offset.StartsWith("-")) offset = "+" + offset;

                        var node = new NoteNode { Offset = offset, Description = desc, RawLineIndex = i, IndentLevel = indent };
                        while (stack.Peek().IndentLevel >= indent) stack.Pop();
                        var parent = stack.Peek();
                        node.Parent = parent; parent.Children.Add(node); stack.Push(node);
                        lastAddedNode = node;
                    }
                    else
                    {
                        if (lastAddedNode != null)
                        {
                            if (lastAddedNode.Content.Length > 0) lastAddedNode.Content += "\r\n";
                            string cleanContentLine = prefixStripRegex.Replace(line.Trim(), "");
                            lastAddedNode.Content += cleanContentLine;
                        }
                    }
                }
                CollectAllNodes(logicalRoot, flatList);
            }
            return flatList;
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