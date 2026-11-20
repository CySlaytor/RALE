#pragma once

#include <QList>
#include <QString>

#include <optional>

namespace RALE {

class LookupEntry {
  public:
    QString Key = "";
    QString Value = "";
};

class RichPresenceLookup {
  public:
    QString Name = "";
    QString Format = "VALUE";
    std::optional<QString> Default;
    QList<LookupEntry> Entries;
};

class RichPresenceDisplayString {
  public:
    QString Condition = "";
    QString Text = "";
};

class RichPresenceScript {
  public:
    QList<RichPresenceLookup> Lookups;
    QList<RichPresenceDisplayString> DisplayStrings;

    QString ToString() const;
};

} // namespace RALE
