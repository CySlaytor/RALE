#pragma once

#include "IPointerTreeParser.h"
#include <QRegularExpression>

namespace RALogicEditor {

// Parses raw Code Note text into a hierarchical tree of NoteNodes.
// Handles standard RetroAchievements pointer notation, including Unicode box characters and PF2 array notation.
class PointerTreeParser : public IPointerTreeParser
{
public:
    // Main entry point: Converts a multiline string into a flat list of nodes
    // preserving hierarchy via Parent/Children references.
    QList<std::shared_ptr<NoteNode>> ParseNoteText(const QString &noteText) override;

    // Constructs the RA Logic String (e.g., I:0x1234_0x10) for a specific node in the tree.
    std::tuple<QString, QString, QString> GeneratePointerLogic(
        const CodeNote &baseNote,
        const std::shared_ptr<NoteNode> &selectedNode,
        int consoleId = 0) override;

    // Reverse-engineers the chain of offsets from a specific node up to the root.
    QList<int64_t> GetOffsetsFromNode(const std::shared_ptr<NoteNode> &node) override;

private:
    // Parses data size from tags in the description (e.g., [16-bit] or [32 bytes] or [30x16 bytes]).
    int64_t ParseSizeFromDescription(const QString &desc, bool *foundTag = nullptr);

    // Helper to traverse tree and populate the list.
    void CollectAllNodes(const std::shared_ptr<NoteNode> &node, QList<std::shared_ptr<NoteNode>> &flatList);
};

} // namespace RALogicEditor
