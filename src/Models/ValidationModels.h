#pragma once

#include "LogicModels.h"

#include <QList>
#include <QString>

#include <memory>
#include <optional>

namespace RALE {

class IAssetItem;

enum class FeedbackSeverity { Pass = 0, None = 0, Info = 1, Warning = 2, Fail = 3, Error = 4 };

struct SuggestedLogicFix {
    int StartConditionIndex = -1;
    int ReplaceCount = 0;
    QList<AchievementCondition> ReplacementConditions;

    bool IsValid() const { return StartConditionIndex >= 0 && ReplaceCount > 0 && !ReplacementConditions.isEmpty(); }
};

class ValidationIssue {
  public:
    FeedbackSeverity Severity = FeedbackSeverity::None;
    QString Title = "";
    QString Description = "";
    QString WikiUrl = "";
    QString Category = "Logic";
    QString SegmentName = "";
    int GroupIndex = -1;
    QString TargetField = "";
    QString NoteAddress = "";

    // The specific condition object this issue relates to (for highlighting in the grid)
    std::optional<AchievementCondition> Target;

    // Optional structured replacement for one-click visual quick fixes
    std::optional<SuggestedLogicFix> SuggestedFix;

    // The asset this issue belongs to (for global list navigation)
    std::shared_ptr<IAssetItem> Asset;

    ValidationIssue() = default;

    ValidationIssue(FeedbackSeverity severity, const QString& title, const QString& description,
                    const std::optional<AchievementCondition>& target = std::nullopt, const QString& wikiUrl = "",
                    std::shared_ptr<IAssetItem> asset = nullptr, const QString& category = "Logic", const QString& segmentName = "",
                    int groupIndex = -1, const QString& targetField = "", const QString& noteAddress = "",
                    const std::optional<SuggestedLogicFix>& suggestedFix = std::nullopt)
        : Severity(severity), Title(title), Description(description), WikiUrl(wikiUrl), Category(category), SegmentName(segmentName),
          GroupIndex(groupIndex), TargetField(targetField), NoteAddress(noteAddress), Target(target), SuggestedFix(suggestedFix),
          Asset(asset) {}
};

} // namespace RALE
