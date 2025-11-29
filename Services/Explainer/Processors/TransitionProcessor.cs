using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Handles Delta Transitions (A > Delta A)
    public class TransitionProcessor : ILogicProcessor
    {
        // CRITICAL: Priority must be 1 to beat AsciiStringProcessor (2) and AccumulatorProcessor (3).
        // Otherwise, AsciiStringProcessor claims Row 2 ("Trigger Mem...") and processes it as a single standard condition.
        public int Priority => 1;

        public bool CanProcess(List<AchievementCondition> conditions, int currentIndex)
        {
            var condA = conditions[currentIndex];

            // 1. Basic Operand Validation for A
            if (condA.RequiredHits > 0 || condA.RightOperand.Type != "Value") return false;

            bool aIsDelta = condA.LeftOperand.Type == "Delta" || condA.LeftOperand.Type == "Prior";
            bool aIsMem = condA.LeftOperand.Type == "Mem";
            if (!aIsDelta && !aIsMem) return false;

            // 2. Look ahead for matching pair (condB)
            for (int j = currentIndex + 1; j < conditions.Count; j++)
            {
                var condB = conditions[j];

                // Skip pointer chain logic
                if (condB.Flag == "Add Address") continue;

                // Validate Flag Compatibility
                // We allow: No Flag, Trigger, Measured*, AndNext, ResetNextIf.
                // This allows catching "Trigger Mem=6" + "AndNext Delta=5".
                bool isValidFlag = string.IsNullOrEmpty(condB.Flag) ||
                                   condB.Flag == "Trigger" ||
                                   condB.Flag == "AndNext" ||
                                   condB.Flag == "ResetNextIf" ||
                                   condB.Flag.StartsWith("Measured");

                if (!isValidFlag) return false;

                // Basic Validation for B
                if (condB.RequiredHits > 0 || condB.RightOperand.Type != "Value") continue;

                bool bIsDelta = condB.LeftOperand.Type == "Delta" || condB.LeftOperand.Type == "Prior";
                bool bIsMem = condB.LeftOperand.Type == "Mem";

                // Ensure we aren't comparing Delta vs Delta or Mem vs Mem
                if (aIsDelta && bIsDelta) continue;
                if (aIsMem && bIsMem) continue;

                // Check if they refer to the same address/value (Robust Comparison)
                // This fixes issues where 0x005f != 0x005F or leading zeros differ
                if (AreSameAddress(condA.LeftOperand.Value, condB.LeftOperand.Value) &&
                    condA.LeftOperand.Size == condB.LeftOperand.Size)
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

            // Find the pair (re-run lookahead)
            int pairIndex = -1;
            for (int j = currentIndex + 1; j < conditions.Count; j++)
            {
                var condB = conditions[j];
                if (condB.Flag == "Add Address") continue;

                // Robust comparison again to ensure we catch the same pair found in CanProcess
                if (AreSameAddress(condA.LeftOperand.Value, condB.LeftOperand.Value) &&
                    condA.LeftOperand.Size == condB.LeftOperand.Size)
                {
                    // Verify types oppose (Mem vs Delta)
                    bool bIsDelta = condB.LeftOperand.Type == "Delta" || condB.LeftOperand.Type == "Prior";
                    if (aIsDelta != bIsDelta) { pairIndex = j; break; }
                }
            }

            // Fallback if something changed between CanProcess and Process
            if (pairIndex == -1) return new StandardConditionProcessor().Process(ctx, conditions, currentIndex);

            var condPair = conditions[pairIndex];

            // Use GetName(Operand) to support Bit Labels (e.g. [Started Career])
            string name = ctx.GetName(condA.LeftOperand, currentIndex);

            // Determine which is Previous and which is Current based on type
            var prevCond = aIsDelta ? condA : condPair;
            var currCond = !aIsDelta ? condA : condPair;

            // Format Values
            string prevVal = ctx.FormatValue(prevCond.RightOperand, conditions.IndexOf(prevCond), condA.LeftOperand.Value, condA.LeftOperand.Size);
            string currVal = ctx.FormatValue(currCond.RightOperand, conditions.IndexOf(currCond), condA.LeftOperand.Value, condA.LeftOperand.Size);

            // Check for Measured logic on the current condition
            string suffix = "";
            if (currCond.Flag.StartsWith("Measured"))
            {
                string limitText = (currCond.Operator == "=" || currCond.Operator == ">=") ? $", measuring up to {currVal}" : "";
                suffix = $". This condition will also display a Measured Indicator{limitText}";
            }

            string text = "";

            // Logic: Prev=X AND Curr=Y -> "Changes from X to Y"
            if (prevCond.Operator == "=" && currCond.Operator == "=")
            {
                text = $"{name} changes from {prevVal} to {currVal}{suffix}";
            }
            // Logic: Prev!=X AND Curr=X -> "Becomes X"
            else if (prevCond.Operator == "!=" && currCond.Operator == "=" && prevVal == currVal)
            {
                text = $"{name} becomes {currVal}{suffix}";
            }
            else
            {
                // Fallback for weird operator combinations (e.g. Delta > 5 AND Mem < 2)
                return new StandardConditionProcessor().Process(ctx, conditions, currentIndex);
            }

            return new ProcessorResult
            {
                Text = text,
                Category = ExplanationCategory.Transition,
                ConditionsConsumed = (pairIndex - currentIndex) + 1
            };
        }

        private bool AreSameAddress(string addrA, string addrB)
        {
            if (string.IsNullOrEmpty(addrA) || string.IsNullOrEmpty(addrB)) return false;

            // Safe normalisation for "0x" vs "0X" vs no prefix
            string a = addrA.Trim();
            if (a.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) a = a.Substring(2);

            string b = addrB.Trim();
            if (b.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) b = b.Substring(2);

            // If lengths differ significantly (leading zeros), parse as numbers
            if (a.Length != b.Length)
            {
                if (long.TryParse(a, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long valA) &&
                    long.TryParse(b, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long valB))
                {
                    return valA == valB;
                }
            }

            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}