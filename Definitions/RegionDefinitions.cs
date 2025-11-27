using System;
using System.Collections.Generic;
using System.Linq;

namespace RALogicEditor.Definitions
{
    // Global definitions for Region keywords to identify NTSC-U, PAL, NTSC-J, etc.
    public static class RegionDefinitions
    {
        // Dictionary mapping a canonical Region Name to a list of keywords
        public static readonly Dictionary<string, List<string>> Regions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            // Removed 2-letter codes to prevent false positives (e.g. AS in ASCII, JP in Mpeg, EU in Pseudo)
            { "USA", new List<string> { "NTSC", "NTSC-U", "USA", "North America", "SLUS", "BLUS", "ULUS", "BASLUS" } },
            { "Europe", new List<string> { "PAL", "PAL-E", "Europe", "SLES", "BLES", "ULES", "BESLES" } },
            { "Japan", new List<string> { "NTSC-J", "Japan", "SLPS", "BLJS", "ULJS" } },
            { "Asia", new List<string> { "NTSC-Asia", "Asia", "SLPM", "BLAS" } }
        };

        // Detects the region from a given text string (e.g. Code Note) based on keywords.
        // Returns "Unknown" if no match is found.
        public static string DetectRegion(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "Unknown";

            foreach (var region in Regions)
            {
                foreach (var keyword in region.Value)
                {
                    // Case-insensitive distinct word match or substring check
                    // We stick to simple case-insensitive substring for robustness with serials
                    if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return region.Key;
                    }
                }
            }

            return "Unknown";
        }
        // Checks if a text contains keywords for a specific region.
        public static bool IsRegion(string text, string regionKey)
        {
            if (!Regions.ContainsKey(regionKey)) return false;
            foreach (var keyword in Regions[regionKey])
            {
                if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}