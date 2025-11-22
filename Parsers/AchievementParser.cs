using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

#nullable enable

namespace RALogicEditor.Parsers
{
    public static class AchievementParser
    {
        public static List<AchievementConditionGroup> ParseAchievementTrigger(string trigger, char separator = 'S', bool isLeaderboard = false)
        {
            var conditionSet = new List<AchievementConditionGroup>();
            if (string.IsNullOrWhiteSpace(trigger)) return conditionSet;

            string[] groupStrings;

            if (isLeaderboard)
            {
                groupStrings = Regex.Split(trigger, "::");
                string[] groupNames = { "START", "CANCEL", "SUBMIT", "VALUE" };
                int count = Math.Min(groupStrings.Length, groupNames.Length);

                for (int i = 0; i < count; i++)
                {
                    string logicPart = groupStrings[i] ?? "";
                    if (logicPart.StartsWith("STA:")) logicPart = logicPart.Substring(4);
                    else if (logicPart.StartsWith("CAN:")) logicPart = logicPart.Substring(4);
                    else if (logicPart.StartsWith("SUB:")) logicPart = logicPart.Substring(4);
                    else if (logicPart.StartsWith("VAL:")) logicPart = logicPart.Substring(4);

                    var group = new AchievementConditionGroup { GroupName = groupNames[i] };
                    if (!string.IsNullOrWhiteSpace(logicPart)) ParseConditionsIntoGroup(group, logicPart);
                    conditionSet.Add(group);
                }
                for (int i = conditionSet.Count; i < 4; i++) conditionSet.Add(new AchievementConditionGroup { GroupName = groupNames[i] });
                return conditionSet;
            }

            if (separator == 'S') groupStrings = Regex.Split(trigger, @"(?<!0x)S", RegexOptions.IgnoreCase);
            else groupStrings = trigger.Split(separator);

            for (int i = 0; i < groupStrings.Length; i++)
            {
                var group = new AchievementConditionGroup { GroupName = (separator == 'S') ? ((i == 0) ? "Core" : $"Alt {i}") : $"Value Group {i + 1}" };
                ParseConditionsIntoGroup(group, groupStrings[i]);
                conditionSet.Add(group);
            }
            return conditionSet;
        }

        private static void ParseConditionsIntoGroup(AchievementConditionGroup group, string logic)
        {
            if (string.IsNullOrWhiteSpace(logic)) return;
            var conditionStrings = logic.Split('_');
            int conditionId = 1;
            foreach (var conditionString in conditionStrings)
            {
                if (!string.IsNullOrWhiteSpace(conditionString))
                {
                    try { var condition = ParseCondition(conditionString); condition.ID = conditionId++; group.Conditions.Add(condition); } catch { }
                }
            }
        }

        private static AchievementCondition ParseCondition(string conditionStr)
        {
            var condition = new AchievementCondition();
            string current = conditionStr;

            var hitsRegex = new Regex(@"\.(\d+)\.$");
            var match = hitsRegex.Match(current);
            if (match.Success)
            {
                if (uint.TryParse(match.Groups[1].Value, out uint hits)) condition.RequiredHits = hits;
                current = current.Substring(0, match.Index);
            }

            if (current.Length > 2 && current[1] == ':')
            {
                char flagChar = char.ToUpper(current[0]);
                var flagEntry = RaLogicSyntax.ReverseFlagMap.FirstOrDefault(x => x.Value == flagChar);

                if (!string.IsNullOrEmpty(flagEntry.Key))
                {
                    condition.Flag = flagEntry.Key;
                    current = current.Substring(2);
                }
            }

            string opStr = "";
            int opIndex = -1;

            // FIX: Sort operators by Length Descending to match strict operators (>=) before substrings (=)
            var sortedOps = RaLogicSyntax.ComparisonOperators
                .Where(op => op != RaLogicSyntax.NO_OPERATOR_TEXT)
                .OrderByDescending(op => op.Length)
                .ToList();

            foreach (var op in sortedOps)
            {
                int idx = current.IndexOf(op);
                if (idx != -1) { opStr = op; opIndex = idx; break; }
            }

            if (opIndex == -1)
            {
                // Apply same sort for Arithmetic operators safety
                var sortedArith = RaLogicSyntax.ArithmeticOperators
                    .Where(op => op != RaLogicSyntax.NO_OPERATOR_TEXT)
                    .OrderByDescending(op => op.Length)
                    .ToList();

                foreach (var op in sortedArith)
                {
                    int idx = current.IndexOf(op);
                    if (idx != -1) { opStr = op; opIndex = idx; break; }
                }
            }

            if (opIndex == -1) condition.LeftOperand = ParseOperand(current);
            else
            {
                condition.Operator = opStr;
                condition.LeftOperand = ParseOperand(current.Substring(0, opIndex));
                condition.RightOperand = ParseOperand(current.Substring(opIndex + opStr.Length));
            }
            return condition;
        }

