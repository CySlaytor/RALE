using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer;
using RALogicEditor.Services.Explainer.Processors;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RALogicEditor.Services
{
    // Service responsible for converting raw logic (Conditions/Groups) into human-readable text explanations.
    // Orchestrates the use of specific ILogicProcessors to handle different logic patterns.
    public static class LogicExplainer
    {
        // Ordered list of processors. Priority matters: Specific logic (Accumulators, Pointers) 
        // must be checked before generic fallback logic.
        private static readonly List<ILogicProcessor> _processors = new List<ILogicProcessor>
        {
            new PointerCleanupProcessor(), // Consumes "Add Address" lines silently to clean up output
            new AsciiStringProcessor(),    // Handles grouped ASCII/Region checks
            new AccumulatorProcessor(),    // Handles complex AddSource/AddHits chains
            new AndNextProcessor(),        // Handles AndNext chains explicitly (e.g. "will operate once")
            new TransitionProcessor(),     // Handles Delta transitions (A > Delta A)
            new ResetProcessor(),          // Handles Reset If
            new PauseProcessor(),          // Handles Pause If / PauseLock
            new MeasuredTriggerProcessor(),// Handles Measured / Trigger (excluding MeasuredIf)
            new RememberProcessor(),       // Handles Remember logic
            new StandardConditionProcessor() // Fallback for standard checks
        };

        public static ExplanationResult ExplainAsset(
            IAssetItem asset,
            List<AchievementConditionGroup> groups,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache,
            bool showDecimal)
        {
            var result = new ExplanationResult();
            var sb = new StringBuilder();

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
                sb.AppendLine(ExplainGroupNatural(core, notesLookup, rangeCache, showDecimal, result.DetailedInfo));
                sb.AppendLine();
            }

            // 2. Process Alt Groups (Sorting into Categories)
            var allAlts = groups.Where(g => g.GroupName != "Core").ToList();
            var regularAlts = new List<AchievementConditionGroup>();
            var globalResets = new List<AchievementConditionGroup>();
            var displayLogic = new List<AchievementConditionGroup>();

            foreach (var alt in allAlts)
            {
                if (alt.Conditions.Count > 0)
                {
                    // Detect Global Resets (Alt groups containing only ResetIfs)
                    if (alt.Conditions.All(c => c.Flag == "Reset If")) globalResets.Add(alt);
                    // Detect Display/UX Logic (Measured/Trigger logic that is effectively impossible to trigger)
                    else if (IsGroupDisplayOnly(alt)) displayLogic.Add(alt);
                    else regularAlts.Add(alt);
                }
                else regularAlts.Add(alt);
            }

            // 3. Explain Regular Alts (Logic Arrays + Multi-Region Merging)
            if (regularAlts.Count > 0)
            {
                sb.AppendLine("## Alternative Requirements (At least one set must be true):");
                ProcessMergedGroups(regularAlts, sb, notesLookup, rangeCache, showDecimal, result.DetailedInfo);
            }

            // 4. Explain Display/UX Logic (Multi-Region Merging applied here too)
            if (displayLogic.Count > 0)
            {
                sb.AppendLine("## Overlay / UX Logic (Does not trigger achievement):");
                ProcessMergedGroups(displayLogic, sb, notesLookup, rangeCache, showDecimal, result.DetailedInfo, isUxHeader: true);
            }

            // 5. Explain Global Resets
            if (globalResets.Count > 0)
            {
                sb.AppendLine("## Global Reset Conditions (Applies to all):");
                var flatResets = globalResets.SelectMany(g => g.Conditions).ToList();
                var dummyCtx = new ExplanationContext(new AchievementConditionGroup(), notesLookup, rangeCache, showDecimal);
                var resetProc = new ResetProcessor();

                for (int k = 0; k < flatResets.Count; k++)
                {
                    // Manually run Reset processor on single lines
                    var res = resetProc.Process(dummyCtx, flatResets, k);
                    sb.AppendLine($"- {res.Text}");
                }
            }

            result.MainText = sb.ToString();
            return result;
        }

        // New Helper to Process Groups by detecting Duplicates/Regions
        private static void ProcessMergedGroups(
            List<AchievementConditionGroup> groups,
            StringBuilder sb,
            Dictionary<string, List<NoteNode>> notes,
            List<(long Start, long End, NoteNode Node)> range,
            bool showDec,
            Dictionary<string, string> details,
            bool isUxHeader = false)
        {
            var processedIndices = new HashSet<int>();

            for (int i = 0; i < groups.Count; i++)
            {
                if (processedIndices.Contains(i)) continue;

                // Check for exact Logic Matches (Multi-Region Detection)
                string currentFingerprint = GetCoreLogicFingerprint(groups[i]);
                var matchingIndices = new List<int> { i };

                for (int j = i + 1; j < groups.Count; j++)
                {
                    if (processedIndices.Contains(j)) continue;
                    if (GetCoreLogicFingerprint(groups[j]) == currentFingerprint)
                    {
                        matchingIndices.Add(j);
                        processedIndices.Add(j);
                    }
                }

                // If we found duplicates, treat them as one logical block
                if (matchingIndices.Count > 1)
                {
                    // Determine if this is an Array (Sequential) or Multi-Region (Split)
                    bool isSequential = true;
                    for (int k = 0; k < matchingIndices.Count - 1; k++)
                    {
                        if (matchingIndices[k + 1] != matchingIndices[k] + 1)
                        {
                            isSequential = false;
                            break;
                        }
                    }

                    if (isSequential && matchingIndices.Count > 2)
                    {
                        // Array Logic
                        var firstGroup = groups[matchingIndices[0]];
                        var lastGroup = groups[matchingIndices.Last()];
                        sb.AppendLine($"### {firstGroup.GroupName} - {lastGroup.GroupName} (Array Logic):");
                        sb.AppendLine($"Trigger when any of the following {matchingIndices.Count} slots match:");

                        string explanation = ExplainGroupNatural(firstGroup, notes, range, showDec, details, isUxHeader);

                        // Indent details
                        var sbIndent = new StringBuilder();
                        foreach (var line in explanation.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            sbIndent.AppendLine("  " + line);

                        string arrayId = Guid.NewGuid().ToString("N");
                        details[arrayId] = sbIndent.ToString().TrimEnd();
                        sb.AppendLine($"[[EXPAND:{arrayId}:Array Details]]");
                        sb.AppendLine();
                    }
                    else
                    {
                        // Multi-Region Logic with Collapsing for Large Lists
                        var names = matchingIndices.Select(idx => groups[idx].GroupName).ToList();
                        string headerNames = "";

                        // Threshold for collapsing: If more than 5 groups, use "X Groups" summary with expander
                        if (names.Count > 5)
                        {
                            string shortList = string.Join(", ", names.Take(3));
                            string hiddenList = string.Join(", ", names.Skip(3));
                            string listId = Guid.NewGuid().ToString("N");

                            // Store the full list in details for expansion
                            details[listId] = hiddenList;

                            // Format: Alt 1, Alt 2, Alt 3... [[EXPAND:ID:and 50 more]] (Multi-Region):
                            headerNames = $"{shortList}... [[EXPAND:{listId}:and {names.Count - 3} more]]";
                        }
                        else
                        {
                            headerNames = string.Join(", ", names);
                        }

                        sb.AppendLine($"### {headerNames} (Multi-Region):");

                        // Explain the logic of the first group
                        string explanation = ExplainGroupNatural(groups[i], notes, range, showDec, details, isUxHeader, filterRegionLocks: true);
                        sb.AppendLine(explanation);
                        sb.AppendLine("- (Region specific locks apply)");
                        sb.AppendLine();
                    }
                }
                else
                {
                    // Single Unique Group
                    sb.AppendLine($"### {groups[i].GroupName}:");
                    string explanation = ExplainGroupNatural(groups[i], notes, range, showDec, details, isUxHeader);
                    sb.AppendLine(explanation);
                    sb.AppendLine();
                }
            }
        }

        // Core explanation loop that iterates through conditions and delegates to processors
        private static string ExplainGroupNatural(
            AchievementConditionGroup group,
            Dictionary<string, List<NoteNode>> notes,
            List<(long Start, long End, NoteNode Node)> range,
            bool showDec,
            Dictionary<string, string> details,
            bool isUxHeader = false,
            bool filterRegionLocks = false)
        {
            var ctx = new ExplanationContext(group, notes, range, showDec);
            var results = new List<ProcessorResult>();
            var conditions = group.Conditions;
            int i = 0;

            // Execution Loop
            while (i < conditions.Count)
            {
                // If filtering region locks, skip Pause If at the start of the group
                if (filterRegionLocks && conditions[i].Flag == "Pause If")
                {
                    i++;
                    continue;
                }

                // Find the correct processor for the current condition
                var proc = _processors.OrderBy(p => p.Priority).First(p => p.CanProcess(conditions, i));
                var res = proc.Process(ctx, conditions, i);

                // Merge details from processor (e.g. array expansions)
                foreach (var kvp in res.DetailedDrillDowns) details[kvp.Key] = kvp.Value;

                results.Add(res);
                i += res.ConditionsConsumed;
            }

            // Formatting Output
            var sb = new StringBuilder();

            // Check for UX Headers (Measured/Trigger)
            bool hasMeasured = group.Conditions.Any(c => c.Flag.StartsWith("Measured"));
            bool hasTrigger = group.Conditions.Any(c => c.Flag == "Trigger");

            // Filter logic to detect if there are leftover items to print
            bool IsRemnant(ProcessorResult r)
            {
                if (isUxHeader && hasMeasured && (r.Text.StartsWith("A Measured Indicator") || r.Text.StartsWith("Start measuring"))) return false;
                if (isUxHeader && r.Category == ExplanationCategory.MeasuredIf) return false;
                if (string.IsNullOrEmpty(r.Text)) return false;
                if (isUxHeader && (r.Text.StartsWith("Always False") || r.Text.StartsWith("Always True"))) return false;
                return true;
            }

            bool hasRemnants = results.Any(IsRemnant);

            if (isUxHeader)
            {
                if (hasMeasured)
                {
                    // Try to find the specific measured result text generated by processors
                    var measuredRes = results.FirstOrDefault(r => r.Text.StartsWith("A Measured Indicator") || r.Text.StartsWith("Start measuring"));
                    if (measuredRes != null)
                        sb.Append(measuredRes.Text);
                    else
                        sb.Append("A Measured Indicator will be displayed");

                    // Summarize MeasuredIf conditions in the header line
                    var mIfs = results.Where(r => r.Category == ExplanationCategory.MeasuredIf).ToList();
                    if (mIfs.Any())
                    {
                        sb.Append(" once ");
                        // Clean the verbose "The Measured condition will work if..." prefix for the header summary
                        var cleanedIfs = mIfs.Select(r =>
                            r.Text.Replace("The Measured condition will work if ", "")
                                  .Replace(" (MeasuredIf)", "")
                                  .Replace(". (MeasuredIf)", "")
                        );
                        sb.Append(string.Join(" AND ", cleanedIfs));

                        if (hasRemnants) sb.AppendLine(" and the following:");
                        else sb.AppendLine(".");
                    }
                    else
                    {
                        if (hasRemnants) sb.AppendLine(" once the following logic is satisfied:");
                        else sb.AppendLine(".");
                    }
                }
                else if (hasTrigger)
                {
                    if (hasRemnants) sb.AppendLine("A Trigger Indicator is displayed once the following logic is satisfied:");
                    else sb.AppendLine("A Trigger Indicator is displayed.");
                }
                else
                {
                    if (hasRemnants) sb.AppendLine("Perform display logic when:");
                    else sb.AppendLine("Display logic active.");
                }
            }

            // Sort/Group lines by category for readability
            // MeasuredIf -> Contexts -> Transitions -> Complex -> Pauses -> Resets

            var cats = new[] {
                ExplanationCategory.MeasuredIf,
                ExplanationCategory.Context,
                ExplanationCategory.Transition,
                ExplanationCategory.Complex,
                ExplanationCategory.Pause,
                ExplanationCategory.Reset
            };

            foreach (var cat in cats)
            {
                var catResults = results.Where(r => r.Category == cat).ToList();
                if (cat == ExplanationCategory.Transition && catResults.Any())
                {
                    // Combine transitions into a single "Trigger when..." sentence if possible
                    var validLines = catResults.Where(IsRemnant).Select(r => r.Text).Where(t => !string.IsNullOrEmpty(t)).Select(t => t.TrimEnd('.')).ToList();
                    if (validLines.Count > 0)
                        sb.AppendLine($"- Trigger when {string.Join(" AND ", validLines)}.");
                }
                else
                {
                    foreach (var res in catResults)
                    {
                        // Don't double print items we filtered out
                        if (!IsRemnant(res)) continue;

                        sb.AppendLine($"- {res.Text}");
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }

        // --- Shared Array/Display Detection Logic ---

        // Heuristic to determine if a group is solely for displaying an overlay (e.g. impossible logic + measured)
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
            return impossibleCount > validAccumulatorTerminators;
        }

        // Detects conditions like 0=1 or 1=0
        private static bool IsConditionAlwaysFalse(AchievementCondition c)
        {
            if (c.LeftOperand.Type != "Value" || c.RightOperand.Type != "Value") return false;
            if (!long.TryParse(c.LeftOperand.Value.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long left)) return false;
            if (!long.TryParse(c.RightOperand.Value.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long right)) return false;

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

        // Generates a unique string hash for a group structure IGNORING specific addresses and Region Locks
        // This allows matching logic structure (Type, Operator, Hits) even if addresses differ (PAL vs USA)
        private static string GetCoreLogicFingerprint(AchievementConditionGroup group)
        {
            var sb = new StringBuilder();

            // NEW LOGIC: We skip leading "Pause If" conditions entirely.
            // These are assumed to be Region Locks.
            // We start fingerprinting from the first non-PauseIf condition.
            // (Or PauseIf inside the logic flow, which we can't easily distinguish, 
            // but typically locks are at the very top).

            int startIndex = 0;
            while (startIndex < group.Conditions.Count && group.Conditions[startIndex].Flag == "Pause If")
            {
                startIndex++;
            }

            for (int i = startIndex; i < group.Conditions.Count; i++)
            {
                var c = group.Conditions[i];

                sb.Append(c.Flag).Append('|');

                // Left Operand
                sb.Append(c.LeftOperand.Type).Append('|');
                sb.Append(c.LeftOperand.Size).Append('|');
                // Include Value ONLY if not Memory (ignore addresses to match PAL/USA)
                if (!IsMem(c.LeftOperand.Type)) sb.Append(c.LeftOperand.Value).Append('|');
                else sb.Append("MEM|"); // Placeholder for any address

                // Operator
                sb.Append(c.Operator).Append('|');

                // Right Operand
                sb.Append(c.RightOperand.Type).Append('|');
                sb.Append(c.RightOperand.Size).Append('|');
                if (!IsMem(c.RightOperand.Type)) sb.Append(c.RightOperand.Value).Append('|');
                else sb.Append("MEM|");

                sb.Append(c.RequiredHits).Append(';');
            }
            return sb.ToString();
        }

        private static bool IsMem(string type)
        {
            return type == "Mem" || type == "Delta" || type == "Prior" || type == "BCD" || type == "Inverted";
        }
    }
}