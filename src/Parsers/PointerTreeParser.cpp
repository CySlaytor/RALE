#include "PointerTreeParser.h"
#include "../Definitions/RaLogicSyntax.h"
#include <QStack>

namespace RALogicEditor {

// Main entry point: Converts a multiline string into a flat list of nodes
// preserving hierarchy via Parent/Children references.
QList<std::shared_ptr<NoteNode>> PointerTreeParser::ParseNoteText(const QString &noteText)
{
    if (noteText.trimmed().isEmpty()) return {};

    QString normalized = noteText;
    normalized.replace("\r\n", "\n").replace('\r', '\n');
    QStringList lines = normalized.split('\n');

    QList<std::shared_ptr<NoteNode>> flatList;

    // Create a root node that represents the "Base" of the note.
    // IndentLevel -2 signifies it is the absolute root container.
    auto root = std::make_shared<NoteNode>();
    root->Offset = "(Default)";
    root->Description = "Full Note";
    root->IndentLevel = -2;
    root->Content = noteText;

    // Attempt to parse the size of the root data from the first line 
    // (useful if the note defines a struct at the base address).
    bool rootHasSizeTag = false;
    if (!lines.isEmpty()) root->Size = ParseSizeFromDescription(lines[0], &rootHasSizeTag);

    flatList.append(root);

    // Stack used to track the current parent at each indentation level.
    QStack<std::shared_ptr<NoteNode>> stack;

    // Logical Root acts as the parent for top-level pointer offsets (Indent 0).
    auto logicalRoot = std::make_shared<NoteNode>();
    logicalRoot->IndentLevel = -1;
    // Attach logicalRoot to root's children to keep it alive for the lifetime of flatList
    root->Children.append(logicalRoot);
    stack.push(logicalRoot);

    std::shared_ptr<NoteNode> lastAddedNode = nullptr;
    bool seenFirstNode = false;

    // Regex supporting both pointer (+0x, -0x) and PF2 array offsets (|0x, |+0x, |-0x)
    // Group 1: Indentation (traditional chars + whitespace + Unicode box drawing chars \u2500-\u257F)
    // Group 2: Sign / Prefix (|, |+, |-, +, -)
    // Group 3: Hex value (0x...).
    // Group 4: Separator (|, =, :). Optional capture.
    // Group 5: Description/Remainder.
    static const QRegularExpression offsetRegex(
        QString::fromUtf8(R"(^([.\+\s\x{2500}-\x{257F}]*)(\|[\+\-]?|[\+\-]?)(0x[0-9A-Fa-f]+)\s*([|=:])?\s*(.*)$)"),
        QRegularExpression::CaseInsensitiveOption);

    // Helper regex to strip structural characters from description/enum lines.
    static const QRegularExpression prefixStripRegex(
        QString::fromUtf8(R"(^([.\+\s\x{2500}-\x{257F}]+))"));

    for (int i = 0; i < lines.size(); i++)
    {
        // TrimEnd instead of Trim ensures leading spaces/visual depth are preserved for the regex
        QString line = lines[i];
        while (!line.isEmpty() && (line.endsWith(' ') || line.endsWith('\t')))
            line.chop(1);

        if (line.trimmed().isEmpty()) continue;

        auto match = offsetRegex.match(line);
        bool isNode = false;

        if (match.hasMatch())
        {
            QString indentStr = match.captured(1);
            QString sign = match.captured(2);
            QString separator = match.captured(4);

            bool hasSign = !sign.isEmpty(); // e.g. +0x10 or |0x00 is definitely an offset
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
        }

        if (isNode)
        {
            seenFirstNode = true;
            QString indentStr = match.captured(1);
            QString sign = match.captured(2);
            QString hex = match.captured(3);
            QString desc = match.captured(5);

            bool isArrayElem = (sign == "|");
            bool isArrayChild = (sign == "|+" || sign == "|-");

            int indent = indentStr.length();
            if (isArrayChild)
            {
                indent += 1;
            }

            // Strip leading separators (- : =) just in case
            while (!desc.isEmpty() && (desc[0] == '-' || desc[0] == ':' || desc[0] == '=' || desc[0].isSpace()))
            {
                desc = desc.mid(1);
            }
            desc = desc.trimmed();

            // Normalization: Ensure offset string carries correct sign/pipeline token
            QString offset;
            if (isArrayElem)
            {
                offset = "|" + hex;
            }
            else if (isArrayChild)
            {
                offset = sign + hex;
            }
            else
            {
                offset = sign + hex;
                if (!offset.startsWith('+') && !offset.startsWith('-')) offset = "+" + offset;
            }

            int64_t size = ParseSizeFromDescription(desc);

            auto node = std::make_shared<NoteNode>();
            node->Offset = offset;
            node->Description = desc;
            node->RawLineIndex = i;
            node->IndentLevel = indent;
            node->Size = size;
            node->IsArrayElement = isArrayElem;
            node->IsArrayChild = isArrayChild;

            // Tree Construction:
            while (!stack.isEmpty() && stack.top()->IndentLevel >= indent)
                stack.pop();

            // Safety fallback
            if (stack.isEmpty()) stack.push(logicalRoot);

            auto parent = stack.top();
            node->Parent = parent.get();
            parent->Children.append(node);

            stack.push(node);

            lastAddedNode = node;
        }
        else
        {
            if (!seenFirstNode && !rootHasSizeTag)
            {
                bool foundTag = false;
                int64_t candidateSize = ParseSizeFromDescription(line, &foundTag);
                if (foundTag)
                {
                    root->Size = candidateSize;
                    rootHasSizeTag = true;
                }
            }

            // Append to content of most recent node
            if (lastAddedNode != nullptr)
            {
                if (!lastAddedNode->Content.isEmpty()) lastAddedNode->Content += "\r\n";
                // Remove structural prefixes (like │ ) from description content to keep tooltips clean
                QString cleanContentLine = line;
                cleanContentLine.remove(prefixStripRegex);
                lastAddedNode->Content += cleanContentLine.trimmed();
            }
        }
    }

    // Recursively flatten the tree for the dropdown list.
    CollectAllNodes(logicalRoot, flatList);
    return flatList;
}

// Parses data size from tags in the description (e.g., [16-bit] or [32 bytes] or [30x16 bytes]).
int64_t PointerTreeParser::ParseSizeFromDescription(const QString &desc, bool *foundTag)
{
    if (foundTag != nullptr) *foundTag = false;
    if (desc.isEmpty()) return 1;

    // 1. Check for explicit byte count or 2D stride: [144 bytes], [30x16 bytes], [ASCII: 20-bytes]
    static const QRegularExpression bytesRegex(
        R"(\[[^\]]*?\b(?:(\d+)\s*x\s*(\d+)|(0x[0-9A-Fa-f]+)|(\d+))[\s\-]*bytes?\b[^\]]*\])",
        QRegularExpression::CaseInsensitiveOption);
    auto bytesMatch = bytesRegex.match(desc);
    if (bytesMatch.hasMatch())
    {
        if (!bytesMatch.captured(1).isEmpty() && !bytesMatch.captured(2).isEmpty())
        {
            bool ok1 = false, ok2 = false;
            int64_t w = bytesMatch.captured(1).toLongLong(&ok1);
            int64_t h = bytesMatch.captured(2).toLongLong(&ok2);
            if (ok1 && ok2 && w > 0 && h > 0)
            {
                if (foundTag != nullptr) *foundTag = true;
                return w * h;
            }
        }
        else if (!bytesMatch.captured(3).isEmpty())
        {
            bool ok = false;
            int64_t bSize = bytesMatch.captured(3).mid(2).toLongLong(&ok, 16);
            if (ok && bSize > 0)
            {
                if (foundTag != nullptr) *foundTag = true;
                return bSize;
            }
        }
        else if (!bytesMatch.captured(4).isEmpty())
        {
            bool ok = false;
            int64_t bSize = bytesMatch.captured(4).toLongLong(&ok);
            if (ok && bSize > 0)
            {
                if (foundTag != nullptr) *foundTag = true;
                return bSize;
            }
        }
    }

    // 2. Check for standard type tags
    static const QRegularExpression bits64Regex(R"(\[[^\]]*\b(?:64[\s\-]*bits?|Double64)\b[^\]]*\])", QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression bits32Regex(R"(\[[^\]]*\b(?:32[\s\-]*bits?|Float(?:32)?|MBF32(?:\s+LE)?|Double32(?:\s+BE)?)\b[^\]]*\])", QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression bits24Regex(R"(\[[^\]]*\b24[\s\-]*bits?\b[^\]]*\])", QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression bits16Regex(R"(\[[^\]]*\b16[\s\-]*bits?\b[^\]]*\])", QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression bits8Regex(R"(\[[^\]]*\b8[\s\-]*bits?\b[^\]]*\])", QRegularExpression::CaseInsensitiveOption);

    if (bits64Regex.match(desc).hasMatch()) { if (foundTag != nullptr) *foundTag = true; return 8; }
    if (bits32Regex.match(desc).hasMatch()) { if (foundTag != nullptr) *foundTag = true; return 4; }
    if (bits24Regex.match(desc).hasMatch()) { if (foundTag != nullptr) *foundTag = true; return 3; }
    if (bits16Regex.match(desc).hasMatch()) { if (foundTag != nullptr) *foundTag = true; return 2; }
    if (bits8Regex.match(desc).hasMatch()) { if (foundTag != nullptr) *foundTag = true; return 1; }

    return 1; // Default to 8-bit (1 byte)
}

// Helper to traverse tree and populate the list.
void PointerTreeParser::CollectAllNodes(const std::shared_ptr<NoteNode> &node, QList<std::shared_ptr<NoteNode>> &flatList)
{
    if (node->IndentLevel != -1) flatList.append(node);
    for (const auto &child : node->Children) CollectAllNodes(child, flatList);
}

// Constructs the RA Logic String (e.g., I:0x1234_0x10) for a specific node in the tree.
std::tuple<QString, QString, QString> PointerTreeParser::GeneratePointerLogic(
    const CodeNote &baseNote,
    const std::shared_ptr<NoteNode> &selectedNode,
    int consoleId)
{
    if (baseNote.Address.isEmpty()) return std::make_tuple("", "", "");

    // Base Address cleanup
    QString baseAddrStr = baseNote.Address.startsWith("0x", Qt::CaseInsensitive)
        ? baseNote.Address.mid(2)
        : baseNote.Address;
    QString type = "Mem";
    QString size = "32-bit";

    // Parse the root note header to determine the pointer type and whether this note is a static root array
    QString noteHeader = "";
    QString normalizedNote = baseNote.Note;
    normalizedNote.replace("\r\n", "\n").replace('\r', '\n');
    QStringList headerLines = normalizedNote.split('\n', Qt::SkipEmptyParts);
    if (!headerLines.isEmpty()) noteHeader = headerLines.first();

    // Check if the base address is a static array structure in RAM
    static const QRegularExpression arrayTagRx(R"(\[[^\]]*?\b(?:(\d+)\s*x\s*(\d+)|(0x[0-9A-Fa-f]+)|(\d+))[\s\-]*bytes?\b[^\]]*\])", QRegularExpression::CaseInsensitiveOption);
    bool hasArrayTag = arrayTagRx.match(noteHeader).hasMatch();
    bool hasPointerTag = noteHeader.contains("Pointer", Qt::CaseInsensitive);
    bool isStaticRootArray = (hasArrayTag && !hasPointerTag);

    QString prefixStr = RaLogicSyntax::GetPlatformPointerPrefix(consoleId);
    QString maskStr = RaLogicSyntax::GetPlatformPointerMask(consoleId);

    if (noteHeader.contains("Pointer", Qt::CaseInsensitive))
    {
        prefixStr = "X";
        if (noteHeader.contains("BE", Qt::CaseInsensitive))
        {
            prefixStr = "G";
            maskStr = "&536870911";
        }
        else if (noteHeader.contains("24-bit", Qt::CaseInsensitive))
        {
            prefixStr = "W";
            maskStr = "";
        }
        else if (noteHeader.contains("16-bit", Qt::CaseInsensitive))
        {
            prefixStr = "";
            maskStr = "";
        }
    }

    // Check for explicit mask in note (e.g. &0xFFFFFF or &536870911)
    static const QRegularExpression maskRegex(R"(&\s*(?:0x([0-9A-Fa-f]+)|(\d+)))");
    auto maskMatch = maskRegex.match(noteHeader);
    if (maskMatch.hasMatch())
    {
        if (!maskMatch.captured(1).isEmpty())
        {
            bool ok = false;
            int64_t val = maskMatch.captured(1).toLongLong(&ok, 16);
            if (ok) maskStr = "&" + QString::number(val);
        }
        else if (!maskMatch.captured(2).isEmpty())
        {
            maskStr = "&" + maskMatch.captured(2);
        }
    }

    QString sb;
    QString descriptionForType = "";

    // Build the chain if selecting a child node
    if (selectedNode != nullptr && selectedNode->IndentLevel != -2)
    {
        descriptionForType = selectedNode->Description;

        // Collect lineage from top-level down to selected node
        QList<NoteNode*> lineage;
        NoteNode* cur = selectedNode.get();
        while (cur != nullptr && cur->IndentLevel != -1)
        {
            lineage.prepend(cur);
            cur = cur->Parent;
        }

        auto ParseHexClean = [](const QString &offsetStr) -> int64_t {
            QString clean = offsetStr;
            clean.remove('|').remove('+').remove("0x", Qt::CaseInsensitive);
            clean = clean.trimmed();
            bool ok = false;
            int64_t v = clean.toLongLong(&ok, 16);
            if (ok && offsetStr.contains('-')) return -v;
            return ok ? v : 0;
        };

        if (isStaticRootArray && !lineage.isEmpty())
        {
            // Static Root Array: Top element offset represents direct memory addition in RAM
            bool okBase = false;
            int64_t baseNum = baseAddrStr.toLongLong(&okBase, 16);
            int64_t elemOffset = ParseHexClean(lineage.first()->Offset);
            int64_t directAddr = baseNum + elemOffset;

            if (lineage.size() == 1)
            {
                // Direct memory read without indirection
                sb.append(QString("0x%1").arg(directAddr, 8, 16, QChar('0')));
            }
            else
            {
                // Pointer dereference starting from array element
                QString directHex = QString("%1").arg(directAddr, 8, 16, QChar('0'));
                sb.append(QString("I:0x%1%2%3").arg(prefixStr, directHex, maskStr));

                for (int i = 1; i < lineage.size() - 1; ++i)
                {
                    int64_t offVal = ParseHexClean(lineage[i]->Offset);
                    sb.append(QString("_I:0x%1%2%3").arg(prefixStr, QString("%1").arg(offVal, 8, 16, QChar('0')), maskStr));
                }

                int64_t lastOff = ParseHexClean(lineage.last()->Offset);
                sb.append(QString("_0x%1").arg(lastOff, 8, 16, QChar('0')));
            }
        }
        else
        {
            // Standard Pointer Chain
            sb.append(QString("I:0x%1%2%3").arg(prefixStr, baseAddrStr, maskStr));

            for (int i = 0; i < lineage.size() - 1; ++i)
            {
                int64_t offVal = ParseHexClean(lineage[i]->Offset);
                sb.append(QString("_I:0x%1%2%3").arg(prefixStr, QString("%1").arg(offVal, 8, 16, QChar('0')), maskStr));
            }

            if (!lineage.isEmpty())
            {
                int64_t lastOff = ParseHexClean(lineage.last()->Offset);
                sb.append(QString("_0x%1").arg(lastOff, 8, 16, QChar('0')));
            }
        }
    }
    else
    {
        // Root selection
        sb.append(QString("0x%1").arg(baseAddrStr));
        descriptionForType = noteHeader;
    }

    // Attempt to detect type/size from the description (e.g. [Float], [16-bit])
    static const QRegularExpression typeRegex(R"(\[(.*?\]))");
    auto typeMatch = typeRegex.match(descriptionForType);
    if (typeMatch.hasMatch())
    {
        QString tag = typeMatch.captured(1);
        if (tag.endsWith(']')) tag.chop(1);
        if (tag.compare("Float", Qt::CaseInsensitive) == 0 || tag.compare("Flaot", Qt::CaseInsensitive) == 0)
        {
            type = "Mem";
            size = "Float";
        }
        else
        {
            size = tag;
            type = "Mem";
        }
    }
    else
    {
        size = "8-bit"; // Default fallback
    }

    return std::make_tuple(sb, type, size);
}

// Reverse-engineers the chain of offsets from a specific node up to the root.
QList<int64_t> PointerTreeParser::GetOffsetsFromNode(const std::shared_ptr<NoteNode> &node)
{
    QList<int64_t> nodeChain;
    NoteNode* current = node.get();
    while (current != nullptr && current->IndentLevel != -1)
    {
        QString raw = current->Offset;
        while (raw.startsWith('|') || raw.startsWith('+')) raw = raw.mid(1);
        QString clean = raw;
        clean.remove("0x", Qt::CaseInsensitive);

        bool ok = false;
        int64_t val = clean.toLongLong(&ok, 16);
        if (ok)
        {
            if (current->Offset.contains('-')) val = -val;
            nodeChain.prepend(val);
        }
        else
            return {}; // Invalid chain

        current = current->Parent;
    }
    return nodeChain;
}

} // namespace RALogicEditor