        public static Operand ParseOperand(string operandStr)
        {
            var operand = new Operand();
            string current = operandStr;
            if (string.IsNullOrEmpty(current)) return operand;
            if (current.Equals("{recall}", StringComparison.OrdinalIgnoreCase)) { operand.Type = "Recall"; return operand; }

            if (current.Length > 0 && "dpb~".Contains(char.ToLower(current[0])))
            {
                switch (char.ToLower(current[0])) { case 'd': operand.Type = "Delta"; break; case 'p': operand.Type = "Prior"; break; case 'b': operand.Type = "BCD"; break; case '~': operand.Type = "Inverted"; break; }
                current = current.Length > 1 ? current.Substring(1).TrimStart() : "";
            }

            if (string.IsNullOrEmpty(current)) return operand;
            char firstChar = current[0];
            string remaining = current.Length > 1 ? current.Substring(1) : "";

            if (char.ToLower(firstChar) == 'f' && current.Length > 1 && RaLogicSyntax.FloatSizeMap.ContainsKey(char.ToUpper(remaining[0])))
            {
                if (string.IsNullOrEmpty(operand.Type)) operand.Type = "Mem";
                operand.Size = RaLogicSyntax.FloatSizeMap[char.ToUpper(remaining[0])];
                operand.Value = "0x" + (remaining.Length > 1 ? remaining.Substring(1).TrimStart() : "");
            }
            else if (char.ToLower(firstChar) == 'f') { operand.Type = "Float"; operand.Value = remaining.TrimStart(); }
            else if (char.IsLetter(firstChar) && RaLogicSyntax.ReverseSizeMap.ContainsValue(char.ToUpper(firstChar)) && remaining.TrimStart().StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(operand.Type)) operand.Type = "Mem";
                foreach (var kvp in RaLogicSyntax.ReverseSizeMap) { if (kvp.Value == char.ToUpper(firstChar)) { operand.Size = kvp.Key; break; } }
                operand.Value = remaining.TrimStart();
            }
            else if (current.TrimStart().StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(operand.Type)) operand.Type = "Mem";
                string addressPart = current.TrimStart().Substring(2).TrimStart();
                char sizeChar = ' ';
                if (addressPart.Length > 0 && char.IsLetter(addressPart[0]) && RaLogicSyntax.ReverseSizeMap.ContainsValue(char.ToUpper(addressPart[0])))
                {
                    sizeChar = addressPart[0];
                    addressPart = addressPart.Substring(1);
                }

                bool foundExplicit = false;
                foreach (var kvp in RaLogicSyntax.ReverseSizeMap)
                {
                    if (kvp.Value == char.ToUpper(sizeChar))
                    {
                        if (kvp.Key == "" && sizeChar == ' ') continue;
                        operand.Size = kvp.Key;
                        foundExplicit = true;
                        break;
                    }
                }
                if (!foundExplicit && sizeChar == ' ') operand.Size = "16-bit";

                operand.Value = "0x" + addressPart;
            }
            else
            {
                if (long.TryParse(current, NumberStyles.Integer, CultureInfo.InvariantCulture, out long val))
                {
                    if (string.IsNullOrEmpty(operand.Type)) operand.Type = "Value";
                    operand.Value = $"0x{val:X}";
                }
            }
            return operand;
        }
    }
}