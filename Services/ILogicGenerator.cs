using RALogicEditor.Models;
using System.Collections.Generic;

namespace RALogicEditor.Services
{
    public interface ILogicGenerator
    {
        void ApplyGeneratorRules(List<AchievementConditionGroup> activeGroups, Dictionary<int, GeneratorDefinition> generatorRules);
    }
}