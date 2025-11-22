using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RALogicEditor.Utilities
{
    public static class LogicFormatter
    {
        public static string ConditionToString(AchievementCondition cond)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(cond.Flag) && RaLogicSyntax.ReverseFlagMap.TryGetValue(cond.Flag, out char f)) sb.Append(f).Append(':');
            sb.Append(OperandToString(cond.LeftOperand));
            sb.Append(cond.Operator);
            sb.Append(OperandToString(cond.RightOperand));
            if (cond.RequiredHits > 0) sb.Append('.').Append(cond.RequiredHits).Append('.');
            return sb.ToString();
        }

        public static string OperandToString(Operand op)
        {
            if (op == null || string.IsNullOrEmpty(op.Type)) return "";
            if (op.Type == "Recall") return "{recall}";
            var sb = new StringBuilder();
            string core = op.Type;
            bool pfx = false;
            switch (op.Type)
            {
                case "Delta": sb.Append('d'); pfx = true; break;
                case "Prior": sb.Append('p'); pfx = true; break;
                case "BCD": sb.Append('b'); pfx = true; break;
                case "Inverted": sb.Append('~'); pfx = true; break;
                case "Float": return "f" + op.Value;
            }
            if (pfx) core = "Mem";
            if (core == "Mem")
            {
                if (RaLogicSyntax.ReverseFloatSizeMap.TryGetValue(op.Size, out char fs))
                {
                    sb.Append("f").Append(fs);
                    sb.Append(op.Value.Replace("0x", ""));
                }
                else
                {
                    sb.Append("0x");
                    if (RaLogicSyntax.ReverseSizeMap.TryGetValue(op.Size, out char s) && s != ' ') sb.Append(char.ToUpper(s));
                    sb.Append(op.Value.Replace("0x", ""));
                }
            }
            else if (core == "Value")
            {
                if (op.Value.StartsWith("0x") && long.TryParse(op.Value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long v)) return v.ToString();
                return op.Value;
            }
            else sb.Append(op.Value);
            return sb.ToString();
        }

        public static string FormatDisplayValue(Operand operand, bool showDecimal, string sizeReference = "")
        {
            if (string.IsNullOrEmpty(operand.Value)) return "";

            if (long.TryParse(operand.Value.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val))
            {
                if (operand.Type == "Value")
                {
                    if (showDecimal) return val.ToString();
                    int padding = GetPaddingForSize(sizeReference);
                    long mask = GetMaskForSize(sizeReference);
                    return "0x" + (val & mask).ToString($"x{padding}");
                }
                else if (operand.Type != "Float")
                {
                    return "0x" + val.ToString("x8");
                }
            }
            if (operand.Type == "Float")
            {
                string normalized = operand.Value.Replace(',', '.');
                if (decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decVal) && decVal % 1 == 0 && !normalized.Contains('.'))
                {
                    return normalized + ".0";
                }
                return normalized;
            }
            return operand.Value;
        }

        public static string ParseAndFormatValue(string input, string operandType, string size)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            if (operandType == "Float") return input.Replace(',', '.');

            if (operandType == "Mem" && RaLogicSyntax.ReverseFloatSizeMap.ContainsKey(size))
            {
                string safeInput = input.Replace(',', '.');
                if (float.TryParse(safeInput, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                {
                    byte[] bytes = BitConverter.GetBytes(floatVal);
                    if (size.EndsWith("BE")) Array.Reverse(bytes);
                    return "0x" + BitConverter.ToUInt32(bytes, 0).ToString("x8");
                }
                if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && uint.TryParse(input.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hexVal))
                    return "0x" + hexVal.ToString("x8");
                return input;
            }

            long val;
            int padding;
            if (operandType == "Mem" || operandType == "Delta" || operandType == "Prior" || operandType == "BCD" || operandType == "Inverted")
            {
                string hexInput = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? input.Substring(2) : input;
                if (!long.TryParse(hexInput, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out val)) return input;
                padding = 8;
            }
            else if (operandType == "Value")
            {
                if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    if (!long.TryParse(input.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out val)) return input;
                }
                else
                {
                    if (!long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out val)) return input;
                }
                padding = GetPaddingForSize(size);
            }
            else return input;

            return "0x" + val.ToString($"x{padding}");
        }

        public static string GetRightOperandSizeReference(AchievementCondition cond)
        {
            if (RaLogicSyntax.ArithmeticOperators.Contains(cond.Operator) && cond.Operator != RaLogicSyntax.NO_OPERATOR_TEXT) return "32-bit";
            return cond.LeftOperand.Size;
        }

        public static int GetPaddingForSize(string size)
        {
            if (size.Contains("8-bit") || size.Contains("Bit") || size.Contains("4")) return 2;
            if (size.Contains("16-bit")) return 4;
            if (size.Contains("24-bit")) return 6;
            return 8;
        }

        public static long GetMaskForSize(string size)
        {
            if (size.Contains("8-bit") || size.Contains("Bit") || size.Contains("4")) return 0xFF;
            if (size.Contains("16-bit")) return 0xFFFF;
            if (size.Contains("24-bit")) return 0xFFFFFF;
            return -1;
        }
    }
}