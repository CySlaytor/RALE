#include "MultiRegionBatchProcessor.h"
#include "AssetStrategies.h"
#include "../Definitions/RaLogicSyntax.h"

namespace RALE {

void MultiRegionBatchProcessor::ApplyMultiRegionToAsset(
    std::shared_ptr<IAssetItem> localItem,
    const RegionConfig *baseConfig,
    const QList<RegionConfig> &targetConfigs)
{
    if (localItem == nullptr) return;

    // Parse Existing Logic
    std::unique_ptr<IAssetStrategy> strategy;
    if (localItem->isLeaderboard()) strategy = std::make_unique<LeaderboardStrategy>();
    else strategy = std::make_unique<AchievementStrategy>();

    auto segments = strategy->ParseLogic(localItem->Logic);
    QStringList keys = segments.keys();

    for (const QString &segmentKey : keys)
    {
        const auto &groups = segments[segmentKey];
        if (groups.isEmpty()) continue;

        // Identify existing groups (Base Groups)
        QList<AchievementConditionGroup> existingGroups;
        // Deep copy existing groups to avoid reference issues
        for (const auto &g : groups)
        {
            AchievementConditionGroup newG;
            newG.GroupName = g.GroupName;
            for (const auto &c : g.Conditions) newG.Conditions.append(c.Clone());
            existingGroups.append(newG);
        }

        QList<AchievementConditionGroup> finalGroups;

        // Handle Base Groups
        if (baseConfig != nullptr && !baseConfig->LockConditions.isEmpty())
        {
            for (auto &grp : existingGroups)
            {
                QList<AchievementCondition> prep;
                for (const auto &c : baseConfig->LockConditions) prep.append(c.Clone());
                for (int i = prep.size() - 1; i >= 0; --i)
                    grp.Conditions.prepend(prep[i]);
            }
        }

        // Rename Base Groups if needed (and Metadata update)
        // When we split Core into Alts, we lose the original Core if we don't save the new names.
        if (!localItem->isLeaderboard() && !existingGroups.isEmpty() && existingGroups[0].GroupName == "Core")
        {
            QString baseName = (baseConfig != nullptr) ? baseConfig->RegionName : QString("Region A");
            existingGroups[0].GroupName = QString("Alt 1 (%1)").arg(baseName);
            for (int i = 1; i < existingGroups.size(); i++)
                existingGroups[i].GroupName = QString("Alt %1 (%2)").arg(i + 1).arg(baseName);

            AchievementConditionGroup emptyCore;
            emptyCore.GroupName = "Core";
            finalGroups.append(emptyCore); // Empty Core
        }

        finalGroups.append(existingGroups);

        // Create Target Region Groups
        for (const auto &targetCfg : targetConfigs)
        {
            // Clone from original logic structure (before we modified existingGroups with locks)
            QList<AchievementConditionGroup> newRegionGroups;
            for (const auto &g : groups) // 'groups' contains the parsed objects from 'localItem.Logic'
            {
                AchievementConditionGroup clone;
                clone.GroupName = g.GroupName + QString(" (%1)").arg(targetCfg.RegionName);
                for (const auto &c : g.Conditions) clone.Conditions.append(c.Clone());
                newRegionGroups.append(clone);
            }

            // Apply Address Swap
            for (auto &grp : newRegionGroups)
            {
                for (auto &c : grp.Conditions)
                {
                    ApplyAddressSwap(c.LeftOperand, targetCfg.AddressRanges);
                    ApplyAddressSwap(c.RightOperand, targetCfg.AddressRanges);
                }
            }

            // Apply Lock
            for (auto &grp : newRegionGroups)
            {
                QList<AchievementCondition> prep;
                for (const auto &c : targetCfg.LockConditions) prep.append(c.Clone());
                for (int i = prep.size() - 1; i >= 0; --i)
                    grp.Conditions.prepend(prep[i]);
            }

            // Reassemble Alts naming
            int startIdx = finalGroups.size();
            for (int i = 0; i < newRegionGroups.size(); i++)
            {
                if (!localItem->isLeaderboard())
                    newRegionGroups[i].GroupName = QString("Alt %1 (%2)").arg(startIdx + i).arg(targetCfg.RegionName);
            }

            finalGroups.append(newRegionGroups);
        }

        segments[segmentKey] = finalGroups;

        // IMPORTANT: Update Metadata with new Group Names
        _metadata.UpdateAssetGroups(localItem->ID, finalGroups);
    }

    // Save Back
    localItem->Logic = strategy->BuildLogic(segments);
}

void MultiRegionBatchProcessor::ApplyAddressSwap(Operand &op, const QList<AddressRangeMap> &ranges)
{
    if (!RaLogicSyntax::MemoryTypes.contains(op.Type)) return;
    QString addr = op.Value.trimmed();
    if (addr.isEmpty()) return;

    QString clean = addr;
    // Match C#'s case-sensitive Replace("0x", ""): "0X..." is left untouched.
    clean.remove("0x");
    clean = clean.trimmed();
    bool ok = false;
    int64_t val = clean.toLongLong(&ok, 16);
    if (ok)
    {
        for (const auto &range : ranges)
        {
            if (range.IsInRange(val))
            {
                int64_t newVal = range.CalculateTarget(val);
                op.Value = QString("0x%1").arg(newVal, 0, 16);
                return; // Swap once
            }
        }
    }
}

} // namespace RALE
