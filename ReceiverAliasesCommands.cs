using System.Data.SQLite;

public static class ReceiverAliasesCommands
{
    // ==========================
    // 🎯 Receiver Aliases
    // ==========================
    public static async Task<List<string>> GetReceiver(string guildId, string channelId)
    {
        var receivers = new List<string>();
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = @"
                SELECT Receiver
                FROM ReceiverAliasesTable
                WHERE GuildId = @GuildId
                  AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var receiver = reader["Receiver"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(receiver))
                            receivers.Add(receiver);
                    }
                }
            }
        }
        return receivers;
    }

    public static async Task<bool> CheckIfReceiverExists(string guildId, string channelId, string receiver)
    {
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = @"
                SELECT COUNT(*)
                FROM ReceiverAliasesTable
                WHERE GuildId = @GuildId
                  AND ChannelId = @ChannelId
                  AND Receiver = @Receiver;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                command.Parameters.AddWithValue("@Receiver", receiver);
                var result = await command.ExecuteScalarAsync();
                var count = (result != null && result != DBNull.Value) ? Convert.ToInt64(result) : 0;
                return count > 0;
            }
        }
    }

    public static async Task<List<string>> GetUserIds(string guildId, string channelId)
    {
        var receivers = new List<string>();
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = @"
                SELECT UserId
                FROM ReceiverAliasesTable
                WHERE GuildId = @GuildId
                  AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var receiver = reader["UserId"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(receiver))
                            receivers.Add(receiver);
                    }
                }
            }
        }
        return receivers;
    }

    // ==========================
    // 🎯 GET RECEIVER USER IDS
    // ==========================
    public static async Task<List<ReceiverUserInfo>> GetReceiverUserIdsAsync(string guildId, string channelId, string receiver)
    {
        var userInfos = new List<ReceiverUserInfo>();

        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();

            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = @"
            SELECT Receiver, UserId, IsEnabled
            FROM ReceiverAliasesTable
            WHERE GuildId = @GuildId
              AND ChannelId = @ChannelId
              AND Receiver = @Receiver;";

                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                command.Parameters.AddWithValue("@Receiver", receiver);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var info = new ReceiverUserInfo()
                        {
                            UserId = reader["UserId"]?.ToString() ?? "",
                            IsEnabled = reader["IsEnabled"] != DBNull.Value && (bool)reader["IsEnabled"],
                        };

                        if (!string.IsNullOrEmpty(info.UserId))
                            userInfos.Add(info);
                    }
                }
            }
        }

        return userInfos;
    }


    // ==========================
    // 🎯 GET ALL USERS IDS
    // ==========================
    public static async Task<List<string>> GetAllUsersIds(string guildId, string channelId, string receiver)
    {
        var UserId = new List<string>();

        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();

            using (var command = new SQLiteCommand(@"
            SELECT UserId
            FROM ReceiverAliasesTable
            WHERE GuildId = @GuildId
              AND ChannelId = @ChannelId
              AND Receiver = @Receiver;", connection))
            {
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                command.Parameters.AddWithValue("@Receiver", receiver);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        UserId.Add(reader.GetString(0));
                    }
                }
            }
        }

        return UserId;
    }

    // ==========================
    // 🎯 DELETE RECEIVER ALIAS
    // ==========================
    public static async Task DeleteReceiverAlias(string guildId, string channelId, string Receiver)
    {
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();
            using (var command = new SQLiteCommand(@"
            DELETE FROM ReceiverAliasesTable
            WHERE GuildId = @GuildId
              AND ChannelId = @ChannelId
              AND Receiver = @Receiver;", connection))
            {
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                command.Parameters.AddWithValue("@Receiver", Receiver);
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    // ==========================
    // 🎯 INSERT RECEIVER ALIAS  
    // ==========================   
    public static async Task InsertReceiverAlias(string guildId, string channelId, string receiver, string userId, bool isEnabled)
    {
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();
            using (var command = new SQLiteCommand(@"
            INSERT INTO ReceiverAliasesTable (GuildId, ChannelId, Receiver, UserId, IsEnabled)
            VALUES (@GuildId, @ChannelId, @Receiver, @UserId, @IsEnabled);", connection))
            {
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                command.Parameters.AddWithValue("@Receiver", receiver);
                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@IsEnabled", isEnabled);
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    public static async Task<Dictionary<string, List<string>>> GetUserAliasesWithItemsAsync(string guildId, string channelId, string userId, string specificAlias = "")
    {
        var aliasesWithItems = new Dictionary<string, List<string>>();

        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();

            var aliasQuery = @"
        SELECT Alias, RecapListTable.Id AS RecapListTableId
        FROM RecapListTable
        WHERE UserId = @UserId
          AND GuildId = @GuildId
          AND ChannelId = @ChannelId";

            using (var command = new SQLiteCommand(aliasQuery, connection))
            {
                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var alias = reader["Alias"].ToString();
                        var recapListTableId = Convert.ToInt64(reader["RecapListTableId"]);

                        if (!string.IsNullOrEmpty(specificAlias) && alias != specificAlias)
                        {
                            continue;
                        }

                        var items = new List<string>();

                        var itemsQuery = @"
                    SELECT Item
                    FROM RecapListItemsTable
                    WHERE RecapListTableId = @RecapListTableId";

                        using (var itemCommand = new SQLiteCommand(itemsQuery, connection))
                        {
                            itemCommand.Parameters.AddWithValue("@RecapListTableId", recapListTableId);

                            using (var itemReader = await itemCommand.ExecuteReaderAsync())
                            {
                                while (await itemReader.ReadAsync())
                                {
                                    var item = itemReader["Item"] as string;
                                    if (!string.IsNullOrEmpty(item))
                                    {
                                        items.Add(item);
                                    }
                                    else
                                    {
                                        items.Add("Aucun élément.");
                                    }
                                }
                            }
                        }

                        if (items.Count > 0)
                        {
                            if (alias == null)
                            {
                                Console.WriteLine($"Aucun alias trouvé pour l'utilisateur : {userId}");
                                continue;
                            }

                            aliasesWithItems[alias] = items;
                        }
                        else
                        {
                            if (alias == null)
                            {
                                Console.WriteLine($"Aucun alias trouvé pour l'utilisateur : {userId}");
                                continue;
                            }

                            aliasesWithItems[alias] = ["Aucun élément."];
                        }
                    }
                }
            }
        }

        return aliasesWithItems;
    }
}
