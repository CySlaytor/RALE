using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer; // Added missing reference
using System.Collections.Generic;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Handles Pointer Chain Setup (Add Address)
    public class PointerCleanupProcessor : ILogicProcessor
    {
        public int Priority => 5; // Very high priority to catch AddAddress before Standard
        public bool CanProcess(List<AchievementCondition> conditions, int index) => conditions[index].Flag == "Add Address";

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            // We consume this line but return no text.
            return new ProcessorResult { Text = "", Category = ExplanationCategory.Context };
        }
    }
}