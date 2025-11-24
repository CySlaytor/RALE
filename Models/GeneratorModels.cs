using System.Collections.Generic;

namespace RALogicEditor.Models
{
    // Models for the logic generator functionality
    public class GeneratorDefinition
    {
        public int Copies { get; set; }

        // New: Determines if we create new Alt groups (False) or append to the current group (True)
        public bool IsArray { get; set; }

        // New: Tracks how many conditions formed the original "Template".
        // This allows the ViewModel to clear previous generations before applying new ones.
        public int TemplateConditionCount { get; set; }

        public List<GeneratorRule> Rules { get; set; } = new();
    }

    public class GeneratorRule
    {
        public string SourceAddress { get; set; } = "";
        public int Stride { get; set; }
    }
}