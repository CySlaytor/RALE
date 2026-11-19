#include "ConditionFormatter.h"
#include "../Definitions/RaLogicSyntax.h"
#include "../Utilities/LogicFormatter.h"
#include <QRegularExpression>

namespace RALogicEditor {

QString ConditionFormatter::FormatOperandDisplay(
    const Operand &op,
    bool showDecimal,
    bool showAliases,
    const QMap<QString, QList<std::shared_ptr<NoteNode>>> *notesLookup,
    const QList<RangeEntry> *rangeCache,
    const AchievementConditionGroup *group,
    int rowIndex)
{
    // Standard fallback without size ref override
    return FormatOperandDisplay(op, showDecimal, showAliases, notesLookup, rangeCache, group, rowIndex, op.Size);
}

QString ConditionFormatter::FormatOperandDisplay(
    const Operand &op,
    bool showDecimal,
    bool showAliases,
    const QMap<QString, QList<std::shared_ptr<NoteNode>>> *notesLookup,
    const QList<RangeEntry> *rangeCache,
    const AchievementConditionGroup *group,
    int rowIndex,
    const QString &sizeRef)
{
    if (showAliases && RaLogicSyntax::MemoryTypes.contains(op.Type) && notesLookup != nullptr)
    {
        QString addr = LogicFormatter::NormalizeAddress(op.Value);
        QString alias = ResolveAlias(addr, sizeRef, group, rowIndex, *notesLookup, rangeCache);
        if (!alias.isEmpty()) return alias.trimmed();
    }
    return LogicFormatter::FormatDisplayValue(op, showDecimal, sizeRef);
}

QString ConditionFormatter::ResolveAlias(
    const QString &address,
    const QString &sizeRef, // NEW: Used for Bit extraction
    const AchievementConditionGroup *group,
    int rowIndex,
    const QMap<QString, QList<std::shared_ptr<NoteNode>>> &notesLookup,
    const QList<RangeEntry> *rangeCache)
{
    // Pre-compiled regex patterns to eliminate allocation churn in cell rendering
    static const QRegularExpression validHexRegex("^[0-9A-Fa-f]+$");
    static const QRegularExpression referRegex(R"(refer to \$?0x([0-9a-f]+))", QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression tagRegex(R"(\[.*?\])");
    static const QRegularExpression bitSizeRegex(R"(^(Bit\d+|Lower4|Upper4))", QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression multiSpaceRegex(R"(\s{2,})");
    static const QRegularExpression arrayTagRx(R"(\[[^\]]*?\b(?:(\d+)\s*x\s*(\d+)|(0x[0-9A-Fa-f]+)|(\d+))[\s\-]*bytes?\b[^\]]*\])", QRegularExpression::CaseInsensitiveOption);

    // Robust Hex Parser: Removes 0x, pipes |, and any non-hex characters
    auto ParseHex = [](const QString &s) -> int64_t {
        if (s.trimmed().isEmpty()) return -1;
        QString clean = s;
        clean.remove('|');
        clean = clean.trimmed();
        if (clean.startsWith('+') || clean.startsWith('-'))
            clean = clean.mid(1).trimmed();
        if (clean.startsWith("0x", Qt::CaseInsensitive))
            clean = clean.mid(2).trimmed();
        if (!validHexRegex.match(clean).hasMatch()) return -1;
        bool ok = false;
        int64_t v = clean.toLongLong(&ok, 16);
        return ok ? v : -1;
    };

    auto ExtractRootHeaderLine = [&](const QString &content) -> QString {
        QString norm = content;
        norm.replace("\r\n", "\n").replace('\r', '\n');
        QStringList parts = norm.split('\n', Qt::SkipEmptyParts);
        for (const QString &part : parts)
        {
            QString candidate = part.trimmed();
            if (candidate.startsWith('+') || candidate.startsWith('|'))
                break;

            auto mIt = tagRegex.globalMatch(candidate);
            QString cleaned = candidate;
            while (mIt.hasNext())
            {
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

    // Local fallback function to do a deep search if strict chain mapping breaks due to bad indentation
    std::function<std::shared_ptr<NoteNode>(const QList<std::shared_ptr<NoteNode>> &, int64_t)> FindNodeByOffsetRecursive;
    FindNodeByOffsetRecursive = [&FindNodeByOffsetRecursive, &ParseHex](const QList<std::shared_ptr<NoteNode>> &nodes, int64_t target) -> std::shared_ptr<NoteNode> {
        for (const auto &n : nodes)
        {
            if (n->IndentLevel != -2 && n->IndentLevel != -1)
            {
                int64_t nodeOff = ParseHex(n->Offset);
                if (n->Offset.contains('-')) nodeOff = -nodeOff;

                if (nodeOff != -1 && target >= nodeOff && target < nodeOff + n->Size)
                    return n;
            }

            auto childMatch = FindNodeByOffsetRecursive(n->Children, target);
            if (childMatch != nullptr) return childMatch;
        }
        return nullptr;
    };

    int64_t targetVal = ParseHex(address);

    // 1. Strict Pointer Chain Logic (Offsets)
    // Fix: Check bounds before accessing group.Conditions
    if (group != nullptr && rowIndex > 0 && rowIndex < group->Conditions.size())
    {
        QList<ChainNode> chainStack;
        int scan = rowIndex - 1;
        while (scan >= 0)
        {
            // Additional check to prevent index out of range if group modified externally
            if (scan >= group->Conditions.size())
            {
                scan--;
                continue;
            }

            const auto &prevCond = group->Conditions[scan];
            if (prevCond.Flag == "Add Address")
            {
                // Add to front of list to maintain top-to-bottom order for indexing filtering
                chainStack.prepend(ChainNode{
                    RaLogicSyntax::MemoryTypes.contains(prevCond.LeftOperand.Type),
                    prevCond.LeftOperand.Value
                });
                scan--;
            }
            else break;
        }

        // Handle Array Indexing (Index + Base) imported from AutoCR
        // If an AddAddress chain starts with something that has no code note, we assume it is an Index.
        while (chainStack.size() > 1)
        {
            const auto &first = chainStack.first();
            int64_t baseAddrNum = ParseHex(LogicFormatter::NormalizeAddress(first.Value));
            if (!first.IsMem || GetEffectiveNote(notesLookup, baseAddrNum) == nullptr)
            {
                chainStack.removeFirst();
            }
            else
            {
                break;
            }
        }
        // Ensure we don't accidentally preserve a non-address value if it's the only one left
        if (chainStack.size() == 1)
        {
            const auto &first = chainStack.first();
            int64_t baseAddrNum = ParseHex(LogicFormatter::NormalizeAddress(first.Value));
            if (!first.IsMem || GetEffectiveNote(notesLookup, baseAddrNum) == nullptr)
            {
                chainStack.removeFirst();
            }
        }

        if (!chainStack.isEmpty())
        {
            bool wasPointerChain = !chainStack.isEmpty();
            auto baseNode = chainStack.takeFirst();
            QString normalizedBase = LogicFormatter::NormalizeAddress(baseNode.Value);

            if (notesLookup.contains(normalizedBase))
            {
                // Redirect Resolution Logic added here
                // Check if the Base Note is a Redirect (refer to $0x...)
                QList<std::shared_ptr<NoteNode>> workingNodes = notesLookup.value(normalizedBase);
                int redirects = 0;

                while (redirects < 5 && !workingNodes.isEmpty())
                {
                    std::shared_ptr<NoteNode> root = nullptr;
                    for (const auto &n : workingNodes)
                    {
                        if (n->IndentLevel == -2) { root = n; break; }
                    }
                    if (root == nullptr) root = workingNodes.first();
                    QString content = root->Content;

                    if (content.contains("refer to", Qt::CaseInsensitive))
                    {
                        auto match = referRegex.match(content);
                        if (match.hasMatch())
                        {
                            QString targetAddr = match.captured(1);
                            QString normTarget = LogicFormatter::NormalizeAddress(targetAddr);
                            if (notesLookup.contains(normTarget))
                            {
                                workingNodes = notesLookup.value(normTarget);
                                redirects++;
                                continue;
                            }
                        }
                    }
                    break;
                }

                QList<std::shared_ptr<NoteNode>> currentLevelNodes;
                for (const auto &n : workingNodes)
                {
                    if (n->IndentLevel != -2 && (n->Parent == nullptr || n->Parent->IndentLevel == -1))
                        currentLevelNodes.append(n);
                }

                bool chainValid = true;

                while (!chainStack.isEmpty())
                {
                    auto offsetNode = chainStack.takeFirst();
                    int64_t offset = ParseHex(offsetNode.Value);
                    if (offset == -1) { chainValid = false; break; }

                    std::shared_ptr<NoteNode> match = nullptr;
                    for (const auto &node : currentLevelNodes)
                    {
                        int64_t nodeOff = ParseHex(node->Offset);
                        if (node->Offset.contains('-')) nodeOff = -nodeOff;

                        if (offset >= nodeOff && offset < nodeOff + node->Size)
                        {
                            match = node;
                            break;
                        }
                    }

                    if (match != nullptr) currentLevelNodes = match->Children;
                    else { chainValid = false; break; }
                }

                std::shared_ptr<NoteNode> finalMatchNode = nullptr;
                if (chainValid && targetVal != -1)
                {
                    for (const auto &node : currentLevelNodes)
                    {
                        int64_t nodeOff = ParseHex(node->Offset);
                        if (node->Offset.contains('-')) nodeOff = -nodeOff;

                        if (targetVal >= nodeOff && targetVal < nodeOff + node->Size)
                        {
                            finalMatchNode = node;
                            break;
                        }
                    }
                }

                // CRITICAL FIX: Only fallback to flat searching if we were NOT in a pointer chain.
                // Flat searching a broken array of structs will result in returning the first element
                // found in the file, causing a false alias to persist.
                if (finalMatchNode == nullptr && targetVal != -1 && !wasPointerChain)
                {
                    finalMatchNode = FindNodeByOffsetRecursive(workingNodes, targetVal);
                }

                if (finalMatchNode != nullptr)
                {
                    QString desc = finalMatchNode->Description;
                    desc.remove(tagRegex);
                    desc = desc.trimmed();

                    // BIT EXTRACTION
                    if (!sizeRef.isEmpty() && bitSizeRegex.match(sizeRef).hasMatch())
                    {
                        QRegularExpression bitRegex(QString(R"(^%1\s*[:=|-]\s*(.*)$)").arg(QRegularExpression::escape(sizeRef)),
                                                    QRegularExpression::CaseInsensitiveOption | QRegularExpression::MultilineOption);
                        auto m = bitRegex.match(finalMatchNode->Content);
                        if (m.hasMatch())
                        {
                            desc = m.captured(1).trimmed();
                        }
                    }

                    int64_t nodeOff = ParseHex(finalMatchNode->Offset);
                    if (finalMatchNode->Offset.contains('-')) nodeOff = -nodeOff;
                    int64_t delta = targetVal - nodeOff;
                    if (delta > 0)
                    {
                        QString suffix = QString(" +0x%1").arg(QString::number(delta, 16).toUpper());
                        if (!desc.endsWith(suffix, Qt::CaseInsensitive))
                            desc += suffix;
                    }
                    return desc;
                }

                // If we were in a pointer chain and failed to find the node, abort and show Hex.
                if (wasPointerChain && finalMatchNode == nullptr) return "";
            }
        }
    }

    // 2. Direct Lookup & Static Array Element Resolution
    if (notesLookup.contains(address))
    {
        // Redirect Resolution Logic added here too
        QList<std::shared_ptr<NoteNode>> workingNodes = notesLookup.value(address);
        int redirects = 0;

        while (redirects < 5 && !workingNodes.isEmpty())
        {
            std::shared_ptr<NoteNode> root = nullptr;
            for (const auto &n : workingNodes)
            {
                if (n->IndentLevel == -2) { root = n; break; }
            }
            if (root == nullptr) root = workingNodes.first();
            QString content = root->Content;

            if (content.contains("refer to", Qt::CaseInsensitive))
            {
                auto match = referRegex.match(content);
                if (match.hasMatch())
                {
                    QString targetAddr = match.captured(1);
                    QString normTarget = LogicFormatter::NormalizeAddress(targetAddr);
                    if (notesLookup.contains(normTarget))
                    {
                        workingNodes = notesLookup.value(normTarget);
                        redirects++;
                        continue;
                    }
                }
            }
            break;
        }

        if (!workingNodes.isEmpty())
        {
            std::shared_ptr<NoteNode> main = nullptr;
            for (const auto &n : workingNodes)
            {
                if (n->IndentLevel == -2) { main = n; break; }
            }
            if (main == nullptr) main = workingNodes.first();

            QString desc = main->Description;

            if (desc.isEmpty() || desc.compare("Full Note", Qt::CaseInsensitive) == 0)
            {
                if (!main->Content.isEmpty())
                {
                    desc = ExtractRootHeaderLine(main->Content);
                }
            }

            auto matchIterator = tagRegex.globalMatch(desc);
            QString cleanDesc = desc;
            while (matchIterator.hasNext())
            {
                auto m = matchIterator.next();
                if (m.captured(0).contains("Pointer", Qt::CaseInsensitive))
                    cleanDesc.replace(m.captured(0), "[Pointer]");
                else
                    cleanDesc.replace(m.captured(0), "");
            }
            desc = cleanDesc.trimmed();

            // BIT EXTRACTION
            if (!sizeRef.isEmpty() && bitSizeRegex.match(sizeRef).hasMatch())
            {
                QRegularExpression bitRegex(QString(R"(^%1\s*[:=|-]\s*(.*)$)").arg(QRegularExpression::escape(sizeRef)),
                                            QRegularExpression::CaseInsensitiveOption | QRegularExpression::MultilineOption);
                auto m = bitRegex.match(main->Content);
                if (m.hasMatch())
                {
                    desc = m.captured(1).trimmed();
                }
            }

            desc.replace(multiSpaceRegex, " ");
            return desc;
        }
    }

    // 3. Ranged Direct Lookup (Including PF2 Static Array Structures)
    if (rangeCache != nullptr && targetVal != -1)
    {
        const RangeEntry *bestMatch = nullptr;
        for (const auto &match : *rangeCache)
        {
            if (targetVal >= match.Start && targetVal < match.End && match.Node != nullptr)
            {
                if (bestMatch == nullptr || match.Start > bestMatch->Start ||
                    (match.Start == bestMatch->Start && (match.End - match.Start) < (bestMatch->End - bestMatch->Start)))
                {
                    bestMatch = &match;
                }
            }
        }

        if (bestMatch != nullptr)
        {
            int64_t relOffset = targetVal - bestMatch->Start;
            std::shared_ptr<NoteNode> specificMatch = nullptr;

            // For range matches, the target is usually an offset within the struct.
            // We can try to find the specific offset node first, before falling back to the struct root!
            QString rootHeader = ExtractRootHeaderLine(bestMatch->Node->Content);
            bool isPointerRoot = rootHeader.contains("Pointer", Qt::CaseInsensitive);
            if (!isPointerRoot)
            {
                QList<std::shared_ptr<NoteNode>> topLevelChildren;
                for (const auto &child : bestMatch->Node->Children)
                {
                    if (child->IndentLevel == -1)
                        topLevelChildren.append(child->Children);
                    else if (child->IndentLevel != -2)
                        topLevelChildren.append(child);
                }

                for (const auto &child : topLevelChildren)
                {
                    if (child->IsArrayChild) continue;
                    int64_t childOff = ParseHex(child->Offset);
                    if (child->Offset.contains('-')) childOff = -childOff;

                    if (childOff != -1 && relOffset >= childOff && relOffset < childOff + child->Size)
                    {
                        specificMatch = child;
                        break;
                    }
                }
            }

            std::shared_ptr<NoteNode> activeNode = (specificMatch != nullptr) ? specificMatch : bestMatch->Node;

            QString desc = activeNode->Description;
            if (desc.isEmpty() || desc.compare("Full Note", Qt::CaseInsensitive) == 0)
            {
                desc = ExtractRootHeaderLine(activeNode->Content);
            }

            auto matchIterator = tagRegex.globalMatch(desc);
            QString cleanDesc = desc;
            while (matchIterator.hasNext())
            {
                auto m = matchIterator.next();
                if (m.captured(0).contains("Pointer", Qt::CaseInsensitive))
                    cleanDesc.replace(m.captured(0), "[Pointer]");
                else
                    cleanDesc.replace(m.captured(0), "");
            }
            desc = cleanDesc.trimmed();

            // BIT EXTRACTION
            if (!sizeRef.isEmpty() && bitSizeRegex.match(sizeRef).hasMatch())
            {
                QRegularExpression bitRegex(QString(R"(^%1\s*[:=|-]\s*(.*)$)").arg(QRegularExpression::escape(sizeRef)),
                                            QRegularExpression::CaseInsensitiveOption | QRegularExpression::MultilineOption);
                auto m = bitRegex.match(activeNode->Content);
                if (m.hasMatch())
                {
                    desc = m.captured(1).trimmed();
                }
            }

            desc.replace(multiSpaceRegex, " ");

            int64_t nodeOff = ParseHex(activeNode->Offset);
            if (activeNode->Offset.contains('-')) nodeOff = -nodeOff;
            // If it was the root struct match, use the Start address. Otherwise use the specific node offset.
            int64_t baseForDelta = (specificMatch != nullptr && nodeOff != -1) ? (bestMatch->Start + nodeOff) : bestMatch->Start;

            int64_t delta = targetVal - baseForDelta;
            if (delta > 0)
            {
                QString suffix = QString(" +0x%1").arg(QString::number(delta, 16).toUpper());
                if (!desc.endsWith(suffix, Qt::CaseInsensitive))
                    desc += suffix;
            }
            return desc;
        }
    }

    if (targetVal != -1)
    {
        // Check for direct static array elements mapped from base note
        for (auto it = notesLookup.cbegin(); it != notesLookup.cend(); ++it)
        {
            int64_t baseAddr = ParseHex(it.key());
            if (baseAddr == -1 || targetVal < baseAddr) continue;

            const auto &nodes = it.value();
            if (nodes.isEmpty()) continue;

            std::shared_ptr<NoteNode> root = nullptr;
            for (const auto &n : nodes)
            {
                if (n->IndentLevel == -2) { root = n; break; }
            }
            if (root == nullptr) root = nodes.first();

            QString firstLine = root->Content.split('\n', Qt::SkipEmptyParts).value(0, "");
            if (firstLine.contains("Pointer", Qt::CaseInsensitive)) continue;

            auto arrMatch = arrayTagRx.match(firstLine);
            if (arrMatch.hasMatch())
            {
                int64_t arrSize = 0;
                if (!arrMatch.captured(1).isEmpty() && !arrMatch.captured(2).isEmpty())
                    arrSize = arrMatch.captured(1).toLongLong() * arrMatch.captured(2).toLongLong();
                else if (!arrMatch.captured(3).isEmpty())
                    arrSize = arrMatch.captured(3).mid(2).toLongLong(nullptr, 16);
                else if (!arrMatch.captured(4).isEmpty())
                    arrSize = arrMatch.captured(4).toLongLong();

                if (arrSize > 0 && targetVal < baseAddr + arrSize)
                {
                    int64_t targetOffset = targetVal - baseAddr;
                    for (const auto &n : nodes)
                    {
                        if (n->IndentLevel == -2 || n->IndentLevel == -1 || n->IsArrayChild) continue;
                        int64_t offVal = ParseHex(n->Offset);
                        if (n->Offset.contains('-')) offVal = -offVal;
                        if (offVal != -1 && targetOffset >= offVal && targetOffset < offVal + n->Size)
                        {
                            QString d = n->Description;
                            d.remove(tagRegex);
                            d = d.trimmed();
                            int64_t delta = targetOffset - offVal;
                            if (delta > 0)
                                d += QString(" +0x%1").arg(QString::number(delta, 16).toUpper());
                            return d;
                        }
                    }
                }
            }
        }
    }

    return "";
}

std::shared_ptr<NoteNode> ConditionFormatter::GetEffectiveNote(
    const QMap<QString, QList<std::shared_ptr<NoteNode>>> &notesLookup,
    int64_t addrVal)
{
    QString addrStr = QString("0x%1").arg(addrVal, 0, 16);
    if (!notesLookup.contains(addrStr))
        return nullptr;

    QList<std::shared_ptr<NoteNode>> workingNodes = notesLookup.value(addrStr);
    int redirects = 0;
    static const QRegularExpression referRegex(R"(refer to \$?0x([0-9a-f]+))", QRegularExpression::CaseInsensitiveOption);

    while (redirects < 5 && !workingNodes.isEmpty())
    {
        std::shared_ptr<NoteNode> root = nullptr;
        for (const auto &n : workingNodes)
        {
            if (n->IndentLevel == -2) { root = n; break; }
        }
        if (root == nullptr) root = workingNodes.first();
        QString content = root->Content;

        if (content.contains("refer to", Qt::CaseInsensitive))
        {
            auto match = referRegex.match(content);
            if (match.hasMatch())
            {
                QString targetAddr = match.captured(1);
                QString normTarget = LogicFormatter::NormalizeAddress(targetAddr);
                if (notesLookup.contains(normTarget))
                {
                    workingNodes = notesLookup.value(normTarget);
                    redirects++;
                    continue;
                }
            }
        }
        break;
    }

    for (const auto &n : workingNodes)
    {
        if (n->IndentLevel == -2) return n;
    }
    return workingNodes.isEmpty() ? nullptr : workingNodes.first();
}

} // namespace RALogicEditor
