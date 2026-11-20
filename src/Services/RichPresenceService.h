#pragma once

#include "../Models/RegionConfig.h"
#include "../Models/RichPresence.h"

#include <QList>
#include <QRegularExpression>

namespace RALE {

class RichPresenceService {
  public:
    RichPresenceScript GenerateMultiRegion(const RichPresenceScript& sourceScript, const QList<RegionConfig>& configs);

  private:
    bool HasAddressLookups(const QString& text);
    QString BuildLockString(const QList<AchievementCondition>& conditions);
    QString ApplyAddressMap(const QString& input, const QList<AddressRangeMap>& ranges);
};

} // namespace RALE
