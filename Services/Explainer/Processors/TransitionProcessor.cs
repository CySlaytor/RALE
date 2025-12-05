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
            if (FindHeadIndex(conditions, currentIndex, out int headIndex))
            {
                if (IsInterrupted(conditions, headIndex, currentIndex))
                {
                    return new ProcessorResult { Text = "", Category = ExplanationCategory.Transition, ConditionsConsumed = 1 };
                }
                return new ProcessorResult { Text = "", Category = ExplanationCategory.Transition, ConditionsConsumed = 1 };
            }

            // CASE 2: This is a "Head"
            if (!FindPairIndex(conditions, currentIndex, out int pairIndex))
            {
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
            bool valuesMatch = prevVal == currVal;

            // --- Specific Templates ---

            // Logic: Prev=X AND Curr=Y -> "Changes from X to Y"
            if (prevCond.Operator == "=" && currCond.Operator == "=")
            {
                text = $"{name} changes from {prevVal} to {currVal}{suffix}";
            }
            // Logic: Prev!=X AND Curr=X -> "Becomes X" (Values match)
            else if (prevCond.Operator == "!=" && currCond.Operator == "=" && valuesMatch)
            {
                text = $"{name} becomes {currVal}{suffix}";
            }
            // Logic: Prev=X AND Curr!=X -> "Changed from X" (Values match)
            else if (prevCond.Operator == "=" && currCond.Operator == "!=" && valuesMatch)
            {
                text = $"{name} changed from {prevVal}{suffix}";
            }
            // Logic: Prev < X AND Curr >= X -> "Increases to at least X" (Threshold Up)
            else if (prevCond.Operator == "<" && currCond.Operator == ">=" && valuesMatch)
            {
                text = $"{name} increases to at least {currVal}{suffix}";
            }
            // Logic: Prev <= X AND Curr > X -> "Increases to more than X" (Threshold Up)
            else if (prevCond.Operator == "<=" && currCond.Operator == ">" && valuesMatch)
            {
                text = $"{name} increases to more than {currVal}{suffix}";
            }
            // Logic: Prev > X AND Curr <= X -> "Decreases to at most X" (Threshold Down)
            else if (prevCond.Operator == ">" && currCond.Operator == "<=" && valuesMatch)
            {
                text = $"{name} decreases to at most {currVal}{suffix}";
            }
            // Logic: Prev >= X AND Curr < X -> "Decreases to less than X" (Threshold Down)
            else if (prevCond.Operator == ">=" && currCond.Operator == "<" && valuesMatch)
            {
                text = $"{name} decreases to less than {currVal}{suffix}";
            }
            // --- Generic Fallback ---
            else
            {
                // Format: "Value was [Op] [Prev] and is now [Op] [Curr]"
                string prevOpText = ctx.GetOperatorText(prevCond.Operator);
                string currOpText = ctx.GetOperatorText(currCond.Operator);

                // Helper to fix grammar: remove "is " prefix so we don't get "was is..."
                string CleanOp(string opTxt)
                {
                    if (opTxt == "is") return "";
                    if (opTxt.StartsWith("is ")) return opTxt.Substring(3);
                    return opTxt;
                }

                string prevClean = CleanOp(prevOpText);
                string currClean = CleanOp(currOpText);

                string prevPart = string.IsNullOrWhiteSpace(prevClean) ? $"was {prevVal}" : $"was {prevClean} {prevVal}";
                string currPart = string.IsNullOrWhiteSpace(currClean) ? $"is now {currVal}" : $"is now {currClean} {currVal}";

                text = $"{name} {prevPart} and {currPart}{suffix}";
            }

            // Determine Consumption
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

                // CRITICAL FIX: If the potential tail has a connector Flag like ResetNextIf,
                // we MUST NOT pair with it here. This processor (Transition) has priority 1.
                // ResetNextIf processing happens in AndNextProcessor (Priority 15).
                // If we consume it here, AndNextProcessor never sees the ResetNextIf logic.
                if (condB.Flag == "AndNext" || condB.Flag == "OrNext" || condB.Flag == "ResetNextIf")
                    return false;

                // Validate Flag Compatibility
                bool isValidFlag = string.IsNullOrEmpty(condB.Flag) ||
                                   condB.Flag == "Trigger" ||
                                   condB.Flag == "Reset If" ||
                                   condB.Flag == "Pause If" ||
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
                    // Match found!
                    pairIndex = j;
                    return true;
                }
            }
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