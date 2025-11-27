using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

    // Handles AndNext / OrNext / ResetNextIf chains
    public class AndNextProcessor : ILogicProcessor
    {
        public int Priority => 15;

        public bool CanProcess(List<AchievementCondition> conditions, int index)
        {
            string f = conditions[index].Flag;
            return f == "AndNext" || f == "OrNext" || f == "ResetNextIf";
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var chainItems = new List<(string Text, string Connector)>();
            var resetConditions = new List<string>();
            int ptr = index;

            // 1. Consume the chain of Logic Flags
            while (ptr < conditions.Count)
            {
                var cond = conditions[ptr];
                string f = cond.Flag;

                if (f != "AndNext" && f != "OrNext" && f != "ResetNextIf") break;

                // Process the gate condition
                var std = new StandardConditionProcessor().Process(ctx, conditions, ptr);

                if (f == "ResetNextIf")
                {
                    resetConditions.Add(std.Text);
                }
                else
                {
                    string connector = (f == "OrNext") ? "OR" : "AND";
                    chainItems.Add((std.Text, connector));
                }

                ptr++;
            }

            // 2. Skip over Add Address lines to find the Target Condition
            while (ptr < conditions.Count && conditions[ptr].Flag == "Add Address")
            {
                ptr++;
            }

            string resultText = "";
            int consumed = ptr - index;
            var category = ExplanationCategory.Context;

            if (ptr < conditions.Count)
            {
                // 3. Resolve Target Text
                // We delegate the processing of the target to the full processor list (manually checking priority)
                // to ensure that if the target is a Transition, Accumulator, Pause, or Reset, it gets formatted correctly.

                ProcessorResult targetRes;
                var transProc = new TransitionProcessor();
                var measuredProc = new MeasuredTriggerProcessor();
                var resetProc = new ResetProcessor();
                var pauseProc = new PauseProcessor();
                var standardProc = new StandardConditionProcessor();

                // Priority Check: Transition -> Measured -> Reset -> Pause -> Standard
                if (transProc.CanProcess(conditions, ptr))
                {
                    targetRes = transProc.Process(ctx, conditions, ptr);
                }
                else if (measuredProc.CanProcess(conditions, ptr))
                {
                    targetRes = measuredProc.Process(ctx, conditions, ptr);
                }
                else if (resetProc.CanProcess(conditions, ptr))
                {
                    targetRes = resetProc.Process(ctx, conditions, ptr);
                }
                else if (pauseProc.CanProcess(conditions, ptr))
                {
                    targetRes = pauseProc.Process(ctx, conditions, ptr);
                }
                else
                {
                    targetRes = standardProc.Process(ctx, conditions, ptr);
                }

                consumed += targetRes.ConditionsConsumed; // Add the target's consumption count
                category = targetRes.Category; // Adopt the category of the target (e.g. Transition, Reset, Pause)

                // 4. Build Final Logic String
                // Format: "(ChainItem1 [AND/OR] ChainItem2 [AND/OR] Target)"
                var sb = new StringBuilder();

                if (chainItems.Count > 0)
                {
                    // Unwrap the target text if it is a complex sentence (like MeasuredIf, Reset, Pause)
                    // so we can place the logic chain inside the condition, not outside the sentence.
                    string targetText = targetRes.Text;
                    string prefixWrapper = "";
                    string suffixWrapper = "";

                    if (category == ExplanationCategory.MeasuredIf)
                    {
                        string p = "The Measured condition will work if ";
                        string s = " (MeasuredIf)";
                        if (targetText.StartsWith(p) && targetText.EndsWith(s))
                        {
                            prefixWrapper = p;
                            suffixWrapper = s;
                            targetText = targetText.Substring(p.Length, targetText.Length - p.Length - s.Length);
                        }
                    }
                    else if (category == ExplanationCategory.Reset)
                    {
                        string p = "If ";
                        string s = ", reset the achievement.";
                        if (targetText.StartsWith(p) && targetText.EndsWith(s))
                        {
                            prefixWrapper = p;
                            suffixWrapper = s;
                            targetText = targetText.Substring(p.Length, targetText.Length - p.Length - s.Length);
                        }
                    }
                    else if (category == ExplanationCategory.Pause)
                    {
                        string p = "If ";
                        // Pause suffix varies slightly
                        if (targetText.StartsWith(p))
                        {
                            int lastComma = targetText.LastIndexOf(',');
                            if (lastComma > p.Length)
                            {
                                prefixWrapper = p;
                                suffixWrapper = targetText.Substring(lastComma); // includes .
                                targetText = targetText.Substring(p.Length, lastComma - p.Length);
                            }
                        }
                    }

                    sb.Append(prefixWrapper);
                    sb.Append("(");
                    foreach (var item in chainItems)
                    {
                        sb.Append(item.Text);
                        sb.Append($" {item.Connector} ");
                    }
                    sb.Append(targetText);
                    sb.Append(")");
                    sb.Append(suffixWrapper);
                }
                else
                {
                    // Only ResetNextIf existed in the chain, just show target
                    sb.Append(targetRes.Text);
                }

                if (resetConditions.Count > 0)
                {
                    string resets = string.Join(" OR ", resetConditions);
                    sb.Append($" (resets if {resets})");
                }

                resultText = sb.ToString();
            }
            else
            {
                // Incomplete chain
                var sb = new StringBuilder();
                foreach (var item in chainItems) sb.Append($"{item.Text} {item.Connector} ");
                resultText = $"{sb} (Incomplete Chain)";
            }

            return new ProcessorResult
            {
                Text = resultText,
                Category = category,
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

            // FIX: Pass cond.LeftOperand.Value and Size as context to GetOperandText for Right Operand
            string name = ctx.GetOperandText(cond.LeftOperand, index);
            string limit = ctx.GetOperandText(cond.RightOperand, index, true, cond.LeftOperand.Value, cond.LeftOperand.Size);
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
                // FIX: Use GetName(Operand)
                string name = ctx.GetName(cond.LeftOperand, index);
                return new ProcessorResult { Text = $"Store the amount {name} decreased by", Category = ExplanationCategory.Context };
            }
            if (cond.LeftOperand.Type == "Mem" && cond.RightOperand.Type == "Delta" && sameAddr && cond.Operator == "-")
            {
                // FIX: Use GetName(Operand)
                string name = ctx.GetName(cond.LeftOperand, index);
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
            if (cond.LeftOperand.Type == "Value" && cond.RightOperand.Type == "Value")
            {
                long l = ParseHex(cond.LeftOperand.Value);
                long r = ParseHex(cond.RightOperand.Value);

                if (cond.Operator == "=")
                {
                    if (l == r) return new ProcessorResult { Text = "Always True (Dummy Condition)", Category = ExplanationCategory.Context };
                    else return new ProcessorResult { Text = "Always False", Category = ExplanationCategory.Context };
                }
            }

            // 2. Simple Change check (Delta)
            if (cond.LeftOperand.Type == "Mem" && cond.RightOperand.Type == "Delta" && cond.LeftOperand.Value == cond.RightOperand.Value)
            {
                // FIX: Use GetName(Operand)
                string name = ctx.GetName(cond.LeftOperand, index);
                string chg = cond.Operator switch { ">" => "increases", "<" => "decreases", "!=" => "changes", "=" => "remains unchanged", _ => "compares" };
                return new ProcessorResult { Text = $"{name} {chg}", Category = ExplanationCategory.Transition };
            }

            // 3. Bitwise Logic Improvement
            if (cond.Operator == "&")
            {
                string bitExplanation = TryExplainBitwise(cond, ctx, index);
                if (!string.IsNullOrEmpty(bitExplanation))
                {
                    return new ProcessorResult { Text = bitExplanation, Category = ExplanationCategory.Context };
                }
            }

            // 4. Standard Processing
            // FIX: Pass contextSize to GetOperandText for Right Operand
            string left = ctx.GetOperandText(cond.LeftOperand, index);
            string right = ctx.GetOperandText(cond.RightOperand, index, true, cond.LeftOperand.Value, cond.LeftOperand.Size);
            string op = ctx.GetOperatorText(cond.Operator);

            string hits = "";
            if (cond.RequiredHits > 0)
            {
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
            if (cond.RightOperand.Type != "Value") return "";

            long maskVal = ParseHex(cond.RightOperand.Value);
            if (maskVal <= 0) return "";

            if ((maskVal & (maskVal - 1)) == 0)
            {
                int bitIndex = (int)Math.Log(maskVal, 2);
                // FIX: Use GetName(Operand) to ensure we get Bit Labels if this is a bitwise op on a variable that has them
                string name = ctx.GetName(cond.LeftOperand, index);
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