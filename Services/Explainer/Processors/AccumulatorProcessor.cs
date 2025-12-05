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
        // This ensures we catch AddAddress lines that are part of an accumulator chain before they are hidden.
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
            // Sometimes an accumulator starts with pointer logic or a gate
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

            // ---------------------------------------------------------
            // STEP 1: Collect all accumulator chains (Grouping Logic)
            // ---------------------------------------------------------
            var chains = new List<AccumulatorChainDef>();
            int currentPtr = index;

            while (currentPtr < conditions.Count)
            {
                // If we are scanning ahead and hit a chain already processed
                if (ctx.ProcessedIndices.Contains(currentPtr)) break;

                // Extract a single contiguous accumulator chain
                var conditionsInChain = ExtractChain(conditions, currentPtr, out int endPtr);

                if (conditionsInChain.Count == 0)
                {
                    // No chain found here. Check if we should stop or scan forward.
                    if (chains.Count > 0)
                    {
                        // Check for barriers: ResetIf, PauseIf, OrNext
                        // If we hit these flags outside of a chain, they usually break the group.
                        var currCond = conditions[currentPtr];
                        if (currCond.Flag == "Reset If" || currCond.Flag == "Pause If" || currCond.Flag == "OrNext")
                        {
                            break;
                        }

                        // If it's a standard condition (empty flag), skip and continue scanning
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

                // Check connectivity with previous chains
                if (chains.Count > 0)
                {
                    var prev = chains.Last();
                    bool prevConnected = prev.Terminal != null && (prev.Terminal.Flag == "OrNext" || prev.Terminal.Flag == "AndNext");

                    // Calculate if we skipped lines
                    bool skippedLines = currentPtr > (prev.EndIndex + 1);

                    // If previous chain demanded a connection but we skipped lines, this grouping is invalid.
                    if (prevConnected && skippedLines)
                    {
                        break;
                    }

                    // FIX: Stop grouping if we transition from a Measured chain to a MeasuredIf chain.
                    // This ensures MeasuredIf is treated as a conditional ("once...") rather than merged into the measurement string.
                    bool lastIsMeasured = prev.Terminal != null && prev.Terminal.Flag.StartsWith("Measured") && prev.Terminal.Flag != "MeasuredIf";
                    bool currentIsMeasuredIf = termCond != null && termCond.Flag == "MeasuredIf";

                    if (lastIsMeasured && currentIsMeasuredIf)
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
                // If a chain ends with a gate (AndNext) but that gate is technically the start of the next line,
                // we move it to the previous chain's terminal.
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

                // Mark indices of this chain (except the first one, which is 'index')
                if (chains.Count > 1)
                {
                    ctx.ProcessedIndices.Add(currentPtr);
                }

                currentPtr = endPtr + 1;
            }

            // Fallback: If no accumulator chains found, process as standard
            if (chains.Count == 0)
            {
                return new StandardConditionProcessor().Process(ctx, conditions, index);
            }

            // ---------------------------------------------------------
            // STEP 2: Recall Context Detection (Remember/Recall)
            // ---------------------------------------------------------
            string recallContext = "";
            bool hasRecallContext = false;

            // Check if the condition immediately preceding this block was a "Remember"
            if (index > 0 && conditions[index - 1].Flag == "Remember")
            {
                var remCond = conditions[index - 1];

                // Verify if any of the collected chains actually USE {recall}
                bool chainUsesRecall = chains.Any(ch => ch.Conditions.Any(c => c.LeftOperand.Type == "Recall" || c.RightOperand.Type == "Recall"));

                if (chainUsesRecall)
                {
                    bool leftIsDelta = remCond.LeftOperand.Type == "Delta";
                    bool rightIsMem = remCond.RightOperand.Type == "Mem";
                    bool sameAddr = remCond.LeftOperand.Value == remCond.RightOperand.Value;

                    // Detect Increasing vs Decreasing
                    if (remCond.Operator == "-" && sameAddr)
                    {
                        string varName = ctx.GetName(remCond.LeftOperand, index - 1, true);
                        if (leftIsDelta && rightIsMem)
                        {
                            recallContext = $"{varName} decreasing";
                            hasRecallContext = true;
                        }
                        else if (remCond.LeftOperand.Type == "Mem" && remCond.RightOperand.Type == "Delta")
                        {
                            recallContext = $"{varName} increasing";
                            hasRecallContext = true;
                        }
                    }

                    if (!hasRecallContext)
                    {
                        recallContext = "stored value";
                    }
                }
            }

            // ---------------------------------------------------------
            // STEP 3: Transition Analysis (Sum of Delta vs Sum of Mem)
            // ---------------------------------------------------------
            var deltaChains = chains.Where(c => IsChainDelta(c.Conditions)).ToList();
            var memChains = chains.Where(c => !IsChainDelta(c.Conditions)).ToList();

            bool isTransition = false;
            // Transition heuristic: We have Delta Chains AND Mem Chains, and they are joined by OrNext (usually)
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
                // Generate Transition Logic Text (Sum A > Sum B)
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
                // ---------------------------------------------------------
                // STEP 4: Standard / Measured Logic
                // ---------------------------------------------------------
                AchievementCondition? finalTerminal = chains.Count > 0 ? chains.Last().Terminal : null;
                bool isMeasuredContext = false;
                string measuredHeader = "";

                // Detect if there is a Measured condition immediately following the last chain
                int lastChainEnd = chains.Last().EndIndex;
                if (lastChainEnd + 1 < conditions.Count)
                {
                    var nextCond = conditions[lastChainEnd + 1];
                    bool chainsUseHits = chains.Any(ch => ch.Conditions.Any(c => c.Flag.Contains("Hits")) || (ch.Terminal != null && ch.Terminal.Flag.Contains("Hits")));

                    if (chainsUseHits && nextCond.Flag.StartsWith("Measured") && nextCond.RequiredHits > 0)
                    {
                        finalTerminal = nextCond;
                        isMeasuredContext = true;
                    }
                }

                if (!isMeasuredContext && finalTerminal != null && finalTerminal.Flag.StartsWith("Measured") && finalTerminal.Flag != "MeasuredIf")
                {
                    isMeasuredContext = true;
                }

                // Build Measured Header if applicable
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

                // Build Chain Description Loop
                var sb = new StringBuilder();
                bool isTriggerStart = !isMeasuredContext;

                int i = 0;
                while (i < chains.Count)
                {
                    var chainTemplate = chains[i];

                    // Check if THIS SPECIFIC chain uses Recall
                    bool currentChainUsesRecall = chainTemplate.Conditions.Any(c => c.LeftOperand.Type == "Recall" || c.RightOperand.Type == "Recall");

                    bool chainHasHits = chainTemplate.Conditions.Any(c => c.Flag.Contains("Hits"));
                    bool ignoreTerminalForGrouping = isMeasuredContext && chainHasHits;

                    // 4a. Array Detection (Group identical sequential chains)
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
                        // ARRAY LOGIC
                        var lastChain = chains[rangeEnd];
                        var terminal = lastChain.Terminal;

                        string noun = (hasRecallContext && currentChainUsesRecall) ? recallContext : "logic groups";

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
                        // SINGLE CHAIN LOGIC
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

                            // FIX: Avoid redundant text. If Measured Header already describes the variable (e.g. "NPCs decreasing"),
                            // use "events" as the noun for the body text instead of repeating "NPCs decreasing".
                            string noun = "items";
                            if (hasRecallContext && currentChainUsesRecall)
                            {
                                noun = isMeasuredContext ? "events" : recallContext;
                            }

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
                                    chainText = hasRecallContext ? $"Start measuring {recallContext} up to {valText}: {expandToken}" : $"Start measuring {label} up to {valText}: {expandToken}";
                                    isTriggerStart = false;
                                }
                            }
                            else
                            {
                                // Standard Logic (Hit Count or Value Sum)
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
                                            chainText = $"{prefix}{recallContext} occurs at least {valText} times: {expandToken}";
                                        else
                                            chainText = $"{prefix}at least {valText} hits of the following occur: {expandToken}";
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
                            string noun = (hasRecallContext && currentChainUsesRecall) ? recallContext : "Logic sequence";
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

            // Return consumption count for just the first chain to allow main loop to handle skipping
            result.ConditionsConsumed = chains.Count > 0 ? (chains[0].EndIndex - chains[0].StartIndex) + 1 : 1;
            return result;
        }

        private string BuildCondensedChainString(List<AccumulatorChainDef> chains, ExplanationContext ctx, List<AchievementCondition> conditions)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < chains.Count)
            {
                int rangeEnd = i;
                while (rangeEnd + 1 < chains.Count)
                {
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
                    // If we encounter OrNext inside an accumulator chain, it usually splits the logic.
                    if (hasAccumulator && c.Flag == "OrNext")
                    {
                        break;
                    }
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
                    // Terminals (Measured, ResetIf, etc) stop the extraction
                    break;
                }
                ptr++;
            }

            if (!hasAccumulator && chain.Count == 0)
            {
                endPtr = start;
                return new List<AchievementCondition>();
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
                string name = ctx.GetName(leaf.LeftOperand, index, false);
                string signature = $"{name} {leaf.Operator} {leaf.RightOperand.Value}";

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
                if ((c1.Terminal == null) != (c2.Terminal == null)) return false;
                if (c1.Terminal != null && c2.Terminal != null)
                {
                    if (c1.Terminal.Flag != c2.Terminal.Flag) return false;
                    if (c1.Terminal.Operator != c2.Terminal.Operator) return false;
                    if (!AreValuesEqual(c1.Terminal.RightOperand.Value, c2.Terminal.RightOperand.Value, false)) return false;
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
                    if (!AreValuesEqual(cond1.RightOperand.Value, cond2.RightOperand.Value, false)) return false;
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