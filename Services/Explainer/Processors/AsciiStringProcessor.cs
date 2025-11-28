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

            // 1. Resolve the Clean Name (for display)
            string varName = ctx.GetName(startCond.LeftOperand, index);

            // 2. Get the Raw Description (for tag detection)
            // We must look up the note directly because ctx.GetName() strips tags like [ASCII].
            string rawDescription = GetRawNoteDescription(ctx, startCond.LeftOperand.Value, index);

            // 3. Validate Keywords in the RAW description
            // Using OrdinalIgnoreCase to handle "ascii", "ASCII", "Serial", "seRial", etc.
            // This checks for the character combination anywhere in the string, ensuring robust detection
            // even if brackets or specific formatting are missing.
            bool isExplicitString = rawDescription.IndexOf("ASCII", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    rawDescription.IndexOf("Region", StringComparison.OrdinalIgnoreCase) >= 0 || // Matches "Region ID"
                                    rawDescription.IndexOf("Serial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    rawDescription.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    rawDescription.IndexOf("String", StringComparison.OrdinalIgnoreCase) >= 0;

            // 4. Process Explicit String (Mem = Value chains)
            // We now pass the 'isExplicitString' flag. If true, the processor will treat a single condition as a string.
            // If false, it will only process if multiple consecutive conditions form a string AND pass heuristic checks.
            return ProcessExplicitString(ctx, conditions, index, varName, isExplicitString);
        }

        private string GetRawNoteDescription(ExplanationContext ctx, string addressStr, int index)
        {
            long targetVal = ParseHex(addressStr);
            if (targetVal == -1) return "";

            // Check if this is part of a pointer chain (Add Address)
            if (ctx.Group != null && index > 0)
            {
                var chainStack = new Stack<string>();
                int scan = index - 1;
                while (scan >= 0)
                {
                    var prevCond = ctx.Group.Conditions[scan];
                    if (prevCond.Flag == "Add Address")
                    {
                        chainStack.Push(prevCond.LeftOperand.Value);
                        scan--;
                    }
                    else break;
                }

                if (chainStack.Count > 0)
                {
                    string baseAddrStr = chainStack.Pop();
                    string normalizedBase = LogicFormatter.NormalizeAddress(baseAddrStr);

                    if (ctx.NotesLookup != null && ctx.NotesLookup.TryGetValue(normalizedBase, out var chainNodes))
                    {
                        var currentLevelNodes = chainNodes.Where(n => n.IndentLevel != -2 && (n.Parent == null || n.Parent.IndentLevel == -1)).ToList();
                        bool chainValid = true;

                        while (chainStack.Count > 0)
                        {
                            string offsetStr = chainStack.Pop();
                            long offset = ParseHex(offsetStr);
                            if (offset == -1) { chainValid = false; break; }

                            NoteNode? match = null;
                            foreach (var node in currentLevelNodes)
                            {
                                long nodeOff = ParseHex(node.Offset);
                                if (node.Offset.Contains("-")) nodeOff = -nodeOff;

                                if (offset >= nodeOff && offset < nodeOff + node.Size)
                                {
                                    match = node;
                                    break;
                                }
                            }

                            if (match != null) currentLevelNodes = match.Children;
                            else { chainValid = false; break; }
                        }

                        if (chainValid)
                        {
                            foreach (var node in currentLevelNodes)
                            {
                                long nodeOff = ParseHex(node.Offset);
                                if (node.Offset.Contains("-")) nodeOff = -nodeOff;

                                if (targetVal >= nodeOff && targetVal < nodeOff + node.Size)
                                {
                                    return GetEffectiveDescription(node);
                                }
                            }
                        }
                    }
                }
            }

            // Fallback 1: Direct Lookup
            // Check exact match first to avoid RangeCache shadowing specific notes.
            string normAddr = LogicFormatter.NormalizeAddress(addressStr);
            if (ctx.NotesLookup != null && ctx.NotesLookup.TryGetValue(normAddr, out var directNodes))
            {
                var root = directNodes.FirstOrDefault(n => n.IndentLevel == -2) ?? directNodes.FirstOrDefault();
                if (root != null)
                {
                    return GetEffectiveDescription(root);
                }
            }

            // Fallback 2: Range Cache (for Direct Memory arrays like [ASCII: 20 bytes])
            if (ctx.RangeCache != null)
            {
                var match = ctx.RangeCache.FirstOrDefault(r => targetVal >= r.Start && targetVal < r.End);
                if (match.Node != null)
                {
                    return GetEffectiveDescription(match.Node);
                }
            }

            return "";
        }

        // Helper to extract the actual text content from a NoteNode.
        // If the Description is generic ("Full Note" from the Parser root), we must look at the Content.
        private string GetEffectiveDescription(NoteNode node)
        {
            if (string.IsNullOrEmpty(node.Description) || node.Description.Equals("Full Note", StringComparison.OrdinalIgnoreCase))
            {
                // Fallback to first line of content if description is generic
                if (!string.IsNullOrEmpty(node.Content))
                {
                    return node.Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                }
            }
            return node.Description ?? "";
        }

        // Logic for: Mem == "A" AND Mem+1 == "B" ...
        private ProcessorResult ProcessExplicitString(ExplanationContext ctx, List<AchievementCondition> conditions, int index, string baseName, bool forceStringMode)
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

            // Logic Updated: If explicitly marked as String/ASCII/Serial in code note, process it even if it's a single condition.
            if (count > 1 || (count == 1 && forceStringMode))
            {
                // If not explicitly marked as string, perform Heuristic Validation
                // to prevent grouping regular integer arrays (like HP, MP) as garbage text.
                if (!forceStringMode)
                {
                    // 1. Length Check: Avoid small 2-byte coincidences
                    if (collectedBytes.Count < 3)
                        return new StandardConditionProcessor().Process(ctx, conditions, index);

                    // 2. Printable Character Check: Requires > 70% valid ASCII (0x20-0x7E)
                    int printableCount = collectedBytes.Count(b => b >= 0x20 && b <= 0x7E);
                    double ratio = (double)printableCount / collectedBytes.Count;

                    if (ratio < 0.7)
                        return new StandardConditionProcessor().Process(ctx, conditions, index);
                }

                // Clean up name
                string cleanName = CleanName(baseName);

                string asciiString = Encoding.ASCII.GetString(collectedBytes.ToArray());
                asciiString = Regex.Replace(asciiString, @"[^\u0020-\u007E]", ".");

                string prefixText = count == 1 ? "String at" : $"{count} grouped";
                string addressText = count == 1 ? "address" : "addresses";

                return new ProcessorResult
                {
                    Text = $"{prefixText} {cleanName} {addressText} is \"{asciiString}\" (0x{collectedHex})",
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