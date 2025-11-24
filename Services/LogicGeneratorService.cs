using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RALogicEditor.Services
{
    public class LogicGeneratorService : ILogicGenerator
    {
        public void ApplyGeneratorRules(
            List<AchievementConditionGroup> activeGroups,
            Dictionary<int, GeneratorDefinition> generatorRules)
        {
            var processedIndices = new List<int>(generatorRules.Keys);
            processedIndices.Sort();

            foreach (int masterIndex in processedIndices)
            {
                if (masterIndex >= activeGroups.Count) continue;

                var masterGroup = activeGroups[masterIndex];
                var def = generatorRules[masterIndex];

                if (def.IsArray)
                {
                    // Array Mode: Append generated logic to the SAME group.
                    // The 'Template' is the set of conditions that existed before generation.
                    // The TriggerViewModel takes care of truncating the group to the TemplateConditionCount
                    // before calling this service, so 'masterGroup.Conditions' currently contains ONLY the template.

                    // We need to capture the current template conditions to clone them
                    var templateConditions = new List<AchievementCondition>();
                    foreach (var c in masterGroup.Conditions) templateConditions.Add(c.Clone());

                    // Generate N copies
                    for (int i = 1; i <= def.Copies; i++)
                    {
                        foreach (var cond in templateConditions)
                        {
                            var newCond = cond.Clone();
                            foreach (var rule in def.Rules)
                            {
                                ApplyOffsetToOperand(newCond.LeftOperand, rule.SourceAddress, rule.Stride, i);
                                ApplyOffsetToOperand(newCond.RightOperand, rule.SourceAddress, rule.Stride, i);
                            }
                            masterGroup.Conditions.Add(newCond);
                        }
                    }
                }
                else
                {
                    // Normal Mode: Generate NEW groups (Alts)
                    while (activeGroups.Count <= masterIndex + def.Copies)
                    {
                        activeGroups.Insert(masterIndex + 1, new AchievementConditionGroup { GroupName = "Temp" });
                    }

                    for (int i = 1; i <= def.Copies; i++)
                    {
                        int targetGroupIndex = masterIndex + i;
                        var targetGroup = activeGroups[targetGroupIndex];

                        string baseName = masterGroup.GroupName;
                        if (baseName.Contains(" #")) baseName = Regex.Replace(baseName, @" #\d+", "");
                        targetGroup.GroupName = $"{baseName} (+{i})";

                        targetGroup.Conditions.Clear();
                        foreach (var cond in masterGroup.Conditions)
                        {
                            var newCond = cond.Clone();
                            foreach (var rule in def.Rules)
                            {
                                ApplyOffsetToOperand(newCond.LeftOperand, rule.SourceAddress, rule.Stride, i);
                                ApplyOffsetToOperand(newCond.RightOperand, rule.SourceAddress, rule.Stride, i);
                            }
                            targetGroup.Conditions.Add(newCond);
                        }
                    }
                }
            }
        }

        private void ApplyOffsetToOperand(Operand op, string targetAddress, int stride, int multiplier)
        {
            if (op == null || string.IsNullOrEmpty(op.Value)) return;
            if (op.Type == "Recall") return;

            string cleanTarget = targetAddress.StartsWith("0x") ? targetAddress.Substring(2) : targetAddress;
            string cleanOp = op.Value.StartsWith("0x") ? op.Value.Substring(2) : op.Value;

            if (cleanOp.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(cleanOp, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long baseVal))
                {
                    long offset = (long)stride * multiplier;
                    long newVal = baseVal + offset;
                    int padding = cleanOp.Length;
                    op.Value = "0x" + newVal.ToString($"x{padding}");
                }
            }
        }
    }
}