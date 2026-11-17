#include "UserTextParser.h"
#include <QFile>
#include <QTextStream>
#include <QRegularExpression>

namespace RALogicEditor {

bool UserTextParser::UserData::operator==(const UserData &other) const
{
    if (Notes != other.Notes) return false;
    if (Achievements.size() != other.Achievements.size()) return false;
    if (Leaderboards.size() != other.Leaderboards.size()) return false;

    for (int i = 0; i < Achievements.size(); ++i)
    {
        if (Achievements[i] == nullptr && other.Achievements[i] == nullptr) continue;
        if (Achievements[i] == nullptr || other.Achievements[i] == nullptr) return false;
        if (*Achievements[i] != *other.Achievements[i]) return false;
    }
    for (int i = 0; i < Leaderboards.size(); ++i)
    {
        if (Leaderboards[i] == nullptr && other.Leaderboards[i] == nullptr) continue;
        if (Leaderboards[i] == nullptr || other.Leaderboards[i] == nullptr) return false;
        if (*Leaderboards[i] != *other.Leaderboards[i]) return false;
    }
    return true;
}

UserTextParser::UserData UserTextParser::ParseUserFile(const QString &filepath)
{
    UserData data;

    // Use FileStream with ReadWrite sharing.
    // This prevents exceptions if the file is currently open in an external text editor.
    QFile file(filepath);
    if (!file.open(QIODevice::ReadOnly | QIODevice::Text))
        return data;

    QTextStream in(&file);
    QStringList lines;
    while (!in.atEnd())
        lines.append(in.readLine());
    file.close();

    static const QRegularExpression noteRegex(R"raw(^N0:\s*(0x[0-9A-Fa-f]+)\s*:\s*"(.+)"\s*$)raw", QRegularExpression::CaseInsensitiveOption);

    for (const QString &line : lines)
    {
        if (line.trimmed().isEmpty()) continue;
        QString trimmedLine = line.trimmed();

        try
        {
            // Use permissive regex for Code Notes to handle manual edits
            // Matches: N0: 0x1234 : "Note"
            if (trimmedLine.startsWith("N0:", Qt::CaseInsensitive))
            {
                auto match = noteRegex.match(trimmedLine);
                if (match.hasMatch())
                {
                    CodeNote note;
                    note.Address = match.captured(1);
                    // Unescape newlines AND quotes correctly
                    QString noteContent = match.captured(2);
                    noteContent.replace("\\r\\n", "\r\n").replace("\\\"", "\"");
                    note.Note = noteContent;
                    note.User = "Local";
                    data.Notes.append(note);
                }
            }
            else if (trimmedLine.startsWith('L', Qt::CaseInsensitive) && trimmedLine.length() > 1 && trimmedLine[1].isDigit())
            {
                // Leaderboards: L123:...
                int firstColon = trimmedLine.indexOf(':');
                if (firstColon == -1) continue;
                QString idStr = trimmedLine.mid(1, firstColon - 1);
                bool ok = false;
                int id = idStr.toInt(&ok);
                if (!ok) continue;

                QString content = trimmedLine.mid(firstColon + 1);
                QStringList parts = ParseColonLine(content);

                if (parts.size() >= 8)
                {
                    auto lb = std::make_shared<RaLeaderboard>();
                    lb->ID = id;
                    lb->Logic = QString("STA:%1::CAN:%2::SUB:%3::VAL:%4")
                                    .arg(parts[0], parts[1], parts[2], parts[3]);
                    lb->Format = parts[4];
                    lb->Title = parts[5];
                    lb->Description = parts[6];
                    lb->LowerIsBetter = (parts[7] == "1" || parts[7].compare("true", Qt::CaseInsensitive) == 0);
                    lb->State = AssetState::Local;
                    data.Leaderboards.append(lb);
                }
            }
            else if (!trimmedLine.isEmpty() && trimmedLine[0].isDigit())
            {
                // Achievements: 123:...
                int firstColon = trimmedLine.indexOf(':');
                if (firstColon == -1) continue;
                QString idStr = trimmedLine.left(firstColon);
                bool ok = false;
                int id = idStr.toInt(&ok);
                if (!ok) continue;

                QString content = trimmedLine.mid(firstColon + 1);
                QStringList parts = ParseColonLine(content);

                if (parts.size() >= 3)
                {
                    auto ach = std::make_shared<RaAchievement>();
                    ach->ID = id;
                    ach->Logic = parts[0];
                    ach->Title = parts[1];
                    ach->Description = parts[2];
                    ach->State = AssetState::Local;

                    if (parts.size() > 5) ach->Type = parts[5];
                    if (parts.size() > 6) ach->Author = parts[6];
                    if (parts.size() > 7)
                    {
                        bool ptsOk = false;
                        int pts = parts[7].toInt(&ptsOk);
                        if (ptsOk) ach->Points = pts;
                    }
                    if (parts.size() > 12)
                    {
                        ach->BadgeName = parts[12].trimmed();
                    }
                    data.Achievements.append(ach);
                }
            }
        }
        catch (...)
        {
            continue;
        }
    }

    return data;
}

QStringList UserTextParser::ParseColonLine(const QString &line)
{
    QStringList result;
    if (line.isEmpty()) return result;
    bool inQuote = false;
    int start = 0;
    for (int i = 0; i < line.length(); i++)
    {
        // Check if quote character is escaped by preceding backslashes
        bool isEscaped = false;
        int b = i - 1;
        while (b >= start && line[b] == '\\') { isEscaped = !isEscaped; b--; }

        if (line[i] == '"' && !isEscaped) inQuote = !inQuote;
        else if (line[i] == ':' && !inQuote)
        {
            result.append(CleanValue(line.mid(start, i - start)));
            start = i + 1;
        }
    }
    if (start <= line.length()) result.append(CleanValue(line.mid(start)));
    return result;
}

QString UserTextParser::CleanValue(const QString &val)
{
    if (val.isEmpty()) return "";
    QString trimmed = val.trimmed();
    if (trimmed.startsWith('"') && trimmed.endsWith('"') && trimmed.length() >= 2)
    {
        QString inner = trimmed.mid(1, trimmed.length() - 2);
        inner.replace("\\\\", "\\").replace("\\\"", "\"");
        return inner;
    }
    return trimmed;
}

} // namespace RALogicEditor
