using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrackerLib.Services
{
    public static class DatapackageClient
    {
        public sealed class DatapackagePayload
        {
            [JsonPropertyName("checksum")]
            public string? Checksum { get; set; }

            [JsonPropertyName("item_name_groups")]
            public Dictionary<string, List<string>>? ItemNameGroups { get; set; }

            [JsonPropertyName("item_name_to_id")]
            public Dictionary<string, JsonElement>? ItemNameToId { get; set; }

            [JsonPropertyName("location_name_groups")]
            public Dictionary<string, List<string>>? LocationNameGroups { get; set; }

            [JsonPropertyName("location_name_to_id")]
            public Dictionary<string, JsonElement>? LocationNameToId { get; set; }
        }

        public sealed class DatapackageIndex
        {
            public string Checksum { get; init; } = string.Empty;
            public Dictionary<int, string> ItemIdToName { get; } = new();
            public Dictionary<int, string > LocationIdToName { get; } = new ();
            public Dictionary<string, List<string>> ItemGroups { get; } = new();
        public Dictionary<string, List<string>> LocationGroups { get; } = new();
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public static async Task<DatapackageIndex> FetchOneAsync(string baseUrl, string checksum, HttpClient? http = null, CancellationToken ct = default)
        {
            var ownClient = http is null;
            http ??= new HttpClient();
            try
            {
                var url = $"{baseUrl.TrimEnd('/')}/api/datapackage/{checksum}";
                var json = await http.GetStringAsync(url, ct);

                var payload = JsonSerializer.Deserialize<DatapackagePayload>(json, JsonOpts)
                              ?? throw new InvalidOperationException("Empty datapackage payload.");

                var idx = new DatapackageIndex { Checksum = payload.Checksum ?? checksum };

                if (payload.ItemNameToId != null)
                {
                    foreach (var kv in payload.ItemNameToId)
                    {
                        int id;
                        try
                        {
                            id = kv.Value.ValueKind switch
                            {
                                JsonValueKind.Number => kv.Value.GetInt32(),
                                JsonValueKind.String when int.TryParse(kv.Value.GetString(), out var n) => n,
                                _ => -1
                            };
                        }
                        catch { id = -1; }
                        if (id >= 0) idx.ItemIdToName[id] = kv.Key;
                    }
                }

                if (payload.LocationNameToId != null)
                {
                    foreach (var kv in payload.LocationNameToId)
                    {
                        int id;
                        try
                        {
                            id = kv.Value.ValueKind switch
                            {
                                JsonValueKind.Number => kv.Value.GetInt32(),
                                JsonValueKind.String when int.TryParse(kv.Value.GetString(), out var n) => n,
                                _ => -1
                            };
                        }
                        catch { id = -1; }
                        if (id >= 0) idx.LocationIdToName[id] = kv.Key;
                    }
                }

                if (payload.ItemNameGroups != null)
                    foreach (var g in payload.ItemNameGroups) idx.ItemGroups[g.Key] = g.Value;
                if (payload.LocationNameGroups != null)
                    foreach (var g in payload.LocationNameGroups) idx.LocationGroups[g.Key] = g.Value;

                return idx;
            }
            finally
            {
                if (ownClient) http?.Dispose();
            }
        }
    }
}
