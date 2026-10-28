using System.Data.SQLite;

public static class DisplayItemCommands
{
    // ==========================
    // 🎯 Display Item (READ)
    // ==========================
    public static async Task<List<DisplayedItem>> GetUserItemsGroupedAsync(
     string guildId,
     string channelId,
     string receiver)
    {
        var itemsFromDb = new List<DisplayedItem>();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
        SELECT GuildId, ChannelId, Finder, Receiver, Item, Location, Game, Flag, FinderSlot, ReceiverSlot, ItemId, LocationId
        FROM TrackingItems
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
                FinderSlot = reader["FinderSlot"] is DBNull ? 0 : Convert.ToInt32(reader["FinderSlot"]),
                ReceiverSlot = reader["ReceiverSlot"] is DBNull ? 0 : Convert.ToInt32(reader["ReceiverSlot"]),
                ItemId = reader["ItemId"] is DBNull ? 0 : Convert.ToInt64(reader["ItemId"]),
                LocationId = reader["LocationId"] is DBNull ? 0 : Convert.ToInt64(reader["LocationId"]),
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
            SELECT Item, GuildId, ChannelId, Finder, Receiver, Location, Game, Flag, FinderSlot, ReceiverSlot, ItemId, LocationId
            FROM TrackingItems
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
                FinderSlot = reader["FinderSlot"] is DBNull ? 0 : Convert.ToInt32(reader["FinderSlot"]),
                ReceiverSlot = reader["ReceiverSlot"] is DBNull ? 0 : Convert.ToInt32(reader["ReceiverSlot"]),
                ItemId = reader["ItemId"] is DBNull ? 0 : Convert.ToInt64(reader["ItemId"]),
                LocationId = reader["LocationId"] is DBNull ? 0 : Convert.ToInt64(reader["LocationId"]),
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

}
