using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer; // Added missing reference
using System.Collections.Generic;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Handles Remember
    public class RememberProcessor : ILogicProcessor
    {
        public int Priority => 60;
        public bool CanProcess(List<AchievementCondition> conditions, int index) => conditions[index].Flag == "Remember";

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var cond = conditions[index];
            bool leftIsDelta = cond.LeftOperand.Type == "Delta";
            bool rightIsMem = cond.RightOperand.Type == "Mem";
            bool sameAddr = cond.LeftOperand.Value == cond.RightOperand.Value;

            if (leftIsDelta && rightIsMem && sameAddr && cond.Operator == "-")
            {
                // FIX: Use GetName(Operand)
                string name = ctx.GetName(cond.LeftOperand, index);
                return new ProcessorResult { Text = $"Store the amount {name} decreased by", Category = ExplanationCategory.Context };
            }
            if (cond.LeftOperand.Type == "Mem" && cond.RightOperand.Type == "Delta" && sameAddr && cond.Operator == "-")
            {
                // FIX: Use GetName(Operand)
                string name = ctx.GetName(cond.LeftOperand, index);
                return new ProcessorResult { Text = $"Store the amount {name} increased by", Category = ExplanationCategory.Context };
            }

            return new StandardConditionProcessor().Process(ctx, conditions, index);
        }
    }
}