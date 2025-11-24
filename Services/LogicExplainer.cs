using RALogicEditor.Definitions;
using RALogicEditor.Helpers;
using RALogicEditor.Models;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Services
{
    public static class LogicExplainer
    {
        public static string ExplainAsset(
            IAssetItem asset,
            List<AchievementConditionGroup> groups,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Logic Analysis for: {asset.Title}");
            sb.AppendLine();

            if (groups == null || groups.Count == 0)
            {
                sb.AppendLine("No logic defined.");
                return sb.ToString();
            }

            var core = groups.FirstOrDefault(g => g.GroupName == "Core");
            if (core != null && core.Conditions.Count > 0)
            {
                sb.AppendLine("## Core Requirements (Must be true):");
                ExplainGroup(sb, core, notesLookup, rangeCache);
                sb.AppendLine();
            }

            var alts = groups.Where(g => g.GroupName != "Core").ToList();
            if (alts.Count > 0)
            {
                sb.AppendLine("## Alternative Requirements (At least one set must be true):");
                foreach (var alt in alts)
                {
                    sb.AppendLine($"### {alt.GroupName}:");
                    ExplainGroup(sb, alt, notesLookup, rangeCache);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static void ExplainGroup(
            StringBuilder sb,
            AchievementConditionGroup group,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache)
        {
            // Accumulator tracking
            var accumulatorParts = new List<string>();
            bool inAccumulator = false;

            for (int i = 0; i < group.Conditions.Count; i++)
            {
                var cond = group.Conditions[i];

                // Handle AddSource/SubSource Chains (Accumulators)
                if (cond.Flag == "Add Source" || cond.Flag == "Sub Source")
                {
                    inAccumulator = true;
                    string sign = cond.Flag == "Sub Source" ? "-" : "+";
                    string part = GetOperandText(cond.LeftOperand, group, i, notesLookup, rangeCache);

                    // Strip brackets for cleaner concatenation
                    part = part.Trim('[', ']');

                    accumulatorParts.Add(i == 0 || !accumulatorParts.Any() ? part : $"{sign} {part}");
                    continue;
                }

                // Check for AddAddress chains (handled implicitly by ExplainCondition via ResolveAlias, 
                // but we need to skip them in the main loop if they are just setup)
                if (cond.Flag == "Add Address" || cond.Flag == "AndNext" || cond.Flag == "OrNext") continue;

                // If we were accumulating, this condition is likely the check (e.g. 0 = CONST)
                if (inAccumulator)
                {
                    // Special Case: Accumulator Check
                    // If the condition is Value 0 = Value X, it means "Sum of Sources = X"
                    if (cond.LeftOperand.Type == "Value" && cond.LeftOperand.Value == "0x00000000") // Check for zero
                    {
                        string op = GetOperatorText(cond.Operator);
                        string target = GetOperandText(cond.RightOperand, group, i, notesLookup, rangeCache, true);

                        // Attempt to summarize the accumulator parts
                        // Heuristic: If parts look like "String +0x4", "String +0x8", just call it "Total [String]"
                        string summary = SummarizeAccumulator(accumulatorParts);

                        sb.AppendLine($"- Sum of ({summary}) {op} {target}");
                    }
                    else
                    {
                        // Standard check incorporating the accumulator
                        // e.g. Mem > 0 (where Mem is added to source) - Rare in RA but possible
                        // We treat the current condition as part of the logic, but mention the sum
                        string explanation = ExplainCondition(cond, group, i, notesLookup, rangeCache);
                        string summary = SummarizeAccumulator(accumulatorParts);
                        sb.AppendLine($"- [Accumulated: {summary}] + {explanation}");
                    }

                    // Reset
                    accumulatorParts.Clear();
                    inAccumulator = false;
                    continue;
                }

                string expl = ExplainCondition(cond, group, i, notesLookup, rangeCache);
                string prefix = "- ";
                if (cond.Flag == "Reset If") prefix = "- [RESET] ";
                else if (cond.Flag == "Pause If") prefix = "- [PAUSE] ";
                else if (cond.Flag == "Measured") prefix = "- [MEASURE] ";
                else if (cond.Flag == "Trigger") prefix = "- [TRIGGER] ";

                sb.AppendLine($"{prefix}{expl}");
            }
        }

        private static string SummarizeAccumulator(List<string> parts)
        {
            if (parts.Count == 0) return "Nothing";
            if (parts.Count == 1) return parts[0];

            // Heuristic: Check if all parts share a common base name
            // e.g. "Level ID (ASCII)", "Level ID (ASCII) +0x4"...
            string commonBase = "";
            string first = parts[0];
            int parenIdx = first.LastIndexOf('('); // Try to find logical grouping like "Name (Details)"
            if (parenIdx > 0) commonBase = first.Substring(0, parenIdx).Trim();
            else commonBase = first.Split('+')[0].Trim();

            if (!string.IsNullOrEmpty(commonBase) && parts.All(p => p.StartsWith(commonBase)))
            {
                return $"Total {commonBase}";
            }

            return string.Join(" ", parts);
        }

        private static string ExplainCondition(
            AchievementCondition cond,
            AchievementConditionGroup group,
            int index,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache)
        {
            // 1. Delta Check
            if (cond.LeftOperand.Type == "Mem" && cond.RightOperand.Type == "Delta" &&
                cond.LeftOperand.Value == cond.RightOperand.Value)
            {
                string name = GetName(cond.LeftOperand.Value, group, index, notesLookup, rangeCache);
                switch (cond.Operator)
                {
                    case ">": return $"{name} increased";
                    case "<": return $"{name} decreased";
                    case "!=": return $"{name} changed";
                    case "=": return $"{name} remained same";
                }
            }

            // 2. Standard Comparison
            string left = GetOperandText(cond.LeftOperand, group, index, notesLookup, rangeCache);
            string right = GetOperandText(cond.RightOperand, group, index, notesLookup, rangeCache, true);

            // ENUM PARSING: If Right is a Value, try to find an enum definition in Left's note
            if (cond.RightOperand.Type == "Value")
            {
                string enumLabel = TryGetEnumLabel(cond.LeftOperand.Value, cond.RightOperand.Value, group, index, notesLookup, rangeCache);
                if (!string.IsNullOrEmpty(enumLabel)) right = enumLabel;
            }

            string op = GetOperatorText(cond.Operator);
            string hits = cond.RequiredHits > 0 ? $" (for {cond.RequiredHits} frames)" : "";

            return $"{left} {op} {right}{hits}";
        }

        private static string TryGetEnumLabel(
            string addr,
            string value,
            AchievementConditionGroup group,
            int index,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache)
        {
            // Resolve the alias to find the relevant note node
            string alias = ConditionFormatter.ResolveAlias(LogicFormatter.NormalizeAddress(addr), group, index, notesLookup, rangeCache);
            if (string.IsNullOrEmpty(alias)) return "";

            // We need to find the actual NoteNode to parse its content for enums
            // ConditionFormatter returns string, so we re-do the lookup logic simplified here to get the node content
            // Warning: This is expensive but necessary for this feature.
            // Ideally ConditionFormatter would return the Node, but for now we look it up again.

            // 1. Direct Lookup
            string normAddr = LogicFormatter.NormalizeAddress(addr);
            if (notesLookup.TryGetValue(normAddr, out var nodes))
            {
                var node = nodes.FirstOrDefault(n => n.IndentLevel == -2) ?? nodes.FirstOrDefault();
                if (node != null) return ParseEnumFromContent(node.Content, value);
            }

            // 2. Range Lookup (if direct failed)
            // (Simplified: Enums usually on base notes, not struct offsets)

            return "";
        }

        private static string ParseEnumFromContent(string content, string value)
        {
            if (string.IsNullOrEmpty(content)) return "";

            // Parse hex value to normalize (0x01 vs 0x1)
            long targetVal = 0;
            if (value.StartsWith("0x")) long.TryParse(value.Substring(2), NumberStyles.HexNumber, null, out targetVal);
            else long.TryParse(value, out targetVal);

            // Regex to find lines like "0x01=In-game" or "1:Start"
            // Supports hex (0x) or decimal prefixes
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"^\s*(?:0x)?([0-9A-Fa-f]+)\s*[:=]\s*(.+)");
                if (match.Success)
                {
                    long lineVal = 0;
                    // Try parsing as hex first (common in notes)
                    if (!long.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, null, out lineVal)) continue;

                    if (lineVal == targetVal)
                    {
                        return match.Groups[2].Value.Trim();
                    }
                }
            }
            return "";
        }

        private static string GetOperandText(
            Operand op,
            AchievementConditionGroup group,
            int index,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache,
            bool isRight = false)
        {
            if (op.Type == "Value") return op.Value;
            if (op.Type == "Float") return op.Value;

            string prefix = "";
            if (op.Type == "Delta") prefix = "previous ";
            if (op.Type == "Prior") prefix = "prior ";

            string name = ConditionFormatter.ResolveAlias(
                LogicFormatter.NormalizeAddress(op.Value),
                group,
                index,
                notesLookup,
                rangeCache);

            if (string.IsNullOrEmpty(name)) name = $"Memory {op.Value}";
            else name = $"[{name}]";

            if (string.IsNullOrEmpty(name)) name = op.Value;

            return $"{prefix}{name}";
        }

        private static string GetName(string addr, AchievementConditionGroup group, int index, Dictionary<string, List<NoteNode>> notes, List<(long Start, long End, NoteNode Node)> range)
        {
            string alias = ConditionFormatter.ResolveAlias(LogicFormatter.NormalizeAddress(addr), group, index, notes, range);
            return string.IsNullOrEmpty(alias) ? $"Memory {addr}" : $"[{alias}]";
        }

        private static string GetOperatorText(string op)
        {
            return op switch
            {
                "=" => "is",
                "!=" => "is not",
                "<" => "is less than",
                "<=" => "is <=",
                ">" => "is greater than",
                ">=" => "is >=",
                _ => op
            };
        }
    }
}