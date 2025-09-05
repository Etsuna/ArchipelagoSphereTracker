using Discord;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class GetData
{
    public static async Task<List<TrackerItemsEnricher.EnrichedTeamItems>?> Data(string baseUrl, string roomId)
    {
        var room = await RoomInfo(baseUrl, roomId);

        if(room is null) return null;

        // 1) checksums depuis /api/tracker/<tracker_id>
        var checksums = await TrackerDatapackageFetcher.GetDatapackageChecksumsAsync(baseUrl, room.Tracker);
        IDictionary<string, string> gameToChecksum = checksums; // "GameName" => "checksum"

        // 2) fetch tous les datapackages
        var all = await DatapackageClient.FetchManyAsync(baseUrl, gameToChecksum.Values);

        // 3) Construire un mapping par jeu: (itemId->name, locationId->name)
        var perGameIndexes = new Dictionary<string, DatapackageClient.DatapackageIndex>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in gameToChecksum) // kv.Key = game, kv.Value = checksum
        {
            if (all.TryGetValue(kv.Value, out var idx))
                perGameIndexes[kv.Key] = idx;
        }

        // 3bis) Convertir vers le type attendu par TrackerItemsEnricher
        var perGameIndexesForTracker = new Dictionary<string, TrackerItemsEnricher.DatapackageIndex>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in perGameIndexes)
        {
            var src = kv.Value;
            var dest = new TrackerItemsEnricher.DatapackageIndex { };
            // copy dicts
            foreach (var p in src.ItemIdToName)
                dest.ItemIdToName[p.Key] = p.Value;
            foreach (var p in src.LocationIdToName)
                dest.LocationIdToName[p.Key] = p.Value;
            perGameIndexesForTracker[kv.Key] = dest;
        }

        // Affichage checksums (optionnel)
        Console.WriteLine("Datapackage checksums:");
        foreach (var kv in checksums)
            Console.WriteLine($"  {kv.Key} => {kv.Value}");

        // Petite fonction locale slot -> nom joueur
        string GetPlayerNameBySlot(int slot)
        {
            var idx = slot - 1;
            if (idx >= 0 && idx < room.Players.Count)
                return room.Players[idx].Name;
            return $"Player{slot}";
        }

        Console.WriteLine($"\nTracker: {room.Tracker}");
        Console.WriteLine($"Last activity: {room.LastActivity:yyyy-MM-dd HH:mm:ss 'UTC'}");

        Console.WriteLine($"\nPlayers ({room.Players.Count}):");
        for (int i = 0; i < room.Players.Count; i++)
        {
            var p = room.Players[i];
            Console.WriteLine($"{p.Name} — {p.Game}");
        }

        Console.WriteLine("\nDownloads:");
        foreach (var dl in room.Downloads)
        {
            var playerName = GetPlayerNameBySlot(dl.Slot);
            Console.WriteLine($"{playerName} — {dl.Download}");
        }

        // 4) Préparer roomPlayers minimal pour l’enrichisseur
        var roomPlayers = new List<TrackerItemsEnricher.RoomPlayer>();
        foreach (var p in room.Players)
            roomPlayers.Add(new TrackerItemsEnricher.RoomPlayer { Name = p.Name, Game = p.Game });

        // 5) Récupérer et enrichir player_items_received
        var enriched = await TrackerItemsEnricher.FetchAndEnrichAsync(
            baseUrl,
            room.Tracker,
            roomPlayers,
            perGameIndexesForTracker
        );

        /*// 6) Affichage des items reçus enrichis
        Console.WriteLine("\nPlayer Items Received (enriched):");
        foreach (var team in enriched)
        {
            Console.WriteLine($"Team {team.Team}:");
            foreach (var pl in team.Players)
            {
                var pname = GetPlayerNameBySlot(pl.Player);
                Console.WriteLine($"  {pname} — {pl.Game}");
                foreach (var it in pl.Items)
                {
                    Console.WriteLine($"    • {it.ItemName} (#{it.ItemId}) " +
                                      $"@ {it.LocationName} (#{it.LocationId}) " +
                                      $"| From {it.FromPlayerName} (#{it.FromPlayerId}, {it.FromPlayerGame}) " +
                                      $"To {it.ToPlayerName} (#{it.ToPlayerId}, {it.ToPlayerGame}) " +
                                      $"Flags={it.Flags}");
                }
            }
        }*/

        return enriched;
    }

    public static async Task<RoomStatus?> RoomInfo(string baseUrl, string roomId)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/room_status/{roomId}";
        using var http = new HttpClient();
        var json = await http.GetStringAsync(url);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        var room = JsonSerializer.Deserialize<RoomStatus>(json, options);

        if (room is null)
        {
            Console.WriteLine("Failed to fetch or parse room status.");
            return null;
        }
        Console.WriteLine($"Tracker: {room.Tracker}");
        Console.WriteLine($"Last activity: {room.LastActivity:yyyy-MM-dd HH:mm:ss 'UTC'}");
        Console.WriteLine($"\nPlayers ({room.Players.Count}):");
        for (int i = 0; i < room.Players.Count; i++)
        {
            var p = room.Players[i];
            Console.WriteLine($"{p.Name} — {p.Game}");
        }
        Console.WriteLine("\nDownloads:");
        foreach (var dl in room.Downloads)
        {
            var playerName = (dl.Slot > 0 && dl.Slot <= room.Players.Count) ? room.Players[dl.Slot - 1].Name : $"Player{dl.Slot}";
            Console.WriteLine($"{playerName} — {dl.Download}");
        }

        return room;
    }

    // ---------- Models ----------
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

    // ---------- Converters ----------
    public sealed class PlayerEntryConverter : JsonConverter<PlayerEntry>
    {
        public override PlayerEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected StartArray for PlayerEntry.");

            reader.Read();
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected name string.");
            var name = reader.GetString() ?? "";

            reader.Read();
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected game string.");
            var game = reader.GetString() ?? "";

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) { }

            return new PlayerEntry { Name = name, Game = game };
        }

        public override void Write(Utf8JsonWriter writer, PlayerEntry value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteStringValue(value.Name);
            writer.WriteStringValue(value.Game);
            writer.WriteEndArray();
        }
    }

    public sealed class Rfc1123DateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
    {
        public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected string for DateTimeOffset.");

            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return null;

            if (DateTimeOffset.TryParseExact(
                    s, "r", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                return dto;

            if (DateTimeOffset.TryParse(s, out dto)) return dto;

            throw new JsonException($"Invalid date: {s}");
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
        {
            if (value is null) { writer.WriteNullValue(); return; }
            writer.WriteStringValue(value.Value.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture));
        }
    }

    // -------- tracker -> checksums --------
    public static class TrackerDatapackageFetcher
    {
        public static async Task<IDictionary<string, string>> GetDatapackageChecksumsAsync(
            string baseUrl, string trackerId)
        {
            var url = $"{baseUrl.TrimEnd('/')}/api/tracker/{trackerId}";
            using var http = new HttpClient();

            var json = await http.GetStringAsync(url);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var root = JsonSerializer.Deserialize<TrackerRoot>(json, options)
                       ?? throw new InvalidOperationException("Empty tracker payload");

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
    }

    // -------- /api/datapackage/<checksum> --------
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
            public Dictionary<int, string> LocationIdToName { get; } = new();
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

        public static async Task<Dictionary<string, DatapackageIndex>> FetchManyAsync(
            string baseUrl,
            IEnumerable<string> checksums,
            int maxConcurrency = 6,
            CancellationToken ct = default)
        {
            var result = new Dictionary<string, DatapackageIndex>(StringComparer.OrdinalIgnoreCase);
            using var http = new HttpClient();
            using var sem = new SemaphoreSlim(maxConcurrency);

            var tasks = new List<Task>();
            foreach (var sum in checksums)
            {
                await sem.WaitAsync(ct);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var idx = await FetchOneAsync(baseUrl, sum, http, ct);
                        lock (result) result[sum] = idx;
                    }
                    finally
                    {
                        sem.Release();
                    }
                }, ct));
            }
            await Task.WhenAll(tasks);
            return result;
        }
    }

    // -------- /api/tracker/<tracker_id> + enrichissement --------
    public static class TrackerItemsEnricher
    {
        public sealed class TrackerRoot
        {
            [JsonPropertyName("player_items_received")]
            public List<TeamItemsReceived> PlayerItemsReceived { get; set; } = new();

            [JsonPropertyName("datapackage")]
            public Dictionary<string, PackageInfo>? DataPackage { get; set; }
        }

        public sealed class PackageInfo
        {
            [JsonPropertyName("checksum")] public string? Checksum { get; set; }
            [JsonPropertyName("version")] public int Version { get; set; }
        }

        public sealed class TeamItemsReceived
        {
            [JsonPropertyName("team")] public int Team { get; set; }
            [JsonPropertyName("players")] public List<PlayerItemsReceived> Players { get; set; } = new();
        }

        public sealed class PlayerItemsReceived
        {
            [JsonPropertyName("player")] public int Player { get; set; }
            [JsonPropertyName("items")] public List<NetworkItem> Items { get; set; } = new();
        }

        [JsonConverter(typeof(NetworkItemConverter))]
        public sealed class NetworkItem
        {
            public int Item { get; set; }
            public int Location { get; set; }
            public int FromPlayer { get; set; }
            public int Flags { get; set; }
        }

        public sealed class NetworkItemConverter : JsonConverter<NetworkItem>
        {
            public override NetworkItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                // Supporte soit un tableau [item, location, player, flags], soit un objet { item, location, player, flags }
                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    // [ itemId, locationId, player, flags ]
                    reader.Read();
                    var item = ReadInt(ref reader);
                    reader.Read();
                    var location = ReadInt(ref reader);
                    reader.Read();
                    var fromPlayer = ReadInt(ref reader);
                    reader.Read();
                    var flags = ReadInt(ref reader);

                    // consume EndArray
                    while (reader.TokenType != JsonTokenType.EndArray && reader.Read()) { }
                    return new NetworkItem { Item = item, Location = location, FromPlayer = fromPlayer, Flags = flags };
                }
                else if (reader.TokenType == JsonTokenType.StartObject)
                {
                    int item = 0, location = 0, fromPlayer = 0, flags = 0;

                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType != JsonTokenType.PropertyName)
                            throw new JsonException("Expected property name in NetworkItem object.");
                        var name = reader.GetString();
                        reader.Read();

                        switch (name)
                        {
                            case "item":
                                item = ReadInt(ref reader);
                                break;
                            case "location":
                                location = ReadInt(ref reader);
                                break;
                            case "player":
                                fromPlayer = ReadInt(ref reader);
                                break;
                            case "flags":
                                flags = ReadInt(ref reader);
                                break;
                            default:
                                // skip unknown
                                SkipValue(ref reader);
                                break;
                        }
                    }
                    return new NetworkItem { Item = item, Location = location, FromPlayer = fromPlayer, Flags = flags };
                }

                throw new JsonException("NetworkItem must be an array or object.");
            }

            public override void Write(Utf8JsonWriter writer, NetworkItem value, JsonSerializerOptions options)
            {
                // On réécrit au format tableau par défaut
                writer.WriteStartArray();
                writer.WriteNumberValue(value.Item);
                writer.WriteNumberValue(value.Location);
                writer.WriteNumberValue(value.FromPlayer);
                writer.WriteNumberValue(value.Flags);
                writer.WriteEndArray();
            }

            private static int ReadInt(ref Utf8JsonReader reader)
            {
                return reader.TokenType switch
                {
                    JsonTokenType.Number => reader.TryGetInt32(out var n) ? n : (int)reader.GetInt64(),
                    JsonTokenType.String => int.TryParse(reader.GetString(), out var n) ? n : 0,
                    _ => 0
                };
            }

            private static void SkipValue(ref Utf8JsonReader reader)
            {
                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                {
                    int depth = 0;
                    do
                    {
                        if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                            depth++;
                        else if (reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
                            depth--;
                    } while (reader.Read() && depth > 0);
                }
                else
                {
                    // simple value already positioned
                }
            }
        }

        public sealed class EnrichedTeamItems
        {
            public int Team { get; set; }
            public List<EnrichedPlayerItems> Players { get; set; } = new();
        }

        public sealed class EnrichedPlayerItems
        {
            public int Player { get; set; }      // slot
            public string Game { get; set; } = "";
            public List<EnrichedItem> Items { get; set; } = new();
        }

        public sealed class EnrichedItem
        {
            public int ItemId { get; set; }
            public string ItemName { get; set; } = "";
            public int LocationId { get; set; }
            public string LocationName { get; set; } = "";

            public int FromPlayerId { get; set; }
            public string FromPlayerName { get; set; } = "";
            public string FromPlayerGame { get; set; } = ""; 

            public int ToPlayerId { get; set; }
            public string ToPlayerName { get; set; } = "";
            public string ToPlayerGame { get; set; } = "";  

            public int Flags { get; set; }
        }

        public sealed class RoomPlayer
        {
            public string Name { get; set; } = "";
            public string Game { get; set; } = "";
        }

        public sealed class DatapackageIndex
        {
            public Dictionary<int, string> ItemIdToName { get; } = new();
            public Dictionary<int, string> LocationIdToName { get; } = new();
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public static async Task<List<EnrichedTeamItems>> FetchAndEnrichAsync(
            string baseUrl,
            string trackerId,
            IList<RoomPlayer> roomPlayers,
            IDictionary<string, DatapackageIndex> perGameIndexes)
        {
            var url = $"{baseUrl.TrimEnd('/')}/api/tracker/{trackerId}";
            using var http = new HttpClient();
            var json = await http.GetStringAsync(url);

            var root = JsonSerializer.Deserialize<TrackerRoot>(json, JsonOpts)
                       ?? throw new InvalidOperationException("Empty tracker payload");

            var enrichedTeams = new List<EnrichedTeamItems>();

            foreach (var teamBlock in root.PlayerItemsReceived)
            {
                var outTeam = new EnrichedTeamItems { Team = teamBlock.Team };

                foreach (var p in teamBlock.Players)
                {
                    var slot = p.Player; // 1-based
                    var idx = slot - 1;
                    var game = (idx >= 0 && idx < roomPlayers.Count) ? roomPlayers[idx].Game : "";

                    var outPlayer = new EnrichedPlayerItems
                    {
                        Player = slot,
                        Game = game
                    };

                    var itemIdx = perGameIndexes.TryGetValue(game, out var gi) ? gi.ItemIdToName : null; // jeu du receveur

                    foreach (var it in p.Items)
                    {
                        // --- ItemName avec le jeu du receveur
                        var itemName = (itemIdx != null && itemIdx.TryGetValue(it.Item, out var iname))
                            ? iname : it.Item.ToString();

                        // --- Finder (donneur)
                        var finderIdx = it.FromPlayer - 1;
                        string fromName = $"Player{it.FromPlayer}";
                        string fromGame = "";
                        if (finderIdx >= 0 && finderIdx < roomPlayers.Count)
                        {
                            fromName = roomPlayers[finderIdx].Name;
                            fromGame = roomPlayers[finderIdx].Game;
                        }

                        // --- LocationName avec le jeu du donneur, fallback sur le jeu du receveur
                        Dictionary<int, string>? locIdx = null;
                        if (!string.IsNullOrEmpty(fromGame) && perGameIndexes.TryGetValue(fromGame, out var glFrom))
                            locIdx = glFrom.LocationIdToName;

                        string locationName =
                            (locIdx != null && locIdx.TryGetValue(it.Location, out var lname)) ? lname :
                            (perGameIndexes.TryGetValue(game, out var glRecv) && glRecv.LocationIdToName.TryGetValue(it.Location, out var lnameRecv)) ? lnameRecv :
                            it.Location.ToString();

                        // --- Receiver (receveur courant)
                        string toName = $"Player{slot}";
                        string toGame = game; // pl.Game
                        if (idx >= 0 && idx < roomPlayers.Count)
                            toName = roomPlayers[idx].Name;

                        outPlayer.Items.Add(new EnrichedItem
                        {
                            ItemId = it.Item,
                            ItemName = itemName,
                            LocationId = it.Location,
                            LocationName = locationName,
                            FromPlayerId = it.FromPlayer,
                            FromPlayerName = fromName,
                            FromPlayerGame = fromGame,
                            ToPlayerId = slot,
                            ToPlayerName = toName,
                            ToPlayerGame = toGame,
                            Flags = it.Flags
                        });
                    }

                    outTeam.Players.Add(outPlayer);
                }

                enrichedTeams.Add(outTeam);
            }

            return enrichedTeams;
        }
    }
}
