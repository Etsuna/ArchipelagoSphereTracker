using System.Data.SQLite;

public static class RecapListCommands
{
    // ==========================
    // 🎯 RecapList (WRITE)
    // ==========================
    public static async Task AddOrEditRecapListAsync(
        string guildId,
        string channelId,
        string userId,
        string alias
        )
    {
        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var command = conn.CreateCommand();
                command.CommandText = @"
                    INSERT INTO RecapListTable (GuildId, ChannelId, UserId, Alias)
                    VALUES (@GuildId, @ChannelId, @UserId, @Alias);";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@Alias", alias);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or updating the RecapList: {ex.Message}");
        }
    }

    // ==========================
    // 🎯 RecapListItems (WRITE) — pour tous
    // ==========================
    public static async Task AddOrEditRecapListItemsForAllAsync(
        string guildId,
        string channelId,
        List<DisplayedItem> items
        )
    {
        if (items is null || items.Count == 0) return;

        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var insert = conn.CreateCommand();
                insert.CommandText = @"
                    INSERT INTO RecapListItemsTable (RecapListTableId, Item)
                    VALUES (@RecapListTableId, @Item);";
                var pId = insert.Parameters.Add("@RecapListTableId", System.Data.DbType.Int64);
                var pItem = insert.Parameters.Add("@Item", System.Data.DbType.String);
                insert.Prepare();

                foreach (var it in items)
                {
                    if (it.Receiver == it.Finder) continue;

                    var ids = await DatabaseCommands
                        .GetIdsAsync(guildId, channelId, it.Receiver, "RecapListTable")
                        .ConfigureAwait(false);
                    if (ids is null || ids.Count == 0) continue;

                    foreach (var id in ids)
                    {
                        pId.Value = id;
                        pItem.Value = it.Item ?? string.Empty;
                        await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or updating RecapList items: {ex.Message}");
        }
    }

    public static async Task AddOrEditRecapListItemsAsync(
        string guildId,
        string channelId,
        string alias,
        List<DisplayedItem> items
        )
    {
        if (items is null || items.Count == 0) return;

        try
        {
            var ids = await DatabaseCommands
                .GetIdsAsync(guildId, channelId, alias, "RecapListTable")
                .ConfigureAwait(false);

            if (ids is null || ids.Count == 0)
            {
                Console.WriteLine("Error: No Guild/Channel record found. Unable to add the items.");
                return;
            }

            await Db.WriteAsync(async conn =>
            {
                using var insert = conn.CreateCommand();
                insert.CommandText = @"
                    INSERT INTO RecapListItemsTable (RecapListTableId, Item)
                    VALUES (@RecapListTableId, @Item);";
                var pId = insert.Parameters.Add("@RecapListTableId", System.Data.DbType.Int64);
                var pItem = insert.Parameters.Add("@Item", System.Data.DbType.String);
                insert.Prepare();

                foreach (var id in ids)
                {
                    foreach (var it in items)
                    {
                        if (it.Receiver == it.Finder) continue;

                        pId.Value = id;
                        pItem.Value = it.Item ?? string.Empty;
                        await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or updating RecapList items: {ex.Message}");
        }
    }

    // ============================
    // 🎯 CHECK IF RecapList EXISTS (READ)
    // ============================
    public static async Task<bool> CheckIfExistsWithoutAlias(
        string guildId,
        string channelId,
        string userId
        )
    {
        try
        {
            await using var connection = await Db.OpenReadAsync();
            using var command = new SQLiteCommand(@"
                SELECT COUNT(*)
                FROM RecapListTable
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND UserId = @UserId;", connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@UserId", userId);

            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            var count = (result != null && result != DBNull.Value) ? Convert.ToInt64(result) : 0L;
            return count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while checking the existence of the RecapList: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> CheckIfExists(
        string guildId,
        string channelId,
        string userId,
        string alias
        )
    {
        try
        {
            await using var connection = await Db.OpenReadAsync();
            using var command = new SQLiteCommand(@"
                SELECT COUNT(*)
                FROM RecapListTable
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId
                  AND UserId = @UserId AND Alias = @Alias;", connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Alias", alias);

            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            var count = (result != null && result != DBNull.Value) ? Convert.ToInt64(result) : 0L;
            return count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while checking the existence of the RecapList: {ex.Message}");
            return false;
        }
    }

    // ==========================
    // 🎯 DELETE RecapList FOR UserId (WRITE)
    // ==========================
    public static async Task DeleteAliasAndItemsForUserIdAsync(
        string guildId,
        string channelId,
        string userId
        )
    {
        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var deleteItems = conn.CreateCommand();
                deleteItems.CommandText = @"
                    DELETE FROM RecapListItemsTable
                    WHERE RecapListTableId IN (
                        SELECT Id
                        FROM RecapListTable
                        WHERE GuildId = @GuildId
                          AND ChannelId = @ChannelId
                          AND UserId = @UserId
                    );";
                deleteItems.Parameters.AddWithValue("@GuildId", guildId);
                deleteItems.Parameters.AddWithValue("@ChannelId", channelId);
                deleteItems.Parameters.AddWithValue("@UserId", userId);
                await deleteItems.ExecuteNonQueryAsync().ConfigureAwait(false);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while deleting: {ex.Message}");
            throw;
        }
    }

    // ==========================
    // 🎯 DELETE RecapList FOR ALIAS (WRITE)
    // ==========================
    public static async Task DeleteRecapListAsync(
        string guildId,
        string channelId,
        string userId,
        string alias
        )
    {
        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var deleteItems = conn.CreateCommand();
                deleteItems.CommandText = @"
                    DELETE FROM RecapListItemsTable
                    WHERE RecapListTableId IN (
                        SELECT Id
                        FROM RecapListTable
                        WHERE GuildId = @GuildId
                          AND ChannelId = @ChannelId
                          AND UserId = @UserId
                          AND Alias = @Alias
                    );";
                deleteItems.Parameters.AddWithValue("@GuildId", guildId);
                deleteItems.Parameters.AddWithValue("@ChannelId", channelId);
                deleteItems.Parameters.AddWithValue("@UserId", userId);
                deleteItems.Parameters.AddWithValue("@Alias", alias);
                await deleteItems.ExecuteNonQueryAsync().ConfigureAwait(false);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while deleting: {ex.Message}");
            throw;
        }
    }

    // ==========================
    // 🎯 DELETE alias + recap list (WRITE)
    // ==========================
    public static async Task DeleteAliasAndRecapListAsync(
        string guildId,
        string channelId,
        string userId,
        string alias
        )
    {
        try
        {
            await Db.WriteAsync(async conn =>
            {
                using (var deleteItems = new SQLiteCommand(@"
                    DELETE FROM RecapListItemsTable
                    WHERE RecapListTableId IN (
                        SELECT Id
                        FROM RecapListTable
                        WHERE GuildId = @GuildId
                          AND ChannelId = @ChannelId
                          AND UserId = @UserId
                          AND Alias = @Alias
                    );", conn))
                {
                    deleteItems.Parameters.AddWithValue("@GuildId", guildId);
                    deleteItems.Parameters.AddWithValue("@ChannelId", channelId);
                    deleteItems.Parameters.AddWithValue("@UserId", userId);
                    deleteItems.Parameters.AddWithValue("@Alias", alias);
                    await deleteItems.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                using (var deleteAlias = new SQLiteCommand(@"
                    DELETE FROM RecapListTable
                    WHERE GuildId = @GuildId
                      AND ChannelId = @ChannelId
                      AND UserId = @UserId
                      AND Alias = @Alias;", conn))
                {
                    deleteAlias.Parameters.AddWithValue("@GuildId", guildId);
                    deleteAlias.Parameters.AddWithValue("@ChannelId", channelId);
                    deleteAlias.Parameters.AddWithValue("@UserId", userId);
                    deleteAlias.Parameters.AddWithValue("@Alias", alias);
                    await deleteAlias.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while deleting: {ex.Message}");
            throw;
        }
    }
}
