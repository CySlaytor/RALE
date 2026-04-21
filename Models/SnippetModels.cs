using System.Text.Json.Serialization;

namespace RALogicEditor.Models
{
    public class Snippet
    {
        public string Name { get; set; } = "";
        public string Logic { get; set; } = "";

        // Legacy fallback to gracefully support loading older snippets 
        // that were saved before the model property was renamed.
        [JsonPropertyName("LogicCode")]
        public string LogicCode
        {
            get => Logic;
            set => Logic = value;
        }
    }
}