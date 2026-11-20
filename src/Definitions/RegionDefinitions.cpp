#include "RegionDefinitions.h"

namespace RALE {

// Dictionary mapping a canonical Region Name to a list of keywords
const QMap<QString, QStringList> RegionDefinitions::Regions = {
    // Removed 2-letter codes to prevent false positives (e.g. AS in ASCII, JP in Mpeg, EU in Pseudo)
    {"USA", {"NTSC", "NTSC-U", "USA", "North America", "SLUS", "BLUS", "ULUS", "BASLUS"}},
    {"Europe", {"PAL", "PAL-E", "Europe", "SLES", "BLES", "ULES", "BESLES"}},
    {"Japan", {"NTSC-J", "Japan", "SLPS", "BLJS", "ULJS"}},
    {"Asia", {"NTSC-Asia", "Asia", "SLPM", "BLAS"}}};

// Detects the region from a given text string (e.g. Code Note) based on keywords.
// Returns "Unknown" if no match is found.
QString RegionDefinitions::DetectRegion(const QString& text) {
    if (text.trimmed().isEmpty())
        return "Unknown";

    // Preserve the C# insertion priority (USA first). QMap iterates alphabetically,
    // which made ambiguous text such as "SLUS-12345 (PAL)" resolve to Europe.
    static const QStringList priority = {"USA", "Europe", "Japan", "Asia"};
    for (const QString& region : priority) {
        auto it = Regions.constFind(region);
        if (it == Regions.constEnd())
            continue;
        for (const QString& keyword : it.value()) {
            // Case-insensitive distinct word match or substring check
            // We stick to simple case-insensitive substring for robustness with serials
            if (text.contains(keyword, Qt::CaseInsensitive)) {
                return it.key();
            }
        }
    }

    return "Unknown";
}

} // namespace RALE
