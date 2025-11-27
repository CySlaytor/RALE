using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer; // Added missing reference
using System.Collections.Generic;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Handles Measured / Measured% / Trigger
    public class MeasuredTriggerProcessor : ILogicProcessor
    {
        public int Priority => 50;
        public bool CanProcess(List<AchievementCondition> conditions, int index)
        {
            var f = conditions[index].Flag;
            // Exclude MeasuredIf so it falls through to StandardProcessor
            return (f.StartsWith("Measured") && f != "MeasuredIf") || f == "Trigger";
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var cond = conditions[index];
            if (cond.Flag == "Trigger")
            {
                // Detect Dummy Triggers
                if (cond.LeftOperand.Type == "Value" && cond.RightOperand.Type == "Value")
                {
                    return new ProcessorResult
                    {
                        Text = "",
                        Category = ExplanationCategory.Complex
                    };
                }

                var res = new StandardConditionProcessor().Process(ctx, conditions, index);
                res.Category = ExplanationCategory.Complex;
                return res;
            }

            // Pass cond.LeftOperand.Value and Size as context to GetOperandText for Right Operand
            string name = ctx.GetOperandText(cond.LeftOperand, index);
            string limit = ctx.GetOperandText(cond.RightOperand, index, true, cond.LeftOperand.Value, cond.LeftOperand.Size);
            string text;

            if (cond.Flag == "Measured%") text = $"{name} as %";
            else
            {
                if (cond.Operator == "=" || cond.Operator == ">=" || cond.Operator == ">")
                    text = $"{name} up to {limit}";
                else
                    text = $"{name} ({ctx.GetOperatorText(cond.Operator)} {limit})";
            }

            return new ProcessorResult
            {
                Text = $"A Measured Indicator will start measuring: {text}",
                Category = ExplanationCategory.Complex
            };
        }
    }
}