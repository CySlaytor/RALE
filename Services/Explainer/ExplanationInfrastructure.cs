using RALogicEditor.Definitions;
using RALogicEditor.Helpers;
using RALogicEditor.Models;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace RALogicEditor.Services.Explainer
{
    public enum ExplanationCategory
    {
        Context,
        Transition,
        Complex, // Accumulators / Measured / Trigger
        Reset,
        Pause,
        MeasuredIf
    }

    public class ProcessorResult
    {
        public string Text { get; set; } = "";
        public int ConditionsConsumed { get; set; } = 1;
        public ExplanationCategory Category { get; set; } = ExplanationCategory.Context;

        // Dictionary for drill-down HTML details (e.g. Arrays)
        public Dictionary<string, string> DetailedDrillDowns { get; set; } = new();
    }

    public interface ILogicProcessor
    {
        // Lower number = checked first
        int Priority { get; }
        bool CanProcess(List<AchievementCondition> conditions, int currentIndex);
        ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int currentIndex);
    }

    public class ExplanationContext
    {
        public AchievementConditionGroup Group { get; }
        public Dictionary<string, List<NoteNode>> NotesLookup { get; }
        public List<(long Start, long End, NoteNode Node)> RangeCache { get; }
        public bool ShowDecimal { get; }

        public ExplanationContext(
            AchievementConditionGroup group,
            Dictionary<string, List<NoteNode>> notes,
            List<(long Start, long End, NoteNode Node)> range,
            bool showDec)
        {
            Group = group;
            NotesLookup = notes;
            RangeCache = range;
            ShowDecimal = showDec;
        }

        // Shared Helper: Get operand name (Memory 0x1234 or [Alias])
        public string GetName(string addr, int index, bool suppressPointers = false)
        {
            if (string.IsNullOrEmpty(addr)) return "";

            string alias = ConditionFormatter.ResolveAlias(LogicFormatter.NormalizeAddress(addr), Group, index, NotesLookup, RangeCache);
            if (!string.IsNullOrEmpty(alias)) return $"[{alias}]";

            if (Group != null && index > 0 && index < Group.Conditions.Count)
            {
                if (Group.Conditions[index - 1].Flag == "Add Address")
                {
                    if (suppressPointers)
                        return $"[... + {addr}]";
                    return $"[Pointer ... + {addr}]";
                }
            }

            return $"Memory {addr}";
        }

        // Shared Helper: Format values (0x10 vs 16)
        public string FormatValue(Operand op, int index, string contextAddress = "")
        {
            if (op.Type == "Float") return op.Value;

            if (!string.IsNullOrEmpty(contextAddress))
            {
                string enumLabel = TryGetEnumLabel(contextAddress, op.Value, index);
                if (!string.IsNullOrEmpty(enumLabel)) return $"\"{enumLabel}\"";
            }

            if (long.TryParse(op.Value.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val))
            {
                if (ShowDecimal) return val.ToString();
                if (op.Value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return op.Value;
                return "0x" + val.ToString("X");
            }

            return op.Value;
        }

        // Shared Helper: Get operand text with prefix (delta/prior)
        public string GetOperandText(Operand op, int index, bool isRight = false, string contextAddress = "", bool suppressPointers = false)
        {
            if (op.Type == "Recall") return "{recall}";

            if (op.Type == "Value" || op.Type == "Float")
                return FormatValue(op, index, contextAddress);

            string prefix = "";
            if (op.Type == "Delta") prefix = "previous frame ";
            if (op.Type == "Prior") prefix = "prior frame ";

            string name = GetName(op.Value, index, suppressPointers);
            return $"{prefix}{name}";
        }

        // Shared Helper: Operator to string
        public string GetOperatorText(string op)
        {
            return op switch
            {
                "=" => "is",
                "!=" => "is not",
                "<" => "is less than",
                "<=" => "is at most",
                ">" => "is greater than",
                ">=" => "is at least",
                _ => op
            };
        }

        private string TryGetEnumLabel(string addr, string value, int index)
        {
            long ParseHex(string s)
            {
                string clean = s.Replace("0x", "").Trim();
                clean = Regex.Replace(clean, "[^0-9A-Fa-f]", "");
                return long.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long v) ? v : -1;
            }

            long targetVal = ParseHex(addr);

            // Lookup Logic: Pointer Chain Resolution
            if (Group != null && index > 0)
            {
                var chainStack = new Stack<string>();
                int scan = index - 1;
                while (scan >= 0)
                {
                    var prevCond = Group.Conditions[scan];
                    if (prevCond.Flag == "Add Address")
                    {
                        chainStack.Push(prevCond.LeftOperand.Value);
                        scan--;
                    }
                    else break;
                }

                if (chainStack.Count > 0)
                {
                    string baseAddrStr = chainStack.Pop();
                    string normalizedBase = LogicFormatter.NormalizeAddress(baseAddrStr);

                    if (NotesLookup.TryGetValue(normalizedBase, out var nodes))
                    {
                        var currentLevelNodes = nodes.Where(n => n.IndentLevel != -2 && (n.Parent == null || n.Parent.IndentLevel == -1)).ToList();
                        bool chainValid = true;

                        while (chainStack.Count > 0)
                        {
                            string offsetStr = chainStack.Pop();
                            long offset = ParseHex(offsetStr);
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
                                    string enumLabel = ParseEnumFromContent(node.Content, value);
                                    if (!string.IsNullOrEmpty(enumLabel)) return enumLabel;
                                }
                            }
                        }
                    }
                }
            }

            string normAddr = LogicFormatter.NormalizeAddress(addr);

            if (NotesLookup.TryGetValue(normAddr, out var lookupNodes))
            {
                var node = lookupNodes.FirstOrDefault(n => n.IndentLevel == -2) ?? lookupNodes.FirstOrDefault();
                if (node != null) return ParseEnumFromContent(node.Content, value);
            }

            if (RangeCache != null)
            {
                if (targetVal != -1)
                {
                    var match = RangeCache.FirstOrDefault(r => targetVal >= r.Start && targetVal < r.End);
                    if (match.Node != null) return ParseEnumFromContent(match.Node.Content, value);
                }
            }

            return "";
        }

        private string ParseEnumFromContent(string content, string value)
        {
            if (string.IsNullOrEmpty(content)) return "";

            long targetVal = 0;
            if (value.StartsWith("0x")) long.TryParse(value.Substring(2), NumberStyles.HexNumber, null, out targetVal);
            else long.TryParse(value, out targetVal);

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"^\s*(?:0x)?([0-9A-Fa-f]+)\s*[:=]\s*(.+)");
                if (match.Success)
                {
                    long lineVal = 0;
                    if (!long.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, null, out lineVal)) continue;

                    if (lineVal == targetVal)
                    {
                        return match.Groups[2].Value.Trim();
                    }
                }
            }
            return "";
        }
    }
}