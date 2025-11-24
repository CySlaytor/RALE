#nullable enable
using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System.Collections.Generic;
using System.Linq;

namespace RALogicEditor.Services
{
    public class LogicValidator : ILogicValidator
    {
        private List<ValidationIssue> ValidateGroup(AchievementConditionGroup group, bool otherGroupsHaveReset, IAssetItem? asset)
        {
            var issues = new List<ValidationIssue>();
            if (group.Conditions.Count == 0) return issues;

            // 1. Check for Bad Chains (Ending with combining flags)
            CheckBadChains(group, issues, asset);

            // 2. Check for PauseLocks (PauseIf with Hits but no Resets)
            CheckPauseLocks(group, issues, otherGroupsHaveReset, asset);

            // 3. Check for Stale AddAddress (AddAddress using Delta/Prior)
            CheckStaleAddAddress(group, issues, asset);

            // 4. Size Mismatches
            CheckSizeMismatches(group, issues, asset);

            return issues;
        }

        // Validates the entire asset (all groups) for cross-group logic like Delta checks
        public List<ValidationIssue> ValidateAsset(List<AchievementConditionGroup> groups, IAssetItem? asset = null)
        {
            var allIssues = new List<ValidationIssue>();
            if (groups == null) return allIssues;

            // Pre-calculate which groups have resets to determine if "Global Reset" exists
            int resetCount = groups.Count(g => g.Conditions.Any(c => c.Flag == "Reset If"));

            foreach (var group in groups)
            {
                bool groupHasReset = group.Conditions.Any(c => c.Flag == "Reset If");

                // If at least one other group has a reset, we consider global reset active for this group
                bool otherGroupsHaveReset = (resetCount - (groupHasReset ? 1 : 0)) > 0;

                allIssues.AddRange(ValidateGroup(group, otherGroupsHaveReset, asset));
            }

            // Cross-group check: Missing Deltas
            CheckMissingDeltas(groups, allIssues, asset);

            return allIssues;
        }

        private void CheckBadChains(AchievementConditionGroup group, List<ValidationIssue> issues, IAssetItem? asset)
        {
            var last = group.Conditions.Last();
            if (RaLogicSyntax.ArithmeticFlags.Contains(last.Flag) && last.Flag != "Remember")
            {
                issues.Add(new ValidationIssue(
                    FeedbackSeverity.Error,
                    "Incomplete Chain",
                    $"Group '{group.GroupName}' ends with a combining flag ({last.Flag}) but has no final condition.",
                    last,
                    "",
                    asset
                ));
            }
        }

        private void CheckPauseLocks(AchievementConditionGroup group, List<ValidationIssue> issues, bool otherGroupsHaveReset, IAssetItem? asset)
        {
            // We check for ResetIf anywhere in the current group
            bool hasLocalResetIf = group.Conditions.Any(c => c.Flag == "Reset If");

            for (int i = 0; i < group.Conditions.Count; i++)
            {
                var cond = group.Conditions[i];

                // PauseLock Definition: PauseIf with Hits > 0
                if (cond.Flag == "Pause If" && cond.RequiredHits > 0)
                {
                    // Rule: PauseLock is safe if:
                    // 1. There is a ResetNextIf immediately preceding the chain leading to this PauseIf
                    // 2. OR There is a ResetIf anywhere in this group (hasLocalResetIf)
                    // 3. OR There is a ResetIf in any other group (otherGroupsHaveReset)

                    // Check for ResetNextIf in preceding chain
                    bool foundRNI = false;
                    int scan = i - 1;
                    while (scan >= 0)
                    {
                        var prev = group.Conditions[scan];
                        if (prev.Flag == "ResetNextIf")
                        {
                            foundRNI = true;
                            break;
                        }
                        // Combining flags continue the chain; Comparison flags terminate it
                        if (!RaLogicSyntax.ArithmeticFlags.Contains(prev.Flag) &&
                            prev.Flag != "AndNext" && prev.Flag != "OrNext")
                        {
                            break;
                        }
                        scan--;
                    }

                    if (!foundRNI && !hasLocalResetIf && !otherGroupsHaveReset)
                    {
                        issues.Add(new ValidationIssue(
                            FeedbackSeverity.Warning,
                            "PauseLock Warning",
                            "PauseLocks (PauseIf with hits) require a reset. No ResetIf found in this group or any other group, and no preceding ResetNextIf.",
                            cond,
                            "https://docs.retroachievements.org/developer-docs/flags/pauseif.html#pauseif-with-hit-counts",
                            asset
                        ));
                    }
                }
            }
        }

