// - AccumulatorProcessor.cs -
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
        public int Priority => 3;

        // Definition for internal chain tracking
        private class AccumulatorChainDef
        {
            public List<AchievementCondition> Conditions { get; set; } = new();
            public AchievementCondition? Terminal { get; set; }
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
        }

        public bool CanProcess(List<AchievementCondition> conditions, int currentIndex)
        {
            // Look ahead to see if this chain eventually feeds into an Accumulator
            int scan = currentIndex;
            while (scan < conditions.Count)
            {
                string f = conditions[scan].Flag;

                // If we hit an explicit Accumulator flag, we can process this block
                if (f.Contains("Add") || f.Contains("Sub")) return true;

                // If we hit a non-chaining flag (Pause, Reset, Trigger, Measured), we can't process it as an accumulator
                // Exception: AddAddress is part of the chain logic
                if (f != "AndNext" && f != "OrNext" && f != "ResetNextIf" && f != "Add Address")
                    return false;

                scan++;
            }
            return false;
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var result = new ProcessorResult { Category = ExplanationCategory.Complex };
            var chains = new List<AccumulatorChainDef>();
            int currentPtr = index;

            while (currentPtr < conditions.Count)
            {
                var conditionsInChain = ExtractChain(conditions, currentPtr, out int endPtr);

                // If ExtractChain returned nothing, or we hit a wall, stop.
                if (conditionsInChain.Count == 0) break;

                // Validation: A valid accumulator unit MUST contain at least one Add/Sub flag
                // Otherwise we just extracted a random AndNext chain that belongs to a Trigger.
                bool hasAccumulatorAction = conditionsInChain.Any(c => c.Flag.Contains("Add") || c.Flag.Contains("Sub"));
                if (!hasAccumulatorAction) break;

                AchievementCondition? termCond = null;
                if (endPtr < conditions.Count) termCond = conditions[endPtr];

                var chainDef = new AccumulatorChainDef
                {
                    Conditions = conditionsInChain,
                    Terminal = termCond,
                    StartIndex = currentPtr,
                    EndIndex = endPtr
                };

                // Structure check for array grouping
                if (chains.Count > 0)
                {
                    if (!AreChainsStructureEqual(chains[0].Conditions, chainDef.Conditions))
                        break;
                }

                chains.Add(chainDef);

                // Determine next step
                bool hasExplicitConnector = termCond != null && (termCond.Flag == "AndNext" || termCond.Flag == "OrNext");

                if (hasExplicitConnector)
                {
                    currentPtr = endPtr + 1;
                }
                else
                {
                    // Check implicit connectivity
                    int nextIndex = endPtr + 1;
                    if (nextIndex > currentPtr && nextIndex < conditions.Count && CanProcess(conditions, nextIndex))
                    {
                        currentPtr = nextIndex;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (chains.Count == 0)
            {
                return new StandardConditionProcessor().Process(ctx, conditions, index);
            }

            // --- RECALL LOGIC CONTEXT ---
            string recallContext = "";
            bool hasRecallContext = false;

            if (index > 0 && conditions[index - 1].Flag == "Remember")
            {
                var remCond = conditions[index - 1];
                bool chainUsesRecall = chains.Any(ch => ch.Conditions.Any(c => c.LeftOperand.Type == "Recall" || c.RightOperand.Type == "Recall"));

                if (chainUsesRecall)
                {
                    bool leftIsDelta = remCond.LeftOperand.Type == "Delta";
                    bool leftIsMem = remCond.LeftOperand.Type == "Mem";
                    bool rightIsDelta = remCond.RightOperand.Type == "Delta";
                    bool rightIsMem = remCond.RightOperand.Type == "Mem";

                    Operand? targetOp = null;
                    string direction = "";

                    if (remCond.Operator == "-")
                    {
                        if (leftIsDelta && rightIsMem) { targetOp = remCond.RightOperand; direction = "decreasing"; }
                        else if (leftIsMem && rightIsDelta) { targetOp = remCond.LeftOperand; direction = "increasing"; }
                    }

                    if (targetOp != null)
                    {
                        string varName = ctx.GetName(targetOp, index - 1, true);
                        recallContext = $"{varName} {direction}";
                        hasRecallContext = true;
                    }
                    else
                    {
                        recallContext = "stored value";
                    }
                }
            }

            // 2. Analyze Chains for Transition (Delta -> Mem)
            var deltaChains = chains.Where(c => IsChainDelta(c.Conditions)).ToList();
            var memChains = chains.Where(c => !IsChainDelta(c.Conditions)).ToList();

            bool isTransition = false;
            if (deltaChains.Count > 0 && memChains.Count > 0)
            {
                var lastDelta = deltaChains.Last();
                if (lastDelta.Terminal == null || lastDelta.Terminal.Flag != "OrNext")
                {
                    isTransition = true;
                }
            }

            if (isTransition)
            {
                var fromConditions = deltaChains.Select(c => FormatTerminalCondition(c.Terminal, ctx, conditions)).ToList();
                var toConditions = memChains.Select(c => FormatTerminalCondition(c.Terminal, ctx, conditions)).ToList();

                var fromBuilder = new StringBuilder();
                for (int i = 0; i < deltaChains.Count; i++)
                {
                    var chain = deltaChains[i];
                    if (i > 0)
                    {
                        var prev = deltaChains[i - 1];
                        string conn = (prev.Terminal != null && prev.Terminal.Flag == "OrNext") ? " OR " : " AND ";
                        fromBuilder.Append(conn);
                    }
                    fromBuilder.Append(FormatTerminalCondition(chain.Terminal, ctx, conditions));
                }

                var toBuilder = new StringBuilder();
                for (int i = 0; i < memChains.Count; i++)
                {
                    var chain = memChains[i];
                    if (i > 0)
                    {
                        var prev = memChains[i - 1];
                        string conn = (prev.Terminal != null && prev.Terminal.Flag == "OrNext") ? " OR " : " AND ";
                        toBuilder.Append(conn);
                    }
                    toBuilder.Append(FormatTerminalCondition(chain.Terminal, ctx, conditions));
                }

                var displayBlocks = PrepareDisplayBlocks(chains[0]);

                string id = Guid.NewGuid().ToString("N");
                result.DetailedDrillDowns[id] = GenerateChainDetails(displayBlocks, ctx, chains[0].Terminal);
                string expandToken = $"[[EXPAND:{id}:{displayBlocks.Count}]]";

                string suffix = "";
                var finalMemChain = memChains.Last();
                if (finalMemChain.Terminal != null)
                {
                    if (finalMemChain.Terminal.Flag == "Reset If") suffix = " (Reset If)";
                    else if (finalMemChain.Terminal.Flag == "Pause If") suffix = " (Pause If)";
                }

                result.Text = $"Sum of {displayBlocks.Count} items {expandToken} changes from ({fromBuilder}) to ({toBuilder}){suffix}";
                result.Category = ExplanationCategory.Transition;
            }
            else
            {
                var sb = new StringBuilder();
                bool isTriggerStart = true;

                for (int i = 0; i < chains.Count; i++)
                {
                    var chain = chains[i];
                    var displayBlocks = PrepareDisplayBlocks(chain);

                    string id = Guid.NewGuid().ToString("N");
                    result.DetailedDrillDowns[id] = GenerateChainDetails(displayBlocks, ctx, chain.Terminal);
                    string expandToken = $"[[EXPAND:{id}:{displayBlocks.Count}]]";

                    string chainText = "";

                    if (chain.Terminal != null)
                    {
                        var term = chain.Terminal;
                        string noun = hasRecallContext ? recallContext : "items";

                        if (term.Flag == "MeasuredIf")
                        {
                            string valText = ctx.FormatValue(term.RightOperand, conditions.IndexOf(term), term.LeftOperand.Value, term.LeftOperand.Size);
                            string opText = ctx.GetOperatorText(term.Operator);
                            chainText = $"Accumulator: sum of {displayBlocks.Count} {noun} {expandToken} {opText} {valText} (MeasuredIf)";
                            result.Category = ExplanationCategory.MeasuredIf;
                            isTriggerStart = false;
                        }
                        else if (term.Flag.StartsWith("Measured"))
                        {
                            bool isHitChain = chain.Conditions.Any(c => c.Flag.Contains("Hits")) || term.Flag.Contains("Hits");
                            string label = isHitChain ? "hits" : "value";
                            string valText = (term.RequiredHits > 0) ? term.RequiredHits.ToString() : ctx.FormatValue(term.RightOperand, conditions.IndexOf(term), term.LeftOperand.Value, term.LeftOperand.Size);

                            if (hasRecallContext)
                                chainText = $"Start measuring {recallContext} up to {valText}: {expandToken}";
                            else
                                chainText = $"Start measuring {label} up to {valText}: {expandToken}";

                            isTriggerStart = false;
                        }
                        else
                        {
                            string valText = (term.RequiredHits > 0) ? term.RequiredHits.ToString() : ctx.FormatValue(term.RightOperand, conditions.IndexOf(term), term.LeftOperand.Value, term.LeftOperand.Size);
                            string opText = ctx.GetOperatorText(term.Operator);

                            string prefix = "";
                            if (isTriggerStart)
                            {
                                if (term.Flag == "Pause If")
                                {
                                    prefix = (term.RequiredHits > 0) ? "Lock the achievement (PauseLock) when " : "Pause the achievement when ";
                                    result.Category = ExplanationCategory.Pause;
                                }
                                else if (term.Flag == "Reset If")
                                {
                                    prefix = "Reset the achievement when ";
                                    result.Category = ExplanationCategory.Reset;
                                }
                                else
                                {
                                    prefix = "Trigger when ";
                                }
                                isTriggerStart = false;
                            }

                            if (chain.Conditions.Any(c => c.Flag.Contains("Hits")))
                            {
                                if (hasRecallContext)
                                    chainText = $"{prefix}{recallContext} decreasing occurs at least {valText} times: {expandToken}";
                                else
                                    chainText = $"{prefix}at least {valText} of the following occur: {expandToken}";
                            }
                            else
                            {
                                chainText = $"{prefix}sum of {displayBlocks.Count} {noun} {expandToken} {opText} {valText}";
                            }
                        }

                        if (i < chains.Count - 1)
                        {
                            string conn = term.Flag == "OrNext" ? " OR " : " AND ";
                            chainText += conn;
                        }
                    }
                    else
                    {
                        string noun = hasRecallContext ? recallContext : "Logic sequence";
                        chainText = $"{noun} {expandToken} (Incomplete)";
                    }

                    sb.Append(chainText);
                }

                result.Text = sb.ToString();
            }

            if (chains.Count > 0)
            {
                int lastIndex = chains.Last().EndIndex;
                result.ConditionsConsumed = (lastIndex - index) + 1;
            }
            else
            {
                result.ConditionsConsumed = 1;
            }

            return result;
        }

        private List<AchievementCondition> ExtractChain(List<AchievementCondition> conditions, int start, out int endPtr)
        {
            var chain = new List<AchievementCondition>();
            int ptr = start;
            var buffer = new List<AchievementCondition>();

            while (ptr < conditions.Count)
            {
                var c = conditions[ptr];

                if (c.Flag == "Add Address")
                {
                    buffer.Add(c);
                }
                else if (c.Flag == "AndNext" || c.Flag == "OrNext" || c.Flag == "ResetNextIf")
                {
                    buffer.Add(c);
                }
                else if (c.Flag.Contains("Add") || c.Flag.Contains("Sub"))
                {
                    chain.AddRange(buffer);
                    buffer.Clear();
                    chain.Add(c);
                }
                else
                {
                    break;
                }
                ptr++;
            }
            endPtr = ptr;
            ptr -= buffer.Count;
            endPtr = ptr;

            return chain;
        }

        private List<List<AchievementCondition>> PrepareDisplayBlocks(AccumulatorChainDef chain)
        {
            var blocks = new List<List<AchievementCondition>>();
            var currentBlock = new List<AchievementCondition>();

            foreach (var c in chain.Conditions)
            {
                currentBlock.Add(c);
                if (c.Flag.Contains("Add") || c.Flag.Contains("Sub"))
                {
                    blocks.Add(new List<AchievementCondition>(currentBlock));
                    currentBlock.Clear();
                }
            }
            if (currentBlock.Count > 0) blocks.Add(currentBlock);

            if (chain.Terminal != null)
            {
                if (blocks.Count > 0 && blocks.Last().All(c => c.Flag == "Add Address"))
                {
                    blocks.Last().Add(chain.Terminal);
                }
                else
                {
                    blocks.Add(new List<AchievementCondition> { chain.Terminal });
                }
            }
            return blocks;
        }

        private string GenerateChainDetails(List<List<AchievementCondition>> blocks, ExplanationContext ctx, AchievementCondition? terminal)
        {
            var sbChain = new StringBuilder();
            foreach (var block in blocks)
            {
                var lines = new List<string>();
                foreach (var c in block)
                {
                    if (c.Flag == "Add Address") continue;
                    bool isTerm = (terminal != null && c == terminal);
                    lines.Add(ExplainSingleCondition(c, ctx, ctx.Group.Conditions.IndexOf(c), true, true, isTerm));
                }

                if (lines.Count > 0)
                {
                    sbChain.AppendLine($"- {string.Join(" AND ", lines)}");
                }
            }
            return sbChain.ToString().TrimEnd();
        }

        private List<string> ExplainBlock(List<AchievementCondition> block, ExplanationContext ctx, AchievementCondition? terminal)
        {
            var lines = new List<string>();
            foreach (var c in block)
            {
                if (c.Flag == "Add Address") continue;
                bool isTerm = (terminal != null && c == terminal);
                lines.Add(ExplainSingleCondition(c, ctx, ctx.Group.Conditions.IndexOf(c), true, true, isTerm));
            }
            return lines;
        }

        private string ExplainSingleCondition(AchievementCondition cond, ExplanationContext ctx, int index, bool suppressHits, bool suppressPointers, bool isTerminalItem)
        {
            string left = ctx.GetOperandText(cond.LeftOperand, index, false, "", "", suppressPointers);
            string leftClean = left.Replace("previous frame ", "").Replace("prior frame ", "");
            if (isTerminalItem) return leftClean.Trim();
            string right = ctx.GetOperandText(cond.RightOperand, index, true, cond.LeftOperand.Value, cond.LeftOperand.Size);
            string op = ctx.GetOperatorText(cond.Operator);
            string hits = (!suppressHits && cond.RequiredHits > 0) ? $" (occurs {cond.RequiredHits} times)" : "";
            if (leftClean.Contains("{recall}")) leftClean = "recalled value";
            return $"{leftClean} {op} {right}{hits}".Trim();
        }

        private string FormatTerminalCondition(AchievementCondition? cond, ExplanationContext ctx, List<AchievementCondition> conditions)
        {
            if (cond == null) return "(No Check)";
            string val = ctx.FormatValue(cond.RightOperand, conditions.IndexOf(cond), cond.LeftOperand.Value, cond.LeftOperand.Size);
            string op = cond.Operator;
            if (op == "=") return val;
            return $"{op} {val}";
        }

        private bool IsChainDelta(List<AchievementCondition> chain) =>
            chain.Any(c => c.LeftOperand.Type == "Delta" || c.LeftOperand.Type == "Prior");

        private bool AreChainsStructureEqual(List<AchievementCondition> chain1, List<AchievementCondition> chain2)
        {
            if (chain1.Count != chain2.Count) return false;
            for (int i = 0; i < chain1.Count; i++)
            {
                var c1 = chain1[i]; var c2 = chain2[i];
                if (c1.Flag != c2.Flag) return false;
                if (!AreValuesEqual(c1.LeftOperand.Value, c2.LeftOperand.Value)) return false;

                if (c1.LeftOperand.Type != c2.LeftOperand.Type && c1.Flag == "Add Address") return false;

                if (c1.LeftOperand.Size != c2.LeftOperand.Size) return false;
                if (c1.Operator != c2.Operator) return false;

                bool c1IsMemLike = IsMemLike(c1.LeftOperand.Type);
                bool c2IsMemLike = IsMemLike(c2.LeftOperand.Type);
                if (c1IsMemLike != c2IsMemLike) return false;

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
                return l1 == l2;
            return clean1.Equals(clean2, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsMemLike(string type)
        {
            return type == "Mem" || type == "Delta" || type == "Prior" || type == "BCD" || type == "Inverted";
        }
    }
}