#include "AchievementParser.h"
#include "../Definitions/RaLogicSyntax.h"
#include <QRegularExpression>
#include <algorithm>

namespace RALogicEditor {

QList<AchievementConditionGroup> AchievementParser::ParseAchievementTrigger(
    const QString &trigger,
    QChar separator,
    bool isLeaderboard)
{
    QList<AchievementConditionGroup> conditionSet;
    if (trigger.trimmed().isEmpty()) return conditionSet;

    QStringList groupStrings;

    if (isLeaderboard)
    {
        groupStrings = trigger.split("::");
        const QString groupNames[4] = { "START", "CANCEL", "SUBMIT", "VALUE" };
        int count = static_cast<int>(std::min<qsizetype>(groupStrings.size(), 4));

        for (int i = 0; i < count; i++)
        {
            QString logicPart = groupStrings[i];
            if (logicPart.startsWith("STA:")) logicPart = logicPart.mid(4);
            else if (logicPart.startsWith("CAN:")) logicPart = logicPart.mid(4);
            else if (logicPart.startsWith("SUB:")) logicPart = logicPart.mid(4);
            else if (logicPart.startsWith("VAL:")) logicPart = logicPart.mid(4);

            AchievementConditionGroup group;
            group.GroupName = groupNames[i];
            if (!logicPart.trimmed().isEmpty()) ParseConditionsIntoGroup(group, logicPart);
            conditionSet.append(group);
        }
        for (int i = conditionSet.size(); i < 4; i++)
        {
            AchievementConditionGroup group;
            group.GroupName = groupNames[i];
            conditionSet.append(group);
        }
        return conditionSet;
    }

    if (separator == 'S')
    {
        static const QRegularExpression sRegex(R"((?<!0x)S)", QRegularExpression::CaseInsensitiveOption);
        groupStrings = trigger.split(sRegex);
    }
    else
    {
        groupStrings = trigger.split(separator);
    }

    for (int i = 0; i < groupStrings.size(); i++)
    {
        AchievementConditionGroup group;
        group.GroupName = (separator == 'S')
            ? ((i == 0) ? "Core" : QString("Alt %1").arg(i))
            : QString("Value Group %1").arg(i + 1);

        ParseConditionsIntoGroup(group, groupStrings[i]);
        conditionSet.append(group);
    }
    return conditionSet;
}

void AchievementParser::ParseConditionsIntoGroup(AchievementConditionGroup &group, const QString &logic)
{
    if (logic.trimmed().isEmpty()) return;
    QStringList conditionStrings = logic.split('_');
    int conditionId = 1;
    for (const QString &conditionString : conditionStrings)
    {
        if (!conditionString.trimmed().isEmpty())
        {
            try
            {
                AchievementCondition condition = ParseCondition(conditionString);
                condition.ID = conditionId++;
                group.Conditions.append(condition);
            }
            catch (...) {}
        }
    }
}

AchievementCondition AchievementParser::ParseCondition(const QString &conditionStr)
{
    AchievementCondition condition;
    QString current = conditionStr;

    static const QRegularExpression hitsRegex(R"(\.(\d+)\.$)");
    auto match = hitsRegex.match(current);
    if (match.hasMatch())
    {
        bool ok = false;
        uint32_t hits = match.captured(1).toUInt(&ok);
        if (ok) condition.RequiredHits = hits;
        current = current.left(match.capturedStart());
    }

    if (current.length() > 2 && current[1] == ':')
    {
        QChar flagChar = current[0].toUpper();
        for (auto it = RaLogicSyntax::ReverseFlagMap.cbegin(); it != RaLogicSyntax::ReverseFlagMap.cend(); ++it)
        {
            if (it.value() == flagChar)
            {
                condition.Flag = it.key();
                current = current.mid(2);
                break;
            }
        }
    }

    QString opStr = "";
    int opIndex = -1;

    // Sort operators by Length Descending to match strict operators (>=) before substrings (=)
    QStringList sortedOps;
    for (const QString &op : RaLogicSyntax::ComparisonOperators)
    {
        if (op != RaLogicSyntax::NO_OPERATOR_TEXT)
            sortedOps.append(op);
    }
    std::sort(sortedOps.begin(), sortedOps.end(), [](const QString &a, const QString &b) {
        return a.length() > b.length();
    });

    for (const QString &op : sortedOps)
    {
        int idx = current.indexOf(op);
        if (idx != -1)
        {
            opStr = op;
            opIndex = idx;
            break;
        }
    }

    if (opIndex == -1)
    {
        // Apply same sort for Arithmetic operators safety
        QStringList sortedArith;
        for (const QString &op : RaLogicSyntax::ArithmeticOperators)
        {
            if (op != RaLogicSyntax::NO_OPERATOR_TEXT)
                sortedArith.append(op);
        }
        std::sort(sortedArith.begin(), sortedArith.end(), [](const QString &a, const QString &b) {
            return a.length() > b.length();
        });

        for (const QString &op : sortedArith)
        {
            int idx = current.indexOf(op);
            if (idx != -1)
            {
                opStr = op;
                opIndex = idx;
                break;
            }
        }
    }

    if (opIndex == -1)
    {
        condition.LeftOperand = ParseOperand(current);
    }
    else
    {
        condition.Operator = opStr;
        condition.LeftOperand = ParseOperand(current.left(opIndex));
        condition.RightOperand = ParseOperand(current.mid(opIndex + opStr.length()));
    }

    return condition;
}

Operand AchievementParser::ParseOperand(const QString &operandStr)
{
    Operand operand;
    QString current = operandStr;
    if (current.isEmpty()) return operand;
    if (current.compare("{recall}", Qt::CaseInsensitive) == 0)
    {
        operand.Type = "Recall";
        return operand;
    }

    if (!current.isEmpty())
    {
        QChar firstLower = current[0].toLower();
        if (firstLower == 'd' || firstLower == 'p' || firstLower == 'b' || firstLower == '~')
        {
            switch (firstLower.toLatin1())
            {
                case 'd': operand.Type = "Delta"; break;
                case 'p': operand.Type = "Prior"; break;
                case 'b': operand.Type = "BCD"; break;
                case '~': operand.Type = "Inverted"; break;
            }
            current = (current.length() > 1) ? current.mid(1).trimmed() : "";
        }
    }

    if (current.isEmpty()) return operand;

    QChar firstChar = current[0];
    QString remaining = (current.length() > 1) ? current.mid(1) : "";

    if (firstChar.toLower() == 'f' && current.length() > 1 &&
        RaLogicSyntax::FloatSizeMap.contains(remaining[0].toUpper()))
    {
        if (operand.Type.isEmpty()) operand.Type = "Mem";
        operand.Size = RaLogicSyntax::FloatSizeMap.value(remaining[0].toUpper());
        operand.Value = "0x" + ((remaining.length() > 1) ? remaining.mid(1).trimmed() : "");
    }
    else if (firstChar.toLower() == 'f')
    {
        operand.Type = "Float";
        operand.Value = remaining.trimmed();
    }
    else if (firstChar.isLetter() &&
             RaLogicSyntax::ReverseSizeMap.values().contains(firstChar.toUpper()) &&
             remaining.trimmed().startsWith("0x", Qt::CaseInsensitive))
    {
        if (operand.Type.isEmpty()) operand.Type = "Mem";
        for (auto it = RaLogicSyntax::ReverseSizeMap.cbegin(); it != RaLogicSyntax::ReverseSizeMap.cend(); ++it)
        {
            if (it.value() == firstChar.toUpper())
            {
                operand.Size = it.key();
                break;
            }
        }
        operand.Value = remaining.trimmed();
    }
    else if (current.trimmed().startsWith("0x", Qt::CaseInsensitive))
    {
        if (operand.Type.isEmpty()) operand.Type = "Mem";
        QString addressPart = current.trimmed().mid(2).trimmed();
        QChar sizeChar = ' ';
        if (!addressPart.isEmpty() && addressPart[0].isLetter() &&
            RaLogicSyntax::ReverseSizeMap.values().contains(addressPart[0].toUpper()))
        {
            sizeChar = addressPart[0];
            addressPart = addressPart.mid(1);
        }

        bool foundExplicit = false;
        for (auto it = RaLogicSyntax::ReverseSizeMap.cbegin(); it != RaLogicSyntax::ReverseSizeMap.cend(); ++it)
        {
            if (it.value() == sizeChar.toUpper())
            {
                if (it.key().isEmpty() && sizeChar == ' ') continue;
                operand.Size = it.key();
                foundExplicit = true;
                break;
            }
        }
        if (!foundExplicit && sizeChar == ' ') operand.Size = "16-bit";

        operand.Value = "0x" + addressPart;
    }
    else
    {
        bool ok = false;
        int64_t val = current.toLongLong(&ok, 10);
        if (ok)
        {
            if (operand.Type.isEmpty()) operand.Type = "Value";
            // Format unsigned 64-bit hex matching C# 0x{val:X}: lower-case prefix,
            // upper-case digits (QString::toUpper() would also uppercase the "0x").
            operand.Value = "0x" + QString::number(static_cast<uint64_t>(val), 16).toUpper();
        }
    }

    return operand;
}

} // namespace RALogicEditor
