#include "LogicOptimizer.h"

#include "AssetStrategies.h"

#include "../Definitions/RaLogicSyntax.h"

namespace RALE {

int LogicOptimizer::OptimizeAsset(std::shared_ptr<IAssetItem> asset) {
    if (asset == nullptr)
        return 0;

    // Select parsing strategy based on asset type
    std::unique_ptr<IAssetStrategy> strategy;
    if (asset->isLeaderboard())
        strategy = std::make_unique<LeaderboardStrategy>();
    else
        strategy = std::make_unique<AchievementStrategy>();

    auto segments = strategy->ParseLogic(asset->Logic);
    int changeCount = 0;

    for (auto& groups : segments) {
        for (auto& group : groups) {
            for (auto& cond : group.Conditions) {
                // Optimize operands
                if (OptimizeOperand(cond.LeftOperand))
                    changeCount++;
                if (OptimizeOperand(cond.RightOperand))
                    changeCount++;
            }
        }
    }

    if (changeCount > 0) {
        QString newLogic = strategy->BuildLogic(segments);
        if (newLogic != asset->Logic) {
            asset->Logic = newLogic;
            if (asset->State == AssetState::Synced)
                asset->State = AssetState::Modified;
        } else {
            changeCount = 0;
        }
    }

    return changeCount;
}

int LogicOptimizer::PadAsset(std::shared_ptr<IAssetItem> asset) {
    if (asset == nullptr)
        return 0;

    std::unique_ptr<IAssetStrategy> strategy;
    if (asset->isLeaderboard())
        strategy = std::make_unique<LeaderboardStrategy>();
    else
        strategy = std::make_unique<AchievementStrategy>();

    auto segments = strategy->ParseLogic(asset->Logic);
    int changeCount = 0;

    for (auto& groups : segments) {
        for (auto& group : groups) {
            for (auto& cond : group.Conditions) {
                if (PadOperand(cond.LeftOperand))
                    changeCount++;
                if (PadOperand(cond.RightOperand))
                    changeCount++;
            }
        }
    }

    if (changeCount > 0) {
        QString newLogic = strategy->BuildLogic(segments);
        if (newLogic != asset->Logic) {
            asset->Logic = newLogic;
            if (asset->State == AssetState::Synced)
                asset->State = AssetState::Modified;
        } else {
            changeCount = 0;
        }
    }

    return changeCount;
}

bool LogicOptimizer::IsFullyOptimized(std::shared_ptr<IAssetItem> asset) {
    if (asset == nullptr)
        return true;

    std::unique_ptr<IAssetStrategy> strategy;
    if (asset->isLeaderboard())
        strategy = std::make_unique<LeaderboardStrategy>();
    else
        strategy = std::make_unique<AchievementStrategy>();

    auto segments = strategy->ParseLogic(asset->Logic);

    for (const auto& groups : segments) {
        for (const auto& group : groups) {
            for (const auto& cond : group.Conditions) {
                if (HasLeadingZeros(cond.LeftOperand))
                    return false;
                if (HasLeadingZeros(cond.RightOperand))
                    return false;
            }
        }
    }
    return true;
}

bool LogicOptimizer::HasLeadingZeros(const Operand& op) {
    if (op.Value.isEmpty())
        return false;

    // Only check memory or explicit hex values
    bool isMemory = RaLogicSyntax::MemoryTypes.contains(op.Type);

    if (!isMemory)
        return false;

    QString val = op.Value.trimmed();
    if (val.startsWith("0x", Qt::CaseInsensitive)) {
        QString body = val.mid(2);
        // If length > 1 and starts with 0, it has leading zeros (e.g., 05, 0012)
        // Exception: 0x0 is fine. 0x00 is not.
        if (body.length() > 1 && body.startsWith('0'))
            return true;
    }
    return false;
}

bool LogicOptimizer::OptimizeOperand(Operand& op) {
    if (op.Value.isEmpty())
        return false;

    bool isMemory = RaLogicSyntax::MemoryTypes.contains(op.Type);

    if (!isMemory)
        return false;

    QString original = op.Value;
    QString clean = original.trimmed();

    if (clean.startsWith("0x", Qt::CaseInsensitive)) {
        QString body = clean.mid(2);

        // Strip leading zeros
        int nonZeroIdx = 0;
        while (nonZeroIdx < body.length() && body[nonZeroIdx] == '0')
            nonZeroIdx++;

        QString optimizedBody = body.mid(nonZeroIdx);

        // Ensure we don't end up with empty string for value 0
        if (optimizedBody.isEmpty())
            optimizedBody = "0";

        // Reconstruct with 0x
        QString optimized = "0x" + optimizedBody.toLower(); // Force lowercase

        if (optimized != original) {
            op.Value = optimized;
            return true;
        }
    }

    return false;
}

bool LogicOptimizer::PadOperand(Operand& op) {
    if (op.Value.isEmpty())
        return false;

    bool isMemory = RaLogicSyntax::MemoryTypes.contains(op.Type);

    if (!isMemory)
        return false;

    QString original = op.Value;
    QString clean = original.trimmed();

    if (clean.startsWith("0x", Qt::CaseInsensitive)) {
        QString body = clean.mid(2);

        // Pad to 8 chars
        if (body.length() < 8) {
            QString paddedBody = body.rightJustified(8, '0');
            QString padded = "0x" + paddedBody.toLower();

            if (padded != original) {
                op.Value = padded;
                return true;
            }
        }
    }
    return false;
}

} // namespace RALE
