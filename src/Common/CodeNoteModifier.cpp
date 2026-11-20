#include "CodeNoteModifier.h"

#include "../Logic/CodeNoteLogic.h"

#include <QRegularExpression>

namespace RALE {

QString CodeNoteModifier::NoteStyle::GetIndentString(int depth) const {
    if (depth <= 1)
        return "";

    if (Indentation == IndentStyle::Unicode) {
        if (depth == 2)
            return QString::fromUtf8("├─");
        QString sb;
        for (int i = 0; i < depth - 2; i++)
            sb += QString::fromUtf8("│ ");
        sb += QString::fromUtf8("├─");
        return sb;
    }

    if (Indentation == IndentStyle::Plus)
        return QString(depth - 1, '+');

    return QString(depth - 1, '.');
}

QString CodeNoteModifier::NoteStyle::GetBodyIndentString(int depth) const {
    if (depth <= 1)
        return "";

    if (Indentation == IndentStyle::Unicode)
        return QString((depth - 1) * 2, ' ');

    return QString(depth - 1, '.');
}

QString CodeNoteModifier::NoteStyle::GetSeparatorString() const {
    switch (Separator) {
    case SeparatorStyle::Pipe:
        return " | ";
    case SeparatorStyle::Equals:
        return " = ";
    case SeparatorStyle::Colon:
        return " : ";
    default:
        return " ";
    }
}

bool CodeNoteModifier::IsNodeBoundary(const QString& line) {
    // Updated to support standard (+0x, -0x) and array (|0x, |+0x, |-0x) boundaries
    static const QRegularExpression regex(
        QString::fromUtf8(R"(^([.\+\s\x{2500}-\x{257F}]*)(\|[\+\-]?|[\+\-]?)(0x[0-9A-Fa-f]+)\s*([|=:])?\s*(.*)$)"),
        QRegularExpression::CaseInsensitiveOption);

    auto match = regex.match(line);
    if (!match.hasMatch())
        return false;

    QString indentStr = match.captured(1);
    QString sign = match.captured(2);
    QString separator = match.captured(4);

    if (!sign.isEmpty())
        return true;
    if (indentStr.contains('+'))
        return true;
    if (indentStr.length() > 0 && separator == "|")
        return true;

    return false;
}

QString CodeNoteModifier::ExtractExactPrefix(const QString& line) {
    static const QRegularExpression regex(QString::fromUtf8(R"(^([.\+\s\x{2500}-\x{257F}]*)(\|[\+\-]?|[\+\-]?)(0x[0-9A-Fa-f]+))"),
                                          QRegularExpression::CaseInsensitiveOption);

    auto match = regex.match(line);
    if (match.hasMatch()) {
        QString indentStr = match.captured(1);
        QString sign = match.captured(2);

        // Safely recover and separate mathematical and array prefix characters
        if (sign.isEmpty()) {
            if (indentStr.endsWith('+') || indentStr.endsWith('-') || indentStr.endsWith('|')) {
                indentStr.chop(1);
            }
        }
        return indentStr;
    }
    return "";
}

CodeNoteModifier::NoteStyle CodeNoteModifier::DetectStyle(const QString& currentNote, const QList<CodeNote>& allNotes) {
    auto localStats = ScanStats({currentNote});
    if (localStats.HasIndent() || localStats.HasSeparator()) {
        auto style = ResolveStyle(localStats);
        if (!localStats.HasSeparator()) {
            auto globalSep = DetectGlobalStyle(allNotes).Separator;
            if (globalSep != SeparatorStyle::Space)
                style.Separator = globalSep;
        }
        return style;
    }

    QStringList allTexts;
    for (const auto& n : allNotes)
        allTexts.append(n.Note);
    auto globalStats = ScanStats(allTexts);
    return ResolveStyle(globalStats);
}

CodeNoteModifier::NoteStyle CodeNoteModifier::DetectGlobalStyle(const QList<CodeNote>& allNotes) {
    QStringList allTexts;
    for (const auto& n : allNotes)
        allTexts.append(n.Note);
    auto stats = ScanStats(allTexts);
    return ResolveStyle(stats);
}

CodeNoteModifier::StyleStats CodeNoteModifier::ScanStats(const QStringList& notes) {
    StyleStats stats;
    static const QRegularExpression indentRegex(QString::fromUtf8(R"(^([.\+\s\x{2500}-\x{257F}]+)0x)"),
                                                QRegularExpression::MultilineOption);
    static const QRegularExpression sepRegex(R"(0x[0-9A-Fa-f]+\s*([|=:])\s*)", QRegularExpression::MultilineOption);

    for (const QString& noteText : notes) {
        if (noteText.isEmpty())
            continue;

        auto it = indentRegex.globalMatch(noteText);
        while (it.hasNext()) {
            auto m = it.next();
            QString indent = m.captured(1);
            if (indent.contains(QChar(0x2502)) || indent.contains(QChar(0x251C)) || indent.contains(QChar(0x2514)) ||
                indent.contains(QChar(0x252C)) || indent.contains(QChar(0x2500))) {
                stats.UnicodeCount++;
            } else if (indent.contains('.')) {
                if (indent.contains('+'))
                    stats.MixedCount++;
                else
                    stats.DotCount++;
            } else if (indent.contains('+')) {
                stats.PlusCount++;
            }
        }

        auto sepIt = sepRegex.globalMatch(noteText);
        while (sepIt.hasNext()) {
            auto m = sepIt.next();
            QString s = m.captured(1);
            if (s == "|")
                stats.PipeCount++;
            else if (s == "=")
                stats.EqCount++;
            else if (s == ":")
                stats.ColCount++;
        }
    }
    return stats;
}

CodeNoteModifier::NoteStyle CodeNoteModifier::ResolveStyle(const StyleStats& stats) {
    NoteStyle style;

    if (stats.UnicodeCount > stats.DotCount && stats.UnicodeCount > stats.PlusCount && stats.UnicodeCount > stats.MixedCount)
        style.Indentation = IndentStyle::Unicode;
    else if (stats.MixedCount > stats.DotCount && stats.MixedCount > stats.PlusCount)
        style.Indentation = IndentStyle::Mixed;
    else if (stats.DotCount > stats.PlusCount && stats.DotCount > stats.MixedCount)
        style.Indentation = IndentStyle::Dot;
    else
        style.Indentation = IndentStyle::Plus;

    if (stats.PipeCount >= stats.EqCount && stats.PipeCount >= stats.ColCount && stats.PipeCount > 0)
        style.Separator = SeparatorStyle::Pipe;
    else if (stats.EqCount >= stats.PipeCount && stats.EqCount >= stats.ColCount && stats.EqCount > 0)
        style.Separator = SeparatorStyle::Equals;
    else if (stats.ColCount >= stats.PipeCount && stats.ColCount >= stats.EqCount && stats.ColCount > 0)
        style.Separator = SeparatorStyle::Colon;
    else
        style.Separator = SeparatorStyle::Pipe; // Changed fallback from Space to Pipe

    if (style.Separator == SeparatorStyle::Space && !stats.HasSeparator())
        style.Separator = SeparatorStyle::Pipe;

    return style;
}

CodeNoteModifier::NoteStyle CodeNoteModifier::DetectLocalStyle(const QString& parentLine, const NoteStyle& fallbackStyle) {
    NoteStyle localStyle;
    localStyle.Separator = fallbackStyle.Separator;
    localStyle.Indentation = fallbackStyle.Indentation;

    if (parentLine.contains('|'))
        localStyle.Separator = SeparatorStyle::Pipe;
    else if (parentLine.contains('='))
        localStyle.Separator = SeparatorStyle::Equals;
    else if (parentLine.contains(':'))
        localStyle.Separator = SeparatorStyle::Colon;

    static const QRegularExpression matchRegex(QString::fromUtf8(R"(^([.\+\s\x{2500}-\x{257F}]+))"));
    auto match = matchRegex.match(parentLine.trimmed());
    if (match.hasMatch()) {
        QString indent = match.captured(1);
        if (indent.contains(QChar(0x2502)) || indent.contains(QChar(0x251C)) || indent.contains(QChar(0x2514)) ||
            indent.contains(QChar(0x252C)) || indent.contains(QChar(0x2500))) {
            localStyle.Indentation = IndentStyle::Unicode;
        } else if (indent.contains('.')) {
            localStyle.Indentation = indent.contains('+') ? IndentStyle::Mixed : IndentStyle::Dot;
        } else if (indent.contains('+')) {
            if (indent == "+" && fallbackStyle.Indentation == IndentStyle::Mixed) {
                localStyle.Indentation = IndentStyle::Mixed;
            } else if (indent.length() > 1 && !indent.contains('.')) {
                localStyle.Indentation = IndentStyle::Plus;
            } else {
                localStyle.Indentation = IndentStyle::Plus;
            }
        }
    }

    return localStyle;
}

QString CodeNoteModifier::DeleteNodeFamily(const QString& baseNoteText, const std::shared_ptr<NoteNode>& node) {
    QString normalized = baseNoteText;
    normalized.replace("\r\n", "\n").replace('\r', '\n');
    QStringList lines = normalized.split('\n');

    if (node->RawLineIndex >= lines.size() || node->RawLineIndex < 0)
        return baseNoteText;

    QString targetPrefix = ExtractExactPrefix(lines[node->RawLineIndex]);

    int removeCount = 1;
    int idx = node->RawLineIndex + 1;

    while (idx < lines.size()) {
        QString line = lines[idx];
        if (line.trimmed().isEmpty()) {
            removeCount++;
            idx++;
            continue;
        }

        if (IsNodeBoundary(line)) {
            QString currentPrefix = ExtractExactPrefix(line);
            if (currentPrefix.length() <= targetPrefix.length()) {
                break;
            }
        }

        removeCount++;
        idx++;
    }

    for (int k = 0; k < removeCount; k++)
        lines.removeAt(node->RawLineIndex);

    for (int i = lines.size() - 1; i > 0; i--) {
        if (lines[i].trimmed().isEmpty() && lines[i - 1].trimmed().isEmpty())
            lines.removeAt(i);
    }

    QString result = lines.join("\r\n");
    while (!result.isEmpty() && result.back().isSpace())
        result.chop(1);
    return result;
}

QString CodeNoteModifier::UpdateRealNode(const QString& baseNoteText, const std::shared_ptr<NoteNode>& node, const QString& newContent,
                                         const NoteStyle& globalStyle, const QString& /*indentStylePref*/, bool /*indentDescriptions*/) {
    QString normalized = baseNoteText;
    normalized.replace("\r\n", "\n").replace('\r', '\n');
    QStringList lines = normalized.split('\n');

    if (node->RawLineIndex >= lines.size())
        return baseNoteText;

    auto style = DetectLocalStyle(lines[node->RawLineIndex], globalStyle);
    QString originalPrefix = ExtractExactPrefix(lines[node->RawLineIndex]);

    int removeCount = 1;
    int idx = node->RawLineIndex + 1;

    while (idx < lines.size()) {
        QString line = lines[idx];

        if (line.trimmed().isEmpty()) {
            idx++;
            removeCount++;
            continue;
        }

        if (IsNodeBoundary(line))
            break;
        if (line.trimmed().startsWith("+0x", Qt::CaseInsensitive) || line.trimmed().startsWith("-0x", Qt::CaseInsensitive) ||
            line.trimmed().startsWith("|0x", Qt::CaseInsensitive) || line.trimmed().startsWith("|+0x", Qt::CaseInsensitive))
            break;

        idx++;
        removeCount++;
    }

    for (int k = 0; k < removeCount; k++)
        lines.removeAt(node->RawLineIndex);

    QString offsetHex = node->Offset;
    bool isArrayElem = offsetHex.startsWith('|');
    while (offsetHex.startsWith('|') || offsetHex.startsWith('+') || offsetHex.startsWith('-'))
        offsetHex = offsetHex.mid(1);
    if (!offsetHex.startsWith("0x", Qt::CaseInsensitive))
        offsetHex = "0x" + offsetHex;
    QString sign = isArrayElem ? "|" : (node->Offset.startsWith('-') ? "-" : "+");

    QString sep = style.GetSeparatorString();

    QString normContent = newContent;
    normContent.replace("\r\n", "\n").replace('\r', '\n');
    QStringList contentParts = normContent.split('\n', Qt::SkipEmptyParts);
    QString title = !contentParts.isEmpty() ? contentParts[0] : "";

    QString header = QString("%1%2%3%4%5").arg(originalPrefix, sign, offsetHex, sep, title);
    lines.insert(node->RawLineIndex, header);

    QString bodyPadding = QString(originalPrefix.length() + 1, ' ');

    for (int k = 1; k < contentParts.size(); k++) {
        QString bodyLine = contentParts[k];
        int p = 0;
        while (p < bodyLine.size() && bodyLine[p].isSpace())
            ++p;
        bodyLine = bodyLine.mid(p);
        lines.insert(node->RawLineIndex + k, bodyPadding + bodyLine);
    }

    return lines.join("\r\n");
}

QString CodeNoteModifier::InjectNoteContent(const QString& baseNoteText, const QList<int64_t>& chainOffsets, const QString& newContent,
                                            const NoteStyle& globalStyle, const QString& indentStylePref, bool indentDescriptions) {
    QString sb = baseNoteText;

    // Add spacing to ensure it breaks cleanly from the previous content
    if (!sb.isEmpty() && !sb.endsWith('\n'))
        sb += "\n";
    sb += "\n";

    for (int i = 0; i < chainOffsets.size(); i++) {
        int64_t offset = chainOffsets[i];
        QString sign = offset < 0 ? "-" : "+";
        QString hex = QString("0x%1").arg(std::abs(offset), 0, 16);

        // Use pure '+' indent to safely pass into the parser, which calculates depth accurately.
        // The CodeNoteLogic.FixHierarchy method will then convert this back to your chosen UI style (e.g. Unicode).
        QString indent = QString(i, '+');

        if (i == chainOffsets.size() - 1) {
            // Final node in chain
            QString normContent = newContent;
            normContent.replace("\r\n", "\n").replace('\r', '\n');
            QStringList contentParts = normContent.split('\n', Qt::SkipEmptyParts);
            QString title = !contentParts.isEmpty() ? contentParts[0] : "New Node";
            QString sep = globalStyle.GetSeparatorString();

            sb += QString("%1%2%3%4%5\n").arg(indent, sign, hex, sep, title);

            for (int k = 1; k < contentParts.size(); k++) {
                QString bodyLine = contentParts[k];
                int p = 0;
                while (p < bodyLine.size() && bodyLine[p].isSpace())
                    ++p;
                sb += bodyLine.mid(p) + "\r\n";
            }
        } else {
            // Intermediate path nodes
            sb += QString("%1%2%3\n").arg(indent, sign, hex);
        }
    }

    // Convert NoteStyle back to the string expected by CodeNoteLogic.FixHierarchy
    // Merge and Format!
    return CodeNoteLogic::FixHierarchy(sb, indentStylePref, indentDescriptions);
}

} // namespace RALE
