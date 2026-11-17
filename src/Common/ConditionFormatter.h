#pragma once

#include <QString>
#include <QList>
#include <QMap>
#include <memory>
#include "../Models/LogicModels.h"
#include "../Models/NoteNode.h"

namespace RALogicEditor {

// Responsibility: Formats raw model data into display strings (Values, Aliases)
class ConditionFormatter
{
public:
    struct RangeEntry {
        int64_t Start = 0;
        int64_t End = 0;
        std::shared_ptr<NoteNode> Node;
    };

    // Standard fallback without size ref override
    static QString FormatOperandDisplay(
        const Operand &op,
        bool showDecimal,
        bool showAliases,
        const QMap<QString, QList<std::shared_ptr<NoteNode>>> *notesLookup,
        const QList<RangeEntry> *rangeCache,
        const AchievementConditionGroup *group,
        int rowIndex);

    // Overload to support passing sizeRef explicitly
    static QString FormatOperandDisplay(
        const Operand &op,
        bool showDecimal,
        bool showAliases,
        const QMap<QString, QList<std::shared_ptr<NoteNode>>> *notesLookup,
        const QList<RangeEntry> *rangeCache,
        const AchievementConditionGroup *group,
        int rowIndex,
        const QString &sizeRef);

    // Changed to PUBLIC to allow the Preview Control to use this logic
    static QString ResolveAlias(
        const QString &address,
        const QString &sizeRef, // NEW: Used for Bit extraction
        const AchievementConditionGroup *group,
        int rowIndex,
        const QMap<QString, QList<std::shared_ptr<NoteNode>>> &notesLookup,
        const QList<RangeEntry> *rangeCache);

    // Newly added helper to fetch a resolved note, handling redirects to mirror AutoCR
    static std::shared_ptr<NoteNode> GetEffectiveNote(
        const QMap<QString, QList<std::shared_ptr<NoteNode>>> &notesLookup,
        int64_t addrVal);

private:
    struct ChainNode {
        bool IsMem = false;
        QString Value = "";
    };
};

} // namespace RALogicEditor
