using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Services.Explainer.Processors
{
    public class AccumulatorProcessor : ILogicProcessor
    {
        public int Priority => 10; // Check Accumulators before single lines

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
            var usedIndices = new HashSet<int>();

            // Extract the first accumulator chain
            var chain1 = ExtractChain(conditions, index, out int ptr1);
            foreach (var c in chain1) usedIndices.Add(conditions.IndexOf(c));

            AchievementCondition? finalCond1 = null;
            if (ptr1 < conditions.Count) finalCond1 = conditions[ptr1]; // The condition ending the chain (the comparison)

            // Lookahead for a second chain to merge (detect Delta transition: Sum A < X -> Sum B = X)
            List<AchievementCondition>? chain2 = null;
            AchievementCondition? finalCond2 = null;
            int ptr2 = ptr1 + 1;

            if (ptr2 < conditions.Count && !usedIndices.Contains(ptr2))
            {
                if (CanProcess(conditions, ptr2)) // Recursive check if next is accumulator
                {
                    chain2 = ExtractChain(conditions, ptr2, out int end2);
                    if (end2 < conditions.Count)
                    {
                        finalCond2 = conditions[end2];
                        ptr2 = end2; // Update pointer
                    }
                }
            }

            // Attempt Merge into Transition Logic
            bool merged = false;
            if (chain1.Count > 0 && chain2 != null && chain2.Count > 0 && finalCond1 != null && finalCond2 != null)
            {
                if (AreChainsDeltaPair(chain1, chain2))
                {
                    string op1 = finalCond1.Operator;
                    string op2 = finalCond2.Operator;
                    string val1 = ctx.FormatValue(finalCond1.RightOperand, conditions.IndexOf(finalCond1));
                    string val2 = ctx.FormatValue(finalCond2.RightOperand, conditions.IndexOf(finalCond2));

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

                    var blocks = SegmentLogicBlocks(chain1);
                    string id = Guid.NewGuid().ToString("N");
                    result.DetailedDrillDowns[id] = GenerateChainDetails(blocks, ctx);
                    string expandToken = $"[[EXPAND:{id}:{blocks.Count}]]";

                    result.Text = $"Sum of {blocks.Count} items {expandToken} changes from {fromVal} to {toVal}";
                    result.Category = ExplanationCategory.Transition;
                    // Consumes both chains + their terminals
                    result.ConditionsConsumed = (ptr2 - index) + 1;
                    merged = true;
                }
            }

            if (!merged)
            {
                // Fallback: Process chain 1 only
                var blocks = SegmentLogicBlocks(chain1);
                string id = Guid.NewGuid().ToString("N");
                result.DetailedDrillDowns[id] = GenerateChainDetails(blocks, ctx);
                string expandToken = $"[[EXPAND:{id}:{blocks.Count}]]";

                if (finalCond1 != null)
                {
                    if (finalCond1.Flag == "MeasuredIf")
                    {
                        // Look for measured target in group
                        string measuredTargetName = FindMeasuredTargetName(ctx.Group);
                        string accSuffix = !string.IsNullOrEmpty(measuredTargetName)
                            ? $". This condition controls when {measuredTargetName} is measured"
                            : ". (MeasuredIf)";

                        result.Text = $"Accumulator chain (sum of {blocks.Count} items {expandToken}){accSuffix}";
                        result.Category = ExplanationCategory.MeasuredIf;
                    }
                    else if (finalCond1.Flag.StartsWith("Measured"))
                    {
                        bool isHitChain = finalCond1.Flag.Contains("Hits"); // Actually check Flag on the cond ending the accumulator isn't fully accurate for Hits, usually flag is on cond itself.
                        // Accumulators usually end with a comparison. If that comparison has Measured, it's measuring the result.
                        string label = "value";
                        // Note: AddHits makes the accumulator count hits.
                        if (chain1.Any(c => c.Flag.Contains("Hits"))) label = "hits";

                        string valText = (finalCond1.RequiredHits > 0) ? finalCond1.RequiredHits.ToString() : ctx.FormatValue(finalCond1.RightOperand, conditions.IndexOf(finalCond1));
                        result.Text = $"Start measuring {label} up to {valText}: {expandToken}";
                    }
                    else
                    {
                        string valText = (finalCond1.RequiredHits > 0) ? finalCond1.RequiredHits.ToString() : ctx.FormatValue(finalCond1.RightOperand, conditions.IndexOf(finalCond1));
                        string opText = ctx.GetOperatorText(finalCond1.Operator);
                        if (chain1.Any(c => c.Flag.Contains("Hits")))
                            result.Text = $"Trigger when at least {valText} of the following occur: {expandToken}";
                        else
                            result.Text = $"Sum of {blocks.Count} items {expandToken} {opText} {valText}";
                    }
                }
                else
                {
                    result.Text = $"Logic sequence {expandToken}";
                }

                // Consumes Chain1 + Terminal
                result.ConditionsConsumed = (ptr1 - index) + 1;
            }

            return result;
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

        // --- Helpers ported from LogicExplainer.cs ---

        private bool AreChainsDeltaPair(List<AchievementCondition> chain1, List<AchievementCondition> chain2)
        {
            if (chain1.Count != chain2.Count) return false;
            bool chain1HasDelta = false;
            bool chain2HasDelta = false;

            for (int i = 0; i < chain1.Count; i++)
            {
                var c1 = chain1[i];
                var c2 = chain2[i];
                if (c1.Flag != c2.Flag) return false;
                if (c1.Flag == "Add Address")
                {
                    if (c1.LeftOperand.Value != c2.LeftOperand.Value) return false;
                    continue;
                }
                bool c1IsDelta = c1.LeftOperand.Type == "Delta" || c1.LeftOperand.Type == "Prior";
                bool c1IsMem = c1.LeftOperand.Type == "Mem";
                bool c2IsDelta = c2.LeftOperand.Type == "Delta" || c2.LeftOperand.Type == "Prior";
                bool c2IsMem = c2.LeftOperand.Type == "Mem";
                if (c1.LeftOperand.Value != c2.LeftOperand.Value) return false;

                if (c1IsDelta) chain1HasDelta = true;
                if (c2IsDelta) chain2HasDelta = true;
                if (c1IsDelta && c2IsMem) continue;
                if (c1IsMem && c2IsDelta) continue;
                if (c1.LeftOperand.Type == "Value" && c2.LeftOperand.Type == "Value") continue;
                return false;
            }
            return (chain1HasDelta && !chain2HasDelta) || (!chain1HasDelta && chain2HasDelta);
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

        private int DetectConditionSequence(List<List<AchievementCondition>> blocks, int startIndex)
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

        private string GetBlockFingerprint(List<AchievementCondition> block)
        {
            var sb = new StringBuilder();
            foreach (var c in block)
            {
                sb.Append(c.Flag).Append('|');
                sb.Append(c.LeftOperand.Type).Append('|');
                // Ignore address for mem to detect structural matches
                if (c.LeftOperand.Type != "Mem" && c.LeftOperand.Type != "Delta" && c.LeftOperand.Type != "Prior")
                    sb.Append(c.LeftOperand.Value);
                sb.Append('|').Append(c.Operator).Append('|');
                sb.Append(c.RightOperand.Type).Append('|');
                if (c.RightOperand.Type != "Mem" && c.RightOperand.Type != "Delta" && c.RightOperand.Type != "Prior")
                    sb.Append(c.RightOperand.Value);
                sb.Append('|').Append(c.RequiredHits).Append(';');
            }
            return sb.ToString();
        }

        private string GenerateChainDetails(List<List<AchievementCondition>> blocks, ExplanationContext ctx)
        {
            var sbChain = new StringBuilder();
            int bIdx = 0;
            while (bIdx < blocks.Count)
            {
                int seqLen = DetectConditionSequence(blocks, bIdx);
                if (seqLen > 2)
                {
                    // Array Pattern
                    var exampleBlock = blocks[bIdx];
                    var firstBlockCond = exampleBlock.FirstOrDefault(c => c.Flag != "Add Address");
                    var lastBlock = blocks[bIdx + seqLen - 1];
                    var lastBlockCond = lastBlock.FirstOrDefault(c => c.Flag != "Add Address");

                    var exampleLines = ExplainBlock(exampleBlock, ctx);

                    sbChain.AppendLine($"- {seqLen} conditions matching pattern (Array):");

                    if (firstBlockCond != null && lastBlockCond != null)
                    {
                        string nameBase = ctx.GetName(firstBlockCond.LeftOperand.Value, ctx.Group.Conditions.IndexOf(firstBlockCond), true);
                        string offsetStart = "0x" + firstBlockCond.LeftOperand.Value.Replace("0x", "");
                        string offsetEnd = "0x" + lastBlockCond.LeftOperand.Value.Replace("0x", "");

                        string nameLast = ctx.GetName(lastBlockCond.LeftOperand.Value, ctx.Group.Conditions.IndexOf(lastBlockCond), true);
                        string cleanBase = Regex.Replace(nameBase, @"\s\+0x[0-9A-Fa-f]+$", "");
                        string cleanLast = Regex.Replace(nameLast, @"\s\+0x[0-9A-Fa-f]+$", "");

                        if (cleanBase == cleanLast && nameBase.Contains("+0x"))
                        {
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
                            sbChain.AppendLine($"  > {string.Join(" AND ", exampleLines)}");
                            // Iterate remainder
                            for (int k = 1; k < seqLen; k++)
                            {
                                var nextBlockLines = ExplainBlock(blocks[bIdx + k], ctx);
                                sbChain.AppendLine($"  > {string.Join(" AND ", nextBlockLines)}");
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
                    var blockLines = ExplainBlock(blocks[bIdx], ctx);
                    sbChain.AppendLine($"- {string.Join(" AND ", blockLines)}");
                    bIdx++;
                }
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
                // Temporarily instantiate a standard processor to explain the single line
                // or replicate simple explanation logic here
                lines.Add(ExplainSingleCondition(c, ctx, realIdx, true, true));
            }
            return lines;
        }

        private string ExplainSingleCondition(AchievementCondition cond, ExplanationContext ctx, int index, bool suppressHits, bool suppressPointers)
        {
            string left = ctx.GetOperandText(cond.LeftOperand, index, false, "", suppressPointers);
            string right = ctx.GetOperandText(cond.RightOperand, index, true, cond.LeftOperand.Value);
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