#include "CodeNoteLogic.h"
#include "../Common/AppSettings.h"
#include "../Definitions/RaLogicSyntax.h"
#include <QRegularExpression>
#include <QStack>
#include <QMap>
#include <algorithm>

namespace RALogicEditor {

// Logic: Parses raw code note text, merges duplicate offsets, fixes hierarchy indentation, and sorts results.
QString CodeNoteLogic::FixHierarchy(const QString &inputText, const QString &indentStylePref, bool indentDescriptions, int consoleId)
{
    bool useUnicode = indentStylePref.startsWith("Unicode Tree", Qt::CaseInsensitive);
    if (indentStylePref.startsWith("Auto Detect", Qt::CaseInsensitive) || indentStylePref.isEmpty())
    {
        QString normalized = inputText;
        normalized.replace("\r\n", "\n").replace('\r', '\n');
        QStringList lines = normalized.split('\n');
        QChar detected = DetectIndentStyle(lines);
        useUnicode = (detected == 'U');
    }

    return FixHierarchy(inputText, useUnicode, indentDescriptions, consoleId);
}

QString CodeNoteLogic::FixHierarchy(const QString &inputText, bool useUnicodeTree, bool indentDescriptions, int consoleId)
{
    if (inputText.trimmed().isEmpty()) return "";

    QString normalized = inputText;
    normalized.replace("\r\n", "\n").replace('\r', '\n');
    QStringList lines = normalized.split('\n');

    QString headerComment;
    // 1. Parse the flat text into a tree structure, preserving top-level comments and headers
    auto parsedTree = ParseLinesToTree(lines, headerComment);

    // 2. Recursively merge AND sort nodes, supporting both pointer (+0x) and array (|0x) offsets
    auto mergedTree = MergeTree(parsedTree);

    QString delimiter = AppSettings::Current.CodeNoteSuffix;
    if (delimiter.isEmpty()) delimiter = "=";
    bool autoPopulate = AppSettings::Current.CodeNoteAutoPopulatePointers;
    QString ptrTemplate = RaLogicSyntax::GetPlatformPointerTemplate(consoleId, headerComment);
    QString indentPrefix = AppSettings::Current.CodeNotePrefix;
    if (indentPrefix.isEmpty()) indentPrefix = "+";

    // 3. Rebuild the text from the cleaned tree structure
    return RebuildTextFromTree(mergedTree, 0, useUnicodeTree, indentDescriptions, headerComment,
                               {}, 0, delimiter, autoPopulate, ptrTemplate, indentPrefix).trimmed();
}

QChar CodeNoteLogic::DetectIndentStyle(const QStringList &lines)
{
    int plusCount = 0;
    int dotCount = 0;
    int unicodeCount = 0;

    for (const QString &line : lines)
    {
        QString trimmed = line.trimmed();
        if (trimmed.startsWith("++") || trimmed.startsWith("+0x", Qt::CaseInsensitive)) plusCount++;
        else if (trimmed.startsWith("..") || trimmed.startsWith(".0x", Qt::CaseInsensitive)) dotCount++;
        else if (trimmed.contains(QChar(0x251C)) || trimmed.contains(QChar(0x2514)) || trimmed.contains(QChar(0x2502))) unicodeCount++;
    }

    if (unicodeCount > plusCount && unicodeCount > dotCount) return 'U'; // Unicode
    if (dotCount > plusCount) return '.'; // Dots
    return '+'; // Default to pluses
}

QList<std::shared_ptr<CodeNoteLogic::CodeNoteNode>> CodeNoteLogic::ParseLinesToTree(const QStringList &lines, QString &outHeaderComment)
{
    auto rootNodes = QList<std::shared_ptr<CodeNoteNode>>();
    struct StackEntry {
        int indent;
        QList<std::shared_ptr<CodeNoteNode>> *childrenList;
    };
    QStack<StackEntry> stack;
    stack.push({ -1, &rootNodes });
    std::shared_ptr<CodeNoteNode> lastNode = nullptr;
    int lineIndex = 0;
    bool hasEncounteredOffset = false;

    // Support standard offset jumps (+0x, -0x) as well as array element offsets (|0x, |+0x, |-0x)
    static const QRegularExpression offsetSearch(
        R"((?:\|[\+\-]?|[\+\-])0x[0-9a-fA-F]+)",
        QRegularExpression::CaseInsensitiveOption);

    for (const auto &line : lines)
    {
        QString trimmedLine = line.trimmed();
        if (trimmedLine.isEmpty())
        {
            if (!hasEncounteredOffset)
            {
                if (!outHeaderComment.isEmpty())
                    outHeaderComment.append("\n");
            }
            lineIndex++;
            continue;
        }

        int indent = 0;
        QString content;
        QString untrimmedLine = line;

        auto offsetMatch = offsetSearch.match(untrimmedLine);
        bool isOffsetLine = offsetMatch.hasMatch();

        if (isOffsetLine)
        {
            hasEncounteredOffset = true;
            int offsetPos = offsetMatch.capturedStart();
            QString prefix = untrimmedLine.left(offsetPos);

            bool hasBoxChar = false;
            for (QChar c : prefix)
            {
                if (c == QChar(0x2502) || c == ' ' || c == QChar(0x251C) || c == QChar(0x2514) || c == QChar(0x252C) || c == QChar(0x2500))
                {
                    hasBoxChar = true;
                    break;
                }
            }

            if (hasBoxChar)
            {
                indent = (prefix.length() > 0) ? (prefix.length() - 1) : 0;
            }
            else
            {
                indent = prefix.length();
            }

            content = untrimmedLine.mid(offsetPos).trimmed();

            // If content starts with an array-dereference compound (e.g. "|+0x..."),
            // it represents a child level beneath the array element (|0x...).
            if (content.startsWith("|+") || content.startsWith("|-"))
            {
                indent += 1;
            }
        }
        else
        {
            indent = 0;
            content = trimmedLine;
        }

        if (indent < 0) indent = 0;

        if (!hasEncounteredOffset && !isOffsetLine)
        {
            if (!outHeaderComment.isEmpty())
                outHeaderComment.append("\n");
            outHeaderComment.append(trimmedLine);
            lineIndex++;
            continue;
        }

        bool isAttachedNote = !isOffsetLine && (lastNode != nullptr);

        if (isAttachedNote)
        {
            // Strip leading prefix dots, pipes, or spaces for clean data representation
            QString cleanNote = trimmedLine;
            int prefixChars = 0;
            while (prefixChars < cleanNote.length() &&
                   (cleanNote[prefixChars] == '.' || cleanNote[prefixChars] == ' ' || cleanNote[prefixChars] == QChar(0x2502)))
            {
                prefixChars++;
            }
            cleanNote = cleanNote.mid(prefixChars).trimmed();

            if (cleanNote.startsWith('=') && cleanNote.length() > 1 && cleanNote[1] != '=')
            {
                cleanNote = cleanNote.mid(1).trimmed();
            }

            if (!cleanNote.isEmpty())
            {
                auto noteNode = std::make_shared<CodeNoteNode>();
                noteNode->Content = cleanNote;
                noteNode->OriginalIndex = lineIndex;
                lastNode->Children.append(noteNode);
            }
        }
        else
        {
            while (stack.top().indent >= indent)
            {
                stack.pop();
            }

            auto node = std::make_shared<CodeNoteNode>();
            node->Content = content;
            node->OriginalIndex = lineIndex;

            stack.top().childrenList->append(node);
            stack.push({ indent, &node->Children });
            lastNode = node;
        }
        lineIndex++;
    }

    return rootNodes;
}

QList<std::shared_ptr<CodeNoteLogic::CodeNoteNode>> CodeNoteLogic::MergeTree(const QList<std::shared_ptr<CodeNoteNode>> &nodes)
{
    QList<std::shared_ptr<CodeNoteNode>> finalNodes;
    QMap<QString, QList<std::shared_ptr<CodeNoteNode>>> mergeableGroups;
    QList<std::shared_ptr<CodeNoteNode>> nonMergeableNodes;

    static const QRegularExpression offsetRegex(
        R"(^((?:\|[\+\-]?|[\+\-])0x[0-9a-fA-F]+))",
        QRegularExpression::CaseInsensitiveOption);

    for (const auto &node : nodes)
    {
        auto match = offsetRegex.match(node->Content);
        if (match.hasMatch())
        {
            QString rawKey = match.captured(1);
            QString pipePrefix = rawKey.startsWith('|') ? "|" : "";
            QString offsetPart = rawKey.startsWith('|') ? rawKey.mid(1) : rawKey;
            QString sign = offsetPart.startsWith('-') ? "-" : (offsetPart.startsWith('+') ? "+" : "");
            QString hexPart = (offsetPart.startsWith('-') || offsetPart.startsWith('+')) ? offsetPart.mid(1) : offsetPart;
            if (hexPart.startsWith("0x", Qt::CaseInsensitive))
            {
                hexPart = hexPart.mid(2);
            }
            bool ok;
            qlonglong offsetVal = hexPart.toLongLong(&ok, 16);
            QString key = QString("%1%20x%3").arg(pipePrefix).arg(sign).arg(QString("%1").arg(offsetVal, 2, 16, QChar('0')).toLower());

            mergeableGroups[key].append(node);
        }
        else
        {
            nonMergeableNodes.append(node);
        }
    }

    static const QRegularExpression contentRegex(
        R"(^((?:\|[\+\-]?|[\+\-])0x)([0-9a-fA-F]+)(.*))",
        QRegularExpression::CaseInsensitiveOption);

    for (auto it = mergeableGroups.cbegin(); it != mergeableGroups.cend(); ++it)
    {
        const auto &group = it.value();
        auto representative = group.front();
        for (const auto &n : group)
        {
            if (n->OriginalIndex < representative->OriginalIndex)
                representative = n;
        }

        QString bestContent = representative->Content;
        for (const auto &n : group)
        {
            if (n->Content.length() > bestContent.length())
                bestContent = n->Content;
        }

        auto contentMatch = contentRegex.match(bestContent);
        if (contentMatch.hasMatch())
        {
            QString signAndPrefix = contentMatch.captured(1);
            qlonglong offsetVal = contentMatch.captured(2).toLongLong(nullptr, 16);
            QString restOfLine = contentMatch.captured(3);
            bestContent = QString("%1%2%3")
                              .arg(signAndPrefix.toLower())
                              .arg(QString("%1").arg(offsetVal, 2, 16, QChar('0')).toLower())
                              .arg(restOfLine);
        }

        auto newNode = std::make_shared<CodeNoteNode>();
        newNode->Content = bestContent;
        newNode->OriginalIndex = representative->OriginalIndex;

        QList<std::shared_ptr<CodeNoteNode>> allChildren;
        for (const auto &n : group)
        {
            allChildren.append(n->Children);
        }

        if (!allChildren.isEmpty())
        {
            newNode->Children = MergeTree(allChildren);
        }
        finalNodes.append(newNode);
    }

    for (const auto &node : nonMergeableNodes)
    {
        if (!node->Children.isEmpty())
        {
            node->Children = MergeTree(node->Children);
        }
        finalNodes.append(node);
    }

    return SortNodes(finalNodes);
}

// Helper method to sort nodes intelligently.
QList<std::shared_ptr<CodeNoteLogic::CodeNoteNode>> CodeNoteLogic::SortNodes(QList<std::shared_ptr<CodeNoteNode>> &nodes)
{
    static const QRegularExpression offsetRegex(
        R"(^(?:\|[\+\-]?|[\+\-])0x([0-9a-fA-F]+))",
        QRegularExpression::CaseInsensitiveOption);

    std::sort(nodes.begin(), nodes.end(), [](const std::shared_ptr<CodeNoteNode> &a, const std::shared_ptr<CodeNoteNode> &b) {
        bool aIsOffset = offsetRegex.match(a->Content).hasMatch();
        bool bIsOffset = offsetRegex.match(b->Content).hasMatch();

        if (aIsOffset != bIsOffset)
        {
            return !aIsOffset;
        }

        if (aIsOffset)
        {
            qlonglong aVal = offsetRegex.match(a->Content).captured(1).toLongLong(nullptr, 16);
            qlonglong bVal = offsetRegex.match(b->Content).captured(1).toLongLong(nullptr, 16);
            return aVal < bVal;
        }

        return a->OriginalIndex < b->OriginalIndex;
    });

    return nodes;
}

// Integrated spacing logic directly into the rebuild function.
// Supports both Unicode box-drawing Tree and Plusses/Dots, plus Array element formatting.
QString CodeNoteLogic::RebuildTextFromTree(
    const QList<std::shared_ptr<CodeNoteNode>> &nodes,
    int indentLevel,
    bool useUnicodeTree,
    bool indentDescriptions,
    const QString &headerComment,
    QList<bool> ancestorIsLast,
    int parentNestingLevel,
    const QString &delimiterSuffix,
    bool autoPopulateTemplate,
    const QString &pointerTypeTemplate,
    const QString &indentPrefix)
{
    QString sb = "";

    QString effectiveDelimiter = !delimiterSuffix.isEmpty() ? delimiterSuffix : AppSettings::Current.CodeNoteSuffix;
    if (effectiveDelimiter.isEmpty()) effectiveDelimiter = "=";

    if (indentLevel == 0 && !headerComment.trimmed().isEmpty())
    {
        QStringList headerLines = headerComment.split('\n');
        QStringList formattedHeader;

        static const QRegularExpression rootEnumKvRegex(
            R"(^((?:0x[0-9a-fA-F]+|\d+|Bit\s*\d+))\s*([=:]|\s-\s)\s*(.*)$)",
            QRegularExpression::CaseInsensitiveOption);

        for (int i = 0; i < headerLines.size(); ++i)
        {
            QString hLine = headerLines[i].trimmed();
            if (i > 0)
            {
                auto match = rootEnumKvRegex.match(hLine);
                if (match.hasMatch())
                {
                    QString key = match.captured(1).trimmed();
                    QString val = match.captured(3).trimmed();
                    formattedHeader.append(key + effectiveDelimiter + val);
                    continue;
                }
            }
            formattedHeader.append(hLine);
        }

        sb.append(formattedHeader.join("\r\n").trimmed() + "\r\n\r\n");
    }

    static const QRegularExpression offsetRegex(
        R"(^((?:\|[\+\-]?|[\+\-])0x[0-9a-fA-F]+)(?:\s*([=:|]|\s-\s)\s*|\s+)(.*)$)",
        QRegularExpression::CaseInsensitiveOption);

    static const QRegularExpression attachedEnumKvRegex(
        R"(^((?:0x[0-9a-fA-F]+|\d+|Bit\s*\d+))\s*([=:]|\s-\s)\s*(.*)$)",
        QRegularExpression::CaseInsensitiveOption);

    static const QRegularExpression attachedNamedKvRegex(
        R"(^([A-Za-z0-9_]+)\s*([=:])\s*(.*)$)",
        QRegularExpression::CaseInsensitiveOption);

    for (int i = 0; i < nodes.size(); ++i)
    {
        const auto &node = nodes[i];
        bool isLastChild = (i == nodes.size() - 1);

        bool isPointerOffset =
            node->Content.startsWith("+0x", Qt::CaseInsensitive) || node->Content.startsWith("-0x", Qt::CaseInsensitive) ||
            node->Content.startsWith("|0x", Qt::CaseInsensitive) || node->Content.startsWith("|+0x", Qt::CaseInsensitive) ||
            node->Content.startsWith("|-0x", Qt::CaseInsensitive);
        bool isAttachedNote = !isPointerOffset;

        bool hasPointerChildren = false;
        for (const auto &child : node->Children)
        {
            if (child->Content.startsWith("+0x", Qt::CaseInsensitive) || child->Content.startsWith("-0x", Qt::CaseInsensitive) ||
                child->Content.startsWith("|0x", Qt::CaseInsensitive) || child->Content.startsWith("|+0x", Qt::CaseInsensitive) ||
                child->Content.startsWith("|-0x", Qt::CaseInsensitive))
            {
                hasPointerChildren = true;
                break;
            }
        }

        QString prefix = "";
        int nextNestingLevel = 0;

        if (useUnicodeTree)
        {
            if (isAttachedNote)
            {
                if (indentDescriptions && indentLevel > 1)
                {
                    for (int a = 0; a < indentLevel - 2; ++a)
                    {
                        prefix.append(ancestorIsLast[a] ? " " : QString::fromUtf8("│"));
                    }
                    prefix.append(ancestorIsLast[indentLevel - 2] ? "  " : QString::fromUtf8("│ "));
                }
            }
            else
            {
                if (indentLevel > 0)
                {
                    for (int a = 0; a < indentLevel - 1; ++a)
                    {
                        prefix.append(ancestorIsLast[a] ? " " : QString::fromUtf8("│"));
                    }
                    prefix.append(isLastChild ? QString::fromUtf8("└") : QString::fromUtf8("├"));
                    prefix.append(hasPointerChildren ? QString::fromUtf8("┬") : QString::fromUtf8("─"));
                }
            }
            nextNestingLevel = indentLevel + 1;
        }
        else
        {
            QChar indentChar = '+';
            if (!indentPrefix.isEmpty())
            {
                indentChar = indentPrefix.at(0);
            }

            if (isAttachedNote)
            {
                if (indentDescriptions)
                {
                    int dotCount = (parentNestingLevel > 0) ? parentNestingLevel : std::max(1, indentLevel);
                    prefix = QString(dotCount, '.');
                    nextNestingLevel = dotCount + 1;
                }
            }
            else
            {
                if (node->Content.startsWith('|'))
                {
                    prefix = "";
                }
                else
                {
                    prefix = indentLevel > 0 ? QString(indentLevel, indentChar) : "";
                }

                QString fullLine = prefix + node->Content;
                int oxIndex = fullLine.indexOf("0x", 0, Qt::CaseInsensitive);
                if (oxIndex > 0)
                {
                    nextNestingLevel = oxIndex;
                }
                else
                {
                    nextNestingLevel = prefix.length() + 1;
                }
            }
        }

        QString normalizedContent = node->Content;

        if (isPointerOffset)
        {
            auto match = offsetRegex.match(node->Content);
            if (match.hasMatch())
            {
                QString offsetToken = match.captured(1);
                QString desc = match.captured(3).trimmed();

                if (hasPointerChildren && autoPopulateTemplate)
                {
                    if (desc.isEmpty() || desc.compare("Pointer", Qt::CaseInsensitive) == 0)
                    {
                        desc = pointerTypeTemplate.isEmpty() ? "[32-bit] Pointer" : pointerTypeTemplate;
                    }
                }

                if (!desc.isEmpty())
                {
                    normalizedContent = offsetToken + effectiveDelimiter + desc;
                }
                else
                {
                    normalizedContent = offsetToken;
                }
            }
        }
        else if (isAttachedNote)
        {
            auto enumMatch = attachedEnumKvRegex.match(node->Content);
            auto namedMatch = attachedNamedKvRegex.match(node->Content);

            if (enumMatch.hasMatch())
            {
                QString key = enumMatch.captured(1).trimmed();
                QString val = enumMatch.captured(3).trimmed();
                normalizedContent = key + effectiveDelimiter + val;
            }
            else if (namedMatch.hasMatch())
            {
                QString key = namedMatch.captured(1).trimmed();
                QString val = namedMatch.captured(3).trimmed();
                normalizedContent = key + effectiveDelimiter + val;
            }
            else
            {
                normalizedContent = node->Content.trimmed();
            }
        }

        sb.append(QString("%1%2\r\n").arg(prefix, normalizedContent));

        if (!node->Children.isEmpty())
        {
            QList<bool> nextAncestors = ancestorIsLast;
            if (indentLevel > 0)
            {
                nextAncestors.append(isLastChild);
            }
            sb.append(RebuildTextFromTree(node->Children, indentLevel + 1, useUnicodeTree, indentDescriptions, "", nextAncestors,
                                          nextNestingLevel, effectiveDelimiter, autoPopulateTemplate, pointerTypeTemplate, indentPrefix));
        }
    }
    return sb;
}

} // namespace RALogicEditor
