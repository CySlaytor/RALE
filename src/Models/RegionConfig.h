#pragma once

#include <QString>
#include <QMap>
#include <QList>
#include "AddressRangeMap.h"
#include "LogicModels.h"

namespace RALogicEditor {

class RegionConfig
{
public:
    QString RegionName = "";
    bool IsBase = false;
    QMap<QString, QString> AddressMap;
    QList<AddressRangeMap> AddressRanges;
    QList<AchievementCondition> LockConditions;
};

} // namespace RALogicEditor
