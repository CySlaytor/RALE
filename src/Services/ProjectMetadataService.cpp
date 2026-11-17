#include "ProjectMetadataService.h"
#include <QDir>
#include <QFile>
#include <QCoreApplication>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>
#include <algorithm>

namespace RALogicEditor {

void ProjectMetadataService::Load(const QString &gameId)
{
    QString folder = QDir(QCoreApplication::applicationDirPath()).filePath("Metadata");
    if (!QDir(folder).exists()) QDir().mkpath(folder);

    _metadataPath = QDir(folder).filePath(QString("%1.json").arg(gameId));

    if (QFile::exists(_metadataPath))
    {
        QFile file(_metadataPath);
        if (file.open(QIODevice::ReadOnly | QIODevice::Text))
        {
            QByteArray json = file.readAll();
            file.close();
            auto doc = QJsonDocument::fromJson(json);
            if (doc.isObject())
            {
                CurrentData.AssetGroupNames.clear();
                auto obj = doc.object();
                auto assetObj = obj.value("AssetGroupNames").toObject();
                for (auto it = assetObj.begin(); it != assetObj.end(); ++it)
                {
                    bool ok = false;
                    int id = it.key().toInt(&ok);
                    if (ok && it.value().isArray())
                    {
                        QStringList names;
                        for (const auto &val : it.value().toArray())
                            names.append(val.toString());
                        CurrentData.AssetGroupNames[id] = names;
                    }
                }
                return;
            }
        }
        CurrentData = ProjectMetadata();
    }
    else
    {
        CurrentData = ProjectMetadata();
    }
}

void ProjectMetadataService::Save()
{
    if (_metadataPath.isEmpty()) return;
    try
    {
        QJsonObject root;
        QJsonObject assetObj;
        for (auto it = CurrentData.AssetGroupNames.cbegin(); it != CurrentData.AssetGroupNames.cend(); ++it)
        {
            QJsonArray arr;
            for (const auto &name : it.value())
                arr.append(name);
            assetObj.insert(QString::number(it.key()), arr);
        }
        root.insert("AssetGroupNames", assetObj);

        QFile file(_metadataPath);
        if (file.open(QIODevice::WriteOnly | QIODevice::Text))
        {
            QJsonDocument doc(root);
            file.write(doc.toJson(QJsonDocument::Indented));
            file.close();
        }
    }
    catch (...) {}
}

void ProjectMetadataService::UpdateAssetGroups(int assetId, const QList<AchievementConditionGroup> &groups)
{
    QStringList names;
    for (const auto &g : groups) names.append(g.GroupName);
    CurrentData.AssetGroupNames[assetId] = names;
}

void ProjectMetadataService::ApplyNamesToAsset(int assetId, QList<AchievementConditionGroup> &groups)
{
    if (CurrentData.AssetGroupNames.contains(assetId))
    {
        const QStringList &names = CurrentData.AssetGroupNames[assetId];
        // We only apply names if counts match or reasonable heuristic
        int count = std::min(groups.size(), names.size());
        for (int i = 0; i < count; i++)
        {
            groups[i].GroupName = names[i];
        }
    }
}

} // namespace RALogicEditor
