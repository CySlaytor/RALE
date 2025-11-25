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
    // Service responsible for converting raw logic (Conditions/Groups) into human-readable text explanations.
    // Handles complex patterns like Arrays, Accumulators, and Delta transitions.
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

            // 1. Explain Core Group
            var core = groups.FirstOrDefault(g => g.GroupName == "Core");
            if (core != null && core.Conditions.Count > 0)
            {
                sb.AppendLine("## Core Requirements (Must be true):");
                sb.AppendLine(ExplainGroupNatural(core, notesLookup, rangeCache, showDecimal, details));
                sb.AppendLine();
            }

            // 2. Process Alt Groups
            var allAlts = groups.Where(g => g.GroupName != "Core").ToList();
            var regularAlts = new List<AchievementConditionGroup>();
            var globalResets = new List<AchievementConditionGroup>();
            var displayLogic = new List<AchievementConditionGroup>();

            foreach (var alt in allAlts)
            {
                if (alt.Conditions.Count > 0)
                {
                    // Detect Global Resets (Alt groups containing only ResetIfs)
                    if (alt.Conditions.All(c => c.Flag == "Reset If"))
                    {
                        globalResets.Add(alt);
                    }
                    // Detect Display/UX Logic (Measured/Trigger logic that is effectively impossible to trigger)
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

            // 3. Explain Regular Alts (Logic Arrays)
            if (regularAlts.Count > 0)
            {
                sb.AppendLine("## Alternative Requirements (At least one set must be true):");

                int i = 0;
                while (i < regularAlts.Count)
                {
                    // Check if multiple Alts form a repeating pattern (Array)
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

            // 4. Explain Display/UX Logic
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

            // 5. Explain Global Resets
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

        // Helper to determine if a group is solely for displaying an overlay (e.g. impossible logic + measured)
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
            // If impossible conditions strictly outnumber valid terminators, it's likely Display Only
            return impossibleCount > validAccumulatorTerminators;
        }

        // Detects conditions like 0=1 or 1=0
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

        // Detects repeating patterns across multiple Alt groups
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

        // Detects repeating patterns within a single group (Accumulator Arrays)
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

        // Generates a unique string hash for a group structure
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
                sb.Append(c.LeftOperand.Value); // Ignore value for Mem to detect structural matches

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
            public List<string> MeasuredIfs = new();
            public List<string> Transitions = new();
            public List<string> Resets = new();
            public List<string> Pauses = new();
            public List<string> ComplexLogic = new();
        }

        // Splits a group into logical blocks based on combinator flags
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

        // Specialized explainer for UX/Overlay groups (Measured/Trigger)
        private static string ExplainUXGroup(
            AchievementConditionGroup group,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache,
            bool showDecimal,
            Dictionary<string, string> details)
        {
            var analysis = new GroupAnalysis();
            string measuredTarget = "";

            // Pre-scan for UX flags
            bool hasMeasured = group.Conditions.Any(c => c.Flag.StartsWith("Measured"));
            bool hasTrigger = group.Conditions.Any(c => c.Flag == "Trigger");

            var conditions = group.Conditions;
            var usedIndices = new HashSet<int>();

            // Helper to find what variable is being measured
            string measuredTargetName = "";
            int measuredIdx = group.Conditions.FindIndex(c => c.Flag == "Measured" || c.Flag == "Measured%");
            if (measuredIdx >= 0)
            {
                var measuredCond = group.Conditions[measuredIdx];
                // Check if it's part of an accumulator chain
                if (measuredIdx > 0)
                {
                    string prevFlag = group.Conditions[measuredIdx - 1].Flag;
                    if (prevFlag == "Add Hits" || prevFlag == "Sub Hits") measuredTargetName = "hits";
                    else if (prevFlag == "Add Source" || prevFlag == "Sub Source") measuredTargetName = "value";
                }

                // Fallback: Get variable name if standalone
                if (string.IsNullOrEmpty(measuredTargetName))
                {
                    measuredTargetName = GetName(measuredCond.LeftOperand.Value, group, measuredIdx, notesLookup, rangeCache);
                }
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                if (usedIndices.Contains(i)) continue;
                var cond = conditions[i];

                // Handle MeasuredIf
                if (cond.Flag == "MeasuredIf")
                {
                    // Added to MeasuredIfs list, explained in the header
                    analysis.MeasuredIfs.Add(ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal));
                    continue;
                }

                // Check for Accumulator Chain start (Add Source/Hits or Add Address leading to them)
                bool isAccumulatorStart = cond.Flag == "Add Source" || cond.Flag == "Sub Source" || cond.Flag == "Add Hits" || cond.Flag == "Sub Hits";

                if (!isAccumulatorStart && cond.Flag == "Add Address")
                {
                    // Look ahead to see if this pointer chain feeds into an accumulator
                    int scan = i + 1;
                    while (scan < conditions.Count)
                    {
                        if (conditions[scan].Flag == "Add Address") { scan++; continue; }
                        if (conditions[scan].Flag.Contains("Add") || conditions[scan].Flag.Contains("Sub"))
                        {
                            isAccumulatorStart = true;
                        }
                        break;
                    }
                }

                if (isAccumulatorStart)
                {
                    // Extract the first accumulator chain
                    var chain1 = new List<AchievementCondition>();
                    int ptr = i;
                    AchievementCondition? finalCond1 = null;

                    while (ptr < conditions.Count)
                    {
                        var c = conditions[ptr];
                        usedIndices.Add(ptr);

                        if (c.Flag == "Add Hits" || c.Flag == "Sub Hits" ||
                            c.Flag == "Add Source" || c.Flag == "Sub Source" ||
                            c.Flag == "AndNext" || c.Flag == "OrNext" || c.Flag == "Add Address")
                        {
                            chain1.Add(c);
                        }
                        else
                        {
                            finalCond1 = c;
                            break;
                        }
                        ptr++;
                    }

                    // Lookahead for a second chain to merge (detect Delta transition: Sum A < X -> Sum B = X)
                    List<AchievementCondition>? chain2 = null;
                    AchievementCondition? finalCond2 = null;
                    int ptr2 = ptr + 1; // check next index

                    if (ptr2 < conditions.Count && !usedIndices.Contains(ptr2))
                    {
                        var checkCond = conditions[ptr2];
                        bool nextIsAcc = checkCond.Flag.Contains("Add") || checkCond.Flag.Contains("Sub") || checkCond.Flag == "Add Address";

                        if (checkCond.Flag == "Add Address")
                        {
                            int scan = ptr2 + 1;
                            while (scan < conditions.Count)
                            {
                                if (conditions[scan].Flag == "Add Address") { scan++; continue; }
                                if (conditions[scan].Flag.Contains("Add") || conditions[scan].Flag.Contains("Sub")) { nextIsAcc = true; }
                                break;
                            }
                        }

                        if (nextIsAcc)
                        {
                            chain2 = new List<AchievementCondition>();
                            while (ptr2 < conditions.Count)
                            {
                                var c = conditions[ptr2];
                                if (c.Flag.Contains("Add") || c.Flag.Contains("Sub") || c.Flag.Contains("Next") || c.Flag == "Add Address")
                                {
                                    chain2.Add(c);
                                }
                                else
                                {
                                    finalCond2 = c;
                                    break;
                                }
                                ptr2++;
                            }
                        }
                    }

                    // Attempt Merge into Transition Logic
                    bool merged = false;
                    if (chain1.Count > 0 && chain2 != null && chain2.Count > 0 && finalCond1 != null && finalCond2 != null)
                    {
                        if (AreChainsDeltaPair(chain1, chain2))
                        {
                            // Mark chain2 as used
                            for (int k = ptr + 1; k <= ptr2; k++) usedIndices.Add(k);

                            string op1 = finalCond1.Operator;
                            string op2 = finalCond2.Operator;
                            string val1 = FormatValue(finalCond1.RightOperand, group, -1, notesLookup, rangeCache, showDecimal);
                            string val2 = FormatValue(finalCond2.RightOperand, group, -1, notesLookup, rangeCache, showDecimal);

                            bool chain1IsDelta = IsChainDelta(chain1);
                            string fromVal, toVal;

                            if (chain1IsDelta)
                            {
                                fromVal = (op1 == "=") ? val1 : $"({op1} {val1})";
                                toVal = (op2 == "=") ? val2 : $"({op2} {val2})";
                            }
                            else
                            {
                                fromVal = (op2 == "=") ? val2 : $"({op2} {val2})";
                                toVal = (op1 == "=") ? val1 : $"({op1} {val1})";
                            }

                            var blocks = SegmentLogicBlocks(chain1); // Use chain 1 for details
                            string id = Guid.NewGuid().ToString("N");
                            details[id] = GenerateChainDetails(blocks, group, notesLookup, rangeCache, showDecimal);
                            string expandToken = $"[[EXPAND:{id}:{blocks.Count}]]";

                            analysis.Transitions.Add($"Sum of {blocks.Count} items {expandToken} changes from {fromVal} to {toVal}");
                            merged = true;
                        }
                    }

                    if (!merged)
                    {
                        // Fallback: Process chain 1 only
                        var blocks = SegmentLogicBlocks(chain1);
                        string id = Guid.NewGuid().ToString("N");
                        details[id] = GenerateChainDetails(blocks, group, notesLookup, rangeCache, showDecimal);
                        string expandToken = $"[[EXPAND:{id}:{blocks.Count}]]";

                        if (finalCond1 != null)
                        {
                            if (finalCond1.Flag == "MeasuredIf")
                            {
                                string accSuffix = "";
                                if (!string.IsNullOrEmpty(measuredTargetName))
                                    accSuffix = $". This condition controls when {measuredTargetName} is measured";
                                else
                                    accSuffix = ". (MeasuredIf)";

                                string complexDesc = $"Accumulator chain (sum of {blocks.Count} items {expandToken})";
                                analysis.ComplexLogic.Add(complexDesc + accSuffix);
                            }
                            else if (finalCond1.Flag.StartsWith("Measured"))
                            {
                                bool isHitChain = (cond.Flag.Contains("Hits"));
                                string label = isHitChain ? "hits" : "value";
                                string valText = (finalCond1.RequiredHits > 0) ? finalCond1.RequiredHits.ToString() : FormatValue(finalCond1.RightOperand, group, -1, notesLookup, rangeCache, showDecimal);
                                analysis.ComplexLogic.Add($"Start measuring {label} up to {valText}: {expandToken}");
                            }
                            else
                            {
                                string valText = (finalCond1.RequiredHits > 0) ? finalCond1.RequiredHits.ToString() : FormatValue(finalCond1.RightOperand, group, -1, notesLookup, rangeCache, showDecimal);
                                string opText = GetOperatorText(finalCond1.Operator);
                                if (cond.Flag.Contains("Hits"))
                                    analysis.ComplexLogic.Add($"Trigger when at least {valText} of the following occur: {expandToken}");
                                else
                                    analysis.ComplexLogic.Add($"Sum of {blocks.Count} items {expandToken} {opText} {valText}");
                            }
                        }
                        else
                        {
                            analysis.ComplexLogic.Add($"Logic sequence {expandToken}");
                        }
                    }
                    continue;
                }

                if (cond.Flag == "Add Address") continue;
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
                }
                else if (cond.Flag == "Remember")
                {
                    analysis.Contexts.Add(ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal));
                }
                else if (cond.Flag == "Pause If")
                {
                    string expl = ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal);
                    if (cond.RequiredHits > 0)
                        analysis.Pauses.Add($"If {expl}, lock the achievement until a Reset occurs (PauseLock).");
                    else
                        analysis.Pauses.Add($"If {expl}, pause the achievement.");
                }
                else
                {
                    analysis.Contexts.Add(ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal));
                }
            }

            var sb = new StringBuilder();

            if (hasMeasured)
            {
                sb.Append("A Measured Indicator will start measuring: ");
                sb.Append(measuredTarget);

                if (analysis.MeasuredIfs.Count > 0)
                {
                    sb.Append(" once ");
                    sb.Append(string.Join(" AND ", analysis.MeasuredIfs));
                    if (analysis.Contexts.Count > 0 || analysis.ComplexLogic.Count > 0)
                        sb.AppendLine(" and the following:");
                    else
                        sb.AppendLine(".");
                }
                else
                {
                    if (analysis.Contexts.Count > 0 || analysis.ComplexLogic.Count > 0)
                        sb.AppendLine(" once the following logic is satisfied:");
                    else
                        sb.AppendLine(".");
                }
            }
            else if (hasTrigger)
            {
                sb.AppendLine("A Trigger Indicator is displayed once the following logic is satisfied:");
            }
            else
            {
                sb.AppendLine("Perform display logic when:");
            }

            foreach (var ctx in analysis.Contexts) sb.AppendLine($"- {ctx}.");
            foreach (var l in analysis.ComplexLogic) sb.AppendLine($"- {l}");
            foreach (var p in analysis.Pauses) sb.AppendLine($"- {p}");

            return sb.ToString();
        }

        // Helper to generate details for an accumulator chain
        private static string GenerateChainDetails(List<List<AchievementCondition>> blocks, AchievementConditionGroup group, Dictionary<string, List<NoteNode>> notesLookup, List<(long Start, long End, NoteNode Node)> rangeCache, bool showDecimal)
        {
            var sbChain = new StringBuilder();
            int bIdx = 0;
            while (bIdx < blocks.Count)
            {
                int seqLen = DetectConditionSequence(blocks, bIdx);
                if (seqLen > 2)
                {
                    // Detected a repeating pattern (Array)
                    var exampleBlock = blocks[bIdx];
                    var firstBlockCond = exampleBlock.FirstOrDefault(c => c.Flag != "Add Address");
                    var lastBlock = blocks[bIdx + seqLen - 1];
                    var lastBlockCond = lastBlock.FirstOrDefault(c => c.Flag != "Add Address");

                    var exampleLines = new List<string>();
                    foreach (var c in exampleBlock)
                    {
                        if (c.Flag == "Add Address") continue;
                        int realIdx = group.Conditions.IndexOf(c);
                        exampleLines.Add(ExplainSingleCondition(c, group, realIdx, notesLookup, rangeCache, showDecimal, true, true));
                    }

                    sbChain.AppendLine($"- {seqLen} conditions matching pattern (Array):");

                    if (firstBlockCond != null && lastBlockCond != null)
                    {
                        string nameBase = GetName(firstBlockCond.LeftOperand.Value, group, group.Conditions.IndexOf(firstBlockCond), notesLookup, rangeCache, true);
                        string offsetStart = "0x" + firstBlockCond.LeftOperand.Value.Replace("0x", "");
                        string offsetEnd = "0x" + lastBlockCond.LeftOperand.Value.Replace("0x", "");

                        // Check for shared note name pattern to determine if it is a contiguous array
                        string nameLast = GetName(lastBlockCond.LeftOperand.Value, group, group.Conditions.IndexOf(lastBlockCond), notesLookup, rangeCache, true);
                        string cleanBase = Regex.Replace(nameBase, @"\s\+0x[0-9A-Fa-f]+$", "");
                        string cleanLast = Regex.Replace(nameLast, @"\s\+0x[0-9A-Fa-f]+$", "");

                        if (cleanBase == cleanLast && nameBase.Contains("+0x"))
                        {
                            // It's a contiguous array based on the same note structure
                            if (nameBase.Contains("+0x"))
                            {
                                int plusIdx = nameBase.LastIndexOf("+0x");
                                if (plusIdx != -1) nameBase = nameBase.Substring(0, plusIdx).TrimEnd() + "]";
                            }

                            if (offsetStart != offsetEnd)
                                sbChain.AppendLine($"  > {nameBase} +{offsetStart} ~ {offsetEnd}");
                            else
                                sbChain.AppendLine($"  > {string.Join(" AND ", exampleLines)}");
                        }
                        else
                        {
                            // Not a consistent contiguous array, list ALL distinct items explicitly
                            sbChain.AppendLine($"  > {string.Join(" AND ", exampleLines)}");

                            // Manually iterate remainder of sequence to print all items
                            for (int k = 1; k < seqLen; k++)
                            {
                                var nextBlock = blocks[bIdx + k];
                                var nextLines = new List<string>();
                                foreach (var c in nextBlock)
                                {
                                    if (c.Flag == "Add Address") continue;
                                    int realIdx = group.Conditions.IndexOf(c);
                                    nextLines.Add(ExplainSingleCondition(c, group, realIdx, notesLookup, rangeCache, showDecimal, true, true));
                                }
                                sbChain.AppendLine($"  > {string.Join(" AND ", nextLines)}");
                            }
                        }
                    }
                    else
                    {
                        sbChain.AppendLine($"  > {string.Join(" AND ", exampleLines)}");
                    }

                    bIdx += seqLen;
                }
                else
                {
                    // Single items
                    var block = blocks[bIdx];
                    var blockLines = new List<string>();
                    foreach (var c in block)
                    {
                        if (c.Flag == "Add Address") continue;
                        int realIdx = group.Conditions.IndexOf(c);
                        blockLines.Add(ExplainSingleCondition(c, group, realIdx, notesLookup, rangeCache, showDecimal, true, true));
                    }
                    sbChain.AppendLine($"- {string.Join(" AND ", blockLines)}");
                    bIdx++;
                }
            }
            return sbChain.ToString().TrimEnd();
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

            // Look for Measured target name in the group to explain MeasuredIf context
            string measuredTargetName = "";
            int measuredIdx = group.Conditions.FindIndex(c => c.Flag == "Measured" || c.Flag == "Measured%");
            if (measuredIdx >= 0)
            {
                var measuredCond = group.Conditions[measuredIdx];
                if (measuredIdx > 0)
                {
                    string prevFlag = group.Conditions[measuredIdx - 1].Flag;
                    if (prevFlag == "Add Hits" || prevFlag == "Sub Hits") measuredTargetName = "hits";
                    else if (prevFlag == "Add Source" || prevFlag == "Sub Source") measuredTargetName = "value";
                }

                if (string.IsNullOrEmpty(measuredTargetName))
                {
                    measuredTargetName = GetName(measuredCond.LeftOperand.Value, group, measuredIdx, notesLookup, rangeCache);
                }
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                if (usedIndices.Contains(i)) continue;
                var cond = conditions[i];

                // FIX: Ensure Add Address starts a chain if followed by Add Source/Hits
                bool isAccumulatorStart = cond.Flag == "Add Source" || cond.Flag == "Sub Source" || cond.Flag == "Add Hits" || cond.Flag == "Sub Hits";

                if (!isAccumulatorStart && cond.Flag == "Add Address")
                {
                    // Look ahead to see if this chain leads to an accumulator
                    int scan = i + 1;
                    while (scan < conditions.Count)
                    {
                        if (conditions[scan].Flag == "Add Address") { scan++; continue; }
                        if (conditions[scan].Flag.Contains("Add") || conditions[scan].Flag.Contains("Sub"))
                        {
                            isAccumulatorStart = true;
                        }
                        break;
                    }
                }

                if (isAccumulatorStart)
                {
                    // Extract first chain
                    var chain1 = new List<AchievementCondition>();
                    int ptr = i;
                    AchievementCondition? finalCond1 = null;

                    while (ptr < conditions.Count)
                    {
                        var c = conditions[ptr];
                        usedIndices.Add(ptr);
                        if (c.Flag.Contains("Add") || c.Flag.Contains("Sub") ||
                            c.Flag.Contains("Next") || c.Flag == "Add Address")
                        {
                            chain1.Add(c);
                        }
                        else
                        {
                            finalCond1 = c;
                            break;
                        }
                        ptr++;
                    }

                    // Lookahead for a second chain to merge (Delta transition detection)
                    List<AchievementCondition>? chain2 = null;
                    AchievementCondition? finalCond2 = null;
                    int ptr2 = ptr + 1; // ptr points to finalCond1 of chain1, so check next

                    // Quick check if next index starts an accumulator
                    if (ptr2 < conditions.Count && !usedIndices.Contains(ptr2))
                    {
                        var checkCond = conditions[ptr2];
                        bool nextIsAcc = checkCond.Flag.Contains("Add") || checkCond.Flag.Contains("Sub") || checkCond.Flag == "Add Address";

                        // Deep check for Add Address chain
                        if (checkCond.Flag == "Add Address")
                        {
                            int scan = ptr2 + 1;
                            while (scan < conditions.Count)
                            {
                                if (conditions[scan].Flag == "Add Address") { scan++; continue; }
                                if (conditions[scan].Flag.Contains("Add") || conditions[scan].Flag.Contains("Sub")) { nextIsAcc = true; }
                                break;
                            }
                        }

                        if (nextIsAcc)
                        {
                            chain2 = new List<AchievementCondition>();
                            while (ptr2 < conditions.Count)
                            {
                                var c = conditions[ptr2];
                                if (c.Flag.Contains("Add") || c.Flag.Contains("Sub") || c.Flag.Contains("Next") || c.Flag == "Add Address")
                                {
                                    chain2.Add(c);
                                }
                                else
                                {
                                    finalCond2 = c;
                                    break;
                                }
                                ptr2++;
                            }
                        }
                    }

                    // Attempt Merge
                    bool merged = false;
                    if (chain1.Count > 0 && chain2 != null && chain2.Count > 0 && finalCond1 != null && finalCond2 != null)
                    {
                        if (AreChainsDeltaPair(chain1, chain2))
                        {
                            // Valid pair found. Mark chain2 indices as used.
                            for (int k = ptr + 1; k <= ptr2; k++) usedIndices.Add(k);

                            // Determine values
                            string val1 = FormatValue(finalCond1.RightOperand, group, -1, notesLookup, rangeCache, showDecimal);
                            string val2 = FormatValue(finalCond2.RightOperand, group, -1, notesLookup, rangeCache, showDecimal);

                            // Fix: Capture Operators
                            string op1 = finalCond1.Operator;
                            string op2 = finalCond2.Operator;

                            // Determine direction (Which is From (Delta) and which is To (Mem))
                            bool chain1IsDelta = IsChainDelta(chain1);

                            string fromVal, toVal;

                            if (chain1IsDelta)
                            {
                                fromVal = (op1 == "=") ? val1 : $"({op1} {val1})";
                                toVal = (op2 == "=") ? val2 : $"({op2} {val2})";
                            }
                            else
                            {
                                fromVal = (op2 == "=") ? val2 : $"({op2} {val2})";
                                toVal = (op1 == "=") ? val1 : $"({op1} {val1})";
                            }

                            var blocks = SegmentLogicBlocks(chain1); // Use chain 1 for detail display
                            string id = Guid.NewGuid().ToString("N");
                            details[id] = GenerateChainDetails(blocks, group, notesLookup, rangeCache, showDecimal);
                            string expandToken = $"[[EXPAND:{id}:{blocks.Count}]]";

                            analysis.Transitions.Add($"Sum of {blocks.Count} items {expandToken} changes from {fromVal} to {toVal}");
                            merged = true;
                        }
                    }

                    if (!merged)
                    {
                        // Fallback: Process chain 1 only
                        var blocks = SegmentLogicBlocks(chain1);
                        string id = Guid.NewGuid().ToString("N");
                        details[id] = GenerateChainDetails(blocks, group, notesLookup, rangeCache, showDecimal);
                        string expandToken = $"[[EXPAND:{id}:{blocks.Count}]]";

                        if (finalCond1 != null)
                        {
                            // Fix: MeasuredIf on Accumulator
                            if (finalCond1.Flag == "MeasuredIf")
                            {
                                string accSuffix = "";
                                if (!string.IsNullOrEmpty(measuredTargetName))
                                    accSuffix = $". This condition controls when {measuredTargetName} is measured";
                                else
                                    accSuffix = ". (MeasuredIf)";

                                string complexDesc = $"Accumulator chain (sum of {blocks.Count} items {expandToken})";
                                analysis.ComplexLogic.Add(complexDesc + accSuffix);
                            }
                            else if (finalCond1.Flag.StartsWith("Measured"))
                            {
                                bool isHitChain = (cond.Flag.Contains("Hits"));
                                string label = isHitChain ? "hits" : "value";
                                string valText = (finalCond1.RequiredHits > 0) ? finalCond1.RequiredHits.ToString() : FormatValue(finalCond1.RightOperand, group, -1, notesLookup, rangeCache, showDecimal);
                                analysis.ComplexLogic.Add($"Start measuring {label} up to {valText}: {expandToken}");
                            }
                            else
                            {
                                string valText = (finalCond1.RequiredHits > 0) ? finalCond1.RequiredHits.ToString() : FormatValue(finalCond1.RightOperand, group, -1, notesLookup, rangeCache, showDecimal);
                                string opText = GetOperatorText(finalCond1.Operator);
                                if (cond.Flag.Contains("Hits"))
                                    analysis.ComplexLogic.Add($"Trigger when at least {valText} of the following occur: {expandToken}");
                                else
                                    analysis.ComplexLogic.Add($"Sum of {blocks.Count} items {expandToken} {opText} {valText}");
                            }
                        }
                        else
                        {
                            analysis.ComplexLogic.Add($"Logic sequence {expandToken}");
                        }
                    }
                    continue;
                }

                if (cond.Flag == "Add Address") continue;

                if (cond.Flag == "Reset If")
                {
                    analysis.Resets.Add(ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal));
                    continue;
                }
                if (cond.Flag == "Pause If")
                {
                    string expl = ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal);
                    if (cond.RequiredHits > 0)
                        analysis.Pauses.Add($"If {expl}, lock the achievement until a Reset occurs (PauseLock).");
                    else
                        analysis.Pauses.Add($"If {expl}, pause the achievement.");
                    continue;
                }
                if (cond.Flag == "AndNext" || cond.Flag == "OrNext") continue;

                // SKIP Add Address conditions here to prevent them from showing as Contexts
                if (cond.Flag == "Add Address") continue;

                string? mergedTransition = TryMergeConditions(conditions, i, group, usedIndices, notesLookup, rangeCache, showDecimal);
                if (mergedTransition != null)
                {
                    analysis.Transitions.Add(mergedTransition);
                    continue;
                }

                string suffix = "";
                if (cond.Flag == "MeasuredIf")
                {
                    if (!string.IsNullOrEmpty(measuredTargetName))
                        suffix = $". This condition controls when {measuredTargetName} is measured";
                    else
                        suffix = ". (MeasuredIf)";
                }

                if (cond.Flag == "Remember")
                {
                    analysis.Contexts.Add(ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal));
                    continue;
                }

                bool hasDelta = cond.LeftOperand.Type == "Delta" || cond.RightOperand.Type == "Delta" || cond.LeftOperand.Type == "Prior";
                if (hasDelta) analysis.Transitions.Add(ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal) + suffix);
                else analysis.Contexts.Add(ExplainSingleCondition(cond, group, i, notesLookup, rangeCache, showDecimal) + suffix);
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

        // Checks if two chains are identical in structure but one uses Deltas and one uses Mems
        private static bool AreChainsDeltaPair(List<AchievementCondition> chain1, List<AchievementCondition> chain2)
        {
            if (chain1.Count != chain2.Count) return false;

            bool chain1HasDelta = false;
            bool chain2HasDelta = false;

            for (int i = 0; i < chain1.Count; i++)
            {
                var c1 = chain1[i];
                var c2 = chain2[i];

                // Flags must match
                if (c1.Flag != c2.Flag) return false;

                // Pointer logic (Add Address) must match exactly
                if (c1.Flag == "Add Address")
                {
                    if (c1.LeftOperand.Value != c2.LeftOperand.Value) return false;
                    // Deltas are irrelevant for base pointer addresses usually, but if present should match
                    continue;
                }

                // Accumulators (Add/Sub Source)
                bool c1IsDelta = c1.LeftOperand.Type == "Delta" || c1.LeftOperand.Type == "Prior";
                bool c1IsMem = c1.LeftOperand.Type == "Mem";
                bool c2IsDelta = c2.LeftOperand.Type == "Delta" || c2.LeftOperand.Type == "Prior";
                bool c2IsMem = c2.LeftOperand.Type == "Mem";

                // One must be Delta/Prior, the other Mem. Or both are identical constants.
                if (c1.LeftOperand.Value != c2.LeftOperand.Value) return false; // Addresses/Values must match

                if (c1IsDelta) chain1HasDelta = true;
                if (c2IsDelta) chain2HasDelta = true;

                // Valid Pairs: (Delta, Mem), (Mem, Delta), (Value, Value)
                if (c1IsDelta && c2IsMem) continue;
                if (c1IsMem && c2IsDelta) continue;
                if (c1.LeftOperand.Type == "Value" && c2.LeftOperand.Type == "Value") continue;

                return false;
            }

            // Must have at least one pair of mismatching types (Delta vs Mem) to be considered a transition check
            return (chain1HasDelta && !chain2HasDelta) || (!chain1HasDelta && chain2HasDelta);
        }

        private static bool IsChainDelta(List<AchievementCondition> chain)
        {
            return chain.Any(c => c.LeftOperand.Type == "Delta" || c.LeftOperand.Type == "Prior");
        }

        private static string? TryMergeConditions(
            List<AchievementCondition> conditions,
            int currentIndex,
            AchievementConditionGroup group,
            HashSet<int> usedIndices,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache,
            bool showDecimal)
        {
            var condA = conditions[currentIndex];
            if (condA.RequiredHits > 0) return null;

            if (condA.RightOperand.Type != "Value") return null;

            bool aIsDelta = condA.LeftOperand.Type == "Delta" || condA.LeftOperand.Type == "Prior";
            bool aIsMem = condA.LeftOperand.Type == "Mem";

            if (!aIsDelta && !aIsMem) return null;

            for (int j = currentIndex + 1; j < conditions.Count; j++)
            {
                if (usedIndices.Contains(j)) continue;
                var condB = conditions[j];

                if (condB.Flag == "Add Address") continue;

                if (condB.Flag != "" && !condB.Flag.StartsWith("Trigger") && !condB.Flag.StartsWith("Measured")) return null;
                if (condB.RequiredHits > 0) continue;
                if (condB.RightOperand.Type != "Value") continue;

                bool bIsDelta = condB.LeftOperand.Type == "Delta" || condB.LeftOperand.Type == "Prior";
                bool bIsMem = condB.LeftOperand.Type == "Mem";

                if (aIsDelta && bIsDelta) continue;
                if (aIsMem && bIsMem) continue;

                if (condA.LeftOperand.Value != condB.LeftOperand.Value) continue;
                if (condA.LeftOperand.Size != condB.LeftOperand.Size) continue;

                string nameA = GetName(condA.LeftOperand.Value, group, currentIndex, notesLookup, rangeCache);
                string nameB = GetName(condB.LeftOperand.Value, group, j, notesLookup, rangeCache);

                if (nameA != nameB) continue;

                var prevCond = aIsDelta ? condA : condB;
                var currCond = aIsMem ? condA : condB;

                // FIX: Pass specific indices to FormatValue to allow context-aware pointer lookups
                int prevIndex = (prevCond == condA) ? currentIndex : j;
                int currIndex = (currCond == condA) ? currentIndex : j;

                string contextAddr = condA.LeftOperand.Value;
                string prevVal = FormatValue(prevCond.RightOperand, group, prevIndex, notesLookup, rangeCache, showDecimal, contextAddr);
                string currVal = FormatValue(currCond.RightOperand, group, currIndex, notesLookup, rangeCache, showDecimal, contextAddr);

                string suffix = "";
                if (currCond.Flag.StartsWith("Measured"))
                {
                    string limitText = "";
                    if (currCond.Operator == "=" || currCond.Operator == ">=")
                        limitText = $", measuring up to {currVal}";

                    suffix = $". This condition will also display a Measured Indicator{limitText}";
                }

                if (prevCond.Operator == "=" && currCond.Operator == "=")
                {
                    usedIndices.Add(j);
                    return $"{nameA} changes from {prevVal} to {currVal}{suffix}";
                }
                if (prevCond.Operator == "!=" && currCond.Operator == "=" && prevVal == currVal)
                {
                    usedIndices.Add(j);
                    return $"{nameA} becomes {currVal}{suffix}";
                }
            }

            return null;
        }

        private static string ExplainSingleCondition(
            AchievementCondition cond,
            AchievementConditionGroup group,
            int index,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache,
            bool showDecimal,
            bool suppressHits = false,
            bool suppressPointers = false)
        {
            // NEW: Dummy Condition Check (e.g. 1=1, 0=0, Value 0x1 = Value 0x1)
            if (cond.LeftOperand.Type == "Value" && cond.RightOperand.Type == "Value" && cond.Operator == "=" && cond.LeftOperand.Value == cond.RightOperand.Value)
            {
                return "Always True (Dummy Condition)";
            }

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

            // ** FIX: Handle Remember logic **
            if (cond.Flag == "Remember")
            {
                bool leftIsDelta = cond.LeftOperand.Type == "Delta";
                bool rightIsMem = cond.RightOperand.Type == "Mem";
                bool sameAddr = cond.LeftOperand.Value == cond.RightOperand.Value;

                if (leftIsDelta && rightIsMem && sameAddr && cond.Operator == "-")
                {
                    string name = GetName(cond.LeftOperand.Value, group, index, notesLookup, rangeCache);
                    return $"Store the amount {name} decreased by";
                }
                if (cond.LeftOperand.Type == "Mem" && cond.RightOperand.Type == "Delta" && sameAddr && cond.Operator == "-")
                {
                    string name = GetName(cond.LeftOperand.Value, group, index, notesLookup, rangeCache);
                    return $"Store the amount {name} increased by";
                }
            }

            string left = GetOperandText(cond.LeftOperand, group, index, notesLookup, rangeCache, showDecimal, false, "", suppressPointers);
            string right = GetOperandText(cond.RightOperand, group, index, notesLookup, rangeCache, showDecimal, true, cond.LeftOperand.Value);
            string op = GetOperatorText(cond.Operator);
            string hits = (!suppressHits && cond.RequiredHits > 0) ? $" (occurs {cond.RequiredHits} times)" : "";

            // Clean up {recall} display
            if (left.Contains("{recall}")) left = "recalled value";

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
            string contextAddress = "",
            bool suppressPointers = false)
        {
            if (op.Type == "Recall") return "{recall}";

            if (op.Type == "Value" || op.Type == "Float")
                return FormatValue(op, group, index, notesLookup, rangeCache, showDecimal, contextAddress);

            string prefix = "";
            if (op.Type == "Delta") prefix = "previous frame ";
            if (op.Type == "Prior") prefix = "prior frame ";

            string name = GetName(op.Value, group, index, notesLookup, rangeCache, suppressPointers);
            return $"{prefix}{name}";
        }

        private static string GetName(string addr, AchievementConditionGroup group, int index, Dictionary<string, List<NoteNode>> notes, List<(long Start, long End, NoteNode Node)> range, bool suppressPointers = false)
        {
            // FIX: Handle empty address to prevent artifacts like [Pointer ... + ]
            if (string.IsNullOrEmpty(addr)) return "";

            string alias = ConditionFormatter.ResolveAlias(LogicFormatter.NormalizeAddress(addr), group, index, notes, range);
            if (!string.IsNullOrEmpty(alias)) return $"[{alias}]";

            if (group != null && index > 0 && index < group.Conditions.Count)
            {
                if (group.Conditions[index - 1].Flag == "Add Address")
                {
                    if (suppressPointers)
                        return $"[... + {addr}]";
                    return $"[Pointer ... + {addr}]";
                }
            }

            return $"Memory {addr}";
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

            if (long.TryParse(op.Value.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val))
            {
                if (showDecimal) return val.ToString();
                if (op.Value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return op.Value;
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
            // Helper to verify hex strings
            long ParseHex(string s) =>
                long.TryParse(s.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long v) ? v : -1;

            long targetVal = ParseHex(addr);

            // FIX: Lookup Logic 1: Pointer Chain Resolution
            if (group != null && index > 0)
            {
                var chainStack = new Stack<string>();
                int scan = index - 1;
                while (scan >= 0)
                {
                    var prevCond = group.Conditions[scan];
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

                    if (notesLookup.TryGetValue(normalizedBase, out var nodes))
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
                                long nodeOff = ParseHex(node.Offset.Replace("+", "").Replace("-", "").Trim());
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
                                long nodeOff = ParseHex(node.Offset.Replace("+", "").Replace("-", "").Trim());
                                if (node.Offset.Contains("-")) nodeOff = -nodeOff;

                                if (targetVal >= nodeOff && targetVal < nodeOff + node.Size)
                                {
                                    // Found correct pointer node context, parse enums from here
                                    string enumLabel = ParseEnumFromContent(node.Content, value);
                                    if (!string.IsNullOrEmpty(enumLabel)) return enumLabel;
                                }
                            }
                        }
                    }
                }
            }

            string normAddr = LogicFormatter.NormalizeAddress(addr);

            if (notesLookup.TryGetValue(normAddr, out var lookupNodes))
            {
                var node = lookupNodes.FirstOrDefault(n => n.IndentLevel == -2) ?? lookupNodes.FirstOrDefault();
                if (node != null) return ParseEnumFromContent(node.Content, value);
            }

            if (rangeCache != null)
            {
                if (targetVal != -1)
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