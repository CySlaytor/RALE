using RALogicEditor.Models;
using System.Collections.Generic;

namespace RALogicEditor.Parsers
{
    // Interface for parsing Code Notes into tree structures and generating logic.
    // Enables Dependency Injection for ViewModels.
    public interface IPointerTreeParser
    {
        List<NoteNode> ParseNoteText(string noteText);
        (string Logic, string Type, string Size) GeneratePointerLogic(CodeNote baseNote, NoteNode selectedNode);
        List<long> GetOffsetsFromNode(NoteNode node);
    }
}