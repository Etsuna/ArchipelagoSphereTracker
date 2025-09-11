using System.Text;
using System.Text.Json;

namespace TrackerLib.Services
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

                if (!MoveToProperty(ref reader, "players", JsonTokenType.StartArray))
                {
                    SkipObject(ref reader);
                    continue;
                }

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

                    if (receiverSlot > 0 && buf is { Count: > 0 })
                    {
                        var receiverAlias = ctx.SlotAlias(receiverSlot);
                        var receiverGame = ctx.SlotGame(receiverSlot);

                        foreach (var (itemId, locId, from, flags) in buf)
                        {
                            var (finderAlias, finderGame) = ctx.SlotAliasGame(from);

                            // 🔑 Item name → dataset du RECEIVER
                            var itemName = ctx.TryGetItemName(receiverGame, itemId, out var iname) ? iname : itemId.ToString();

                            // 🔑 Location name → dataset du FINDER
                            var locName = ctx.TryGetLocationName(finderGame, locId, out var lname) ? lname : locId.ToString();

                            list.Add(new DisplayedItem
                            {
                                Finder = finderAlias,
                                Receiver = receiverAlias,
                                Item = itemName,
                                Location = locName,
                                Game = finderGame,  // pour Items, on garde le jeu du finder (comme avant)
                                Flag = flags.ToString()
                            });
                        }
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

                if (!MoveToProperty(ref reader, "players", JsonTokenType.StartArray))
                {
                    SkipObject(ref reader);
                    continue;
                }

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray) break;
                    if (reader.TokenType != JsonTokenType.StartObject) { SkipValue(ref reader); continue; }

                    int receiverSlot = 0;
                    List<(int from, int to, long locId, long itemId, bool found, string ent)>? buf = null;

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
                            buf ??= new List<(int, int, long, long, bool, string)>(16);
                            while (reader.Read())
                            {
                                if (reader.TokenType == JsonTokenType.EndArray) break;
                                if (reader.TokenType != JsonTokenType.StartArray) { SkipValue(ref reader); continue; }

                                reader.Read(); var from = ReadInt(ref reader);   // FINDER
                                reader.Read(); var to = ReadInt(ref reader);   // RECEIVER
                                reader.Read(); var locId = ReadInt64(ref reader);
                                reader.Read(); var itemId = ReadInt64(ref reader);
                                reader.Read(); var found = ReadBool(ref reader);
                                reader.Read(); var ent = ReadString(ref reader);
                                while (reader.TokenType != JsonTokenType.EndArray && reader.Read()) { }

                                buf.Add((from, to, locId, itemId, found, ent));
                            }
                        }
                        else
                        {
                            SkipValue(ref reader);
                        }
                    }

                    if (receiverSlot > 0 && buf is { Count: > 0 })
                    {
                        foreach (var (from, to, locId, itemId, found, ent) in buf)
                        {
                            if (to != receiverSlot) continue;

                            // ✅ finder = from, receiver = to
                            var (finderAlias, finderGame) = ctx.SlotAliasGame(to);
                            var receiverAlias = ctx.SlotAlias(from);
                            var receiverGame = ctx.SlotGame(from);

                            // 🔑 Item = dataset du RECEIVER
                            var itemName = ctx.TryGetItemName(receiverGame, itemId, out var iname) ? iname : itemId.ToString();
                            // 🔑 Location = dataset du FINDER
                            var locName = ctx.TryGetLocationName(finderGame, locId, out var lname) ? lname : locId.ToString();

                            var entrance = string.IsNullOrWhiteSpace(ent) ? "Vanilla" : ent;

                            list.Add(new HintStatus
                            {
                                Finder = finderAlias,
                                Receiver = receiverAlias,
                                Item = itemName,
                                Location = locName,
                                Game = finderGame,             // Game = finder
                                Entrance = entrance,
                                Flag = found ? "True" : "False"
                            });
                        }
                    }
                }
            }
            return list;
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
        private static void SkipObject(ref Utf8JsonReader r)
        {
            if (r.TokenType != JsonTokenType.StartObject) return;
            int depth = 1;
            while (r.Read() && depth > 0)
            {
                if (r.TokenType == JsonTokenType.StartObject) depth++;
                else if (r.TokenType == JsonTokenType.EndObject) depth--;
            }
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
        private static bool ReadBool(ref Utf8JsonReader r)
            => r.TokenType switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.Number => r.TryGetInt32(out var n) && n != 0,
                JsonTokenType.String => bool.TryParse(r.GetString(), out var b) ? b : (r.GetString() == "1"),
                _ => false
            };
        private static string ReadString(ref Utf8JsonReader r)
            => r.TokenType == JsonTokenType.String ? (r.GetString() ?? "") : "";
    }
}
