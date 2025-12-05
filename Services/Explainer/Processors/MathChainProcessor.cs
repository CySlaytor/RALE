using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RALogicEditor.Services.Explainer.Processors
{
    public class MathChainProcessor : ILogicProcessor
    {
        public int Priority => 10;

        public bool CanProcess(List<AchievementCondition> conditions, int index)
        {
            if (conditions[index].Flag != "Remember") return false;
            string op = conditions[index].Operator;
            return (op == "+" || op == "-" || op == "*" || op == "/" || op == "%");
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            int ptr = index;
            string currentExpression = "";
            bool chainEnded = false;

            // 1. Build the Mathematical Expression
            while (ptr < conditions.Count && !chainEnded)
            {
                var cond = conditions[ptr];

                // Skip Add Address lines
                if (cond.Flag == "Add Address")
                {
                    ptr++;
                    continue;
                }

                if (cond.Flag != "Remember")
                {
                    chainEnded = true;
                    continue;
                }

                if (string.IsNullOrEmpty(cond.Operator))
                {
                    chainEnded = true;
                    continue;
                }

                string op = cond.Operator;
                string left = "";
                string right = "";

                if (cond.LeftOperand.Type == "Recall") left = currentExpression;
                else left = ctx.GetOperandText(cond.LeftOperand, ptr, false, "", "", true);

                if (cond.RightOperand.Type == "Recall") right = currentExpression;
                else right = ctx.GetOperandText(cond.RightOperand, ptr, true, cond.LeftOperand.Value, cond.LeftOperand.Size, true);

                bool leftIsZero = IsZero(cond.LeftOperand);
                bool rightIsZero = IsZero(cond.RightOperand);

                if (op == "+")
                {
                    if (leftIsZero) currentExpression = right;
                    else if (rightIsZero) currentExpression = left;
                    else currentExpression = $"({left} + {right})";
                }
                else if (op == "-" && rightIsZero) currentExpression = left;
                else if (op == "*" && (IsOne(cond.LeftOperand) || IsOne(cond.RightOperand))) currentExpression = IsOne(cond.LeftOperand) ? right : left;
                else if (op == "/" && IsOne(cond.RightOperand)) currentExpression = left;
                else
                {
                    if (!string.IsNullOrEmpty(currentExpression) && currentExpression.Contains(" "))
                    {
                        if (cond.LeftOperand.Type == "Recall") left = $"({left})";
                        if (cond.RightOperand.Type == "Recall") right = $"({right})";
                    }
                    currentExpression = $"{left} {op} {right}";
                }

                ptr++;
            }

            // 2. Look Ahead: Is this Math Chain feeding into an Accumulator that uses {recall}?
            // If so, consume this line silently. The AccumulatorProcessor will explain the context ("increasing"/"decreasing").
            if (ptr < conditions.Count)
            {
                var nextFlag = conditions[ptr].Flag;
                if (nextFlag.Contains("Add") || nextFlag.Contains("Sub"))
                {
                    int scan = ptr;
                    bool usesRecall = false;
                    while (scan < conditions.Count)
                    {
                        var c = conditions[scan];
                        if (!c.Flag.Contains("Add") && !c.Flag.Contains("Sub") && !c.Flag.Contains("Next") && c.Flag != "Add Address")
                            break;

                        if (c.LeftOperand.Type == "Recall" || c.RightOperand.Type == "Recall")
                        {
                            usesRecall = true;
                            break;
                        }
                        scan++;
                    }

                    if (usesRecall)
                    {
                        // Return empty text to "consume" these lines silently.
                        return new ProcessorResult
                        {
                            Text = "",
                            Category = ExplanationCategory.Context,
                            ConditionsConsumed = ptr - index
                        };
                    }
                }
            }

            // 3. Process Result Checks (if not consumed above)
            var sb = new StringBuilder();
            sb.AppendLine($"Calculate: {currentExpression}");

            int consumed = ptr - index;

            while (ptr < conditions.Count)
            {
                var cond = conditions[ptr];

                if (cond.Flag == "Add Address")
                {
                    consumed++;
                    ptr++;
                    continue;
                }

                bool leftIsRecall = cond.LeftOperand.Type == "Recall";
                bool rightIsRecall = cond.RightOperand.Type == "Recall";

                if (!leftIsRecall && !rightIsRecall) break;

                if (cond.Flag == "Measured%")
                {
                    string limit = leftIsRecall
                        ? ctx.GetOperandText(cond.RightOperand, ptr)
                        : ctx.GetOperandText(cond.LeftOperand, ptr);

                    sb.AppendLine($"- Display result as % (Limit: {limit})");
                    consumed++;
                    ptr++;
                    continue;
                }

                if (string.IsNullOrEmpty(cond.Flag) || cond.Flag == "Trigger" || cond.Flag == "MeasuredIf")
                {
                    string opText = ctx.GetOperatorText(cond.Operator);
                    string compareVal = leftIsRecall
                        ? ctx.GetOperandText(cond.RightOperand, ptr)
                        : ctx.GetOperandText(cond.LeftOperand, ptr);

                    string prefix = cond.Flag == "MeasuredIf" ? "Only measure if" : "Require";
                    sb.AppendLine($"- {prefix} result {opText} {compareVal}");

                    consumed++;
                    ptr++;
                    continue;
                }
                break;
            }

            return new ProcessorResult
            {
                Text = sb.ToString().TrimEnd(),
                Category = ExplanationCategory.Complex,
                ConditionsConsumed = consumed
            };
        }

        private bool IsZero(Operand op)
        {
            if (op.Type == "Float" && float.TryParse(op.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) return f == 0;
            if (op.Type == "Value" && long.TryParse(op.Value.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out long i)) return i == 0;
            return false;
        }

        private bool IsOne(Operand op)
        {
            if (op.Type == "Float" && float.TryParse(op.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) return f == 1.0f;
            if (op.Type == "Value" && long.TryParse(op.Value.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out long i)) return i == 1;
            return false;
        }
    }
}