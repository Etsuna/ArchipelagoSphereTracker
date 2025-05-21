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
            Console.WriteLine($"Erreur lors de la récupération des guildes : {ex.Message}");
        }

        return guilds;
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
            Console.WriteLine($"Erreur lors de la récupération des canaux : {ex.Message}");
        }

        return channels;
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
                        Console.WriteLine("Aucun enregistrement trouvé pour le GuildId et ChannelId spécifiés.");
                        return -1;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la récupération de l'ID du canal : {ex.Message}");
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
            Console.WriteLine($"Erreur lors de la récupération des IDs : {ex.Message}");
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
            Console.WriteLine($"Erreur lors de la vérification de l'existence du canal: {ex.Message}");
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
                    Console.WriteLine("Suppression réussie !");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur lors de la suppression : {ex.Message}");
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
                    Console.WriteLine("Suppression par GuildId réussie !");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur lors de la suppression par GuildId : {ex.Message}");
                    await transaction.RollbackAsync();
                }
            }
        }
    }
}
