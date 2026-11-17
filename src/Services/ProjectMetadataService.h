#pragma once

#include <QString>
#include <QList>
#include "../Models/ProjectMetadata.h"
#include "../Models/LogicModels.h"

namespace RALogicEditor {

class ProjectMetadataService
{
private:
    QString _metadataPath = "";

public:
    ProjectMetadata CurrentData;

    void Load(const QString &gameId);
    void Save();
    void UpdateAssetGroups(int assetId, const QList<AchievementConditionGroup> &groups);
    void ApplyNamesToAsset(int assetId, QList<AchievementConditionGroup> &groups);
};

} // namespace RALogicEditor
