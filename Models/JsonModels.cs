using System.Text.Json.Serialization;

namespace RALogicEditor.Models
{
    public enum AssetState
    {
        Synced,     // Uploaded (Clean)
        Modified,   // Uploaded (Dirty)
        Local       // New Local
    }

    public interface IAssetItem
    {
        int ID { get; set; }
        string Title { get; set; }
        string Description { get; set; }
        string Logic { get; set; }
        string Type { get; set; }
        bool IsLeaderboard { get; }
        AssetState State { get; set; }
    }

    public class RaRoot
    {
        public bool Success { get; set; }
        public int GameId { get; set; }
        public string Title { get; set; } = "";

        // Added to handle the massive patch string without errors
        public string RichPresencePatch { get; set; } = "";

        public List<RaSet> Sets { get; set; } = new List<RaSet>();
    }

    public class RaSet
    {
        public string Type { get; set; } = "";
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
        public long Created { get; set; }
        public long Modified { get; set; }
        public string Type { get; set; } = "";

        [JsonPropertyName("MemAddr")]
        public string Logic { get; set; } = "";

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
                Logic = this.Logic,
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
        public int LowerIsBetter { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public bool Hidden { get; set; }

        [JsonIgnore]
        public string Type { get; set; } = "Leaderboard";

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
                State = this.State
            };
        }
    }
}