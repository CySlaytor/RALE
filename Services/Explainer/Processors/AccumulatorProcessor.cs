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
            // If this chain has already been consumed by a previous accumulator block (via lookahead), skip it.
            if (ctx.ProcessedIndices.Contains(index))
            {
                // We need to calculate how much to skip (length of current chain)
                var skipChain = ExtractChain(conditions, index, out int skipEnd);
                return new ProcessorResult { Text = "", Category = ExplanationCategory.Complex, ConditionsConsumed = (skipEnd - index) + 1 };
            }

            var result = new ProcessorResult { Category = ExplanationCategory.Complex };

            // 1. Collect all accumulator chains, including non-contiguous ones
            var chains = new List<AccumulatorChainDef>();
            int currentPtr = index;
            int referenceChainIndex = 0;

            // Main collection loop
            while (currentPtr < conditions.Count)
            {
                // If we are scanning ahead and hit a chain already processed (unlikely in this logic flow, but safe)
                if (ctx.ProcessedIndices.Contains(currentPtr)) break;

                // Extract a single contiguous accumulator chain
                var conditionsInChain = ExtractChain(conditions, currentPtr, out int endPtr);

                if (conditionsInChain.Count == 0)
                {
                    // No chain found here. Check if we should stop or scan forward.
                    // If we have existing chains, we try to skip intermediate non-accumulator logic.
                    if (chains.Count > 0)
                    {
                        // Check for barriers: ResetIf, PauseIf, OrNext
                        var currCond = conditions[currentPtr];
                        if (currCond.Flag == "Reset If" || currCond.Flag == "Pause If" || currCond.Flag == "OrNext")
                        {
                            break; // Barrier found, stop grouping
                        }

                        // If it's a standard condition (empty flag) or other safe intermediates, skip and continue scanning
                        currentPtr++;
                        continue;
                    }
                    else
                    {
                        break; // Nothing found at start
                    }
                }

                // Verify it's actually an accumulator chain (contains Add/Sub)
                bool hasAccumulatorAction = conditionsInChain.Any(c =>
                    c.Flag == "Add Source" || c.Flag == "Sub Source" ||
                    c.Flag == "Add Hits" || c.Flag == "Sub Hits");

                if (!hasAccumulatorAction)
                {
                    // This chain extracted only setup flags (e.g. Add Address) but no actual Add/Sub action.
                    // This shouldn't happen if CanProcess passed, but safety check.
                    // Treat as intermediate logic?
                    if (chains.Count > 0)
                    {
                        currentPtr = endPtr + 1;
                        continue;
                    }
                    break;
                }

                // Valid Chain Found
                AchievementCondition? termCond = null;
                if (endPtr < conditions.Count) termCond = conditions[endPtr];

                // Check if this chain is connected to previous chains via OrNext/AndNext at the terminal level.
                // If the previous chain ended with `OrNext`, we MUST be contiguous.
                // If we skipped lines to get here, but the previous chain ended with `OrNext`, that's invalid logic (OrNext implies immediate follow-up).
                if (chains.Count > 0)
                {
                    var prev = chains.Last();
                    bool prevConnected = prev.Terminal != null && (prev.Terminal.Flag == "OrNext" || prev.Terminal.Flag == "AndNext");

                    // Calculate if we skipped lines
                    bool skippedLines = currentPtr > (prev.EndIndex + 1);

                    // If previous chain demanded a connection but we skipped lines, this grouping is invalid.
                    // However, `OrNext` usually binds the accumulator result. `Acc1 OR Acc2`.
                    // If we have `Acc1 OrNext` ... `Mem=1` ... `Acc2`, logic is `(Acc1 OR Acc2) AND Mem=1`? 
                    // No, RA parses strictly sequentially. `Acc1` evaluation includes `OrNext`.
                    // The `Mem=1` breaks the `OrNext` binding.
                    // So if `prevConnected` is true, we CANNOT have skipped lines.
                    if (prevConnected && skippedLines)
                    {
                        break;
                    }
                }

                var chainDef = new AccumulatorChainDef
                {
                    Conditions = conditionsInChain,
                    Terminal = termCond,
                    StartIndex = currentPtr,
                    EndIndex = endPtr
                };

                // "Terminal Stealing" Logic (for naked gates)
                if (chains.Count > 0 && chainDef.Conditions.Count > 0)
                {
                    var prev = chains.Last();
                    var first = chainDef.Conditions[0];

                    bool isNakedGate = (first.Flag == "AndNext" || first.Flag == "OrNext" || first.Flag == "ResetNextIf")
                                       && !first.Flag.Contains("Add") && !first.Flag.Contains("Sub");

                    if (prev.Terminal == null && isNakedGate)
                    {
                        prev.Terminal = first;
                        chainDef.Conditions.RemoveAt(0);

                        if (chainDef.Conditions.Count == 0 || !chainDef.Conditions.Any(c => c.Flag.Contains("Add") || c.Flag.Contains("Sub")))
                        {
                            // Chain consumed by terminal steal, move pointer
                            currentPtr = chainDef.StartIndex + 1;
                            continue;
                        }
                    }
                }

                // Add to collection and mark as processed
                chains.Add(chainDef);

                // Mark indices of this chain (except the first one, which is 'index' and is being processed now)
                // We will handle 'ConditionsConsumed' for the first chain, but mark subsequent chains to be skipped.
                if (chains.Count > 1)
                {
                    ctx.ProcessedIndices.Add(currentPtr);
                }

                // Determine next scan pointer
                bool hasExplicitConnector = termCond != null && (termCond.Flag == "AndNext" || termCond.Flag == "OrNext");

                if (hasExplicitConnector)
                {
                    currentPtr = endPtr + 1;
                }
                else
                {
                    // Scan forward to look for split accumulator blocks
                    // We just increment pointer and loop, the loop logic handles skipping.
                    currentPtr = endPtr + 1;
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

                string fromText = BuildCondensedChainString(deltaChains, ctx, conditions);
                string toText = BuildCondensedChainString(memChains, ctx, conditions);

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

                result.Text = $"Sum of {displayBlocks.Count} items {expandToken} changes from ({fromText}) to ({toText}){suffix}";
                result.Category = ExplanationCategory.Transition;
            }
            else
            {
                // -- Measured Context Detection --
                AchievementCondition? finalTerminal = chains.Count > 0 ? chains.Last().Terminal : null;
                bool isMeasuredContext = false;
                string measuredHeader = "";

                // If we grouped non-contiguous chains, we need to verify if the FINAL chain is Measured.
                // Or if there is a Measured condition immediately following the last chain (External Trigger).
                // Check if the condition AFTER the last chain is Measured
                int lastChainEnd = chains.Last().EndIndex;
                if (lastChainEnd + 1 < conditions.Count)
                {
                    var nextCond = conditions[lastChainEnd + 1];
                    bool chainsUseHits = chains.Any(ch => ch.Conditions.Any(c => c.Flag.Contains("Hits")) || (ch.Terminal != null && ch.Terminal.Flag.Contains("Hits")));

                    if (chainsUseHits && nextCond.Flag.StartsWith("Measured") && nextCond.RequiredHits > 0)
                    {
                        finalTerminal = nextCond;
                        isMeasuredContext = true;
                        // We mark this extra condition as consumed in the context, but we don't include it in 'chains' list logic
                        // We adjust 'conditionsConsumed' calculation at the end.
                        // Actually, better to treat it as part of the last chain's terminal logic display
                    }
                }

                if (!isMeasuredContext && finalTerminal != null && finalTerminal.Flag.StartsWith("Measured") && finalTerminal.Flag != "MeasuredIf")
                {
                    isMeasuredContext = true;
                }

                if (isMeasuredContext && finalTerminal != null)
                {
                    bool isHitChain = chains.Any(ch => ch.Conditions.Any(c => c.Flag.Contains("Hits"))) || finalTerminal.Flag.Contains("Hits");
                    string label = isHitChain ? "hits" : "value";
                    string valText = (finalTerminal.RequiredHits > 0) ? finalTerminal.RequiredHits.ToString() : ctx.FormatValue(finalTerminal.RightOperand, conditions.IndexOf(finalTerminal), finalTerminal.LeftOperand.Value, finalTerminal.LeftOperand.Size);

                    if (hasRecallContext)
                        measuredHeader = $"Start measuring {recallContext} up to {valText}";
                    else
                        measuredHeader = $"Start measuring {label} up to {valText}";

                    result.Category = ExplanationCategory.Complex;
                }

                var sb = new StringBuilder();
                bool isTriggerStart = !isMeasuredContext;

                int i = 0;
                while (i < chains.Count)
                {
                    var chainTemplate = chains[i];
                    bool chainHasHits = chainTemplate.Conditions.Any(c => c.Flag.Contains("Hits"));
                    bool ignoreTerminalForGrouping = isMeasuredContext && chainHasHits;

                    // Array detection
                    int rangeEnd = i;
                    while (rangeEnd + 1 < chains.Count)
                    {
                        if (AreChainsStructureEqual(chains[i], chains[rangeEnd + 1], true, ignoreTerminalForGrouping))
                            rangeEnd++;
                        else
                            break;
                    }

                    int count = (rangeEnd - i) + 1;

                    if (count > 3)
                    {
                        var lastChain = chains[rangeEnd];
                        var terminal = lastChain.Terminal;
                        string noun = hasRecallContext ? recallContext : "logic groups";

                        bool showVal = true;
                        if (isMeasuredContext) showVal = false;
                        else if (chainHasHits && (terminal == null || terminal.RequiredHits == 0)) showVal = false;

                        string valText = "(varies)";
                        if (terminal != null)
                            valText = (terminal.RequiredHits > 0) ? terminal.RequiredHits.ToString() : ctx.FormatValue(terminal.RightOperand, conditions.IndexOf(terminal), terminal.LeftOperand.Value, terminal.LeftOperand.Size);

                        string prefix = "";
                        if (isTriggerStart)
                        {
                            if (terminal != null && terminal.Flag == "Pause If") prefix = "Pause when ";
                            else if (terminal != null && terminal.Flag == "Reset If") prefix = "Reset when ";
                            else prefix = "Trigger when ";
                            isTriggerStart = false;
                        }

                        // Generate details for the array (using start index i)
                        var sbDetails = new StringBuilder();
                        for (int k = i; k <= rangeEnd; k++)
                        {
                            var c = chains[k];
                            string instanceVal = (c.Terminal?.RequiredHits > 0) ? c.Terminal.RequiredHits.ToString() : ctx.FormatValue(c.Terminal?.RightOperand ?? new Operand(), 0);
                            var firstAdd = c.Conditions.FirstOrDefault(cond => cond.Flag.Contains("Add") || cond.Flag.Contains("Sub"));
                            string name = firstAdd != null ? ctx.GetName(firstAdd.LeftOperand, 0) : $"Item {k - i + 1}";
                            sbDetails.AppendLine($"- {name} ... {instanceVal}");
                        }

                        string id = Guid.NewGuid().ToString("N");
                        result.DetailedDrillDowns[id] = sbDetails.ToString();
                        string expandToken = $"[[EXPAND:{id}:{count} groups]]";

                        string summary = showVal
                            ? $"{prefix}any of the following {count} {noun} match (at least {valText}): {expandToken}"
                            : $"{prefix}any of the following {count} {noun} match: {expandToken}";

                        sb.Append(summary);

                        if (rangeEnd < chains.Count - 1)
                        {
                            string conn = terminal?.Flag == "OrNext" ? " OR " : " AND ";
                            sb.Append(conn);
                        }

                        i = rangeEnd + 1;
                    }
                    else
                    {
                        // Single Chain Processing
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
                                // If inside MeasuredContext, we just describe the accumulation, not "Start measuring"
                                string valText = (term.RequiredHits > 0) ? term.RequiredHits.ToString() : ctx.FormatValue(term.RightOperand, conditions.IndexOf(term), term.LeftOperand.Value, term.LeftOperand.Size);
                                string opText = ctx.GetOperatorText(term.Operator);

                                if (isMeasuredContext)
                                {
                                    chainText = $"sum of {displayBlocks.Count} {noun} {expandToken} {opText} {valText}";
                                }
                                else
                                {
                                    bool isHitChain = chain.Conditions.Any(c => c.Flag.Contains("Hits")) || term.Flag.Contains("Hits");
                                    string label = isHitChain ? "hits" : "value";
                                    if (hasRecallContext)
                                        chainText = $"Start measuring {recallContext} up to {valText}: {expandToken}";
                                    else
                                        chainText = $"Start measuring {label} up to {valText}: {expandToken}";
                                    isTriggerStart = false;
                                }
                            }
                            else
                            {
                                // Determine if we show value
                                bool isHitChain = chain.Conditions.Any(c => c.Flag.Contains("Hits"));
                                bool showVal = true;
                                if (isMeasuredContext) showVal = false;
                                else if (isHitChain && term.RequiredHits == 0) showVal = false;

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

                                if (isHitChain)
                                {
                                    if (showVal)
                                    {
                                        if (hasRecallContext)
                                            chainText = $"{prefix}{recallContext} decreasing occurs at least {valText} times: {expandToken}";
                                        else
                                            chainText = $"{prefix}at least {valText} of the following occur: {expandToken}";
                                    }
                                    else
                                    {
                                        chainText = $"{prefix}the following occur: {expandToken}";
                                    }
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
                        i++;
                    }
                }

                if (isMeasuredContext)
                {
                    result.Text = $"{measuredHeader} when {sb}";
                }
                else
                {
                    result.Text = sb.ToString();
                }
            }

            if (chains.Count > 0)
            {
                // We return consumption ONLY for the first chain.
                // The main loop will advance past the first chain.
                // It will then process intermediate rows.
                // When it hits the second chain (which we already included in the text), 
                // the check at the top of Process() will catch it via ProcessedIndices and skip it.
                int firstChainLength = (chains[0].EndIndex - chains[0].StartIndex) + 1;
                result.ConditionsConsumed = firstChainLength;
            }
            else
            {
                result.ConditionsConsumed = 1;
            }

            return result;
        }

        private string BuildCondensedChainString(List<AccumulatorChainDef> chains, ExplanationContext ctx, List<AchievementCondition> conditions)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < chains.Count)
            {
                // Collapse identical terminals logic
                int rangeEnd = i;
                while (rangeEnd + 1 < chains.Count)
                {
                    // Strict match here because we are in Transition mode logic which expects specific values
                    if (AreChainsStructureEqual(chains[i], chains[rangeEnd + 1], false, false))
                        rangeEnd++;
                    else
                        break;
                }

                if (i > 0)
                {
                    var prev = chains[i - 1];
                    string conn = (prev.Terminal != null && prev.Terminal.Flag == "OrNext") ? " OR " : " AND ";
                    sb.Append(conn);
                }

                string termText = FormatTerminalCondition(chains[i].Terminal, ctx, conditions);
                sb.Append(termText);

                if (rangeEnd > i)
                {
                    sb.Append($" (x{rangeEnd - i + 1})");
                    i = rangeEnd + 1;
                }
                else
                {
                    i++;
                }
            }
            return sb.ToString();
        }

        private List<AchievementCondition> ExtractChain(List<AchievementCondition> conditions, int start, out int endPtr)
        {
            var chain = new List<AchievementCondition>();
            int ptr = start;
            var buffer = new List<AchievementCondition>();
            bool hasAccumulator = false;

            while (ptr < conditions.Count)
            {
                var c = conditions[ptr];

                if (c.Flag == "Add Address")
                {
                    buffer.Add(c);
                }
                else if (c.Flag == "AndNext" || c.Flag == "OrNext" || c.Flag == "ResetNextIf")
                {
                    if (hasAccumulator) break;
                    buffer.Add(c);
                }
                else if (c.Flag.Contains("Add") || c.Flag.Contains("Sub"))
                {
                    hasAccumulator = true;
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

        private List<List<AchievementCondition>> DeduplicateBlocks(List<List<AchievementCondition>> blocks, ExplanationContext ctx)
        {
            var unique = new List<List<AchievementCondition>>();
            var seenSignatures = new HashSet<string>();

            foreach (var block in blocks)
            {
                if (block.Count == 0) continue;

                var leaf = block.Last();

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
            chain.Where(c => c.Flag != "Add Address")
                 .Any(c => c.LeftOperand.Type == "Delta" || c.LeftOperand.Type == "Prior");

        private bool AreChainsStructureEqual(AccumulatorChainDef c1, AccumulatorChainDef c2, bool ignoreAddresses, bool ignoreTerminal)
        {
            if (!ignoreTerminal)
            {
                // Check Terminal consistency first
                if ((c1.Terminal == null) != (c2.Terminal == null)) return false;
                if (c1.Terminal != null && c2.Terminal != null)
                {
                    if (c1.Terminal.Flag != c2.Terminal.Flag) return false;
                    if (c1.Terminal.Operator != c2.Terminal.Operator) return false;
                    if (!AreValuesEqual(c1.Terminal.RightOperand.Value, c2.Terminal.RightOperand.Value, false)) return false; // Terminals must usually match value (e.g. 5 vs 6 hits)
                }
            }

            var chain1 = c1.Conditions;
            var chain2 = c2.Conditions;

            if (chain1.Count != chain2.Count) return false;
            for (int i = 0; i < chain1.Count; i++)
            {
                var cond1 = chain1[i]; var cond2 = chain2[i];
                if (cond1.Flag != cond2.Flag) return false;

                if (!AreValuesEqual(cond1.LeftOperand.Value, cond2.LeftOperand.Value, ignoreAddresses)) return false;

                if (cond1.LeftOperand.Type != cond2.LeftOperand.Type && cond1.Flag == "Add Address") return false;

                if (cond1.LeftOperand.Size != cond2.LeftOperand.Size) return false;
                if (cond1.Operator != cond2.Operator) return false;

                bool c1IsMemLike = IsMemLike(cond1.LeftOperand.Type);
                bool c2IsMemLike = IsMemLike(cond2.LeftOperand.Type);
                if (c1IsMemLike != c2IsMemLike) return false;

                bool c1RightIsVal = cond1.RightOperand.Type == "Value" || cond1.RightOperand.Type == "Float" || string.IsNullOrEmpty(cond1.RightOperand.Type);
                bool c2RightIsVal = cond2.RightOperand.Type == "Value" || cond2.RightOperand.Type == "Float" || string.IsNullOrEmpty(cond2.RightOperand.Type);

                if (c1RightIsVal && c2RightIsVal)
                {
                    if (!AreValuesEqual(cond1.RightOperand.Value, cond2.RightOperand.Value, false)) return false; // Right side constants usually must match
                }
                else
                {
                    if (cond1.RightOperand.Type != cond2.RightOperand.Type) return false;
                    if (!AreValuesEqual(cond1.RightOperand.Value, cond2.RightOperand.Value, ignoreAddresses)) return false;
                }
            }
            return true;
        }

        private bool AreValuesEqual(string v1, string v2, bool ignoreAddresses)
        {
            if (v1 == v2) return true;

            // If checking array-compatibility, ignore actual hex values for Memory fields
            if (ignoreAddresses) return true;

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