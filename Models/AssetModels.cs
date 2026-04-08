using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RALogicEditor.Models
{
    public enum AssetState
    {
        Synced,
        Modified,
        Local
    }

    public interface IAssetItem
    {
        int ID { get; set; }
        string Title { get; set; }
        string Description { get; set; }
        string Logic { get; set; }
        string Type { get; set; }
        string Category { get; set; } // Tracks "Core", "Unofficial", or "Local"
        bool IsLeaderboard { get; }
        AssetState State { get; set; }
    }

    public class RaRoot
    {
        public bool Success { get; set; }
        public int GameId { get; set; }
        public string Title { get; set; } = "";
        public string ImageIconUrl { get; set; } = "";
        public int RichPresenceGameId { get; set; }
        public string RichPresencePatch { get; set; } = "";
        public int ConsoleId { get; set; }
        public List<RaSet> Sets { get; set; } = new List<RaSet>();
    }

    public class RaSet
    {
        public string Title { get; set; } = "";
        public string Type { get; set; } = ""; // "core", "unofficial"
        public int AchievementSetId { get; set; }
        public int GameId { get; set; }
        public string ImageIconUrl { get; set; } = "";
        public List<RaAchievement> Achievements { get; set; } = new List<RaAchievement>();
        public List<RaLeaderboard> Leaderboards { get; set; } = new List<RaLeaderboard>();
    }

    public class RaAchievement : IAssetItem
    {
        public int ID { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int Points { get; set; }
        public string Author { get; set; } = "Unknown";
        public string BadgeName { get; set; } = "00000";
        public string Type { get; set; } = "";

        // Used to determine Category: 3 = Core, 5 = Unofficial
        public int Flags { get; set; }

        [JsonPropertyName("MemAddr")]
        public string Logic { get; set; } = "";

        [JsonIgnore]
        public string Category { get; set; } = "Local"; // Default to Local until linked

        [JsonIgnore]
        public bool IsLeaderboard => false;

        [JsonIgnore]
        public AssetState State { get; set; } = AssetState.Synced;

        public override string ToString() => $"{ID} - {Title}";

        public RaAchievement Clone(bool newId = true)
        {
            return new RaAchievement
            {
                ID = newId ? 0 : this.ID,
                Title = this.Title,
                Description = this.Description,
                Points = this.Points,
                Author = this.Author,
                BadgeName = this.BadgeName,
                Type = this.Type,
                Flags = this.Flags,
                Logic = this.Logic,
                Category = this.Category,
                State = this.State
            };
        }
    }

    public class RaLeaderboard : IAssetItem
    {
        public int ID { get; set; }

        [JsonPropertyName("Mem")]
        public string Logic { get; set; } = "";

        public string Format { get; set; } = "VALUE";
        public bool LowerIsBetter { get; set; } // Fixed JSON breaking change (int -> bool)
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public bool Hidden { get; set; }

        [JsonIgnore]
        public string Type { get; set; } = "Leaderboard";

        [JsonIgnore]
        public string Category { get; set; } = "Local"; // Default

        [JsonIgnore]
        public bool IsLeaderboard => true;

        [JsonIgnore]
        public AssetState State { get; set; } = AssetState.Synced;

        public override string ToString() => $"L{ID} - {Title}";

        public RaLeaderboard Clone(bool newId = true)
        {
            return new RaLeaderboard
            {
                ID = newId ? 0 : this.ID,
                Title = this.Title,
                Description = this.Description,
                Format = this.Format,
                LowerIsBetter = this.LowerIsBetter,
                Hidden = this.Hidden,
                Logic = this.Logic,
                Category = this.Category,
                State = this.State
            };
        }
    }
}