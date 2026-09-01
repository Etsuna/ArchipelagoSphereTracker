using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ArchipelagoSphereTracker.src.TrackerLib.Services
{
    public static class TrackerStreamParser
    {
        // ---------- Items ----------
        public static List<DisplayedItem> ParseItems(ProcessingContext ctx, string json)
        {
            var list = new List<DisplayedItem>(256);
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json),
                new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

            if (!MoveToProperty(ref reader, "player_items_received", JsonTokenType.StartArray))
                return list;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) break;
                if (reader.TokenType != JsonTokenType.StartObject) { SkipValue(ref reader); continue; }

                int receiverSlot = 0;
                List<(long itemId, long locId, int from, int flags)>? buf = null;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) { SkipValue(ref reader); continue; }
                    var prop = reader.GetString(); reader.Read();

                    if (prop == "player")
                    {
                        receiverSlot = ReadInt(ref reader);
                    }
                    else if (prop == "items" && reader.TokenType == JsonTokenType.StartArray)
                    {
                        buf ??= new List<(long, long, int, int)>(16);
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.EndArray) break;
                            if (reader.TokenType != JsonTokenType.StartArray) { SkipValue(ref reader); continue; }

                            reader.Read(); var itemId = ReadInt64(ref reader);
                            reader.Read(); var locId = ReadInt64(ref reader);
                            reader.Read(); var from = ReadInt(ref reader);
                            reader.Read(); var flags = ReadInt(ref reader);
                            while (reader.TokenType != JsonTokenType.EndArray && reader.Read()) { }

                            buf.Add((itemId, locId, from, flags));
                        }
                    }
                    else
                    {
                        SkipValue(ref reader);
                    }
                }

                if (receiverSlot >= 0 && buf is { Count: > 0 })
                {
                    var receiverAlias = ctx.SlotAlias(receiverSlot);
                    var receiverGame = ctx.SlotGame(receiverSlot);

                    foreach (var (itemId, locId, from, flags) in buf)
                    {
                        var (finderAlias, finderGame) = ctx.SlotAliasGame(from);

                        var itemName = ctx.TryGetItemName(receiverGame, itemId, out var iname) ? iname : itemId.ToString();
                        var locName = ctx.TryGetLocationName(finderGame, locId, out var lname) ? lname : locId.ToString();

                        list.Add(new DisplayedItem
                        {
                            FinderSlot = from,
                            ReceiverSlot = receiverSlot,
                            ItemId = itemId,
                            LocationId = locId,
                            Finder = finderAlias,
                            Receiver = receiverAlias,
                            Item = itemName,
                            Location = locName,
                            Game = finderGame,
                            Flag = flags.ToString()
                        });
                    }
                }
            }
            return list;
        }

        // ---------- Hints ----------
        public static List<HintStatus> ParseHints(ProcessingContext ctx, string json)
        {
            var list = new List<HintStatus>(256);
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json),
                new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

            if (!MoveToProperty(ref reader, "hints", JsonTokenType.StartArray))
                return list;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) break;
                if (reader.TokenType != JsonTokenType.StartObject) { SkipValue(ref reader); continue; }

                int receiverSlot = 0;
                List<(int from, int to, long locId, long itemId, bool found, string ent, int itemFlags, int status)>? buf = null;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) { SkipValue(ref reader); continue; }
                    var prop = reader.GetString(); reader.Read();

                    if (prop == "player")
                    {
                        receiverSlot = ReadInt(ref reader);
                    }
                    else if (prop == "hints" && reader.TokenType == JsonTokenType.StartArray)
                    {
                        buf ??= new List<(int, int, long, long, bool, string, int, int)>(16);
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.EndArray) break;
                            if (reader.TokenType != JsonTokenType.StartArray) { SkipValue(ref reader); continue; }

                            reader.Read(); var from = ReadInt(ref reader);
                            reader.Read(); var to = ReadInt(ref reader);
                            reader.Read(); var locId = ReadInt64(ref reader);
                            reader.Read(); var itemId = ReadInt64(ref reader);
                            reader.Read(); var found = ReadBool(ref reader);
                            reader.Read(); var ent = ReadString(ref reader);
                            var itemFlags = 0;
                            var status = 0;
                            if (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                itemFlags = ReadInt(ref reader);
                                if (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                                    status = ReadInt(ref reader);
                            }
                            while (reader.TokenType != JsonTokenType.EndArray && reader.Read()) { }

                            buf.Add((from, to, locId, itemId, found, ent, itemFlags, status));
                        }
                    }
                    else
                    {
                        SkipValue(ref reader);
                    }
                }

                if (receiverSlot >= 0 && buf is { Count: > 0 })
                {
                    foreach (var (from, to, locId, itemId, found, ent, itemFlags, status) in buf)
                    {
                        if (to != receiverSlot) continue;

                        var (finderAlias, finderGame) = ctx.SlotAliasGame(to);
                        var receiverAlias = ctx.SlotAlias(from);
                        var receiverGame = ctx.SlotGame(from);

                        var itemName = ctx.TryGetItemName(receiverGame, itemId, out var iname) ? iname : itemId.ToString();
                        var locName = ctx.TryGetLocationName(finderGame, locId, out var lname) ? lname : locId.ToString();

                        var entrance = string.IsNullOrWhiteSpace(ent) ? "Vanilla" : ent;

                        list.Add(new HintStatus
                        {
                            FinderSlot = to,
                            ReceiverSlot = from,
                            ItemId = itemId,
                            LocationId = locId,
                            Finder = finderAlias,
                            Receiver = receiverAlias,
                            Item = itemName,
                            Location = locName,
                            Game = finderGame,
                            Entrance = entrance,
                            Flag = found ? "True" : "False",
                            ItemFlags = itemFlags,
                            Status = status
                        });
                    }
                }
            }
            return list;
        }

        public static List<GameStatus> ParseGameStatus(ProcessingContext ctx, string json, IReadOnlyDictionary<int, int> totalsBySlot)
        {
            var list = new List<GameStatus>(64);

            var activityBySlot = ParseActivityTimersMap(json);
            var foundBySlot = ParseChecksDoneCountsMap(json);

            for (int slot = 1; slot <= ctx.SlotIndex.Count; slot++)
            {
                var alias = ctx.SlotAlias(slot) ?? string.Empty;
                var game = ctx.SlotGame(slot) ?? string.Empty;
                var last = activityBySlot.TryGetValue(slot, out var t) ? t : null;
                var found = foundBySlot.TryGetValue(slot, out var c) ? c : 0;
                var total = totalsBySlot.TryGetValue(slot, out var configuredTotal) ? configuredTotal : 0;

                list.Add(new GameStatus
                {
                    Slot = slot,
                    Name = alias,
                    Game = game,
                    Checks = found.ToString(CultureInfo.InvariantCulture),
                    Total = total.ToString(CultureInfo.InvariantCulture),
                    LastActivity = last ?? string.Empty
                });
            }

            return list;
        }

        public static Dictionary<int, int> ParsePlayerLocationTotals(string jsonStatic)
        {
            var totalsBySlot = new Dictionary<int, int>();
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonStatic),
                new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

            if (!MoveToProperty(ref reader, "player_locations_total", JsonTokenType.StartArray))
                return totalsBySlot;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) break;
                if (reader.TokenType != JsonTokenType.StartObject) { SkipValue(ref reader); continue; }

                int slot = 0;
                int total = 0;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) { SkipValue(ref reader); continue; }
                    var prop = reader.GetString();
                    reader.Read();

                    if (prop == "player") slot = ReadInt(ref reader);
                    else if (prop == "total_locations") total = ReadInt(ref reader);
                    else SkipValue(ref reader);
                }

                if (slot > 0)
                    totalsBySlot[slot] = total;
            }

            return totalsBySlot;
        }

        public static Dictionary<int, IReadOnlyList<long>> ParseCheckedLocations(string json)
        {
            var result = new Dictionary<int, IReadOnlyList<long>>();
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json),
                new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

            if (!MoveToProperty(ref reader, "player_checks_done", JsonTokenType.StartArray))
                return result;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) break;
                if (reader.TokenType != JsonTokenType.StartObject) { SkipValue(ref reader); continue; }

                int slot = 0;
                List<long>? locations = null;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) { SkipValue(ref reader); continue; }
                    var property = reader.GetString();
                    reader.Read();

                    if (property == "player")
                    {
                        slot = ReadInt(ref reader);
                    }
                    else if (property == "locations" && reader.TokenType == JsonTokenType.StartArray)
                    {
                        locations = new List<long>();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            if (reader.TokenType is JsonTokenType.Number or JsonTokenType.String)
                                locations.Add(ReadInt64(ref reader));
                            else
                                SkipValue(ref reader);
                        }
                    }
                    else
                    {
                        SkipValue(ref reader);
                    }
                }

                if (slot > 0 && locations != null)
                    result[slot] = locations.Distinct().Order().ToArray();
            }

            return result;
        }

        public static Dictionary<int, string?> ParsePlayerAliases(string json)
        {
            using var document = JsonDocument.Parse(json);
            var result = new Dictionary<int, string?>();
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("aliases", out var aliases) ||
                aliases.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var entry in aliases.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !entry.TryGetProperty("player", out var playerElement) ||
                    !TryReadElementInt(playerElement, out var player) ||
                    player <= 0)
                {
                    continue;
                }

                var alias = entry.TryGetProperty("alias", out var aliasElement) && aliasElement.ValueKind == JsonValueKind.String
                    ? aliasElement.GetString()
                    : null;
                result[player] = string.IsNullOrWhiteSpace(alias) ? null : alias;
            }

            return result;
        }

        public static Dictionary<int, int> ParsePlayerStatuses(string json)
        {
            using var document = JsonDocument.Parse(json);
            var result = new Dictionary<int, int>();
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("player_status", out var statuses) ||
                statuses.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var entry in statuses.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !entry.TryGetProperty("player", out var playerElement) ||
                    !TryReadElementInt(playerElement, out var player) ||
                    player <= 0 ||
                    !entry.TryGetProperty("status", out var statusElement) ||
                    !TryReadElementInt(statusElement, out var status))
                {
                    continue;
                }

                result[player] = status;
            }

            return result;
        }

        private static Dictionary<int, int> ParseChecksDoneCountsMap(string json)
        {
            return ParseCheckedLocations(json)
                .ToDictionary(entry => entry.Key, entry => entry.Value.Count);
        }

        // ---------- helpers JSON ----------
        private static bool MoveToProperty(ref Utf8JsonReader r, string name, JsonTokenType expectStart)
        {
            while (r.Read())
            {
                if (r.TokenType == JsonTokenType.PropertyName && r.ValueTextEquals(name))
                {
                    r.Read();
                    return r.TokenType == expectStart;
                }
            }
            return false;
        }

        private static void SkipValue(ref Utf8JsonReader r)
        {
            if (r.TokenType != JsonTokenType.StartObject && r.TokenType != JsonTokenType.StartArray) return;
            int depth = 0;
            do
            {
                if (r.TokenType == JsonTokenType.StartObject || r.TokenType == JsonTokenType.StartArray) depth++;
                else if (r.TokenType == JsonTokenType.EndObject || r.TokenType == JsonTokenType.EndArray) depth--;
            } while (r.Read() && depth > 0);
        }

        private static int ReadInt(ref Utf8JsonReader r)
            => r.TokenType switch
            {
                JsonTokenType.Number => r.TryGetInt32(out var n) ? n : (int)r.GetInt64(),
                JsonTokenType.String => int.TryParse(r.GetString(), out var n) ? n : 0,
                _ => 0
            };

        private static long ReadInt64(ref Utf8JsonReader r)
            => r.TokenType switch
            {
                JsonTokenType.Number => r.GetInt64(),
                JsonTokenType.String => long.TryParse(r.GetString(), out var n) ? n : 0L,
                _ => 0L
            };

        private static bool TryReadElementInt(JsonElement element, out int value)
        {
            if (element.ValueKind == JsonValueKind.Number)
                return element.TryGetInt32(out value);
            if (element.ValueKind == JsonValueKind.String)
                return int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

            value = 0;
            return false;
        }

        private static bool ReadBool(ref Utf8JsonReader r)
            => r.TokenType switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.Number => r.TryGetInt32(out var n) && n != 0,
                JsonTokenType.String => bool.TryParse(r.GetString(), out var b) ? b : r.GetString() == "1",
                _ => false
            };

        private static string ReadString(ref Utf8JsonReader r)
            => r.TokenType == JsonTokenType.String ? r.GetString() ?? "" : "";

        private static Dictionary<int, string?> ParseActivityTimersMap(string json)
        {
            var map = new Dictionary<int, string?>(64);
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json),
                new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

            if (!MoveToProperty(ref reader, "activity_timers", JsonTokenType.StartArray))
                return map;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) break;
                if (reader.TokenType != JsonTokenType.StartObject) { SkipValue(ref reader); continue; }

                int slot = 0;
                string? time = null;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) { SkipValue(ref reader); continue; }
                    var prop = reader.GetString(); reader.Read();

                    if (prop == "player")
                    {
                        slot = ReadInt(ref reader);
                    }
                    else if (prop == "time")
                    {
                        if (reader.TokenType == JsonTokenType.Null)
                            time = null;
                        else
                            time = ReadString(ref reader);
                    }
                    else
                    {
                        SkipValue(ref reader);
                    }
                }

                if (slot >= 0)
                    map[slot] = string.IsNullOrEmpty(time) ? null : time;
            }

            return map;
        }
    }
}
