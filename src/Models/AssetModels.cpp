#include "AssetModels.h"

namespace RALE {

QString RaAchievement::toString() const
{
    return QString("%1 - %2").arg(ID).arg(Title);
}

std::shared_ptr<IAssetItem> RaAchievement::clone(bool newId) const
{
    return cloneAchievement(newId);
}

std::shared_ptr<RaAchievement> RaAchievement::cloneAchievement(bool newId) const
{
    auto copy = std::make_shared<RaAchievement>();
    copy->ID = newId ? 0 : this->ID;
    copy->Title = this->Title;
    copy->Description = this->Description;
    copy->Points = this->Points;
    copy->Author = this->Author;
    copy->BadgeName = this->BadgeName;
    copy->Type = this->Type;
    copy->Flags = this->Flags;
    copy->Logic = this->Logic;
    copy->Category = this->Category;
    copy->State = this->State;
    return copy;
}

RaLeaderboard::RaLeaderboard()
{
    Type = "Leaderboard";
    Category = "Local"; // Default
    State = AssetState::Synced;
}

QString RaLeaderboard::toString() const
{
    return QString("L%1 - %2").arg(ID).arg(Title);
}

std::shared_ptr<IAssetItem> RaLeaderboard::clone(bool newId) const
{
    return cloneLeaderboard(newId);
}

std::shared_ptr<RaLeaderboard> RaLeaderboard::cloneLeaderboard(bool newId) const
{
    auto copy = std::make_shared<RaLeaderboard>();
    copy->ID = newId ? 0 : this->ID;
    copy->Title = this->Title;
    copy->Description = this->Description;
    copy->Format = this->Format;
    copy->LowerIsBetter = this->LowerIsBetter;
    copy->Hidden = this->Hidden;
    copy->Logic = this->Logic;
    copy->Category = this->Category;
    copy->State = this->State;
    return copy;
}

} // namespace RALE
