using RALogicEditor.Models; // Now finds RegionConfig here
using RALogicEditor.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Services
{
    public class RichPresenceService
    {
        public RichPresenceScript GenerateMultiRegion(RichPresenceScript sourceScript, List<RegionConfig> configs)
        {
            var newScript = new RichPresenceScript();

            // 1. Copy Lookups (Shared)
            foreach (var lookup in sourceScript.Lookups)
            {
                newScript.Lookups.Add(lookup);
            }

            // 2. Process Display Strings
            for (int i = 0; i < sourceScript.DisplayStrings.Count; i++)
            {
                var ds = sourceScript.DisplayStrings[i];
                bool isLastLine = (i == sourceScript.DisplayStrings.Count - 1);

                // Optimization: If the last line has no condition (default) and contains no lookup addresses (plain text),
                // do NOT duplicate it with locks. Just add it once at the end.
                // This prevents "Playing Project: Snowblind" from being turned into "?Lock?Playing..."
                bool isPlainTextDefault = isLastLine &&
                                          string.IsNullOrEmpty(ds.Condition) &&
                                          !HasAddressLookups(ds.Text);

                if (isPlainTextDefault)
                {
                    newScript.DisplayStrings.Add(new RichPresenceDisplayString
                    {
                        Condition = "",
                        Text = ds.Text
                    });
                    continue;
                }

                foreach (var config in configs)
                {
                    var newDs = new RichPresenceDisplayString();

                    // A. Build Lock Logic (Using positive conditions)
                    string lockLogic = BuildLockString(config.LockConditions);

                    // B. Combine
                    if (string.IsNullOrEmpty(ds.Condition)) newDs.Condition = lockLogic;
                    else newDs.Condition = $"{lockLogic}_{ds.Condition}";

                    // C. Address Mapping (Range Aware)
                    if (!config.IsBase && config.AddressRanges.Count > 0)
                    {
                        newDs.Condition = ApplyAddressMap(newDs.Condition, config.AddressRanges);
                        newDs.Text = ApplyAddressMap(ds.Text, config.AddressRanges);
                    }
                    else
                    {
                        newDs.Text = ds.Text;
                    }

                    newScript.DisplayStrings.Add(newDs);
                }
            }

            return newScript;
        }

        private bool HasAddressLookups(string text)
        {
            // Checks for @Lookup(0x...) or just 0x...
            return Regex.IsMatch(text, @"0x[0-9A-Fa-f]+");
        }

        private string BuildLockString(List<AchievementCondition> conditions)
        {
            var sb = new StringBuilder();
            foreach (var c in conditions)
            {
                if (sb.Length > 0) sb.Append('_');
                var copy = c.Clone();
                copy.Flag = ""; // RP script logic does not use flags

                // Note: We deliberately do NOT invert the operator here.
                // Rich Presence Display strings need the POSITIVE condition to show the string (e.g. ?0x1234=1?Playing USA)
                sb.Append(LogicFormatter.ConditionToString(copy));
            }
            return sb.ToString();
        }

        private string ApplyAddressMap(string input, List<AddressRangeMap> ranges)
        {
            return Regex.Replace(input, @"0x\s*([HhXxWwMmNnOoPpQqRrSsTtLlUuKkGgIiJj])?\s*([0-9A-Fa-f]+)", m =>
            {
                string prefix = m.Groups[1].Success ? m.Groups[1].Value : "";
                string rawHex = m.Groups[2].Value;

                if (long.TryParse(rawHex, System.Globalization.NumberStyles.HexNumber, null, out long addr))
                {
                    foreach (var range in ranges)
                    {
                        if (range.IsInRange(addr))
                        {
                            long newAddr = range.CalculateTarget(addr);
                            return "0x" + prefix + newAddr.ToString("x");
                        }
                    }
                }
                return m.Value;
            });
        }
    }
}