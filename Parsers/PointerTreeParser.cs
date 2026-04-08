using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Parsers
{
    // Parses raw Code Note text into a hierarchical tree of NoteNodes.
    // Handles standard RetroAchievements pointer notation, including Unicode box characters.
    public class PointerTreeParser : IPointerTreeParser
    {
        // Main entry point: Converts a multiline string into a flat list of nodes
        // preserving hierarchy via Parent/Children references.
        public List<NoteNode> ParseNoteText(string noteText)
        {
            if (string.IsNullOrWhiteSpace(noteText)) return new List<NoteNode>();

            string[] lines = noteText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            var flatList = new List<NoteNode>();

            // Create a root node that represents the "Base" of the note.
            // IndentLevel -2 signifies it is the absolute root container.
            var root = new NoteNode { Offset = "(Default)", Description = "Full Note", IndentLevel = -2, Content = noteText };

            // Attempt to parse the size of the root data from the first line 
            // (useful if the note defines a struct at the base address).
            if (lines.Length > 0) root.Size = ParseSizeFromDescription(lines[0]);

            flatList.Add(root);

            // Stack used to track the current parent at each indentation level.
            var stack = new Stack<NoteNode>();

            // Logical Root acts as the parent for top-level pointer offsets (Indent 0).
            var logicalRoot = new NoteNode { IndentLevel = -1 };
            stack.Push(logicalRoot);

            NoteNode? lastAddedNode = null;

            // Regex to parse specific pointer lines.
            // Group 1: Indentation (traditional chars + whitespace + Unicode box drawing chars \u2500-\u257F)
            // Group 2: Sign (+ or -). Optional capture.
            // Group 3: Hex value (0x...).
            // Group 4: Separator (|, =, :). Optional capture.
            // Group 5: Description/Remainder.
            var offsetRegex = new Regex(@"^([.\+\s\u2500-\u257F]*)([-+]?)(0x[0-9A-Fa-f]+)\s*([|=:])?\s*(.*)$");

            // Helper regex to strip structural characters from description/enum lines.
            var prefixStripRegex = new Regex(@"^([.\+\s\u2500-\u257F]+)");

            for (int i = 0; i < lines.Length; i++)
            {
                // TrimEnd instead of Trim ensures leading spaces/visual depth are preserved for the regex
                string line = lines[i].TrimEnd();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var match = offsetRegex.Match(line);
                bool isNode = false;

                if (match.Success)
                {
                    string indentStr = match.Groups[1].Value;
                    string sign = match.Groups[2].Value;
                    string separator = match.Groups[4].Value;

                    bool hasSign = !string.IsNullOrEmpty(sign); // e.g. +0x10 is definitely an offset
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
                }

                if (isNode)
                {
                    // Natural depth tracking: visual prefix length dictates the hierarchy level
                    string indentStr = match.Groups[1].Value;
                    int indent = indentStr.Length;

                    string sign = match.Groups[2].Value;
                    string hex = match.Groups[3].Value;
                    string desc = match.Groups[5].Value;

                    // Strip leading separators (- : =) just in case
                    while (desc.Length > 0 && (desc[0] == '-' || desc[0] == ':' || desc[0] == '=' || char.IsWhiteSpace(desc[0])))
                    {
                        desc = desc.Substring(1);
                    }
                    desc = desc.Trim();

                    // Normalization: Ensure offset string always has a sign for logic generation consistency.
                    string offset = sign + hex;
                    if (!offset.StartsWith("+") && !offset.StartsWith("-")) offset = "+" + offset;

                    long size = ParseSizeFromDescription(desc);

                    var node = new NoteNode { Offset = offset, Description = desc, RawLineIndex = i, IndentLevel = indent, Size = size };

                    // Tree Construction:
                    while (stack.Count > 0 && stack.Peek().IndentLevel >= indent)
                        stack.Pop();

                    // Safety fallback
                    if (stack.Count == 0) stack.Push(logicalRoot);

                    var parent = stack.Peek();
                    node.Parent = parent;
                    parent.Children.Add(node);

                    stack.Push(node);

                    lastAddedNode = node;
                }
                else
                {
                    // Append to content of most recent node
                    if (lastAddedNode != null)
                    {
                        if (lastAddedNode.Content.Length > 0) lastAddedNode.Content += "\r\n";
                        // Remove structural prefixes (like │ ) from description content to keep tooltips clean
                        string cleanContentLine = prefixStripRegex.Replace(line, "").TrimStart();
                        lastAddedNode.Content += cleanContentLine;
                    }
                }
            }

            // Recursively flatten the tree for the dropdown list.
            CollectAllNodes(logicalRoot, flatList);
            return flatList;
        }

        // Parses data size from tags in the description (e.g., [16-bit] or [32 bytes]).
        private long ParseSizeFromDescription(string desc)
        {
            if (string.IsNullOrEmpty(desc)) return 1;

            // 1. Check for explicit byte count: [X bytes], [ASCII: 20-bytes], [20-byte]
            // Allow characters before the digits (like ASCII: ) and allow dash in byte(s)
            var bytesMatch = Regex.Match(desc, @"\[.*?(\d+)\s*[-]?\s*bytes?.*?\]", RegexOptions.IgnoreCase);
            if (bytesMatch.Success && long.TryParse(bytesMatch.Groups[1].Value, out long bSize))
                return bSize;

            // 2. Check for standard type tags
            if (desc.Contains("[32-bit") || desc.Contains("[Float")) return 4;
            if (desc.Contains("[24-bit")) return 3;
            if (desc.Contains("[16-bit")) return 2;
            if (desc.Contains("[8-bit")) return 1;

            return 1; // Default to 8-bit (1 byte)
        }

        // Helper to traverse tree and populate the list.
        private void CollectAllNodes(NoteNode node, List<NoteNode> flatList)
        {
            if (node.IndentLevel != -1) flatList.Add(node);
            foreach (var child in node.Children) CollectAllNodes(child, flatList);
        }

        // Constructs the RA Logic String (e.g., I:0x1234_0x10) for a specific node in the tree.
        public (string Logic, string Type, string Size) GeneratePointerLogic(CodeNote baseNote, NoteNode selectedNode)
        {
            if (baseNote == null) return ("", "", "");

            // Base Address cleanup
            string baseAddrStr = baseNote.Address.StartsWith("0x") ? baseNote.Address.Substring(2) : baseNote.Address;
            string type = "Mem"; string size = "32-bit";

            // Parse the root note header to determine the pointer type (32-bit, 24-bit, BE, etc.)
            string noteHeader = baseNote.Note.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

            char ptrPrefix = ' ';
            bool defaultMask = false;

            // Robust case-insensitive pointer detection
            if (noteHeader.Contains("Pointer", StringComparison.OrdinalIgnoreCase))
            {
                // Default to 32-bit standard if just "Pointer" is found
                ptrPrefix = 'X';

                // Check for specific attributes
                if (noteHeader.Contains("BE", StringComparison.OrdinalIgnoreCase))
                {
                    ptrPrefix = 'G'; // Big Endian
                    defaultMask = true; // Default mask for BE pointers
                }

                if (noteHeader.Contains("24-bit", StringComparison.OrdinalIgnoreCase))
                {
                    ptrPrefix = 'W';
                }
                else if (noteHeader.Contains("16-bit", StringComparison.OrdinalIgnoreCase))
                {
                    // 16-bit pointer usually implies standard memory read (no prefix)
                    ptrPrefix = ' ';
                }
            }
            // Fallback for legacy/explicit exact matches just in case
            else if (noteHeader.Contains("32-bit BE", StringComparison.OrdinalIgnoreCase)) { ptrPrefix = 'G'; defaultMask = true; }
            else if (noteHeader.Contains("24-bit", StringComparison.OrdinalIgnoreCase)) ptrPrefix = 'W';

            // Check for explicit mask in note (e.g. &0xFFFFFF)
            long explicitMaskVal = -1;
            var maskMatch = Regex.Match(noteHeader, @"&\s*(?:0x)?([0-9A-Fa-f]+)");
            if (maskMatch.Success) long.TryParse(maskMatch.Groups[1].Value, NumberStyles.HexNumber, null, out explicitMaskVal);

            string prefixStr = ptrPrefix == ' ' ? "" : ptrPrefix.ToString();
            string maskStr = "";
            if (explicitMaskVal != -1) maskStr = "&" + explicitMaskVal.ToString();
            else if (defaultMask) maskStr = "&536870911"; // Default 29-bit mask for BE pointers (0x1FFFFFFF)

            var sb = new StringBuilder();
            string descriptionForType = "";

            // Build the chain if selecting a child node
            if (selectedNode != null && selectedNode.IndentLevel != -2)
            {
                var chain = new List<string>();
                var current = selectedNode;
                descriptionForType = selectedNode.Description;

                // Walk up the tree to collect offsets
                while (current != null && current.IndentLevel != -1)
                {
                    string hex = current.Offset.TrimStart('+', '-').Replace("0x", "");
                    if (hex.Length < 8) hex = hex.PadLeft(8, '0');
                    chain.Insert(0, hex);
                    current = current.Parent;
                }

                // Format: I:Base_I:Offset1_Offset2
                sb.Append($"I:0x{prefixStr}{baseAddrStr}{maskStr}");

                // Add intermediate offsets as "Add Address" (I:)
                for (int i = 0; i < chain.Count - 1; i++) sb.Append($"_I:0x{prefixStr}{chain[i]}{maskStr}");

                // Add final offset (The read target)
                if (chain.Count > 0) sb.Append($"_0x{chain[chain.Count - 1]}");
            }
            else
            {
                // Root selection
                sb.Append($"0x{baseAddrStr}");
                descriptionForType = noteHeader;
            }

            // Attempt to detect type/size from the description (e.g. [Float], [16-bit])
            var typeMatch = Regex.Match(descriptionForType, @"\[(.*?)]");
            if (typeMatch.Success)
            {
                string tag = typeMatch.Groups[1].Value;
                if (tag.Equals("Float", StringComparison.OrdinalIgnoreCase) || tag.Equals("Flaot", StringComparison.OrdinalIgnoreCase)) { type = "Mem"; size = "Float"; }
                else { size = tag; type = "Mem"; }
            }
            else size = "8-bit"; // Default fallback

            return (sb.ToString(), type, size);
        }

        // Reverse-engineers the chain of offsets from a specific node up to the root.
        public List<long> GetOffsetsFromNode(NoteNode node)
        {
            var nodeChain = new List<long>();
            var current = node;
            while (current != null && current.IndentLevel != -1)
            {
                string raw = current.Offset.TrimStart('+');
                if (long.TryParse(raw.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val))
                    nodeChain.Insert(0, val);
                else
                    return new List<long>(); // Invalid chain

                current = current.Parent;
            }
            return nodeChain;
        }
    }
}