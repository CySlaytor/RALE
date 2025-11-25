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
    // Handles standard RetroAchievements pointer notation (indentation with dots/pluses).
    // Refactored to instance-based class implementing IPointerTreeParser.
    public class PointerTreeParser : IPointerTreeParser
    {
        // Main entry point: Converts a multiline string into a flat list of nodes
        // preserving hierarchy via Parent/Children references.
        public List<NoteNode> ParseNoteText(string noteText)
        {
            string[] lines = noteText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            // Check for pointer keyword in the entire text to enable tree parsing
            bool isPointerNote = noteText.Contains("Pointer", StringComparison.OrdinalIgnoreCase);

            var flatList = new List<NoteNode>();

            // Create a root node that represents the "Base" of the note.
            // IndentLevel -2 signifies it is the absolute root container.
            var root = new NoteNode { Offset = "(Default)", Description = "Full Note", IndentLevel = -2, Content = noteText };

            // Attempt to parse the size of the root data from the first line 
            // (useful if the note defines a struct at the base address).
            if (lines.Length > 0) root.Size = ParseSizeFromDescription(lines[0]);

            flatList.Add(root);

            if (isPointerNote)
            {
                // Stack used to track the current parent at each indentation level.
                var stack = new Stack<NoteNode>();

                // Logical Root acts as the parent for top-level pointer offsets (Indent 0).
                var logicalRoot = new NoteNode { IndentLevel = -1 };
                stack.Push(logicalRoot);

                NoteNode? lastAddedNode = null;

                // Regex to parse specific pointer lines.
                // Group 1: Indentation (e.g., "+", ".+", "..", "++")
                // Group 2: Sign (+ or -). Optional capture.
                // Group 3: Hex value (0x...).
                // Group 4: Description/Remainder.
                var offsetRegex = new Regex(@"^([.\+\s]*)([-+]?)(0x[0-9A-Fa-f]+)\s*(?:[|=:]\s*)?(.*)$");

                // Helper regex to strip indentation characters from description lines.
                var prefixStripRegex = new Regex(@"^([.\+\s]+)");

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Replace("\r", "").Replace("\n", "");
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var match = offsetRegex.Match(line.Trim());
                    bool isNode = false;

                    if (match.Success)
                    {
                        string indentStr = match.Groups[1].Value;
                        string sign = match.Groups[2].Value;

                        // Heuristic Logic to distinguish a "Pointer Offset" from a "Value Enumeration".
                        // 
                        // 1. If it has an explicit sign (+ or -), it is definitely an offset (e.g., +0x10).
                        // 2. If it has indentation containing '+', it implies an offset chain (e.g., ++0x10).
                        // 3. If it has indentation AND a separator (| or :), it is likely a struct offset (e.g., ..0x10 | Health).

                        bool hasSign = !string.IsNullOrEmpty(sign);
                        bool hasIndent = indentStr.Contains(".") || indentStr.Contains("+");
                        bool hasSeparator = line.Contains("|") || line.Contains(":");

                        if (hasSign)
                        {
                            isNode = true;
                        }
                        else if (indentStr.Contains("+"))
                        {
                            // FIX: Treat any indentation with '+' as a node, even if the sign was swallowed by the indent group.
                            // Example: "+0x10" -> G1="+" G2="" G3="0x10".
                            isNode = true;
                        }
                        else if (hasIndent && hasSeparator)
                        {
                            // Indented, no sign, but has separator -> Treat as Node.
                            isNode = true;
                        }
                        // Else: It remains false (Value Enum).
                    }

                    if (isNode)
                    {
                        // Determine indentation depth
                        string indentStr = match.Groups[1].Value;
                        int indent = 0;
                        if (indentStr.Contains("+"))
                        {
                            // Standard/Mixed format
                            if (indentStr.Contains("."))
                            {
                                // Dot-based (e.g., ".+") -> count dots
                                indent = indentStr.Count(c => c == '.');
                            }
                            else
                            {
                                // Pure plus-based (e.g., "++") -> count pluses minus 1
                                // + = 0, ++ = 1, etc.
                                indent = indentStr.Count(c => c == '+') - 1;
                                if (indent < 0) indent = 0;
                            }
                        }
                        else
                        {
                            // Non-standard format: just dots.
                            indent = indentStr.Count(c => c == '.');
                        }

                        string sign = match.Groups[2].Value;
                        string hex = match.Groups[3].Value;
                        string desc = match.Groups[4].Value;

                        // FIX: Strip leading separators (- : =) from the description
                        // Loop to handle cases where multiple separators might exist or just whitespace
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
                        // Pop the stack until we find a node with a lower indent level (the parent).
                        while (stack.Count > 0 && stack.Peek().IndentLevel >= indent)
                            stack.Pop();

                        // Safety fallback
                        if (stack.Count == 0) stack.Push(logicalRoot);

                        var parent = stack.Peek();
                        node.Parent = parent;
                        parent.Children.Add(node);

                        // Push this node as the potential parent for the next iteration.
                        stack.Push(node);

                        lastAddedNode = node;
                    }
                    else
                    {
                        // This line is not a node (e.g., a value definition like "0x01 = True").
                        // Append it to the Content of the most recently added node.
                        if (lastAddedNode != null)
                        {
                            if (lastAddedNode.Content.Length > 0) lastAddedNode.Content += "\r\n";
                            // Clean up indentation markers so the content box looks clean.
                            string cleanContentLine = prefixStripRegex.Replace(line.Trim(), "");
                            lastAddedNode.Content += cleanContentLine;
                        }
                    }
                }
                // Recursively flatten the tree for the dropdown list.
                CollectAllNodes(logicalRoot, flatList);
            }
            return flatList;
        }

        // Parses data size from tags in the description (e.g., [16-bit] or [32 bytes]).
        private long ParseSizeFromDescription(string desc)
        {
            if (string.IsNullOrEmpty(desc)) return 1;

            // 1. Check for explicit byte count: [X bytes]
            var bytesMatch = Regex.Match(desc, @"\[(\d+)\s*bytes?\]", RegexOptions.IgnoreCase);
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

            // FIX: Robust case-insensitive pointer detection
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