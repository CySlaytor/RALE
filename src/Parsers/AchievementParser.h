#pragma once

#include <QString>
#include <QList>
#include <QChar>
#include "../Models/LogicModels.h"

namespace RALE {

class AchievementParser
{
public:
    static QList<AchievementConditionGroup> ParseAchievementTrigger(
        const QString &trigger,
        QChar separator = 'S',
        bool isLeaderboard = false);

    static Operand ParseOperand(const QString &operandStr);

private:
    static void ParseConditionsIntoGroup(AchievementConditionGroup &group, const QString &logic);
    static AchievementCondition ParseCondition(const QString &conditionStr);
};

} // namespace RALE
