using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer; // Added missing reference
using System;
using System.Collections.Generic;
using System.Globalization;

namespace RALogicEditor.Services.Explainer.Processors
{
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
                // Use GetName(Operand)
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
            // Pass contextSize to GetOperandText for Right Operand
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
                // Use GetName(Operand) to ensure we get Bit Labels if this is a bitwise op on a variable that has them
                string name = ctx.GetName(cond.LeftOperand, index);
                return $"{name} has Bit {bitIndex} set";
            }
            return "";
        }

        private long ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return 0;
            hex = hex.Replace("0x", "").Trim();
            if (long.TryParse(hex, NumberStyles.HexNumber, null, out long val)) return val;
            return 0;
        }
    }
}