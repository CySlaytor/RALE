#include "PointerTreeParser.h"
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
    if (!lines.isEmpty()) root->Size = ParseSizeFromDescription(lines[0]);

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

    // Regex to parse specific pointer lines.
    // Group 1: Indentation (traditional chars + whitespace + Unicode box drawing chars \u2500-\u257F)
    // Group 2: Sign (+ or -). Optional capture.
    // Group 3: Hex value (0x...).
    // Group 4: Separator (|, =, :). Optional capture.
    // Group 5: Description/Remainder.
    static const QRegularExpression offsetRegex(
        QString::fromUtf8(R"(^([.\+\s\x{2500}-\x{257F}]*)([-+]?)(0x[0-9A-Fa-f]+)\s*([|=:])?\s*(.*)$)"),
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

            bool hasSign = !sign.isEmpty(); // e.g. +0x10 is definitely an offset
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
            // Natural depth tracking: visual prefix length dictates the hierarchy level
            QString indentStr = match.captured(1);
            int indent = indentStr.length();

            QString sign = match.captured(2);
            QString hex = match.captured(3);
            QString desc = match.captured(5);

            // Strip leading separators (- : =) just in case
            while (!desc.isEmpty() && (desc[0] == '-' || desc[0] == ':' || desc[0] == '=' || desc[0].isSpace()))
            {
                desc = desc.mid(1);
            }
            desc = desc.trimmed();

            // Normalization: Ensure offset string always has a sign for logic generation consistency.
            QString offset = sign + hex;
            if (!offset.startsWith('+') && !offset.startsWith('-')) offset = "+" + offset;

            int64_t size = ParseSizeFromDescription(desc);

            auto node = std::make_shared<NoteNode>();
            node->Offset = offset;
            node->Description = desc;
            node->RawLineIndex = i;
            node->IndentLevel = indent;
            node->Size = size;

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

// Parses data size from tags in the description (e.g., [16-bit] or [32 bytes]).
int64_t PointerTreeParser::ParseSizeFromDescription(const QString &desc)
{
    if (desc.isEmpty()) return 1;

    // 1. Check for explicit byte count: [X bytes], [ASCII: 20-bytes], [20-byte]
    // Allow characters before the digits (like ASCII: ) and allow dash in byte(s)
    static const QRegularExpression bytesRegex(R"(\[.*?(\d+)\s*[-]?\s*bytes?.*?\])", QRegularExpression::CaseInsensitiveOption);
    auto bytesMatch = bytesRegex.match(desc);
    if (bytesMatch.hasMatch())
    {
        bool ok = false;
        int64_t bSize = bytesMatch.captured(1).toLongLong(&ok);
        if (ok) return bSize;
    }

    // 2. Check for standard type tags
    if (desc.contains("[32-bit", Qt::CaseInsensitive) || desc.contains("[Float", Qt::CaseInsensitive)) return 4;
    if (desc.contains("[24-bit", Qt::CaseInsensitive)) return 3;
    if (desc.contains("[16-bit", Qt::CaseInsensitive)) return 2;
    if (desc.contains("[8-bit", Qt::CaseInsensitive)) return 1;

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
    const std::shared_ptr<NoteNode> &selectedNode)
{
    if (baseNote.Address.isEmpty()) return std::make_tuple("", "", "");

    // Base Address cleanup
    QString baseAddrStr = baseNote.Address.startsWith("0x", Qt::CaseInsensitive)
        ? baseNote.Address.mid(2)
        : baseNote.Address;
    QString type = "Mem";
    QString size = "32-bit";

    // Parse the root note header to determine the pointer type (32-bit, 24-bit, BE, etc.)
    QString noteHeader = "";
    QString normalizedNote = baseNote.Note;
    normalizedNote.replace("\r\n", "\n").replace('\r', '\n');
    QStringList headerLines = normalizedNote.split('\n', Qt::SkipEmptyParts);
    if (!headerLines.isEmpty()) noteHeader = headerLines.first();

    QChar ptrPrefix = ' ';
    bool defaultMask = false;

    // Robust case-insensitive pointer detection
    if (noteHeader.contains("Pointer", Qt::CaseInsensitive))
    {
        // Default to 32-bit standard if just "Pointer" is found
        ptrPrefix = 'X';

        // Check for specific attributes
        if (noteHeader.contains("BE", Qt::CaseInsensitive))
        {
            ptrPrefix = 'G'; // Big Endian
            defaultMask = true; // Default mask for BE pointers
        }

        if (noteHeader.contains("24-bit", Qt::CaseInsensitive))
        {
            ptrPrefix = 'W';
        }
        else if (noteHeader.contains("16-bit", Qt::CaseInsensitive))
        {
            // 16-bit pointer usually implies standard memory read (no prefix)
            ptrPrefix = ' ';
        }
    }
    // Fallback for legacy/explicit exact matches just in case
    else if (noteHeader.contains("32-bit BE", Qt::CaseInsensitive)) { ptrPrefix = 'G'; defaultMask = true; }
    else if (noteHeader.contains("24-bit", Qt::CaseInsensitive)) ptrPrefix = 'W';

    // Check for explicit mask in note (e.g. &0xFFFFFF)
    int64_t explicitMaskVal = -1;
    static const QRegularExpression maskRegex(R"(&\s*(?:0x)?([0-9A-Fa-f]+))");
    auto maskMatch = maskRegex.match(noteHeader);
    if (maskMatch.hasMatch())
    {
        bool ok = false;
        explicitMaskVal = maskMatch.captured(1).toLongLong(&ok, 16);
        if (!ok) explicitMaskVal = -1;
    }

    QString prefixStr = (ptrPrefix == ' ') ? "" : QString(ptrPrefix);
    QString maskStr = "";
    if (explicitMaskVal != -1) maskStr = "&" + QString::number(explicitMaskVal);
    else if (defaultMask) maskStr = "&536870911"; // Default 29-bit mask for BE pointers (0x1FFFFFFF)

    QString sb;
    QString descriptionForType = "";

    // Build the chain if selecting a child node
    if (selectedNode != nullptr && selectedNode->IndentLevel != -2)
    {
        QStringList chain;
        NoteNode* current = selectedNode.get();
        descriptionForType = selectedNode->Description;

        // Walk up the tree to collect offsets
        while (current != nullptr && current->IndentLevel != -1)
        {
            QString hex = current->Offset;
            while (hex.startsWith('+') || hex.startsWith('-')) hex = hex.mid(1);
            hex.remove("0x", Qt::CaseInsensitive);
            if (hex.length() < 8) hex = hex.rightJustified(8, '0');
            chain.prepend(hex);
            current = current->Parent;
        }

        // Format: I:Base_I:Offset1_Offset2
        sb.append(QString("I:0x%1%2%3").arg(prefixStr, baseAddrStr, maskStr));

        // Add intermediate offsets as "Add Address" (I:)
        for (int i = 0; i < chain.size() - 1; i++)
            sb.append(QString("_I:0x%1%2%3").arg(prefixStr, chain[i], maskStr));

        // Add final offset (The read target)
        if (!chain.isEmpty())
            sb.append(QString("_0x%1").arg(chain.last()));
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
        while (raw.startsWith('+')) raw = raw.mid(1);
        QString clean = raw;
        clean.remove("0x", Qt::CaseInsensitive);

        bool ok = false;
        int64_t val = clean.toLongLong(&ok, 16);
        if (ok)
            nodeChain.prepend(val);
        else
            return {}; // Invalid chain

        current = current->Parent;
    }
    return nodeChain;
}

} // namespace RALogicEditor
