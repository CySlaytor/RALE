#include "GeneratorNoteDuplicator.h"

#include "CodeNoteTemplateService.h"
#include "../Common/CodeNoteModifier.h"
#include "../Common/ConditionFormatter.h"
#include "../Definitions/RaLogicSyntax.h"
#include "../Parsers/PointerTreeParser.h"
#include "../Utilities/LogicFormatter.h"

#include <QMap>
#include <QRegularExpression>
#include <QSet>

#include <algorithm>
#include <cmath>
#include <functional>

namespace RALE {

namespace {

bool IsMemoryOperand(const Operand& op) { return RaLogicSyntax::MemoryTypes.contains(op.Type) && !op.Value.trimmed().isEmpty(); }

bool TryParseHexValue(const QString& input, int64_t& outVal) {
    QString clean = input.trimmed();
    clean.remove('|');
    clean = clean.trimmed();
    bool isNeg = clean.startsWith('-');
    if (isNeg || clean.startsWith('+'))
        clean = clean.mid(1).trimmed();
    if (clean.startsWith("0x", Qt::CaseInsensitive))
        clean = clean.mid(2).trimmed();
    if (clean.isEmpty())
        return false;

    bool ok = false;
    uint64_t uv = clean.toULongLong(&ok, 16);
    if (!ok)
        return false;
    outVal = isNeg ? -static_cast<int64_t>(uv) : static_cast<int64_t>(uv);
    return true;
}

bool OperandMatchesRuleAddress(const Operand& op, const QString& targetAddress) {
    if (!IsMemoryOperand(op))
        return false;

    int64_t opVal = 0;
    int64_t targetVal = 0;
    if (TryParseHexValue(op.Value, opVal) && TryParseHexValue(targetAddress, targetVal))
        return opVal == targetVal;

    QString cleanTarget = targetAddress.startsWith("0x", Qt::CaseInsensitive) ? targetAddress.mid(2) : targetAddress;
    QString cleanOp = op.Value.startsWith("0x", Qt::CaseInsensitive) ? op.Value.mid(2) : op.Value;
    return cleanOp.compare(cleanTarget, Qt::CaseInsensitive) == 0;
}

const GeneratorRule* FindMatchingRule(const Operand& op, const GeneratorDefinition& def) {
    for (const auto& rule : def.Rules) {
        if (OperandMatchesRuleAddress(op, rule.SourceAddress))
            return &rule;
    }
    return nullptr;
}

bool UnitMatchesRules(const QList<AchievementCondition>& unit, const GeneratorDefinition& def) {
    for (const auto& cond : unit) {
        if (FindMatchingRule(cond.LeftOperand, def) != nullptr || FindMatchingRule(cond.RightOperand, def) != nullptr)
            return true;
    }
    return false;
}

QList<QList<AchievementCondition>> SplitChainUnits(const QList<AchievementCondition>& conditions) {
    QList<QList<AchievementCondition>> units;
    QList<AchievementCondition> currentUnit;
    for (const auto& cond : conditions) {
        currentUnit.append(cond);
        if (cond.Flag != "Add Address") {
            units.append(currentUnit);
            currentUnit.clear();
        }
    }
    if (!currentUnit.isEmpty())
        units.append(currentUnit);
    return units;
}

QString FormatAddressMatchingWidth(int64_t addrVal, const QString& templateAddress) {
    QString clean = templateAddress.trimmed();
    if (clean.startsWith("0x", Qt::CaseInsensitive))
        clean = clean.mid(2);
    int padLen = std::max(1, static_cast<int>(clean.length()));
    QString hex = QString::number(static_cast<quint64>(addrVal), 16);
    if (hex.length() < padLen)
        hex = hex.rightJustified(padLen, '0');
    return "0x" + hex.toLower();
}

QString FormatOffsetToken(int64_t offsetVal) {
    QString sign = (offsetVal < 0) ? "-" : "+";
    QString hex = QString::number(std::abs(offsetVal), 16).toLower();
    if (hex.length() < 2)
        hex = hex.rightJustified(2, '0');
    return sign + "0x" + hex;
}

bool IsMeaningfulNoteText(const QString& noteText) {
    QString trimmed = noteText.trimmed();
    return !trimmed.isEmpty() && trimmed != "[No code note for this address]";
}

bool IsRedirectStub(const QString& noteText, QString& outTargetKey) {
    static const QRegularExpression structRegex(QString::fromUtf8(R"(^[.\+\s\x{2500}-\x{257F}])"), QRegularExpression::MultilineOption);
    static const QRegularExpression referralRegex(R"(refer to \$?0x([0-9a-f]+))", QRegularExpression::CaseInsensitiveOption);

    if (structRegex.match(noteText).hasMatch())
        return false;
    auto m = referralRegex.match(noteText);
    if (!m.hasMatch())
        return false;

    outTargetKey = LogicFormatter::NormalizeAddress(m.captured(1));
    return !outTargetKey.isEmpty();
}

QList<std::shared_ptr<NoteNode>> GetTopLevelNodes(const QList<std::shared_ptr<NoteNode>>& parsedNodes,
                                                  std::shared_ptr<NoteNode>* outRoot = nullptr) {
    QList<std::shared_ptr<NoteNode>> top;
    std::shared_ptr<NoteNode> root = nullptr;
    for (const auto& n : parsedNodes) {
        if (n->IndentLevel == -2) {
            root = n;
            for (const auto& ch : n->Children) {
                if (ch->IndentLevel == -1)
                    top.append(ch->Children);
                else if (ch->IndentLevel != -2)
                    top.append(ch);
            }
            break;
        }
    }
    if (top.isEmpty()) {
        for (const auto& n : parsedNodes) {
            if (n->IndentLevel != -2 && (n->Parent == nullptr || n->Parent->IndentLevel == -1))
                top.append(n);
        }
    }
    if (outRoot != nullptr)
        *outRoot = root;
    return top;
}

// Checks whether an exact offset path already exists in a parsed note tree.
// Returns true if every step matches an explicit (or non-fallback) node and the target node is already documented.
bool ExactPointerChainExistsInTree(const QList<std::shared_ptr<NoteNode>>& parsedNodes, const QList<int64_t>& offsets) {
    if (offsets.isEmpty() || parsedNodes.isEmpty())
        return false;

    QList<std::shared_ptr<NoteNode>> currentLevel = GetTopLevelNodes(parsedNodes);
    std::shared_ptr<NoteNode> matchedNode = nullptr;

    for (int step = 0; step < offsets.size(); ++step) {
        int64_t targetOff = offsets[step];
        std::shared_ptr<NoteNode> found = nullptr;

        for (const auto& node : currentLevel) {
            if (node == nullptr || node->IsInlineArray)
                continue;
            int64_t nodeOff = 0;
            if (!TryParseHexValue(node->Offset, nodeOff))
                continue;
            if (nodeOff == targetOff) {
                // Prefer explicit (non-inherited) node over template-inherited
                if (!node->IsTemplateInherited) {
                    found = node;
                    break;
                }
                if (found == nullptr)
                    found = node;
            }
        }

        if (found == nullptr)
            return false;

        matchedNode = found;
        currentLevel = found->Children;
    }

    if (matchedNode == nullptr)
        return false;

    return !matchedNode->Description.trimmed().isEmpty() || !matchedNode->Content.trimmed().isEmpty() ||
           !matchedNode->Children.isEmpty();
}

QString ExpandHeaderForRule(const QString& sourceHeaderText, const GeneratorRule* rule, int copyIndex, uint32_t deltaBytes) {
    if (rule != nullptr && rule->NoteExplicitTitles.contains(copyIndex)) {
        const QString explicitTitle = rule->NoteExplicitTitles.value(copyIndex).trimmed();
        if (!explicitTitle.isEmpty())
            return explicitTitle;
    }

    auto tmpl = CodeNoteTemplateService::ParseTemplateFromNote(sourceHeaderText);
    QString headerTemplate = (rule != nullptr && !rule->NoteHeaderTemplate.trimmed().isEmpty())
                                 ? rule->NoteHeaderTemplate.trimmed()
                                 : tmpl.HeaderTemplate;
    QString customName = (rule != nullptr && rule->NoteCustomNames.contains(copyIndex))
                             ? rule->NoteCustomNames.value(copyIndex)
                             : tmpl.FirstCustomName;

    return CodeNoteTemplateService::ExpandTemplate(headerTemplate, tmpl.BaseIndex + copyIndex, deltaBytes, customName);
}

// Serializes a NoteNode and its non-inherited subtree starting at relative '+' depth 0.
// overrideRootOffset sets the generated offset of the root node being cloned (depth == 0).
QString SerializeNoteNodeSubtree(const std::shared_ptr<NoteNode>& sourceNode, int64_t overrideRootOffset, const QString& overrideHeader,
                                 const QString& sepString, const QList<int64_t>* remainingSrcOffsets = nullptr,
                                 const QList<int64_t>* remainingDstOffsets = nullptr, int copyIndex = 1) {
    QStringList lines;

    std::function<void(const std::shared_ptr<NoteNode>&, int, const QString*)> appendNode =
        [&](const std::shared_ptr<NoteNode>& node, int depth, const QString* headerOverride) {
            if (node == nullptr)
                return;

            int64_t nodeOffVal = 0;
            if (depth == 0) {
                nodeOffVal = overrideRootOffset;
            } else {
                TryParseHexValue(node->Offset, nodeOffVal);
            }

            QString desc = (headerOverride != nullptr) ? *headerOverride : node->Description.trimmed();
            int remIdx = depth - 1;
            if (headerOverride == nullptr && remainingSrcOffsets != nullptr && remainingDstOffsets != nullptr && remIdx >= 0 &&
                remIdx < remainingSrcOffsets->size() && remIdx < remainingDstOffsets->size() &&
                nodeOffVal == remainingSrcOffsets->at(remIdx) && remainingDstOffsets->at(remIdx) != remainingSrcOffsets->at(remIdx)) {
                int64_t oldOff = remainingSrcOffsets->at(remIdx);
                int64_t newOff = remainingDstOffsets->at(remIdx);
                nodeOffVal = newOff;
                if (!desc.isEmpty()) {
                    auto tmpl = CodeNoteTemplateService::ParseTemplateFromNote(desc);
                    uint32_t delta = static_cast<uint32_t>(std::llabs(newOff - oldOff));
                    desc = CodeNoteTemplateService::ExpandTemplate(tmpl.HeaderTemplate, tmpl.BaseIndex + copyIndex, delta,
                                                                   tmpl.FirstCustomName);
                }
            }

            QString indent(depth, '+');
            QString offToken = FormatOffsetToken(nodeOffVal);
            if (!desc.isEmpty()) {
                lines.append(QString("%1%2%3%4").arg(indent, offToken, sepString, desc));
            } else {
                lines.append(QString("%1%2").arg(indent, offToken));
            }

            if (!node->Content.trimmed().isEmpty()) {
                QString normContent = node->Content;
                normContent.replace("\r\n", "\n").replace('\r', '\n');
                for (const QString& rawLine : normContent.split('\n', Qt::SkipEmptyParts)) {
                    QString clean = rawLine.trimmed();
                    while (!clean.isEmpty() && (clean[0] == '.' || clean[0] == ' '))
                        clean = clean.mid(1);
                    clean = clean.trimmed();
                    if (!clean.isEmpty())
                        lines.append(clean);
                }
            }

            for (const auto& child : node->Children) {
                if (child == nullptr || child->IsTemplateInherited)
                    continue;
                appendNode(child, depth + 1, nullptr);
            }
        };

    appendNode(sourceNode, 0, &overrideHeader);
    return lines.join("\n");
}

QString OffsetPathKey(const QList<int64_t>& offsets) {
    QStringList parts;
    parts.reserve(offsets.size());
    for (int64_t off : offsets)
        parts.append(QString::number(off));
    return parts.join('/');
}

} // namespace

GeneratorNoteDuplicator::DuplicationResult GeneratorNoteDuplicator::DuplicateNotesForGenerator(
    const QList<AchievementConditionGroup>& finalizedGroups, int masterGroupIndex, const GeneratorDefinition& def,
    const QList<CodeNote>& currentNotes, int consoleId, bool useUnicodeTree, bool indentDescriptions) {
    DuplicationResult result;

    if (masterGroupIndex < 0 || masterGroupIndex >= finalizedGroups.size() || def.Copies <= 0 || def.Rules.isEmpty())
        return result;

    // 1. Extract template units and align with finalized Phase 1 copy units
    QList<AchievementCondition> templateConds;
    if (def.IsArray) {
        int tmplCount = std::min<int>(def.TemplateConditionCount, finalizedGroups[masterGroupIndex].Conditions.size());
        if (tmplCount <= 0)
            return result;
        templateConds = finalizedGroups[masterGroupIndex].Conditions.mid(0, tmplCount);
    } else {
        templateConds = finalizedGroups[masterGroupIndex].Conditions;
    }

    QList<QList<AchievementCondition>> allTemplateUnits = SplitChainUnits(templateConds);
    if (allTemplateUnits.isEmpty())
        return result;

    QList<QList<AchievementCondition>> expectedCopyUnits;
    if (def.IsArray && def.DuplicateSelectedOnly) {
        for (const auto& u : allTemplateUnits) {
            if (UnitMatchesRules(u, def))
                expectedCopyUnits.append(u);
        }
    } else {
        expectedCopyUnits = allTemplateUnits;
    }

    if (expectedCopyUnits.isEmpty())
        return result;

    int expectedUnitCondCount = 0;
    for (const auto& u : expectedCopyUnits)
        expectedUnitCondCount += u.size();
    if (expectedUnitCondCount <= 0)
        return result;

    // 2. Build working notes lookup & parsed trees
    PointerTreeParser parser;
    QMap<QString, CodeNote> workingNotesMap;
    QSet<QString> preExistingKeys;
    QMap<QString, QList<std::shared_ptr<NoteNode>>> parsedNotesCache;
    QList<ConditionFormatter::RangeEntry> rangeCache;
    QSet<int64_t> rangedStarts;

    auto indexNote = [&](const CodeNote& note) {
        QString key = LogicFormatter::NormalizeAddress(note.Address);
        workingNotesMap.insert(key, note);
        auto nodes = parser.ParseNoteText(note.Note);
        parsedNotesCache.insert(key, nodes);

        if (!nodes.isEmpty() && nodes[0]->Size > 1) {
            int64_t startAddr = 0;
            if (TryParseHexValue(key, startAddr) && !rangedStarts.contains(startAddr)) {
                rangedStarts.insert(startAddr);
                rangeCache.append(ConditionFormatter::RangeEntry{startAddr, startAddr + nodes[0]->Size, nodes[0]});
            }
        }
    };

    for (const auto& n : currentNotes) {
        QString key = LogicFormatter::NormalizeAddress(n.Address);
        if (IsMeaningfulNoteText(n.Note))
            preExistingKeys.insert(key);
        indexNote(n);
    }

    auto resolveRedirectKey = [&](const QString& startKey) -> QString {
        QString cur = startKey;
        QSet<QString> visited;
        for (int hop = 0; hop < 5; ++hop) {
            if (visited.contains(cur) || !workingNotesMap.contains(cur))
                break;
            visited.insert(cur);
            QString nextTarget;
            if (IsRedirectStub(workingNotesMap.value(cur).Note, nextTarget) && workingNotesMap.contains(nextTarget))
                cur = nextTarget;
            else
                break;
        }
        return cur;
    };

    auto findNarrowestRangeFor = [&](int64_t addrVal, bool excludePointerArrays) -> const ConditionFormatter::RangeEntry* {
        const ConditionFormatter::RangeEntry* best = nullptr;
        for (const auto& entry : rangeCache) {
            if (entry.Node == nullptr)
                continue;
            if (excludePointerArrays && entry.Node->IsPointerArray)
                continue;
            if (addrVal >= entry.Start && addrVal < entry.End) {
                if (best == nullptr || entry.Start > best->Start ||
                    (entry.Start == best->Start && (entry.End - entry.Start) < (best->End - best->Start))) {
                    best = &entry;
                }
            }
        }
        return best;
    };

    auto hasEffectiveNote = [&](const QString& addrStr) -> bool {
        int64_t addrVal = 0;
        if (!TryParseHexValue(addrStr, addrVal))
            return false;
        QString key = LogicFormatter::NormalizeAddress(addrStr);
        if (workingNotesMap.contains(key) && IsMeaningfulNoteText(workingNotesMap.value(key).Note))
            return true;
        return findNarrowestRangeFor(addrVal, true) != nullptr;
    };

    const auto globalStyle = CodeNoteModifier::DetectGlobalStyle(currentNotes);
    QString sepStr = globalStyle.GetSeparatorString();

    // Pending subtree injections grouped by base CodeNote normalized key
    QMap<QString, QList<CodeNoteModifier::SubtreeInjection>> pendingInjections;
    QMap<QString, QSet<QString>> queuedPathKeys;
    QSet<QString> batchTouchedStaticKeys;

    auto queueSubtreeInjection = [&](const QString& baseKey, const QList<int64_t>& dstPath, const QString& nodeBlockText) -> bool {
        QString pathKey = OffsetPathKey(dstPath);
        if (queuedPathKeys[baseKey].contains(pathKey))
            return false;

        if (parsedNotesCache.contains(baseKey) && ExactPointerChainExistsInTree(parsedNotesCache.value(baseKey), dstPath)) {
            result.SkippedExistingCount++;
            queuedPathKeys[baseKey].insert(pathKey);
            return false;
        }

        queuedPathKeys[baseKey].insert(pathKey);
        pendingInjections[baseKey].append(CodeNoteModifier::SubtreeInjection{dstPath, nodeBlockText});
        return true;
    };

    auto handleStaticAddressPair = [&](const Operand& srcOp, const Operand& dstOp, const GeneratorRule* matchedRule, int copyIndex) {
        if (!IsMemoryOperand(srcOp) || !IsMemoryOperand(dstOp))
            return;

        int64_t srcVal = 0;
        int64_t dstVal = 0;
        if (!TryParseHexValue(srcOp.Value, srcVal) || !TryParseHexValue(dstOp.Value, dstVal) || srcVal == dstVal)
            return;

        QString srcKey = LogicFormatter::NormalizeAddress(srcOp.Value);
        QString dstKey = LogicFormatter::NormalizeAddress(dstOp.Value);

        // If already handled in this batch, do not overwrite or double-count
        if (batchTouchedStaticKeys.contains(dstKey))
            return;

        // 1. Exact standalone source CodeNote
        if (workingNotesMap.contains(srcKey) && IsMeaningfulNoteText(workingNotesMap.value(srcKey).Note)) {
            QString resolvedSrcKey = resolveRedirectKey(srcKey);
            const CodeNote& srcNote = workingNotesMap.value(resolvedSrcKey);
            if (!IsMeaningfulNoteText(srcNote.Note))
                return;

            // Smart existence check: skip if target already has a meaningful standalone note or exact range offset
            if (preExistingKeys.contains(dstKey)) {
                result.SkippedExistingCount++;
                batchTouchedStaticKeys.insert(dstKey);
                return;
            }
            if (const auto* dstRange = findNarrowestRangeFor(dstVal, true)) {
                QString dstRangeKey = LogicFormatter::NormalizeAddress(QString("0x%1").arg(dstRange->Start, 0, 16));
                QString resolvedDstRangeKey = resolveRedirectKey(dstRangeKey);
                if (parsedNotesCache.contains(resolvedDstRangeKey) &&
                    ExactPointerChainExistsInTree(parsedNotesCache.value(resolvedDstRangeKey), {dstVal - dstRange->Start})) {
                    result.SkippedExistingCount++;
                    batchTouchedStaticKeys.insert(dstKey);
                    return;
                }
            }

            auto tmpl = CodeNoteTemplateService::ParseTemplateFromNote(srcNote.Note);
            uint32_t deltaBytes = static_cast<uint32_t>(std::llabs(dstVal - srcVal));
            QString newHeader = ExpandHeaderForRule(srcNote.Note, matchedRule, copyIndex, deltaBytes);

            CodeNote newNote;
            newNote.User = "Local";
            newNote.Address = FormatAddressMatchingWidth(dstVal, workingNotesMap.value(srcKey).Address);
            newNote.Note = newHeader + tmpl.BodyText;

            indexNote(newNote);
            batchTouchedStaticKeys.insert(dstKey);
            result.CreatedOrModifiedCount++;
            return;
        }

        // 2. Range-contained static source address (e.g. offset inside a root struct note)
        if (const auto* srcRange = findNarrowestRangeFor(srcVal, true)) {
            QString rangeKey = LogicFormatter::NormalizeAddress(QString("0x%1").arg(srcRange->Start, 0, 16));
            QString resolvedRangeKey = resolveRedirectKey(rangeKey);
            const auto& rangeNodes = parsedNotesCache.value(resolvedRangeKey);
            QList<std::shared_ptr<NoteNode>> topNodes = GetTopLevelNodes(rangeNodes);

            int64_t srcRel = srcVal - srcRange->Start;
            int64_t matchedOff = 0;
            auto srcNode = ConditionFormatter::FindBestMatchingNode(topNodes, srcRel, true, &matchedOff);
            if (srcNode == nullptr)
                return;

            if (srcNode->IsTemplateInherited) {
                if (auto realSrc = srcNode->TemplateSource.lock())
                    srcNode = realSrc;
            }

            QString headerText = srcNode->Description.trimmed();
            if (headerText.isEmpty())
                return;

            uint32_t deltaBytes = static_cast<uint32_t>(std::llabs(dstVal - srcVal));
            QString newHeader = ExpandHeaderForRule(headerText, matchedRule, copyIndex, deltaBytes);

            if (dstVal >= srcRange->Start && dstVal < srcRange->End) {
                // Inject as child offset inside the owning range note
                int64_t dstRel = dstVal - srcRange->Start;
                QString nodeBlock = SerializeNoteNodeSubtree(srcNode, dstRel, newHeader, sepStr, nullptr, nullptr, copyIndex);
                if (queueSubtreeInjection(resolvedRangeKey, {dstRel}, nodeBlock))
                    result.CreatedOrModifiedCount++;
            } else {
                // Target steps outside the range note: create a standalone CodeNote if it doesn't already exist
                if (preExistingKeys.contains(dstKey)) {
                    result.SkippedExistingCount++;
                    batchTouchedStaticKeys.insert(dstKey);
                    return;
                }

                QStringList childLines;
                if (!srcNode->Content.trimmed().isEmpty()) {
                    QString norm = srcNode->Content;
                    norm.replace("\r\n", "\n").replace('\r', '\n');
                    childLines.append(norm.split('\n', Qt::SkipEmptyParts));
                }

                std::function<void(const std::shared_ptr<NoteNode>&, int)> collectChildren =
                    [&](const std::shared_ptr<NoteNode>& n, int depth) {
                        for (const auto& ch : n->Children) {
                            if (ch == nullptr || ch->IsTemplateInherited)
                                continue;
                            int64_t cOff = 0;
                            TryParseHexValue(ch->Offset, cOff);
                            QString indent(depth, '+');
                            QString offTok = FormatOffsetToken(cOff);
                            childLines.append(ch->Description.trimmed().isEmpty()
                                                  ? (indent + offTok)
                                                  : QString("%1%2%3%4").arg(indent, offTok, sepStr, ch->Description.trimmed()));
                            if (!ch->Content.trimmed().isEmpty()) {
                                QString cNorm = ch->Content;
                                cNorm.replace("\r\n", "\n").replace('\r', '\n');
                                childLines.append(cNorm.split('\n', Qt::SkipEmptyParts));
                            }
                            collectChildren(ch, depth + 1);
                        }
                    };
                collectChildren(srcNode, 0);

                CodeNote newNote;
                newNote.User = "Local";
                newNote.Address = FormatAddressMatchingWidth(dstVal, srcOp.Value);
                newNote.Note = childLines.isEmpty() ? newHeader : (newHeader + "\r\n" + childLines.join("\r\n"));

                indexNote(newNote);
                batchTouchedStaticKeys.insert(dstKey);
                result.CreatedOrModifiedCount++;
            }
        }
    };

    // 3. Process each copy i = 1 .. def.Copies using Phase 1 finalized groups
    for (int i = 1; i <= def.Copies; ++i) {
        QList<AchievementCondition> copyConds;
        if (!def.IsArray) {
            if (masterGroupIndex + i >= finalizedGroups.size())
                break;
            copyConds = finalizedGroups[masterGroupIndex + i].Conditions;
        } else {
            int startIdx = def.TemplateConditionCount + (i - 1) * expectedUnitCondCount;
            if (startIdx + expectedUnitCondCount > finalizedGroups[masterGroupIndex].Conditions.size())
                break;
            copyConds = finalizedGroups[masterGroupIndex].Conditions.mid(startIdx, expectedUnitCondCount);
        }

        QList<QList<AchievementCondition>> copyUnits = SplitChainUnits(copyConds);
        if (copyUnits.size() != expectedCopyUnits.size())
            continue;

        for (int uIdx = 0; uIdx < expectedCopyUnits.size(); ++uIdx) {
            const auto& srcUnit = expectedCopyUnits[uIdx];
            const auto& dstUnit = copyUnits[uIdx];
            if (srcUnit.size() != dstUnit.size() || !UnitMatchesRules(srcUnit, def))
                continue;

            // Check standalone RightOperand on the terminating condition
            const auto& srcLast = srcUnit.last();
            const auto& dstLast = dstUnit.last();
            if (const GeneratorRule* rightRule = FindMatchingRule(srcLast.RightOperand, def)) {
                handleStaticAddressPair(srcLast.RightOperand, dstLast.RightOperand, rightRule, i);
            }

            // Static single-condition unit
            if (srcUnit.size() == 1) {
                if (const GeneratorRule* leftRule = FindMatchingRule(srcLast.LeftOperand, def)) {
                    handleStaticAddressPair(srcLast.LeftOperand, dstLast.LeftOperand, leftRule, i);
                }
                continue;
            }

            // Dynamic AddAddress chain: strip leading unnoted array-index AddAddress rows
            int baseIdx = -1;
            for (int k = 0; k < srcUnit.size() - 1; ++k) {
                if (srcUnit[k].Flag == "Add Address" && IsMemoryOperand(srcUnit[k].LeftOperand) &&
                    hasEffectiveNote(srcUnit[k].LeftOperand.Value)) {
                    baseIdx = k;
                    break;
                }
            }

            if (baseIdx == -1) {
                // No AddAddress step resolved to a note; check if terminating LeftOperand is a standalone static note
                if (const GeneratorRule* leftRule = FindMatchingRule(srcLast.LeftOperand, def)) {
                    handleStaticAddressPair(srcLast.LeftOperand, dstLast.LeftOperand, leftRule, i);
                }
                continue;
            }

            int64_t srcBaseVal = 0;
            int64_t dstBaseVal = 0;
            if (!TryParseHexValue(srcUnit[baseIdx].LeftOperand.Value, srcBaseVal) ||
                !TryParseHexValue(dstUnit[baseIdx].LeftOperand.Value, dstBaseVal)) {
                continue;
            }

            QList<int64_t> srcOffsets;
            QList<int64_t> dstOffsets;
            QList<const GeneratorRule*> offsetRules;
            bool offsetsOk = true;
            for (int k = baseIdx + 1; k < srcUnit.size(); ++k) {
                int64_t sOff = 0;
                int64_t dOff = 0;
                if (!TryParseHexValue(srcUnit[k].LeftOperand.Value, sOff) || !TryParseHexValue(dstUnit[k].LeftOperand.Value, dOff)) {
                    offsetsOk = false;
                    break;
                }
                srcOffsets.append(sOff);
                dstOffsets.append(dOff);
                offsetRules.append(FindMatchingRule(srcUnit[k].LeftOperand, def));
            }
            if (!offsetsOk || srcOffsets.isEmpty())
                continue;

            QString srcBaseKey = LogicFormatter::NormalizeAddress(srcUnit[baseIdx].LeftOperand.Value);
            QString dstBaseKey = LogicFormatter::NormalizeAddress(dstUnit[baseIdx].LeftOperand.Value);
            const GeneratorRule* baseRule = FindMatchingRule(srcUnit[baseIdx].LeftOperand, def);

            // If the base pointer resolved via a range note rather than a standalone exact note, prepend the relative offset
            if (!workingNotesMap.contains(srcBaseKey) || !IsMeaningfulNoteText(workingNotesMap.value(srcBaseKey).Note)) {
                if (const auto* baseRange = findNarrowestRangeFor(srcBaseVal, true)) {
                    srcOffsets.prepend(srcBaseVal - baseRange->Start);
                    offsetRules.prepend(baseRule);
                    if (dstBaseVal >= baseRange->Start && dstBaseVal < baseRange->End) {
                        dstOffsets.prepend(dstBaseVal - baseRange->Start);
                        srcBaseVal = baseRange->Start;
                        dstBaseVal = baseRange->Start;
                        srcBaseKey = LogicFormatter::NormalizeAddress(QString("0x%1").arg(baseRange->Start, 0, 16));
                        dstBaseKey = srcBaseKey;
                    } else {
                        // Base range element stepped outside range; treat base as standalone if exact range note exists
                        srcBaseVal = baseRange->Start;
                        srcBaseKey = LogicFormatter::NormalizeAddress(QString("0x%1").arg(baseRange->Start, 0, 16));
                    }
                }
            }

            bool baseChanged = (dstBaseVal != srcBaseVal);
            int firstChangedOffsetIdx = -1;
            for (int k = 0; k < srcOffsets.size(); ++k) {
                if (dstOffsets[k] != srcOffsets[k]) {
                    firstChangedOffsetIdx = k;
                    break;
                }
            }

            // Subcase 1: Base pointer address changed
            if (baseChanged) {
                QString resolvedSrcBaseKey = resolveRedirectKey(srcBaseKey);
                if (workingNotesMap.contains(resolvedSrcBaseKey) && IsMeaningfulNoteText(workingNotesMap.value(resolvedSrcBaseKey).Note)) {
                    if (!batchTouchedStaticKeys.contains(dstBaseKey)) {
                        if (preExistingKeys.contains(dstBaseKey)) {
                            if (firstChangedOffsetIdx == -1)
                                result.SkippedExistingCount++;
                            batchTouchedStaticKeys.insert(dstBaseKey);
                        } else {
                            const CodeNote& srcBaseNote = workingNotesMap.value(resolvedSrcBaseKey);
                            auto tmpl = CodeNoteTemplateService::ParseTemplateFromNote(srcBaseNote.Note);
                            uint32_t deltaBytes = static_cast<uint32_t>(std::llabs(dstBaseVal - srcBaseVal));
                            QString newHeader = ExpandHeaderForRule(srcBaseNote.Note, baseRule, i, deltaBytes);

                            CodeNote newNote;
                            newNote.User = "Local";
                            newNote.Address = FormatAddressMatchingWidth(dstBaseVal, srcUnit[baseIdx].LeftOperand.Value);
                            newNote.Note = newHeader + tmpl.BodyText;

                            indexNote(newNote);
                            batchTouchedStaticKeys.insert(dstBaseKey);
                            result.CreatedOrModifiedCount++;
                        }
                    }
                }
            }

            // Subcase 2: Intermediate or leaf offset inside the pointer chain changed
            if (firstChangedOffsetIdx != -1) {
                QString resolvedSrcBaseKey = resolveRedirectKey(srcBaseKey);
                QString targetBaseKey = baseChanged ? resolveRedirectKey(dstBaseKey) : resolvedSrcBaseKey;

                if (!parsedNotesCache.contains(resolvedSrcBaseKey) || !workingNotesMap.contains(targetBaseKey))
                    continue;

                const auto& srcTree = parsedNotesCache.value(resolvedSrcBaseKey);
                const auto& dstTree = parsedNotesCache.value(targetBaseKey);

                // Walk source tree to collect the matched source nodes at each offset level
                std::shared_ptr<NoteNode> srcRoot = nullptr;
                QList<std::shared_ptr<NoteNode>> srcLevelNodes = GetTopLevelNodes(srcTree, &srcRoot);
                std::shared_ptr<NoteNode> srcParent = srcRoot;
                QList<std::shared_ptr<NoteNode>> matchedSrcNodes;

                for (int k = 0; k < srcOffsets.size(); ++k) {
                    bool isLast = (k == srcOffsets.size() - 1);
                    auto match = ConditionFormatter::FindBestMatchingNode(srcLevelNodes, srcOffsets[k], isLast, nullptr, srcParent);
                    if (match == nullptr)
                        break;
                    if (match->IsTemplateInherited) {
                        if (auto realSrc = match->TemplateSource.lock())
                            match = realSrc;
                    }
                    matchedSrcNodes.append(match);
                    srcLevelNodes = match->Children;
                    srcParent = match;
                }

                if (matchedSrcNodes.size() <= firstChangedOffsetIdx)
                    continue;

                // Smart check: if target path already exists in targetBaseKey, skip unless a deeper level is missing
                int injectIdx = firstChangedOffsetIdx;
                while (injectIdx < dstOffsets.size()) {
                    QList<int64_t> prefixPath = dstOffsets.mid(0, injectIdx + 1);
                    if (!ExactPointerChainExistsInTree(dstTree, prefixPath))
                        break;
                    injectIdx++;
                }

                if (injectIdx >= dstOffsets.size()) {
                    // Full target pointer chain already exists in the note
                    QString fullKey = OffsetPathKey(dstOffsets);
                    if (!queuedPathKeys[targetBaseKey].contains(fullKey)) {
                        result.SkippedExistingCount++;
                        queuedPathKeys[targetBaseKey].insert(fullKey);
                    }
                    continue;
                }

                if (injectIdx >= matchedSrcNodes.size())
                    continue;

                auto srcNode = matchedSrcNodes[injectIdx];
                QString srcDesc = srcNode->Description.trimmed();
                if (srcDesc.isEmpty())
                    continue;

                const GeneratorRule* matchedRule = (injectIdx < offsetRules.size()) ? offsetRules[injectIdx] : nullptr;
                uint32_t deltaBytes = static_cast<uint32_t>(std::llabs(dstOffsets[injectIdx] - srcOffsets[injectIdx]));
                QString newHeader = ExpandHeaderForRule(srcDesc, matchedRule, i, deltaBytes);

                QList<int64_t> remSrc = srcOffsets.mid(injectIdx + 1);
                QList<int64_t> remDst = dstOffsets.mid(injectIdx + 1);
                QString nodeBlockText =
                    SerializeNoteNodeSubtree(srcNode, dstOffsets[injectIdx], newHeader, sepStr, &remSrc, &remDst, i);

                QList<int64_t> dstPath = dstOffsets.mid(0, injectIdx + 1);
                if (queueSubtreeInjection(targetBaseKey, dstPath, nodeBlockText))
                    result.CreatedOrModifiedCount++;
            }
        }
    }

    // 4. Apply batched subtree injections once per modified base CodeNote
    if (!pendingInjections.isEmpty()) {
        QString indentStylePref = useUnicodeTree ? QString::fromUtf8("Unicode Tree (├─)") : QString("Auto Detect");
        for (auto it = pendingInjections.cbegin(); it != pendingInjections.cend(); ++it) {
            const QString& baseKey = it.key();
            if (!workingNotesMap.contains(baseKey))
                continue;

            CodeNote baseNote = workingNotesMap.value(baseKey);
            QString updatedText = CodeNoteModifier::InjectNoteSubtrees(baseNote.Note, it.value(), indentStylePref, indentDescriptions,
                                                                         consoleId);
            if (updatedText != baseNote.Note) {
                baseNote.Note = updatedText;
                workingNotesMap.insert(baseKey, baseNote);
            }
        }
    }

    if (result.CreatedOrModifiedCount > 0) {
        QList<CodeNote> finalList = workingNotesMap.values();
        std::sort(finalList.begin(), finalList.end(), [](const CodeNote& a, const CodeNote& b) {
            int64_t va = 0;
            int64_t vb = 0;
            bool oka = TryParseHexValue(a.Address, va);
            bool okb = TryParseHexValue(b.Address, vb);
            if (oka && okb)
                return va < vb;
            return a.Address < b.Address;
        });
        result.UpdatedNotes = finalList;
    }

    return result;
}

} // namespace RALE
