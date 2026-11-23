#pragma once

#include "LogicModels.h"

#include <QString>
#include <QStringList>

#include <memory>
#include <optional>

namespace RALE {

class IAssetItem;

enum class FeedbackSeverity { Pass = 0, None = 0, Info = 1, Warning = 2, Fail = 3, Error = 4 };

enum class SuggestedFixType {
    None = 0,
    InvertChain,
    ClearResetHitCount,
    RemoveUselessAndNext,
    ApplyTitleCase,
    NormalizeSmartQuotes
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

    SuggestedFixType FixType = SuggestedFixType::None;
    QString FixPreviewText = "";
    QStringList FixDetails;

    // The specific condition object this issue relates to (for highlighting in the grid)
    std::optional<AchievementCondition> Target;

    // The asset this issue belongs to (for global list navigation)
    std::shared_ptr<IAssetItem> Asset;

    bool HasSuggestedFix() const { return FixType != SuggestedFixType::None; }

    ValidationIssue() = default;

    ValidationIssue(FeedbackSeverity severity, const QString& title, const QString& description,
                    const std::optional<AchievementCondition>& target = std::nullopt, const QString& wikiUrl = "",
                    std::shared_ptr<IAssetItem> asset = nullptr, const QString& category = "Logic", const QString& segmentName = "",
                    int groupIndex = -1, const QString& targetField = "", const QString& noteAddress = "",
                    SuggestedFixType fixType = SuggestedFixType::None, const QString& fixPreviewText = "",
                    const QStringList& fixDetails = {})
        : Severity(severity), Title(title), Description(description), WikiUrl(wikiUrl), Category(category), SegmentName(segmentName),
          GroupIndex(groupIndex), TargetField(targetField), NoteAddress(noteAddress), FixType(fixType), FixPreviewText(fixPreviewText),
          FixDetails(fixDetails), Target(target), Asset(asset) {}
};

} // namespace RALE
