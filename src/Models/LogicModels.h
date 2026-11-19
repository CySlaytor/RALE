#pragma once

#include <QString>
#include <QList>
#include <cstdint>

namespace RALE {

class Operand
{
public:
    QString Type = "";
    QString Size = "";
    QString Value = "";

    Operand Clone() const
    {
        Operand op;
        op.Type = this->Type;
        op.Size = this->Size;
        op.Value = this->Value;
        return op;
    }

    bool operator==(const Operand &other) const
    {
        return Type == other.Type && Size == other.Size && Value == other.Value;
    }
    bool operator!=(const Operand &other) const
    {
        return !(*this == other);
    }
};

class AchievementCondition
{
public:
    int ID = 0;
    QString Flag = "";
    Operand LeftOperand;
    QString Operator = "";
    Operand RightOperand;
    uint32_t RequiredHits = 0;

    AchievementCondition Clone() const
    {
        AchievementCondition cond;
        cond.ID = this->ID;
        cond.Flag = this->Flag;
        cond.LeftOperand = this->LeftOperand.Clone();
        cond.Operator = this->Operator;
        cond.RightOperand = this->RightOperand.Clone();
        cond.RequiredHits = this->RequiredHits;
        return cond;
    }

    bool operator==(const AchievementCondition &other) const
    {
        return ID == other.ID &&
               Flag == other.Flag &&
               LeftOperand == other.LeftOperand &&
               Operator == other.Operator &&
               RightOperand == other.RightOperand &&
               RequiredHits == other.RequiredHits;
    }
    bool operator!=(const AchievementCondition &other) const
    {
        return !(*this == other);
    }
};

class AchievementConditionGroup
{
public:
    QString GroupName = "";
    QList<AchievementCondition> Conditions;

    bool operator==(const AchievementConditionGroup &other) const
    {
        return GroupName == other.GroupName && Conditions == other.Conditions;
    }
    bool operator!=(const AchievementConditionGroup &other) const
    {
        return !(*this == other);
    }
};

class CodeNote
{
public:
    QString User = "";
    QString Address = "";
    QString Note = "";

    bool operator==(const CodeNote &other) const
    {
        return Address == other.Address && Note == other.Note && User == other.User;
    }
    bool operator!=(const CodeNote &other) const
    {
        return !(*this == other);
    }
};

} // namespace RALE
