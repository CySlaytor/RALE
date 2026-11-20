#pragma once

#include "../Models/AssetModels.h"
#include "../Models/LogicModels.h"

#include <QList>
#include <QString>
#include <QStringList>

#include <memory>

namespace RALE {

class UserTextParser {
  public:
    class UserData {
      public:
        QList<std::shared_ptr<RaAchievement>> Achievements;
        QList<std::shared_ptr<RaLeaderboard>> Leaderboards;
        QList<CodeNote> Notes;

        bool operator==(const UserData& other) const;
        bool operator!=(const UserData& other) const { return !(*this == other); }
    };

    static UserData ParseUserFile(const QString& filepath);

  private:
    static QStringList ParseColonLine(const QString& line);
    static QString CleanValue(const QString& val);
};

} // namespace RALE
