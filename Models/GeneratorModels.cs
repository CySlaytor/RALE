using System.Collections.Generic;

namespace RALogicEditor.Models
{
    // Models for the logic generator functionality
    public class GeneratorDefinition
    {
        public int Copies { get; set; }
        public List<GeneratorRule> Rules { get; set; } = new();
    }

    public class GeneratorRule
    {
        public string SourceAddress { get; set; } = "";
        public int Stride { get; set; }
    }
}