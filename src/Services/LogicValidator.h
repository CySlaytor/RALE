#pragma once

#include "ILogicValidator.h"

namespace RALogicEditor {

class LogicValidator : public ILogicValidator
{
public:
    // Validates the entire asset (all groups) for cross-group logic like Delta checks
    QList<ValidationIssue> ValidateAsset(
        const QList<AchievementConditionGroup> &groups,
        std::shared_ptr<IAssetItem> asset,
        const QMap<QString, QList<std::shared_ptr<NoteNode>>> *notesLookup = nullptr,
        const QList<ConditionFormatter::RangeEntry> *rangeCache = nullptr) override;

private:
    QList<ValidationIssue> ValidateGroup(
        const AchievementConditionGroup &group,
        bool otherGroupsHaveReset,
        std::shared_ptr<IAssetItem> asset,
        const QMap<QString, QList<std::shared_ptr<NoteNode>>> *notesLookup,
        const QList<ConditionFormatter::RangeEntry> *rangeCache);

    void CheckBadChains(const AchievementConditionGroup &group, QList<ValidationIssue> &issues, std::shared_ptr<IAssetItem> asset);
    void CheckPauseLocks(const AchievementConditionGroup &group, QList<ValidationIssue> &issues, bool otherGroupsHaveReset, std::shared_ptr<IAssetItem> asset);
    void CheckStaleAddAddress(const AchievementConditionGroup &group, QList<ValidationIssue> &issues, std::shared_ptr<IAssetItem> asset);
    void CheckSizeMismatches(const AchievementConditionGroup &group, QList<ValidationIssue> &issues, std::shared_ptr<IAssetItem> asset);
    void CheckMissingDeltas(const QList<AchievementConditionGroup> &groups, QList<ValidationIssue> &issues, std::shared_ptr<IAssetItem> asset);
    bool IsChainFlag(const QString &flag);

    // Check for undocumented memory addresses (Missing Code Notes)
    void CheckMissingNotes(
        const AchievementConditionGroup &group,
        QList<ValidationIssue> &issues,
        std::shared_ptr<IAssetItem> asset,
        const QMap<QString, QList<std::shared_ptr<NoteNode>>> &notesLookup,
        const QList<ConditionFormatter::RangeEntry> *rangeCache);
};

} // namespace RALogicEditor
