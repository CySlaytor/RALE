using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System.Collections.Generic;
using System.Linq;

namespace RALogicEditor.Services
{
    public static class LogicValidator
    {
        public static List<string> ValidateGroup(AchievementConditionGroup group)
        {
            var warnings = new List<string>();
            if (group.Conditions.Count == 0) return warnings;

            // Check 1: Group ends with AddSource/SubSource (Incomplete chain)
            var last = group.Conditions.Last();
            if (RaLogicSyntax.ArithmeticFlags.Contains(last.Flag) && last.Flag != "Remember")
            {
                warnings.Add($"Group '{group.GroupName}' ends with a combining flag ({last.Flag}) but has no final condition.");
            }

            // Check 2: PauseIf as the ONLY condition (Achievement never active)
            if (group.Conditions.Count == 1 && group.Conditions[0].Flag == "Pause If")
            {
                warnings.Add($"Group '{group.GroupName}' has only a PauseIf condition. This achievement will never trigger.");
            }

            // Check 3: Size Mismatches
            foreach (var cond in group.Conditions)
            {
                if (RaLogicSyntax.MemoryTypes.Contains(cond.LeftOperand.Type) &&
                    RaLogicSyntax.MemoryTypes.Contains(cond.RightOperand.Type))
                {
                    // Only warn if both have explicit sizes and they differ significantly
                    // e.g. 8-bit vs 32-bit is usually wrong unless masking is involved
                    if (!string.IsNullOrEmpty(cond.LeftOperand.Size) &&
                        !string.IsNullOrEmpty(cond.RightOperand.Size) &&
                        cond.LeftOperand.Size != cond.RightOperand.Size)
                    {
                        // Ignore Bit comparisons as they are often mixed
                        if (!cond.LeftOperand.Size.StartsWith("Bit") && !cond.RightOperand.Size.StartsWith("Bit"))
                        {
                            warnings.Add($"Size mismatch at Row {cond.ID}: {cond.LeftOperand.Size} vs {cond.RightOperand.Size}.");
                        }
                    }
                }
            }

            return warnings;
        }
    }
}