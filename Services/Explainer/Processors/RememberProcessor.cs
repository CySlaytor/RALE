using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer;
using System.Collections.Generic;
using System.Linq;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Handles Remember flag logic
    public class RememberProcessor : ILogicProcessor
    {
        public int Priority => 60;
        public bool CanProcess(List<AchievementCondition> conditions, int index) => conditions[index].Flag == "Remember";

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            // 1. Look Ahead: Is this Remember feeding into an Accumulator (AddHits/Source) that uses {recall}?
            // If so, we hide this line because the AccumulatorProcessor will explain it contextually
            // (e.g., "Start measuring the stored value...").
            if (index + 1 < conditions.Count)
            {
                var nextFlag = conditions[index + 1].Flag;
                if (nextFlag.Contains("Add") || nextFlag.Contains("Sub"))
                {
                    // Scan the next chain to see if it uses Recall
                    int scan = index + 1;
                    bool usesRecall = false;
                    while (scan < conditions.Count)
                    {
                        var c = conditions[scan];
                        // Stop if we hit the end of the accumulator chain
                        if (!c.Flag.Contains("Add") && !c.Flag.Contains("Sub") && !c.Flag.Contains("Next") && c.Flag != "Add Address")
                            break;

                        if (c.LeftOperand.Type == "Recall" || c.RightOperand.Type == "Recall")
                        {
                            usesRecall = true;
                            break;
                        }
                        scan++;
                    }

                    if (usesRecall)
                    {
                        // Return empty text to "consume" this line silently.
                        // The AccumulatorProcessor will pick up the context from this line.
                        return new ProcessorResult { Text = "", Category = ExplanationCategory.Context };
                    }
                }
            }

            // 2. Transition Logic (Increase/Decrease calculation)
            var cond = conditions[index];
            bool leftIsDelta = cond.LeftOperand.Type == "Delta";
            bool rightIsMem = cond.RightOperand.Type == "Mem";
            bool sameAddr = cond.LeftOperand.Value == cond.RightOperand.Value;

            if (leftIsDelta && rightIsMem && sameAddr && cond.Operator == "-")
            {
                string name = ctx.GetName(cond.LeftOperand, index);
                return new ProcessorResult { Text = $"Store the amount {name} decreased by", Category = ExplanationCategory.Context };
            }
            if (cond.LeftOperand.Type == "Mem" && cond.RightOperand.Type == "Delta" && sameAddr && cond.Operator == "-")
            {
                string name = ctx.GetName(cond.LeftOperand, index);
                return new ProcessorResult { Text = $"Store the amount {name} increased by", Category = ExplanationCategory.Context };
            }

            // 3. Standard Storage Logic (The fix for your screenshot)
            // We use the standard processor to resolve names/values, but we wrap the output text.
            var res = new StandardConditionProcessor().Process(ctx, conditions, index);

            if (string.IsNullOrEmpty(cond.Operator))
            {
                // No operator means we are just remembering a value/address
                // Example: "Remember Mem [0x1234]" -> "Store [Name]"
                res.Text = $"Store {res.Text}";
            }
            else
            {
                // Operator exists means we are remembering the result of a comparison (1 or 0)
                // Example: "Remember Mem > 5" -> "Store result: [Name] is greater than 5"
                res.Text = $"Store result: {res.Text}";
            }

            // Force category to Context so it appears at the top of the explanation block
            // This ensures the "Store" action is read before the "Trigger" action that uses it.
            res.Category = ExplanationCategory.Context;

            return res;
        }
    }
}