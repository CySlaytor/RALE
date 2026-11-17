#include "LogicGeneratorService.h"
#include <QRegularExpression>
#include <algorithm>

namespace RALogicEditor {

void LogicGeneratorService::ApplyGeneratorRules(
    QList<AchievementConditionGroup> &activeGroups,
    const QMap<int, GeneratorDefinition> &generatorRules)
{
    QList<int> processedIndices = generatorRules.keys();
    std::sort(processedIndices.begin(), processedIndices.end());

    for (int masterIndex : processedIndices)
    {
        if (masterIndex >= activeGroups.size()) continue;

        const auto &def = generatorRules.value(masterIndex);

        if (def.IsArray)
        {
            // Array Mode: Append generated logic to the SAME group.
            // No groups are inserted in this branch, so the reference stays valid.
            // The 'Template' is the set of conditions that existed before generation.
            // The TriggerViewModel takes care of truncating the group to the TemplateConditionCount
            // before calling this service, so 'arrayGroup.Conditions' currently contains ONLY the template.
            auto &arrayGroup = activeGroups[masterIndex];

            // We need to capture the current template conditions to clone them
            QList<AchievementCondition> templateConditions;
            for (const auto &c : arrayGroup.Conditions)
                templateConditions.append(c.Clone());

            // Generate N copies
            for (int i = 1; i <= def.Copies; i++)
            {
                for (const auto &cond : templateConditions)
                {
                    AchievementCondition newCond = cond.Clone();
                    for (const auto &rule : def.Rules)
                    {
                        ApplyOffsetToOperand(newCond.LeftOperand, rule.SourceAddress, rule.Stride, i);
                        ApplyOffsetToOperand(newCond.RightOperand, rule.SourceAddress, rule.Stride, i);
                    }
                    arrayGroup.Conditions.append(newCond);
                }
            }
        }
        else
        {
            // Copy the master group: inserting generated groups below can reallocate
            // the QList's storage, which would leave a reference dangling (unlike C# class refs).
            AchievementConditionGroup masterGroup = activeGroups.at(masterIndex);

            // Normal Mode: Generate NEW groups (Alts)
            while (activeGroups.size() <= masterIndex + def.Copies)
            {
                AchievementConditionGroup tempGroup;
                tempGroup.GroupName = "Temp";
                activeGroups.insert(masterIndex + 1, tempGroup);
            }

            for (int i = 1; i <= def.Copies; i++)
            {
                int targetGroupIndex = masterIndex + i;
                auto &targetGroup = activeGroups[targetGroupIndex];

                QString baseName = masterGroup.GroupName;
                static const QRegularExpression hashRegex(R"( #\d+)");
                if (baseName.contains(" #")) baseName.remove(hashRegex);
                targetGroup.GroupName = QString("%1 (+%2)").arg(baseName).arg(i);

                targetGroup.Conditions.clear();
                for (const auto &cond : masterGroup.Conditions)
                {
                    AchievementCondition newCond = cond.Clone();
                    for (const auto &rule : def.Rules)
                    {
                        ApplyOffsetToOperand(newCond.LeftOperand, rule.SourceAddress, rule.Stride, i);
                        ApplyOffsetToOperand(newCond.RightOperand, rule.SourceAddress, rule.Stride, i);
                    }
                    targetGroup.Conditions.append(newCond);
                }
            }
        }
    }
}

void LogicGeneratorService::ApplyOffsetToOperand(Operand &op, const QString &targetAddress, int stride, int multiplier)
{
    if (op.Value.isEmpty()) return;
    if (op.Type.compare("Recall", Qt::CaseInsensitive) == 0) return;

    QString cleanTarget = targetAddress.startsWith("0x", Qt::CaseInsensitive) ? targetAddress.mid(2) : targetAddress;
    QString cleanOp = op.Value.startsWith("0x", Qt::CaseInsensitive) ? op.Value.mid(2) : op.Value;

    if (cleanOp.compare(cleanTarget, Qt::CaseInsensitive) == 0)
    {
        bool ok = false;
        int64_t baseVal = cleanOp.toLongLong(&ok, 16);
        if (ok)
        {
            int64_t offset = static_cast<int64_t>(stride) * multiplier;
            int64_t newVal = baseVal + offset;
            int padding = cleanOp.length();
            // .NET "xN" formatting: negative values use two's-complement hex.
            QString hex = QString::number(static_cast<quint64>(newVal), 16);
            if (hex.length() < padding) hex = hex.rightJustified(padding, '0');
            op.Value = "0x" + hex;
        }
    }
}

} // namespace RALogicEditor
