#include "CodeNoteMultiplier.h"
#include <QRegularExpression>
#include <QStringList>
#include <algorithm>

namespace RALE {

QString CodeNoteMultiplier::GenerateText(
    const QString &templateText,
    int copies,
    int64_t stride,
    NamingStyle naming,
    const QString &search,
    const QString &replace,
    int startNumber,
    StrideTarget target)
{
    QString sb;
    QString normalized = templateText;
    normalized.replace("\r\n", "\n").replace('\r', '\n');
    QStringList lines = normalized.split('\n');

    static const QRegularExpression offsetRegex(
        QString::fromUtf8(R"(^([.\+\s\x{2500}-\x{257F}]*)([-+]?)(0x[0-9A-Fa-f]+)(.*)$)"),
        QRegularExpression::CaseInsensitiveOption);

    // Determine minimum indentation to differentiate Parents from Children
    int minIndent = std::numeric_limits<int>::max();
    for (const QString &line : lines)
    {
        auto m = offsetRegex.match(line);
        if (m.hasMatch())
        {
            int ind = m.captured(1).length();
            if (ind < minIndent) minIndent = ind;
        }
    }
    if (minIndent == std::numeric_limits<int>::max()) minIndent = 0;

    for (int i = 0; i < copies; i++)
    {
        int currentNumber = startNumber + i;
        int64_t currentStride = stride * (i + 1);

        for (int j = 0; j < lines.size(); j++)
        {
            QString line = lines[j];
            if (line.trimmed().isEmpty())
            {
                sb += "\r\n";
                continue;
            }

            auto match = offsetRegex.match(line);
            if (match.hasMatch())
            {
                QString indent = match.captured(1);
                QString sign = match.captured(2);
                QString hexStr = match.captured(3).mid(2);
                QString remainder = match.captured(4);

                // If the line is indented further than the root selection, it's a child.
                bool isChild = indent.length() > minIndent;
                int64_t appliedStride = currentStride;

                if (target == StrideTarget::ParentsOnly && isChild) appliedStride = 0;
                if (target == StrideTarget::ChildrenOnly && !isChild) appliedStride = 0;

                bool ok = false;
                int64_t baseVal = hexStr.toLongLong(&ok, 16);
                if (ok)
                {
                    int64_t actualVal = (sign == "-") ? -baseVal : baseVal;
                    int64_t newVal = actualVal + appliedStride;

                    QString newSign = newVal < 0 ? "-" : sign;
                    if (sign.isEmpty() && newVal < 0) newSign = "-";

                    QString newHex = QString("0x%1").arg(std::abs(newVal), hexStr.length(), 16, QChar('0'));
                    QString newRemainder = ApplyNaming(remainder, naming, search, replace, currentNumber);

                    sb += QString("%1%2%3%4\r\n").arg(indent, newSign, newHex, newRemainder);
                }
                else
                {
                    sb += ApplyNaming(line, naming, search, replace, currentNumber) + "\r\n";
                }
            }
            else
            {
                sb += ApplyNaming(line, naming, search, replace, currentNumber) + "\r\n";
            }
        }

        if (i < copies - 1) sb += "\r\n";
    }

    while (!sb.isEmpty() && sb.back().isSpace())
        sb.chop(1);

    return sb;
}

QString CodeNoteMultiplier::ApplyNamingToNote(
    const QString &noteText,
    NamingStyle style,
    const QString &search,
    const QString &replace,
    int number)
{
    QString normalized = noteText;
    normalized.replace("\r\n", "\n").replace('\r', '\n');
    QStringList lines = normalized.split('\n');

    if (!lines.isEmpty())
    {
        // Always apply to the first line (Title/Header of the note)
        lines[0] = ApplyNaming(lines[0], style, search, replace, number);

        // For replacements, apply to the entire body too
        if (style != NamingStyle::AppendSlot)
        {
            for (int i = 1; i < lines.size(); i++)
            {
                lines[i] = ApplyNaming(lines[i], style, search, replace, number);
            }
        }
    }
    return lines.join("\r\n");
}

QString CodeNoteMultiplier::ApplyNaming(
    const QString &input,
    NamingStyle style,
    const QString &search,
    const QString &replace,
    int number)
{
    switch (style)
    {
        case NamingStyle::AppendSlot:
            return input + QString(" (Slot %1)").arg(number);
        case NamingStyle::ReplaceSlot1:
        {
            static const QRegularExpression regex("Slot 1", QRegularExpression::CaseInsensitiveOption);
            QString copy = input;
            copy.replace(regex, QString("Slot %1").arg(number));
            return copy;
        }
        case NamingStyle::ReplacePlayer1:
        {
            static const QRegularExpression regex("Player 1", QRegularExpression::CaseInsensitiveOption);
            QString copy = input;
            copy.replace(regex, QString("Player %1").arg(number));
            return copy;
        }
        case NamingStyle::Custom:
            if (search.isEmpty()) return input;
            QString actualReplace = replace;
            actualReplace.replace("{N}", QString::number(number));
            QRegularExpression regex(QRegularExpression::escape(search), QRegularExpression::CaseInsensitiveOption);
            QString copy = input;
            copy.replace(regex, actualReplace);
            return copy;
    }
    return input;
}

} // namespace RALE
