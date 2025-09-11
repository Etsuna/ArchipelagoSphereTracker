using System.Text.Json.Serialization;
using TrackerLib.Converters;

namespace TrackerLib.Models
{
    public sealed class RoomStatus
    {
        [JsonPropertyName("downloads")]
        public List<DownloadEntry> Downloads { get; set; } = new();

        [JsonPropertyName("last_activity")]
        [JsonConverter(typeof(Rfc1123DateTimeOffsetConverter))]
        public DateTimeOffset? LastActivity { get; set; }

        [JsonPropertyName("last_port")]
        public int LastPort { get; set; }

        [JsonPropertyName("players")]
        public List<PlayerEntry> Players { get; set; } = new();

        [JsonPropertyName("timeout")]
        public int Timeout { get; set; }

        [JsonPropertyName("tracker")]
        public string Tracker { get; set; } = "";
    }

    public sealed class DownloadEntry
    {
        [JsonPropertyName("download")]
        public string Download { get; set; } = "";

        [JsonPropertyName("slot")]
        public int Slot { get; set; }
    }

    [JsonConverter(typeof(PlayerEntryConverter))]
    public sealed class PlayerEntry
    {
        public string Name { get; set; } = "";
        public string Game { get; set; } = "";
    }
}
