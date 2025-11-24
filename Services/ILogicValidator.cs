using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System.Collections.Generic;

namespace RALogicEditor.Services
{
    public interface ILogicValidator
    {
        List<ValidationIssue> ValidateAsset(List<AchievementConditionGroup> groups, IAssetItem? asset = null);
    }
}