#nullable enable
using RALogicEditor.Definitions;
using RALogicEditor.Helpers;
using RALogicEditor.Models;
using RALogicEditor.Utilities;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Services
{
    public class LogicValidator : ILogicValidator
    {
        private List<ValidationIssue> ValidateGroup(
            AchievementConditionGroup group,
            bool otherGroupsHaveReset,
            IAssetItem? asset,
            Dictionary<string, List<NoteNode>>? notesLookup,
            List<(long Start, long End, NoteNode Node)>? rangeCache)
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

            // 5. Check for Missing Code Notes (Documentation)
            if (notesLookup != null)
            {
                CheckMissingNotes(group, issues, asset, notesLookup, rangeCache);
            }

            return issues;
        }

        // Validates the entire asset (all groups) for cross-group logic like Delta checks
        public List<ValidationIssue> ValidateAsset(
            List<AchievementConditionGroup> groups,
            IAssetItem? asset,
            Dictionary<string, List<NoteNode>>? notesLookup = null,
            List<(long Start, long End, NoteNode Node)>? rangeCache = null)
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

                allIssues.AddRange(ValidateGroup(group, otherGroupsHaveReset, asset, notesLookup, rangeCache));
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

        // Check for undocumented memory addresses (Missing Code Notes)
        private void CheckMissingNotes(
            AchievementConditionGroup group,
            List<ValidationIssue> issues,
            IAssetItem? asset,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)>? rangeCache)
        {
            // Internal flag to enable detailed logging. Set to true to debug pointer issues.
            bool enableTrace = false;
            StringBuilder? debugLog = enableTrace ? new StringBuilder() : null;

            long ParseHex(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return -1;
                string clean = s.Trim().Replace("0x", "", System.StringComparison.OrdinalIgnoreCase);
                clean = Regex.Replace(clean, "[^0-9A-Fa-f]", "");
                return long.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long v) ? v : -1;
            }

            for (int i = 0; i < group.Conditions.Count; i++)
            {
                var cond = group.Conditions[i];

                // IMPORTANT: "check the leaf only, the last offset of an AddAddress chain"
                // If this is an AddAddress line, we skip validation. It is an intermediate step.
                // We only validate lines that CONSUME the pointer (Mem, AddSource, SubSource, etc.)
                if (cond.Flag == "Add Address") continue;

                // Helper to check a single operand
                void CheckOperand(Operand op, bool isLeft)
                {
                    if (!RaLogicSyntax.MemoryTypes.Contains(op.Type)) return;

                    // Resolve the alias for this address/pointer-chain
                    string address = LogicFormatter.NormalizeAddress(op.Value);

                    // ResolveAlias handles the pointer chain math (looking backwards at AddAddress lines)
                    // and returns the note name for the final resolved address (Leaf).
                    string alias = ConditionFormatter.ResolveAlias(address, group, i, notesLookup, rangeCache);

                    if (string.IsNullOrWhiteSpace(alias))
                    {
                        debugLog?.AppendLine($"Validating Operand: {op.Value} (Index: {i})");
                        debugLog?.AppendLine("--------------------------------------------------");

                        string msg = $"Address {op.Value} has no Code Note.";

                        // Improved Detection: Check if this is a failure in a Pointer Chain where the Base IS documented
                        if (i > 0 && group.Conditions[i - 1].Flag == "Add Address")
                        {
                            // Backtrack to find the Base Address of the chain
                            var chain = new List<string>();
                            int scan = i - 1;
                            while (scan >= 0 && group.Conditions[scan].Flag == "Add Address")
                            {
                                chain.Insert(0, group.Conditions[scan].LeftOperand.Value);
                                scan--;
                            }

                            if (chain.Count > 0)
                            {
                                debugLog?.AppendLine($"Pointer Chain Detected: {string.Join(" -> ", chain)} -> {op.Value}");
                                string baseStr = chain[0];
                                string normBase = LogicFormatter.NormalizeAddress(baseStr);

                                List<NoteNode>? currentNodes = null;
                                string contextMsg = "";

                                // Try Direct Lookup
                                if (notesLookup.TryGetValue(normBase, out var lookupNodes))
                                {
                                    currentNodes = lookupNodes;
                                    debugLog?.AppendLine($"Base {baseStr} found in NotesLookup.");
                                }
                                // Try Range Cache Lookup
                                else if (rangeCache != null)
                                {
                                    long baseVal = ParseHex(baseStr);
                                    if (baseVal != -1)
                                    {
                                        var match = rangeCache.FirstOrDefault(r => baseVal >= r.Start && baseVal < r.End);
                                        if (match.Node != null)
                                        {
                                            currentNodes = new List<NoteNode> { match.Node };
                                            contextMsg = " (Base found in Struct Range)";
                                            debugLog?.AppendLine($"Base {baseStr} found in RangeCache (Struct).");
                                        }
                                    }
                                }

                                if (currentNodes != null)
                                {
                                    // Base Found. Loop to resolve Redirects (Virtual Links).
                                    int redirects = 0;
                                    while (redirects < 5)
                                    {
                                        if (currentNodes.Count == 0) break;

                                        var root = currentNodes.FirstOrDefault(n => n.IndentLevel == -2) ?? currentNodes[0];
                                        string content = root.Content ?? "";

                                        if (content.Contains("refer to", System.StringComparison.OrdinalIgnoreCase))
                                        {
                                            debugLog?.AppendLine($"Redirect detected: '{root.Description}'\n{content.Trim()}");
                                            var match = Regex.Match(content, @"refer to \$0x([0-9a-f]+)", RegexOptions.IgnoreCase);
                                            if (match.Success)
                                            {
                                                string targetAddr = match.Groups[1].Value;
                                                string normTarget = LogicFormatter.NormalizeAddress(targetAddr);

                                                if (notesLookup.TryGetValue(normTarget, out var targetNodes))
                                                {
                                                    currentNodes = targetNodes;
                                                    contextMsg = $" (Base Linked to 0x{targetAddr}, resolved)";
                                                    debugLog?.AppendLine($"Redirect resolved to {targetAddr}.");
                                                    redirects++;
                                                    continue;
                                                }
                                                else
                                                {
                                                    contextMsg = $" (Base Linked to 0x{targetAddr} but target missing)";
                                                    debugLog?.AppendLine($"Redirect target {targetAddr} MISSING from NotesLookup.");
                                                    break;
                                                }
                                            }
                                        }
                                        break;
                                    }

                                    // 2. Trace the Chain using the Resolved Nodes
                                    string brokenOffset = "";
                                    var levelNodes = currentNodes.Where(n => n.IndentLevel != -2 && (n.Parent == null || n.Parent.IndentLevel == -1)).ToList();

                                    debugLog?.AppendLine($"Tracing offsets starting with {levelNodes.Count} root nodes.");

                                    // Iterate intermediate offsets
                                    for (int k = 1; k < chain.Count; k++)
                                    {
                                        long offVal = ParseHex(chain[k]);
                                        debugLog?.AppendLine($"Looking for offset +{chain[k]} (0x{offVal:X})...");
                                        NoteNode? next = null;
                                        foreach (var n in levelNodes)
                                        {
                                            long nOff = ParseHex(n.Offset);
                                            if (n.Offset.Contains("-")) nOff = -nOff;

                                            // Exact match OR Range match
                                            if (offVal >= nOff && offVal < nOff + n.Size)
                                            {
                                                next = n;
                                                debugLog?.AppendLine($"  Matched Node: {n.Offset} ({n.Description})");
                                                break;
                                            }
                                        }

                                        if (next == null)
                                        {
                                            debugLog?.AppendLine($"  FAILED to find match for +{chain[k]}");
                                            brokenOffset = chain[k];
                                            break;
                                        }
                                        levelNodes = next.Children;
                                        debugLog?.AppendLine($"  Stepped into node. Children count: {levelNodes.Count}");
                                    }

                                    if (!string.IsNullOrEmpty(brokenOffset))
                                    {
                                        msg = $"Pointer chain broken at intermediate offset +{brokenOffset}.{contextMsg} Verify note structure.";
                                    }
                                    else
                                    {
                                        // Chain OK, check Leaf (op.Value)
                                        long leafVal = ParseHex(op.Value);
                                        debugLog?.AppendLine($"Checking Leaf: +{op.Value} (0x{leafVal:X})...");

                                        bool leafFound = false;
                                        foreach (var n in levelNodes)
                                        {
                                            long nOff = ParseHex(n.Offset);
                                            if (n.Offset.Contains("-")) nOff = -nOff;

                                            if (leafVal >= nOff && leafVal < nOff + n.Size)
                                            {
                                                debugLog?.AppendLine($"  Matched Leaf: {n.Offset} ({n.Description})");
                                                leafFound = true;
                                                break;
                                            }
                                        }

                                        if (!leafFound)
                                        {
                                            debugLog?.AppendLine("  FAILED to find leaf node.");
                                            msg = $"Pointer leaf (+{op.Value}) not found in resolved structure of Base {baseStr}.{contextMsg}";
                                        }
                                        else
                                        {
                                            msg = $"ConditionFormatter.ResolveAlias failed but Validator trace succeeded. Base: {baseStr}{contextMsg}";
                                            debugLog?.AppendLine("SUCCESS: Validator found the note. CodeReview logic discrepancy detected.");
                                        }
                                    }
                                }
                                else
                                {
                                    msg = $"Pointer Base {baseStr} is undocumented. Cannot resolve chain.";
                                    debugLog?.AppendLine($"Base {baseStr} NOT found in NotesLookup or RangeCache.");
                                }
                            }
                        }

                        // LogicFormatter.NormalizeAddress ensures we have a clean hex string or value
                        // We use the original op.Value for display
                        var issue = new ValidationIssue(
                            FeedbackSeverity.Warning,
                            "Undocumented Memory",
                            msg,
                            cond,
                            "",
                            asset
                        );

                        if (enableTrace) issue.DebugLog = debugLog?.ToString() ?? "";
                        issues.Add(issue);
                    }
                }

                CheckOperand(cond.LeftOperand, true);
                CheckOperand(cond.RightOperand, false);
            }
        }
    }
}