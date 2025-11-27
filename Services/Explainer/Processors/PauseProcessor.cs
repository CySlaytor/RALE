using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer; // Added missing reference
using System.Collections.Generic;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Handles Pause If
    public class PauseProcessor : ILogicProcessor
    {
        public int Priority => 40;
        public bool CanProcess(List<AchievementCondition> conditions, int index) => conditions[index].Flag == "Pause If";

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var res = new StandardConditionProcessor().Process(ctx, conditions, index);
            string action = conditions[index].RequiredHits > 0 ? "lock the achievement until a Reset occurs (PauseLock)" : "pause the achievement";
            res.Text = $"If {res.Text}, {action}.";
            res.Category = ExplanationCategory.Pause;
            return res;
        }
    }
}