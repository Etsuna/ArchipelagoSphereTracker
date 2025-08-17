using ArchipelagoSphereTracker.src.Resources;
using System.Data.SQLite;

public static class DatabaseCommands
{

    // ==============
    //  ALL COMMANDS
    // ==============

    // ====================
    // 🎯 GET ALL GUILDS
    // ====================
    public static async Task<List<string>> GetAllGuildsAsync(string table)
    {
        var guilds = new List<string>();

        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var command = new SQLiteCommand($@"SELECT GuildId FROM {table}", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            guilds.Add(reader.GetString(0));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving guilds: {ex.Message}");
        }

        return guilds;
    }

    public static async Task<string> ProgramIdentifier(string tableName)
    {
        try
        {
            using var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;");
            await connection.OpenAsync();

            using (var selectCmd = new SQLiteCommand("SELECT ProgramId FROM ProgramIdTable LIMIT 1", connection))
            {
                var result = await selectCmd.ExecuteScalarAsync() as string;
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }

            var newId = Guid.NewGuid().ToString();
            if (Declare.IsDev)
            {
                newId = "AST_TEST";
            }
            else
            {
                newId = Guid.NewGuid().ToString();
            }

            using (var insertCmd = new SQLiteCommand("INSERT INTO ProgramIdTable (ProgramId) VALUES (@ProgramId)", connection))
            {
                insertCmd.Parameters.AddWithValue("@ProgramId", newId);
                await insertCmd.ExecuteNonQueryAsync();
            }

            return newId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetOrCreateProgramIdAsync: {ex.Message}");
            return Guid.NewGuid().ToString();
        }
    }

    // ====================
    // 🎯 GET ALL CHANNELS
    // ====================
    public static async Task<List<string>> GetAllChannelsAsync(string guildId, string table)
    {
        var channels = new List<string>();

        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var command = new SQLiteCommand($@"SELECT ChannelId FROM {table} WHERE GuildId = @GuildId", connection))
                {
                    command.Parameters.AddWithValue("@GuildId", guildId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            channels.Add(reader.GetString(0));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving channels: {ex.Message}");
        }

        return channels;
    }

    // ==========================
    // 🎯 GET DISTINCT GUILDS AND CHANNELS COUNT
    // ==========================
    public static async Task<(int GuildCount, int ChannelCount)> GetDistinctGuildsAndChannelsCountAsync(string table)
    {
        int guildCount = 0;
        int channelCount = 0;

        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                // Count distinct GuildIds
                using (var cmdGuild = new SQLiteCommand($@"SELECT COUNT(DISTINCT GuildId) FROM {table}", connection))
                {
                    var resultGuild = await cmdGuild.ExecuteScalarAsync();
                    if (resultGuild != null && int.TryParse(resultGuild.ToString(), out int parsedGuildCount))
                        guildCount = parsedGuildCount;
                }

                // Count distinct ChannelIds
                using (var cmdChannel = new SQLiteCommand($@"SELECT COUNT(DISTINCT ChannelId) FROM {table}", connection))
                {
                    var resultChannel = await cmdChannel.ExecuteScalarAsync();
                    if (resultChannel != null && int.TryParse(resultChannel.ToString(), out int parsedChannelCount))
                        channelCount = parsedChannelCount;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving counts: {ex.Message}");
        }

        return (guildCount, channelCount);
    }

    // ==========================================
    // 🎯 GET ID FROM GUILD AND CHANNEL AND TABLE
    // ==========================================
    public static async Task<long> GetGuildChannelIdAsync(string guildId, string channelId, string table)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var command = new SQLiteCommand($@"SELECT Id FROM {table}
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId", connection))
                {
                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);

                    var result = await command.ExecuteScalarAsync();
                    if (result != null)
                    {
                        return (long)result;
                    }
                    else
                    {
                        Console.WriteLine("No record found for the specified GuildId and ChannelId.");
                        return -1;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving the channel ID: {ex.Message}");
            return -1;
        }
    }

