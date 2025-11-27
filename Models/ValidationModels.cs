#nullable enable
using RALogicEditor.Models;

namespace RALogicEditor.Definitions
{
    public enum FeedbackSeverity
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    public class ValidationIssue
    {
        public FeedbackSeverity Severity { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string WikiUrl { get; set; } = "";

        // The specific condition object this issue relates to (for highlighting in the grid)
        public AchievementCondition? Target { get; set; }

        // The asset this issue belongs to (for global list navigation)
        public IAssetItem? Asset { get; set; }

        // NEW: Trace log for debugging
        public string DebugLog { get; set; } = "";

        public ValidationIssue(FeedbackSeverity severity, string title, string description, AchievementCondition? target = null, string wikiUrl = "", IAssetItem? asset = null)
        {
            Severity = severity;
            Title = title;
            Description = description;
            Target = target;
            WikiUrl = wikiUrl;
            Asset = asset;
        }
    }
}