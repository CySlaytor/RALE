#pragma once

#include <QString>
#include <QStringList>
#include <QMap>
#include <QList>
#include "../Models/LogicModels.h"

namespace RALogicEditor {

// Interface to abstract differences between Achievement logic (Core/Alts) 
// and Leaderboard logic (Start/Cancel/Submit/Value)
class IAssetStrategy
{
public:
    virtual ~IAssetStrategy() = default;

    virtual bool isLeaderboard() const = 0;

    // Returns the list of segment names (e.g. "Core" for Ach, "Start", "Cancel"... for LB)
    virtual QStringList GetSegmentNames() const = 0;

    // Parses the raw logic string into a dictionary of segment names -> condition groups
    virtual QMap<QString, QList<AchievementConditionGroup>> ParseLogic(const QString &logic) const = 0;

    // Rebuilds the full logic string from the segments
    virtual QString BuildLogic(const QMap<QString, QList<AchievementConditionGroup>> &segments) const = 0;
};

class AchievementStrategy : public IAssetStrategy
{
public:
    bool isLeaderboard() const override { return false; }

    QStringList GetSegmentNames() const override { return { "Core" }; }

    QMap<QString, QList<AchievementConditionGroup>> ParseLogic(const QString &logic) const override;

    QString BuildLogic(const QMap<QString, QList<AchievementConditionGroup>> &segments) const override;
};

class LeaderboardStrategy : public IAssetStrategy
{
public:
    bool isLeaderboard() const override { return true; }

    QStringList GetSegmentNames() const override { return { "Start", "Cancel", "Submit", "Value" }; }

    QMap<QString, QList<AchievementConditionGroup>> ParseLogic(const QString &logic) const override;

    QString BuildLogic(const QMap<QString, QList<AchievementConditionGroup>> &segments) const override;
};

} // namespace RALogicEditor
