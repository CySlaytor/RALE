using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System.Collections.Generic;

namespace RALogicEditor.Services
{
    public interface ILogicValidator
    {
        // Updated to accept notesLookup and rangeCache for documentation checks
        List<ValidationIssue> ValidateAsset(
            List<AchievementConditionGroup> groups,
            IAssetItem? asset,
            Dictionary<string, List<NoteNode>>? notesLookup = null,
            List<(long Start, long End, NoteNode Node)>? rangeCache = null);
    }
}