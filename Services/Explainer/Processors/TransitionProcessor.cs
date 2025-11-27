using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer; // Added missing reference
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

            // FIX: Use GetName(Operand) to support Bit Labels (e.g. [Started Career])
            string name = ctx.GetName(condA.LeftOperand, currentIndex);

            var prevCond = aIsDelta ? condA : condPair;
            var currCond = !aIsDelta ? condA : condPair;

            // Context addresses
            // FIX: Pass condA.LeftOperand.Size as contextSize to FormatValue
            string prevVal = ctx.FormatValue(prevCond.RightOperand, conditions.IndexOf(prevCond), condA.LeftOperand.Value, condA.LeftOperand.Size);
            string currVal = ctx.FormatValue(currCond.RightOperand, conditions.IndexOf(currCond), condA.LeftOperand.Value, condA.LeftOperand.Size);

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
}