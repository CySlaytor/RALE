using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Services.Explainer.Processors
{
    public class AccumulatorProcessor : ILogicProcessor
    {
        // Priority MUST be higher (lower value) than PointerCleanupProcessor (5).
        public int Priority => 3;

        public bool CanProcess(List<AchievementCondition> conditions, int currentIndex)
        {
            var cond = conditions[currentIndex];
            // Check for Accumulator Chain start
            bool isAccumulatorStart = cond.Flag == "Add Source" || cond.Flag == "Sub Source" ||
                                      cond.Flag == "Add Hits" || cond.Flag == "Sub Hits";

            if (!isAccumulatorStart && cond.Flag == "Add Address")
            {
                // Look ahead to see if this pointer chain feeds into an accumulator
                int scan = currentIndex + 1;
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
            return isAccumulatorStart;
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var result = new ProcessorResult { Category = ExplanationCategory.Complex };

            // 1. Collect all consecutive accumulator chains that share the same structure
            var chains = new List<AccumulatorChainDef>();
            var usedIndices = new HashSet<int>();
            int currentPtr = index;

            while (currentPtr < conditions.Count)
            {
                if (usedIndices.Contains(currentPtr)) break;

                // Check if this line starts a valid accumulator chain
                if (!CanProcess(conditions, currentPtr)) break;

                var conditionsInChain = ExtractChain(conditions, currentPtr, out int endPtr);
                if (conditionsInChain.Count == 0) break;

                AchievementCondition? termCond = null;
                if (endPtr < conditions.Count) termCond = conditions[endPtr];

                var chainDef = new AccumulatorChainDef
                {
                    Conditions = conditionsInChain,
                    Terminal = termCond,
                    StartIndex = currentPtr,
                    EndIndex = endPtr
                };

                // If this is not the first chain, verify it matches the structure of the first chain
                if (chains.Count > 0)
                {
                    if (!AreChainsStructureEqual(chains[0].Conditions, chainDef.Conditions))
                        break; // Different structure, stop collecting
                }

                chains.Add(chainDef);

                // Mark indices as used (chain + terminal)
                int lastIdx = (termCond != null) ? endPtr : endPtr - 1;
                for (int k = currentPtr; k <= lastIdx && k < conditions.Count; k++) usedIndices.Add(k);

                currentPtr = endPtr + 1;
            }

            // 2. Analyze Chains for Transition (Delta -> Mem)
            var deltaChains = chains.Where(c => IsChainDelta(c.Conditions)).ToList();
            var memChains = chains.Where(c => !IsChainDelta(c.Conditions)).ToList();

            // We merge if we have at least one Delta chain AND at least one Mem chain
            bool isTransition = deltaChains.Count > 0 && memChains.Count > 0;

            if (isTransition)
            {
                // Merge Logic: "Changes from (A and B) to (C)"
                var fromConditions = deltaChains.Select(c => FormatTerminalCondition(c.Terminal, ctx, conditions)).ToList();
                var toConditions = memChains.Select(c => FormatTerminalCondition(c.Terminal, ctx, conditions)).ToList();

                string fromText = string.Join(" AND ", fromConditions);
                string toText = string.Join(" AND ", toConditions);

                // Use the first chain to generate the drill-down details
                var blocks = SegmentLogicBlocks(chains[0].Conditions);
                string id = Guid.NewGuid().ToString("N");
                result.DetailedDrillDowns[id] = GenerateChainDetails(blocks, ctx);
                string expandToken = $"[[EXPAND:{id}:{blocks.Count}]]";

                string suffix = "";
                // Check if the final Mem chain triggers a Reset or Pause
                var finalMemChain = memChains.Last();
                if (finalMemChain.Terminal != null)
                {
                    if (finalMemChain.Terminal.Flag == "Reset If") suffix = " (Reset If)";
                    else if (finalMemChain.Terminal.Flag == "Pause If") suffix = " (Pause If)";
                }

                result.Text = $"Sum of {blocks.Count} items {expandToken} changes from ({fromText}) to ({toText}){suffix}";
                result.Category = ExplanationCategory.Transition;

                // Consume all collected chains
                int lastIndex = chains.Last().EndIndex;
                result.ConditionsConsumed = (lastIndex - index) + 1;
            }
            else
            {
                // Standard Processing (Single Chain or non-matching sequence)
                var chain1 = chains[0];
                var blocks = SegmentLogicBlocks(chain1.Conditions);
                string id = Guid.NewGuid().ToString("N");
                result.DetailedDrillDowns[id] = GenerateChainDetails(blocks, ctx);
                string expandToken = $"[[EXPAND:{id}:{blocks.Count}]]";

                if (chain1.Terminal != null)
                {
                    var finalCond1 = chain1.Terminal;
                    if (finalCond1.Flag == "MeasuredIf")
                    {
                        // Pass Context (Value + Size) to FormatValue for correct Bit Label resolution
                        string valText = ctx.FormatValue(finalCond1.RightOperand, conditions.IndexOf(finalCond1), finalCond1.LeftOperand.Value, finalCond1.LeftOperand.Size);
                        string opText = ctx.GetOperatorText(finalCond1.Operator);
                        string criteria = $"sum of {blocks.Count} items {expandToken} {opText} {valText}";
                        result.Text = $"Accumulator: {criteria} (MeasuredIf)";
                        result.Category = ExplanationCategory.MeasuredIf;
                    }
                    else if (finalCond1.Flag.StartsWith("Measured"))
                    {
                        bool isHitChain = chain1.Conditions.Any(c => c.Flag.Contains("Hits")) || finalCond1.Flag.Contains("Hits");
                        string label = isHitChain ? "hits" : "value";
                        // Pass Context (Value + Size)
                        string valText = (finalCond1.RequiredHits > 0) ? finalCond1.RequiredHits.ToString() : ctx.FormatValue(finalCond1.RightOperand, conditions.IndexOf(finalCond1), finalCond1.LeftOperand.Value, finalCond1.LeftOperand.Size);
                        result.Text = $"Start measuring {label} up to {valText}: {expandToken}";
                    }
                    else
                    {
                        // Pass Context (Value + Size)
                        string valText = (finalCond1.RequiredHits > 0) ? finalCond1.RequiredHits.ToString() : ctx.FormatValue(finalCond1.RightOperand, conditions.IndexOf(finalCond1), finalCond1.LeftOperand.Value, finalCond1.LeftOperand.Size);
                        string opText = ctx.GetOperatorText(finalCond1.Operator);
                        string actionPrefix = "Trigger";

                        if (finalCond1.Flag == "Pause If")
                        {
                            actionPrefix = (finalCond1.RequiredHits > 0) ? "Lock the achievement (PauseLock)" : "Pause the achievement";
                            result.Category = ExplanationCategory.Pause;
                        }
                        else if (finalCond1.Flag == "Reset If")
                        {
                            actionPrefix = "Reset the achievement";
                            result.Category = ExplanationCategory.Reset;
                        }

                        if (chain1.Conditions.Any(c => c.Flag.Contains("Hits")))
                        {
                            result.Text = $"{actionPrefix} when at least {valText} of the following occur: {expandToken}";
                        }
                        else
                        {
                            if (finalCond1.Flag == "Pause If" || finalCond1.Flag == "Reset If")
                                result.Text = $"{actionPrefix} when sum of {blocks.Count} items {expandToken} {opText} {valText}";
                            else
                                result.Text = $"Sum of {blocks.Count} items {expandToken} {opText} {valText}";
                        }
                    }
                }
                else
                {
                    result.Text = $"Logic sequence {expandToken}";
                }

                int consumed = (chain1.Terminal != null) ? (chain1.EndIndex - index) + 1 : (chain1.EndIndex - index);
                result.ConditionsConsumed = consumed;
            }

            return result;
        }

        private string FormatTerminalCondition(AchievementCondition? cond, ExplanationContext ctx, List<AchievementCondition> conditions)
        {
            if (cond == null) return "(No Check)";
            // Pass Context (Value + Size) to FormatValue
            string val = ctx.FormatValue(cond.RightOperand, conditions.IndexOf(cond), cond.LeftOperand.Value, cond.LeftOperand.Size);
            string op = cond.Operator;

            if (op == "=") return val;
            return $"{op} {val}";
        }

        private class AccumulatorChainDef
        {
            public List<AchievementCondition> Conditions { get; set; } = new();
            public AchievementCondition? Terminal { get; set; }
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
        }

        private List<AchievementCondition> ExtractChain(List<AchievementCondition> conditions, int start, out int endPtr)
        {
            var chain = new List<AchievementCondition>();
            int ptr = start;
            while (ptr < conditions.Count)
            {
                var c = conditions[ptr];
                if (c.Flag.Contains("Add") || c.Flag.Contains("Sub") ||
                    c.Flag.Contains("Next") || c.Flag == "Add Address")
                {
                    chain.Add(c);
                }
                else
                {
                    break;
                }
                ptr++;
            }
            endPtr = ptr;
            return chain;
        }

        private bool AreChainsStructureEqual(List<AchievementCondition> chain1, List<AchievementCondition> chain2)
        {
            if (chain1.Count != chain2.Count) return false;

            for (int i = 0; i < chain1.Count; i++)
            {
                var c1 = chain1[i];
                var c2 = chain2[i];

                if (c1.Flag != c2.Flag) return false;

                // Left Operand Check (Value/Address and Size)
                if (!AreValuesEqual(c1.LeftOperand.Value, c2.LeftOperand.Value)) return false;
                if (c1.LeftOperand.Size != c2.LeftOperand.Size) return false;

                // Add Address: Strict Type check (Mem vs Mem)
                if (c1.Flag == "Add Address")
                {
                    if (c1.LeftOperand.Type != c2.LeftOperand.Type) return false;
                    continue;
                }

                // Operator must match
                string op1 = c1.Operator ?? "";
                string op2 = c2.Operator ?? "";
                if (op1 != op2) return false;

                // Add Source/Hits: Allow Delta/Mem/Prior mixing
                bool c1IsMemLike = IsMemLike(c1.LeftOperand.Type);
                bool c2IsMemLike = IsMemLike(c2.LeftOperand.Type);
                if (c1IsMemLike != c2IsMemLike) return false;

                // Right Operand Check
                bool c1RightIsVal = c1.RightOperand.Type == "Value" || c1.RightOperand.Type == "Float" || string.IsNullOrEmpty(c1.RightOperand.Type);
                bool c2RightIsVal = c2.RightOperand.Type == "Value" || c2.RightOperand.Type == "Float" || string.IsNullOrEmpty(c2.RightOperand.Type);

                if (c1RightIsVal && c2RightIsVal)
                {
                    if (!AreValuesEqual(c1.RightOperand.Value, c2.RightOperand.Value)) return false;
                }
                else
                {
                    if (c1.RightOperand.Type != c2.RightOperand.Type) return false;
                    if (!AreValuesEqual(c1.RightOperand.Value, c2.RightOperand.Value)) return false;
                }
            }
            return true;
        }

        private bool AreValuesEqual(string v1, string v2)
        {
            if (v1 == v2) return true;
            string clean1 = string.IsNullOrEmpty(v1) ? "0" : v1.Trim().Replace("0x", "");
            string clean2 = string.IsNullOrEmpty(v2) ? "0" : v2.Trim().Replace("0x", "");
            if (long.TryParse(clean1, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long l1) &&
                long.TryParse(clean2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long l2))
            {
                return l1 == l2;
            }
            return clean1.Equals(clean2, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsMemLike(string type)
        {
            return type == "Mem" || type == "Delta" || type == "Prior" || type == "BCD" || type == "Inverted";
        }

        private bool IsChainDelta(List<AchievementCondition> chain) =>
            chain.Any(c => c.LeftOperand.Type == "Delta" || c.LeftOperand.Type == "Prior");

        private List<List<AchievementCondition>> SegmentLogicBlocks(List<AchievementCondition> conditions)
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

        private string GenerateChainDetails(List<List<AchievementCondition>> blocks, ExplanationContext ctx)
        {
            var sbChain = new StringBuilder();
            int bIdx = 0;
            while (bIdx < blocks.Count)
            {
                var exampleBlock = blocks[bIdx];
                // Use GetName(Operand) to get Bit Labels if applicable for the chain description
                var firstBlockCond = exampleBlock.FirstOrDefault(c => c.Flag != "Add Address");
                string nameBase = "Unknown";
                if (firstBlockCond != null)
                {
                    nameBase = ctx.GetName(firstBlockCond.LeftOperand, ctx.Group.Conditions.IndexOf(firstBlockCond), true);
                }

                // Just listing the conditions for now
                var exampleLines = ExplainBlock(exampleBlock, ctx);
                sbChain.AppendLine($"- {string.Join(" AND ", exampleLines)}");
                bIdx++;
            }
            return sbChain.ToString().TrimEnd();
        }

        private List<string> ExplainBlock(List<AchievementCondition> block, ExplanationContext ctx)
        {
            var lines = new List<string>();
            foreach (var c in block)
            {
                if (c.Flag == "Add Address") continue;
                int realIdx = ctx.Group.Conditions.IndexOf(c);
                lines.Add(ExplainSingleCondition(c, ctx, realIdx, true, true));
            }
            return lines;
        }

        private string ExplainSingleCondition(AchievementCondition cond, ExplanationContext ctx, int index, bool suppressHits, bool suppressPointers)
        {
            string left = ctx.GetOperandText(cond.LeftOperand, index, false, "", "", suppressPointers);
            string right = ctx.GetOperandText(cond.RightOperand, index, true, cond.LeftOperand.Value, cond.LeftOperand.Size);
            string op = ctx.GetOperatorText(cond.Operator);
            string hits = (!suppressHits && cond.RequiredHits > 0) ? $" (occurs {cond.RequiredHits} times)" : "";
            if (left.Contains("{recall}")) left = "recalled value";
            return $"{left} {op} {right}{hits}";
        }

        private string FindMeasuredTargetName(AchievementConditionGroup group)
        {
            int measuredIdx = group.Conditions.FindIndex(c => c.Flag == "Measured" || c.Flag == "Measured%");
            if (measuredIdx > 0)
            {
                string prevFlag = group.Conditions[measuredIdx - 1].Flag;
                if (prevFlag == "Add Hits" || prevFlag == "Sub Hits") return "hits";
                else if (prevFlag == "Add Source" || prevFlag == "Sub Source") return "value";
            }
            return "";
        }
    }
}