#include "ProjectRepository.h"

#include "../Parsers/RichPresenceParser.h"

#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QSet>

#include <algorithm>

namespace RALE {

void ProjectRepository::LoadFiles(const QString& primaryPath, QString& userFilePath) {
    ServerData = nullptr;
    LocalData = nullptr;
    RichPresence = nullptr;
    _serverNotes.clear();
    userFilePath = "";
    CurrentGameId = "";
    RichPresenceFilePath = "";

    QFileInfo fi(primaryPath);
    QString dir = fi.absolutePath();
    // completeBaseName() matches .NET's GetFileNameWithoutExtension (last dot, not first).
    QString fileName = fi.completeBaseName();

    // 1. Extract Game ID: leading digit run, otherwise all digits, otherwise the name.
    QString gameIdStr;
    for (QChar c : fileName) {
        if (c.isDigit())
            gameIdStr.append(c);
        else
            break;
    }
    if (gameIdStr.isEmpty()) {
        for (QChar c : fileName) {
            if (c.isDigit())
                gameIdStr.append(c);
        }
        if (gameIdStr.isEmpty())
            gameIdStr = fileName;
    }
    CurrentGameId = gameIdStr;

    // Load Metadata immediately to have names ready
    Metadata.Load(CurrentGameId);

    // 2. Determine Paths
    QString jsonPath = QDir(dir).filePath(QString("%1.json").arg(gameIdStr));
    QString userPath = QDir(dir).filePath(QString("%1-User.txt").arg(gameIdStr));
    QString notesPath = QDir(dir).filePath(QString("%1-Notes.json").arg(gameIdStr));
    QString rpPath = QDir(dir).filePath(QString("%1-Rich.txt").arg(gameIdStr));

    QString selectedExt = fi.suffix().toLower();

    if (selectedExt == "json") {
        // If opening Notes file directly, treat it as such, otherwise logic file
        if (primaryPath.endsWith("-Notes.json", Qt::CaseInsensitive))
            notesPath = primaryPath;
        else
            jsonPath = primaryPath;
    } else if (selectedExt == "txt") {
        if (primaryPath.endsWith("-Rich.txt", Qt::CaseInsensitive))
            rpPath = primaryPath;
        else
            userPath = primaryPath;
    }

    // 3. Load Logic Data
    if (QFile::exists(jsonPath))
        LoadServerLogic(jsonPath);

    // 4. Load User Data
    if (QFile::exists(userPath)) {
        LocalData = std::make_shared<UserTextParser::UserData>(UserTextParser::ParseUserFile(userPath));
        userFilePath = userPath;
    } else {
        // Initialize empty local data if no user file exists yet
        LocalData = std::make_shared<UserTextParser::UserData>();
    }

    // 5. Load and Merge Notes
    LoadAndMergeNotes(notesPath);

    // 6. Load Rich Presence
    RichPresenceFilePath = rpPath;
    if (QFile::exists(rpPath)) {
        try {
            QFile rpFile(rpPath);
            if (rpFile.open(QIODevice::ReadOnly | QIODevice::Text)) {
                QString rpContent = QString::fromUtf8(rpFile.readAll());
                rpFile.close();
                RichPresence = std::make_shared<RichPresenceScript>(RichPresenceParser::Parse(rpContent));
            }
        } catch (...) { /* Ignore RP load errors */
        }
    }
}

void ProjectRepository::LoadServerLogic(const QString& filepath) {
    try {
        QFile file(filepath);
        if (!file.open(QIODevice::ReadOnly | QIODevice::Text))
            return;
        QByteArray jsonContent = file.readAll();
        file.close();

        // C# StreamReader transparently skips a UTF-8 BOM; QJsonDocument does not.
        if (jsonContent.startsWith("\xEF\xBB\xBF"))
            jsonContent.remove(0, 3);

        auto doc = QJsonDocument::fromJson(jsonContent);
        if (!doc.isObject())
            return;
        auto rootObj = doc.object();

        ServerData = std::make_shared<RaRoot>();
        ServerData->Success = rootObj.value("Success").toBool();
        ServerData->GameId = rootObj.value("GameId").toInt();
        ServerData->Title = rootObj.value("Title").toString();
        ServerData->ImageIconUrl = rootObj.value("ImageIconUrl").toString();
        ServerData->RichPresenceGameId = rootObj.value("RichPresenceGameId").toInt();
        ServerData->RichPresencePatch = rootObj.value("RichPresencePatch").toString();
        ServerData->ConsoleId = rootObj.value("ConsoleId").toInt();

        auto setsArr = rootObj.value("Sets").toArray();
        for (const auto& setVal : setsArr) {
            auto setObj = setVal.toObject();
            RaSet set;
            set.Title = setObj.value("Title").toString();
            set.Type = setObj.value("Type").toString();
            set.AchievementSetId = setObj.value("AchievementSetId").toInt();
            set.GameId = setObj.value("GameId").toInt();
            set.ImageIconUrl = setObj.value("ImageIconUrl").toString();

            auto achArr = setObj.value("Achievements").toArray();
            for (const auto& aVal : achArr) {
                auto aObj = aVal.toObject();
                auto ach = std::make_shared<RaAchievement>();
                ach->ID = aObj.value("ID").toInt();
                ach->Title = aObj.value("Title").toString();
                ach->Description = aObj.value("Description").toString();
                ach->Points = aObj.value("Points").toInt();
                ach->Author = aObj.value("Author").toString("Unknown");
                ach->BadgeName = aObj.value("BadgeName").toString("00000");
                ach->Type = aObj.value("Type").toString();
                ach->Flags = aObj.value("Flags").toInt();
                ach->Logic = aObj.value("MemAddr").toString();

                // Rule: Flags 3 = Core, Flags 5 = Unofficial. Everything else is treated as invalid/fallback.
                if (ach->Flags == 3)
                    ach->Category = "Core";
                else if (ach->Flags == 5)
                    ach->Category = "Unofficial";
                else
                    ach->Category = "Unofficial"; // Fallback for invalid/unknown flags within a server set

                set.Achievements.append(ach);
            }

            auto lbArr = setObj.value("Leaderboards").toArray();
            for (const auto& lVal : lbArr) {
                auto lObj = lVal.toObject();
                auto lb = std::make_shared<RaLeaderboard>();
                lb->ID = lObj.value("ID").toInt();
                lb->Logic = lObj.value("Mem").toString();
                lb->Format = lObj.value("Format").toString("VALUE");
                if (lObj.value("LowerIsBetter").isBool())
                    lb->LowerIsBetter = lObj.value("LowerIsBetter").toBool();
                else
                    lb->LowerIsBetter = lObj.value("LowerIsBetter").toInt() != 0;
                lb->Title = lObj.value("Title").toString();
                lb->Description = lObj.value("Description").toString();
                lb->Hidden = lObj.value("Hidden").toBool();
                // Leaderboards don't usually have Flags in this JSON format, utilize Set Type
                lb->Category = set.Type;

                set.Leaderboards.append(lb);
            }

            ServerData->Sets.append(set);
        }

        CurrentTitle = QString("%1 (%2)").arg(ServerData->Title).arg(ServerData->GameId);
    } catch (...) { /* Log handling could go here */
    }
}

void ProjectRepository::LoadAndMergeNotes(const QString& notesPath) {
    if (QFile::exists(notesPath)) {
        try {
            QFile file(notesPath);
            if (file.open(QIODevice::ReadOnly | QIODevice::Text)) {
                QByteArray jsonContent = file.readAll();
                file.close();
                if (jsonContent.startsWith("\xEF\xBB\xBF"))
                    jsonContent.remove(0, 3);
                auto doc = QJsonDocument::fromJson(jsonContent);
                if (doc.isArray()) {
                    _serverNotes.clear();
                    for (const auto& val : doc.array()) {
                        auto obj = val.toObject();
                        CodeNote note;
                        note.Address = obj.value("Address").toString();
                        note.Note = obj.value("Note").toString();
                        note.User = obj.value("User").toString();
                        _serverNotes.append(note);
                    }
                }
            }
        } catch (...) { /* Ignore load errors */
        }
    }

    // Merge Logic:
    // 1. Start with Server Notes
    // 2. Overlay Local Notes (User.txt)

    QMap<QString, CodeNote> mergedNotesMap;

    for (const auto& n : _serverNotes) {
        // Store a copy to ensure we don't modify the reference in _serverNotes list
        if (!mergedNotesMap.contains(n.Address))
            mergedNotesMap.insert(n.Address, CodeNote{n.User, n.Address, n.Note});
    }

    if (LocalData != nullptr) {
        for (const auto& n : LocalData->Notes) {
            // Local notes take precedence (edits)
            mergedNotesMap.insert(n.Address, n);
        }

        // Update the LocalData list to contain the full merged set for the UI
        QList<CodeNote> mergedList = mergedNotesMap.values();
        std::sort(mergedList.begin(), mergedList.end(), [](const CodeNote& a, const CodeNote& b) { return a.Address < b.Address; });
        LocalData->Notes = mergedList;
    }
}

// Checks if a note matches exactly what is on the server (Address and Content)
bool ProjectRepository::IsNoteSynced(const CodeNote& note) const {
    for (const auto& n : _serverNotes) {
        if (n.Address == note.Address) {
            // Compare content, ignoring User field
            return n.Note == note.Note;
        }
    }
    return false; // New local note
}

// Safely deletes the local note modification and restores the pristine server copy
void ProjectRepository::RevertNote(const QString& address) {
    if (LocalData == nullptr)
        return;

    for (int i = 0; i < LocalData->Notes.size(); i++) {
        if (LocalData->Notes[i].Address == address) {
            LocalData->Notes.removeAt(i);
            break;
        }
    }

    for (const auto& serverNote : _serverNotes) {
        if (serverNote.Address == address) {
            // Put it back in the active merged list so the UI receives the original
            LocalData->Notes.append(CodeNote{serverNote.User, serverNote.Address, serverNote.Note});
            std::sort(LocalData->Notes.begin(), LocalData->Notes.end(),
                      [](const CodeNote& a, const CodeNote& b) { return a.Address < b.Address; });
            break;
        }
    }
}

QList<std::shared_ptr<RaAchievement>> ProjectRepository::GetMergedAchievements() {
    QList<std::shared_ptr<RaAchievement>> list;
    if (ServerData == nullptr && LocalData == nullptr)
        return list;

    // Preserve JSON order natively
    QList<std::shared_ptr<RaAchievement>> serverItemsList;
    if (ServerData != nullptr) {
        for (const auto& set : ServerData->Sets)
            serverItemsList.append(set.Achievements);
    }

    QList<std::shared_ptr<RaAchievement>> localItemsList;
    if (LocalData != nullptr)
        localItemsList = LocalData->Achievements;

    // Safely convert to dictionary ignoring duplicate user manual errors
    QMap<int, std::shared_ptr<RaAchievement>> localItemsDict;
    for (const auto& item : localItemsList) {
        if (!localItemsDict.contains(item->ID))
            localItemsDict.insert(item->ID, item);
    }

    QSet<int> processedIds;

    // 1. Map Server Items in their natural JSON order, tagging local modifications if they exist
    for (const auto& srv : serverItemsList) {
        if (localItemsDict.contains(srv->ID)) {
            auto loc = localItemsDict.value(srv->ID);
            if (AreAssetsEqual(*loc, *srv))
                loc->State = AssetState::Synced;
            else
                loc->State = AssetState::Modified;

            loc->Category = srv->Category;
            list.append(loc);
        } else {
            srv->State = AssetState::Synced;
            list.append(srv);
        }
        processedIds.insert(srv->ID);
    }

    // 2. Append Local-Only (New) items at the bottom in their natural User.txt order
    for (const auto& loc : localItemsList) {
        if (!processedIds.contains(loc->ID)) {
            loc->State = AssetState::Local;
            list.append(loc);
        }
    }

    return list;
}

QList<std::shared_ptr<RaLeaderboard>> ProjectRepository::GetMergedLeaderboards() {
    QList<std::shared_ptr<RaLeaderboard>> list;
    if (ServerData == nullptr && LocalData == nullptr)
        return list;

    // Preserve JSON order natively
    QList<std::shared_ptr<RaLeaderboard>> serverItemsList;
    if (ServerData != nullptr) {
        for (const auto& set : ServerData->Sets)
            serverItemsList.append(set.Leaderboards);
    }

    QList<std::shared_ptr<RaLeaderboard>> localItemsList;
    if (LocalData != nullptr)
        localItemsList = LocalData->Leaderboards;

    // Safely convert to dictionary ignoring duplicate user manual errors
    QMap<int, std::shared_ptr<RaLeaderboard>> localItemsDict;
    for (const auto& item : localItemsList) {
        if (!localItemsDict.contains(item->ID))
            localItemsDict.insert(item->ID, item);
    }

    QSet<int> processedIds;

    // 1. Map Server Items in their natural JSON order, tagging local modifications if they exist
    for (const auto& srv : serverItemsList) {
        if (localItemsDict.contains(srv->ID)) {
            auto loc = localItemsDict.value(srv->ID);
            if (AreAssetsEqual(*loc, *srv))
                loc->State = AssetState::Synced;
            else
                loc->State = AssetState::Modified;

            loc->Category = srv->Category;
            list.append(loc);
        } else {
            srv->State = AssetState::Synced;
            list.append(srv);
        }
        processedIds.insert(srv->ID);
    }

    // 2. Append Local-Only (New) items at the bottom in their natural User.txt order
    for (const auto& loc : localItemsList) {
        if (!processedIds.contains(loc->ID)) {
            loc->State = AssetState::Local;
            list.append(loc);
        }
    }

    return list;
}

QList<std::shared_ptr<IAssetItem>> ProjectRepository::GetMergedAssets(bool isLeaderboard) {
    QList<std::shared_ptr<IAssetItem>> result;
    if (isLeaderboard) {
        for (const auto& lb : GetMergedLeaderboards())
            result.append(lb);
    } else {
        for (const auto& ach : GetMergedAchievements())
            result.append(ach);
    }
    return result;
}

bool ProjectRepository::AreAssetsEqual(const IAssetItem& a, const IAssetItem& b) const {
    if (a.Title != b.Title)
        return false;
    if (a.Description != b.Description)
        return false;
    if (a.Logic != b.Logic)
        return false;

    if (!a.isLeaderboard() && !b.isLeaderboard()) {
        const auto& aa = static_cast<const RaAchievement&>(a);
        const auto& bb = static_cast<const RaAchievement&>(b);
        if (aa.Points != bb.Points)
            return false;
        if (aa.Type != bb.Type)
            return false;
        // Note: We don't compare Flags/Category for sync state, as User.txt doesn't store them.
        // Sync state is strictly about data persisted in User.txt vs Server.
    } else if (a.isLeaderboard() && b.isLeaderboard()) {
        const auto& la = static_cast<const RaLeaderboard&>(a);
        const auto& lb = static_cast<const RaLeaderboard&>(b);
        if (la.Format != lb.Format)
            return false;
        if (la.LowerIsBetter != lb.LowerIsBetter)
            return false;
    }
    return true;
}

void ProjectRepository::EnsureLocalDataExists() {
    if (LocalData == nullptr)
        LocalData = std::make_shared<UserTextParser::UserData>();
}

void ProjectRepository::SaveRichPresence() {
    if (RichPresence != nullptr && !RichPresenceFilePath.isEmpty()) {
        try {
            QFile file(RichPresenceFilePath);
            if (file.open(QIODevice::WriteOnly | QIODevice::Text)) {
                file.write(RichPresence->ToString().toUtf8());
                file.close();
            }
        } catch (...) { /* Ignore save errors */
        }
    }
}

} // namespace RALE
