#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RALogicEditor.Models
{
    public class Operand
    {
        public string Type { get; set; } = "";
        public string Size { get; set; } = "";
        public string Value { get; set; } = "";

        public Operand Clone() => new Operand
        {
            Type = this.Type,
            Size = this.Size,
            Value = this.Value
        };
    }

    public class AchievementCondition
    {
        public int ID { get; set; }
        public string Flag { get; set; } = "";
        public Operand LeftOperand { get; set; } = new Operand();
        public string Operator { get; set; } = "";
        public Operand RightOperand { get; set; } = new Operand();
        public uint RequiredHits { get; set; }

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

    public class AchievementConditionGroup
    {
        public string GroupName { get; set; } = "";
        public List<AchievementCondition> Conditions { get; set; } = new List<AchievementCondition>();
    }

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