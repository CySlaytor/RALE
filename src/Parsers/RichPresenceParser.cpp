#include "RichPresenceParser.h"

namespace RALE {

RichPresenceScript RichPresenceParser::Parse(const QString& content) {
    RichPresenceScript script;
    QString normalized = content;
    normalized.replace("\r\n", "\n").replace('\r', '\n');
    QStringList lines = normalized.split('\n', Qt::SkipEmptyParts);

    QString section = "";
    RichPresenceLookup* currentLookup = nullptr;

    for (const QString& rawLine : lines) {
        QString line = rawLine.trimmed();
        if (line.startsWith("//"))
            continue;

        if (line.startsWith("Lookup:")) {
            section = "Lookup";
            RichPresenceLookup lookup;
            lookup.Name = line.mid(7).trimmed();
            script.Lookups.append(lookup);
            currentLookup = &script.Lookups.last();
            continue;
        }
        if (line.startsWith("Format:")) {
            section = "Format";
            RichPresenceLookup lookup;
            lookup.Name = line.mid(7).trimmed();
            script.Lookups.append(lookup);
            currentLookup = &script.Lookups.last();
            continue;
        }
        if (line.startsWith("Display:")) {
            section = "Display";
            continue;
        }

        if (section == "Lookup" && currentLookup != nullptr) {
            int eqIdx = line.indexOf('=');
            if (eqIdx != -1) {
                QString key = line.left(eqIdx).trimmed();
                QString val = line.mid(eqIdx + 1).trimmed();

                if (key == "*")
                    currentLookup->Default = val;
                else
                    currentLookup->Entries.append(LookupEntry{key, val});
            }
        } else if (section == "Format" && currentLookup != nullptr) {
            if (line.startsWith("FormatType="))
                currentLookup->Format = line.mid(11).trimmed();
        } else if (section == "Display") {
            RichPresenceDisplayString ds;
            QString temp = line;
            if (temp.startsWith('?')) {
                int endQ = temp.indexOf('?', 1);
                if (endQ > 1) {
                    ds.Condition = temp.mid(1, endQ - 1);
                    temp = temp.mid(endQ + 1);
                }
            }
            ds.Text = temp;
            script.DisplayStrings.append(ds);
        }
    }

    return script;
}

} // namespace RALE
