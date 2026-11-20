#pragma once

#include "ILogicGenerator.h"

namespace RALE {

class LogicGeneratorService : public ILogicGenerator {
  public:
    void ApplyGeneratorRules(QList<AchievementConditionGroup>& activeGroups, const QMap<int, GeneratorDefinition>& generatorRules) override;

  private:
    void ApplyOffsetToOperand(Operand& op, const QString& targetAddress, int stride, int multiplier);
};

} // namespace RALE
