using System.Data.SQLite;

namespace TrackerLib.Services
{
    // Contexte runtime pour un guild/channel donné
    public sealed class ProcessingContext
    {
        public string GuildId { get; init; } = "";
        public string ChannelId { get; init; } = "";
        public bool Silent { get; init; }

        public List<(string Alias, string Game)> SlotIndex { get; } = new();

        private readonly Dictionary<string, string> _gameToDataset =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Dictionary<long, string>> _itemsByDataset =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<long, string>> _locationsByDataset =
            new(StringComparer.OrdinalIgnoreCase);

        public void SetGameDataset(string game, string datasetKey)
        {
            if (!string.IsNullOrWhiteSpace(game) && !string.IsNullOrWhiteSpace(datasetKey))
                _gameToDataset[game] = datasetKey;
        }

        public void SetDatasetItems(string datasetKey, IEnumerable<(long Id, string Name)> rows)
        {
            if (string.IsNullOrWhiteSpace(datasetKey)) return;
            if (!_itemsByDataset.TryGetValue(datasetKey, out var dict))
                _itemsByDataset[datasetKey] = dict = new Dictionary<long, string>();
            foreach (var (id, name) in rows) dict[id] = name ?? "";
        }

        public void SetDatasetLocations(string datasetKey, IEnumerable<(long Id, string Name)> rows)
        {
            if (string.IsNullOrWhiteSpace(datasetKey)) return;
            if (!_locationsByDataset.TryGetValue(datasetKey, out var dict))
                _locationsByDataset[datasetKey] = dict = new Dictionary<long, string>();
            foreach (var (id, name) in rows) dict[id] = name ?? "";
        }


        public string SlotAlias(int slot)
            => (slot > 0 && slot - 1 < SlotIndex.Count)
               ? SlotIndex[slot - 1].Alias
               : $"Player{Math.Max(1, slot)}";

        public (string Alias, string Game) SlotAliasGame(int slot)
            => (slot > 0 && slot - 1 < SlotIndex.Count)
               ? SlotIndex[slot - 1]
               : ($"Player{Math.Max(1, slot)}", "");

        public string SlotGame(int slot)
            => (slot > 0 && slot - 1 < SlotIndex.Count)
               ? SlotIndex[slot - 1].Game
               : "";

        public bool TryGetItemName(string game, long itemId, out string name)
        {
            name = "";
            if (string.IsNullOrWhiteSpace(game)) return false;
            if (!_gameToDataset.TryGetValue(game, out var ds) || string.IsNullOrWhiteSpace(ds)) return false;
            return _itemsByDataset.TryGetValue(ds, out var dict) && dict.TryGetValue(itemId, out name);
        }

        public bool TryGetLocationName(string game, long locationId, out string name)
        {
            name = "";
            if (string.IsNullOrWhiteSpace(game)) return false;
            if (!_gameToDataset.TryGetValue(game, out var ds) || string.IsNullOrWhiteSpace(ds)) return false;
            return _locationsByDataset.TryGetValue(ds, out var dict) && dict.TryGetValue(locationId, out name);
        }
    }

    public static class ProcessingContextLoader
    {
        /// <summary>
        /// Charge 1) slots (AliasChoicesTable), 2) mapping Game→DatasetKey (DatapackageGameMap),
        /// 3) dictionnaires Items/Locations par Dataset depuis les tables Datapackage*.
        /// </summary>
        public static async Task<ProcessingContext> LoadOneShotAsync(string guildId, string channelId, bool silent)
        {
            var ctx = new ProcessingContext { GuildId = guildId, ChannelId = channelId, Silent = silent };

            await using var cn = await Db.OpenReadAsync().ConfigureAwait(false);

            var games = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                    if (!string.IsNullOrWhiteSpace(game)) games.Add(game);
                }

                if (rows.Count > 0)
                {
                    var maxSlot = rows[^1].Slot;
                    ctx.SlotIndex.Capacity = maxSlot;
                    for (int i = 1; i <= maxSlot; i++) ctx.SlotIndex.Add(($"Player{i}", ""));
                    foreach (var row in rows) ctx.SlotIndex[row.Slot - 1] = (row.Alias, row.Game);
                }
            }

            if (games.Count == 0) return ctx;

            var datasetKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            {
                const string sql = @"
                    SELECT GameName, DatasetKey
                    FROM DatapackageGameMap
                    WHERE GuildId=@G AND ChannelId=@C;";
                await using var cmd = new SQLiteCommand(sql, cn);
                cmd.Parameters.AddWithValue("@G", guildId);
                cmd.Parameters.AddWithValue("@C", channelId);
                await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await r.ReadAsync().ConfigureAwait(false))
                {
                    var game = r["GameName"]?.ToString() ?? "";
                    var ds = r["DatasetKey"]?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(game) || string.IsNullOrWhiteSpace(ds)) continue;
                    if (games.Contains(game))
                    {
                        ctx.SetGameDataset(game, ds);
                        datasetKeys.Add(ds);
                    }
                }
            }

            if (datasetKeys.Count == 0) return ctx;

            {
                const string sql = @"
                    SELECT i.DatasetKey, i.Id, i.Name
                    FROM DatapackageItems i
                    WHERE i.GuildId=@G AND i.ChannelId=@C
                      AND i.DatasetKey IN (SELECT DatasetKey FROM DatapackageGameMap WHERE GuildId=@G AND ChannelId=@C);";
                await using var cmd = new SQLiteCommand(sql, cn);
                cmd.Parameters.AddWithValue("@G", guildId);
                cmd.Parameters.AddWithValue("@C", channelId);

                var buffer = new Dictionary<string, List<(long, string)>>(StringComparer.OrdinalIgnoreCase);
                await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await r.ReadAsync().ConfigureAwait(false))
                {
                    var ds = r["DatasetKey"]?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(ds)) continue;
                    var id = (r["Id"] is long L) ? L : Convert.ToInt64(r["Id"]);
                    var name = r["Name"]?.ToString() ?? "";
                    if (!buffer.TryGetValue(ds, out var list)) buffer[ds] = list = new List<(long, string)>();
                    list.Add((id, name));
                }
                foreach (var (ds, rows) in buffer) ctx.SetDatasetItems(ds, rows);
            }

            {
                const string sql = @"
                    SELECT l.DatasetKey, l.Id, l.Name
                    FROM DatapackageLocations l
                    WHERE l.GuildId=@G AND l.ChannelId=@C
                      AND l.DatasetKey IN (SELECT DatasetKey FROM DatapackageGameMap WHERE GuildId=@G AND ChannelId=@C);";
                await using var cmd = new SQLiteCommand(sql, cn);
                cmd.Parameters.AddWithValue("@G", guildId);
                cmd.Parameters.AddWithValue("@C", channelId);

                var buffer = new Dictionary<string, List<(long, string)>>(StringComparer.OrdinalIgnoreCase);
                await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await r.ReadAsync().ConfigureAwait(false))
                {
                    var ds = r["DatasetKey"]?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(ds)) continue;
                    var id = (r["Id"] is long L) ? L : Convert.ToInt64(r["Id"]);
                    var name = r["Name"]?.ToString() ?? "";
                    if (!buffer.TryGetValue(ds, out var list)) buffer[ds] = list = new List<(long, string)>();
                    list.Add((id, name));
                }
                foreach (var (ds, rows) in buffer) ctx.SetDatasetLocations(ds, rows);
            }

            return ctx;
        }
    }
}
