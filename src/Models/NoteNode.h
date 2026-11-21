#pragma once

#include <QList>
#include <QString>

#include <cstdint>
#include <memory>

namespace RALE {

// Represents a node in the pointer tree structure (e.g., +0x4)
class NoteNode {
  public:
    QString Offset = "";
    QString Description = "";

    // Size in bytes covered by this note (default 1)
    int64_t Size = 1;
    int64_t ArrayStride = 0;

    int RawLineIndex = 0;
    int IndentLevel = 0;
    QString Content = "";
    NoteNode* Parent = nullptr;
    QList<std::shared_ptr<NoteNode>> Children;

    // Array structure tracking ported from Pointer Finder 2.0
    bool IsArrayElement = false;      // True for root/indexed array elements: |0x00
    bool IsArrayChild = false;        // True for child fields inside array elements: |+0x04
    bool IsInlineArray = false;       // True for embedded arrays at current address: [8000x4 bytes] [Array]
    bool IsPointerArray = false;      // True for pointers to arrays: [30x16 bytes] Pointer to ...
    bool IsTemplateInherited = false; // True when cloned from an array template onto a sibling pointer
    std::weak_ptr<NoteNode> TemplateSource;

    QString ToString() const {
        // Formatting happens in the ListControl's Format event,
        // but this serves as a default fallback.
        if (Description.trimmed().isEmpty())
            return Offset;
        return QString("%1 | %2").arg(Offset, Description);
    }
};

} // namespace RALE
