using System.Data.SQLite;
using System.Text.RegularExpressions;

public static class HintStatusCommands
{
    public static async Task UpdateHintStatusAsync(
        string guildId,
        string channelId,
        List<HintStatus> hintstatusList,
        CancellationToken ct = default)
    {
        if (hintstatusList is null || hintstatusList.Count == 0) return;

        await Db.WriteAsync(async conn =>
        {
            using var command = conn.CreateCommand();
            command.CommandText = @"
                UPDATE HintStatusTable
                SET Found = @Found
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
            var pFound = command.Parameters.Add("@Found", System.Data.DbType.String);

            command.Prepare();

            foreach (var s in hintstatusList)
            {
                ct.ThrowIfCancellationRequested();
                pFinder.Value = s.Finder ?? (object)DBNull.Value;
                pReceiver.Value = s.Receiver ?? (object)DBNull.Value;
                pItem.Value = s.Item ?? (object)DBNull.Value;
                pLocation.Value = s.Location ?? (object)DBNull.Value;
                pGame.Value = s.Game ?? (object)DBNull.Value;
                pEntrance.Value = s.Entrance ?? (object)DBNull.Value;
                pFound.Value = s.Found ?? (object)DBNull.Value;

                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }, ct);
    }

    public static async Task<List<HintStatus>> GetHintStatusForReceiver(
        string guildId,
        string channelId,
        string receiverId,
        CancellationToken ct = default)
    {
        var list = new List<HintStatus>();

        await using var connection = await Db.OpenReadAsync(ct);
        using var command = new SQLiteCommand(@"
            SELECT Finder, Receiver, Item, Location, Game, Entrance, Found
            FROM HintStatusTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Receiver = @Receiver;", connection);

        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Receiver", receiverId);

        using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new HintStatus
            {
                Finder = reader["Finder"]?.ToString() ?? string.Empty,
                Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                Item = reader["Item"]?.ToString() ?? string.Empty,
                Location = reader["Location"]?.ToString() ?? string.Empty,
                Game = reader["Game"]?.ToString() ?? string.Empty,
                Entrance = reader["Entrance"]?.ToString() ?? string.Empty,
                Found = reader["Found"]?.ToString() ?? string.Empty
            });
        }

        return list;
    }

    public static async Task<List<HintStatus>> GetHintStatusForFinder(
        string guildId,
        string channelId,
        string finderId,
        CancellationToken ct = default)
    {
        var list = new List<HintStatus>();

        await using var connection = await Db.OpenReadAsync(ct);
        using var command = new SQLiteCommand(@"
            SELECT Finder, Receiver, Item, Location, Game, Entrance, Found
            FROM HintStatusTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Finder = @Finder;", connection);

        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Finder", finderId);

        using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new HintStatus
            {
                Finder = reader["Finder"]?.ToString() ?? string.Empty,
                Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                Item = reader["Item"]?.ToString() ?? string.Empty,
                Location = reader["Location"]?.ToString() ?? string.Empty,
                Game = reader["Game"]?.ToString() ?? string.Empty,
                Entrance = reader["Entrance"]?.ToString() ?? string.Empty,
                Found = reader["Found"]?.ToString() ?? string.Empty
            });
        }

