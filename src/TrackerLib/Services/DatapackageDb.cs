using System.Data.SQLite;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrackerLib.Services
{
    public static class DatapackageDb
    {
        public static async Task<Dictionary<string, DatapackageClient.DatapackageIndex>>
            BuildPerGameFromDbAsync(string guildId, string channelId)
        {
            // 1) Jeux utilisés (via AliasChoicesTable)
            var games = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var cn = await Db.OpenReadAsync().ConfigureAwait(false))
            await using (var cmd = new SQLiteCommand(
                @"SELECT DISTINCT Game
              FROM AliasChoicesTable
              WHERE GuildId=@G AND ChannelId=@C AND ifnull(Game,'')<>'';", cn))
            {
                cmd.Parameters.AddWithValue("@G", guildId);
                cmd.Parameters.AddWithValue("@C", channelId);
                await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await r.ReadAsync().ConfigureAwait(false))
                    games.Add(r["Game"]?.ToString() ?? "");
            }

            // 2) Résoudre Game -> DatasetKey
            var gameToDataset = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await using (var cn = await Db.OpenReadAsync().ConfigureAwait(false))
            await using (var cmd = new SQLiteCommand(
                @"SELECT GameName, DatasetKey
              FROM DatapackageGameMap
              WHERE GuildId=@G AND ChannelId=@C;", cn))
            {
                cmd.Parameters.AddWithValue("@G", guildId);
                cmd.Parameters.AddWithValue("@C", channelId);
                await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await r.ReadAsync().ConfigureAwait(false))
                {
                    var game = r["GameName"]?.ToString() ?? "";
                    var ds = r["DatasetKey"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(game) && !string.IsNullOrEmpty(ds))
                        gameToDataset[game] = ds;
                }
            }

            // 3) Construire les index par jeu, en lisant les Datapackage* via datasetKey
            var perGame = new Dictionary<string, DatapackageClient.DatapackageIndex>(StringComparer.OrdinalIgnoreCase);

            foreach (var game in games)
            {
                if (!gameToDataset.TryGetValue(game, out var datasetKey) || string.IsNullOrEmpty(datasetKey))
                    continue; // pas encore mappé → seed requis la première fois

                var idx = await BuildOneFromDbAsync(guildId, channelId, datasetKey);
                perGame[game] = idx;
            }

            return perGame;
        }

        // idempotent : même implémentation que je t’ai donnée précédemment
        public static async Task<DatapackageClient.DatapackageIndex> BuildOneFromDbAsync(
            string guildId, string channelId, string datasetKey)
        {
            var idx = new DatapackageClient.DatapackageIndex { Checksum = datasetKey };
            await using var cn = await Db.OpenReadAsync().ConfigureAwait(false);

            // Items
            await using (var cmd = new SQLiteCommand(
                @"SELECT Id, Name FROM DatapackageItems
              WHERE GuildId=@G AND ChannelId=@C AND DatasetKey=@D;", cn))
            {
                cmd.Parameters.AddWithValue("@G", guildId);
                cmd.Parameters.AddWithValue("@C", channelId);
                cmd.Parameters.AddWithValue("@D", datasetKey);
                await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await r.ReadAsync().ConfigureAwait(false))
                {
                    var id = (r["Id"] is long L) ? L : Convert.ToInt64(r["Id"]);
                    if (id >= int.MinValue && id <= int.MaxValue)
                        idx.ItemIdToName[(int)id] = r["Name"]?.ToString() ?? "";
                }
            }

            // Locations
            await using (var cmd = new SQLiteCommand(
                @"SELECT Id, Name FROM DatapackageLocations
              WHERE GuildId=@G AND ChannelId=@C AND DatasetKey=@D;", cn))
            {
                cmd.Parameters.AddWithValue("@G", guildId);
                cmd.Parameters.AddWithValue("@C", channelId);
                cmd.Parameters.AddWithValue("@D", datasetKey);
                await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await r.ReadAsync().ConfigureAwait(false))
                {
                    var id = (r["Id"] is long L) ? L : Convert.ToInt64(r["Id"]);
                    if (id >= int.MinValue && id <= int.MaxValue)
                        idx.LocationIdToName[(int)id] = r["Name"]?.ToString() ?? "";
                }
            }

            // ItemGroups
            await using (var cmd = new SQLiteCommand(
                @"SELECT g.GroupName, i.Name
              FROM DatapackageItemGroups g
              JOIN DatapackageItems i
                ON i.GuildId=g.GuildId AND i.ChannelId=g.ChannelId
               AND i.DatasetKey=g.DatasetKey AND i.Id=g.ItemId
              WHERE g.GuildId=@G AND g.ChannelId=@C AND g.DatasetKey=@D;", cn))
            {
                cmd.Parameters.AddWithValue("@G", guildId);
                cmd.Parameters.AddWithValue("@C", channelId);
                cmd.Parameters.AddWithValue("@D", datasetKey);
                await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await r.ReadAsync().ConfigureAwait(false))
                {
                    var group = r["GroupName"]?.ToString() ?? "";
                    var name = r["Name"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(name)) continue;
                    if (!idx.ItemGroups.TryGetValue(group, out var list))
                        idx.ItemGroups[group] = list = new List<string>();
                    list.Add(name);
                }
            }

            // LocationGroups
            await using (var cmd = new SQLiteCommand(
                @"SELECT g.GroupName, l.Name
              FROM DatapackageLocationGroups g
              JOIN DatapackageLocations l
                ON l.GuildId=g.GuildId AND l.ChannelId=g.ChannelId
               AND l.DatasetKey=g.DatasetKey AND l.Id=g.LocationId
              WHERE g.GuildId=@G AND g.ChannelId=@C AND g.DatasetKey=@D;", cn))
            {
                cmd.Parameters.AddWithValue("@G", guildId);
                cmd.Parameters.AddWithValue("@C", channelId);
                cmd.Parameters.AddWithValue("@D", datasetKey);
                await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await r.ReadAsync().ConfigureAwait(false))
                {
                    var group = r["GroupName"]?.ToString() ?? "";
                    var name = r["Name"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(name)) continue;
                    if (!idx.LocationGroups.TryGetValue(group, out var list))
                        idx.LocationGroups[group] = list = new List<string>();
                    list.Add(name);
                }
            }

            return idx;
        }
    }
}
