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
            var cond = conditions[currentIndex];

            // 1. Direct Accumulator Start
            bool isAccumulatorStart = cond.Flag == "Add Source" || cond.Flag == "Sub Source" ||
                                      cond.Flag == "Add Hits" || cond.Flag == "Sub Hits";

            if (isAccumulatorStart) return true;

            // 2. Logic Chain Start (Add Address / AndNext / OrNext / ResetNextIf)
            bool isPotentialLead = cond.Flag == "Add Address" ||
                                   cond.Flag == "AndNext" ||
                                   cond.Flag == "OrNext" ||
                                   cond.Flag == "ResetNextIf";

            if (isPotentialLead)
            {
                int scan = currentIndex + 1;
                while (scan < conditions.Count)
                {
                    string f = conditions[scan].Flag;

                    if (f == "Add Address" || f == "AndNext" || f == "OrNext" || f == "ResetNextIf")
                    {
                        scan++;
                        continue;
                    }

                    if (f.Contains("Add") || f.Contains("Sub"))
                    {
                        return true;
                    }
                    break;
                }
            }
            return false;
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var result = new ProcessorResult { Category = ExplanationCategory.Complex };

            // 1. Collect all consecutive accumulator chains
            var chains = new List<AccumulatorChainDef>();
            int currentPtr = index;
            int referenceChainIndex = 0;

            while (currentPtr < conditions.Count)
            {
                var conditionsInChain = ExtractChain(conditions, currentPtr, out int endPtr);

                if (conditionsInChain.Count == 0) break;

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

                // Grouping Logic
                if (chains.Count > 0)
                {
                    var prevChain = chains.Last();
                    bool connectedByLogic = prevChain.Terminal != null &&
                                            (prevChain.Terminal.Flag == "AndNext" || prevChain.Terminal.Flag == "OrNext");

                    bool isTransitionBoundary = false;

                    // Transition Check: Delta -> Mem
                    if (!connectedByLogic)
                    {
                        bool prevIsDelta = IsChainDelta(prevChain.Conditions);
                        bool currIsDelta = IsChainDelta(chainDef.Conditions);

                        if (prevIsDelta && !currIsDelta)
                        {
                            isTransitionBoundary = true;
                            referenceChainIndex = chains.Count;
                        }
                    }

                    if (!connectedByLogic && !isTransitionBoundary)
                    {
                        if (!AreChainsStructureEqual(chains[referenceChainIndex].Conditions, chainDef.Conditions))
                            break;
                    }
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

                var rawBlocks = PrepareDisplayBlocks(chains[0]);
                var displayBlocks = DeduplicateBlocks(rawBlocks, ctx);

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
                    var rawBlocks = PrepareDisplayBlocks(chain);
                    var displayBlocks = DeduplicateBlocks(rawBlocks, ctx);

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
            chain.AddRange(buffer);
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
                if (c.Flag == "Add Source" || c.Flag == "Sub Source" ||
                    c.Flag == "Add Hits" || c.Flag == "Sub Hits")
                {
                    blocks.Add(new List<AchievementCondition>(currentBlock));
                    currentBlock.Clear();
                }
            }
            if (currentBlock.Count > 0) blocks.Add(currentBlock);

            if (chain.Terminal != null)
            {
                // Check for Dummy Terminal (Value X = Value Y)
                // If the terminal is just holding the hit count/limit and is logically dummy (0=1, 0=0),
                // we exclude it from the visualization blocks so it doesn't show up as "- 0x0"
                bool isDummyTerminal = chain.Terminal.LeftOperand.Type == "Value" &&
                                       chain.Terminal.RightOperand.Type == "Value";

                if (!isDummyTerminal)
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
            }
            return blocks;
        }

        // Helper to deduplicate identical variable blocks
        private List<List<AchievementCondition>> DeduplicateBlocks(List<List<AchievementCondition>> blocks, ExplanationContext ctx)
        {
            var unique = new List<List<AchievementCondition>>();
            var seenSignatures = new HashSet<string>();

            foreach (var block in blocks)
            {
                if (block.Count == 0) continue;

                var leaf = block.Last();

                // Bypass deduplication for Recall logic to ensure all steps are shown
                if (leaf.LeftOperand.Type == "Recall")
                {
                    unique.Add(block);
                    continue;
                }

                int index = ctx.Group.Conditions.IndexOf(leaf);
                string signature = ctx.GetName(leaf.LeftOperand, index, false);

                if (seenSignatures.Add(signature))
                {
                    unique.Add(block);
                }
            }
            return unique;
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
            {
                return l1 == l2;
            }
            return clean1.Equals(clean2, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsMemLike(string type)
        {
            return type == "Mem" || type == "Delta" || type == "Prior" || type == "BCD" || type == "Inverted";
        }
    }
}