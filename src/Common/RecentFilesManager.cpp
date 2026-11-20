#include "RecentFilesManager.h"

#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QRegularExpression>
#include <QTextStream>

namespace RALE {

QString RecentFilesManager::GetFilePathJson() const {
    QString folder = QDir(QCoreApplication::applicationDirPath()).filePath("Metadata");
    if (!QDir(folder).exists())
        QDir().mkpath(folder);
    return QDir(folder).filePath("recent.json");
}

QString RecentFilesManager::GetFilePathTxt() const { return QDir(QCoreApplication::applicationDirPath()).filePath("recent.txt"); }

void RecentFilesManager::Load() {
    Files.clear();
    QString pathJson = GetFilePathJson();
    QString pathTxt = GetFilePathTxt();
    QString legacyRootJson = QDir(QCoreApplication::applicationDirPath()).filePath("recent.json");

    // Migrate existing recent.json from root to Metadata/ folder if needed
    if (!QFile::exists(pathJson) && QFile::exists(legacyRootJson)) {
        QFile::rename(legacyRootJson, pathJson);
    }

    // 1. Try loading new JSON format
    if (QFile::exists(pathJson)) {
        try {
            QFile file(pathJson);
            if (file.open(QIODevice::ReadOnly | QIODevice::Text)) {
                QByteArray json = file.readAll();
                file.close();
                auto doc = QJsonDocument::fromJson(json);
                if (doc.isArray()) {
                    for (const auto& val : doc.array()) {
                        if (val.isObject()) {
                            auto obj = val.toObject();
                            QString p = obj.value("Path").toString();
                            // Filter out files that no longer exist
                            if (QFile::exists(p)) {
                                RecentFileEntry entry;
                                entry.Path = p;
                                entry.GameTitle = obj.value("GameTitle").toString();
                                Files.append(entry);
                            }
                        }
                    }
                }
            }
        } catch (...) { /* Ignore corrupt file */
        }
    }
    // 2. Fallback: Migrate from old TXT format
    else if (QFile::exists(pathTxt)) {
        try {
            QFile file(pathTxt);
            if (file.open(QIODevice::ReadOnly | QIODevice::Text)) {
                QTextStream in(&file);
                while (!in.atEnd()) {
                    QString p = in.readLine().trimmed();
                    if (QFile::exists(p)) {
                        QString title = ExtractGameTitle(p);
                        Files.append(RecentFileEntry{p, title});
                    }
                }
                file.close();
                // Save immediately to JSON format
                Save();
            }
        } catch (...) { /* Ignore errors */
        }
    }
}

void RecentFilesManager::Add(const QString& path) {
    // Normalize path
    QString fullPath = QFileInfo(path).absoluteFilePath();

    // Remove existing entry for this path to bump it to top
    for (int i = Files.size() - 1; i >= 0; --i) {
        if (Files[i].Path.compare(fullPath, Qt::CaseInsensitive) == 0) {
            Files.removeAt(i);
        }
    }

    // Extract title
    QString title = ExtractGameTitle(fullPath);

    // Insert at top
    Files.prepend(RecentFileEntry{fullPath, title});

    // Trim list
    if (Files.size() > MaxFiles) {
        Files.removeLast();
    }

    Save();
}

void RecentFilesManager::Save() {
    try {
        QJsonArray arr;
        for (const auto& f : Files) {
            QJsonObject obj;
            obj.insert("Path", f.Path);
            obj.insert("GameTitle", f.GameTitle);
            arr.append(obj);
        }

        QFile file(GetFilePathJson());
        if (file.open(QIODevice::WriteOnly | QIODevice::Text)) {
            QJsonDocument doc(arr);
            file.write(doc.toJson(QJsonDocument::Indented));
            file.close();
        }
    } catch (...) { /* Ignore write errors */
    }
}

QString RecentFilesManager::ExtractGameTitle(const QString& path) {
    if (path.isEmpty() || !QFile::exists(path))
        return "";

    try {
        // Method 1: Check for JSON (Server Data or Notes)
        if (path.endsWith(".json", Qt::CaseInsensitive)) {
            // Peek at the file content without full deserialization for speed, or just use regex
            // JSON format: "Title": "Game Name"
            QFile file(path);
            if (file.open(QIODevice::ReadOnly | QIODevice::Text)) {
                QString content = QString::fromUtf8(file.readAll());
                file.close();

                // Simple Regex to capture Title value
                static const QRegularExpression titleRegex(R"raw("Title"\s*:\s*"(.*?)")raw");
                auto match = titleRegex.match(content);
                if (match.hasMatch()) {
                    return match.captured(1);
                }
            }
        }
        // Method 2: Check for User Text File (-User.txt)
        else if (path.endsWith(".txt", Qt::CaseInsensitive)) {
            // Format:
            // 1.0
            // Game Title
            QFile file(path);
            if (file.open(QIODevice::ReadOnly | QIODevice::Text)) {
                QTextStream reader(&file);
                reader.readLine();                     // Skip 1.0
                QString titleLine = reader.readLine(); // Game Title
                file.close();

                if (!titleLine.trimmed().isEmpty()) {
                    return titleLine.trimmed();
                }
            }
        }
    } catch (...) {
        // Fail silently and return empty
    }

    return "";
}

} // namespace RALE
