using System.Data.SQLite;

namespace TrackerLib.Services
{
    public sealed class ProcessingContext
    {
        public string GuildId { get; init; } = "";
        public string ChannelId { get; init; } = "";
        public bool Silent { get; init; }

        // slot (1-based) -> (Alias, Game)
        public List<(string Alias, string Game)> SlotIndex { get; } = new();

        // id -> name (tous jeux du salon)
        public Dictionary<long, string> ItemIdToName { get; } = new();
        public Dictionary<long, string> LocationIdToName { get; } = new();
    }

    public static class ProcessingContextLoader
    {
        public static async Task<ProcessingContext> LoadAsync(string guildId, string channelId, bool silent)
        {
            var ctx = new ProcessingContext { GuildId = guildId, ChannelId = channelId, Silent = silent };

            await using var cn = await Db.OpenReadAsync().ConfigureAwait(false);

            // 1) Slots -> alias/game
            {
                const string sql = @"SELECT Slot, Alias, Game
                                     FROM AliasChoicesTable
                                     WHERE GuildId=@G AND ChannelId=@C
                                     ORDER BY Slot;";
                await using var cmd = new SQLiteCommand(sql, cn);
                cmd.Parameters.AddWithValue("@G", guildId);
                cmd.Parameters.AddWithValue("@C", channelId);

                var rows = new List<(int Slot, string Alias, string Game)>();
                await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await r.ReadAsync().ConfigureAwait(false))
                {
                    var slot = (int)((r["Slot"] is long L) ? L : Convert.ToInt64(r["Slot"]));
                    var alias = r["Alias"]?.ToString() ?? $"Player{slot}";
                    var game = r["Game"]?.ToString() ?? "";
                    rows.Add((slot, alias, game));
                }

                if (rows.Count > 0)
                {
                    var maxSlot = rows[^1].Slot;
                    ctx.SlotIndex.Capacity = maxSlot;
                    for (int i = 1; i <= maxSlot; i++) ctx.SlotIndex.Add(($"Player{i}", ""));
                    foreach (var row in rows) ctx.SlotIndex[row.Slot - 1] = (row.Alias, row.Game);
                }
            }

            // 2) Games actifs -> dataset keys
            var games = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, game) in ctx.SlotIndex) if (!string.IsNullOrEmpty(game)) games.Add(game);

            var gameToDataset = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (games.Count > 0)
            {
                const string mapSql = @"SELECT GameName, DatasetKey
                                        FROM DatapackageGameMap
                                        WHERE GuildId=@G AND ChannelId=@C;";
                await using var mapCmd = new SQLiteCommand(mapSql, cn);
                mapCmd.Parameters.AddWithValue("@G", guildId);
                mapCmd.Parameters.AddWithValue("@C", channelId);
                await using var mr = await mapCmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await mr.ReadAsync().ConfigureAwait(false))
                {
                    var g = mr["GameName"]?.ToString();
                    var ds = mr["DatasetKey"]?.ToString();
                    if (!string.IsNullOrEmpty(g) && !string.IsNullOrEmpty(ds))
                        gameToDataset[g] = ds;
                }
            }

            // 3) id->name Items
            if (gameToDataset.Count > 0)
            {
                const string sqlItems = @"SELECT Id, Name
                                          FROM DatapackageItems
                                          WHERE GuildId=@G AND ChannelId=@C AND DatasetKey=@D;";
                await using var cmdI = new SQLiteCommand(sqlItems, cn);
                cmdI.Parameters.AddWithValue("@G", guildId);
                cmdI.Parameters.AddWithValue("@C", channelId);
                var pD1 = cmdI.Parameters.Add("@D", System.Data.DbType.String);

                foreach (var g in games)
                {
                    if (!gameToDataset.TryGetValue(g, out var ds)) continue;
                    pD1.Value = ds;
                    await using var r = await cmdI.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await r.ReadAsync().ConfigureAwait(false))
                    {
                        var id = (r["Id"] is long L) ? L : Convert.ToInt64(r["Id"]);
                        var name = r["Name"]?.ToString() ?? "";
                        ctx.ItemIdToName[id] = name;
                    }
                }

                // 4) id->name Locations
                const string sqlLocs = @"SELECT Id, Name
                                         FROM DatapackageLocations
                                         WHERE GuildId=@G AND ChannelId=@C AND DatasetKey=@D;";
                await using var cmdL = new SQLiteCommand(sqlLocs, cn);
                cmdL.Parameters.AddWithValue("@G", guildId);
                cmdL.Parameters.AddWithValue("@C", channelId);
                var pD2 = cmdL.Parameters.Add("@D", System.Data.DbType.String);

                foreach (var g in games)
                {
                    if (!gameToDataset.TryGetValue(g, out var ds)) continue;
                    pD2.Value = ds;
                    await using var r = await cmdL.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await r.ReadAsync().ConfigureAwait(false))
                    {
                        var id = (r["Id"] is long L) ? L : Convert.ToInt64(r["Id"]);
                        var name = r["Name"]?.ToString() ?? "";
                        ctx.LocationIdToName[id] = name;
                    }
                }
            }

            return ctx;
        }
    }
}
