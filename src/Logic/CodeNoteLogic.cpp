#include "CodeNoteLogic.h"
#include <QRegularExpression>
#include <QStack>
#include <QMap>
#include <algorithm>

namespace RALogicEditor {

// Logic: Parses raw code note text, merges duplicate offsets, fixes hierarchy indentation, and sorts results.
QString CodeNoteLogic::FixHierarchy(const QString &inputText, const QString &indentStylePref, bool indentDescriptions)
{
    if (inputText.trimmed().isEmpty()) return "";

    QString normalized = inputText;
    normalized.replace("\r\n", "\n").replace('\r', '\n');
    QStringList lines = normalized.split('\n');

    QString style = indentStylePref;
    if (style.startsWith("Auto Detect", Qt::CaseInsensitive) || style.isEmpty())
    {
        QChar detectedIndent = DetectIndentStyle(lines);
        if (detectedIndent == 'U') style = "Unicode Tree (├─)";
        else style = (detectedIndent == '.') ? "Dots (.)" : "Plusses (+)";
    }

    // 1. Parse the flat text into a tree structure.
    auto parsedTree = ParseLinesToTree(lines);

    // 2. Recursively merge AND sort nodes.
    auto mergedTree = MergeTree(parsedTree);

    // 3. Rebuild the text from the cleaned tree structure, using the detected style and user preference.
    return RebuildTextFromTree(mergedTree, 0, style, indentDescriptions).trimmed();
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

QList<std::shared_ptr<CodeNoteLogic::CodeNoteNode>> CodeNoteLogic::ParseLinesToTree(const QStringList &lines)
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

    // Regex matching AutoCR's robust structural detection
    static const QRegularExpression offsetRegex(
        QString::fromUtf8(R"(^([.\+\s\x{2500}-\x{257F}]*)([-+]?)(0x[0-9A-Fa-f]+)\s*([|=:])?\s*(.*)$)"),
        QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression prefixStripRegex(
        QString::fromUtf8(R"(^([.\+\s\x{2500}-\x{257F}]+))"));

    for (const QString &line : lines)
    {
        QString untrimmedLine = line;
        while (!untrimmedLine.isEmpty() && (untrimmedLine.endsWith(' ') || untrimmedLine.endsWith('\t')))
            untrimmedLine.chop(1);

        if (untrimmedLine.trimmed().isEmpty() || untrimmedLine.trimmed().startsWith("---"))
        {
            lineIndex++;
            continue;
        }

        int indent = 0;
        bool isNode = false;

        // CRITICAL FIX: Match against untrimmedLine to preserve leading spaces for indentation detection!
        // If we match against trimmedLine, Unicode styles (e.g. "  ├─") lose their spaces and get flattened to indent=2.
        auto match = offsetRegex.match(untrimmedLine);

        if (match.hasMatch())
        {
            QString indentStr = match.captured(1);
            QString sign = match.captured(2);
            QString separator = match.captured(4);

            bool hasSign = !sign.isEmpty();
            bool hasIndent = indentStr.length() > 0;

            if (hasSign)
            {
                isNode = true;
            }
            else if (indentStr.contains('+'))
            {
                isNode = true; // Treat any indentation with '+' as a node
            }
            else if (hasIndent)
            {
                // Indented with dots/spaces/box only, no sign (e.g. "..0x04").
                if (separator == "|")
                {
                    isNode = true;
                }
            }

            if (isNode)
            {
                // Safely recover sign if the greedy group 1 ate it
                if (sign.isEmpty())
                {
                    if (indentStr.endsWith('+')) { sign = "+"; indentStr.chop(1); }
                    else if (indentStr.endsWith('-')) { sign = "-"; indentStr.chop(1); }
                }

                indent = indentStr.length();

                QString hex = match.captured(3);
                QString remainder = match.captured(5);

                // Overwrite untrimmed line completely to cleanly detach the payload from the visual hierarchy
                untrimmedLine = QString("%1%2 %3 %4").arg(sign, hex, separator, remainder).trimmed();
            }
        }

        if (indent < 0) indent = 0;

        bool isAttachedNote = (indent == 0) && (lastNode != nullptr) &&
                              !(untrimmedLine.trimmed().startsWith("+0x", Qt::CaseInsensitive) ||
                                untrimmedLine.trimmed().startsWith("-0x", Qt::CaseInsensitive) ||
                                untrimmedLine.trimmed().startsWith('['));

        if (isAttachedNote)
        {
            QString cleanContentLine = untrimmedLine;
            cleanContentLine.remove(prefixStripRegex);
            auto childNode = std::make_shared<CodeNoteNode>();
            childNode->Content = cleanContentLine.trimmed();
            childNode->OriginalIndex = lineIndex;
            lastNode->Children.append(childNode);
        }
        else
        {
            while (stack.top().indent >= indent)
            {
                stack.pop();
            }

            QString content = isNode ? untrimmedLine : QString(untrimmedLine).remove(prefixStripRegex).trimmed();
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
    // Preserve first-seen group order like C#'s Dictionary + stable OrderBy;
    // QMap would iterate keys alphabetically and reorder equal offsets.
    struct MergeGroup { QString Key; QList<std::shared_ptr<CodeNoteNode>> Nodes; };
    QList<MergeGroup> mergeableGroups;
    QList<std::shared_ptr<CodeNoteNode>> nonMergeableNodes;
    static const QRegularExpression offsetRegex(R"(^([\+\-]0x[0-9a-fA-F]+))", QRegularExpression::CaseInsensitiveOption);

    // First pass: Group all mergeable nodes by their normalized offset key.
    for (const auto &node : nodes)
    {
        auto match = offsetRegex.match(node->Content);
        if (match.hasMatch())
        {
            // Normalize the key to handle case (+0xAB vs +0xab) and padding (+0x4 vs +0x04).
            QString rawKey = match.captured(1);
            QString sign = rawKey.left(1);
            bool ok = false;
            int64_t offsetValue = rawKey.mid(3).toLongLong(&ok, 16);
            QString key = sign + "0x" + QString::number(offsetValue, 16);

            bool found = false;
            for (auto &group : mergeableGroups)
            {
                if (group.Key == key)
                {
                    group.Nodes.append(node);
                    found = true;
                    break;
                }
            }
            if (!found)
                mergeableGroups.append(MergeGroup{ key, { node } });
        }
        else
        {
            nonMergeableNodes.append(node);
        }
    }

    // Process mergeable groups.
    static const QRegularExpression contentRegex(R"(^([\+\-]0x)([0-9a-fA-F]+)(.*))", QRegularExpression::CaseInsensitiveOption);
    for (const auto &mergeGroup : mergeableGroups)
    {
        const auto &group = mergeGroup.Nodes;
        auto representative = *std::min_element(group.begin(), group.end(), [](const auto &a, const auto &b) {
            return a->OriginalIndex < b->OriginalIndex;
        });

        // Select the content with the longest description to preserve information.
        auto bestIt = std::max_element(group.begin(), group.end(), [](const auto &a, const auto &b) {
            return a->Content.length() < b->Content.length();
        });
        QString bestContent = (bestIt != group.end()) ? (*bestIt)->Content : representative->Content;

        // Normalize the chosen content for consistent output format (e.g., +0x0, lowercase hex).
        auto contentMatch = contentRegex.match(bestContent);
        if (contentMatch.hasMatch())
        {
            QString signAndPrefix = contentMatch.captured(1).toLower();
            int64_t offsetVal = contentMatch.captured(2).toLongLong(nullptr, 16);
            QString restOfLine = contentMatch.captured(3);
            bestContent = QString("%1%2%3").arg(signAndPrefix).arg(offsetVal, 0, 16).arg(restOfLine);
        }

        auto newNode = std::make_shared<CodeNoteNode>();
        newNode->Content = bestContent;
        newNode->OriginalIndex = representative->OriginalIndex;

        // Combine all children from all nodes in the group.
        QList<std::shared_ptr<CodeNoteNode>> allChildren;
        for (const auto &n : group)
        {
            allChildren.append(n->Children);
        }

        if (!allChildren.isEmpty())
        {
            // Recursively merge children to properly combine sub-branches
            newNode->Children.append(MergeTree(allChildren));
        }
        finalNodes.append(newNode);
    }

    // Process non-mergeable nodes, which just pass through but have their children merged.
    for (const auto &node : nonMergeableNodes)
    {
        if (!node->Children.isEmpty())
        {
            auto mergedChildren = MergeTree(node->Children);
            node->Children = mergedChildren;
        }
        finalNodes.append(node);
    }

    // Finally, sort the processed nodes at this level.
    // This ensures offsets are numerically sorted, while other content stays in place.
    return SortNodes(finalNodes);
}

// Helper method to sort nodes intelligently.
QList<std::shared_ptr<CodeNoteLogic::CodeNoteNode>> CodeNoteLogic::SortNodes(const QList<std::shared_ptr<CodeNoteNode>> &nodes)
{
    static const QRegularExpression offsetRegex(R"(^[\+\-]0x([0-9a-fA-F]+))", QRegularExpression::CaseInsensitiveOption);

    auto sorted = nodes;

    // Logic:
    // 1. Primary Sort: Non-offsets (Group 0) come before Offsets (Group 1).
    // 2. Secondary Sort:
    //    - If it's an offset (Group 1), sort by the numeric hexadecimal value.
    //    - If it's not an offset (Group 0), sort by OriginalIndex to preserve file order.
    std::stable_sort(sorted.begin(), sorted.end(), [](const auto &a, const auto &b) {
        bool aIsOffset = offsetRegex.match(a->Content).hasMatch();
        bool bIsOffset = offsetRegex.match(b->Content).hasMatch();

        int aGroup = aIsOffset ? 1 : 0;
        int bGroup = bIsOffset ? 1 : 0;

        if (aGroup != bGroup)
            return aGroup < bGroup;

        if (aIsOffset)
        {
            int64_t aVal = offsetRegex.match(a->Content).captured(1).toLongLong(nullptr, 16);
            int64_t bVal = offsetRegex.match(b->Content).captured(1).toLongLong(nullptr, 16);
            return aVal < bVal;
        }

        return a->OriginalIndex < b->OriginalIndex;
    });

    return sorted;
}

// Integrated spacing logic directly into the rebuild function.
// Now supports robust Unicode box-drawing Tree generations.
QString CodeNoteLogic::RebuildTextFromTree(
    const QList<std::shared_ptr<CodeNoteNode>> &nodes,
    int indentLevel,
    const QString &style,
    bool indentDescriptions,
    QList<bool> ancestorIsLast)
{
    QString sb;

    for (int i = 0; i < nodes.size(); i++)
    {
        const auto &node = nodes[i];
        bool isLastChild = (i == nodes.size() - 1);

        // Check if any actual pointer descendants exist to know whether to draw a "┬" or "─"
        bool hasPointerChildren = false;
        for (const auto &c : node->Children)
        {
            if (c->Content.startsWith("+0x", Qt::CaseInsensitive) || c->Content.startsWith("-0x", Qt::CaseInsensitive))
            {
                hasPointerChildren = true;
                break;
            }
        }

        // Add a blank line before any top-level node except the very first one.
        if (indentLevel == 0 && !sb.isEmpty())
        {
            sb += "\r\n";
        }

        bool isPointerOffset = node->Content.startsWith("+0x", Qt::CaseInsensitive) || node->Content.startsWith("-0x", Qt::CaseInsensitive);
        bool isAttachedNote = !isPointerOffset;

        QString prefix = "";

        if (style.startsWith("Unicode Tree"))
        {
            if (isAttachedNote)
            {
                if (indentDescriptions && indentLevel > 0)
                {
                    for (int a = 0; a < indentLevel - 1; a++)
                    {
                        prefix += ancestorIsLast[a] ? "  " : QString::fromUtf8("│ ");
                    }
                    prefix += isLastChild ? "   " : QString::fromUtf8("│  ");
                }
            }
            else
            {
                if (indentLevel > 0)
                {
                    for (int a = 0; a < indentLevel - 1; a++)
                    {
                        prefix += ancestorIsLast[a] ? "  " : QString::fromUtf8("│ ");
                    }
                    prefix += isLastChild ? QString::fromUtf8("└") : QString::fromUtf8("├");
                    prefix += hasPointerChildren ? QString::fromUtf8("┬") : QString::fromUtf8("─");
                    prefix += " ";
                }
            }
        }
        else
        {
            char indentChar = style.startsWith("Plusses") ? '+' : '.';

            if (isAttachedNote)
            {
                if (indentDescriptions)
                {
                    prefix = (indentLevel > 0) ? QString(indentLevel, '.') : "";
                }
            }
            else
            {
                prefix = (indentLevel > 0) ? QString(indentLevel, indentChar) : "";
            }
        }

        sb += QString("%1%2\r\n").arg(prefix, node->Content);

        if (!node->Children.isEmpty())
        {
            QList<bool> newAncestorIsLast;
            if (indentLevel == 0)
            {
                // Start empty: every deeper level appends its parent's isLast flag below.
                // (Seeding this with the root's flag shifted every column by one level.)
                newAncestorIsLast = QList<bool>();
            }
            else
            {
                newAncestorIsLast = ancestorIsLast;
                newAncestorIsLast.append(isLastChild);
            }
            sb += RebuildTextFromTree(node->Children, indentLevel + 1, style, indentDescriptions, newAncestorIsLast);
        }
    }

    return sb;
}

} // namespace RALogicEditor
