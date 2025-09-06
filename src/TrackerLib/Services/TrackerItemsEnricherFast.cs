using System.Text.Json;

namespace TrackerLib.Services
{
    public static class TrackerItemsEnricherFast
    {
        public static List<DisplayedItem> Enrich(ProcessingContext ctx, string json)
        {
            var list = new List<DisplayedItem>(256);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("player_items_received", out var teams)
                || teams.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var team in teams.EnumerateArray())
            {
                if (!team.TryGetProperty("players", out var players) || players.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var p in players.EnumerateArray())
                {
                    if (!p.TryGetProperty("player", out var playerProp)) continue;
                    int receiverSlot = playerProp.GetInt32();

                    string receiverAlias = receiverSlot - 1 >= 0 && receiverSlot - 1 < ctx.SlotIndex.Count
                        ? ctx.SlotIndex[receiverSlot - 1].Alias
                        : $"Player{receiverSlot}";

                    if (!p.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var it in items.EnumerateArray())
                    {
                        if (it.ValueKind != JsonValueKind.Array) continue;
                        var arr = it.EnumerateArray();

                        long itemId = NextInt64(ref arr);
                        long locId = NextInt64(ref arr);
                        int from = NextInt(ref arr);
                        int flags = NextInt(ref arr);

                        string finderAlias, finderGame;
                        if (from - 1 >= 0 && from - 1 < ctx.SlotIndex.Count)
                        {
                            var tup = ctx.SlotIndex[from - 1];
                            finderAlias = tup.Alias;
                            finderGame = tup.Game;
                        }
                        else { finderAlias = $"Player{from}"; finderGame = ""; }

                        string itemName = ctx.ItemIdToName.TryGetValue(itemId, out var iname) ? iname : itemId.ToString();
                        string locName = ctx.LocationIdToName.TryGetValue(locId, out var lname) ? lname : locId.ToString();

                        list.Add(new DisplayedItem
                        {
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

            static int NextInt(ref JsonElement.ArrayEnumerator e) => e.MoveNext() ? (e.Current.ValueKind == JsonValueKind.Number ? e.Current.GetInt32() : int.TryParse(e.Current.GetString(), out var n) ? n : 0) : 0;
            static long NextInt64(ref JsonElement.ArrayEnumerator e) => e.MoveNext() ? (e.Current.ValueKind == JsonValueKind.Number ? e.Current.GetInt64() : long.TryParse(e.Current.GetString(), out var n) ? n : 0L) : 0L;
        }
    }

    public static class TrackerHintsEnricherFast
    {
        public static List<HintStatus> Enrich(ProcessingContext ctx, string json)
        {
            var list = new List<HintStatus>(256);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("hints", out var teams)
                || teams.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var team in teams.EnumerateArray())
            {
                if (!team.TryGetProperty("players", out var players) || players.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var p in players.EnumerateArray())
                {
                    if (!p.TryGetProperty("player", out var playerProp)) continue;
                    int receiverSlot = playerProp.GetInt32();

                    if (!p.TryGetProperty("hints", out var hints) || hints.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var h in hints.EnumerateArray())
                    {
                        if (h.ValueKind != JsonValueKind.Array) continue;
                        var arr = h.EnumerateArray();

                        // [ fromPlayer, toPlayer, location, item, found, entrance ]
                        int from = NextInt(ref arr);
                        int to = NextInt(ref arr);
                        long locId = NextInt64(ref arr);
                        long itemId = NextInt64(ref arr);
                        bool found = NextBool(ref arr);
                        string ent = NextString(ref arr);

                        if (to != receiverSlot) continue;

                        string finderAlias, finderGame, receiverAlias;
                        if (from - 1 >= 0 && from - 1 < ctx.SlotIndex.Count)
                        {
                            var tup = ctx.SlotIndex[from - 1];
                            finderAlias = tup.Alias;
                            finderGame = tup.Game;
                        }
                        else { finderAlias = $"Player{from}"; finderGame = ""; }

                        receiverAlias = to - 1 >= 0 && to - 1 < ctx.SlotIndex.Count
                            ? ctx.SlotIndex[to - 1].Alias
                            : $"Player{to}";

                        string itemName = ctx.ItemIdToName.TryGetValue(itemId, out var iname) ? iname : itemId.ToString();
                        string locName = ctx.LocationIdToName.TryGetValue(locId, out var lname) ? lname : locId.ToString();
                        string entrance = string.IsNullOrWhiteSpace(ent) ? "Vanilla" : ent;

                        list.Add(new HintStatus
                        {
                            Finder = finderAlias,
                            Receiver = receiverAlias,
                            Item = itemName,
                            Location = locName,
                            Game = finderGame,
                            Entrance = entrance,
                            Flag = found.ToString()
                        });
                    }
                }
            }
            return list;

            static int NextInt(ref JsonElement.ArrayEnumerator e) => e.MoveNext() ? (e.Current.ValueKind == JsonValueKind.Number ? e.Current.GetInt32() : int.TryParse(e.Current.GetString(), out var n) ? n : 0) : 0;
            static long NextInt64(ref JsonElement.ArrayEnumerator e) => e.MoveNext() ? (e.Current.ValueKind == JsonValueKind.Number ? e.Current.GetInt64() : long.TryParse(e.Current.GetString(), out var n) ? n : 0L) : 0L;
            static bool NextBool(ref JsonElement.ArrayEnumerator e)
                => e.MoveNext() && (e.Current.ValueKind == JsonValueKind.True
                                    || (e.Current.ValueKind == JsonValueKind.String && bool.TryParse(e.Current.GetString(), out var b) && b)
                                    || (e.Current.ValueKind == JsonValueKind.Number && e.Current.TryGetInt32(out var i) && i != 0));
            static string NextString(ref JsonElement.ArrayEnumerator e)
                => e.MoveNext() ? (e.Current.ValueKind == JsonValueKind.String ? e.Current.GetString() ?? "" : e.Current.ToString()) : "";
        }
    }
}
