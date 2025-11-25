using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System.Collections.Generic;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Handles Delta Transitions (A > Delta A)
    public class TransitionProcessor : ILogicProcessor
    {
        public int Priority => 20;

        public bool CanProcess(List<AchievementCondition> conditions, int currentIndex)
        {
            // Must have a delta, not be hits, value on right
            var condA = conditions[currentIndex];
            if (condA.RequiredHits > 0 || condA.RightOperand.Type != "Value") return false;

            bool aIsDelta = condA.LeftOperand.Type == "Delta" || condA.LeftOperand.Type == "Prior";
            bool aIsMem = condA.LeftOperand.Type == "Mem";
            if (!aIsDelta && !aIsMem) return false;

            // Look ahead for matching pair
            for (int j = currentIndex + 1; j < conditions.Count; j++)
            {
                var condB = conditions[j];
                if (condB.Flag == "Add Address") continue; // skip add address
                if (condB.Flag != "" && !condB.Flag.StartsWith("Trigger") && !condB.Flag.StartsWith("Measured")) return false; // Break on flags

                if (condB.RequiredHits > 0 || condB.RightOperand.Type != "Value") continue;

                bool bIsDelta = condB.LeftOperand.Type == "Delta" || condB.LeftOperand.Type == "Prior";
                bool bIsMem = condB.LeftOperand.Type == "Mem";

                if (aIsDelta && bIsDelta) continue;
                if (aIsMem && bIsMem) continue;

                if (condA.LeftOperand.Value == condB.LeftOperand.Value && condA.LeftOperand.Size == condB.LeftOperand.Size)
                {
                    return true;
                }
            }
            return false;
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int currentIndex)
        {
            var condA = conditions[currentIndex];
            bool aIsDelta = condA.LeftOperand.Type == "Delta" || condA.LeftOperand.Type == "Prior";

            // Find the pair (re-run logic from CanProcess briefly)
            int pairIndex = -1;
            for (int j = currentIndex + 1; j < conditions.Count; j++)
            {
                var condB = conditions[j];
                if (condB.Flag == "Add Address") continue;
                if (condA.LeftOperand.Value == condB.LeftOperand.Value && condA.LeftOperand.Size == condB.LeftOperand.Size)
                {
                    // Verify types oppose
                    bool bIsDelta = condB.LeftOperand.Type == "Delta" || condB.LeftOperand.Type == "Prior";
                    if (aIsDelta != bIsDelta) { pairIndex = j; break; }
                }
            }

            if (pairIndex == -1) return new StandardConditionProcessor().Process(ctx, conditions, currentIndex);

            var condPair = conditions[pairIndex];
            string name = ctx.GetName(condA.LeftOperand.Value, currentIndex);

            var prevCond = aIsDelta ? condA : condPair;
            var currCond = !aIsDelta ? condA : condPair;

            // Context addresses
            string prevVal = ctx.FormatValue(prevCond.RightOperand, conditions.IndexOf(prevCond), condA.LeftOperand.Value);
            string currVal = ctx.FormatValue(currCond.RightOperand, conditions.IndexOf(currCond), condA.LeftOperand.Value);

            string suffix = "";
            if (currCond.Flag.StartsWith("Measured"))
            {
                string limitText = (currCond.Operator == "=" || currCond.Operator == ">=") ? $", measuring up to {currVal}" : "";
                suffix = $". This condition will also display a Measured Indicator{limitText}";
            }

            string text = "";
            if (prevCond.Operator == "=" && currCond.Operator == "=")
                text = $"{name} changes from {prevVal} to {currVal}{suffix}";
            else if (prevCond.Operator == "!=" && currCond.Operator == "=" && prevVal == currVal)
                text = $"{name} becomes {currVal}{suffix}";
            else
                // Fallback if logic is weird
                return new StandardConditionProcessor().Process(ctx, conditions, currentIndex);

            return new ProcessorResult
            {
                Text = text,
                Category = ExplanationCategory.Transition,
                ConditionsConsumed = (pairIndex - currentIndex) + 1
            };
        }
    }

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

    // Handles Measured / Measured% / Trigger
    public class MeasuredTriggerProcessor : ILogicProcessor
    {
        public int Priority => 50;
        public bool CanProcess(List<AchievementCondition> conditions, int index)
        {
            var f = conditions[index].Flag;
            return f.StartsWith("Measured") || f == "Trigger";
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            // Note: Complex accumulator measured logic is handled by AccumulatorProcessor.
            // This handles simple single-condition measured/trigger lines.

            var cond = conditions[index];
            if (cond.Flag == "Trigger")
            {
                // Just explain it as a condition but mark category
                var res = new StandardConditionProcessor().Process(ctx, conditions, index);
                res.Category = ExplanationCategory.Complex;
                return res;
            }

            string name = ctx.GetOperandText(cond.LeftOperand, index);
            string limit = ctx.GetOperandText(cond.RightOperand, index, true, cond.LeftOperand.Value);
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
                string name = ctx.GetName(cond.LeftOperand.Value, index);
                return new ProcessorResult { Text = $"Store the amount {name} decreased by", Category = ExplanationCategory.Context };
            }
            if (cond.LeftOperand.Type == "Mem" && cond.RightOperand.Type == "Delta" && sameAddr && cond.Operator == "-")
            {
                string name = ctx.GetName(cond.LeftOperand.Value, index);
                return new ProcessorResult { Text = $"Store the amount {name} increased by", Category = ExplanationCategory.Context };
            }

            return new StandardConditionProcessor().Process(ctx, conditions, index);
        }
    }

    // Fallback Processor
    public class StandardConditionProcessor : ILogicProcessor
    {
        public int Priority => 100;
        public bool CanProcess(List<AchievementCondition> conditions, int index) => true;

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var cond = conditions[index];

            // Dummy check
            if (cond.LeftOperand.Type == "Value" && cond.RightOperand.Type == "Value" && cond.Operator == "=" && cond.LeftOperand.Value == cond.RightOperand.Value)
                return new ProcessorResult { Text = "Always True (Dummy Condition)", Category = ExplanationCategory.Context };

            // Simple Change check
            if (cond.LeftOperand.Type == "Mem" && cond.RightOperand.Type == "Delta" && cond.LeftOperand.Value == cond.RightOperand.Value)
            {
                string name = ctx.GetName(cond.LeftOperand.Value, index);
                string chg = cond.Operator switch { ">" => "increases", "<" => "decreases", "!=" => "changes", "=" => "remains unchanged", _ => "compares" };
                return new ProcessorResult { Text = $"{name} {chg}", Category = ExplanationCategory.Transition };
            }

            string left = ctx.GetOperandText(cond.LeftOperand, index);
            string right = ctx.GetOperandText(cond.RightOperand, index, true, cond.LeftOperand.Value);
            string op = ctx.GetOperatorText(cond.Operator);
            string hits = (cond.RequiredHits > 0) ? $" (occurs {cond.RequiredHits} times)" : "";

            if (left.Contains("{recall}")) left = "recalled value";
            string text = $"{left} {op} {right}{hits}";

            if (cond.Flag == "MeasuredIf")
            {
                text += ". (MeasuredIf)";
                return new ProcessorResult { Text = text, Category = ExplanationCategory.MeasuredIf };
            }

            // Detect if this is likely a transition based on Delta usage
            var cat = (cond.LeftOperand.Type == "Delta" || cond.RightOperand.Type == "Delta" || cond.LeftOperand.Type == "Prior")
                ? ExplanationCategory.Transition
                : ExplanationCategory.Context;

            return new ProcessorResult { Text = text, Category = cat };
        }
    }
}