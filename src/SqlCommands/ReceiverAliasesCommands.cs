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

    public static async Task<bool> CheckIfReceiverExists(string guildId, string channelId, string receiver)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
            SELECT COUNT(*)
            FROM ReceiverAliasesTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Receiver = @Receiver;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Receiver", receiver);

        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
        var count = (result != null && result != DBNull.Value) ? Convert.ToInt64(result) : 0L;
        return count > 0;
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
            SELECT Receiver, UserId, IsEnabled
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
                IsEnabled = reader["IsEnabled"] != DBNull.Value && Convert.ToBoolean(reader["IsEnabled"]),
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
    public static async Task InsertReceiverAlias(string guildId, string channelId, string receiver, string userId, bool isEnabled)
    {
        await Db.WriteAsync(async conn =>
        {
            using var command = new SQLiteCommand(@"
                INSERT INTO ReceiverAliasesTable (GuildId, ChannelId, Receiver, UserId, IsEnabled)
                VALUES (@GuildId, @ChannelId, @Receiver, @UserId, @IsEnabled);", conn);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@Receiver", receiver);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@IsEnabled", isEnabled);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        });
    }

    // ==========================
    // 🎯 USER ALIASES + ITEMS (READ)
    // ==========================
    public static async Task<Dictionary<string, List<string>>> GetUserAliasesWithItemsAsync(
        string guildId,
        string channelId,
        string userId,
        string specificAlias = ""
        )
    {
        var aliasesWithItems = new Dictionary<string, List<string>>();

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

            var items = new List<string>();

            const string itemsQuery = @"
                SELECT Item
                FROM RecapListItemsTable
                WHERE RecapListTableId = @RecapListTableId;";
            using var itemCommand = new SQLiteCommand(itemsQuery, connection);
            itemCommand.Parameters.AddWithValue("@RecapListTableId", recapListTableId);

            using var itemReader = await itemCommand.ExecuteReaderAsync().ConfigureAwait(false);
            while (await itemReader.ReadAsync().ConfigureAwait(false))
            {
                var item = itemReader["Item"] as string;
                items.Add(!string.IsNullOrEmpty(item) ? item : Resource.GetUserAliasesWithItemsAsyncNoItem);
            }

            if (!string.IsNullOrEmpty(alias))
                aliasesWithItems[alias] = items.Count > 0 ? items : new List<string> { Resource.GetUserAliasesWithItemsAsyncNoItem };
            else
                Console.WriteLine(string.Format(Resource.GetUserAliasesWithItemsAsyncNoAlias, userId));
        }

        return aliasesWithItems;
    }
}
