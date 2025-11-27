using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer; // Added missing reference
using System.Collections.Generic;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Handles Reset If
    public class ResetProcessor : ILogicProcessor
    {
        public int Priority => 30;
        public bool CanProcess(List<AchievementCondition> conditions, int index) => conditions[index].Flag == "Reset If";

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var res = new StandardConditionProcessor().Process(ctx, conditions, index);
            res.Text = $"If {res.Text}, reset the achievement.";
            res.Category = ExplanationCategory.Reset;
            return res;
        }
    }
}