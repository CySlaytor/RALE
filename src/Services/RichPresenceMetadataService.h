#pragma once

#include <QString>
#include <optional>

namespace RALogicEditor {

// Persists backup metadata for Rich Presence to allow reverting changes
class RichPresenceMetadataService
{
public:
    class RichPresenceBackup
    {
    public:
        QString OriginalScript = "";
        QString Timestamp = "";
    };

    std::optional<RichPresenceBackup> CurrentBackup;

    void Load(const QString &gameId);
    void SaveBackup(const QString &script);

private:
    QString _path = "";
};

} // namespace RALogicEditor
