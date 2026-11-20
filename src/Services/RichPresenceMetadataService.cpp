#include "RichPresenceMetadataService.h"

#include <QCoreApplication>
#include <QDateTime>
#include <QDir>
#include <QFile>
#include <QJsonDocument>
#include <QJsonObject>
#include <QLocale>

namespace RALE {

void RichPresenceMetadataService::Load(const QString& gameId) {
    QString folder = QDir(QCoreApplication::applicationDirPath()).filePath("Metadata");
    if (!QDir(folder).exists())
        QDir().mkpath(folder);

    _path = QDir(folder).filePath(QString("%1-Rich.json").arg(gameId));

    if (QFile::exists(_path)) {
        QFile file(_path);
        if (file.open(QIODevice::ReadOnly | QIODevice::Text)) {
            QByteArray json = file.readAll();
            file.close();
            auto doc = QJsonDocument::fromJson(json);
            if (doc.isObject()) {
                auto obj = doc.object();
                RichPresenceBackup backup;
                backup.OriginalScript = obj.value("OriginalScript").toString();
                backup.Timestamp = obj.value("Timestamp").toString();
                CurrentBackup = backup;
                return;
            }
        }
        CurrentBackup = std::nullopt;
    } else {
        CurrentBackup = std::nullopt;
    }
}

void RichPresenceMetadataService::SaveBackup(const QString& script) {
    if (_path.isEmpty())
        return;

    RichPresenceBackup backup;
    backup.OriginalScript = script;
    // Culture-aware short date/time, matching DateTime.Now.ToString("g") in C#.
    backup.Timestamp = QLocale().toString(QDateTime::currentDateTime(), QLocale::ShortFormat);

    try {
        QJsonObject obj;
        obj.insert("OriginalScript", backup.OriginalScript);
        obj.insert("Timestamp", backup.Timestamp);

        QFile file(_path);
        if (file.open(QIODevice::WriteOnly | QIODevice::Text)) {
            QJsonDocument doc(obj);
            file.write(doc.toJson(QJsonDocument::Indented));
            file.close();
            CurrentBackup = backup;
        }
    } catch (...) {
    }
}

} // namespace RALE
