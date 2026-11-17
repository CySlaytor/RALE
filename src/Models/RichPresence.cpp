#include "RichPresence.h"
#include <QStringBuilder>

namespace RALogicEditor {

QString RichPresenceScript::ToString() const
{
    QString result;

    // Lookups
    for (const auto &lookup : Lookups)
    {
        if (!lookup.Entries.isEmpty() || lookup.Default.has_value())
        {
            result += QString("Lookup:%1\n").arg(lookup.Name);
            for (const auto &entry : lookup.Entries)
                result += QString("%1=%2\n").arg(entry.Key, entry.Value);
            if (lookup.Default.has_value())
                result += QString("*=%1\n").arg(lookup.Default.value());
            result += "\n";
        }
    }

    // Formatters
    for (const auto &fmt : Lookups)
    {
        if (fmt.Entries.isEmpty() && !fmt.Default.has_value())
        {
            result += QString("Format:%1\n").arg(fmt.Name);
            result += QString("FormatType=%1\n").arg(fmt.Format);
            result += "\n";
        }
    }

    // Display
    result += "Display:\n";
    for (const auto &ds : DisplayStrings)
    {
        QString line = "";
        if (!ds.Condition.isEmpty()) line += QString("?%1?").arg(ds.Condition);
        line += ds.Text;
        result += line + "\n";
    }

    return result;
}

} // namespace RALogicEditor
