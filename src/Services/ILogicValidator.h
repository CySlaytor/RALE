#pragma once

#include "../Common/ConditionFormatter.h"
#include "../Models/AssetModels.h"
#include "../Models/LogicModels.h"
#include "../Models/NoteNode.h"
#include "../Models/ValidationModels.h"

#include <QList>
#include <QMap>

#include <memory>

namespace RALE {

class ILogicValidator {
  public:
    virtual ~ILogicValidator() = default;

    // Updated to accept notesLookup and rangeCache for documentation checks
    virtual QList<ValidationIssue> ValidateAsset(const QList<AchievementConditionGroup>& groups, std::shared_ptr<IAssetItem> asset,
                                                 const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup = nullptr,
                                                 const QList<ConditionFormatter::RangeEntry>* rangeCache = nullptr,
                                                 const QList<CodeNote>* rawNotes = nullptr) = 0;

    virtual QList<ValidationIssue> ValidateLeaderboard(const QMap<QString, QList<AchievementConditionGroup>>& segments,
                                                       std::shared_ptr<IAssetItem> asset,
                                                       const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup = nullptr,
                                                       const QList<ConditionFormatter::RangeEntry>* rangeCache = nullptr,
                                                       const QList<CodeNote>* rawNotes = nullptr) = 0;

    virtual QList<ValidationIssue> ValidateCodeNotes(const QList<CodeNote>& notes, int consoleId = 0) = 0;

    virtual QList<ValidationIssue> ValidateSet(const QList<std::shared_ptr<IAssetItem>>& allAssets) = 0;
};

} // namespace RALE
