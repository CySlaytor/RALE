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
    public static class LogicExplainer
    {
        private static readonly List<ILogicProcessor> _processors = new List<ILogicProcessor>
        {
            new AccumulatorProcessor(),
            new TransitionProcessor(),
            new ResetProcessor(),
            new PauseProcessor(),
            new MeasuredTriggerProcessor(),
            new RememberProcessor(),
            new StandardConditionProcessor()
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

            // 1. Explain Core
            var core = groups.FirstOrDefault(g => g.GroupName == "Core");
            if (core != null && core.Conditions.Count > 0)
            {
                sb.AppendLine("## Core Requirements (Must be true):");
                sb.AppendLine(ExplainGroupNatural(core, notesLookup, rangeCache, showDecimal, result.DetailedInfo));
                sb.AppendLine();
            }

            // 2. Sort Alts
            var allAlts = groups.Where(g => g.GroupName != "Core").ToList();
            var regularAlts = new List<AchievementConditionGroup>();
            var globalResets = new List<AchievementConditionGroup>();
            var displayLogic = new List<AchievementConditionGroup>();

            foreach (var alt in allAlts)
            {
                if (alt.Conditions.Count > 0)
                {
                    if (alt.Conditions.All(c => c.Flag == "Reset If")) globalResets.Add(alt);
                    else if (IsGroupDisplayOnly(alt)) displayLogic.Add(alt);
                    else regularAlts.Add(alt);
                }
                else regularAlts.Add(alt);
            }

            // 3. Explain Regular Alts (Arrays)
            if (regularAlts.Count > 0)
            {
                sb.AppendLine("## Alternative Requirements (At least one set must be true):");
                int i = 0;
                while (i < regularAlts.Count)
                {
                    int seqLength = DetectArraySequence(regularAlts, i);
                    if (seqLength > 2)
                    {
                        var firstGroup = regularAlts[i];
                        var lastGroup = regularAlts[i + seqLength - 1];
                        sb.AppendLine($"### {firstGroup.GroupName} - {lastGroup.GroupName} (Array Logic):");
                        sb.AppendLine($"Trigger when any of the following {seqLength} slots match:");

                        string explanation = ExplainGroupNatural(firstGroup, notesLookup, rangeCache, showDecimal, result.DetailedInfo);

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

            // 4. Display/UX
            if (displayLogic.Count > 0)
            {
                sb.AppendLine("## Overlay / UX Logic (Does not trigger achievement):");
                foreach (var group in displayLogic)
                {
                    sb.AppendLine($"### {group.GroupName}:");
                    // Use ExplainGroupNatural but it handles sorting automatically via categories
                    string explanation = ExplainGroupNatural(group, notesLookup, rangeCache, showDecimal, result.DetailedInfo, isUxHeader: true);
                    sb.AppendLine(explanation);
                    sb.AppendLine();
                }
            }

            // 5. Global Resets
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
                var cond = conditions[i];
                if (cond.Flag == "Add Address" || cond.Flag == "AndNext" || cond.Flag == "OrNext")
                {
                    // These should have been consumed by AccumulatorProcessor or are floating.
                    // If floating, skip or handle generic. Processor handles them if part of chain.
                    // If we hit them here, it means they are orphans or handled by Standard.
                }

                var proc = _processors.OrderBy(p => p.Priority).First(p => p.CanProcess(conditions, i));
                var res = proc.Process(ctx, conditions, i);

                // Merge details
                foreach (var kvp in res.DetailedDrillDowns) details[kvp.Key] = kvp.Value;

                results.Add(res);
                i += res.ConditionsConsumed;
            }

            // Formatting Output
            var sb = new StringBuilder();

            // Check for UX Headers
            bool hasMeasured = group.Conditions.Any(c => c.Flag.StartsWith("Measured"));
            bool hasTrigger = group.Conditions.Any(c => c.Flag == "Trigger");

            if (isUxHeader)
            {
                if (hasMeasured)
                {
                    // Find the measured result text
                    var measuredRes = results.FirstOrDefault(r => r.Text.StartsWith("A Measured Indicator") || r.Text.StartsWith("Start measuring"));
                    if (measuredRes != null)
                        sb.Append(measuredRes.Text); // Specialized processors already format this
                    else
                        sb.Append("A Measured Indicator will be displayed");

                    var mIfs = results.Where(r => r.Category == ExplanationCategory.MeasuredIf).ToList();
                    if (mIfs.Any())
                    {
                        sb.Append(" once ");
                        sb.Append(string.Join(" AND ", mIfs.Select(r => r.Text.Replace(". (MeasuredIf)", ""))));
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
            // Contexts -> Transitions -> Complex -> Pauses -> Resets

            var cats = new[] {
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
                    sb.AppendLine($"- Trigger when {string.Join(" AND ", catResults.Select(r => r.Text.TrimEnd('.')))}.");
                }
                else
                {
                    foreach (var res in catResults)
                    {
                        // Don't double print the header Measured line if we handled it in UX Header
                        if (isUxHeader && hasMeasured && (res.Text.StartsWith("A Measured Indicator") || res.Text.StartsWith("Start measuring"))) continue;
                        sb.AppendLine($"- {res.Text}");
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }

        // --- Shared Array/Display Detection Logic ---

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

        private static string GetGroupFingerprint(AchievementConditionGroup group)
        {
            var sb = new StringBuilder();
            foreach (var c in group.Conditions)
            {
                sb.Append(c.Flag).Append('|');
                sb.Append(c.LeftOperand.Type).Append('|');
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