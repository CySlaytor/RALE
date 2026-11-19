#include "LogicValidator.h"
#include "../Definitions/RaLogicSyntax.h"
#include "../Utilities/LogicFormatter.h"
#include <QRegularExpression>

namespace RALE {

QList<ValidationIssue> LogicValidator::ValidateGroup(
    const AchievementConditionGroup &group,
    bool otherGroupsHaveReset,
    std::shared_ptr<IAssetItem> asset,
    const QMap<QString, QList<std::shared_ptr<NoteNode>>> *notesLookup,
    const QList<ConditionFormatter::RangeEntry> *rangeCache)
{
    QList<ValidationIssue> issues;
    if (group.Conditions.isEmpty()) return issues;

    // 1. Check for Bad Chains (Ending with combining flags)
    CheckBadChains(group, issues, asset);

    // 2. Check for PauseLocks (PauseIf with Hits but no Resets)
    CheckPauseLocks(group, issues, otherGroupsHaveReset, asset);

    // 3. Check for Stale AddAddress (AddAddress using Delta/Prior)
    CheckStaleAddAddress(group, issues, asset);

    // 4. Size Mismatches
    CheckSizeMismatches(group, issues, asset);

    // 5. Check for Missing Code Notes (Documentation)
    if (notesLookup != nullptr)
    {
        CheckMissingNotes(group, issues, asset, *notesLookup, rangeCache);
    }

    return issues;
}

// Validates the entire asset (all groups) for cross-group logic like Delta checks
QList<ValidationIssue> LogicValidator::ValidateAsset(
    const QList<AchievementConditionGroup> &groups,
    std::shared_ptr<IAssetItem> asset,
    const QMap<QString, QList<std::shared_ptr<NoteNode>>> *notesLookup,
    const QList<ConditionFormatter::RangeEntry> *rangeCache)
{
    QList<ValidationIssue> allIssues;

    // Check for Database Limit
    if (asset != nullptr && asset->Logic.length() > 65535)
    {
        allIssues.append(ValidationIssue(
            FeedbackSeverity::Warning,
            "Database Limit Exceeded",
            QString("Logic length (%1) exceeds the standard database limit of 65535 characters. This asset may fail to save or function correctly on the server.")
                .arg(asset->Logic.length()),
            std::nullopt,
            "",
            asset
        ));
    }

    if (groups.isEmpty()) return allIssues;

    // Pre-calculate which groups have resets to determine if "Global Reset" exists
    int resetCount = 0;
    for (const auto &g : groups)
    {
        bool hasReset = false;
        for (const auto &c : g.Conditions)
        {
            if (c.Flag == "Reset If") { hasReset = true; break; }
        }
        if (hasReset) resetCount++;
    }

    for (const auto &group : groups)
    {
        bool groupHasReset = false;
        for (const auto &c : group.Conditions)
        {
            if (c.Flag == "Reset If") { groupHasReset = true; break; }
        }

        // If at least one other group has a reset, we consider global reset active for this group
        bool otherGroupsHaveReset = (resetCount - (groupHasReset ? 1 : 0)) > 0;

        allIssues.append(ValidateGroup(group, otherGroupsHaveReset, asset, notesLookup, rangeCache));
    }

    // Cross-group check: Missing Deltas
    CheckMissingDeltas(groups, allIssues, asset);

    return allIssues;
}

void LogicValidator::CheckBadChains(const AchievementConditionGroup &group, QList<ValidationIssue> &issues, std::shared_ptr<IAssetItem> asset)
{
    const auto &last = group.Conditions.last();
    if (RaLogicSyntax::ArithmeticFlags.contains(last.Flag) && last.Flag != "Remember")
    {
        issues.append(ValidationIssue(
            FeedbackSeverity::Error,
            "Incomplete Chain",
            QString("Group '%1' ends with a combining flag (%2) but has no final condition.")
                .arg(group.GroupName, last.Flag),
            last,
            "",
            asset
        ));
    }
}

void LogicValidator::CheckPauseLocks(const AchievementConditionGroup &group, QList<ValidationIssue> &issues, bool otherGroupsHaveReset, std::shared_ptr<IAssetItem> asset)
{
    // We check for ResetIf anywhere in the current group
    bool hasLocalResetIf = false;
    for (const auto &c : group.Conditions)
    {
        if (c.Flag == "Reset If") { hasLocalResetIf = true; break; }
    }

    for (int i = 0; i < group.Conditions.size(); i++)
    {
        const auto &cond = group.Conditions[i];

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
                const auto &prev = group.Conditions[scan];
                if (prev.Flag == "ResetNextIf")
                {
                    foundRNI = true;
                    break;
                }
                // Combining flags continue the chain; Comparison flags terminate it
                if (!RaLogicSyntax::ArithmeticFlags.contains(prev.Flag) &&
                    prev.Flag != "AndNext" && prev.Flag != "OrNext")
                {
                    break;
                }
                scan--;
            }

            if (!foundRNI && !hasLocalResetIf && !otherGroupsHaveReset)
            {
                issues.append(ValidationIssue(
                    FeedbackSeverity::Warning,
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

void LogicValidator::CheckStaleAddAddress(const AchievementConditionGroup &group, QList<ValidationIssue> &issues, std::shared_ptr<IAssetItem> asset)
{
    for (const auto &cond : group.Conditions)
    {
        if (cond.Flag == "Add Address")
        {
            if (cond.LeftOperand.Type == "Delta" || cond.LeftOperand.Type == "Prior")
            {
                issues.append(ValidationIssue(
                    FeedbackSeverity::Info,
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

void LogicValidator::CheckSizeMismatches(const AchievementConditionGroup &group, QList<ValidationIssue> &issues, std::shared_ptr<IAssetItem> asset)
{
    for (const auto &cond : group.Conditions)
    {
        if (RaLogicSyntax::MemoryTypes.contains(cond.LeftOperand.Type) &&
            RaLogicSyntax::MemoryTypes.contains(cond.RightOperand.Type))
        {
            if (!cond.LeftOperand.Size.isEmpty() &&
                !cond.RightOperand.Size.isEmpty() &&
                cond.LeftOperand.Size != cond.RightOperand.Size)
            {
                if (!cond.LeftOperand.Size.startsWith("Bit") && !cond.RightOperand.Size.startsWith("Bit"))
                {
                    issues.append(ValidationIssue(
                        FeedbackSeverity::Info,
                        "Size Mismatch",
                        QString("Comparison size mismatch: %1 vs %2.").arg(cond.LeftOperand.Size, cond.RightOperand.Size),
                        cond,
                        "",
                        asset
                    ));
                }
            }
        }
    }
}

void LogicValidator::CheckMissingDeltas(const QList<AchievementConditionGroup> &groups, QList<ValidationIssue> &issues, std::shared_ptr<IAssetItem> asset)
{
    if (groups.isEmpty()) return;

    // Helper: Is this group explicitly disabled (e.g. 0=1)?
    auto IsGroupAlwaysFalse = [](const AchievementConditionGroup &g) -> bool {
        for (const auto &cond : g.Conditions)
        {
            if (cond.LeftOperand.Type == "Value" && cond.RightOperand.Type == "Value")
            {
                if (cond.Operator == "=" && cond.LeftOperand.Value != cond.RightOperand.Value) return true;
            }
        }
        return false;
    };

    // Helper: Is this group PURELY for resetting?
    // A group is a Reset Group if every condition chain terminates in a ResetIf or PauseIf.
    // If it contains ANY condition that can trigger the achievement (Empty Flag or Trigger), it is NOT a Reset Group.
    // Exception: If the triggering condition is part of a chain leading to ResetIf (e.g. AddSource... ResetIf).
    auto IsResetGroup = [this](const AchievementConditionGroup &g) -> bool {
        // Iterate backwards to identify chain terminators
        for (int i = g.Conditions.size() - 1; i >= 0; i--)
        {
            const auto &cond = g.Conditions[i];

            // Skip non-terminating flags (AddSource, AddHits, etc.)
            // These are just parts of a chain. We care about what the chain DOES.
            if (IsChainFlag(cond.Flag)) continue;

            // If we find a terminator, check its type.
            // If it is NOT ResetIf or PauseIf, then this group is trying to do something other than reset/pause.
            if (cond.Flag != "Reset If" && cond.Flag != "Pause If")
            {
                return false; // Found a trigger condition!
            }
        }
        return true; // All chains ended in ResetIf or PauseIf
    };

    // Helper: Does group have a Delta?
    auto GroupHasDelta = [](const AchievementConditionGroup &g) -> bool {
        for (const auto &c : g.Conditions)
        {
            if (c.LeftOperand.Type == "Delta" || c.RightOperand.Type == "Delta") return true;
        }
        return false;
    };

    bool coreHasDelta = GroupHasDelta(groups[0]);

    // If Core has delta, logic is usually safe.
    if (coreHasDelta) return;

    // If Core is empty or valid but no delta, check Alts.
    if (groups.size() == 1)
    {
        if (IsGroupAlwaysFalse(groups[0])) return;

        issues.append(ValidationIssue(
            FeedbackSeverity::Warning,
            "Missing Delta",
            "Achievements must contain a Delta to isolate the specific moment that an achievement should trigger.",
            std::nullopt,
            "https://docs.retroachievements.org/developer-docs/why-delta.html",
            asset
        ));
        return;
    }

    // Check Alts: ALL active alt groups must have a delta.
    bool allAltsHaveDelta = true;
    for (int i = 1; i < groups.size(); i++)
    {
        const auto &grp = groups[i];
        if (IsGroupAlwaysFalse(grp)) continue;
        if (IsResetGroup(grp)) continue; // Skip Reset/Pause groups

        if (!GroupHasDelta(grp))
        {
            allAltsHaveDelta = false;
            break;
        }
    }

    if (!allAltsHaveDelta)
    {
        issues.append(ValidationIssue(
            FeedbackSeverity::Warning,
            "Improper Delta",
            "Appropriate use of Delta requires checking a value change in the Core group, or in ALL Alt groups.",
            std::nullopt,
            "https://docs.retroachievements.org/developer-docs/why-delta.html",
            asset
        ));
    }
}

bool LogicValidator::IsChainFlag(const QString &flag)
{
    return flag == "Add Source" || flag == "Sub Source" ||
           flag == "Add Hits" || flag == "Sub Hits" ||
           flag == "Add Address" ||
           flag == "AndNext" || flag == "OrNext" || flag == "ResetNextIf";
}

// Check for undocumented memory addresses (Missing Code Notes)
void LogicValidator::CheckMissingNotes(
    const AchievementConditionGroup &group,
    QList<ValidationIssue> &issues,
    std::shared_ptr<IAssetItem> asset,
    const QMap<QString, QList<std::shared_ptr<NoteNode>>> &notesLookup,
    const QList<ConditionFormatter::RangeEntry> *rangeCache)
{
    auto ParseHex = [](const QString &s) -> int64_t {
        if (s.trimmed().isEmpty()) return -1;
        QString clean = s.trimmed();
        clean.remove("0x", Qt::CaseInsensitive);
        static const QRegularExpression hexFilterRegex("[^0-9A-Fa-f]");
        clean.remove(hexFilterRegex);
        bool ok = false;
        int64_t v = clean.toLongLong(&ok, 16);
        return ok ? v : -1;
    };

    for (int i = 0; i < group.Conditions.size(); i++)
    {
        const auto &cond = group.Conditions[i];

        // IMPORTANT: "check the leaf only, the last offset of an AddAddress chain"
        // If this is an AddAddress line, we skip validation. It is an intermediate step.
        // We only validate lines that CONSUME the pointer (Mem, AddSource, SubSource, etc.)
        if (cond.Flag == "Add Address") continue;

        // Helper to check a single operand
        auto CheckOperand = [&](const Operand &op) {
            if (!RaLogicSyntax::MemoryTypes.contains(op.Type)) return;

            // Resolve the alias for this address/pointer-chain
            QString address = LogicFormatter::NormalizeAddress(op.Value);

            // ResolveAlias handles the pointer chain math (looking backwards at AddAddress lines)
            // and returns the note name for the final resolved address (Leaf).
            QString alias = ConditionFormatter::ResolveAlias(address, op.Size, &group, i, notesLookup, rangeCache);

            if (alias.trimmed().isEmpty())
            {
                QString msg = QString("Address %1 has no Code Note.").arg(op.Value);

                // Improved Detection: Check if this is a failure in a Pointer Chain where the Base IS documented
                if (i > 0 && group.Conditions[i - 1].Flag == "Add Address")
                {
                    // Backtrack to find the Base Address of the chain
                    QStringList chain;
                    int scan = i - 1;
                    while (scan >= 0 && group.Conditions[scan].Flag == "Add Address")
                    {
                        chain.prepend(group.Conditions[scan].LeftOperand.Value);
                        scan--;
                    }

                    if (!chain.isEmpty())
                    {
                        QString baseStr = chain[0];
                        QString normBase = LogicFormatter::NormalizeAddress(baseStr);

                        QList<std::shared_ptr<NoteNode>> currentNodes;
                        bool hasCurrentNodes = false;
                        QString contextMsg = "";

                        // Try Direct Lookup
                        if (notesLookup.contains(normBase))
                        {
                            currentNodes = notesLookup.value(normBase);
                            hasCurrentNodes = true;
                        }
                        // Try Range Cache Lookup
                        else if (rangeCache != nullptr)
                        {
                            int64_t baseVal = ParseHex(baseStr);
                            if (baseVal != -1)
                            {
                                for (const auto &match : *rangeCache)
                                {
                                    if (baseVal >= match.Start && baseVal < match.End && match.Node != nullptr)
                                    {
                                        currentNodes = { match.Node };
                                        hasCurrentNodes = true;
                                        contextMsg = " (Base found in Struct Range)";
                                        break;
                                    }
                                }
                            }
                        }

                        if (hasCurrentNodes)
                        {
                            // Base Found. Loop to resolve Redirects (Virtual Links).
                            int redirects = 0;
                            static const QRegularExpression referRegex(R"(refer to \$0x([0-9a-f]+))", QRegularExpression::CaseInsensitiveOption);

                            while (redirects < 5)
                            {
                                if (currentNodes.isEmpty()) break;

                                std::shared_ptr<NoteNode> root = nullptr;
                                for (const auto &n : currentNodes)
                                {
                                    if (n->IndentLevel == -2) { root = n; break; }
                                }
                                if (root == nullptr) root = currentNodes[0];

                                QString content = root->Content;

                                if (content.contains("refer to", Qt::CaseInsensitive))
                                {
                                    auto match = referRegex.match(content);
                                    if (match.hasMatch())
                                    {
                                        QString targetAddr = match.captured(1);
                                        QString normTarget = LogicFormatter::NormalizeAddress(targetAddr);

                                        if (notesLookup.contains(normTarget))
                                        {
                                            currentNodes = notesLookup.value(normTarget);
                                            contextMsg = QString(" (Base Linked to 0x%1, resolved)").arg(targetAddr);
                                            redirects++;
                                            continue;
                                        }
                                        else
                                        {
                                            contextMsg = QString(" (Base Linked to 0x%1 but target missing)").arg(targetAddr);
                                            break;
                                        }
                                    }
                                }
                                break;
                            }

                            // 2. Trace the Chain using the Resolved Nodes
                            QString brokenOffset = "";
                            QList<std::shared_ptr<NoteNode>> levelNodes;
                            for (const auto &n : currentNodes)
                            {
                                if (n->IndentLevel != -2 && (n->Parent == nullptr || n->Parent->IndentLevel == -1))
                                    levelNodes.append(n);
                            }

                            // Iterate intermediate offsets
                            for (int k = 1; k < chain.size(); k++)
                            {
                                int64_t offVal = ParseHex(chain[k]);
                                std::shared_ptr<NoteNode> next = nullptr;
                                for (const auto &n : levelNodes)
                                {
                                    int64_t nOff = ParseHex(n->Offset);
                                    if (n->Offset.contains('-')) nOff = -nOff;

                                    // Exact match OR Range match
                                    if (offVal >= nOff && offVal < nOff + n->Size)
                                    {
                                        next = n;
                                        break;
                                    }
                                }

                                if (next == nullptr)
                                {
                                    brokenOffset = chain[k];
                                    break;
                                }
                                levelNodes = next->Children;
                            }

                            if (!brokenOffset.isEmpty())
                            {
                                msg = QString("Pointer chain broken at intermediate offset +%1.%2 Verify note structure.").arg(brokenOffset, contextMsg);
                            }
                            else
                            {
                                // Chain OK, check Leaf (op.Value)
                                int64_t leafVal = ParseHex(op.Value);

                                bool leafFound = false;
                                for (const auto &n : levelNodes)
                                {
                                    int64_t nOff = ParseHex(n->Offset);
                                    if (n->Offset.contains('-')) nOff = -nOff;

                                    if (leafVal >= nOff && leafVal < nOff + n->Size)
                                    {
                                        leafFound = true;
                                        break;
                                    }
                                }

                                if (!leafFound)
                                {
                                    msg = QString("Pointer leaf (+%1) not found in resolved structure of Base %2.%3").arg(op.Value, baseStr, contextMsg);
                                }
                                else
                                {
                                    msg = QString("ConditionFormatter.ResolveAlias failed but Validator trace succeeded. Base: %1%2").arg(baseStr, contextMsg);
                                }
                            }
                        }
                        else
                        {
                            msg = QString("Pointer Base %1 is undocumented. Cannot resolve chain.").arg(baseStr);
                        }
                    }
                }

                // LogicFormatter.NormalizeAddress ensures we have a clean hex string or value
                // We use the original op.Value for display
                ValidationIssue issue(
                    FeedbackSeverity::Warning,
                    "Undocumented Memory",
                    msg,
                    cond,
                    "",
                    asset
                );

                issues.append(issue);
            }
        };

        CheckOperand(cond.LeftOperand);
        CheckOperand(cond.RightOperand);
    }
}

} // namespace RALE
