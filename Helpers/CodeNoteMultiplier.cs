using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Helpers
{
    public enum NamingStyle
    {
        AppendSlot,
        ReplaceSlot1,
        ReplacePlayer1,
        Custom
    }

    public enum StrideTarget
    {
        All,
        ParentsOnly,
        ChildrenOnly
    }

    public static class CodeNoteMultiplier
    {
        public static string GenerateText(string templateText, int copies, long stride, NamingStyle naming, string search, string replace, int startNumber, StrideTarget target)
        {
            var sb = new StringBuilder();
            string[] lines = templateText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            var offsetRegex = new Regex(@"^([.\+\s\u2500-\u257F]*)([-+]?)(0x[0-9A-Fa-f]+)(.*)$", RegexOptions.IgnoreCase);

            // Determine minimum indentation to differentiate Parents from Children
            int minIndent = int.MaxValue;
            foreach (var line in lines)
            {
                var m = offsetRegex.Match(line);
                if (m.Success)
                {
                    int ind = m.Groups[1].Length;
                    if (ind < minIndent) minIndent = ind;
                }
            }
            if (minIndent == int.MaxValue) minIndent = 0;

            for (int i = 0; i < copies; i++)
            {
                int currentNumber = startNumber + i;
                long currentStride = stride * (i + 1);

                for (int j = 0; j < lines.Length; j++)
                {
                    string line = lines[j];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        sb.AppendLine();
                        continue;
                    }

                    var match = offsetRegex.Match(line);
                    if (match.Success)
                    {
                        string indent = match.Groups[1].Value;
                        string sign = match.Groups[2].Value;
                        string hexStr = match.Groups[3].Value.Substring(2);
                        string remainder = match.Groups[4].Value;

                        // If the line is indented further than the root selection, it's a child.
                        bool isChild = indent.Length > minIndent;
                        long appliedStride = currentStride;

                        if (target == StrideTarget.ParentsOnly && isChild) appliedStride = 0;
                        if (target == StrideTarget.ChildrenOnly && !isChild) appliedStride = 0;

                        if (long.TryParse(hexStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long baseVal))
                        {
                            long actualVal = (sign == "-") ? -baseVal : baseVal;
                            long newVal = actualVal + appliedStride;

                            string newSign = newVal < 0 ? "-" : sign;
                            if (string.IsNullOrEmpty(sign) && newVal < 0) newSign = "-";

                            string newHex = "0x" + Math.Abs(newVal).ToString("x" + hexStr.Length);
                            string newRemainder = ApplyNaming(remainder, naming, search, replace, currentNumber);

                            sb.AppendLine($"{indent}{newSign}{newHex}{newRemainder}");
                        }
                        else
                        {
                            sb.AppendLine(ApplyNaming(line, naming, search, replace, currentNumber));
                        }
                    }
                    else
                    {
                        sb.AppendLine(ApplyNaming(line, naming, search, replace, currentNumber));
                    }
                }

                if (i < copies - 1) sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        public static string ApplyNamingToNote(string noteText, NamingStyle style, string search, string replace, int number)
        {
            string[] lines = noteText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (lines.Length > 0)
            {
                // Always apply to the first line (Title/Header of the note)
                lines[0] = ApplyNaming(lines[0], style, search, replace, number);

                // For replacements, apply to the entire body too
                if (style != NamingStyle.AppendSlot)
                {
                    for (int i = 1; i < lines.Length; i++)
                    {
                        lines[i] = ApplyNaming(lines[i], style, search, replace, number);
                    }
                }
            }
            return string.Join("\r\n", lines);
        }

        private static string ApplyNaming(string input, NamingStyle style, string search, string replace, int number)
        {
            switch (style)
            {
                case NamingStyle.AppendSlot:
                    return input + $" (Slot {number})";
                case NamingStyle.ReplaceSlot1:
                    return Regex.Replace(input, "Slot 1", $"Slot {number}", RegexOptions.IgnoreCase);
                case NamingStyle.ReplacePlayer1:
                    return Regex.Replace(input, "Player 1", $"Player {number}", RegexOptions.IgnoreCase);
                case NamingStyle.Custom:
                    if (string.IsNullOrEmpty(search)) return input;
                    string actualReplace = replace.Replace("{N}", number.ToString());
                    return Regex.Replace(input, Regex.Escape(search), actualReplace, RegexOptions.IgnoreCase);
            }
            return input;
        }
    }
}