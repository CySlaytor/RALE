using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System.Collections.Generic;
using System.Globalization;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Detects chains of "AndNext Prior/Delta X=A, Trigger X=B" that form a sequence
    // Example: 0->1, 1->2, 2->3
    public class SequenceProcessor : ILogicProcessor
    {
        // Priority 14: Must run before AndNextProcessor (15) to capture the chain context
        public int Priority => 14;

        public bool CanProcess(List<AchievementCondition> conditions, int index)
        {
            // We need at least 4 lines for a sequence (Pair 1 + Pair 2) to justify summarizing
            if (index + 3 >= conditions.Count) return false;

            // Check Pair 1
            if (!IsTransitionPair(conditions[index], conditions[index + 1])) return false;

            // Check Pair 2
            if (!IsTransitionPair(conditions[index + 2], conditions[index + 3])) return false;

            // Check Variable Continuity (Must be same variable)
            if (conditions[index].LeftOperand.Value != conditions[index + 2].LeftOperand.Value) return false;

            // Check Value Continuity (Sequence)
            long end1 = ParseValue(conditions[index + 1].RightOperand.Value);
            long start2 = ParseValue(conditions[index + 2].RightOperand.Value);

            return end1 == start2;
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            int currentIdx = index;
            int conditionsConsumed = 0;

            string variableName = ctx.GetName(conditions[index].LeftOperand, index);
            // Use formatter to get nice Hex/Decimal string for display
            string startValStr = ctx.FormatValue(conditions[index].RightOperand, index, conditions[index].LeftOperand.Value, conditions[index].LeftOperand.Size);

            long lastEndValue = -1;
            int lastEndIndex = -1;

            while (currentIdx + 1 < conditions.Count)
            {
                var condA = conditions[currentIdx];
                var condB = conditions[currentIdx + 1];

                if (!IsTransitionPair(condA, condB)) break;

                // Ensure variable matches logic start
                if (condA.LeftOperand.Value != conditions[index].LeftOperand.Value) break;

                long valA = ParseValue(condA.RightOperand.Value);
                long valB = ParseValue(condB.RightOperand.Value);

                // Sequence Check: The "Prior" of this step must match "Current" of previous step
                if (conditionsConsumed > 0)
                {
                    if (valA != lastEndValue) break;
                }

                lastEndValue = valB;
                lastEndIndex = currentIdx + 1;

                currentIdx += 2;
                conditionsConsumed += 2;
            }

            // Format end value
            var lastCond = conditions[lastEndIndex];
            string endValStr = ctx.FormatValue(lastCond.RightOperand, lastEndIndex, lastCond.LeftOperand.Value, lastCond.LeftOperand.Size);

            return new ProcessorResult
            {
                Text = $"{variableName} increments sequentially from {startValStr} to {endValStr} (step-by-step)",
                Category = ExplanationCategory.Complex, // Group under Complex/Logic
                ConditionsConsumed = conditionsConsumed
            };
        }

        private bool IsTransitionPair(AchievementCondition a, AchievementCondition b)
        {
            // Pattern: 
            // A: AndNext Prior/Delta X = V
            // B: (Any Flag) Mem X = V+1 (or just != V)

            if (a.Flag != "AndNext") return false;

            // Allow both Prior and Delta
            bool aIsPriorOrDelta = a.LeftOperand.Type == "Prior" || a.LeftOperand.Type == "Delta";
            bool bIsMem = b.LeftOperand.Type == "Mem";

            if (!aIsPriorOrDelta || !bIsMem) return false;
            if (a.LeftOperand.Value != b.LeftOperand.Value) return false;

            return true;
        }

        private long ParseValue(string val)
        {
            string clean = val.Trim().Replace("0x", "");
            if (long.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long res)) return res;
            return 0;
        }
    }
}