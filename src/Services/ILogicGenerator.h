#pragma once

#include <QList>
#include <QMap>
#include "../Models/LogicModels.h"
#include "../Models/GeneratorModels.h"

namespace RALogicEditor {

class ILogicGenerator
{
public:
    virtual ~ILogicGenerator() = default;

    virtual void ApplyGeneratorRules(
        QList<AchievementConditionGroup> &activeGroups,
        const QMap<int, GeneratorDefinition> &generatorRules) = 0;
};

} // namespace RALogicEditor
