using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer;
using System.Collections.Generic;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Handles Measured / Measured% / Trigger conditions
    public class MeasuredTriggerProcessor : ILogicProcessor
    {
        public int Priority => 50;

        public bool CanProcess(List<AchievementCondition> conditions, int index)
        {
            var f = conditions[index].Flag;
            // Exclude MeasuredIf so it falls through to StandardProcessor (or specific handlers)
            return (f.StartsWith("Measured") && f != "MeasuredIf") || f == "Trigger";
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var cond = conditions[index];

            // 1. Handle Trigger
            if (cond.Flag == "Trigger")
            {
                // Detect Dummy Triggers (Value = Value) and hide them
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

            // 2. Handle Measured with Hits (e.g. "Increases up to 8")
            if (cond.RequiredHits > 0)
            {
                // Delegate to Standard/Transition logic to describe WHAT is happening (e.g. "Value increases")
                // This ensures we get the "Changes from X to Y" text if applicable, or standard comparison text.
                var stdRes = new StandardConditionProcessor().Process(ctx, conditions, index);

                return new ProcessorResult
                {
                    Text = $"A Measured Indicator will start measuring: {stdRes.Text} up to {cond.RequiredHits}",
                    Category = ExplanationCategory.Complex
                };
            }

            // 3. Handle Standard Value Measurement
            // Pass cond.LeftOperand.Value and Size as context to GetOperandText for Right Operand
            // so we can resolve Bit/Enum labels for values if defined in the note.
            string name = ctx.GetOperandText(cond.LeftOperand, index);
            string limit = ctx.GetOperandText(cond.RightOperand, index, true, cond.LeftOperand.Value, cond.LeftOperand.Size);
            string text;

            if (cond.Flag == "Measured%")
            {
                text = $"{name} as %";
            }
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