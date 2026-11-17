#pragma once

#include <QString>
#include <memory>
#include <optional>
#include "LogicModels.h"

namespace RALogicEditor {

class IAssetItem;

enum class FeedbackSeverity
{
    None = 0,
    Info = 1,
    Warning = 2,
    Error = 3
};

class ValidationIssue
{
public:
    FeedbackSeverity Severity = FeedbackSeverity::None;
    QString Title = "";
    QString Description = "";
    QString WikiUrl = "";

    // The specific condition object this issue relates to (for highlighting in the grid)
    std::optional<AchievementCondition> Target;

    // The asset this issue belongs to (for global list navigation)
    std::shared_ptr<IAssetItem> Asset;

    ValidationIssue() = default;

    ValidationIssue(FeedbackSeverity severity,
                    const QString &title,
                    const QString &description,
                    const std::optional<AchievementCondition> &target = std::nullopt,
                    const QString &wikiUrl = "",
                    std::shared_ptr<IAssetItem> asset = nullptr)
        : Severity(severity),
          Title(title),
          Description(description),
          WikiUrl(wikiUrl),
          Target(target),
          Asset(asset)
    {
    }
};

} // namespace RALogicEditor
