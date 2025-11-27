#nullable enable
using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace RALogicEditor.Services
{
    public class LogicOptimizer
    {
        public int OptimizeAsset(IAssetItem asset)
        {
            // Select parsing strategy based on asset type
            IAssetStrategy strategy = asset.IsLeaderboard ? new LeaderboardStrategy() : new AchievementStrategy();

            var segments = strategy.ParseLogic(asset.Logic);
            int changeCount = 0;

            foreach (var segmentKey in segments.Keys)
            {
                var groups = segments[segmentKey];
                foreach (var group in groups)
                {
                    foreach (var cond in group.Conditions)
                    {
                        // Optimize operands
                        if (OptimizeOperand(cond.LeftOperand)) changeCount++;
                        if (OptimizeOperand(cond.RightOperand)) changeCount++;
                    }
                }
            }

            if (changeCount > 0)
            {
                asset.Logic = strategy.BuildLogic(segments);
                if (asset.State == AssetState.Synced) asset.State = AssetState.Modified;
            }

            return changeCount;
        }

        public int PadAsset(IAssetItem asset)
        {
            IAssetStrategy strategy = asset.IsLeaderboard ? new LeaderboardStrategy() : new AchievementStrategy();
            var segments = strategy.ParseLogic(asset.Logic);
            int changeCount = 0;

            foreach (var segmentKey in segments.Keys)
            {
                var groups = segments[segmentKey];
                foreach (var group in groups)
                {
                    foreach (var cond in group.Conditions)
                    {
                        if (PadOperand(cond.LeftOperand)) changeCount++;
                        if (PadOperand(cond.RightOperand)) changeCount++;
                    }
                }
            }

            if (changeCount > 0)
            {
                asset.Logic = strategy.BuildLogic(segments);
                if (asset.State == AssetState.Synced) asset.State = AssetState.Modified;
            }

            return changeCount;
        }

        public bool IsFullyOptimized(IAssetItem asset)
        {
            IAssetStrategy strategy = asset.IsLeaderboard ? new LeaderboardStrategy() : new AchievementStrategy();
            var segments = strategy.ParseLogic(asset.Logic);

            foreach (var segmentKey in segments.Keys)
            {
                var groups = segments[segmentKey];
                foreach (var group in groups)
                {
                    foreach (var cond in group.Conditions)
                    {
                        if (HasLeadingZeros(cond.LeftOperand)) return false;
                        if (HasLeadingZeros(cond.RightOperand)) return false;
                    }
                }
            }
            return true;
        }

        private bool HasLeadingZeros(Operand op)
        {
            if (string.IsNullOrEmpty(op.Value)) return false;

            // Only check memory or explicit hex values
            bool isMemory = RaLogicSyntax.MemoryTypes.Contains(op.Type);
            bool isHexValue = op.Type == "Value" && op.Value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);

            if (!isMemory && !isHexValue) return false;

            string val = op.Value.Trim();
            if (val.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                string body = val.Substring(2);
                // If length > 1 and starts with 0, it has leading zeros (e.g., 05, 0012)
                // Exception: 0x0 is fine. 0x00 is not.
                if (body.Length > 1 && body.StartsWith("0")) return true;
            }
            return false;
        }

        private bool OptimizeOperand(Operand op)
        {
            if (string.IsNullOrEmpty(op.Value)) return false;

            bool isMemory = RaLogicSyntax.MemoryTypes.Contains(op.Type);
            bool isHexValue = op.Type == "Value" && op.Value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);

            if (!isMemory && !isHexValue) return false;

            string original = op.Value;
            string clean = original.Trim();

            if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                string body = clean.Substring(2);

                // Strip leading zeros
                string optimizedBody = body.TrimStart('0');

                // Ensure we don't end up with empty string for value 0
                if (string.IsNullOrEmpty(optimizedBody)) optimizedBody = "0";

                // Reconstruct with 0x
                string optimized = "0x" + optimizedBody.ToLower(); // Force lowercase

                if (optimized != original)
                {
                    op.Value = optimized;
                    return true;
                }
            }

            return false;
        }

        private bool PadOperand(Operand op)
        {
            if (string.IsNullOrEmpty(op.Value)) return false;

            bool isMemory = RaLogicSyntax.MemoryTypes.Contains(op.Type);
            bool isHexValue = op.Type == "Value" && op.Value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);

            if (!isMemory && !isHexValue) return false;

            string original = op.Value;
            string clean = original.Trim();

            if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                string body = clean.Substring(2);

                // Pad to 8 chars
                if (body.Length < 8)
                {
                    string paddedBody = body.PadLeft(8, '0');
                    string padded = "0x" + paddedBody.ToLower();

                    if (padded != original)
                    {
                        op.Value = padded;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}