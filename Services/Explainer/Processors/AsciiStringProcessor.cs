using RALogicEditor.Definitions;
using RALogicEditor.Helpers;
using RALogicEditor.Models;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Services.Explainer.Processors
{
    public class AsciiStringProcessor : ILogicProcessor
    {
        // Priority 2: Must run before AccumulatorProcessor (3) and StandardProcessor (100)
        public int Priority => 2;

        public bool CanProcess(List<AchievementCondition> conditions, int index)
        {
            var cond = conditions[index];

            // Only process standard Memory checks: Mem = Value
            // We explicitly EXCLUDE Add Source / Sub Source here now, per instructions.
            // Those will fall through to the standard AccumulatorProcessor.
            if (cond.LeftOperand.Type != "Mem" || cond.RightOperand.Type != "Value" || cond.Operator != "=")
                return false;

            // Must not have complex flags that alter flow (like Pause/Reset/Measured)
            // We only group "requirements" (empty flag or Trigger)
            if (!string.IsNullOrEmpty(cond.Flag) && cond.Flag != "Trigger")
                return false;

            return true;
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var startCond = conditions[index];

            // Resolve the name of the variable to check for ASCII/Region tags.
            // ctx.GetName handles pointer chain resolution automatically.
            string varName = ctx.GetName(startCond.LeftOperand, index);

            // 1. Validate Keywords in the resolved name
            bool isAscii = varName.IndexOf("ASCII", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isRegion = varName.IndexOf("Region ID", StringComparison.OrdinalIgnoreCase) >= 0;

            // If not marked as string logic, delegate to standard processor
            if (!isAscii && !isRegion)
            {
                return new StandardConditionProcessor().Process(ctx, conditions, index);
            }

            // 2. Process Explicit String (Mem = Value chains)
            return ProcessExplicitString(ctx, conditions, index, varName);
        }

        // Logic for: Mem == "A" AND Mem+1 == "B" ...
        private ProcessorResult ProcessExplicitString(ExplanationContext ctx, List<AchievementCondition> conditions, int index, string baseName)
        {
            var startCond = conditions[index];
            var collectedBytes = new List<byte>();
            var collectedHex = new StringBuilder();
            int count = 0;
            long currentAddr = ParseHex(startCond.LeftOperand.Value);
            int currentSize = GetByteSize(startCond.LeftOperand.Size);

            // Helper to extract bytes from value, handling Endianness
            void ProcessValue(string valStr, string sizeStr)
            {
                long val = ParseHex(valStr);
                int size = GetByteSize(sizeStr);
                bool isBigEndian = !string.IsNullOrEmpty(sizeStr) && sizeStr.Contains("BE", StringComparison.OrdinalIgnoreCase);

                collectedHex.Append(val.ToString($"X{size * 2}"));

                if (isBigEndian)
                {
                    for (int b = size - 1; b >= 0; b--)
                    {
                        byte bVal = (byte)((val >> (b * 8)) & 0xFF);
                        collectedBytes.Add(bVal);
                    }
                }
                else
                {
                    for (int b = 0; b < size; b++)
                    {
                        byte bVal = (byte)((val >> (b * 8)) & 0xFF);
                        collectedBytes.Add(bVal);
                    }
                }
            }

            ProcessValue(startCond.RightOperand.Value, startCond.LeftOperand.Size);
            count++;

            int scan = index + 1;
            while (scan < conditions.Count)
            {
                var next = conditions[scan];
                if (next.Flag != startCond.Flag) break;
                if (next.LeftOperand.Type != "Mem" || next.RightOperand.Type != "Value" || next.Operator != "=") break;

                long nextAddr = ParseHex(next.LeftOperand.Value);
                if (nextAddr != currentAddr + currentSize) break;

                int nextSize = GetByteSize(next.LeftOperand.Size);
                ProcessValue(next.RightOperand.Value, next.LeftOperand.Size);

                currentAddr = nextAddr;
                currentSize = nextSize;
                count++;
                scan++;
            }

            if (count > 1)
            {
                // Clean up name
                string cleanName = CleanName(baseName);

                string asciiString = Encoding.ASCII.GetString(collectedBytes.ToArray());
                asciiString = Regex.Replace(asciiString, @"[^\u0020-\u007E]", ".");

                return new ProcessorResult
                {
                    Text = $"{count} grouped {cleanName} addresses is \"{asciiString}\" (0x{collectedHex})",
                    Category = ExplanationCategory.Context,
                    ConditionsConsumed = count
                };
            }

            return new StandardConditionProcessor().Process(ctx, conditions, index);
        }

        private string CleanName(string name)
        {
            // Remove pointer offsets like "+0x8"
            string clean = Regex.Replace(name, @"\s*\+0x[0-9A-Fa-f]+", "");
            // Remove brackets [ ]
            if (clean.StartsWith("[") && clean.EndsWith("]"))
                clean = clean.Substring(1, clean.Length - 2);
            return clean.Trim();
        }

        private long ParseHex(string hex)
        {
            string clean = hex.Replace("0x", "").Trim();
            if (long.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val)) return val;
            return -1;
        }

        private int GetByteSize(string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr)) return 4;
            if (sizeStr.Contains("8-bit")) return 1;
            if (sizeStr.Contains("16-bit")) return 2;
            if (sizeStr.Contains("24-bit")) return 3;
            if (sizeStr.Contains("32-bit")) return 4;
            return 4;
        }
    }
}