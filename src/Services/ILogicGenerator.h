#pragma once

#include "../Models/GeneratorModels.h"
#include "../Models/LogicModels.h"

#include <QList>
#include <QMap>

namespace RALE {

class ILogicGenerator {
  public:
    virtual ~ILogicGenerator() = default;

    virtual void ApplyGeneratorRules(QList<AchievementConditionGroup>& activeGroups,
                                     const QMap<int, GeneratorDefinition>& generatorRules) = 0;
};

} // namespace RALE
