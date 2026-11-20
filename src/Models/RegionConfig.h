#pragma once

#include "AddressRangeMap.h"
#include "LogicModels.h"

#include <QList>
#include <QMap>
#include <QString>

namespace RALE {

class RegionConfig {
  public:
    QString RegionName = "";
    bool IsBase = false;
    QMap<QString, QString> AddressMap;
    QList<AddressRangeMap> AddressRanges;
    QList<AchievementCondition> LockConditions;
};

} // namespace RALE
