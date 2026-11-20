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
                                                 const QList<ConditionFormatter::RangeEntry>* rangeCache = nullptr) = 0;
};

} // namespace RALE
