#pragma once

#include <QString>
#include <QStringList>
#include <QList>
#include <memory>

namespace RALogicEditor {

// Ported from Pointer Finder 2.0
// A tool to parse, merge, and clean up complex code notes with hierarchical pointer chains.
class CodeNoteLogic
{
public:
    // Logic: Parses raw code note text, merges duplicate offsets, fixes hierarchy indentation, and sorts results.
    static QString FixHierarchy(const QString &inputText, const QString &indentStylePref, bool indentDescriptions, int consoleId = 0);
    static QString FixHierarchy(const QString &inputText, bool useUnicodeTree, bool indentDescriptions, int consoleId = 0);

private:
    // Represents a single line/node in the code note's hierarchy.
    class CodeNoteNode
    {
    public:
        // The content of the line without leading indentation, used as the key for merging.
        QString Content = "";
        // Child nodes in the hierarchy.
        QList<std::shared_ptr<CodeNoteNode>> Children;
        // The original index, used to maintain order for non-mergeable items.
        int OriginalIndex = 0;
    };

    static QChar DetectIndentStyle(const QStringList &lines);
    static QList<std::shared_ptr<CodeNoteNode>> ParseLinesToTree(const QStringList &lines, QString &outHeaderComment);
    static QList<std::shared_ptr<CodeNoteNode>> MergeTree(const QList<std::shared_ptr<CodeNoteNode>> &nodes);
    static QList<std::shared_ptr<CodeNoteNode>> SortNodes(QList<std::shared_ptr<CodeNoteNode>> &nodes);
    static QString RebuildTextFromTree(
        const QList<std::shared_ptr<CodeNoteNode>> &nodes,
        int indentLevel,
        bool useUnicodeTree,
        bool indentDescriptions,
        const QString &headerComment = "",
        QList<bool> ancestorIsLast = {},
        int parentNestingLevel = 0,
        const QString &delimiterSuffix = "",
        bool autoPopulateTemplate = false,
        const QString &pointerTypeTemplate = "",
        const QString &indentPrefix = "");
};

} // namespace RALogicEditor
