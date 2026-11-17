#pragma once

#include <QString>
#include <QStringList>
#include <QMap>

namespace RALogicEditor {

class ProjectMetadata
{
public:
    // Key: Asset ID, Value: List of Group Names (Core, Alt 1 (USA)...)
    QMap<int, QStringList> AssetGroupNames;
};

} // namespace RALogicEditor
