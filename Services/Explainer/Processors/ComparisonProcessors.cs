using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Handles Pointer Chain Setup (Add Address)
    public class PointerCleanupProcessor : ILogicProcessor
    {
        public int Priority => 5; // Very high priority to catch AddAddress before Standard
        public bool CanProcess(List<AchievementCondition> conditions, int index) => conditions[index].Flag == "Add Address";

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            // We consume this line but return no text.
            return new ProcessorResult { Text = "", Category = ExplanationCategory.Context };
        }
    }

    // Handles AndNext / OrNext chains
    public class AndNextProcessor : ILogicProcessor
    {
        public int Priority => 15;
        public bool CanProcess(List<AchievementCondition> conditions, int index) =>
            conditions[index].Flag == "AndNext" || conditions[index].Flag == "OrNext";

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var chainParts = new List<string>();
            int ptr = index;

            // Consume the chain of AndNext/OrNext
            while (ptr < conditions.Count)
            {
                var cond = conditions[ptr];
                if (cond.Flag != "AndNext" && cond.Flag != "OrNext") break;

                // Process the gate condition
                var std = new StandardConditionProcessor().Process(ctx, conditions, ptr);
                string text = std.Text;
                if (cond.Flag == "OrNext") text = "(OR) " + text;

                chainParts.Add(text);
                ptr++;
            }

            // Combine with the Target Condition (The logic being gated)
            string targetExplanation = "(No target condition)";
            int consumed = ptr - index; // Default if we run out of conditions

            if (ptr < conditions.Count)
            {
                var targetCond = conditions[ptr];

                // If the target is complex (e.g. Accumulator or Reset), we might not want to merge text deeply, 
                // but for Standard/Transition conditions, merging looks great.

                // We'll use the Standard Processor for the target to get its basic text
                // Note: If target has a Flag (e.g. ResetIf), merging gets confusing. 
                // Only merge if target has NO flag or basic flags.
                if (string.IsNullOrEmpty(targetCond.Flag) || targetCond.Flag == "Trigger" || targetCond.Flag == "Measured")
                {
                    var targetRes = new StandardConditionProcessor().Process(ctx, conditions, ptr);
                    targetExplanation = targetRes.Text;

                    // Add flag context if needed
                    if (targetCond.Flag == "Trigger") targetExplanation += " (Trigger)";
                    else if (targetCond.Flag == "Measured") targetExplanation += " (Measured)";

                    consumed++; // Consume the target too
                }
                else
                {
                    // Fallback if target is complex (e.g. ResetIf): Just describe the variable
                    string targetName = ctx.GetOperandText(targetCond.LeftOperand, ptr, false, "", true);
                    targetExplanation = $"Check {targetName}... (See next line)";
                    // Don't consume target if we can't merge cleanly
                }
            }

            // Final Output Format: "When [Gate A] and [Gate B], [Target Logic]"
            string gates = string.Join(" AND ", chainParts);
            string explanation = $"When {gates}, check if: {targetExplanation}";

            return new ProcessorResult
            {
                Text = explanation,
                Category = ExplanationCategory.Context,
                ConditionsConsumed = consumed
            };
        }
    }

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

            // 1. Dummy check
            if (cond.LeftOperand.Type == "Value" && cond.RightOperand.Type == "Value" && cond.Operator == "=" && cond.LeftOperand.Value == cond.RightOperand.Value)
                return new ProcessorResult { Text = "Always True (Dummy Condition)", Category = ExplanationCategory.Context };

            // 2. Simple Change check (Delta)
            if (cond.LeftOperand.Type == "Mem" && cond.RightOperand.Type == "Delta" && cond.LeftOperand.Value == cond.RightOperand.Value)
            {
                string name = ctx.GetName(cond.LeftOperand.Value, index);
                string chg = cond.Operator switch { ">" => "increases", "<" => "decreases", "!=" => "changes", "=" => "remains unchanged", _ => "compares" };
                return new ProcessorResult { Text = $"{name} {chg}", Category = ExplanationCategory.Transition };
            }

            // 3. Bitwise Logic Improvement (NEW)
            if (cond.Operator == "&")
            {
                string bitExplanation = TryExplainBitwise(cond, ctx, index);
                if (!string.IsNullOrEmpty(bitExplanation))
                {
                    return new ProcessorResult { Text = bitExplanation, Category = ExplanationCategory.Context };
                }
            }

            // 4. Standard Processing
            string left = ctx.GetOperandText(cond.LeftOperand, index);
            string right = ctx.GetOperandText(cond.RightOperand, index, true, cond.LeftOperand.Value);
            string op = ctx.GetOperatorText(cond.Operator);

            // Humanize Hits
            string hits = "";
            if (cond.RequiredHits > 0)
            {
                // "once X has happened N times" vs "occurs N times"
                if (cond.RequiredHits == 1) hits = " (once)";
                else hits = $" (once this happens {cond.RequiredHits} times)";
            }

            if (left.Contains("{recall}")) left = "recalled value";
            string text = $"{left} {op} {right}{hits}";

            if (cond.Flag == "MeasuredIf")
            {
                return new ProcessorResult
                {
                    Text = $"The Measured condition will work if {text} (MeasuredIf)",
                    Category = ExplanationCategory.MeasuredIf
                };
            }

            var cat = (cond.LeftOperand.Type == "Delta" || cond.RightOperand.Type == "Delta" || cond.LeftOperand.Type == "Prior")
                ? ExplanationCategory.Transition
                : ExplanationCategory.Context;

            return new ProcessorResult { Text = text, Category = cat };
        }

        private string TryExplainBitwise(AchievementCondition cond, ExplanationContext ctx, int index)
        {
            // Detect: Mem & Mask = Mask (Bit Set) OR Mem & Mask = 0 (Bit Reset)
            // Note: If Operator is '&', it implies the condition is checking if the bitwise AND result is non-zero (truthy).
            // However, usually bitwise checks involve checking specific bits from the Right Operand mask.

            // Ensure Right side is value
            if (cond.RightOperand.Type != "Value") return "";

            long maskVal = ParseHex(cond.RightOperand.Value);
            if (maskVal <= 0) return "";

            // Check if power of two (single bit)
            if ((maskVal & (maskVal - 1)) == 0)
            {
                int bitIndex = (int)Math.Log(maskVal, 2);
                string name = ctx.GetName(cond.LeftOperand.Value, index);

                // If this is just "Mem & Mask" (truthy check)
                return $"{name} has Bit {bitIndex} set";
            }
            return "";
        }

        private long ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return 0;
            hex = hex.Replace("0x", "").Trim();
            if (long.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out long val)) return val;
            return 0;
        }
    }
}