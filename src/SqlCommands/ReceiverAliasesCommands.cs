using ArchipelagoSphereTracker.src.Resources;
using System.Data.SQLite;

public static class ReceiverAliasesCommands
{
    // ==========================
    // 🎯 Receiver Aliases (READ)
    // ==========================
    public static async Task<List<string>> GetReceiver(string guildId, string channelId)
    {
        var receivers = new List<string>();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
            SELECT Receiver
            FROM ReceiverAliasesTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var receiver = reader["Receiver"]?.ToString() ?? "";
            if (!string.IsNullOrEmpty(receiver))
                receivers.Add(receiver);
        }
        return receivers;
    }

    public static async Task<List<string>> GetUserIds(string guildId, string channelId)
    {
        var receivers = new List<string>();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
            SELECT UserId
            FROM ReceiverAliasesTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var uid = reader["UserId"]?.ToString() ?? "";
            if (!string.IsNullOrEmpty(uid))
                receivers.Add(uid);
        }
        return receivers;
    }

    // ==========================
    // 🎯 GET RECEIVER USER IDS (READ)
    // ==========================
    public static async Task<List<ReceiverUserInfo>> GetReceiverUserIdsAsync(string guildId, string channelId, string receiver)
    {
        var userInfos = new List<ReceiverUserInfo>();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
            SELECT Receiver, UserId, Flag
            FROM ReceiverAliasesTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Receiver = @Receiver;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Receiver", receiver);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var info = new ReceiverUserInfo
            {
                UserId = reader["UserId"]?.ToString() ?? "",
                Flag = reader["Flag"]?.ToString() ?? "",
            };
            if (!string.IsNullOrEmpty(info.UserId))
                userInfos.Add(info);
        }
        return userInfos;
    }

    // ==========================
    // 🎯 GET ALL USERS IDS (READ)
    // ==========================
    public static async Task<List<string>> GetAllUsersIds(string guildId, string channelId, string receiver)
    {
        var userIds = new List<string>();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
            SELECT UserId
            FROM ReceiverAliasesTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Receiver = @Receiver;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Receiver", receiver);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (!reader.IsDBNull(0))
                userIds.Add(reader.GetString(0));
        }
        return userIds;
    }

    // ==========================
    // 🎯 DELETE RECEIVER ALIAS (WRITE)
    // ==========================
    public static async Task DeleteReceiverAlias(string guildId, string channelId, string receiver)
    {
        await Db.WriteAsync(async conn =>
        {
            using var command = new SQLiteCommand(@"
                DELETE FROM ReceiverAliasesTable
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Receiver = @Receiver;", conn);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@Receiver", receiver);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        });
    }

    // ==========================
    // 🎯 INSERT RECEIVER ALIAS  (WRITE)
    // ==========================   
    public static async Task InsertReceiverAlias(string guildId, string channelId, string receiver, string userId, string flag)
    {
        await Db.WriteAsync(async conn =>
        {
            using var command = new SQLiteCommand(@"
                INSERT INTO ReceiverAliasesTable (GuildId, ChannelId, Receiver, UserId, Flag)
                VALUES (@GuildId, @ChannelId, @Receiver, @UserId, @Flag);", conn);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@Receiver", receiver);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Flag", flag);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        });
    }

    // ==========================
    // 🎯 USER ALIASES + ITEMS (READ)
    // ==========================
    public static async Task<Dictionary<string, List<(string Item, long? Flag)>>> GetUserAliasesWithItemsAsync(
    string guildId,
    string channelId,
    string userId,
    string specificAlias = ""
)
    {
        var aliasesWithItems = new Dictionary<string, List<(string Item, long? Flag)>>();

        await using var connection = await Db.OpenReadAsync();

        const string aliasQuery = @"
        SELECT Alias, RecapListTable.Id AS RecapListTableId
        FROM RecapListTable
        WHERE UserId = @UserId AND GuildId = @GuildId AND ChannelId = @ChannelId;";

        using var command = new SQLiteCommand(aliasQuery, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var alias = reader["Alias"]?.ToString();
            var recapListTableId = Convert.ToInt64(reader["RecapListTableId"]);

            if (!string.IsNullOrEmpty(specificAlias) && alias != specificAlias)
                continue;

            var items = new List<(string Item, long? Flag)>();

            const string itemsQuery = @"
                SELECT i.Item,
                (
                    SELECT d.Flag
                    FROM DisplayedItemTable AS d
                    WHERE d.GuildId = @GuildId
                    AND d.ChannelId = @ChannelId
                    AND d.Item = i.Item
                    ORDER BY d.Flag DESC
                    LIMIT 1
                ) AS Flag
                FROM RecapListItemsTable AS i
                WHERE i.RecapListTableId = @RecapListTableId;";

            using var itemCommand = new SQLiteCommand(itemsQuery, connection);
            itemCommand.Parameters.AddWithValue("@RecapListTableId", recapListTableId);
            itemCommand.Parameters.AddWithValue("@GuildId", guildId);
            itemCommand.Parameters.AddWithValue("@ChannelId", channelId);

            using var itemReader = await itemCommand.ExecuteReaderAsync().ConfigureAwait(false);
            while (await itemReader.ReadAsync().ConfigureAwait(false))
            {
                var item = itemReader["Item"] as string;
                var flagObj = itemReader["Flag"];
                long? flag = flagObj == DBNull.Value ? (long?)null : Convert.ToInt64(flagObj);

                items.Add(!string.IsNullOrEmpty(item)
                    ? (item, flag)
                    : (Resource.GetUserAliasesWithItemsAsyncNoItem, (long?)null));
            }

            if (!string.IsNullOrEmpty(alias))
                aliasesWithItems[alias] = items.Count > 0
                    ? items
                    : new List<(string, long?)> { (Resource.GetUserAliasesWithItemsAsyncNoItem, null) };
            else
                Console.WriteLine(string.Format(Resource.GetUserAliasesWithItemsAsyncNoAlias, userId));
        }

        return aliasesWithItems;
    }

}
