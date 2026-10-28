using System.Data.SQLite;

public static class HintStatusCommands
{
    public static async Task<List<HintStatus>> GetHintStatusForReceiver(
    string guildId, string channelId, string receiverId)
    {
        var list = new List<HintStatus>();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
        SELECT Finder, Receiver, Item, Location, Game, Entrance, Flag, FinderSlot, ReceiverSlot, ItemId, LocationId, ItemFlags, Status
        FROM TrackingHints
        WHERE GuildId = @GuildId
          AND ChannelId = @ChannelId
          AND Receiver = @Receiver
          AND Flag = 'False';", connection);

        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Receiver", receiverId);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(new HintStatus
            {
                Finder = reader["Finder"]?.ToString() ?? string.Empty,
                FinderSlot = reader["FinderSlot"] is DBNull ? 0 : Convert.ToInt32(reader["FinderSlot"]),
                ReceiverSlot = reader["ReceiverSlot"] is DBNull ? 0 : Convert.ToInt32(reader["ReceiverSlot"]),
                ItemId = reader["ItemId"] is DBNull ? 0 : Convert.ToInt64(reader["ItemId"]),
                LocationId = reader["LocationId"] is DBNull ? 0 : Convert.ToInt64(reader["LocationId"]),
                ItemFlags = Convert.ToInt32(reader["ItemFlags"]),
                Status = Convert.ToInt32(reader["Status"]),
                Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                Item = reader["Item"]?.ToString() ?? string.Empty,
                Location = reader["Location"]?.ToString() ?? string.Empty,
                Game = reader["Game"]?.ToString() ?? string.Empty,
                Entrance = reader["Entrance"]?.ToString() ?? string.Empty,
                Flag = reader["Flag"]?.ToString() ?? string.Empty
            });
        }

        return list;
    }

    public static async Task<List<HintStatus>> GetHintStatusForFinder(
        string guildId, string channelId, string finderId)
    {
        var list = new List<HintStatus>();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
        SELECT Finder, Receiver, Item, Location, Game, Entrance, Flag, FinderSlot, ReceiverSlot, ItemId, LocationId, ItemFlags, Status
        FROM TrackingHints
        WHERE GuildId = @GuildId
          AND ChannelId = @ChannelId
          AND Finder = @Finder
          AND Flag = 'False';", connection);

        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Finder", finderId);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(new HintStatus
            {
                Finder = reader["Finder"]?.ToString() ?? string.Empty,
                FinderSlot = reader["FinderSlot"] is DBNull ? 0 : Convert.ToInt32(reader["FinderSlot"]),
                ReceiverSlot = reader["ReceiverSlot"] is DBNull ? 0 : Convert.ToInt32(reader["ReceiverSlot"]),
                ItemId = reader["ItemId"] is DBNull ? 0 : Convert.ToInt64(reader["ItemId"]),
                LocationId = reader["LocationId"] is DBNull ? 0 : Convert.ToInt64(reader["LocationId"]),
                ItemFlags = Convert.ToInt32(reader["ItemFlags"]),
                Status = Convert.ToInt32(reader["Status"]),
                Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                Item = reader["Item"]?.ToString() ?? string.Empty,
                Location = reader["Location"]?.ToString() ?? string.Empty,
                Game = reader["Game"]?.ToString() ?? string.Empty,
                Entrance = reader["Entrance"]?.ToString() ?? string.Empty,
                Flag = reader["Flag"]?.ToString() ?? string.Empty
            });
        }

        return list;
    }

    public static async Task<List<HintStatus>> GetHintStatus(
        string guild,
        string channel
        )
    {
        var list = new List<HintStatus>();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
            SELECT Finder, Receiver, Item, Location, Game, Entrance, Flag, FinderSlot, ReceiverSlot, ItemId, LocationId, ItemFlags, Status
            FROM TrackingHints
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);

        command.Parameters.AddWithValue("@GuildId", guild);
        command.Parameters.AddWithValue("@ChannelId", channel);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(new HintStatus
            {
                Finder = reader["Finder"]?.ToString() ?? string.Empty,
                FinderSlot = reader["FinderSlot"] is DBNull ? 0 : Convert.ToInt32(reader["FinderSlot"]),
                ReceiverSlot = reader["ReceiverSlot"] is DBNull ? 0 : Convert.ToInt32(reader["ReceiverSlot"]),
                ItemId = reader["ItemId"] is DBNull ? 0 : Convert.ToInt64(reader["ItemId"]),
                LocationId = reader["LocationId"] is DBNull ? 0 : Convert.ToInt64(reader["LocationId"]),
                ItemFlags = Convert.ToInt32(reader["ItemFlags"]),
                Status = Convert.ToInt32(reader["Status"]),
                Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                Item = reader["Item"]?.ToString() ?? string.Empty,
                Location = reader["Location"]?.ToString() ?? string.Empty,
                Game = reader["Game"]?.ToString() ?? string.Empty,
                Entrance = reader["Entrance"]?.ToString() ?? string.Empty,
                Flag = reader["Flag"]?.ToString() ?? string.Empty
            });
        }

        return list;
    }

}
