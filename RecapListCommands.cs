using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

public static class RecapListCommands
{
    // ==========================
    // 🎯 RecapList
    // ==========================
    public static async Task AddOrEditRecapListAsync(string guildId, string channelId, string userId, string alias)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"INSERT OR REPLACE INTO RecapListTable
                        (GuildId, ChannelId, UserId, Alias)
                        VALUES (@GuildId, @ChannelId, @UserId, @Alias)";
                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@Alias", alias);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'ajout ou mise à jour du RecapList: {ex.Message}");
        }
    }

    // ==========================
    // 🎯 RecapListItems
    // ==========================
    public static async Task AddOrEditRecapListItemsForAllAsync(string guildId, string channelId, List<DisplayedItem> items)
    {
        if (items == null || items.Count == 0)
            return;

        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                foreach (var item in items)
                {
                    long guildChannelId = await DatabaseCommands.GetIdAsync(guildId, channelId, item.Receiver, "RecapListTable");

                    if (guildChannelId == -1)
                    {
                        Console.WriteLine("Erreur : Aucun enregistrement Guild/Channel trouvé. Impossible d'ajouter les items.");
                        return;
                    }

                    using (var transaction = connection.BeginTransaction())
                    {
                        using (var command = new SQLiteCommand(@"
                        INSERT OR REPLACE INTO RecapListItemsTable
                        (RecapListTableId, Item)
                        VALUES (@RecapListTableId, @Item);", connection, transaction))
                        {
                            command.Parameters.AddWithValue("@RecapListTableId", guildChannelId);
                            command.Parameters.AddWithValue("@Item", item.Item);
                            await command.ExecuteNonQueryAsync();
                        }
                        transaction.Commit();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'ajout ou mise à jour des éléments du RecapList : {ex.Message}");
        }
    }

    public static async Task AddOrEditRecapListItemsAsync(string guildId, string channelId, string alias, List<DisplayedItem> items)
    {
        if (items == null || items.Count == 0)
            return;

        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();
                long guildChannelId = await DatabaseCommands.GetIdAsync(guildId, channelId, alias, "RecapListTable");

                if (guildChannelId == -1)
                {
                    Console.WriteLine("Erreur : Aucun enregistrement Guild/Channel trouvé. Impossible d'ajouter les items.");
                    return;
                }

                using (var transaction = connection.BeginTransaction())
                {
                    foreach (var item in items)
                    {
                        using (var command = new SQLiteCommand(@"
                        INSERT OR REPLACE INTO RecapListItemsTable
                        (RecapListTableId, Item)
                        VALUES (@RecapListTableId, @Item);", connection, transaction))
                        {
                            command.Parameters.AddWithValue("@RecapListTableId", guildChannelId);
                            command.Parameters.AddWithValue("@Item", item.Item);
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                    transaction.Commit();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'ajout ou mise à jour des éléments du RecapList : {ex.Message}");
        }
    }

    // ============================
    // 🎯 CHECK IF RecapList EXISTS
    // ============================
    public static async Task<bool> CheckIfExistsWithoutAlias(string guildId, string channelId, string userId)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"SELECT COUNT(*) FROM RecapListTable
                        WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND UserId = @UserId";
                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);
                    command.Parameters.AddWithValue("@UserId", userId);
                    var result = await command.ExecuteScalarAsync();
                    var count = (result != null && result != DBNull.Value) ? Convert.ToInt64(result) : 0;

                    return count > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la vérification de l'existence du RecapList: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> CheckIfExists(string guildId, string channelId, string userId, string alias)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"SELECT COUNT(*) FROM RecapListTable
                        WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND UserId = @UserId AND Alias = @Alias";
                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@Alias", alias);
                    var result = await command.ExecuteScalarAsync();
                    var count = (result != null && result != DBNull.Value) ? Convert.ToInt64(result) : 0;

                    return count > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la vérification de l'existence du RecapList: {ex.Message}");
            return false;
        }
    }

    // ==========================
    // 🎯 DELETE RecapList FOR UserId
    // ==========================
    public static async Task DeleteAliasAndItemsForUserIdAsync(string guildId, string channelId, string userId)
    {
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var deleteItemsCommand = new SQLiteCommand(@"
                    DELETE FROM RecapListItemsTable
                    WHERE RecapListTableId IN (
                        SELECT Id
                        FROM RecapListTable
                        WHERE GuildId = @GuildId
                          AND ChannelId = @ChannelId
                          AND UserId = @UserId
                    );", connection, transaction))
                    {
                        deleteItemsCommand.Parameters.AddWithValue("@GuildId", guildId);
                        deleteItemsCommand.Parameters.AddWithValue("@ChannelId", channelId);
                        deleteItemsCommand.Parameters.AddWithValue("@UserId", userId);

                        await deleteItemsCommand.ExecuteNonQueryAsync();
                    }

                    using (var deleteAliasCommand = new SQLiteCommand(@"
                    DELETE FROM RecapListTable
                    WHERE GuildId = @GuildId
                      AND ChannelId = @ChannelId
                      AND UserId = @UserId;", connection, transaction))
                    {
                        deleteAliasCommand.Parameters.AddWithValue("@GuildId", guildId);
                        deleteAliasCommand.Parameters.AddWithValue("@ChannelId", channelId);
                        deleteAliasCommand.Parameters.AddWithValue("@UserId", userId);

                        await deleteAliasCommand.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Erreur lors de la suppression : {ex.Message}");
                    throw;
                }
            }
        }
    }

    // ==========================
    // 🎯 DELETE RecapList FOR ALIAS
    // ==========================
    public static async Task DeleteRecapListAsync(string guildId, string channelId, string userId, string alias)
    {
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var deleteItemsCommand = new SQLiteCommand(@"
                    DELETE FROM RecapListItemsTable
                    WHERE RecapListTableId IN (
                        SELECT Id
                        FROM RecapListTable
                        WHERE GuildId = @GuildId
                          AND ChannelId = @ChannelId
                          AND UserId = @UserId
                          AND Alias = @Alias
                    );", connection, transaction))
                    {
                        deleteItemsCommand.Parameters.AddWithValue("@GuildId", guildId);
                        deleteItemsCommand.Parameters.AddWithValue("@ChannelId", channelId);
                        deleteItemsCommand.Parameters.AddWithValue("@UserId", userId);
                        deleteItemsCommand.Parameters.AddWithValue("@Alias", alias);

                        await deleteItemsCommand.ExecuteNonQueryAsync();
                    }

                    using (var deleteAliasCommand = new SQLiteCommand(@"
                    DELETE FROM RecapListTable
                    WHERE GuildId = @GuildId
                      AND ChannelId = @ChannelId
                      AND UserId = @UserId
                      AND Alias = @Alias;", connection, transaction))
                    {
                        deleteAliasCommand.Parameters.AddWithValue("@GuildId", guildId);
                        deleteAliasCommand.Parameters.AddWithValue("@ChannelId", channelId);
                        deleteAliasCommand.Parameters.AddWithValue("@UserId", userId);
                        deleteAliasCommand.Parameters.AddWithValue("@Alias", alias);

                        await deleteAliasCommand.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Erreur lors de la suppression : {ex.Message}");
                    throw;
                }
            }
        }
    }

    // ==========================
    // 🎯 DELETE ITEMS LIST FOR ALIAS
    // ==========================
    public static async Task<List<string>> GetAllItemsForAliasAsync(string guildId, string channelId, string userId, string alias)
    {
        var items = new List<string>();
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"SELECT Item FROM RecapListItemsTable
                        WHERE RecapListTableId IN (
                            SELECT Id FROM RecapListTable
                            WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND UserId = @UserId AND Alias = @Alias
                        )";
                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@Alias", alias);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var item = reader["Item"] as string;
                            if (!string.IsNullOrEmpty(item))
                            {
                                items.Add(item);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la récupération des éléments : {ex.Message}");
        }
        return items;
    }



}
