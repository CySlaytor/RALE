using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Handles Delta Transitions (A > Delta A)
    // Supports both adjacent pairs and split pairs (where other conditions exist in between)
    public class TransitionProcessor : ILogicProcessor
    {
        // CRITICAL: Priority must be 1 to beat AsciiStringProcessor (2) and AccumulatorProcessor (3).
        public int Priority => 1;

        public bool CanProcess(List<AchievementCondition> conditions, int currentIndex)
        {
            // 1. Check if this is a "Head" (Start of transition)
            if (FindPairIndex(conditions, currentIndex, out _)) return true;

            // 2. Check if this is a "Tail" (End of transition)
            // This is necessary to silence the tail condition in split transitions
            if (FindHeadIndex(conditions, currentIndex, out _)) return true;

            return false;
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int currentIndex)
        {
            // CASE 1: This is a "Tail" (The second part of a split transition)
            // If we are here, it means we were not consumed by the Head (because it was split).
            // We should hide this line to avoid redundancy, as the Head already explained the transition.
            if (FindHeadIndex(conditions, currentIndex, out int headIndex))
            {
                // Verify interruption to be safe (though if we are being processed individually, it implies interruption or logic flow issues)
                if (IsInterrupted(conditions, headIndex, currentIndex))
                {
                    return new ProcessorResult { Text = "", Category = ExplanationCategory.Transition, ConditionsConsumed = 1 };
                }

                // If not interrupted, we should have been consumed. Fallback to empty to be safe.
                return new ProcessorResult { Text = "", Category = ExplanationCategory.Transition, ConditionsConsumed = 1 };
            }

            // CASE 2: This is a "Head"
            if (!FindPairIndex(conditions, currentIndex, out int pairIndex))
            {
                // Should not happen given CanProcess, but fallback just in case
                return new StandardConditionProcessor().Process(ctx, conditions, currentIndex);
            }

            var condA = conditions[currentIndex];
            var condPair = conditions[pairIndex];
            bool aIsDelta = condA.LeftOperand.Type == "Delta" || condA.LeftOperand.Type == "Prior";

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
            if (currCond.Flag == "Measured%")
            {
                suffix = ". This condition also acts as a Measured% indicator";
            }
            else if (currCond.Flag.StartsWith("Measured"))
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
            // Logic: Prev=X AND Curr!=X -> "Changed from X" (Value was X, now is NOT X)
            else if (prevCond.Operator == "=" && currCond.Operator == "!=" && prevVal == currVal)
            {
                text = $"{name} changed from {prevVal}{suffix}";
            }
            // Logic: Prev < X AND Curr >= X -> "Increases to at least X" (Threshold Up)
            else if (prevCond.Operator == "<" && currCond.Operator == ">=" && prevVal == currVal)
            {
                text = $"{name} increases to at least {currVal}{suffix}";
            }
            // Logic: Prev <= X AND Curr > X -> "Increases to more than X" (Threshold Up)
            else if (prevCond.Operator == "<=" && currCond.Operator == ">" && prevVal == currVal)
            {
                text = $"{name} increases to more than {currVal}{suffix}";
            }
            // Logic: Prev > X AND Curr <= X -> "Decreases to at most X" (Threshold Down)
            else if (prevCond.Operator == ">" && currCond.Operator == "<=" && prevVal == currVal)
            {
                text = $"{name} decreases to at most {currVal}{suffix}";
            }
            // Logic: Prev >= X AND Curr < X -> "Decreases to less than X" (Threshold Down)
            else if (prevCond.Operator == ">=" && currCond.Operator == "<" && prevVal == currVal)
            {
                text = $"{name} decreases to less than {currVal}{suffix}";
            }
            else
            {
                // Fallback for weird operator combinations
                return new StandardConditionProcessor().Process(ctx, conditions, currentIndex);
            }

            // Determine Consumption
            // If there are unrelated conditions between Head and Tail, we cannot consume the block.
            // We consume ONLY the Head (1), and let the iterator proceed to the intermediate conditions.
            // The Tail will be silenced when the iterator eventually reaches it (Case 1 above).
            if (IsInterrupted(conditions, currentIndex, pairIndex))
            {
                return new ProcessorResult
                {
                    Text = text,
                    Category = ExplanationCategory.Transition,
                    ConditionsConsumed = 1 // Only consume Head
                };
            }
            else
            {
                // Contiguous block (adjacent or only AddAddress in between) -> Consume everything
                return new ProcessorResult
                {
                    Text = text,
                    Category = ExplanationCategory.Transition,
                    ConditionsConsumed = (pairIndex - currentIndex) + 1
                };
            }
        }

        private bool FindPairIndex(List<AchievementCondition> conditions, int currentIndex, out int pairIndex)
        {
            pairIndex = -1;
            var condA = conditions[currentIndex];

            // 1. Basic Operand Validation for A
            if (condA.RequiredHits > 0 || condA.RightOperand.Type != "Value") return false;

            bool aIsDelta = condA.LeftOperand.Type == "Delta" || condA.LeftOperand.Type == "Prior";
            bool aIsMem = condA.LeftOperand.Type == "Mem";
            if (!aIsDelta && !aIsMem) return false;

            // 2. Look ahead
            for (int j = currentIndex + 1; j < conditions.Count; j++)
            {
                var condB = conditions[j];

                // Skip pointer chain logic, as it belongs to the condition that follows it
                if (condB.Flag == "Add Address") continue;

                // Validate Flag Compatibility
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

                // Check if they refer to the same address/value (Left Operand)
                if (AreSameAddress(condA.LeftOperand.Value, condB.LeftOperand.Value) &&
                    condA.LeftOperand.Size == condB.LeftOperand.Size)
                {
                    // FIX: Ensure Right Operands match
                    if (!AreSameAddress(condA.RightOperand.Value, condB.RightOperand.Value)) continue;

                    // FIX: Ensure Operators form a valid transition
                    var prevCond = aIsDelta ? condA : condB;
                    var currCond = !aIsDelta ? condA : condB;

                    if (IsSupportedTransition(prevCond.Operator, currCond.Operator))
                    {
                        pairIndex = j;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsSupportedTransition(string opA, string opB)
        {
            // Change (Prev = X, Curr = X) - Note: This usually implies "Change to X" if context allows, but formally it's weird.
            // Actually usually "Change" is "Prev != Curr" (Implicit) or "Prev = X, Curr = Y".
            // But here we check same value on right.
            // If right values match:

            // Prev=X, Curr=X -> Changes from X to X? No.
            // Ah, the logic in Process checks "if prevVal == currVal".
            // If they are equal, then:
            // = and = => "Changes from X to X" ??
            // Actually, `Prev = X` and `Curr = X` means value stayed X. Not a transition.
            // Wait, existing code: `if (prevCond.Operator == "=" && currCond.Operator == "=")`
            // This is "Remained X".

            // Re-reading logic in Process:
            // It assumes specific pairs.

            // Let's stick to what was requested: "Changed from X" (Prev=X, Curr!=X)

            if (opA == "=" && opB == "=") return true; // Existing
            if (opA == "!=" && opB == "=") return true; // Become X
            if (opA == "=" && opB == "!=") return true; // Changed from X (New)

            // Crossing Threshold (Up)
            if (opA == "<" && opB == ">=") return true;
            if (opA == "<=" && opB == ">") return true;
            // Crossing Threshold (Down)
            if (opA == ">" && opB == "<=") return true;
            if (opA == ">=" && opB == "<") return true;

            return false;
        }

        private bool FindHeadIndex(List<AchievementCondition> conditions, int currentIndex, out int headIndex)
        {
            headIndex = -1;
            var condB = conditions[currentIndex]; // Current is potential Tail

            // Basic Validation for Tail
            if (condB.RequiredHits > 0 || condB.RightOperand.Type != "Value") return false;
            bool bIsDelta = condB.LeftOperand.Type == "Delta" || condB.LeftOperand.Type == "Prior";
            bool bIsMem = condB.LeftOperand.Type == "Mem";
            if (!bIsDelta && !bIsMem) return false;

            // Look backward
            for (int j = currentIndex - 1; j >= 0; j--)
            {
                var condA = conditions[j];

                // Skip Add Address (could be part of this condition or previous)
                if (condA.Flag == "Add Address") continue;

                // Validate A as potential Head
                if (condA.RequiredHits > 0 || condA.RightOperand.Type != "Value") continue;

                bool aIsDelta = condA.LeftOperand.Type == "Delta" || condA.LeftOperand.Type == "Prior";
                bool aIsMem = condA.LeftOperand.Type == "Mem";

                // Opposing types check
                if ((aIsDelta && bIsDelta) || (aIsMem && bIsMem)) continue;
                if (!aIsDelta && !aIsMem) continue;

                // Match Check
                if (AreSameAddress(condA.LeftOperand.Value, condB.LeftOperand.Value) &&
                    condA.LeftOperand.Size == condB.LeftOperand.Size)
                {
                    // Check if condA actually targets condB (Forward check from A)
                    // This implicitly checks operator validity via FindPairIndex now
                    if (FindPairIndex(conditions, j, out int verifyIndex) && verifyIndex == currentIndex)
                    {
                        headIndex = j;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsInterrupted(List<AchievementCondition> conditions, int start, int end)
        {
            for (int k = start + 1; k < end; k++)
            {
                if (conditions[k].Flag != "Add Address") return true;
            }
            return false;
        }

        private bool AreSameAddress(string addrA, string addrB)
        {
            if (string.IsNullOrEmpty(addrA) || string.IsNullOrEmpty(addrB)) return false;

            string a = addrA.Trim();
            if (a.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) a = a.Substring(2);

            string b = addrB.Trim();
            if (b.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) b = b.Substring(2);

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