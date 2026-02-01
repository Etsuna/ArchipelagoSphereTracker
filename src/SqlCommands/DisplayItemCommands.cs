using System.Data.SQLite;

public static class DisplayItemCommands
{
    // ==========================
    // 🎯 Display Item (READ)
    // ==========================
    public static async Task<HashSet<string>> GetExistingKeysAsync(
        string guildId,
        string channelId
        )
    {
        var keys = new HashSet<string>();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
            SELECT Finder, Receiver, Item, Location, Game, Flag
            FROM DisplayedItemTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);

        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var key = $"{reader["Finder"]}|{reader["Receiver"]}|{reader["Item"]}|{reader["Location"]}|{reader["Game"]}|{reader["Flag"]}";
            keys.Add(key);
        }

        return keys;
    }

    public static async Task<List<DisplayedItem>> GetUserItemsGroupedAsync(
     string guildId,
     string channelId,
     string receiver)
    {
        var itemsFromDb = new List<DisplayedItem>();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
        SELECT GuildId, ChannelId, Finder, Receiver, Item, Location, Game, Flag
        FROM DisplayedItemTable
        WHERE GuildId = @GuildId
          AND ChannelId = @ChannelId
          AND Receiver = @Receiver;", connection);

        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Receiver", receiver);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            itemsFromDb.Add(new DisplayedItem
            {
                GuildId = reader["GuildId"]?.ToString() ?? string.Empty,
                ChannelId = reader["ChannelId"]?.ToString() ?? string.Empty,
                Finder = reader["Finder"]?.ToString() ?? string.Empty,
                Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                Item = reader["Item"]?.ToString() ?? string.Empty,
                Location = reader["Location"]?.ToString() ?? string.Empty,
                Game = reader["Game"]?.ToString() ?? string.Empty,
                Flag = reader["Flag"]?.ToString() ?? string.Empty, 
            });
        }

        return itemsFromDb;
    }

    public static async Task<List<DisplayedItem>> GetAliasItems(
        string guildId,
        string channelId,
        string alias
        )
    {
        var itemsFromDb = new List<DisplayedItem>();

        await using var connection = await Db.OpenReadAsync();
        using (var command = new SQLiteCommand(@"
            SELECT Item, GuildId, ChannelId, Finder, Receiver, Location, Game, Flag
            FROM DisplayedItemTable
            WHERE GuildId = @GuildId
              AND ChannelId = @ChannelId
              AND Receiver = @Receiver;", connection))
        {
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@Receiver", alias);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                itemsFromDb.Add(new DisplayedItem
                {
                    GuildId = reader["GuildId"]?.ToString() ?? string.Empty,
                    ChannelId = reader["ChannelId"]?.ToString() ?? string.Empty,
                    Flag = reader["Flag"]?.ToString() ?? string.Empty,
                    Finder = reader["Finder"]?.ToString() ?? string.Empty,
                    Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                    Item = reader["Item"]?.ToString() ?? string.Empty,
                    Location = reader["Location"]?.ToString() ?? string.Empty,
                    Game = reader["Game"]?.ToString() ?? string.Empty,
                });
            }
        }

        return itemsFromDb;
    }

    // ==========================
    // 🎯 INSERT (WRITE)
    // ==========================
    public static async Task AddItemsAsync(
        List<DisplayedItem> items,
        string guildId,
        string channelId
        )
    {
        if (items == null || items.Count == 0)
            return;

        await Db.WriteAsync(async conn =>
        {
            using var command = conn.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO DisplayedItemTable
                    (GuildId, ChannelId, Finder, Receiver, Item, Location, Game, Flag)
                VALUES
                    (@GuildId, @ChannelId, @Finder, @Receiver, @Item, @Location, @Game, @Flag);";

            command.Parameters.Add("@GuildId", System.Data.DbType.String);
            command.Parameters.Add("@ChannelId", System.Data.DbType.String);
            command.Parameters.Add("@Finder", System.Data.DbType.String);
            command.Parameters.Add("@Receiver", System.Data.DbType.String);
            command.Parameters.Add("@Item", System.Data.DbType.String);
            command.Parameters.Add("@Location", System.Data.DbType.String);
            command.Parameters.Add("@Game", System.Data.DbType.String);
            command.Parameters.Add("@Flag", System.Data.DbType.String);

            command.Prepare();

            foreach (var it in items)
            {
                command.Parameters["@GuildId"].Value = guildId;
                command.Parameters["@ChannelId"].Value = channelId;
                command.Parameters["@Finder"].Value = (object?)it.Finder ?? DBNull.Value;
                command.Parameters["@Receiver"].Value = (object?)it.Receiver ?? DBNull.Value;
                command.Parameters["@Item"].Value = (object?)it.Item ?? DBNull.Value;
                command.Parameters["@Location"].Value = (object?)it.Location ?? DBNull.Value;
                command.Parameters["@Game"].Value = (object?)it.Game ?? DBNull.Value;
                command.Parameters["@Flag"].Value = (object?)it.Flag ?? DBNull.Value;

                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        });
    }
}
