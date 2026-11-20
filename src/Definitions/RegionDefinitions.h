#pragma once

#include <QMap>
#include <QString>
#include <QStringList>

namespace RALE {

// Global definitions for Region keywords to identify NTSC-U, PAL, NTSC-J, etc.
class RegionDefinitions {
  public:
    // Dictionary mapping a canonical Region Name to a list of keywords
    static const QMap<QString, QStringList> Regions;

    // Detects the region from a given text string (e.g. Code Note) based on keywords.
    // Returns "Unknown" if no match is found.
    static QString DetectRegion(const QString& text);

    // Checks if a text contains keywords for a specific region.
    static bool IsRegion(const QString& text, const QString& regionKey);
};

} // namespace RALE
