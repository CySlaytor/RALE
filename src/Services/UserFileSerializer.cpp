#include "UserFileSerializer.h"

#include "AssetStrategies.h"
#include "ProjectRepository.h"

#include "../Utilities/LogicFormatter.h"

namespace RALE {

QString UserFileSerializer::Serialize(const QString& gameTitle, const UserTextParser::UserData* localData,
                                      const QList<std::shared_ptr<RaAchievement>>& mergedAchievements,
                                      const QList<std::shared_ptr<RaLeaderboard>>& mergedLeaderboards, ProjectRepository& repo) {
    QString sb;
    sb += "1.0\n";
    sb += gameTitle + "\n";

    LeaderboardStrategy lbStrategy;

    // --- HELPER FOR ESCAPING STRINGS (Fixes the Colon/Backslash Bug) ---
    auto EscapeUserString = [](const QString& input) -> QString {
        if (input.isEmpty())
            return "";

        // If content has a colon, quote, or backslash, it must be escaped/wrapped
        if (input.contains(':') || input.contains('"') || input.contains('\\')) {
            // 1. Escape backslashes first ( \ -> \\ ) to prevents paths like "local\img" becoming "local\\img" literals
            // 2. Escape quotes ( " -> \" )
            // 3. Wrap the entire string in quotes
            QString escaped = input;
            escaped.replace('\\', "\\\\").replace('"', "\\\"");
            return QString("\"%1\"").arg(escaped);
        }
        return input;
    };

    for (const auto& a : mergedAchievements) {
        if (a->State == AssetState::Synced)
            continue; // Don't save synced items to User.txt

        // Sanitize BadgeName to prevent multiline corruption
        // Match C#: only a null BadgeName falls back to "00000"; an intentionally
        // empty badge must stay empty in the saved file.
        QString cleanBadge = a->BadgeName;
        cleanBadge.remove('\r').remove('\n');
        cleanBadge = cleanBadge.trimmed();

        // APPLY ESCAPE TO TITLE, DESCRIPTION AND BADGE
        sb += QString("%1:\"%2\":%3:%4:::%5:%6:%7:::::%8\n")
                  .arg(QString::number(a->ID), a->Logic, EscapeUserString(a->Title), EscapeUserString(a->Description), a->Type, a->Author,
                       QString::number(a->Points), EscapeUserString(cleanBadge));
    }

    for (const auto& l : mergedLeaderboards) {
        if (l->State == AssetState::Synced)
            continue;

        // Use strategy to ensure we get clean segments for the file format
        auto segments = lbStrategy.ParseLogic(l->Logic);

        auto GetSeg = [&](const QString& key) -> QString {
            if (!segments.contains(key))
                return "";
            return LogicFormatter::GroupsToString(segments[key], (key == "Value") ? '$' : 'S');
        };

        // APPLY ESCAPE TO TITLE AND DESCRIPTION. Convert boolean to 1/0 for integration compatibility.
        sb += QString("L%1:\"%2\":\"%3\":\"%4\":\"%5\":%6:%7:%8:%9\n")
                  .arg(QString::number(l->ID), GetSeg("Start"), GetSeg("Cancel"), GetSeg("Submit"), GetSeg("Value"), l->Format,
                       EscapeUserString(l->Title), EscapeUserString(l->Description), l->LowerIsBetter ? "1" : "0");
    }

    // Save notes - Filter out notes that match the server data
    if (localData != nullptr) {
        for (const auto& n : localData->Notes) {
            // Only save notes that are modified or new compared to the server JSON
            if (repo.IsNoteSynced(n))
                continue;

            // Replaces literal line feeds with \r\n formatting and escapes quotes safely.
            // NOTE: Backslashes are NOT escaped here: UserTextParser only unescapes \" and \r\n,
            // so escaping '\' would double every backslash each save/reload cycle.
            QString formattedNote = n.Note;
            formattedNote.replace('"', "\\\"").remove('\r').replace('\n', "\\r\\n");
            sb += QString("N0:%1:\"%2\"\n").arg(n.Address, formattedNote);
        }
    }

    return sb;
}

} // namespace RALE
