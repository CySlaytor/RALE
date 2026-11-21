#include "PointerTreeParser.h"

#include "../Definitions/RaLogicSyntax.h"

#include <QMap>
#include <QStack>

#include <functional>

namespace RALE {

int64_t PointerTreeParser::ParseOffsetValue(const QString& offsetStr) {
    QString clean = offsetStr.trimmed();
    clean.remove('|');
    clean = clean.trimmed();
    bool isNeg = clean.startsWith('-');
    if (isNeg || clean.startsWith('+'))
        clean = clean.mid(1).trimmed();
    if (clean.startsWith("0x", Qt::CaseInsensitive))
        clean = clean.mid(2).trimmed();
    bool ok = false;
    int64_t v = clean.toLongLong(&ok, 16);
    if (!ok)
        return 0;
    return isNeg ? -v : v;
}

bool PointerTreeParser::IsPointerLikeNode(const std::shared_ptr<NoteNode>& node) {
    if (node == nullptr || node->IsInlineArray)
        return false;
    if (!node->Children.isEmpty())
        return true;
    return node->Description.contains("Pointer", Qt::CaseInsensitive) || node->Description.contains('&');
}

void PointerTreeParser::MergeDuplicateNodes(QList<std::shared_ptr<NoteNode>>& nodes) {
    if (nodes.isEmpty())
        return;

    QList<std::shared_ptr<NoteNode>> merged;
    QMap<QString, std::shared_ptr<NoteNode>> seenByKey;

    for (const auto& node : nodes) {
        if (node == nullptr)
            continue;

        if (node->Offset.trimmed().isEmpty()) {
            merged.append(node);
            continue;
        }

        int64_t offVal = ParseOffsetValue(node->Offset);
        QString kindPrefix = node->IsArrayElement ? "E:" : (node->IsArrayChild ? "C:" : "N:");
        QString key = kindPrefix + QString::number(offVal);

        auto it = seenByKey.find(key);
        if (it == seenByKey.end()) {
            seenByKey.insert(key, node);
            merged.append(node);
        } else {
            auto primary = it.value();
            if (node->Description.length() > primary->Description.length()) {
                primary->Description = node->Description;
                if (node->IsInlineArray || node->IsPointerArray || node->Size > primary->Size) {
                    primary->Size = node->Size;
                    primary->ArrayStride = node->ArrayStride;
                    primary->IsInlineArray = node->IsInlineArray;
                    primary->IsPointerArray = node->IsPointerArray;
                }
            } else if ((node->IsInlineArray || node->IsPointerArray) && !primary->IsInlineArray && !primary->IsPointerArray) {
                primary->Size = node->Size;
                primary->ArrayStride = node->ArrayStride;
                primary->IsInlineArray = node->IsInlineArray;
                primary->IsPointerArray = node->IsPointerArray;
            }

            if (primary->Content.isEmpty() && !node->Content.isEmpty()) {
                primary->Content = node->Content;
            }

            for (const auto& child : node->Children) {
                if (child != nullptr) {
                    child->Parent = primary.get();
                    primary->Children.append(child);
                }
            }
        }
    }

    for (const auto& node : merged) {
        MergeDuplicateNodes(node->Children);
    }

    nodes = merged;
}

void PointerTreeParser::CloneTemplateSubtree(const std::shared_ptr<NoteNode>& templateParent,
                                             const std::shared_ptr<NoteNode>& targetParent) {
    if (templateParent == nullptr || targetParent == nullptr)
        return;

    for (const auto& src : templateParent->Children) {
        if (src == nullptr || src->IsTemplateInherited)
            continue;

        auto dst = std::make_shared<NoteNode>();
        int64_t offVal = ParseOffsetValue(src->Offset);
        QString offHex = QString::number(std::abs(offVal), 16).toLower();
        dst->Offset = (offVal < 0 ? "-0x" : "+0x") + offHex;
        dst->Description = src->Description;
        dst->Size = src->Size;
        dst->ArrayStride = src->ArrayStride;
        dst->RawLineIndex = src->RawLineIndex;
        dst->IndentLevel = targetParent->IndentLevel + 1;
        dst->Content = src->Content;
        dst->Parent = targetParent.get();
        dst->IsArrayElement = false;
        dst->IsArrayChild = false;
        dst->IsInlineArray = src->IsInlineArray;
        dst->IsPointerArray = src->IsPointerArray;
        dst->IsTemplateInherited = true;
        dst->TemplateSource = src;

        CloneTemplateSubtree(src, dst);
        targetParent->Children.append(dst);
    }
}

void PointerTreeParser::ApplyArrayTemplates(const std::shared_ptr<NoteNode>& root, const std::shared_ptr<NoteNode>& logicalRoot) {
    if (logicalRoot == nullptr)
        return;

    auto applyToContainer = [](const std::shared_ptr<NoteNode>& parentNode, int64_t arrOffset, int64_t arrSize, int64_t arrStride,
                               bool useArrayElemTemplates) {
        if (parentNode == nullptr || arrSize <= 0)
            return;

        QMap<int64_t, std::shared_ptr<NoteNode>> templateMap;
        for (const auto& child : parentNode->Children) {
            if (child == nullptr || child->IsTemplateInherited)
                continue;
            if (useArrayElemTemplates && !child->IsArrayElement)
                continue;
            if (child->Children.isEmpty() || !IsPointerLikeNode(child))
                continue;

            int64_t tOffset = ParseOffsetValue(child->Offset);
            if (!templateMap.contains(tOffset)) {
                templateMap.insert(tOffset, child);
            }
        }

        if (templateMap.isEmpty())
            return;

        for (const auto& sibling : parentNode->Children) {
            if (sibling == nullptr || sibling->IsTemplateInherited)
                continue;
            if (!sibling->Children.isEmpty() || !IsPointerLikeNode(sibling))
                continue;
            if (useArrayElemTemplates && sibling->IsArrayElement)
                continue;

            int64_t sOffset = ParseOffsetValue(sibling->Offset);
            int64_t rel = sOffset - arrOffset;
            if (rel >= 0 && rel < arrSize) {
                int64_t elemOffset = (arrStride > 0) ? (rel % arrStride) : rel;
                auto tmpl = templateMap.value(elemOffset, nullptr);
                if (tmpl != nullptr && tmpl != sibling) {
                    CloneTemplateSubtree(tmpl, sibling);
                }
            }
        }
    };

    // Process all container parents bottom-up (post-order) so nested arrays within arrays
    // resolve inner templates before outer arrays clone them across sibling pointers.
    std::function<void(const std::shared_ptr<NoteNode>&)> processNode = [&](const std::shared_ptr<NoteNode>& parent) {
        if (parent == nullptr)
            return;

        for (const auto& child : parent->Children) {
            if (child != nullptr && !child->IsTemplateInherited) {
                processNode(child);
            }
        }

        // Sub-case B: Inline array sibling(s) under `parent` providing templates to sibling pointers within their range
        for (const auto& item : parent->Children) {
            if (item == nullptr || item->IsTemplateInherited)
                continue;
            if (item->IsInlineArray && !item->Children.isEmpty() && item->Size > 0) {
                int64_t arrOffset = ParseOffsetValue(item->Offset);

                QMap<int64_t, std::shared_ptr<NoteNode>> templateMap;
                for (const auto& tmpl : item->Children) {
                    if (tmpl == nullptr || tmpl->Children.isEmpty() || !IsPointerLikeNode(tmpl))
                        continue;
                    int64_t tOffset = ParseOffsetValue(tmpl->Offset);
                    if (!templateMap.contains(tOffset)) {
                        templateMap.insert(tOffset, tmpl);
                    }
                }

                if (!templateMap.isEmpty()) {
                    for (const auto& sibling : parent->Children) {
                        if (sibling == nullptr || sibling == item || sibling->IsTemplateInherited)
                            continue;
                        if (!sibling->Children.isEmpty() || !IsPointerLikeNode(sibling))
                            continue;

                        int64_t sOffset = ParseOffsetValue(sibling->Offset);
                        int64_t rel = sOffset - arrOffset;
                        if (rel >= 0 && rel < item->Size) {
                            int64_t elemOffset = (item->ArrayStride > 0) ? (rel % item->ArrayStride) : rel;
                            auto tmpl = templateMap.value(elemOffset, nullptr);
                            if (tmpl != nullptr) {
                                CloneTemplateSubtree(tmpl, sibling);
                            }
                        }
                    }
                }
            }
        }

        // Sub-case C: Pointer-to-array parent containing both template |0x.. and sibling +0x.. children
        if (parent != logicalRoot && parent->IsPointerArray && parent->Size > 0) {
            applyToContainer(parent, 0, parent->Size, parent->ArrayStride, true);
        }
    };

    processNode(logicalRoot);

    // Sub-case A: Root-level array declaration
    if (root != nullptr && (root->IsInlineArray || root->IsPointerArray) && root->Size > 0) {
        applyToContainer(logicalRoot, 0, root->Size, root->ArrayStride, true);
    }
}

// Main entry point: Converts a multiline string into a flat list of nodes
// preserving hierarchy via Parent/Children references.
QList<std::shared_ptr<NoteNode>> PointerTreeParser::ParseNoteText(const QString& noteText) {
    if (noteText.trimmed().isEmpty())
        return {};

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
    if (!lines.isEmpty()) {
        int64_t rootStride = 0;
        bool rootHasArrayTag = false;
        root->Size = ParseSizeFromDescription(lines[0], &rootHasSizeTag, &rootStride, &rootHasArrayTag);
        root->ArrayStride = rootStride;
        if (rootHasArrayTag) {
            bool rootPtr = lines[0].contains("Pointer", Qt::CaseInsensitive) || lines[0].contains('&');
            root->IsPointerArray = rootPtr;
            root->IsInlineArray = !rootPtr;
        }
    }

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

    // Support standard offset jumps (+0x, -0x) as well as array element offsets (|0x, |+0x, |-0x)
    static const QRegularExpression offsetSearch(
        QString::fromUtf8(R"((?:\|[\+\-]?|[\+\-])0x[0-9a-fA-F]+)"),
        QRegularExpression::CaseInsensitiveOption);

    static const QRegularExpression offsetSplitRx(
        QString::fromUtf8(R"(^((?:\|[\+\-]?|[\+\-])0x[0-9a-fA-F]+)(?:([=\s\|:]+)(.*))?$)"),
        QRegularExpression::CaseInsensitiveOption);

    // Helper regex to strip structural characters from description/enum lines.
    static const QRegularExpression prefixStripRegex(QString::fromUtf8(R"(^([.\+\s\x{2500}-\x{257F}]+))"));

    for (int i = 0; i < lines.size(); i++) {
        // TrimEnd instead of Trim ensures leading spaces/visual depth are preserved for the regex
        QString line = lines[i];
        while (!line.isEmpty() && (line.endsWith(' ') || line.endsWith('\t')))
            line.chop(1);

        if (line.trimmed().isEmpty())
            continue;

        auto offsetMatch = offsetSearch.match(line);
        bool isNode = offsetMatch.hasMatch();

        if (isNode) {
            seenFirstNode = true;
            int offsetPos = offsetMatch.capturedStart();
            QString prefix = line.left(offsetPos);

            int indent = 0;
            bool hasBoxChar = false;
            for (QChar c : prefix) {
                if (c == QChar(0x2502) || c == ' ' || c == QChar(0x251C) || c == QChar(0x2514) || c == QChar(0x252C) ||
                    c == QChar(0x2500)) {
                    hasBoxChar = true;
                    break;
                }
            }

            if (hasBoxChar) {
                indent = (prefix.length() > 0) ? (prefix.length() - 1) : 0;
            } else {
                indent = prefix.length();
            }

            QString rawOffsetAndDesc = line.mid(offsetPos).trimmed();
            if (rawOffsetAndDesc.startsWith("|+") || rawOffsetAndDesc.startsWith("|-")) {
                indent += 1;
            }

            QString offset = offsetMatch.captured(0);
            QString desc = "";

            auto splitMatch = offsetSplitRx.match(rawOffsetAndDesc);
            if (splitMatch.hasMatch()) {
                offset = splitMatch.captured(1);
                desc = splitMatch.captured(3).trimmed();
            } else {
                offset = rawOffsetAndDesc;
            }

            // Strip leading separators (- : =) just in case
            while (!desc.isEmpty() && (desc[0] == '-' || desc[0] == ':' || desc[0] == '=' || desc[0].isSpace())) {
                desc = desc.mid(1);
            }
            desc = desc.trimmed();

            bool isArrayElem = offset.startsWith('|') && !offset.startsWith("|+") && !offset.startsWith("|-");
            bool isArrayChild = offset.startsWith("|+") || offset.startsWith("|-");

            int64_t nodeStride = 0;
            bool hasArrayTag = false;
            int64_t size = ParseSizeFromDescription(desc, nullptr, &nodeStride, &hasArrayTag);

            auto node = std::make_shared<NoteNode>();
            node->Offset = offset;
            node->Description = desc;
            node->RawLineIndex = i;
            node->IndentLevel = indent;
            node->Size = size;
            node->ArrayStride = nodeStride;
            node->IsArrayElement = isArrayElem;
            node->IsArrayChild = isArrayChild;
            if (hasArrayTag) {
                bool isPtr = desc.contains("Pointer", Qt::CaseInsensitive) || desc.contains('&');
                node->IsPointerArray = isPtr;
                node->IsInlineArray = !isPtr;
            }

            // Tree Construction:
            while (!stack.isEmpty() && stack.top()->IndentLevel >= indent)
                stack.pop();

            // Safety fallback
            if (stack.isEmpty())
                stack.push(logicalRoot);

            auto parent = stack.top();
            node->Parent = parent.get();
            parent->Children.append(node);

            stack.push(node);

            lastAddedNode = node;
        } else {
            if (!seenFirstNode && !rootHasSizeTag) {
                bool foundTag = false;
                int64_t candidateStride = 0;
                bool candidateArrayTag = false;
                int64_t candidateSize = ParseSizeFromDescription(line, &foundTag, &candidateStride, &candidateArrayTag);
                if (foundTag) {
                    root->Size = candidateSize;
                    root->ArrayStride = candidateStride;
                    if (candidateArrayTag) {
                        bool rootPtr = line.contains("Pointer", Qt::CaseInsensitive) || line.contains('&');
                        root->IsPointerArray = rootPtr;
                        root->IsInlineArray = !rootPtr;
                    }
                    rootHasSizeTag = true;
                }
            }

            // Append to content of most recent node
            if (lastAddedNode != nullptr) {
                if (!lastAddedNode->Content.isEmpty())
                    lastAddedNode->Content += "\r\n";
                // Remove structural prefixes (like │ ) from description content to keep tooltips clean
                QString cleanContentLine = line;
                cleanContentLine.remove(prefixStripRegex);
                lastAddedNode->Content += cleanContentLine.trimmed();
            }
        }
    }

    MergeDuplicateNodes(logicalRoot->Children);
    ApplyArrayTemplates(root, logicalRoot);

    // Recursively flatten the tree for the dropdown list.
    CollectAllNodes(logicalRoot, flatList);
    return flatList;
}

// Parses data size from tags in the description (e.g., [16-bit] or [32 bytes] or [30x16 bytes]).
int64_t PointerTreeParser::ParseSizeFromDescription(const QString& desc, bool* foundTag, int64_t* outStride, bool* outHasArrayTag) {
    if (foundTag != nullptr)
        *foundTag = false;
    if (outStride != nullptr)
        *outStride = 0;
    if (outHasArrayTag != nullptr)
        *outHasArrayTag = false;
    if (desc.isEmpty())
        return 1;

    // 1. Check for explicit byte count or 2D stride: [144 bytes], [30x16 bytes], [ASCII: 20-bytes]
    static const QRegularExpression bytesRegex(R"(\[[^\]]*?\b(?:(\d+)\s*x\s*(\d+)|(0x[0-9A-Fa-f]+)|(\d+))[\s\-]*bytes?\b[^\]]*\])",
                                               QRegularExpression::CaseInsensitiveOption);
    auto bytesMatch = bytesRegex.match(desc);
    if (bytesMatch.hasMatch()) {
        if (!bytesMatch.captured(1).isEmpty() && !bytesMatch.captured(2).isEmpty()) {
            bool ok1 = false, ok2 = false;
            int64_t w = bytesMatch.captured(1).toLongLong(&ok1);
            int64_t h = bytesMatch.captured(2).toLongLong(&ok2);
            if (ok1 && ok2 && w > 0 && h > 0) {
                if (foundTag != nullptr)
                    *foundTag = true;
                if (outStride != nullptr)
                    *outStride = h;
                if (outHasArrayTag != nullptr)
                    *outHasArrayTag = true;
                return w * h;
            }
        } else if (!bytesMatch.captured(3).isEmpty()) {
            bool ok = false;
            int64_t bSize = bytesMatch.captured(3).mid(2).toLongLong(&ok, 16);
            if (ok && bSize > 0) {
                if (foundTag != nullptr)
                    *foundTag = true;
                if (outHasArrayTag != nullptr)
                    *outHasArrayTag = true;
                return bSize;
            }
        } else if (!bytesMatch.captured(4).isEmpty()) {
            bool ok = false;
            int64_t bSize = bytesMatch.captured(4).toLongLong(&ok);
            if (ok && bSize > 0) {
                if (foundTag != nullptr)
                    *foundTag = true;
                if (outHasArrayTag != nullptr)
                    *outHasArrayTag = true;
                return bSize;
            }
        }
    }

    // 2. Check for standard type tags
    static const QRegularExpression bits64Regex(R"(\[[^\]]*\b(?:64[\s\-]*bits?|Double64)\b[^\]]*\])",
                                                QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression bits32Regex(R"(\[[^\]]*\b(?:32[\s\-]*bits?|Float(?:32)?|MBF32(?:\s+LE)?|Double32(?:\s+BE)?)\b[^\]]*\])",
                                                QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression bits24Regex(R"(\[[^\]]*\b24[\s\-]*bits?\b[^\]]*\])", QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression bits16Regex(R"(\[[^\]]*\b16[\s\-]*bits?\b[^\]]*\])", QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression bits8Regex(R"(\[[^\]]*\b8[\s\-]*bits?\b[^\]]*\])", QRegularExpression::CaseInsensitiveOption);

    if (bits64Regex.match(desc).hasMatch()) {
        if (foundTag != nullptr)
            *foundTag = true;
        return 8;
    }
    if (bits32Regex.match(desc).hasMatch()) {
        if (foundTag != nullptr)
            *foundTag = true;
        return 4;
    }
    if (bits24Regex.match(desc).hasMatch()) {
        if (foundTag != nullptr)
            *foundTag = true;
        return 3;
    }
    if (bits16Regex.match(desc).hasMatch()) {
        if (foundTag != nullptr)
            *foundTag = true;
        return 2;
    }
    if (bits8Regex.match(desc).hasMatch()) {
        if (foundTag != nullptr)
            *foundTag = true;
        return 1;
    }

    return 1; // Default to 8-bit (1 byte)
}

// Helper to traverse tree and populate the list.
void PointerTreeParser::CollectAllNodes(const std::shared_ptr<NoteNode>& node, QList<std::shared_ptr<NoteNode>>& flatList) {
    if (node->IndentLevel != -1)
        flatList.append(node);
    for (const auto& child : node->Children)
        CollectAllNodes(child, flatList);
}

// Constructs the RA Logic String (e.g., I:0x1234_0x10) for a specific node in the tree.
std::tuple<QString, QString, QString>
PointerTreeParser::GeneratePointerLogic(const CodeNote& baseNote, const std::shared_ptr<NoteNode>& selectedNode, int consoleId) {
    if (baseNote.Address.isEmpty())
        return std::make_tuple("", "", "");

    // Base Address cleanup
    QString baseAddrStr = baseNote.Address.startsWith("0x", Qt::CaseInsensitive) ? baseNote.Address.mid(2) : baseNote.Address;
    QString type = "Mem";
    QString size = "32-bit";

    // Parse the root note header to determine the pointer type and whether this note is a static root array
    QString noteHeader = "";
    QString normalizedNote = baseNote.Note;
    normalizedNote.replace("\r\n", "\n").replace('\r', '\n');
    QStringList headerLines = normalizedNote.split('\n', Qt::SkipEmptyParts);
    if (!headerLines.isEmpty())
        noteHeader = headerLines.first();

    // Check if the base address is a static array structure in RAM
    static const QRegularExpression arrayTagRx(R"(\[[^\]]*?\b(?:(\d+)\s*x\s*(\d+)|(0x[0-9A-Fa-f]+)|(\d+))[\s\-]*bytes?\b[^\]]*\])",
                                               QRegularExpression::CaseInsensitiveOption);
    bool hasArrayTag = arrayTagRx.match(noteHeader).hasMatch();
    bool hasPointerTag = noteHeader.contains("Pointer", Qt::CaseInsensitive);
    bool isStaticRootArray = (hasArrayTag && !hasPointerTag);

    QString prefixStr = RaLogicSyntax::GetPlatformPointerPrefix(consoleId);
    QString maskStr = RaLogicSyntax::GetPlatformPointerMask(consoleId);

    if (noteHeader.contains("Pointer", Qt::CaseInsensitive)) {
        prefixStr = "X";
        if (noteHeader.contains("BE", Qt::CaseInsensitive)) {
            prefixStr = "G";
            maskStr = "&536870911";
        } else if (noteHeader.contains("24-bit", Qt::CaseInsensitive)) {
            prefixStr = "W";
            maskStr = "";
        } else if (noteHeader.contains("16-bit", Qt::CaseInsensitive)) {
            prefixStr = "";
            maskStr = "";
        }
    }

    // Check for explicit mask in note (e.g. &0xFFFFFF or &536870911)
    static const QRegularExpression maskRegex(R"(&\s*(?:0x([0-9A-Fa-f]+)|(\d+)))");
    auto maskMatch = maskRegex.match(noteHeader);
    if (maskMatch.hasMatch()) {
        if (!maskMatch.captured(1).isEmpty()) {
            bool ok = false;
            int64_t val = maskMatch.captured(1).toLongLong(&ok, 16);
            if (ok)
                maskStr = "&" + QString::number(val);
        } else if (!maskMatch.captured(2).isEmpty()) {
            maskStr = "&" + maskMatch.captured(2);
        }
    }

    QString sb;
    QString descriptionForType = "";

    // Build the chain if selecting a child node
    if (selectedNode != nullptr && selectedNode->IndentLevel != -2) {
        descriptionForType = selectedNode->Description;

        // Collect lineage from top-level down to selected node
        QList<NoteNode*> lineage;
        NoteNode* cur = selectedNode.get();
        while (cur != nullptr && cur->IndentLevel != -1) {
            lineage.prepend(cur);
            cur = cur->Parent;
        }

        auto ParseHexClean = [](const QString& offsetStr) -> int64_t {
            QString clean = offsetStr;
            clean.remove('|').remove('+').remove("0x", Qt::CaseInsensitive);
            clean = clean.trimmed();
            bool ok = false;
            int64_t v = clean.toLongLong(&ok, 16);
            if (ok && offsetStr.contains('-'))
                return -v;
            return ok ? v : 0;
        };

        auto FormatHex8 = [](int64_t val) -> QString {
            return QString("%1").arg(static_cast<quint32>(val), 8, 16, QChar('0'));
        };

        if (isStaticRootArray && !lineage.isEmpty()) {
            // Static Root Array: Top element offset represents direct memory addition in RAM
            bool okBase = false;
            int64_t baseNum = baseAddrStr.toLongLong(&okBase, 16);
            int64_t directAddr = okBase ? baseNum : 0;

            int firstIndirectIdx = 0;
            while (firstIndirectIdx < lineage.size() - 1 && lineage[firstIndirectIdx]->IsInlineArray) {
                directAddr += ParseHexClean(lineage[firstIndirectIdx]->Offset);
                firstIndirectIdx++;
            }

            if (firstIndirectIdx == lineage.size() - 1) {
                // Direct memory read without indirection
                directAddr += ParseHexClean(lineage[firstIndirectIdx]->Offset);
                sb.append("0x" + FormatHex8(directAddr));
            } else {
                // Pointer dereference starting from array element
                directAddr += ParseHexClean(lineage[firstIndirectIdx]->Offset);
                QString directHex = FormatHex8(directAddr);
                sb.append(QString("I:0x%1%2%3").arg(prefixStr, directHex, maskStr));

                int64_t pendingOffset = 0;
                for (int i = firstIndirectIdx + 1; i < lineage.size() - 1; ++i) {
                    int64_t offVal = ParseHexClean(lineage[i]->Offset);
                    if (lineage[i]->IsInlineArray) {
                        pendingOffset += offVal;
                    } else {
                        sb.append(QString("_I:0x%1%2%3").arg(prefixStr, FormatHex8(pendingOffset + offVal), maskStr));
                        pendingOffset = 0;
                    }
                }

                int64_t lastOff = ParseHexClean(lineage.last()->Offset);
                sb.append(QString("_0x%1").arg(FormatHex8(pendingOffset + lastOff)));
            }
        } else {
            // Standard Pointer Chain
            sb.append(QString("I:0x%1%2%3").arg(prefixStr, baseAddrStr, maskStr));

            int64_t pendingOffset = 0;
            for (int i = 0; i < lineage.size() - 1; ++i) {
                int64_t offVal = ParseHexClean(lineage[i]->Offset);
                if (lineage[i]->IsInlineArray) {
                    pendingOffset += offVal;
                } else {
                    sb.append(QString("_I:0x%1%2%3").arg(prefixStr, FormatHex8(pendingOffset + offVal), maskStr));
                    pendingOffset = 0;
                }
            }

            if (!lineage.isEmpty()) {
                int64_t lastOff = ParseHexClean(lineage.last()->Offset);
                sb.append(QString("_0x%1").arg(FormatHex8(pendingOffset + lastOff)));
            }
        }
    } else {
        // Root selection
        sb.append(QString("0x%1").arg(baseAddrStr));
        descriptionForType = noteHeader;
    }

    // Attempt to detect type/size from the description (e.g. [Float], [16-bit])
    static const QRegularExpression typeRegex(R"(\[(.*?\]))");
    auto typeMatch = typeRegex.match(descriptionForType);
    if (typeMatch.hasMatch()) {
        QString tag = typeMatch.captured(1);
        if (tag.endsWith(']'))
            tag.chop(1);
        if (tag.compare("Float", Qt::CaseInsensitive) == 0 || tag.compare("Flaot", Qt::CaseInsensitive) == 0) {
            type = "Mem";
            size = "Float";
        } else {
            size = tag;
            type = "Mem";
        }
    } else {
        size = "8-bit"; // Default fallback
    }

    return std::make_tuple(sb, type, size);
}

} // namespace RALE
