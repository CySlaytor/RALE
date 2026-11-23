#pragma once

#include "../Models/GeneratorModels.h"
#include "../Models/LogicModels.h"

#include <QList>

namespace RALE {

class GeneratorNoteDuplicator {
  public:
    struct DuplicationResult {
        QList<CodeNote> UpdatedNotes;
        int CreatedOrModifiedCount = 0;
        int SkippedExistingCount = 0;
        bool HasChanges() const { return CreatedOrModifiedCount > 0; }
    };

    static DuplicationResult DuplicateNotesForGenerator(const QList<AchievementConditionGroup>& finalizedGroups, int masterGroupIndex,
                                                        const GeneratorDefinition& def, const QList<CodeNote>& currentNotes,
                                                        int consoleId = 0, bool useUnicodeTree = false, bool indentDescriptions = true);
};

} // namespace RALE
