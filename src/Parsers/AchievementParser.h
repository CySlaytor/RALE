#pragma once

#include "../Models/LogicModels.h"

#include <QChar>
#include <QList>
#include <QString>

namespace RALE {

class AchievementParser {
  public:
    static QList<AchievementConditionGroup> ParseAchievementTrigger(const QString& trigger, QChar separator = 'S',
                                                                    bool isLeaderboard = false);

    static Operand ParseOperand(const QString& operandStr);

  private:
    static void ParseConditionsIntoGroup(AchievementConditionGroup& group, const QString& logic);
    static AchievementCondition ParseCondition(const QString& conditionStr);
};

} // namespace RALE
