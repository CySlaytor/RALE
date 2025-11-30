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
            foreach (var ds in sourceScript.DisplayStrings)
            {
                foreach (var config in configs)
                {
                    var newDs = new RichPresenceDisplayString();

                    // A. Build Lock Logic
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

        private string BuildLockString(List<AchievementCondition> conditions)
        {
            var sb = new StringBuilder();
            foreach (var c in conditions)
            {
                if (sb.Length > 0) sb.Append('_');
                var copy = c.Clone();
                copy.Flag = "";
                copy.Operator = InvertOperator(copy.Operator);
                sb.Append(LogicFormatter.ConditionToString(copy));
            }
            return sb.ToString();
        }

        private string InvertOperator(string op)
        {
            return op switch
            {
                "=" => "!=",
                "!=" => "=",
                "<" => ">=",
                "<=" => ">",
                ">" => "<=",
                ">=" => "<",
                _ => op
            };
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