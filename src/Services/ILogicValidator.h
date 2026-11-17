#pragma once

#include <QList>
#include <QMap>
#include <memory>
#include "../Models/LogicModels.h"
#include "../Models/NoteNode.h"
#include "../Models/AssetModels.h"
#include "../Models/ValidationModels.h"
#include "../Common/ConditionFormatter.h"

namespace RALogicEditor {

class ILogicValidator
{
public:
    virtual ~ILogicValidator() = default;

    // Updated to accept notesLookup and rangeCache for documentation checks
    virtual QList<ValidationIssue> ValidateAsset(
        const QList<AchievementConditionGroup> &groups,
        std::shared_ptr<IAssetItem> asset,
        const QMap<QString, QList<std::shared_ptr<NoteNode>>> *notesLookup = nullptr,
        const QList<ConditionFormatter::RangeEntry> *rangeCache = nullptr) = 0;
};

} // namespace RALogicEditor
