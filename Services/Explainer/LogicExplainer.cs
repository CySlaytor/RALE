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
            new AsciiStringProcessor(),    // NEW: Handles grouped ASCII/Region checks
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

            // 3. Explain Regular Alts (Logic Arrays)
            if (regularAlts.Count > 0)
            {
                sb.AppendLine("## Alternative Requirements (At least one set must be true):");
                int i = 0;
                while (i < regularAlts.Count)
                {
                    // Check if multiple Alts form a repeating pattern (Array)
                    int seqLength = DetectArraySequence(regularAlts, i);
                    if (seqLength > 2)
                    {
                        var firstGroup = regularAlts[i];
                        var lastGroup = regularAlts[i + seqLength - 1];
                        sb.AppendLine($"### {firstGroup.GroupName} - {lastGroup.GroupName} (Array Logic):");
                        sb.AppendLine($"Trigger when any of the following {seqLength} slots match:");

                        string explanation = ExplainGroupNatural(firstGroup, notesLookup, rangeCache, showDecimal, result.DetailedInfo);

                        // Indent the explanation for the expandable details
                        var sbIndent = new StringBuilder();
                        foreach (var line in explanation.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            sbIndent.AppendLine("  " + line);

                        string arrayId = Guid.NewGuid().ToString("N");
                        result.DetailedInfo[arrayId] = sbIndent.ToString().TrimEnd();
                        sb.AppendLine($"[[EXPAND:{arrayId}:Array Details]]");
                        sb.AppendLine();
                        i += seqLength;
                    }
                    else
                    {
                        sb.AppendLine($"### {regularAlts[i].GroupName}:");
                        sb.AppendLine(ExplainGroupNatural(regularAlts[i], notesLookup, rangeCache, showDecimal, result.DetailedInfo));
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
                    // Use ExplainGroupNatural but pass isUxHeader=true to handle formatting differently
                    string explanation = ExplainGroupNatural(group, notesLookup, rangeCache, showDecimal, result.DetailedInfo, isUxHeader: true);
                    sb.AppendLine(explanation);
                    sb.AppendLine();
                }
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

        // Core explanation loop that iterates through conditions and delegates to processors
        private static string ExplainGroupNatural(
            AchievementConditionGroup group,
            Dictionary<string, List<NoteNode>> notes,
            List<(long Start, long End, NoteNode Node)> range,
            bool showDec,
            Dictionary<string, string> details,
            bool isUxHeader = false)
        {
            var ctx = new ExplanationContext(group, notes, range, showDec);
            var results = new List<ProcessorResult>();
            var conditions = group.Conditions;
            int i = 0;

            // Execution Loop
            while (i < conditions.Count)
            {
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
                        sb.AppendLine(" and the following:");
                    }
                    else
                        sb.AppendLine(" once the following logic is satisfied:");
                }
                else if (hasTrigger)
                {
                    sb.AppendLine("A Trigger Indicator is displayed once the following logic is satisfied:");
                }
                else
                {
                    sb.AppendLine("Perform display logic when:");
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
                    var validLines = catResults.Select(r => r.Text).Where(t => !string.IsNullOrEmpty(t)).Select(t => t.TrimEnd('.')).ToList();
                    if (validLines.Count > 0)
                        sb.AppendLine($"- Trigger when {string.Join(" AND ", validLines)}.");
                }
                else
                {
                    foreach (var res in catResults)
                    {
                        // Don't double print the header Measured line if we handled it in UX Header
                        if (isUxHeader && hasMeasured && (res.Text.StartsWith("A Measured Indicator") || res.Text.StartsWith("Start measuring"))) continue;

                        // If isUxHeader is true, MeasuredIf lines are summarized in the header ("once X and Y").
                        // We don't need to list them again as bullet points.
                        if (isUxHeader && cat == ExplanationCategory.MeasuredIf) continue;

                        // Filter empty lines (e.g. from PointerCleanupProcessor)
                        if (string.IsNullOrEmpty(res.Text)) continue;

                        // Filter "Always False" / "Always True" lines in UX logic to avoid confusion,
                        // as they are usually structural (preventing trigger).
                        if (isUxHeader && (res.Text.StartsWith("Always False") || res.Text.StartsWith("Always True")))
                            continue;

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

        // Detects repeating patterns across multiple Alt groups
        private static int DetectArraySequence(List<AchievementConditionGroup> alts, int startIndex)
        {
            if (startIndex >= alts.Count - 1) return 0;
            string baseFingerprint = GetGroupFingerprint(alts[startIndex]);

            int count = 1;
            for (int i = startIndex + 1; i < alts.Count; i++)
            {
                if (GetGroupFingerprint(alts[i]) == baseFingerprint) count++;
                else break;
            }
            return count;
        }

        // Generates a unique string hash for a group structure
        private static string GetGroupFingerprint(AchievementConditionGroup group)
        {
            var sb = new StringBuilder();
            foreach (var c in group.Conditions)
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
    }
}