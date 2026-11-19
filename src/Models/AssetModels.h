#pragma once

#include <QString>
#include <QList>
#include <memory>

namespace RALE {

enum class AssetState
{
    Synced,
    Modified,
    Local
};

class IAssetItem
{
public:
    virtual ~IAssetItem() = default;

    int ID = 0;
    QString Title = "";
    QString Description = "";
    QString Logic = "";
    QString Type = "";
    QString Category = "Local"; // Tracks "Core", "Unofficial", or "Local"
    AssetState State = AssetState::Synced;

    virtual bool isLeaderboard() const = 0;
    virtual std::shared_ptr<IAssetItem> clone(bool newId = true) const = 0;
    virtual QString toString() const = 0;
};

class RaAchievement;
class RaLeaderboard;

class RaSet
{
public:
    QString Title = "";
    QString Type = ""; // "core", "unofficial"
    int AchievementSetId = 0;
    int GameId = 0;
    QString ImageIconUrl = "";
    QList<std::shared_ptr<RaAchievement>> Achievements;
    QList<std::shared_ptr<RaLeaderboard>> Leaderboards;
};

class RaRoot
{
public:
    bool Success = false;
    int GameId = 0;
    QString Title = "";
    QString ImageIconUrl = "";
    int RichPresenceGameId = 0;
    QString RichPresencePatch = "";
    int ConsoleId = 0;
    QList<RaSet> Sets;
};

class RaAchievement : public IAssetItem
{
public:
    int Points = 0;
    QString Author = "Unknown";
    QString BadgeName = "00000";

    // Used to determine Category: 3 = Core, 5 = Unofficial
    int Flags = 0;

    bool isLeaderboard() const override { return false; }

    QString toString() const override;

    std::shared_ptr<IAssetItem> clone(bool newId = true) const override;
    std::shared_ptr<RaAchievement> cloneAchievement(bool newId = true) const;

    bool operator==(const RaAchievement &other) const
    {
        return ID == other.ID &&
               Title == other.Title &&
               Description == other.Description &&
               Points == other.Points &&
               Author == other.Author &&
               BadgeName == other.BadgeName &&
               Type == other.Type &&
               Flags == other.Flags &&
               Logic == other.Logic &&
               Category == other.Category &&
               State == other.State;
    }
    bool operator!=(const RaAchievement &other) const { return !(*this == other); }
};

class RaLeaderboard : public IAssetItem
{
public:
    RaLeaderboard();

    QString Format = "VALUE";
    bool LowerIsBetter = false; // Fixed JSON breaking change (int -> bool)
    bool Hidden = false;

    bool isLeaderboard() const override { return true; }

    QString toString() const override;

    std::shared_ptr<IAssetItem> clone(bool newId = true) const override;
    std::shared_ptr<RaLeaderboard> cloneLeaderboard(bool newId = true) const;

    bool operator==(const RaLeaderboard &other) const
    {
        return ID == other.ID &&
               Title == other.Title &&
               Description == other.Description &&
               Format == other.Format &&
               LowerIsBetter == other.LowerIsBetter &&
               Hidden == other.Hidden &&
               Logic == other.Logic &&
               Category == other.Category &&
               State == other.State;
    }
    bool operator!=(const RaLeaderboard &other) const { return !(*this == other); }
};

} // namespace RALE
