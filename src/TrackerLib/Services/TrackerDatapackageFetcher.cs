using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchipelagoSphereTracker.src.TrackerLib.Services
{
    public static class TrackerDatapackageFetcher
    {
        public static async Task<TrackerRoot> getRoots(string baseUrl, string trackerId, HttpClient? http = null)
        {
            var url = $"{baseUrl.TrimEnd('/')}/api/tracker/{trackerId}";
            http ??= new HttpClient();
            var json = await http.GetStringAsync(url);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var rootTracker = JsonSerializer.Deserialize<TrackerRoot>(json, options)
                               ?? throw new InvalidOperationException("Empty tracker payload");

            return rootTracker;
        }

        public static IDictionary<string, string> GetDatapackageChecksums(TrackerRoot root)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.DataPackage != null)
            {
                foreach (var kvp in root.DataPackage)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Value?.Checksum))
                        result[kvp.Key] = kvp.Value.Checksum!;
                }
            }
            return result;
        }

        public sealed class TrackerRoot
        {
            [JsonPropertyName("datapackage")]
            public Dictionary<string, PackageInfo>? DataPackage { get; set; }
        }

        public sealed class PackageInfo
        {
            [JsonPropertyName("checksum")]
            public string? Checksum { get; set; }

            [JsonPropertyName("version")]
            public int Version { get; set; }
        }

        public static async Task SeedDatapackagesFromTrackerAsync(
    string baseUrl, string guildId, string channelId, TrackerRoot root)
        {
            if (root.DataPackage is null || root.DataPackage.Count == 0) return;

            foreach (var (game, info) in root.DataPackage)
            {
                var checksum = info?.Checksum;
                if (string.IsNullOrWhiteSpace(game) || string.IsNullOrWhiteSpace(checksum))
                    continue;

                var link = $"{baseUrl.TrimEnd('/')}/api/datapackage/{checksum}";
                await DatapackageStore.ImportAsync(
                    link, guildId, channelId,
                    datasetKey: checksum,
                    truncate: false,
                    gameName: game);
            }
        }
    }
}