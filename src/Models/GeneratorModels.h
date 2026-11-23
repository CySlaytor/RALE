#pragma once

#include <QList>
#include <QMap>
#include <QString>

namespace RALE {

class GeneratorRule {
  public:
    QString SourceAddress = "";
    int Stride = 0;

    // Optional per-rule note template and per-copy overrides (1-based copy index)
    QString NoteHeaderTemplate = "";
    QMap<int, QString> NoteCustomNames;
    QMap<int, QString> NoteExplicitTitles;

    bool operator==(const GeneratorRule& other) const {
        return SourceAddress == other.SourceAddress && Stride == other.Stride && NoteHeaderTemplate == other.NoteHeaderTemplate &&
               NoteCustomNames == other.NoteCustomNames && NoteExplicitTitles == other.NoteExplicitTitles;
    }
    bool operator!=(const GeneratorRule& other) const { return !(*this == other); }
};

// Models for the logic generator functionality
class GeneratorDefinition {
  public:
    int Copies = 0;

    // Determines if we create new Alt groups (False) or append to the current group (True)
    bool IsArray = false;

    // When generating an array, duplicates only conditions referencing selected addresses instead of the entire group
    bool DuplicateSelectedOnly = false;

    // When enabled, duplicates associated Code Notes (static and AddAddress pointer chains) after groups are generated
    bool DuplicateNotes = false;

    // Tracks how many conditions formed the original "Template".
    // This allows the ViewModel to clear previous generations before applying new ones.
    int TemplateConditionCount = 0;

    QList<GeneratorRule> Rules;

    bool operator==(const GeneratorDefinition& other) const {
        return Copies == other.Copies && IsArray == other.IsArray && DuplicateSelectedOnly == other.DuplicateSelectedOnly &&
               DuplicateNotes == other.DuplicateNotes && TemplateConditionCount == other.TemplateConditionCount && Rules == other.Rules;
    }
    bool operator!=(const GeneratorDefinition& other) const { return !(*this == other); }
};

} // namespace RALE
