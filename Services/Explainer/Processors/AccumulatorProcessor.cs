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

                // Strict check for accumulator flags to avoid processing "Add Address" chains as accumulators
                bool hasAccumulatorAction = conditionsInChain.Any(c =>
                    c.Flag == "Add Source" || c.Flag == "Sub Source" ||
                    c.Flag == "Add Hits" || c.Flag == "Sub Hits");

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

                // "Terminal Stealing" Logic.
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
                            currentPtr = chainDef.StartIndex + 1;
                            continue;
                        }
                    }
                }

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
                        bool prevIsDelta = IsChainDelta(prevChain.Conditions);
                        bool currIsDelta = IsChainDelta(chainDef.Conditions);

                        // IMPROVEMENT: Allow grouping delta constraints on strictly same variable (Same Body, Different Terminal)
                        if (prevIsDelta && currIsDelta && AreChainsStructureEqual(chains[referenceChainIndex], chainDef, ignoreAddresses: false, ignoreTerminal: true))
                        {
                            // Grouping allowed
                        }
                        else
                        {
                            // Fallback to standard grouping (Strict Match or Array Match)
                            bool strictMatch = AreChainsStructureEqual(chains[referenceChainIndex], chainDef, false, false);
                            bool arrayMatch = AreChainsStructureEqual(chains[referenceChainIndex], chainDef, true, false); // Ignore Addresses

                            if (!strictMatch && !arrayMatch)
                                break;
                        }
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

                // Collapse repetitive lists in the "from/to" strings
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
                // -- NEW: Measured Context Detection --
                // Check if the final terminal of the entire chain sequence is Measured.
                // If so, we restructure the output to "Start measuring ... when ..."
                // This handles complex arrays where many groups feed into one Measured condition.
                AchievementCondition? finalTerminal = chains.Count > 0 ? chains.Last().Terminal : null;
                bool isMeasuredContext = false;
                string measuredHeader = "";

                if (finalTerminal != null && finalTerminal.Flag.StartsWith("Measured") && finalTerminal.Flag != "MeasuredIf")
                {
                    isMeasuredContext = true;
                    bool isHitChain = chains.Any(ch => ch.Conditions.Any(c => c.Flag.Contains("Hits"))) || finalTerminal.Flag.Contains("Hits");
                    string label = isHitChain ? "hits" : "value";

                    // Use final terminal for the limit value
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

                    // Determine if we should ignore terminals for grouping (e.g. Add Hits feeding Measured)
                    // Renamed from isHitChain to avoid conflict with inner scopes
                    bool chainHasHits = chainTemplate.Conditions.Any(c => c.Flag.Contains("Hits"));
                    bool ignoreTerminalForGrouping = isMeasuredContext && chainHasHits;

                    // Look ahead for structurally identical chains (Array detection)
                    int rangeEnd = i;
                    while (rangeEnd + 1 < chains.Count)
                    {
                        // Pass ignoreTerminal if we are in a measured hit context to group everything together
                        if (AreChainsStructureEqual(chains[i], chains[rangeEnd + 1], true, ignoreTerminalForGrouping))
                            rangeEnd++;
                        else
                            break;
                    }

                    int count = (rangeEnd - i) + 1;

                    if (count > 3) // Threshold for collapsing
                    {
                        // Use the LAST chain in the array to determine the effective terminal
                        var lastChain = chains[rangeEnd];
                        var terminal = lastChain.Terminal;

                        string noun = hasRecallContext ? recallContext : "logic groups";

                        // Determine if we show value (suppress if Add Hits with 0 required hits, or in Measured Context)
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
                            // Generate simple explanation for this chain instance
                            string instanceVal = (c.Terminal?.RequiredHits > 0) ? c.Terminal.RequiredHits.ToString() : ctx.FormatValue(c.Terminal?.RightOperand ?? new Operand(), 0);

                            var firstAdd = c.Conditions.FirstOrDefault(cond => cond.Flag.Contains("Add") || cond.Flag.Contains("Sub"));
                            string name = firstAdd != null ? ctx.GetName(firstAdd.LeftOperand, 0) : $"Item {k - i + 1}";

                            sbDetails.AppendLine($"- {name} ... {instanceVal}");
                        }

                        string id = Guid.NewGuid().ToString("N");
                        result.DetailedDrillDowns[id] = sbDetails.ToString();
                        string expandToken = $"[[EXPAND:{id}:{count} groups]]";

                        string summary;
                        if (showVal)
                            summary = $"{prefix}any of the following {count} {noun} match (at least {valText}): {expandToken}";
                        else
                            summary = $"{prefix}any of the following {count} {noun} match: {expandToken}";

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
                        // Process Single Chain (Standard Logic)
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
                                isTriggerStart = false; // Reset trigger start since MeasuredIf usually standalone
                            }
                            else if (term.Flag.StartsWith("Measured"))
                            {
                                // If inside MeasuredContext, we just describe the accumulation, not "Start measuring"
                                string valText = (term.RequiredHits > 0) ? term.RequiredHits.ToString() : ctx.FormatValue(term.RightOperand, conditions.IndexOf(term), term.LeftOperand.Value, term.LeftOperand.Size);
                                string opText = ctx.GetOperatorText(term.Operator);

                                if (isMeasuredContext)
                                {
                                    // Just the input part
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
                                        // No value limit (just accumulation or connector)
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

                // If we detected a Measured context, prepend the header
                if (isMeasuredContext)
                {
                    // Check if sb already starts with a connector or something clean
                    string body = sb.ToString();
                    // If multiple groups were found, they are joined by AND/OR.
                    // We wrap the whole thing.
                    result.Text = $"{measuredHeader} when {body}";
                }
                else
                {
                    result.Text = sb.ToString();
                }
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