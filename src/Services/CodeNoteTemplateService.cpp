#include "CodeNoteTemplateService.h"

#include <QList>
#include <QRegularExpression>
#include <QStringList>

namespace RALE {

NoteTemplateInfo CodeNoteTemplateService::ParseTemplateFromNote(const QString& noteText) {
    NoteTemplateInfo info;
    info.DetectedStride = 64; // Fallback default stride
    info.BaseIndex = 1;
    info.FirstCustomName = "";
    info.HeaderTemplate = "Structure {INDEX:02}";

    QString trimmed = noteText.trimmed();
    if (trimmed.isEmpty())
        return info;

    QString normalized = trimmed;
    normalized.replace("\r\n", "\n").replace('\r', '\n');
    QStringList lines = normalized.split('\n');
    QString header = lines.first().trimmed();

    // Separate header line from structure body offsets
    if (lines.size() > 1) {
        QStringList bodyLines = lines.mid(1);
        info.BodyText = "\r\n" + bodyLines.join("\r\n");
    } else {
        info.BodyText = "";
    }

    // 1. Match ALL leading bracketed tags (e.g. [32-bit BE & 0x1FFFFFFF], [8-bit] [Bitfield], [144 bytes], [30x16 bytes])
    static const QRegularExpression leadingBracketRx(R"(^((?:\s*\[[^\]]+\]\s*)+))");
    auto bracketMatch = leadingBracketRx.match(header);
    if (bracketMatch.hasMatch()) {
        info.PrefixTag = bracketMatch.captured(1).trimmed();

        // Auto-detect stride if bracketed tag specifies byte size
        static const QRegularExpression bytesRx(R"(\[(?:(\d+)\s*x\s*(\d+)|(\d+))\s*bytes\])",
                                                QRegularExpression::CaseInsensitiveOption);
        auto bytesMatch = bytesRx.match(info.PrefixTag);
        if (bytesMatch.hasMatch()) {
            if (!bytesMatch.captured(1).isEmpty() && !bytesMatch.captured(2).isEmpty()) {
                info.DetectedStride = bytesMatch.captured(1).toUInt() * bytesMatch.captured(2).toUInt();
            } else if (!bytesMatch.captured(3).isEmpty()) {
                info.DetectedStride = bytesMatch.captured(3).toUInt();
            }
        }
    }

    // 2. Auto-detect numbering and parenthesized names strictly outside the prefix tags
    QString titleCandidate = header;
    if (!info.PrefixTag.isEmpty()) {
        titleCandidate = titleCandidate.mid(bracketMatch.capturedLength()).trimmed();
    }

    // Check parenthesized segments: distinguish numbered slot/index tags like "(Slot 1)" from custom names like "(Isolation)"
    static const QRegularExpression parenRx(R"(\(([^)]+)\))");
    static const QRegularExpression parenNumRx(R"(^((?:Level|Stage|Player|Char|Slot|Item|Part|Save|File|#)?\s*)(\d+)$)",
                                               QRegularExpression::CaseInsensitiveOption);

    QList<QRegularExpressionMatch> parenMatches;
    auto pIt = parenRx.globalMatch(titleCandidate);
    while (pIt.hasNext())
        parenMatches.append(pIt.next());

    bool foundParenIndex = false;
    for (int mIdx = static_cast<int>(parenMatches.size()) - 1; mIdx >= 0; --mIdx) {
        const auto& pm = parenMatches[mIdx];
        QString inner = pm.captured(1).trimmed();
        auto pNum = parenNumRx.match(inner);
        if (pNum.hasMatch() && !foundParenIndex) {
            QString digits = pNum.captured(2);
            bool ok = false;
            int parsedNum = digits.toInt(&ok);
            if (ok)
                info.BaseIndex = parsedNum;
            QString token = (digits.length() >= 2) ? "{INDEX:02}" : "{INDEX}";
            QString replaced = QString("(%1%2)").arg(pNum.captured(1), token);
            titleCandidate.replace(pm.capturedStart(), pm.capturedLength(), replaced);
            foundParenIndex = true;
        } else if (info.FirstCustomName.isEmpty()) {
            info.FirstCustomName = inner;
            titleCandidate.replace(pm.capturedStart(), pm.capturedLength(), "({NAME})");
        }
    }

    // Detect sequential index e.g. "Level 01", "Level 1", "#01" if not already tokenized inside parentheses
    if (!foundParenIndex) {
        static const QRegularExpression numRx(R"((\b(?:Level|Stage|Player|Char|Slot|Item|#)?\s*)(\d+))",
                                              QRegularExpression::CaseInsensitiveOption);
        auto numMatch = numRx.match(titleCandidate);
        if (numMatch.hasMatch()) {
            QString digits = numMatch.captured(2);
            bool ok = false;
            int parsedNum = digits.toInt(&ok);
            if (ok)
                info.BaseIndex = parsedNum;
            QString token = (digits.length() >= 2) ? "{INDEX:02}" : "{INDEX}";
            titleCandidate.replace(numMatch.capturedStart(2), digits.length(), token);
        } else if (!titleCandidate.contains("{INDEX")) {
            // If title has no number, append index token so each clone has a distinct sequential title
            if (!titleCandidate.isEmpty()) {
                titleCandidate += " {INDEX:02}";
            } else {
                titleCandidate = "{INDEX:02}";
            }
        }
    }

    // Re-attach prefix tag to the template
    if (!info.PrefixTag.isEmpty()) {
        info.HeaderTemplate = info.PrefixTag + " " + titleCandidate;
    } else {
        info.HeaderTemplate = titleCandidate;
    }

    return info;
}

QString CodeNoteTemplateService::ExpandTemplate(const QString& headerTemplate, int indexNumber, uint32_t offsetBytes,
                                                const QString& customName) {
    QString tmpl = headerTemplate.isEmpty() ? QString("Structure {INDEX:02}") : headerTemplate;

    // Replace index placeholders
    QString idxStr = QString::number(indexNumber);
    QString idx02Str = QString("%1").arg(indexNumber, 2, 10, QChar('0'));
    QString idx03Str = QString("%1").arg(indexNumber, 3, 10, QChar('0'));

    tmpl.replace("{INDEX:03}", idx03Str);
    tmpl.replace("{INDEX:02}", idx02Str);
    tmpl.replace("{INDEX}", idxStr);

    // Replace offset placeholder with clean lowercase 0x and uppercase hex digits
    QString offHex = QString("+0x%1").arg(QString("%1").arg(offsetBytes, 2, 16, QChar('0')).toUpper());
    tmpl.replace("{OFFSET}", offHex);

    // Replace name placeholder
    if (tmpl.contains("({NAME})")) {
        if (customName.trimmed().isEmpty()) {
            tmpl.replace("({NAME})", "");
        } else {
            tmpl.replace("({NAME})", "(" + customName.trimmed() + ")");
        }
    } else {
        tmpl.replace("{NAME}", customName.trimmed());
    }

    // Clean up redundant consecutive spaces
    return tmpl.simplified();
}

} // namespace RALE