        return list;
    }

    public static async Task<List<HintStatus>> GetHintStatus(
        string guild,
        string channel,
        CancellationToken ct = default)
    {
        var list = new List<HintStatus>();

        await using var connection = await Db.OpenReadAsync(ct);
        using var command = new SQLiteCommand(@"
            SELECT Finder, Receiver, Item, Location, Game, Entrance, Found
            FROM HintStatusTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);

        command.Parameters.AddWithValue("@GuildId", guild);
        command.Parameters.AddWithValue("@ChannelId", channel);

        using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new HintStatus
            {
                Finder = reader["Finder"]?.ToString() ?? string.Empty,
                Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                Item = reader["Item"]?.ToString() ?? string.Empty,
                Location = reader["Location"]?.ToString() ?? string.Empty,
                Game = reader["Game"]?.ToString() ?? string.Empty,
                Entrance = reader["Entrance"]?.ToString() ?? string.Empty,
                Found = reader["Found"]?.ToString() ?? string.Empty
            });
        }

        return list;
    }

    public static async Task AddHintStatusAsync(
        string guild,
        string channel,
        List<HintStatus> hintStatus,
        CancellationToken ct = default)
    {
        if (hintStatus is null || hintStatus.Count == 0) return;

        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var command = conn.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO HintStatusTable
                        (GuildId, ChannelId, Finder, Receiver, Item, Location, Game, Entrance, Found)
                    VALUES
                        (@GuildId, @ChannelId, @Finder, @Receiver, @Item, @Location, @Game, @Entrance, @Found);";

                var pGuild = command.Parameters.Add("@GuildId", System.Data.DbType.String);
                var pChannel = command.Parameters.Add("@ChannelId", System.Data.DbType.String);
                var pFinder = command.Parameters.Add("@Finder", System.Data.DbType.String);
                var pReceiver = command.Parameters.Add("@Receiver", System.Data.DbType.String);
                var pItem = command.Parameters.Add("@Item", System.Data.DbType.String);
                var pLocation = command.Parameters.Add("@Location", System.Data.DbType.String);
                var pGame = command.Parameters.Add("@Game", System.Data.DbType.String);
                var pEntrance = command.Parameters.Add("@Entrance", System.Data.DbType.String);
                var pFound = command.Parameters.Add("@Found", System.Data.DbType.String);

                command.Prepare();

                foreach (var s in hintStatus)
                {
                    ct.ThrowIfCancellationRequested();

                    pGuild.Value = guild;
                    pChannel.Value = channel;
                    pFinder.Value = (object?)s.Finder ?? DBNull.Value;
                    pReceiver.Value = (object?)s.Receiver ?? DBNull.Value;
                    pItem.Value = (object?)s.Item ?? DBNull.Value;
                    pLocation.Value = (object?)s.Location ?? DBNull.Value;
                    pGame.Value = (object?)s.Game ?? DBNull.Value;
                    pEntrance.Value = (object?)s.Entrance ?? DBNull.Value;
                    pFound.Value = (object?)s.Found ?? DBNull.Value;

                    await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or replacing hint status: {ex.Message}");
        }
    }

    public static async Task DeleteDuplicateReceiversAliasAsync(
        string guildId,
        string channelId,
        CancellationToken ct = default)
    {
        try
        {
            await Db.WriteAsync(async conn =>
            {
                // 1) lire tous les Receiver
                var existing = new List<string>();
                using (var select = new SQLiteCommand(@"
                    SELECT Receiver
                    FROM HintStatusTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", conn))
                {
                    select.Parameters.AddWithValue("@GuildId", guildId);
                    select.Parameters.AddWithValue("@ChannelId", channelId);

                    using var reader = await select.ExecuteReaderAsync(ct).ConfigureAwait(false);
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        if (!reader.IsDBNull(0))
                            existing.Add(reader.GetString(0));
                    }
                }

                // 2) détecter les doublons à supprimer
                var toDelete = new HashSet<string>(StringComparer.Ordinal);
                foreach (var name in existing)
                {
                    var m = Regex.Match(name, @"\((.+?)\)$");
                    if (m.Success)
                    {
                        var baseName = m.Groups[1].Value;
                        if (existing.Contains(baseName))
                            toDelete.Add(baseName);
                    }
                }

                // 3) supprimer
                using (var del = new SQLiteCommand(@"
                    DELETE FROM HintStatusTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Receiver = @Receiver;", conn))
                {
                    del.Parameters.AddWithValue("@GuildId", guildId);
                    del.Parameters.AddWithValue("@ChannelId", channelId);
                    var p = del.Parameters.Add("@Receiver", System.Data.DbType.String);

                    foreach (var name in toDelete)
                    {
                        ct.ThrowIfCancellationRequested();
                        p.Value = name;
                        await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                }
            }, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while deleting HintStatus: {ex.Message}");
        }
    }

    public static async Task DeleteDuplicateFindersAliasAsync(
        string guildId,
        string channelId,
        CancellationToken ct = default)
    {
        try
        {
            await Db.WriteAsync(async conn =>
            {
                // 1) lire tous les Finder
                var existing = new List<string>();
                using (var select = new SQLiteCommand(@"
                    SELECT Finder
                    FROM HintStatusTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", conn))
                {
                    select.Parameters.AddWithValue("@GuildId", guildId);
                    select.Parameters.AddWithValue("@ChannelId", channelId);

                    using var reader = await select.ExecuteReaderAsync(ct).ConfigureAwait(false);
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        if (!reader.IsDBNull(0))
                            existing.Add(reader.GetString(0));
                    }
                }

                // 2) détecter les doublons à supprimer
                var toDelete = new HashSet<string>(StringComparer.Ordinal);
                foreach (var name in existing)
                {
                    var m = Regex.Match(name, @"\((.+?)\)$");
                    if (m.Success)
                    {
                        var baseName = m.Groups[1].Value;
                        if (existing.Contains(baseName))
                            toDelete.Add(baseName);
                    }
                }

                // 3) supprimer (⚠️ dans HintStatusTable, pas GameStatusTable)
                using (var del = new SQLiteCommand(@"
                    DELETE FROM HintStatusTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Finder = @Finder;", conn))
                {
                    del.Parameters.AddWithValue("@GuildId", guildId);
                    del.Parameters.AddWithValue("@ChannelId", channelId);
                    var p = del.Parameters.Add("@Finder", System.Data.DbType.String);

                    foreach (var name in toDelete)
                    {
                        ct.ThrowIfCancellationRequested();
                        p.Value = name;
                        await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                }
            }, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while deleting HintStatus: {ex.Message}");
        }
    }
}
