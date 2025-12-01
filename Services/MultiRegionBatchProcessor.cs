using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RALogicEditor.Services
{
    public class MultiRegionBatchProcessor
    {
        private readonly ProjectMetadataService _metadata;

        public MultiRegionBatchProcessor(ProjectMetadataService metadata)
        {
            _metadata = metadata;
        }

        public void ApplyMultiRegionToAsset(IAssetItem localItem, RegionConfig? baseConfig, List<RegionConfig> targetConfigs)
        {
            // Parse Existing Logic
            IAssetStrategy strategy = localItem.IsLeaderboard ? new LeaderboardStrategy() : new AchievementStrategy();
            var segments = strategy.ParseLogic(localItem.Logic);

            var keys = new List<string>(segments.Keys);

            foreach (var segmentKey in keys)
            {
                var groups = segments[segmentKey];
                if (groups.Count == 0) continue;

                // Identify existing groups (Base Groups)
                var existingGroups = new List<AchievementConditionGroup>();
                // Deep copy existing groups to avoid reference issues
                foreach (var g in groups)
                {
                    var newG = new AchievementConditionGroup { GroupName = g.GroupName };
                    foreach (var c in g.Conditions) newG.Conditions.Add(c.Clone());
                    existingGroups.Add(newG);
                }

                var finalGroups = new List<AchievementConditionGroup>();

                // Handle Base Groups
                if (baseConfig != null && baseConfig.LockConditions.Count > 0)
                {
                    foreach (var grp in existingGroups)
                    {
                        grp.Conditions.InsertRange(0, baseConfig.LockConditions.Select(c => c.Clone()));
                    }
                }

                // Rename Base Groups if needed (and Metadata update)
                // When we split Core into Alts, we lose the original Core if we don't save the new names.
                if (!localItem.IsLeaderboard && existingGroups.Count > 0 && existingGroups[0].GroupName == "Core")
                {
                    string baseName = baseConfig?.RegionName ?? "Region A";
                    existingGroups[0].GroupName = $"Alt 1 ({baseName})";
                    for (int i = 1; i < existingGroups.Count; i++) existingGroups[i].GroupName = $"Alt {i + 1} ({baseName})";
                    finalGroups.Add(new AchievementConditionGroup { GroupName = "Core" }); // Empty Core
                }
                else if (!localItem.IsLeaderboard && existingGroups.Count > 0 && existingGroups[0].GroupName == "Core")
                {
                    finalGroups.Add(existingGroups[0]);
                    existingGroups.RemoveAt(0);
                }

                finalGroups.AddRange(existingGroups);

                // Create Target Region Groups
                foreach (var targetCfg in targetConfigs)
                {
                    // Clone from original logic structure (before we modified existingGroups with locks)
                    var newRegionGroups = new List<AchievementConditionGroup>();
                    foreach (var g in groups) // 'groups' contains the parsed objects from 'localItem.Logic'
                    {
                        var clone = new AchievementConditionGroup { GroupName = g.GroupName + $" ({targetCfg.RegionName})" };
                        foreach (var c in g.Conditions) clone.Conditions.Add(c.Clone());
                        newRegionGroups.Add(clone);
                    }

                    // Apply Address Swap
                    foreach (var grp in newRegionGroups)
                    {
                        foreach (var c in grp.Conditions)
                        {
                            ApplyAddressSwap(c.LeftOperand, targetCfg.AddressRanges);
                            ApplyAddressSwap(c.RightOperand, targetCfg.AddressRanges);
                        }
                    }

                    // Apply Lock
                    foreach (var grp in newRegionGroups)
                    {
                        grp.Conditions.InsertRange(0, targetCfg.LockConditions.Select(c => c.Clone()));
                    }

                    // Reassemble Alts naming
                    int startIdx = finalGroups.Count;
                    for (int i = 0; i < newRegionGroups.Count; i++)
                    {
                        if (!localItem.IsLeaderboard) newRegionGroups[i].GroupName = $"Alt {startIdx + i} ({targetCfg.RegionName})";
                    }

                    finalGroups.AddRange(newRegionGroups);
                }

                segments[segmentKey] = finalGroups;

                // IMPORTANT: Update Metadata with new Group Names
                _metadata.UpdateAssetGroups(localItem.ID, finalGroups);
            }

            // Save Back
            localItem.Logic = strategy.BuildLogic(segments);
        }

        private void ApplyAddressSwap(Operand op, List<AddressRangeMap> ranges)
        {
            if (!RaLogicSyntax.MemoryTypes.Contains(op.Type)) return;
            string addr = op.Value.Trim();
            if (string.IsNullOrEmpty(addr)) return;

            string clean = addr.Replace("0x", "").Trim();
            if (long.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val))
            {
                foreach (var range in ranges)
                {
                    if (range.IsInRange(val))
                    {
                        long newVal = range.CalculateTarget(val);
                        op.Value = "0x" + newVal.ToString("x");
                        return; // Swap once
                    }
                }
            }
        }
    }
}