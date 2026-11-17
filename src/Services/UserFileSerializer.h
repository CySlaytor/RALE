#pragma once

#include <QString>
#include <QList>
#include <memory>
#include "../Models/AssetModels.h"
#include "../Parsers/UserTextParser.h"

namespace RALogicEditor {

class ProjectRepository;

class UserFileSerializer
{
public:
    QString Serialize(
        const QString &gameTitle,
        const UserTextParser::UserData *localData,
        const QList<std::shared_ptr<RaAchievement>> &mergedAchievements,
        const QList<std::shared_ptr<RaLeaderboard>> &mergedLeaderboards,
        ProjectRepository &repo);
};

} // namespace RALogicEditor
