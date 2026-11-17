#include "RichPresenceService.h"
#include "../Utilities/LogicFormatter.h"
#include <QRegularExpression>

namespace RALogicEditor {

RichPresenceScript RichPresenceService::GenerateMultiRegion(
    const RichPresenceScript &sourceScript,
    const QList<RegionConfig> &configs)
{
    RichPresenceScript newScript;

    // 1. Copy Lookups (Shared)
    for (const auto &lookup : sourceScript.Lookups)
    {
        newScript.Lookups.append(lookup);
    }

    // 2. Process Display Strings
    for (int i = 0; i < sourceScript.DisplayStrings.size(); i++)
    {
        const auto &ds = sourceScript.DisplayStrings[i];
        bool isLastLine = (i == sourceScript.DisplayStrings.size() - 1);

        // Optimization: If the last line has no condition (default) and contains no lookup addresses (plain text),
        // do NOT duplicate it with locks. Just add it once at the end.
        // This prevents "Playing Project: Snowblind" from being turned into "?Lock?Playing..."
        bool isPlainTextDefault = isLastLine &&
                                  ds.Condition.isEmpty() &&
                                  !HasAddressLookups(ds.Text);

        if (isPlainTextDefault)
        {
            RichPresenceDisplayString newDs;
            newDs.Condition = "";
            newDs.Text = ds.Text;
            newScript.DisplayStrings.append(newDs);
            continue;
        }

        for (const auto &config : configs)
        {
            RichPresenceDisplayString newDs;

            // A. Build Lock Logic (Using positive conditions)
            QString lockLogic = BuildLockString(config.LockConditions);

            // B. Combine
            if (lockLogic.isEmpty()) newDs.Condition = ds.Condition;
            else if (ds.Condition.isEmpty()) newDs.Condition = lockLogic;
            else newDs.Condition = QString("%1_%2").arg(lockLogic, ds.Condition);

            // C. Address Mapping (Range Aware)
            if (!config.IsBase && !config.AddressRanges.isEmpty())
            {
                newDs.Condition = ApplyAddressMap(newDs.Condition, config.AddressRanges);
                newDs.Text = ApplyAddressMap(ds.Text, config.AddressRanges);
            }
            else
            {
                newDs.Text = ds.Text;
            }

            newScript.DisplayStrings.append(newDs);
        }
    }

    return newScript;
}

bool RichPresenceService::HasAddressLookups(const QString &text)
{
    // Checks for @Lookup(0x...) or just 0x...
    static const QRegularExpression regex(R"(0x[0-9A-Fa-f]+)");
    return regex.match(text).hasMatch();
}

QString RichPresenceService::BuildLockString(const QList<AchievementCondition> &conditions)
{
    QString sb;
    for (const auto &c : conditions)
    {
        if (!sb.isEmpty()) sb += '_';
        AchievementCondition copy = c.Clone();
        copy.Flag = ""; // RP script logic does not use flags

        // Note: We deliberately do NOT invert the operator here.
        // Rich Presence Display strings need the POSITIVE condition to show the string (e.g. ?0x1234=1?Playing USA)
        sb += LogicFormatter::ConditionToString(copy);
    }
    return sb;
}

QString RichPresenceService::ApplyAddressMap(const QString &input, const QList<AddressRangeMap> &ranges)
{
    static const QRegularExpression regex(R"(0x\s*([HhXxWwMmNnOoPpQqRrSsTtLlUuKkGgIiJj])?\s*([0-9A-Fa-f]+))");
    QString result = input;
    auto matches = regex.globalMatch(input);

    // Replace in reverse order so offsets don't invalidate
    QList<QRegularExpressionMatch> matchList;
    while (matches.hasNext()) matchList.append(matches.next());

    for (int i = matchList.size() - 1; i >= 0; --i)
    {
        const auto &m = matchList[i];
        QString prefix = m.captured(1);
        QString rawHex = m.captured(2);

        bool ok = false;
        int64_t addr = rawHex.toLongLong(&ok, 16);
        if (ok)
        {
            for (const auto &range : ranges)
            {
                if (range.IsInRange(addr))
                {
                    int64_t newAddr = range.CalculateTarget(addr);
                    // Pass both arguments in a single .arg() call so %1 and %2 are evaluated simultaneously
                    QString replacement = QString("0x%1%2").arg(prefix, QString::number(newAddr, 16));
                    result.replace(m.capturedStart(), m.capturedLength(), replacement);
                    break;
                }
            }
        }
    }

    return result;
}

} // namespace RALogicEditor
