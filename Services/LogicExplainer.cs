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
        public static ExplanationResult ExplainAsset(
            IAssetItem asset,
            List<AchievementConditionGroup> groups,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache,
            bool showDecimal)
        {
            var result = new ExplanationResult();
            var sb = new StringBuilder();
            var details = result.DetailedInfo;

            sb.AppendLine($"# Logic Analysis for: {asset.Title}");
            sb.AppendLine();

            if (groups == null || groups.Count == 0)
            {
                sb.AppendLine("No logic defined.");
                result.MainText = sb.ToString();
                return result;
            }

            var core = groups.FirstOrDefault(g => g.GroupName == "Core");
            if (core != null && core.Conditions.Count > 0)
            {
                sb.AppendLine("## Core Requirements (Must be true):");
                sb.AppendLine(ExplainGroupNatural(core, notesLookup, rangeCache, showDecimal, details));
                sb.AppendLine();
            }

            var allAlts = groups.Where(g => g.GroupName != "Core").ToList();
            var regularAlts = new List<AchievementConditionGroup>();
            var globalResets = new List<AchievementConditionGroup>();
            var displayLogic = new List<AchievementConditionGroup>();

            foreach (var alt in allAlts)
            {
                if (alt.Conditions.Count > 0)
                {
                    if (alt.Conditions.All(c => c.Flag == "Reset If"))
                    {
                        globalResets.Add(alt);
                    }
                    else if (IsGroupDisplayOnly(alt))
                    {
                        displayLogic.Add(alt);
                    }
                    else
                    {
                        regularAlts.Add(alt);
                    }
                }
                else
                {
                    regularAlts.Add(alt);
                }
            }

            if (regularAlts.Count > 0)
            {
                sb.AppendLine("## Alternative Requirements (At least one set must be true):");

                int i = 0;
                while (i < regularAlts.Count)
                {
                    int sequenceLength = DetectArraySequence(regularAlts, i);

                    if (sequenceLength > 2)
                    {
                        var firstGroup = regularAlts[i];
                        var lastGroup = regularAlts[i + sequenceLength - 1];

                        sb.AppendLine($"### {firstGroup.GroupName} - {lastGroup.GroupName} (Array Logic):");
                        sb.AppendLine($"Trigger when any of the following {sequenceLength} slots match:");

                        string explanation = ExplainGroupNatural(firstGroup, notesLookup, rangeCache, showDecimal, details);

                        var sbIndent = new StringBuilder();
                        foreach (var line in explanation.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            sbIndent.AppendLine("  " + line);
                        }

                        string arrayId = Guid.NewGuid().ToString("N");
                        details[arrayId] = sbIndent.ToString().TrimEnd();
                        sb.AppendLine($"[[EXPAND:{arrayId}:Array Details]]");
                        sb.AppendLine();

                        i += sequenceLength;
                    }
                    else
                    {
                        sb.AppendLine($"### {regularAlts[i].GroupName}:");
                        sb.AppendLine(ExplainGroupNatural(regularAlts[i], notesLookup, rangeCache, showDecimal, details));
                        sb.AppendLine();
                        i++;
                    }
                }
            }

            if (displayLogic.Count > 0)
            {
                sb.AppendLine("## Overlay / UX Logic (Does not trigger achievement):");
                foreach (var group in displayLogic)
                {
                    sb.AppendLine($"### {group.GroupName}:");
                    string explanation = ExplainUXGroup(group, notesLookup, rangeCache, showDecimal, details);
                    sb.AppendLine(explanation);
                    sb.AppendLine();
                }
            }

            if (globalResets.Count > 0)
            {
                sb.AppendLine("## Global Reset Conditions (Applies to all):");
                var flatResets = globalResets.SelectMany(g => g.Conditions).ToList();
                var dummyGroup = new AchievementConditionGroup { GroupName = "Global", Conditions = flatResets };

                foreach (var cond in flatResets)
                {
                    string text = ExplainSingleCondition(cond, dummyGroup, 0, notesLookup, rangeCache, showDecimal);
                    sb.AppendLine($"- If {text}, reset the achievement.");
                }
            }

            result.MainText = sb.ToString();
            return result;
        }

        // Updated Logic: A group is Display Only if it has UX flags AND contains at least one "False" condition 
        // that is NOT part of a functional accumulator chain.
        private static bool IsGroupDisplayOnly(AchievementConditionGroup group)
        {
            bool hasUXFlag = group.Conditions.Any(c => c.Flag.StartsWith("Measured") || c.Flag == "Trigger");
            if (!hasUXFlag) return false;

            int impossibleCount = 0;
            int validAccumulatorTerminators = 0;

            for (int i = 0; i < group.Conditions.Count; i++)
            {
                if (IsConditionAlwaysFalse(group.Conditions[i]))
                {
                    impossibleCount++;

                    // Check if this False condition is the terminator for a valid accumulator chain
                    if (i > 0)
                    {
                        string prevFlag = group.Conditions[i - 1].Flag;
                        if (prevFlag == "Add Hits" || prevFlag == "Sub Hits" ||
                            prevFlag == "Add Source" || prevFlag == "Sub Source")
                        {
                            validAccumulatorTerminators++;
                        }
                    }
                }
            }

            // If there are more impossible conditions than there are accumulator terminators,
            // it means there is at least one "Naked" 0=1 or T:0=1 that effectively disables the group.
            return impossibleCount > validAccumulatorTerminators;
        }

        private static bool IsConditionAlwaysFalse(AchievementCondition c)
        {
            if (c.LeftOperand.Type != "Value" || c.RightOperand.Type != "Value") return false;
            if (!TryParseLong(c.LeftOperand.Value, out long left)) return false;
            if (!TryParseLong(c.RightOperand.Value, out long right)) return false;

            return c.Operator switch
            {
                "=" => left != right,
                "!=" => left == right,
                "<" => !(left < right),
                "<=" => !(left <= right),
                ">" => !(left > right),
                ">=" => !(left >= right),
                _ => false
            };
        }

        private static bool TryParseLong(string input, out long result)
        {
            string clean = input.Replace("0x", "").Trim();
            return long.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
        }

        private static int DetectArraySequence(List<AchievementConditionGroup> alts, int startIndex)
        {
            if (startIndex >= alts.Count - 1) return 0;
            var baseGroup = alts[startIndex];
            string baseFingerprint = GetGroupFingerprint(baseGroup);

            int count = 1;
            for (int i = startIndex + 1; i < alts.Count; i++)
            {
                if (GetGroupFingerprint(alts[i]) == baseFingerprint) count++;
                else break;
            }
            return count;
        }

        private static int DetectConditionSequence(List<List<AchievementCondition>> blocks, int startIndex)
        {
            if (startIndex >= blocks.Count - 1) return 0;
            string baseFp = GetBlockFingerprint(blocks[startIndex]);

            int count = 1;
            for (int i = startIndex + 1; i < blocks.Count; i++)
            {
                if (GetBlockFingerprint(blocks[i]) == baseFp) count++;
                else break;
            }
            return count;
        }

        private static string GetGroupFingerprint(AchievementConditionGroup group)
        {
            var sb = new StringBuilder();
            foreach (var c in group.Conditions) sb.Append(GetConditionFingerprint(c));
            return sb.ToString();
        }

        private static string GetBlockFingerprint(List<AchievementCondition> block)
        {
            var sb = new StringBuilder();
            foreach (var c in block) sb.Append(GetConditionFingerprint(c));
            return sb.ToString();
        }

        private static string GetConditionFingerprint(AchievementCondition c)
        {
            var sb = new StringBuilder();
            sb.Append(c.Flag).Append('|');
            sb.Append(c.LeftOperand.Type).Append('|');
            if (c.LeftOperand.Type != "Mem" && c.LeftOperand.Type != "Delta" && c.LeftOperand.Type != "Prior")
                sb.Append(c.LeftOperand.Value);

            sb.Append('|').Append(c.Operator).Append('|');
            sb.Append(c.RightOperand.Type).Append('|');
            if (c.RightOperand.Type != "Mem" && c.RightOperand.Type != "Delta" && c.RightOperand.Type != "Prior")
                sb.Append(c.RightOperand.Value);

            sb.Append('|').Append(c.RequiredHits).Append(';');
            return sb.ToString();
        }

        private class GroupAnalysis
        {
            public List<string> Contexts = new();
            public List<string> Transitions = new();
            public List<string> Resets = new();
            public List<string> Pauses = new();
            public List<string> ComplexLogic = new();
        }

        private static List<List<AchievementCondition>> SegmentLogicBlocks(List<AchievementCondition> conditions)
        {
            var blocks = new List<List<AchievementCondition>>();
            var currentBlock = new List<AchievementCondition>();

            foreach (var c in conditions)
            {
                currentBlock.Add(c);
                if (c.Flag == "Add Hits" || c.Flag == "Sub Hits" ||
                    (!c.Flag.Contains("Next") && c.Flag != "Add Address"))
                {
                    blocks.Add(new List<AchievementCondition>(currentBlock));
                    currentBlock.Clear();
                }
            }
            if (currentBlock.Count > 0) blocks.Add(currentBlock);

            return blocks;
        }

        private static string ExplainUXGroup(
            AchievementConditionGroup group,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache,
            bool showDecimal,
            Dictionary<string, string> details)
        {
            var analysis = new GroupAnalysis();
            string measuredTarget = "";
            string triggerReason = "";
            bool hasMeasured = false;
            bool hasTrigger = false;

            var conditions = group.Conditions;
            var usedIndices = new HashSet<int>();

            for (int i = 0; i < conditions.Count; i++)
            {
                if (usedIndices.Contains(i)) continue;
                var cond = conditions[i];

                if (cond.Flag == "Add Source" || cond.Flag == "Sub Source" || cond.Flag == "Add Hits" || cond.Flag == "Sub Hits")
                {
                    var chainRaw = new List<AchievementCondition>();
                    int ptr = i;
                    uint required = 0;
                    bool targetIsMeasured = false;
                    bool isHitChain = (cond.Flag.Contains("Hits"));

                    while (ptr < conditions.Count)
                    {
                        var c = conditions[ptr];
                        usedIndices.Add(ptr);

                        if (c.Flag == "Add Hits" || c.Flag == "Sub Hits" ||
                            c.Flag == "Add Source" || c.Flag == "Sub Source" ||
                            c.Flag == "AndNext" || c.Flag == "OrNext" || c.Flag == "Add Address")
                        {
                            chainRaw.Add(c);
                        }
                        else
                        {
                            required = c.RequiredHits;
                            if (c.Flag.StartsWith("Measured")) targetIsMeasured = true;
                            if (required == 0 && c.RightOperand.Type == "Value")
                                uint.TryParse(c.RightOperand.Value.Replace("0x", ""), NumberStyles.HexNumber, null, out required);
                            break;
                        }
                        ptr++;
                    }

                    var blocks = SegmentLogicBlocks(chainRaw);
                    var sbChain = new StringBuilder();
                    int bIdx = 0;
                    while (bIdx < blocks.Count)
                    {
                        int seqLen = DetectConditionSequence(blocks, bIdx);
                        if (seqLen > 2)
                        {
                            var exampleBlock = blocks[bIdx];
                            var exampleLines = new List<string>();
                            foreach (var c in exampleBlock)
                                exampleLines.Add(ExplainSingleCondition(c, group, 0, notesLookup, rangeCache, showDecimal, true));

                            sbChain.AppendLine($"- {seqLen} conditions matching pattern (Array):");
                            sbChain.AppendLine($"  > {string.Join(" AND ", exampleLines)}");
                            bIdx += seqLen;
                        }
                        else
                        {
                            var block = blocks[bIdx];
                            var blockLines = new List<string>();
                            foreach (var c in block)
                                blockLines.Add(ExplainSingleCondition(c, group, 0, notesLookup, rangeCache, showDecimal, true));
                            sbChain.AppendLine($"- {string.Join(" AND ", blockLines)}");
                            bIdx++;
                        }
                    }

                    string id = Guid.NewGuid().ToString("N");
                    details[id] = sbChain.ToString().TrimEnd();
                    string expandToken = $"[[EXPAND:{id}:{blocks.Count}]]";

                    if (targetIsMeasured)
                    {
                        hasMeasured = true;
                        string label = isHitChain ? "hits" : "value";
                        measuredTarget = $"{label} up to {required} {expandToken}";
                    }
                    else
                    {
                        analysis.ComplexLogic.Add($"Trigger when at least {required} of the following occur: {expandToken}");
                    }
                    continue;
                }

                if (IsConditionAlwaysFalse(cond)) continue;

                if (cond.Flag.StartsWith("Measured"))
                {
                    hasMeasured = true;
                    string name = GetOperandText(cond.LeftOperand, group, i, notesLookup, rangeCache, showDecimal);
                    string limit = GetOperandText(cond.RightOperand, group, i, notesLookup, rangeCache, showDecimal, true);

                    if (cond.Flag == "Measured%") measuredTarget = $"{name} as %";
                    else
                    {
                        if (cond.Operator == "=" || cond.Operator == ">=" || cond.Operator == ">")
                            measuredTarget = $"{name} up to {limit}";
                        else
                            measuredTarget = $"{name} ({GetOperatorText(cond.Operator)} {limit})";
                    }
                }
                else if (cond.Flag == "Trigger")
                {
                    hasTrigger = true;
                    triggerReason = ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal);
                }
                else
                {
                    analysis.Contexts.Add(ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal));
                }
            }

            var sb = new StringBuilder();
            string context = analysis.Contexts.Count > 0 ? $"If {string.Join(", ", analysis.Contexts)}, " : "";

            if (hasMeasured) sb.Append($"{context}start measuring {measuredTarget}.");
            else if (hasTrigger) sb.Append($"{context}show the Trigger icon when {triggerReason}.");
            else if (analysis.ComplexLogic.Count > 0)
            {
                sb.AppendLine($"{context}perform logic:");
                foreach (var l in analysis.ComplexLogic) sb.AppendLine($"- {l}");
            }
            else sb.Append($"{context}perform display logic.");

            return sb.ToString();
        }

        private static string ExplainGroupNatural(
            AchievementConditionGroup group,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache,
            bool showDecimal,
            Dictionary<string, string> details)
        {
            var analysis = new GroupAnalysis();
            var conditions = group.Conditions;
            var usedIndices = new HashSet<int>();

            for (int i = 0; i < conditions.Count; i++)
            {
                if (usedIndices.Contains(i)) continue;
                var cond = conditions[i];

                if (cond.Flag == "Add Source" || cond.Flag == "Sub Source" || cond.Flag == "Add Hits" || cond.Flag == "Sub Hits")
                {
                    var chainRaw = new List<AchievementCondition>();
                    int ptr = i;
                    uint required = 0;
                    bool isHitChain = (cond.Flag.Contains("Hits"));
                    bool targetIsMeasured = false;

                    while (ptr < conditions.Count)
                    {
                        var c = conditions[ptr];
                        usedIndices.Add(ptr);

                        if (c.Flag.Contains("Add") || c.Flag.Contains("Sub") ||
                            c.Flag.Contains("Next") || c.Flag == "Add Address")
                        {
                            chainRaw.Add(c);
                        }
                        else
                        {
                            required = c.RequiredHits;
                            if (c.Flag.StartsWith("Measured")) targetIsMeasured = true;
                            if (required == 0 && c.RightOperand.Type == "Value")
                                uint.TryParse(c.RightOperand.Value.Replace("0x", ""), NumberStyles.HexNumber, null, out required);
                            break;
                        }
                        ptr++;
                    }

                    var blocks = SegmentLogicBlocks(chainRaw);
                    var sbChain = new StringBuilder();
                    int bIdx = 0;
                    while (bIdx < blocks.Count)
                    {
                        int seqLen = DetectConditionSequence(blocks, bIdx);
                        if (seqLen > 2)
                        {
                            var exampleBlock = blocks[bIdx];
                            var exampleLines = new List<string>();
                            foreach (var c in exampleBlock)
                                exampleLines.Add(ExplainSingleCondition(c, group, 0, notesLookup, rangeCache, showDecimal, true));

                            sbChain.AppendLine($"- {seqLen} conditions matching pattern (Array):");
                            sbChain.AppendLine($"  > {string.Join(" AND ", exampleLines)}");
                            bIdx += seqLen;
                        }
                        else
                        {
                            var block = blocks[bIdx];
                            var blockLines = new List<string>();
                            foreach (var c in block)
                                blockLines.Add(ExplainSingleCondition(c, group, 0, notesLookup, rangeCache, showDecimal, true));
                            sbChain.AppendLine($"- {string.Join(" AND ", blockLines)}");
                            bIdx++;
                        }
                    }

                    string id = Guid.NewGuid().ToString("N");
                    details[id] = sbChain.ToString().TrimEnd();
                    string expandToken = $"[[EXPAND:{id}:{blocks.Count}]]";

                    if (isHitChain)
                    {
                        if (targetIsMeasured)
                            analysis.ComplexLogic.Add($"Start measuring hits up to {required}: {expandToken}");
                        else
                            analysis.ComplexLogic.Add($"Trigger when at least {required} of the following occur: {expandToken}");
                    }
                    else
                    {
                        analysis.ComplexLogic.Add($"Sum of {blocks.Count} items {expandToken} is at least {required}");
                    }
                    continue;
                }
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                if (usedIndices.Contains(i)) continue;
                var cond = conditions[i];

                if (cond.Flag == "Reset If")
                {
                    analysis.Resets.Add(ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal));
                    continue;
                }
                if (cond.Flag == "Pause If")
                {
                    string expl = ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal);
                    if (cond.RequiredHits > 0)
                        analysis.Pauses.Add($"If {expl}, permanently lock the achievement.");
                    else
                        analysis.Pauses.Add($"If {expl}, pause the achievement.");
                    continue;
                }
                if (cond.Flag == "Add Address" || cond.Flag == "AndNext" || cond.Flag == "OrNext") continue;

                bool hasDelta = cond.LeftOperand.Type == "Delta" || cond.RightOperand.Type == "Delta" || cond.LeftOperand.Type == "Prior";

                if (hasDelta) analysis.Transitions.Add(ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal));
                else analysis.Contexts.Add(ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal));
            }

            var lines = new List<string>();

            if (analysis.Contexts.Count > 0)
            {
                foreach (var ctx in analysis.Contexts)
                    lines.Add($"- {ctx}.");
            }

            if (analysis.Transitions.Count > 0)
            {
                lines.Add($"- Trigger when {string.Join(" AND ", analysis.Transitions)}.");
            }

            if (analysis.ComplexLogic.Count > 0)
            {
                foreach (var line in analysis.ComplexLogic)
                    lines.Add($"- {line}");
            }

            if (analysis.Pauses.Count > 0)
            {
                foreach (var line in analysis.Pauses)
                    lines.Add($"- {line}");
            }

            if (analysis.Resets.Count > 0)
            {
                foreach (var line in analysis.Resets)
                    lines.Add($"- If {line}, reset the achievement.");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string ExplainSingleCondition(
            AchievementCondition cond,
            AchievementConditionGroup group,
            int index,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache,
            bool showDecimal,
            bool suppressHits = false)
        {
            if (cond.LeftOperand.Type == "Mem" && cond.RightOperand.Type == "Delta" &&
                cond.LeftOperand.Value == cond.RightOperand.Value)
            {
                string name = GetName(cond.LeftOperand.Value, group, index, notesLookup, rangeCache);
                switch (cond.Operator)
                {
                    case ">": return $"{name} increases";
                    case "<": return $"{name} decreases";
                    case "!=": return $"{name} changes";
                    case "=": return $"{name} remains unchanged";
                }
            }

            string left = GetOperandText(cond.LeftOperand, group, index, notesLookup, rangeCache, showDecimal);
            string right = GetOperandText(cond.RightOperand, group, index, notesLookup, rangeCache, showDecimal, true, cond.LeftOperand.Value);
            string op = GetOperatorText(cond.Operator);
            string hits = (!suppressHits && cond.RequiredHits > 0) ? $" (occurs {cond.RequiredHits} times)" : "";

            return $"{left} {op} {right}{hits}";
        }

        private static string GetOperandText(
            Operand op,
            AchievementConditionGroup group,
            int index,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache,
            bool showDecimal,
            bool isRight = false,
            string contextAddress = "")
        {
            if (op.Type == "Value" || op.Type == "Float")
                return FormatValue(op, group, index, notesLookup, rangeCache, showDecimal, contextAddress);

            string prefix = "";
            if (op.Type == "Delta") prefix = "previous ";
            if (op.Type == "Prior") prefix = "prior ";

            string name = GetName(op.Value, group, index, notesLookup, rangeCache);
            return $"{prefix}{name}";
        }

        private static string GetName(string addr, AchievementConditionGroup group, int index, Dictionary<string, List<NoteNode>> notes, List<(long Start, long End, NoteNode Node)> range)
        {
            string alias = ConditionFormatter.ResolveAlias(LogicFormatter.NormalizeAddress(addr), group, index, notes, range);
            return string.IsNullOrEmpty(alias) ? $"Memory {addr}" : $"[{alias}]";
        }

        private static string FormatValue(
            Operand op,
            AchievementConditionGroup group,
            int index,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache,
            bool showDecimal,
            string contextAddress = "")
        {
            if (op.Type == "Float") return op.Value;

            if (!string.IsNullOrEmpty(contextAddress))
            {
                string enumLabel = TryGetEnumLabel(contextAddress, op.Value, group, index, notesLookup, rangeCache);
                if (!string.IsNullOrEmpty(enumLabel)) return $"\"{enumLabel}\"";
            }

            // Explicitly handle Hex persistence:
            // If showDecimal is False, avoid the round-trip conversion if possible, 
            // OR force the output to be hex.
            if (long.TryParse(op.Value.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val))
            {
                if (showDecimal) return val.ToString();

                // If not showing decimal, prefer the original string if it looks like hex
                if (op.Value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return op.Value;

                // Otherwise force hex formatting
                return "0x" + val.ToString("X");
            }

            return op.Value;
        }

        private static string TryGetEnumLabel(
            string addr,
            string value,
            AchievementConditionGroup group,
            int index,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache)
        {
            string normAddr = LogicFormatter.NormalizeAddress(addr);

            if (notesLookup.TryGetValue(normAddr, out var nodes))
            {
                var node = nodes.FirstOrDefault(n => n.IndentLevel == -2) ?? nodes.FirstOrDefault();
                if (node != null) return ParseEnumFromContent(node.Content, value);
            }

            if (rangeCache != null)
            {
                if (long.TryParse(normAddr.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long targetVal))
                {
                    var match = rangeCache.FirstOrDefault(r => targetVal >= r.Start && targetVal < r.End);
                    if (match.Node != null) return ParseEnumFromContent(match.Node.Content, value);
                }
            }

            return "";
        }

        private static string ParseEnumFromContent(string content, string value)
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

        private static string GetOperatorText(string op)
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
    }
}