    public static async Task<List<long>> GetIdsAsync(string guildId, string channelId, string alias, string table)
    {
        var ids = new List<long>();

        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var command = new SQLiteCommand($@"
                SELECT Id FROM {table}
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Alias = @Alias", connection))
                {
                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);
                    command.Parameters.AddWithValue("@Alias", alias);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            ids.Add(reader.GetInt64(0));
                        }
                    }
                }
            }

            return ids;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving IDs: {ex.Message}");
            return new List<long>();
        }
    }

    // ==========================
    // 🎯 CHECK IF CHANNEL EXISTS
    // ==========================
    public static async Task<bool> CheckIfChannelExistsAsync(string guildId, string channelId, string table)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @$"SELECT COUNT(*) FROM {table} WHERE GuildId = @GuildId AND ChannelId = @ChannelId";
                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);

                    var result = await command.ExecuteScalarAsync();
                    var count = (result != null && result != DBNull.Value) ? Convert.ToInt64(result) : 0;

                    return count > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while checking the existence of the channel: {ex.Message}");
            return false;
        }
    }



    // ==========================
    // 🎯 DELETE !
    // ==========================
    public static async Task DeleteChannelDataAsync(string guildId, string channelId)
    {
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                        DELETE FROM UrlAndChannelPatchTable
                        WHERE ChannelsAndUrlsTableId IN (
                            SELECT Id FROM ChannelsAndUrlsTable
                            WHERE GuildId = @GuildId AND ChannelId = @ChannelId
                        );";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        command.Parameters.AddWithValue("@ChannelId", channelId);
                        await command.ExecuteNonQueryAsync();

                        command.Parameters.Clear();

                        command.CommandText = @"
                        DELETE FROM ChannelsAndUrlsTable
                        WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        command.Parameters.AddWithValue("@ChannelId", channelId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                        DELETE FROM ReceiverAliasesTable
                        WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        command.Parameters.AddWithValue("@ChannelId", channelId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                        DELETE FROM RecapListItemsTable
                        WHERE RecapListTableId IN (
                            SELECT Id FROM RecapListTable
                            WHERE GuildId = @GuildId AND ChannelId = @ChannelId
                        );";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        command.Parameters.AddWithValue("@ChannelId", channelId);
                        await command.ExecuteNonQueryAsync();

                        command.Parameters.Clear();

                        command.CommandText = @"
                        DELETE FROM RecapListTable
                        WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        command.Parameters.AddWithValue("@ChannelId", channelId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                        DELETE FROM DisplayedItemTable
                        WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        command.Parameters.AddWithValue("@ChannelId", channelId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                        DELETE FROM AliasChoicesTable
                        WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        command.Parameters.AddWithValue("@ChannelId", channelId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                        DELETE FROM GameStatusTable
                        WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        command.Parameters.AddWithValue("@ChannelId", channelId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                        DELETE FROM HintStatusTable
                        WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        command.Parameters.AddWithValue("@ChannelId", channelId);
                        await command.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                    Console.WriteLine(Resource.DeleteChannelDataAsyncDeleteSuccessful);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while deleting: {ex.Message}");
                    await transaction.RollbackAsync();
                }
            }
        }
    }

    // ==========================
    // 🎯 DELETE BY GUILDID !
    // ==========================
    public static async Task DeleteChannelDataByGuildIdAsync(string guildId)
    {
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                    DELETE FROM UrlAndChannelPatchTable
                    WHERE ChannelsAndUrlsTableId IN (
                        SELECT Id FROM ChannelsAndUrlsTable
                        WHERE GuildId = @GuildId
                    );";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                    DELETE FROM ChannelsAndUrlsTable
                    WHERE GuildId = @GuildId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                    DELETE FROM ReceiverAliasesTable
                    WHERE GuildId = @GuildId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                    DELETE FROM RecapListItemsTable
                    WHERE RecapListTableId IN (
                        SELECT Id FROM RecapListTable
                        WHERE GuildId = @GuildId
                    );";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                    DELETE FROM RecapListTable
                    WHERE GuildId = @GuildId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                        DELETE FROM DisplayedItemTable
                        WHERE GuildId = @GuildId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                    DELETE FROM AliasChoicesTable
                    WHERE GuildId = @GuildId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                    DELETE FROM GameStatusTable
                    WHERE GuildId = @GuildId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        await command.ExecuteNonQueryAsync();
                    }

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                    DELETE FROM HintStatusTable
                    WHERE GuildId = @GuildId;";
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        await command.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                    Console.WriteLine(Resource.DeleteChannelDataByGuildIdAsyncDeletionGuildIdSuccessful);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while deleting by GuildId: {ex.Message}");
                    await transaction.RollbackAsync();
                }
            }
        }
    }
}
