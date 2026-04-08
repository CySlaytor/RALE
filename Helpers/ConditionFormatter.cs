#nullable enable
using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace RALogicEditor.Helpers
{
    // Responsibility: Formats raw model data into display strings (Values, Aliases)
    public static class ConditionFormatter
    {
        private class ChainNode { public bool IsMem; public string Value = ""; }

        public static string FormatOperandDisplay(
            Operand op,
            bool showDecimal,
            bool showAliases,
            Dictionary<string, List<NoteNode>>? notesLookup,
            List<(long Start, long End, NoteNode Node)>? rangeCache,
            AchievementConditionGroup? group,
            int rowIndex)
        {
            // If Alias Mode is ON and this is a Memory type
            if (showAliases && RaLogicSyntax.MemoryTypes.Contains(op.Type) && notesLookup != null)
            {
                string addr = LogicFormatter.NormalizeAddress(op.Value);
                string alias = ResolveAlias(addr, group, rowIndex, notesLookup, rangeCache);
                if (!string.IsNullOrEmpty(alias))
                {
                    return alias.Trim();
                }
            }

            string sizeRef = "";
            return LogicFormatter.FormatDisplayValue(op, showDecimal, sizeRef);
        }

        // Overload to support passing sizeRef explicitly
        public static string FormatOperandDisplay(
            Operand op,
            bool showDecimal,
            bool showAliases,
            Dictionary<string, List<NoteNode>>? notesLookup,
            List<(long Start, long End, NoteNode Node)>? rangeCache,
            AchievementConditionGroup? group,
            int rowIndex,
            string sizeRef)
        {
            if (showAliases && RaLogicSyntax.MemoryTypes.Contains(op.Type) && notesLookup != null)
            {
                string addr = LogicFormatter.NormalizeAddress(op.Value);
                string alias = ResolveAlias(addr, group, rowIndex, notesLookup, rangeCache);
                if (!string.IsNullOrEmpty(alias)) return alias.Trim();
            }
            return LogicFormatter.FormatDisplayValue(op, showDecimal, sizeRef);
        }

        // Changed to PUBLIC to allow the Preview Control to use this logic
        public static string ResolveAlias(
            string address,
            AchievementConditionGroup? group,
            int rowIndex,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)>? rangeCache)
        {
            // Robust Hex Parser: Removes 0x and any non-hex size prefixes (W, H, X, etc.)
            long ParseHex(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return -1;
                string clean = s.Replace("0x", "").Trim();
                // Regex to keep only valid hex characters 0-9, A-F
                clean = Regex.Replace(clean, "[^0-9A-Fa-f]", "");
                return long.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long v) ? v : -1;
            }

            long targetVal = ParseHex(address);

            // 1. Strict Pointer Chain Logic (Offsets)
            // Fix: Check bounds before accessing group.Conditions
            if (group != null && group.Conditions != null && rowIndex > 0 && rowIndex < group.Conditions.Count)
            {
                var chainStack = new List<ChainNode>();
                int scan = rowIndex - 1;
                while (scan >= 0)
                {
                    // Additional check to prevent index out of range if group modified externally
                    if (scan >= group.Conditions.Count)
                    {
                        scan--;
                        continue;
                    }

                    var prevCond = group.Conditions[scan];
                    if (prevCond.Flag == "Add Address")
                    {
                        // Add to front of list to maintain top-to-bottom order for indexing filtering
                        chainStack.Insert(0, new ChainNode
                        {
                            IsMem = RaLogicSyntax.MemoryTypes.Contains(prevCond.LeftOperand.Type),
                            Value = prevCond.LeftOperand.Value
                        });
                        scan--;
                    }
                    else break;
                }

                // Handle Array Indexing (Index + Base) imported from AutoCR
                // If an AddAddress chain starts with something that has no code note, we assume it is an Index.
                while (chainStack.Count > 1)
                {
                    var first = chainStack[0];
                    long baseAddrNum = ParseHex(LogicFormatter.NormalizeAddress(first.Value));
                    if (!first.IsMem || GetEffectiveNote(notesLookup, baseAddrNum) == null)
                    {
                        chainStack.RemoveAt(0);
                    }
                    else
                    {
                        break;
                    }
                }
                // Ensure we don't accidentally preserve a non-address value if it's the only one left
                if (chainStack.Count == 1)
                {
                    var first = chainStack[0];
                    long baseAddrNum = ParseHex(LogicFormatter.NormalizeAddress(first.Value));
                    if (!first.IsMem || GetEffectiveNote(notesLookup, baseAddrNum) == null)
                    {
                        chainStack.RemoveAt(0);
                    }
                }

                if (chainStack.Count > 0)
                {
                    var baseNode = chainStack[0];
                    chainStack.RemoveAt(0);
                    string normalizedBase = LogicFormatter.NormalizeAddress(baseNode.Value);

                    if (notesLookup.TryGetValue(normalizedBase, out var nodes))
                    {
                        // Redirect Resolution Logic added here
                        // Check if the Base Note is a Redirect (refer to $0x...)
                        List<NoteNode> workingNodes = nodes;
                        int redirects = 0;
                        while (redirects < 5 && workingNodes.Count > 0)
                        {
                            var root = workingNodes.FirstOrDefault(n => n.IndentLevel == -2) ?? workingNodes[0];
                            string content = root.Content ?? "";

                            if (content.Contains("refer to", StringComparison.OrdinalIgnoreCase))
                            {
                                var match = Regex.Match(content, @"refer to \$0x([0-9a-f]+)", RegexOptions.IgnoreCase);
                                if (match.Success)
                                {
                                    string targetAddr = match.Groups[1].Value;
                                    string normTarget = LogicFormatter.NormalizeAddress(targetAddr);
                                    if (notesLookup.TryGetValue(normTarget, out var targetNodes))
                                    {
                                        workingNodes = targetNodes;
                                        redirects++;
                                        continue;
                                    }
                                }
                            }
                            break;
                        }

                        var currentLevelNodes = workingNodes.Where(n => n.IndentLevel != -2 && (n.Parent == null || n.Parent.IndentLevel == -1)).ToList();
                        bool chainValid = true;

                        while (chainStack.Count > 0)
                        {
                            var offsetNode = chainStack[0];
                            chainStack.RemoveAt(0);
                            long offset = ParseHex(offsetNode.Value);
                            if (offset == -1) { chainValid = false; break; }

                            NoteNode? match = null;
                            foreach (var node in currentLevelNodes)
                            {
                                long nodeOff = ParseHex(node.Offset);
                                if (node.Offset.Contains("-")) nodeOff = -nodeOff;

                                if (offset >= nodeOff && offset < nodeOff + node.Size)
                                {
                                    match = node;
                                    break;
                                }
                            }

                            if (match != null) currentLevelNodes = match.Children;
                            else { chainValid = false; break; }
                        }

                        if (chainValid && targetVal != -1)
                        {
                            foreach (var node in currentLevelNodes)
                            {
                                long nodeOff = ParseHex(node.Offset);
                                if (node.Offset.Contains("-")) nodeOff = -nodeOff;

                                if (targetVal >= nodeOff && targetVal < nodeOff + node.Size)
                                {
                                    string desc = node.Description;
                                    desc = Regex.Replace(desc, @"\[.*?\]", "").Trim();

                                    long delta = targetVal - nodeOff;
                                    if (delta > 0)
                                    {
                                        string suffix = $" +0x{delta:X}";
                                        if (!desc.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                                            desc += suffix;
                                    }
                                    return desc;
                                }
                            }
                        }
                    }
                }
            }

            // 2. Direct Lookup
            if (notesLookup.TryGetValue(address, out var directNodes))
            {
                // Redirect Resolution Logic added here too
                List<NoteNode> workingNodes = directNodes;
                int redirects = 0;
                while (redirects < 5 && workingNodes.Count > 0)
                {
                    var root = workingNodes.FirstOrDefault(n => n.IndentLevel == -2) ?? workingNodes[0];
                    string content = root.Content ?? "";

                    if (content.Contains("refer to", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(content, @"refer to \$0x([0-9a-f]+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string targetAddr = match.Groups[1].Value;
                            string normTarget = LogicFormatter.NormalizeAddress(targetAddr);
                            if (notesLookup.TryGetValue(normTarget, out var targetNodes))
                            {
                                workingNodes = targetNodes;
                                redirects++;
                                continue;
                            }
                        }
                    }
                    break;
                }

                var rootNode = workingNodes.FirstOrDefault();
                if (rootNode != null)
                {
                    var main = workingNodes.FirstOrDefault(n => n.IndentLevel == -2) ?? workingNodes.First();
                    string desc = main.Description;

                    if (string.IsNullOrEmpty(desc) || desc.Equals("Full Note", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(main.Content))
                            desc = main.Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                    }

                    desc = Regex.Replace(desc, @"\[.*?\]", m =>
                    {
                        if (m.Value.IndexOf("Pointer", StringComparison.OrdinalIgnoreCase) >= 0) return "[Pointer]";
                        return "";
                    }).Trim();
                    desc = Regex.Replace(desc, @"\s{2,}", " ");
                    return desc;
                }
            }

            // 3. Ranged Direct Lookup
            if (rangeCache != null && targetVal != -1)
            {
                var match = rangeCache.FirstOrDefault(r => targetVal >= r.Start && targetVal < r.End);
                if (match.Node != null)
                {
                    string desc = match.Node.Description;
                    if (string.IsNullOrEmpty(desc) || desc.Equals("Full Note", StringComparison.OrdinalIgnoreCase))
                    {
                        string content = match.Node.Content;
                        desc = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                    }

                    desc = Regex.Replace(desc, @"\[.*?\]", m =>
                    {
                        if (m.Value.IndexOf("Pointer", StringComparison.OrdinalIgnoreCase) >= 0) return "[Pointer]";
                        return "";
                    }).Trim();
                    desc = Regex.Replace(desc, @"\s{2,}", " ");

                    long delta = targetVal - match.Start;
                    if (delta > 0)
                    {
                        string suffix = $" +0x{delta:X}";
                        if (!desc.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                            desc += suffix;
                    }
                    return desc;
                }
            }

            return "";
        }

        // Newly added helper to fetch a resolved note, handling redirects to mirror AutoCR
        public static NoteNode? GetEffectiveNote(Dictionary<string, List<NoteNode>> notesLookup, long addrVal)
        {
            string addrStr = "0x" + addrVal.ToString("x");
            if (!notesLookup.TryGetValue(addrStr, out var nodes))
                return null;

            List<NoteNode> workingNodes = nodes;
            int redirects = 0;

            while (redirects < 5 && workingNodes.Count > 0)
            {
                var root = workingNodes.FirstOrDefault(n => n.IndentLevel == -2) ?? workingNodes[0];
                string content = root.Content ?? "";

                if (content.Contains("refer to", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(content, @"refer to \$0x([0-9a-f]+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        string targetAddr = match.Groups[1].Value;
                        string normTarget = LogicFormatter.NormalizeAddress(targetAddr);
                        if (notesLookup.TryGetValue(normTarget, out var targetNodes))
                        {
                            workingNodes = targetNodes;
                            redirects++;
                            continue;
                        }
                    }
                }
                break;
            }

            return workingNodes.FirstOrDefault(n => n.IndentLevel == -2) ?? workingNodes.FirstOrDefault();
        }
    }
}