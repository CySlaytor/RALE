#nullable enable
using System.Text.Json.Serialization;

namespace RARPEditor.Models
{
    // Represents a single part of a condition (e.g., the "Mem 0x1234" part or the "Value 5" part).
    public class Operand
    {
        public string Type { get; set; } = ""; // Mem, Value, Float, Delta, etc.
        public string Size { get; set; } = ""; // 8-bit, 16-bit, Bit0, etc.
        public string Value { get; set; } = ""; // The actual hex or decimal string

        public Operand Clone() => new Operand
        {
            Type = this.Type,
            Size = this.Size,
            Value = this.Value
        };
    }

    // Represents a single line of logic (e.g., ResetIf Mem 0x1234 = 1).
    public class AchievementCondition
    {
        public int ID { get; set; } // Used for display order in the grid
        public string Flag { get; set; } = ""; // ResetIf, PauseIf, AddSource, etc.
        public Operand LeftOperand { get; set; } = new Operand();
        public string Operator { get; set; } = ""; // =, >, <, !=
        public Operand RightOperand { get; set; } = new Operand();
        public uint RequiredHits { get; set; } // (10)

        public AchievementCondition Clone()
        {
            return new AchievementCondition
            {
                ID = this.ID,
                Flag = this.Flag,
                LeftOperand = this.LeftOperand.Clone(),
                Operator = this.Operator,
                RightOperand = this.RightOperand.Clone(),
                RequiredHits = this.RequiredHits
            };
        }
    }

    // A container for a list of conditions. 
    // GroupName will be "Core", "Alt 1", "Alt 2", etc.
    public class AchievementConditionGroup
    {
        public string GroupName { get; set; } = "";
        public List<AchievementCondition> Conditions { get; set; } = new List<AchievementCondition>();
    }

    // Represents a raw Code Note loaded from the JSON file.
    // Used by the CodeNoteEditorControl.
    public class CodeNote
    {
        [JsonPropertyName("User")]
        public string User { get; set; } = "";

        [JsonPropertyName("Address")]
        public string Address { get; set; } = "";

        [JsonPropertyName("Note")]
        public string Note { get; set; } = "";
    }
}
#nullable disable