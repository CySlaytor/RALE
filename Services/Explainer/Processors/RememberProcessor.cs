using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer; // Added missing reference
using System.Collections.Generic;
using System.Linq;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Handles Remember
    public class RememberProcessor : ILogicProcessor
    {
        public int Priority => 60;
        public bool CanProcess(List<AchievementCondition> conditions, int index) => conditions[index].Flag == "Remember";

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            // 1. Look Ahead: Is this Remember feeding into an Accumulator (AddHits/Source) that uses {recall}?
            // If so, we hide this line because the AccumulatorProcessor will explain it contextually.
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
                        if (!c.Flag.Contains("Add") && !c.Flag.Contains("Sub") && !c.Flag.Contains("Next") && c.Flag != "Add Address")
                            break; // End of accumulator chain

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

            // 2. Standard Explanation (if not part of a Recall chain or standalone)
            var cond = conditions[index];
            bool leftIsDelta = cond.LeftOperand.Type == "Delta";
            bool rightIsMem = cond.RightOperand.Type == "Mem";
            bool sameAddr = cond.LeftOperand.Value == cond.RightOperand.Value;

            if (leftIsDelta && rightIsMem && sameAddr && cond.Operator == "-")
            {
                // Use GetName(Operand)
                string name = ctx.GetName(cond.LeftOperand, index);
                return new ProcessorResult { Text = $"Store the amount {name} decreased by", Category = ExplanationCategory.Context };
            }
            if (cond.LeftOperand.Type == "Mem" && cond.RightOperand.Type == "Delta" && sameAddr && cond.Operator == "-")
            {
                // Use GetName(Operand)
                string name = ctx.GetName(cond.LeftOperand, index);
                return new ProcessorResult { Text = $"Store the amount {name} increased by", Category = ExplanationCategory.Context };
            }

            return new StandardConditionProcessor().Process(ctx, conditions, index);
        }
    }
}