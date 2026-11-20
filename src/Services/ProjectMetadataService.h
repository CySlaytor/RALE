#pragma once

#include "../Models/LogicModels.h"
#include "../Models/ProjectMetadata.h"

#include <QList>
#include <QString>

namespace RALE {

class ProjectMetadataService {
  private:
    QString _metadataPath = "";

  public:
    ProjectMetadata CurrentData;

    void Load(const QString& gameId);
    void Save();
    void UpdateAssetGroups(int assetId, const QList<AchievementConditionGroup>& groups);
    void ApplyNamesToAsset(int assetId, QList<AchievementConditionGroup>& groups);
};

} // namespace RALE
