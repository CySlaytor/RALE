#include "ConditionFormatter.h"

#include "../Definitions/RaLogicSyntax.h"
#include "../Utilities/LogicFormatter.h"

#include <QRegularExpression>

#include <optional>

namespace RALE {

namespace {

std::optional<int64_t> ParseOffsetHexOpt(const QString& s) {
    static const QRegularExpression validHexRegex("^[0-9A-Fa-f]+$");
    if (s.trimmed().isEmpty())
        return std::nullopt;
    QString clean = s;
    clean.remove('|');
    clean = clean.trimmed();
    bool isNeg = clean.startsWith('-');
    if (isNeg || clean.startsWith('+'))
        clean = clean.mid(1).trimmed();
    if (clean.startsWith("0x", Qt::CaseInsensitive))
        clean = clean.mid(2).trimmed();
    if (!validHexRegex.match(clean).hasMatch())
        return std::nullopt;
    bool ok = false;
    int64_t v = clean.toLongLong(&ok, 16);
    if (!ok)
        return std::nullopt;
    return isNeg ? -v : v;
}

bool IsPointerLikeNoteNode(const std::shared_ptr<NoteNode>& node) {
    if (node == nullptr || node->IsInlineArray)
        return false;
    if (!node->Children.isEmpty())
        return true;
    return node->Description.contains("Pointer", Qt::CaseInsensitive) || node->Description.contains('&');
}

std::shared_ptr<NoteNode> FindFirstDocumentedPointer(const QList<std::shared_ptr<NoteNode>>& nodes, int64_t minRelOffset,
                                                     int64_t maxSize, int64_t baseOffset = 0) {
    std::shared_ptr<NoteNode> best = nullptr;
    int64_t bestRel = 0;

    for (const auto& n : nodes) {
        if (n == nullptr || n->IsTemplateInherited || n->Children.isEmpty() || !IsPointerLikeNoteNode(n))
            continue;
        auto offOpt = ParseOffsetHexOpt(n->Offset);
        if (!offOpt.has_value())
            continue;
        int64_t rel = offOpt.value() - baseOffset;
        if (rel < minRelOffset || (maxSize > 0 && rel >= maxSize))
            continue;
        if (best == nullptr || rel < bestRel) {
            best = n;
            bestRel = rel;
        }
    }
    return best;
}

QList<std::shared_ptr<NoteNode>> ResolveNoteRedirects(QList<std::shared_ptr<NoteNode>> workingNodes,
                                                      const QMap<QString, QList<std::shared_ptr<NoteNode>>>& notesLookup) {
    static const QRegularExpression referRegex(R"(refer to \$?0x([0-9a-f]+))", QRegularExpression::CaseInsensitiveOption);
    int redirects = 0;

    while (redirects < 5 && !workingNodes.isEmpty()) {
        std::shared_ptr<NoteNode> root = nullptr;
        for (const auto& n : workingNodes) {
            if (n->IndentLevel == -2) {
                root = n;
                break;
            }
        }
        if (root == nullptr)
            root = workingNodes.first();
        QString content = root->Content;

        if (content.contains("refer to", Qt::CaseInsensitive)) {
            auto match = referRegex.match(content);
            if (match.hasMatch()) {
                QString targetAddr = match.captured(1);
                QString normTarget = LogicFormatter::NormalizeAddress(targetAddr);
                if (notesLookup.contains(normTarget)) {
                    workingNodes = notesLookup.value(normTarget);
                    redirects++;
                    continue;
                }
            }
        }
        break;
    }

    return workingNodes;
}

} // namespace

// Prioritizes exact non-inline-array matches and narrow enclosing ranges before checking
// inline [NxM bytes] arrays via modulo element stride or first documented pointer fallback.
std::shared_ptr<NoteNode> ConditionFormatter::FindBestMatchingNode(const QList<std::shared_ptr<NoteNode>>& levelNodes, int64_t offset,
                                                                   bool allowInlineArrayFallback, int64_t* outMatchedNodeOff,
                                                                   const std::shared_ptr<NoteNode>& parentContainer) {
    if (offset < 0 && parentContainer == nullptr)
        return nullptr;

    // 1. Exact offset match on non-inline-array nodes
    for (const auto& node : levelNodes) {
        if (node == nullptr || node->IsInlineArray)
            continue;
        auto nodeOffOpt = ParseOffsetHexOpt(node->Offset);
        if (nodeOffOpt.has_value() && offset == nodeOffOpt.value()) {
            if (outMatchedNodeOff != nullptr)
                *outMatchedNodeOff = nodeOffOpt.value();
            return node;
        }
    }

    // 2. Range match on non-inline-array nodes (smallest enclosing span)
    std::shared_ptr<NoteNode> bestRangeNode = nullptr;
    int64_t bestRangeOff = 0;
    for (const auto& node : levelNodes) {
        if (node == nullptr || node->IsInlineArray)
            continue;
        auto nodeOffOpt = ParseOffsetHexOpt(node->Offset);
        if (nodeOffOpt.has_value() && offset >= nodeOffOpt.value() && offset < nodeOffOpt.value() + node->Size) {
            if (bestRangeNode == nullptr || node->Size < bestRangeNode->Size) {
                bestRangeNode = node;
                bestRangeOff = nodeOffOpt.value();
            }
        }
    }
    if (bestRangeNode != nullptr) {
        if (outMatchedNodeOff != nullptr)
            *outMatchedNodeOff = bestRangeOff;
        return bestRangeNode;
    }

    // 3. Inline array container match: resolve element offset modulo ArrayStride or first documented pointer
    for (const auto& node : levelNodes) {
        if (node == nullptr || !node->IsInlineArray || node->Size <= 0)
            continue;
        auto nodeOffOpt = ParseOffsetHexOpt(node->Offset);
        if (!nodeOffOpt.has_value())
            continue;
        int64_t nodeOff = nodeOffOpt.value();
        if (offset >= nodeOff && offset < nodeOff + node->Size) {
            int64_t rel = offset - nodeOff;
            if (!node->Children.isEmpty()) {
                if (node->ArrayStride > 0) {
                    int64_t elemOff = rel % node->ArrayStride;
                    int64_t childNodeOff = 0;
                    auto matchedChild = FindBestMatchingNode(node->Children, elemOff, allowInlineArrayFallback, &childNodeOff, node);
                    if (matchedChild != nullptr) {
                        if (outMatchedNodeOff != nullptr)
                            *outMatchedNodeOff = nodeOff + (rel - elemOff) + childNodeOff;
                        return matchedChild;
                    }
                } else {
                    int64_t childNodeOff = 0;
                    auto matchedChild = FindBestMatchingNode(node->Children, rel, allowInlineArrayFallback, &childNodeOff, node);
                    if (matchedChild != nullptr) {
                        if (outMatchedNodeOff != nullptr)
                            *outMatchedNodeOff = nodeOff + childNodeOff;
                        return matchedChild;
                    }
                }
            }

            auto firstSiblingDocPtr = FindFirstDocumentedPointer(levelNodes, 0, node->Size, nodeOff);
            if (firstSiblingDocPtr != nullptr && (!allowInlineArrayFallback || node->ArrayStride == 0)) {
                if (outMatchedNodeOff != nullptr)
                    *outMatchedNodeOff = nodeOff + (rel & ~int64_t(3));
                return firstSiblingDocPtr;
            }

            if (allowInlineArrayFallback) {
                if (outMatchedNodeOff != nullptr)
                    *outMatchedNodeOff = nodeOff;
                return node;
            }
        }
    }

    // 4. Parent array container match: applies strictly to pointers/elements inside [0, parentContainer->Size)
    if (parentContainer != nullptr && (parentContainer->IsPointerArray || parentContainer->IsInlineArray) && parentContainer->Size > 0) {
        if (offset >= 0 && offset < parentContainer->Size) {
            if (parentContainer->ArrayStride > 0) {
                int64_t elemOff = offset % parentContainer->ArrayStride;

                for (const auto& node : levelNodes) {
                    if (node == nullptr || node->IsInlineArray || node->IsTemplateInherited)
                        continue;
                    auto nodeOffOpt = ParseOffsetHexOpt(node->Offset);
                    if (!nodeOffOpt.has_value() || nodeOffOpt.value() < 0 || nodeOffOpt.value() >= parentContainer->Size)
                        continue;
                    if ((nodeOffOpt.value() % parentContainer->ArrayStride) == elemOff) {
                        if (outMatchedNodeOff != nullptr)
                            *outMatchedNodeOff = offset - elemOff + (nodeOffOpt.value() % parentContainer->ArrayStride);
                        return node;
                    }
                }

                std::shared_ptr<NoteNode> bestModRange = nullptr;
                int64_t bestModStart = 0;
                for (const auto& node : levelNodes) {
                    if (node == nullptr || node->IsInlineArray || node->IsTemplateInherited)
                        continue;
                    auto nodeOffOpt = ParseOffsetHexOpt(node->Offset);
                    if (!nodeOffOpt.has_value() || nodeOffOpt.value() < 0 || nodeOffOpt.value() >= parentContainer->Size)
                        continue;
                    int64_t slotStart = nodeOffOpt.value() % parentContainer->ArrayStride;
                    if (elemOff >= slotStart && elemOff < slotStart + node->Size) {
                        if (bestModRange == nullptr || node->Size < bestModRange->Size) {
                            bestModRange = node;
                            bestModStart = slotStart;
                        }
                    }
                }
                if (bestModRange != nullptr) {
                    if (outMatchedNodeOff != nullptr)
                        *outMatchedNodeOff = offset - elemOff + bestModStart;
                    return bestModRange;
                }
            }

            auto firstDocPtr = FindFirstDocumentedPointer(levelNodes, 0, parentContainer->Size);
            if (firstDocPtr != nullptr && (!allowInlineArrayFallback || parentContainer->ArrayStride == 0)) {
                if (outMatchedNodeOff != nullptr)
                    *outMatchedNodeOff = offset & ~int64_t(3);
                return firstDocPtr;
            }
        }
    }

    return nullptr;
}

QString ConditionFormatter::FormatOperandDisplay(const Operand& op, bool showDecimal, bool showAliases,
                                                 const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup,
                                                 const QList<RangeEntry>* rangeCache, const AchievementConditionGroup* group,
                                                 int rowIndex) {
    // Standard fallback without size ref override
    return FormatOperandDisplay(op, showDecimal, showAliases, notesLookup, rangeCache, group, rowIndex, op.Size);
}

QString ConditionFormatter::FormatOperandDisplay(const Operand& op, bool showDecimal, bool showAliases,
                                                 const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup,
                                                 const QList<RangeEntry>* rangeCache, const AchievementConditionGroup* group, int rowIndex,
                                                 const QString& sizeRef) {
    if (showAliases && RaLogicSyntax::MemoryTypes.contains(op.Type) && notesLookup != nullptr) {
        QString addr = LogicFormatter::NormalizeAddress(op.Value);
        QString alias = ResolveAlias(addr, sizeRef, group, rowIndex, *notesLookup, rangeCache);
        if (!alias.isEmpty())
            return alias.trimmed();
    }
    return LogicFormatter::FormatDisplayValue(op, showDecimal, sizeRef);
}

QString ConditionFormatter::ResolveAlias(const QString& address,
                                         const QString& sizeRef, // NEW: Used for Bit extraction
                                         const AchievementConditionGroup* group, int rowIndex,
                                         const QMap<QString, QList<std::shared_ptr<NoteNode>>>& notesLookup,
                                         const QList<RangeEntry>* rangeCache) {
    // Pre-compiled regex patterns to eliminate allocation churn in cell rendering
    static const QRegularExpression validHexRegex("^[0-9A-Fa-f]+$");
    static const QRegularExpression tagRegex(R"(\[.*?\])");
    static const QRegularExpression bitSizeRegex(R"(^(Bit\d+|Lower4|Upper4))", QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression multiSpaceRegex(R"(\s{2,})");
    static const QRegularExpression arrayTagRx(R"(\[[^\]]*?\b(?:(\d+)\s*x\s*(\d+)|(0x[0-9A-Fa-f]+)|(\d+))[\s\-]*bytes?\b[^\]]*\])",
                                               QRegularExpression::CaseInsensitiveOption);

    // Robust Hex Parser: Removes 0x, pipes |, and any non-hex characters
    auto ParseHex = [](const QString& s) -> int64_t {
        if (s.trimmed().isEmpty())
            return -1;
        QString clean = s;
        clean.remove('|');
        clean = clean.trimmed();
        if (clean.startsWith('+') || clean.startsWith('-'))
            clean = clean.mid(1).trimmed();
        if (clean.startsWith("0x", Qt::CaseInsensitive))
            clean = clean.mid(2).trimmed();
        if (!validHexRegex.match(clean).hasMatch())
            return -1;
        bool ok = false;
        int64_t v = clean.toLongLong(&ok, 16);
        return ok ? v : -1;
    };

    auto ExtractRootHeaderLine = [&](const QString& content) -> QString {
        QString norm = content;
        norm.replace("\r\n", "\n").replace('\r', '\n');
        QStringList parts = norm.split('\n', Qt::SkipEmptyParts);
        for (const QString& part : parts) {
            QString candidate = part.trimmed();
            if (candidate.startsWith('+') || candidate.startsWith('|'))
                break;

            auto mIt = tagRegex.globalMatch(candidate);
            QString cleaned = candidate;
            while (mIt.hasNext()) {
                auto m = mIt.next();
                if (m.captured(0).contains("Pointer", Qt::CaseInsensitive))
                    cleaned.replace(m.captured(0), "[Pointer]");
                else
                    cleaned.replace(m.captured(0), "");
            }
            if (!cleaned.trimmed().isEmpty())
                return candidate;
        }
        return !parts.isEmpty() ? parts.first() : "";
    };

    int64_t targetVal = ParseHex(address);

    // 1. Strict Pointer Chain Logic (Offsets)
    // Fix: Check bounds before accessing group.Conditions
    if (group != nullptr && rowIndex > 0 && rowIndex < group->Conditions.size()) {
        QList<ChainNode> chainStack;
        int scan = rowIndex - 1;
        while (scan >= 0) {
            // Additional check to prevent index out of range if group modified externally
            if (scan >= group->Conditions.size()) {
                scan--;
                continue;
            }

            const auto& prevCond = group->Conditions[scan];
            if (prevCond.Flag == "Add Address") {
                // Add to front of list to maintain top-to-bottom order for indexing filtering
                chainStack.prepend(ChainNode{RaLogicSyntax::MemoryTypes.contains(prevCond.LeftOperand.Type), prevCond.LeftOperand.Value});
                scan--;
            } else
                break;
        }

        // Handle Array Indexing (Index + Base) imported from AutoCR
        // If an AddAddress chain starts with something that has no code note, we assume it is an Index.
        while (chainStack.size() > 1) {
            const auto& first = chainStack.first();
            int64_t baseAddrNum = ParseHex(LogicFormatter::NormalizeAddress(first.Value));
            if (!first.IsMem || GetEffectiveNote(notesLookup, baseAddrNum, rangeCache) == nullptr) {
                chainStack.removeFirst();
            } else {
                break;
            }
        }
        // Ensure we don't accidentally preserve a non-address value if it's the only one left
        if (chainStack.size() == 1) {
            const auto& first = chainStack.first();
            int64_t baseAddrNum = ParseHex(LogicFormatter::NormalizeAddress(first.Value));
            if (!first.IsMem || GetEffectiveNote(notesLookup, baseAddrNum, rangeCache) == nullptr) {
                chainStack.removeFirst();
            }
        }

        if (!chainStack.isEmpty()) {
            auto baseNode = chainStack.takeFirst();
            QString normalizedBase = LogicFormatter::NormalizeAddress(baseNode.Value);

            QList<std::shared_ptr<NoteNode>> workingNodes;
            bool hasWorkingNodes = false;

            if (notesLookup.contains(normalizedBase)) {
                // Redirect Resolution Logic added here
                // Check if the Base Note is a Redirect (refer to $0x...)
                workingNodes = ResolveNoteRedirects(notesLookup.value(normalizedBase), notesLookup);
                hasWorkingNodes = !workingNodes.isEmpty();
            } else if (rangeCache != nullptr) {
                int64_t baseVal = ParseHex(baseNode.Value);
                const RangeEntry* bestRangeMatch = nullptr;
                if (baseVal != -1) {
                    for (const auto& match : *rangeCache) {
                        if (baseVal >= match.Start && baseVal < match.End && match.Node != nullptr && !match.Node->IsPointerArray) {
                            if (bestRangeMatch == nullptr || match.Start > bestRangeMatch->Start ||
                                (match.Start == bestRangeMatch->Start && (match.End - match.Start) < (bestRangeMatch->End - bestRangeMatch->Start))) {
                                bestRangeMatch = &match;
                            }
                        }
                    }
                }
                if (bestRangeMatch != nullptr) {
                    QString startKey = QString("0x%1").arg(bestRangeMatch->Start, 0, 16);
                    if (notesLookup.contains(startKey)) {
                        workingNodes = ResolveNoteRedirects(notesLookup.value(startKey), notesLookup);
                    } else {
                        workingNodes = {bestRangeMatch->Node};
                    }
                    hasWorkingNodes = !workingNodes.isEmpty();
                    chainStack.prepend(ChainNode{false, QString("0x%1").arg(baseVal - bestRangeMatch->Start, 0, 16)});
                }
            }

            if (hasWorkingNodes) {
                std::shared_ptr<NoteNode> rootContainer = nullptr;
                QList<std::shared_ptr<NoteNode>> currentLevelNodes;
                for (const auto& n : workingNodes) {
                    if (n->IndentLevel == -2) {
                        rootContainer = n;
                        for (const auto& ch : n->Children) {
                            if (ch->IndentLevel == -1)
                                currentLevelNodes.append(ch->Children);
                            else if (ch->IndentLevel != -2)
                                currentLevelNodes.append(ch);
                        }
                    }
                }
                if (currentLevelNodes.isEmpty()) {
                    for (const auto& n : workingNodes) {
                        if (n->IndentLevel != -2 && (n->Parent == nullptr || n->Parent->IndentLevel == -1))
                            currentLevelNodes.append(n);
                    }
                }

                std::shared_ptr<NoteNode> parentContainer = rootContainer;
                bool chainValid = true;

                while (!chainStack.isEmpty()) {
                    auto offsetNode = chainStack.takeFirst();
                    int64_t offset = ParseHex(offsetNode.Value);
                    if (offset == -1) {
                        chainValid = false;
                        break;
                    }

                    auto match = FindBestMatchingNode(currentLevelNodes, offset, false, nullptr, parentContainer);
                    if (match != nullptr) {
                        currentLevelNodes = match->Children;
                        parentContainer = match;
                    } else {
                        chainValid = false;
                        break;
                    }
                }

                std::shared_ptr<NoteNode> finalMatchNode = nullptr;
                int64_t finalMatchedOff = 0;
                if (chainValid && targetVal != -1) {
                    finalMatchNode = FindBestMatchingNode(currentLevelNodes, targetVal, true, &finalMatchedOff, parentContainer);
                }

                if (finalMatchNode != nullptr) {
                    QString desc = finalMatchNode->Description;
                    desc.remove(tagRegex);
                    desc = desc.trimmed();

                    // BIT EXTRACTION
                    if (!sizeRef.isEmpty() && bitSizeRegex.match(sizeRef).hasMatch()) {
                        QRegularExpression bitRegex(QString(R"(^%1\s*[:=|-]\s*(.*)$)").arg(QRegularExpression::escape(sizeRef)),
                                                    QRegularExpression::CaseInsensitiveOption | QRegularExpression::MultilineOption);
                        auto m = bitRegex.match(finalMatchNode->Content);
                        if (m.hasMatch()) {
                            desc = m.captured(1).trimmed();
                        }
                    }

                    int64_t delta = targetVal - finalMatchedOff;
                    if (delta > 0) {
                        QString suffix = QString(" +0x%1").arg(QString::number(delta, 16).toUpper());
                        if (!desc.endsWith(suffix, Qt::CaseInsensitive))
                            desc += suffix;
                    }
                    return desc;
                }

                // If we were in a pointer chain and failed to find the node, abort and show Hex.
                return "";
            }
        }
    }

    // 2. Direct Lookup & Static Array Element Resolution
    if (notesLookup.contains(address)) {
        // Redirect Resolution Logic added here too
        QList<std::shared_ptr<NoteNode>> workingNodes = ResolveNoteRedirects(notesLookup.value(address), notesLookup);

        if (!workingNodes.isEmpty()) {
            std::shared_ptr<NoteNode> main = nullptr;
            for (const auto& n : workingNodes) {
                if (n->IndentLevel == -2) {
                    main = n;
                    break;
                }
            }
            if (main == nullptr)
                main = workingNodes.first();

            QString desc = main->Description;

            if (desc.isEmpty() || desc.compare("Full Note", Qt::CaseInsensitive) == 0) {
                if (!main->Content.isEmpty()) {
                    desc = ExtractRootHeaderLine(main->Content);
                }
            }

            auto matchIterator = tagRegex.globalMatch(desc);
            QString cleanDesc = desc;
            while (matchIterator.hasNext()) {
                auto m = matchIterator.next();
                if (m.captured(0).contains("Pointer", Qt::CaseInsensitive))
                    cleanDesc.replace(m.captured(0), "[Pointer]");
                else
                    cleanDesc.replace(m.captured(0), "");
            }
            desc = cleanDesc.trimmed();

            // BIT EXTRACTION
            if (!sizeRef.isEmpty() && bitSizeRegex.match(sizeRef).hasMatch()) {
                QRegularExpression bitRegex(QString(R"(^%1\s*[:=|-]\s*(.*)$)").arg(QRegularExpression::escape(sizeRef)),
                                            QRegularExpression::CaseInsensitiveOption | QRegularExpression::MultilineOption);
                auto m = bitRegex.match(main->Content);
                if (m.hasMatch()) {
                    desc = m.captured(1).trimmed();
                }
            }

            desc.replace(multiSpaceRegex, " ");
            return desc;
        }
    }

    // 3. Ranged Direct Lookup (Including PF2 Static Array Structures)
    if (rangeCache != nullptr && targetVal != -1) {
        const RangeEntry* bestMatch = nullptr;
        for (const auto& match : *rangeCache) {
            if (targetVal >= match.Start && targetVal < match.End && match.Node != nullptr) {
                if (bestMatch == nullptr || match.Start > bestMatch->Start ||
                    (match.Start == bestMatch->Start && (match.End - match.Start) < (bestMatch->End - bestMatch->Start))) {
                    bestMatch = &match;
                }
            }
        }

        if (bestMatch != nullptr) {
            int64_t relOffset = targetVal - bestMatch->Start;
            std::shared_ptr<NoteNode> specificMatch = nullptr;
            int64_t matchedOff = 0;

            // For range matches, the target is usually an offset within the struct.
            // We can try to find the specific offset node first, before falling back to the struct root!
            QString rootHeader = ExtractRootHeaderLine(bestMatch->Node->Content);
            bool isPointerRoot = rootHeader.contains("Pointer", Qt::CaseInsensitive);
            if (!isPointerRoot) {
                QList<std::shared_ptr<NoteNode>> topLevelChildren;
                for (const auto& child : bestMatch->Node->Children) {
                    if (child->IndentLevel == -1)
                        topLevelChildren.append(child->Children);
                    else if (child->IndentLevel != -2)
                        topLevelChildren.append(child);
                }

                specificMatch = FindBestMatchingNode(topLevelChildren, relOffset, true, &matchedOff, bestMatch->Node);
            }

            std::shared_ptr<NoteNode> activeNode = (specificMatch != nullptr) ? specificMatch : bestMatch->Node;

            QString desc = activeNode->Description;
            if (desc.isEmpty() || desc.compare("Full Note", Qt::CaseInsensitive) == 0) {
                desc = ExtractRootHeaderLine(activeNode->Content);
            }

            auto matchIterator = tagRegex.globalMatch(desc);
            QString cleanDesc = desc;
            while (matchIterator.hasNext()) {
                auto m = matchIterator.next();
                if (m.captured(0).contains("Pointer", Qt::CaseInsensitive))
                    cleanDesc.replace(m.captured(0), "[Pointer]");
                else
                    cleanDesc.replace(m.captured(0), "");
            }
            desc = cleanDesc.trimmed();

            // BIT EXTRACTION
            if (!sizeRef.isEmpty() && bitSizeRegex.match(sizeRef).hasMatch()) {
                QRegularExpression bitRegex(QString(R"(^%1\s*[:=|-]\s*(.*)$)").arg(QRegularExpression::escape(sizeRef)),
                                            QRegularExpression::CaseInsensitiveOption | QRegularExpression::MultilineOption);
                auto m = bitRegex.match(activeNode->Content);
                if (m.hasMatch()) {
                    desc = m.captured(1).trimmed();
                }
            }

            desc.replace(multiSpaceRegex, " ");

            // If it was the root struct match, use the Start address. Otherwise use the specific node offset.
            int64_t baseForDelta = (specificMatch != nullptr) ? (bestMatch->Start + matchedOff) : bestMatch->Start;

            int64_t delta = targetVal - baseForDelta;
            if (delta > 0) {
                QString suffix = QString(" +0x%1").arg(QString::number(delta, 16).toUpper());
                if (!desc.endsWith(suffix, Qt::CaseInsensitive))
                    desc += suffix;
            }
            return desc;
        }
    }

    if (targetVal != -1) {
        // Check for direct static array elements mapped from base note
        for (auto it = notesLookup.cbegin(); it != notesLookup.cend(); ++it) {
            int64_t baseAddr = ParseHex(it.key());
            if (baseAddr == -1 || targetVal < baseAddr)
                continue;

            const auto& nodes = it.value();
            if (nodes.isEmpty())
                continue;

            std::shared_ptr<NoteNode> root = nullptr;
            for (const auto& n : nodes) {
                if (n->IndentLevel == -2) {
                    root = n;
                    break;
                }
            }
            if (root == nullptr)
                root = nodes.first();

            QString firstLine = root->Content.split('\n', Qt::SkipEmptyParts).value(0, "");
            if (firstLine.contains("Pointer", Qt::CaseInsensitive))
                continue;

            auto arrMatch = arrayTagRx.match(firstLine);
            if (arrMatch.hasMatch()) {
                int64_t arrSize = 0;
                if (!arrMatch.captured(1).isEmpty() && !arrMatch.captured(2).isEmpty())
                    arrSize = arrMatch.captured(1).toLongLong() * arrMatch.captured(2).toLongLong();
                else if (!arrMatch.captured(3).isEmpty())
                    arrSize = arrMatch.captured(3).mid(2).toLongLong(nullptr, 16);
                else if (!arrMatch.captured(4).isEmpty())
                    arrSize = arrMatch.captured(4).toLongLong();

                if (arrSize > 0 && targetVal < baseAddr + arrSize) {
                    int64_t targetOffset = targetVal - baseAddr;
                    QList<std::shared_ptr<NoteNode>> topLevelChildren;
                    for (const auto& n : nodes) {
                        if (n->IndentLevel != -2 && n->IndentLevel != -1 && (n->Parent == nullptr || n->Parent->IndentLevel == -1))
                            topLevelChildren.append(n);
                    }
                    int64_t matchedOff = 0;
                    auto matchedNode = FindBestMatchingNode(topLevelChildren, targetOffset, true, &matchedOff, root);
                    if (matchedNode != nullptr) {
                        QString d = matchedNode->Description;
                        d.remove(tagRegex);
                        d = d.trimmed();
                        int64_t delta = targetOffset - matchedOff;
                        if (delta > 0)
                            d += QString(" +0x%1").arg(QString::number(delta, 16).toUpper());
                        return d;
                    }
                }
            }
        }
    }

    return "";
}

std::shared_ptr<NoteNode> ConditionFormatter::GetEffectiveNote(const QMap<QString, QList<std::shared_ptr<NoteNode>>>& notesLookup,
                                                               int64_t addrVal, const QList<RangeEntry>* rangeCache) {
    QString addrStr = QString("0x%1").arg(addrVal, 0, 16);
    if (notesLookup.contains(addrStr)) {
        QList<std::shared_ptr<NoteNode>> workingNodes = ResolveNoteRedirects(notesLookup.value(addrStr), notesLookup);

        for (const auto& n : workingNodes) {
            if (n->IndentLevel == -2)
                return n;
        }
        return workingNodes.isEmpty() ? nullptr : workingNodes.first();
    }

    if (rangeCache != nullptr && addrVal >= 0) {
        for (const auto& match : *rangeCache) {
            if (addrVal >= match.Start && addrVal < match.End && match.Node != nullptr && !match.Node->IsPointerArray) {
                return match.Node;
            }
        }
    }

    return nullptr;
}

} // namespace RALE
