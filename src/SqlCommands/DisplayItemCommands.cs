using System.Data.SQLite;

public static class DisplayItemCommands
{
    // ==========================
    // 🎯 Display Item
    // ==========================
    public static async Task<HashSet<string>> GetExistingKeysAsync(string guildId, string channelId)
    {
        var keys = new HashSet<string>();

        using var connection = await Db.OpenAsync(Declare.CT);
        using var command = new SQLiteCommand(@"
    SELECT Sphere, Finder, Receiver, Item, Location, Game 
    FROM DisplayedItemTable 
    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);

        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = $"{reader["Sphere"]}|{reader["Finder"]}|{reader["Receiver"]}|{reader["Item"]}|{reader["Location"]}|{reader["Game"]}";
            keys.Add(key);
        }

        return keys;
    }

    public static async Task<List<DisplayedItem>> GetUserItemsGroupedAsync(string guildId, string channelId, string receiver)
    {
        var itemsFromDb = new List<DisplayedItem>();

        using var connection = await Db.OpenAsync(Declare.CT);
        
        using (var command = new SQLiteCommand(@"
            SELECT *
            FROM DisplayedItemTable
            WHERE GuildId = @GuildId
              AND ChannelId = @ChannelId
              AND Receiver = @Receiver;", connection))
        {
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@Receiver", receiver);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    itemsFromDb.Add(new DisplayedItem
                    {
                        GuildId = reader["GuildId"]?.ToString() ?? string.Empty,
                        ChannelId = reader["ChannelId"]?.ToString() ?? string.Empty,
                        Sphere = reader["Sphere"]?.ToString() ?? string.Empty,
                        Finder = reader["Finder"]?.ToString() ?? string.Empty,
                        Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                        Item = reader["Item"]?.ToString() ?? string.Empty,
                        Location = reader["Location"]?.ToString() ?? string.Empty,
                        Game = reader["Game"]?.ToString() ?? string.Empty,
                    });
                }
            }
        }

        return itemsFromDb;
    }

    public static async Task<List<DisplayedItem>> GetAliasItems(string guildId, string channelId, string alias)
    {
        var itemsFromDb = new List<DisplayedItem>();

        using var connection = await Db.OpenAsync(Declare.CT);

        using (var command = new SQLiteCommand(@"
            SELECT Item, GuildId, ChannelId, Sphere, Finder, Receiver, Location, Game
            FROM DisplayedItemTable
            WHERE GuildId = @GuildId
              AND ChannelId = @ChannelId
              AND Receiver = @Receiver;", connection))
        {
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@Receiver", alias);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    itemsFromDb.Add(new DisplayedItem
                    {
                        GuildId = reader["GuildId"]?.ToString() ?? string.Empty,
                        ChannelId = reader["ChannelId"]?.ToString() ?? string.Empty,
                        Sphere = reader["Sphere"]?.ToString() ?? string.Empty,
                        Finder = reader["Finder"]?.ToString() ?? string.Empty,
                        Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                        Item = reader["Item"]?.ToString() ?? string.Empty,
                        Location = reader["Location"]?.ToString() ?? string.Empty,
                        Game = reader["Game"]?.ToString() ?? string.Empty,
                    });
                }
            }
        }

        return itemsFromDb;
    }

    public static async Task AddItemsAsync(List<DisplayedItem> items, string guildId, string channelId)
    {
        if (items.Count == 0)
            return;

        using var connection = await Db.OpenAsync(Declare.CT);
        using var transaction = connection.BeginTransaction();

        using var command = new SQLiteCommand(@"
        INSERT OR IGNORE INTO DisplayedItemTable
            (GuildId, ChannelId, Sphere, Finder, Receiver, Item, Location, Game)
        VALUES
            (@GuildId, @ChannelId, @Sphere, @Finder, @Receiver, @Item, @Location, @Game);",
            connection, transaction);

        command.Parameters.Add("@GuildId", System.Data.DbType.String);
        command.Parameters.Add("@ChannelId", System.Data.DbType.String);
        command.Parameters.Add("@Sphere", System.Data.DbType.String);
        command.Parameters.Add("@Finder", System.Data.DbType.String);
        command.Parameters.Add("@Receiver", System.Data.DbType.String);
        command.Parameters.Add("@Item", System.Data.DbType.String);
        command.Parameters.Add("@Location", System.Data.DbType.String);
        command.Parameters.Add("@Game", System.Data.DbType.String);

        command.Prepare();

        foreach (var item in items)
        {
            command.Parameters["@GuildId"].Value = guildId;
            command.Parameters["@ChannelId"].Value = channelId;
            command.Parameters["@Sphere"].Value = item.Sphere ?? (object)DBNull.Value;
            command.Parameters["@Finder"].Value = item.Finder ?? (object)DBNull.Value;
            command.Parameters["@Receiver"].Value = item.Receiver ?? (object)DBNull.Value;
            command.Parameters["@Item"].Value = item.Item ?? (object)DBNull.Value;
            command.Parameters["@Location"].Value = item.Location ?? (object)DBNull.Value;
            command.Parameters["@Game"].Value = item.Game ?? (object)DBNull.Value;

            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

}

