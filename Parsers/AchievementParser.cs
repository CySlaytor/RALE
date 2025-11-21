using RALogicEditor.Models;
using RARPEditor.Models;
using System.Globalization;
using System.Text.RegularExpressions;

#nullable enable

namespace RALogicEditor.Parsers
{
    public static class AchievementParser
    {
        private static readonly Dictionary<char, string> FlagMap = new()
        {
            {'R', "Reset If"}, {'P', "Pause If"}, {'A', "Add Source"},
            {'B', "Sub Source"}, {'C', "Add Hits"}, {'D', "Sub Hits"},
            {'I', "Add Address"}, {'N', "AndNext"}, {'O', "OrNext"},
            {'M', "Measured"}, {'Q', "MeasuredIf"}, {'G', "Measured%"},
            {'T', "Trigger"}, {'Z', "ResetNextIf"}, {'K', "Remember"}
        };

        private static readonly Dictionary<char, string> SizeMap = new()
        {
            {' ', "16-bit"},
            {'H', "8-bit"}, {'X', "32-bit"}, {'W', "24-bit"},
            {'M', "Bit0"}, {'N', "Bit1"}, {'O', "Bit2"}, {'P', "Bit3"},
            {'Q', "Bit4"}, {'R', "Bit5"}, {'S', "Bit6"}, {'T', "Bit7"},
            {'L', "Lower4"}, {'U', "Upper4"}, {'K', "BitCount"},
            {'G', "32-bit BE"}, {'I', "16-bit BE"}, {'J', "24-bit BE"}
        };

        private static readonly Dictionary<char, string> FloatSizeMap = new()
        {
            {'F', "Float"}, {'B', "Float BE"}, {'H', "Double32"},
            {'I', "Double32 BE"}, {'M', "MBF32"}, {'L', "MBF32 LE"}
        };

        public static List<AchievementConditionGroup> ParseAchievementTrigger(string trigger, char separator = 'S', bool isLeaderboard = false)
        {
            var conditionSet = new List<AchievementConditionGroup>();
            if (string.IsNullOrWhiteSpace(trigger)) return conditionSet;

            string[] groupStrings;

            if (isLeaderboard)
            {
                // Robustly handle leaderboard segments
                groupStrings = Regex.Split(trigger, "::");
                string[] groupNames = { "START", "CANCEL", "SUBMIT", "VALUE" };

                // Ensure we don't exceed 4 segments, but process what we have
                int count = Math.Min(groupStrings.Length, groupNames.Length);

                for (int i = 0; i < count; i++)
                {
                    string logicPart = groupStrings[i] ?? "";

                    if (logicPart.StartsWith("STA:")) logicPart = logicPart.Substring(4);
                    else if (logicPart.StartsWith("CAN:")) logicPart = logicPart.Substring(4);
                    else if (logicPart.StartsWith("SUB:")) logicPart = logicPart.Substring(4);
                    else if (logicPart.StartsWith("VAL:")) logicPart = logicPart.Substring(4);

                    var group = new AchievementConditionGroup
                    {
                        GroupName = groupNames[i]
                    };

                    if (!string.IsNullOrWhiteSpace(logicPart))
                    {
                        ParseConditionsIntoGroup(group, logicPart);
                    }

                    conditionSet.Add(group);
                }

                // Fill missing segments if input was incomplete
                for (int i = conditionSet.Count; i < 4; i++)
                {
                    conditionSet.Add(new AchievementConditionGroup { GroupName = groupNames[i] });
                }

                return conditionSet;
            }

            if (separator == 'S')
            {
                // Look for 'S' that isn't preceded by '0x' (to avoid hex confusion, though strictly logic strings use upper case S usually)
                groupStrings = Regex.Split(trigger, @"(?<!0x)S", RegexOptions.IgnoreCase);
            }
            else
            {
                groupStrings = trigger.Split(separator);
            }

            for (int i = 0; i < groupStrings.Length; i++)
            {
                var group = new AchievementConditionGroup
                {
                    GroupName = (separator == 'S')
                        ? ((i == 0) ? "Core" : $"Alt {i}")
                        : $"Value Group {i + 1}"
                };

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
                    try
                    {
                        var condition = ParseCondition(conditionString);
                        condition.ID = conditionId++;
                        group.Conditions.Add(condition);
                    }
                    catch
                    {
                        // Swallow individual condition parse errors to allow partial loading
                        // In a production app, we might log this to a debug console
                    }
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
                if (uint.TryParse(match.Groups[1].Value, out uint hits))
                {
                    condition.RequiredHits = hits;
                }
                current = current.Substring(0, match.Index);
            }

            if (current.Length > 2 && current[1] == ':' && FlagMap.TryGetValue(char.ToUpper(current[0]), out var flag))
            {
                condition.Flag = flag;
                current = current.Substring(2);
            }

            string[] operators = { "!=", "<=", ">=", "=", "<", ">", "*", "/", "%", "+", "-", "&", "^" };
            string opStr = "";
            int opIndex = -1;

            foreach (var op in operators)
            {
                opIndex = current.IndexOf(op);
                if (opIndex != -1)
                {
                    opStr = op;
                    break;
                }
            }

            if (opIndex == -1)
            {
                condition.LeftOperand = ParseOperand(current);
            }
            else
            {
                condition.Operator = opStr;
                string left = current.Substring(0, opIndex);
                string right = current.Substring(opIndex + opStr.Length);
                condition.LeftOperand = ParseOperand(left);
                condition.RightOperand = ParseOperand(right);
            }

            return condition;
        }

