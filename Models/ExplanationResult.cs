using System.Collections.Generic;

namespace RALogicEditor.Models
{
    public class ExplanationResult
    {
        public string MainText { get; set; } = "";
        public Dictionary<string, string> DetailedInfo { get; set; } = new Dictionary<string, string>();
    }
}