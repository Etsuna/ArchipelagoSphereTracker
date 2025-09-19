using System.Data.SQLite;

public static class HintStatusCommands
{
    public static async Task UpdateHintStatusAsync(
        string guildId,
        string channelId,
        List<HintStatus> hintstatusList
        )
    {
        if (hintstatusList is null || hintstatusList.Count == 0) return;

        await Db.WriteAsync(async conn =>
        {
            using var command = conn.CreateCommand();
            command.CommandText = @"
                UPDATE HintStatusTable
                SET Flag = @Flag
                WHERE GuildId = @GuildId
                  AND ChannelId = @ChannelId
                  AND Finder = @Finder
                  AND Receiver = @Receiver
                  AND Item = @Item
                  AND Location = @Location
                  AND Game = @Game
                  AND Entrance = @Entrance;";

            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            var pFinder = command.Parameters.Add("@Finder", System.Data.DbType.String);
            var pReceiver = command.Parameters.Add("@Receiver", System.Data.DbType.String);
            var pItem = command.Parameters.Add("@Item", System.Data.DbType.String);
            var pLocation = command.Parameters.Add("@Location", System.Data.DbType.String);
            var pGame = command.Parameters.Add("@Game", System.Data.DbType.String);
            var pEntrance = command.Parameters.Add("@Entrance", System.Data.DbType.String);
            var pFlag = command.Parameters.Add("@Flag", System.Data.DbType.String);

            command.Prepare();

            foreach (var s in hintstatusList)
            {
                pFinder.Value = s.Finder ?? (object)DBNull.Value;
                pReceiver.Value = s.Receiver ?? (object)DBNull.Value;
                pItem.Value = s.Item ?? (object)DBNull.Value;
                pLocation.Value = s.Location ?? (object)DBNull.Value;
                pGame.Value = s.Game ?? (object)DBNull.Value;
                pEntrance.Value = s.Entrance ?? (object)DBNull.Value;
                pFlag.Value = s.Flag ?? (object)DBNull.Value;

                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        });
    }

    public static async Task<List<HintStatus>> GetHintStatusForReceiver(
        string guildId,
        string channelId,
        string receiverId
        )
    {
        var list = new List<HintStatus>();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
            SELECT Finder, Receiver, Item, Location, Game, Entrance, Flag
            FROM HintStatusTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Receiver = @Receiver;", connection);

        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Receiver", receiverId);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(new HintStatus
            {
                Finder = reader["Finder"]?.ToString() ?? string.Empty,
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
        string guildId,
        string channelId,
        string finderId
        )
    {
        var list = new List<HintStatus>();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
            SELECT Finder, Receiver, Item, Location, Game, Entrance, Flag
            FROM HintStatusTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Finder = @Finder;", connection);

        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Finder", finderId);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(new HintStatus
            {
                Finder = reader["Finder"]?.ToString() ?? string.Empty,
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
            SELECT Finder, Receiver, Item, Location, Game, Entrance, Flag
            FROM HintStatusTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);

        command.Parameters.AddWithValue("@GuildId", guild);
        command.Parameters.AddWithValue("@ChannelId", channel);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(new HintStatus
            {
                Finder = reader["Finder"]?.ToString() ?? string.Empty,
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

    public static async Task AddHintStatusAsync(
        string guild,
        string channel,
        List<HintStatus> hintStatus
        )
    {
        if (hintStatus is null || hintStatus.Count == 0) return;

        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var command = conn.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO HintStatusTable
                        (GuildId, ChannelId, Finder, Receiver, Item, Location, Game, Entrance, Flag)
                    VALUES
                        (@GuildId, @ChannelId, @Finder, @Receiver, @Item, @Location, @Game, @Entrance, @Flag);";

                var pGuild = command.Parameters.Add("@GuildId", System.Data.DbType.String);
                var pChannel = command.Parameters.Add("@ChannelId", System.Data.DbType.String);
                var pFinder = command.Parameters.Add("@Finder", System.Data.DbType.String);
                var pReceiver = command.Parameters.Add("@Receiver", System.Data.DbType.String);
                var pItem = command.Parameters.Add("@Item", System.Data.DbType.String);
                var pLocation = command.Parameters.Add("@Location", System.Data.DbType.String);
                var pGame = command.Parameters.Add("@Game", System.Data.DbType.String);
                var pEntrance = command.Parameters.Add("@Entrance", System.Data.DbType.String);
                var pFlag = command.Parameters.Add("@Flag", System.Data.DbType.String);

                command.Prepare();

                foreach (var s in hintStatus)
                {
                    pGuild.Value = guild;
                    pChannel.Value = channel;
                    pFinder.Value = (object?)s.Finder ?? DBNull.Value;
                    pReceiver.Value = (object?)s.Receiver ?? DBNull.Value;
                    pItem.Value = (object?)s.Item ?? DBNull.Value;
                    pLocation.Value = (object?)s.Location ?? DBNull.Value;
                    pGame.Value = (object?)s.Game ?? DBNull.Value;
                    pEntrance.Value = (object?)s.Entrance ?? DBNull.Value;
                    pFlag.Value = (object?)s.Flag ?? DBNull.Value;

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or replacing hint status: {ex.Message}");
        }
    }
}
