#pragma once

#include "../Models/AssetModels.h"
#include "../Parsers/UserTextParser.h"

#include <QList>
#include <QString>

#include <memory>

namespace RALE {

class ProjectRepository;

class UserFileSerializer {
  public:
    QString Serialize(const QString& gameTitle, const UserTextParser::UserData* localData,
                      const QList<std::shared_ptr<RaAchievement>>& mergedAchievements,
                      const QList<std::shared_ptr<RaLeaderboard>>& mergedLeaderboards, ProjectRepository& repo);
};

} // namespace RALE
