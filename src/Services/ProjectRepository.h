#pragma once

#include "ProjectMetadataService.h"

#include "../Models/AssetModels.h"
#include "../Models/RichPresence.h"
#include "../Parsers/UserTextParser.h"

#include <QList>
#include <QString>

#include <memory>

namespace RALE {

class ProjectRepository {
  public:
    std::shared_ptr<RaRoot> ServerData = nullptr;
    std::shared_ptr<UserTextParser::UserData> LocalData = nullptr;
    std::shared_ptr<RichPresenceScript> RichPresence = nullptr;

    QString RichPresenceFilePath = "";

    // Metadata Service to persist group names
    ProjectMetadataService Metadata;

    QString CurrentGameId = "";
    QString CurrentTitle = "";

    void LoadFiles(const QString& primaryPath, QString& userFilePath);

    // Checks if a note matches exactly what is on the server (Address and Content)
    bool IsNoteSynced(const CodeNote& note) const;

    // Safely deletes the local note modification and restores the pristine server copy
    void RevertNote(const QString& address);

    // Restores the last saved/loaded disk state for a note, discarding current unsaved edits
    void ResetNote(const QString& address);

    // Restores the last saved/loaded disk state for an asset, discarding current unsaved edits
    void ResetAssetToSaved(int id, bool isLeaderboard);

    // Discards local modifications for an asset, restoring the JSON server snapshot
    void RevertAssetToServer(int id, bool isLeaderboard);

    void MarkCurrentAsSaved();
    bool HasUnsavedAssetChanges(const IAssetItem* item) const;
    bool HasUnsavedChanges() const;

    const QList<CodeNote>& GetServerNotes() const { return _serverNotes; }
    QList<CodeNote> GetSavedNotes() const;

    // Merges Server and Local lists based on IDs to determine Sync status
    // Modified: Now preserves the natural reading order of the JSON files rather than forcing numerical ID sorts
    QList<std::shared_ptr<RaAchievement>> GetMergedAchievements();
    QList<std::shared_ptr<RaLeaderboard>> GetMergedLeaderboards();
    QList<std::shared_ptr<IAssetItem>> GetMergedAssets(bool isLeaderboard);

    void EnsureLocalDataExists();
    void SaveRichPresence();
    bool AreAssetsEqual(const IAssetItem& a, const IAssetItem& b) const;

  private:
    QList<CodeNote> _serverNotes; // Store original server notes for comparison
    std::shared_ptr<UserTextParser::UserData> _savedLocalData = nullptr;

    void LoadServerLogic(const QString& filepath);
    void LoadAndMergeNotes(const QString& notesPath);
    std::shared_ptr<UserTextParser::UserData> CloneUserData(const UserTextParser::UserData* src) const;
};

} // namespace RALE
