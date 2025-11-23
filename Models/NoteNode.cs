using System.Collections.Generic;

namespace RALogicEditor.Models
{
    // Represents a node in the pointer tree structure (e.g., +0x4)
    public class NoteNode
    {
        public string Offset { get; set; } = "";
        public string Description { get; set; } = "";

        // Size in bytes covered by this note (default 1)
        public long Size { get; set; } = 1;

        public int RawLineIndex { get; set; }
        public int IndentLevel { get; set; }
        public string Content { get; set; } = "";
        public NoteNode? Parent { get; set; }
        public List<NoteNode> Children { get; set; } = new();

        public override string ToString()
        {
            // Formatting happens in the ListControl's Format event, 
            // but this serves as a default fallback.
            if (string.IsNullOrWhiteSpace(Description)) return Offset;
            return $"{Offset} | {Description}";
        }
    }
}