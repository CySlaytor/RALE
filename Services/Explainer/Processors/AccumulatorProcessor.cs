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

            // 1. Direct Accumulator Start
            bool isAccumulatorStart = cond.Flag == "Add Source" || cond.Flag == "Sub Source" ||
                                      cond.Flag == "Add Hits" || cond.Flag == "Sub Hits";

            if (isAccumulatorStart) return true;

            // 2. Logic Chain Start (Add Address / AndNext / OrNext / ResetNextIf)
            // We need to look ahead to see if this chain feeds into an accumulator.
            // If we don't do this, the AndNextProcessor (Priority 15) will steal the start of the chain,
            // breaking the "Array/Sum" grouping logic.
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

                    // Continue scanning through setup flags
                    if (f == "Add Address" || f == "AndNext" || f == "OrNext" || f == "ResetNextIf")
                    {
                        scan++;
                        continue;
                    }

                    // Found target?
                    if (f.Contains("Add") || f.Contains("Sub"))
                    {
                        // It IS an accumulator chain
                        return true;
                    }

                    // Hit a terminal or unrelated flag (e.g. empty, PauseIf, Measured) -> Not an accumulator
                    break;
                }
            }
            return false;
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var result = new ProcessorResult { Category = ExplanationCategory.Complex };

            // 1. Collect all consecutive accumulator chains that are connected by logic gates or implicit grouping
            var chains = new List<AccumulatorChainDef>();
            int currentPtr = index;

            while (currentPtr < conditions.Count)
            {
                // Extract body of the accumulator (AddSource/SubSource lines)
                var conditionsInChain = ExtractChain(conditions, currentPtr, out int endPtr);

                if (conditionsInChain.Count == 0) break;

                AchievementCondition? termCond = null;
                if (endPtr < conditions.Count) termCond = conditions[endPtr];

                var chainDef = new AccumulatorChainDef
                {
                    Conditions = conditionsInChain,
                    Terminal = termCond,
                    StartIndex = currentPtr,
                    EndIndex = endPtr // Terminal index
                };

                // If this is not the first chain, verify it matches the structure of the first chain
                // This ensures we only group accumulators counting the same things (e.g. Array logic)
                if (chains.Count > 0)
                {
                    if (!AreChainsStructureEqual(chains[0].Conditions, chainDef.Conditions))
                        break;
                }

                chains.Add(chainDef);

                // Determine if we should continue collecting.
                // We continue if:
                // A) There is an explicit connector (AndNext/OrNext)
                // B) There is an implicit connector (No flag, but next line starts another accumulator)

                bool hasExplicitConnector = termCond != null && (termCond.Flag == "AndNext" || termCond.Flag == "OrNext");

                if (hasExplicitConnector)
                {
                    currentPtr = endPtr + 1;
                }
                else
                {
                    // Check ahead for implicit AND (next condition starts a valid accumulator chain)
                    int nextIndex = endPtr + 1;
                    if (nextIndex < conditions.Count && CanProcess(conditions, nextIndex))
                    {
                        currentPtr = nextIndex;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // --- RECALL LOGIC CONTEXT ---
            // Check if the previous line was a "Remember" instruction to add context to the variable name.
            string recallContext = "";
            bool hasRecallContext = false;

            if (index > 0 && conditions[index - 1].Flag == "Remember")
            {
                var remCond = conditions[index - 1];
                bool chainUsesRecall = chains.Any(ch => ch.Conditions.Any(c => c.LeftOperand.Type == "Recall" || c.RightOperand.Type == "Recall"));

                if (chainUsesRecall)
                {
                    // Attempt to decipher the Remember intent (Increase vs Decrease)
                    bool leftIsDelta = remCond.LeftOperand.Type == "Delta";
                    bool leftIsMem = remCond.LeftOperand.Type == "Mem";
                    bool rightIsDelta = remCond.RightOperand.Type == "Delta";
                    bool rightIsMem = remCond.RightOperand.Type == "Mem";

                    Operand? targetOp = null;
                    string direction = "";

                    if (remCond.Operator == "-")
                    {
                        // Delta - Mem = Decreasing (Value got smaller)
                        if (leftIsDelta && rightIsMem) { targetOp = remCond.RightOperand; direction = "decreasing"; }
                        // Mem - Delta = Increasing (Value got bigger)
                        else if (leftIsMem && rightIsDelta) { targetOp = remCond.LeftOperand; direction = "increasing"; }
                    }

                    if (targetOp != null)
                    {
                        // Get the name of the variable being tracked
                        string varName = ctx.GetName(targetOp, index - 1, true); // true = suppress "Pointer..." prefix for cleaner text

                        // Construct context string without leading "of" to fit sentence structure better
                        recallContext = $"{varName} {direction}";
                        hasRecallContext = true;
                    }
                    else
                    {
                        // Fallback if structure isn't simple subtraction
                        recallContext = "stored value";
                    }
                }
            }
            // ---------------------------

            // 2. Analyze Chains for Transition (Delta -> Mem)
            // Transition Requirements:
            // 1. Must have at least one Delta chain and one Mem chain.
            // 2. Logic must connect Delta group to Mem group via AND (standard transition logic).
            //    If last Delta chain connects with 'OrNext', it's disjoint logic (A or B), not transition (A becomes B).
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
                // Merge Logic: "Changes from (A and B) to (C)"
                var fromConditions = deltaChains.Select(c => FormatTerminalCondition(c.Terminal, ctx, conditions)).ToList();
                var toConditions = memChains.Select(c => FormatTerminalCondition(c.Terminal, ctx, conditions)).ToList();

                // Build "From" text (Delta conditions)
                var fromBuilder = new StringBuilder();
                for (int i = 0; i < deltaChains.Count; i++)
                {
                    var chain = deltaChains[i];
                    if (i > 0)
                    {
                        // Use connector from previous chain's terminal
                        var prev = deltaChains[i - 1];
                        string conn = (prev.Terminal != null && prev.Terminal.Flag == "OrNext") ? " OR " : " AND ";
                        fromBuilder.Append(conn);
                    }
                    fromBuilder.Append(FormatTerminalCondition(chain.Terminal, ctx, conditions));
                }

                // Build "To" text (Mem conditions)
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

                // FIX 1: Include Terminal in the block list for display
                var displayBlocks = PrepareDisplayBlocks(chains[0]);

                string id = Guid.NewGuid().ToString("N");
                result.DetailedDrillDowns[id] = GenerateChainDetails(displayBlocks, ctx, chains[0].Terminal);
                string expandToken = $"[[EXPAND:{id}:{displayBlocks.Count}]]";

                string suffix = "";
                // Check if the final Mem chain triggers a Reset or Pause
                var finalMemChain = memChains.Last();
                if (finalMemChain.Terminal != null)
                {
                    if (finalMemChain.Terminal.Flag == "Reset If") suffix = " (Reset If)";
                    else if (finalMemChain.Terminal.Flag == "Pause If") suffix = " (Pause If)";
                }

                // LogicExplainer.cs automatically adds "Trigger when" for Transition category.
                // We should NOT include it here to avoid duplication.
                result.Text = $"Sum of {displayBlocks.Count} items {expandToken} changes from ({fromBuilder}) to ({toBuilder}){suffix}";
                result.Category = ExplanationCategory.Transition;
            }
            else
            {
                // Multi-Chain Output (A OR B OR C)
                // We construct the text by iterating all chains and joining them.
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

                        // Special Flags on Terminal
                        if (term.Flag == "MeasuredIf")
                        {
                            string valText = ctx.FormatValue(term.RightOperand, conditions.IndexOf(term), term.LeftOperand.Value, term.LeftOperand.Size);
                            string opText = ctx.GetOperatorText(term.Operator);
                            chainText = $"Accumulator: sum of {displayBlocks.Count} {noun} {expandToken} {opText} {valText} (MeasuredIf)";
                            result.Category = ExplanationCategory.MeasuredIf;
                            isTriggerStart = false; // Reset prefixing for MeasuredIf
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
                            // Standard Condition (Trigger/Pause/Reset)
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

                        // Append Connector based on current terminal flag
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

            // Calculate total consumed conditions
            int lastIndex = chains.Last().EndIndex;
            result.ConditionsConsumed = (lastIndex - index) + 1;

            return result;
        }

        // Helper to merge the main chain blocks + the terminal condition for the display list
        private List<List<AchievementCondition>> PrepareDisplayBlocks(AccumulatorChainDef chain)
        {
            var blocks = SegmentLogicBlocks(chain.Conditions);
            if (chain.Terminal != null)
            {
                // Check if the last block is an "orphaned" pointer chain (Add Address)
                // that belongs to the terminal condition. If so, merge them.
                if (blocks.Count > 0)
                {
                    var lastBlock = blocks.Last();
                    var lastCond = lastBlock.Last();

                    bool isSetupBlock = lastCond.Flag == "Add Address";

                    if (isSetupBlock)
                    {
                        // Merge Terminal into this pointer block so GetName can resolve the full chain
                        lastBlock.Add(chain.Terminal);
                    }
                    else
                    {
                        // Last block was a complete item. Add Terminal as a new independent item.
                        blocks.Add(new List<AchievementCondition> { chain.Terminal });
                    }
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
            int bIdx = 0;
            while (bIdx < blocks.Count)
            {
                var exampleBlock = blocks[bIdx];
                // Pass terminal to ExplainBlock
                var exampleLines = ExplainBlock(exampleBlock, ctx, terminal);
                // Filter out empty lines (e.g. if a block was just Add Address that got consumed without output)
                var validLines = exampleLines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

                if (validLines.Count > 0)
                {
                    sbChain.AppendLine($"- {string.Join(" AND ", validLines)}");
                }
                bIdx++;
            }
            return sbChain.ToString().TrimEnd();
        }

        private List<string> ExplainBlock(List<AchievementCondition> block, ExplanationContext ctx, AchievementCondition? terminal)
        {
            var lines = new List<string>();
            foreach (var c in block)
            {
                if (c.Flag == "Add Address") continue;
                int realIdx = ctx.Group.Conditions.IndexOf(c);

                // Determine if this is the terminal condition we want to simplify (hide operator)
                bool isTerminal = (terminal != null && c == terminal);

                lines.Add(ExplainSingleCondition(c, ctx, realIdx, true, true, isTerminal));
            }
            return lines;
        }

        private string ExplainSingleCondition(AchievementCondition cond, ExplanationContext ctx, int index, bool suppressHits, bool suppressPointers, bool isTerminalItem)
        {
            string left = ctx.GetOperandText(cond.LeftOperand, index, false, "", "", suppressPointers);

            // Strip redundant "previous frame" for list items
            string leftClean = left.Replace("previous frame ", "").Replace("prior frame ", "");

            if (isTerminalItem)
            {
                // For the terminal item in the list, we only want to show the variable name (Left Operand).
                return leftClean.Trim();
            }

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
                // STRICTLY identifying accumulator components.
                // We do NOT include 'Next' here because 'Next' usually belongs to the terminal condition acting as a connector,
                // or breaks the accumulator logic if inside.
                if (c.Flag == "Add Source" || c.Flag == "Sub Source" ||
                    c.Flag == "Add Hits" || c.Flag == "Sub Hits" ||
                    c.Flag == "Add Address")
                {
                    chain.Add(c);
                }
                else
                {
                    // Any other flag (including AndNext, OrNext, ResetNextIf, ResetIf, or empty) 
                    // terminates the accumulator body definition.
                    break;
                }
                ptr++;
            }
            endPtr = ptr; // ptr points to the Terminal condition
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
    }
}