        private void CheckStaleAddAddress(AchievementConditionGroup group, List<ValidationIssue> issues, IAssetItem? asset)
        {
            foreach (var cond in group.Conditions)
            {
                if (cond.Flag == "Add Address")
                {
                    if (cond.LeftOperand.Type == "Delta" || cond.LeftOperand.Type == "Prior")
                    {
                        issues.Add(new ValidationIssue(
                            FeedbackSeverity.Info,
                            "Stale AddAddress",
                            "Stale references with AddAddress can be dangerous. Use caution when reading a pointer from the previous frame (AddAddress + Delta).",
                            cond,
                            "https://docs.retroachievements.org/developer-docs/flags/addaddress.html#using-delta-with-chained-pointers",
                            asset
                        ));
                    }
                }
            }
        }

        private void CheckSizeMismatches(AchievementConditionGroup group, List<ValidationIssue> issues, IAssetItem? asset)
        {
            foreach (var cond in group.Conditions)
            {
                if (RaLogicSyntax.MemoryTypes.Contains(cond.LeftOperand.Type) &&
                    RaLogicSyntax.MemoryTypes.Contains(cond.RightOperand.Type))
                {
                    if (!string.IsNullOrEmpty(cond.LeftOperand.Size) &&
                        !string.IsNullOrEmpty(cond.RightOperand.Size) &&
                        cond.LeftOperand.Size != cond.RightOperand.Size)
                    {
                        if (!cond.LeftOperand.Size.StartsWith("Bit") && !cond.RightOperand.Size.StartsWith("Bit"))
                        {
                            issues.Add(new ValidationIssue(
                                FeedbackSeverity.Info,
                                "Size Mismatch",
                                $"Comparison size mismatch: {cond.LeftOperand.Size} vs {cond.RightOperand.Size}.",
                                cond,
                                "",
                                asset
                            ));
                        }
                    }
                }
            }
        }

        private void CheckMissingDeltas(List<AchievementConditionGroup> groups, List<ValidationIssue> issues, IAssetItem? asset)
        {
            if (groups == null || groups.Count == 0) return;

            // Helper: Is this group explicitly disabled (e.g. 0=1)?
            bool IsGroupAlwaysFalse(AchievementConditionGroup g)
            {
                foreach (var cond in g.Conditions)
                {
                    if (cond.LeftOperand.Type == "Value" && cond.RightOperand.Type == "Value")
                    {
                        if (cond.Operator == "=" && cond.LeftOperand.Value != cond.RightOperand.Value) return true;
                    }
                }
                return false;
            }

            // Helper: Does group have a Delta?
            bool GroupHasDelta(AchievementConditionGroup g)
            {
                return g.Conditions.Any(c => c.LeftOperand.Type == "Delta" || c.RightOperand.Type == "Delta");
            }

            bool coreHasDelta = GroupHasDelta(groups[0]);

            // If Core has delta, logic is usually safe.
            if (coreHasDelta) return;

            // If Core is empty or valid but no delta, check Alts.
            if (groups.Count == 1)
            {
                if (IsGroupAlwaysFalse(groups[0])) return;

                issues.Add(new ValidationIssue(
                    FeedbackSeverity.Warning,
                    "Missing Delta",
                    "Achievements must contain a Delta to isolate the specific moment that an achievement should trigger.",
                    null,
                    "https://docs.retroachievements.org/developer-docs/why-delta.html",
                    asset
                ));
                return;
            }

            // Check Alts: ALL active alt groups must have a delta.
            bool allAltsHaveDelta = true;
            for (int i = 1; i < groups.Count; i++)
            {
                if (IsGroupAlwaysFalse(groups[i])) continue;

                if (!GroupHasDelta(groups[i]))
                {
                    allAltsHaveDelta = false;
                    break;
                }
            }

            if (!allAltsHaveDelta)
            {
                issues.Add(new ValidationIssue(
                    FeedbackSeverity.Warning,
                    "Improper Delta",
                    "Appropriate use of Delta requires checking a value change in the Core group, or in ALL Alt groups.",
                    null,
                    "https://docs.retroachievements.org/developer-docs/why-delta.html",
                    asset
                ));
            }
        }
    }
}