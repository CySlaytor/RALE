#include "AssetStrategies.h"

#include "../Parsers/AchievementParser.h"
#include "../Utilities/LogicFormatter.h"

namespace RALE {

QMap<QString, QList<AchievementConditionGroup>> AchievementStrategy::ParseLogic(const QString& logic) const {
    // Achievements are parsed as a single list of groups (Core + Alts)
    // We map this entire list to the "Core" segment key for consistency in the ViewModel
    auto groups = AchievementParser::ParseAchievementTrigger(logic, 'S', false);

    // Ensure at least one group exists
    if (groups.isEmpty()) {
        AchievementConditionGroup coreGroup;
        coreGroup.GroupName = "Core";
        groups.append(coreGroup);
    }

    QMap<QString, QList<AchievementConditionGroup>> result;
    result.insert("Core", groups);
    return result;
}

QString AchievementStrategy::BuildLogic(const QMap<QString, QList<AchievementConditionGroup>>& segments) const {
    if (!segments.contains("Core"))
        return "";
    const auto& groups = segments.value("Core");

    // Rebuild string using 'S' separator
    return LogicFormatter::GroupsToString(groups);
}

QMap<QString, QList<AchievementConditionGroup>> LeaderboardStrategy::ParseLogic(const QString& logic) const {
    QMap<QString, QList<AchievementConditionGroup>> segments;
    segments.insert("Start", {});
    segments.insert("Cancel", {});
    segments.insert("Submit", {});
    segments.insert("Value", {});

    if (logic.isEmpty()) {
        // Ensure default empty groups
        for (auto it = segments.begin(); it != segments.end(); ++it) {
            AchievementConditionGroup g;
            g.GroupName = "Core";
            it.value().append(g);
        }
        return segments;
    }

    QStringList parts = logic.split("::");

    for (const QString& part : parts) {
        QString typeKey = "";
        QString cleanLogic = part;

        if (part.startsWith("STA:")) {
            typeKey = "Start";
            cleanLogic = part.mid(4);
        } else if (part.startsWith("CAN:")) {
            typeKey = "Cancel";
            cleanLogic = part.mid(4);
        } else if (part.startsWith("SUB:")) {
            typeKey = "Submit";
            cleanLogic = part.mid(4);
        } else if (part.startsWith("VAL:")) {
            typeKey = "Value";
            cleanLogic = part.mid(4);
        }

        if (!typeKey.isEmpty()) {
            auto groups = AchievementParser::ParseAchievementTrigger(cleanLogic, 'S', false);
            if (groups.isEmpty()) {
                AchievementConditionGroup g;
                g.GroupName = "Core";
                groups.append(g);
            }
            segments[typeKey] = groups;
        }
    }

    // Ensure defaults for missing segments
    for (auto it = segments.begin(); it != segments.end(); ++it) {
        if (it.value().isEmpty()) {
            AchievementConditionGroup g;
            g.GroupName = "Core";
            it.value().append(g);
        }
    }

    return segments;
}

QString LeaderboardStrategy::BuildLogic(const QMap<QString, QList<AchievementConditionGroup>>& segments) const {
    QString sb;

    // Helper to build a segment string
    sb += "STA:" + LogicFormatter::GroupsToString(segments.value("Start")) + "::";
    sb += "CAN:" + LogicFormatter::GroupsToString(segments.value("Cancel")) + "::";
    sb += "SUB:" + LogicFormatter::GroupsToString(segments.value("Submit")) + "::";
    sb += "VAL:" + LogicFormatter::GroupsToString(segments.value("Value"));

    return sb;
}

} // namespace RALE
