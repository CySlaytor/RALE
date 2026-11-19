#pragma once

#include <QString>
#include <QMap>
#include <QList>
#include "AddressRangeMap.h"
#include "LogicModels.h"

namespace RALE {

class RegionConfig
{
public:
    QString RegionName = "";
    bool IsBase = false;
    QMap<QString, QString> AddressMap;
    QList<AddressRangeMap> AddressRanges;
    QList<AchievementCondition> LockConditions;
};

} // namespace RALE