        public static Operand ParseOperand(string operandStr)
        {
            var operand = new Operand();
            string current = operandStr;

            if (string.IsNullOrEmpty(current)) return operand;

            if (current.Equals("{recall}", StringComparison.OrdinalIgnoreCase))
            {
                operand.Type = "Recall";
                return operand;
            }

            // Check for prefixes d/p/b/~
            if (current.Length > 0 && "dpb~".Contains(char.ToLower(current[0])))
            {
                switch (char.ToLower(current[0]))
                {
                    case 'd': operand.Type = "Delta"; break;
                    case 'p': operand.Type = "Prior"; break;
                    case 'b': operand.Type = "BCD"; break;
                    case '~': operand.Type = "Inverted"; break;
                }
                // Safety check for index
                if (current.Length > 1)
                    current = current.Substring(1).TrimStart();
                else
                    current = "";
            }

            if (string.IsNullOrEmpty(current)) return operand;

            char firstChar = current[0];
            string remaining = current.Length > 1 ? current.Substring(1) : "";

            // Float Parsing
            if (char.ToLower(firstChar) == 'f' && current.Length > 1 && FloatSizeMap.ContainsKey(char.ToUpper(remaining[0])))
            {
                if (string.IsNullOrEmpty(operand.Type)) operand.Type = "Mem";
                operand.Size = FloatSizeMap[char.ToUpper(remaining[0])];
                operand.Value = "0x" + (remaining.Length > 1 ? remaining.Substring(1).TrimStart() : "");
            }
            else if (char.ToLower(firstChar) == 'f')
            {
                operand.Type = "Float";
                operand.Value = remaining.TrimStart();
            }
            // Explicit Memory Size Parsing (e.g. H0x1234)
            else if (char.IsLetter(firstChar) && SizeMap.ContainsKey(char.ToUpper(firstChar)) && remaining.TrimStart().StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(operand.Type)) operand.Type = "Mem";
                operand.Size = SizeMap[char.ToUpper(firstChar)];
                operand.Value = remaining.TrimStart();
            }
            // Default Hex Parsing (0x...)
            else if (current.TrimStart().StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(operand.Type)) operand.Type = "Mem";
                string addressPart = current.TrimStart().Substring(2).TrimStart();

                // Logic to detect legacy format or strict format
                char sizeChar = ' ';
                if (addressPart.Length > 0 && char.IsLetter(addressPart[0]) && SizeMap.ContainsKey(char.ToUpper(addressPart[0])))
                {
                    sizeChar = addressPart[0];
                    addressPart = addressPart.Substring(1);
                }

                operand.Size = SizeMap[char.ToUpper(sizeChar)];
                operand.Value = "0x" + addressPart;
            }
            // Value (Decimal)
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