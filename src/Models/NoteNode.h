#pragma once

#include <QString>
#include <QList>
#include <memory>
#include <cstdint>

namespace RALogicEditor {

// Represents a node in the pointer tree structure (e.g., +0x4)
class NoteNode
{
public:
    QString Offset = "";
    QString Description = "";

    // Size in bytes covered by this note (default 1)
    int64_t Size = 1;

    int RawLineIndex = 0;
    int IndentLevel = 0;
    QString Content = "";
    NoteNode* Parent = nullptr;
    QList<std::shared_ptr<NoteNode>> Children;

    QString ToString() const
    {
        // Formatting happens in the ListControl's Format event, 
        // but this serves as a default fallback.
        if (Description.trimmed().isEmpty()) return Offset;
        return QString("%1 | %2").arg(Offset, Description);
    }
};

} // namespace RALogicEditor
