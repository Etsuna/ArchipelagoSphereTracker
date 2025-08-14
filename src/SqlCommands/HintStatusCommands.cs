using System.Data.SQLite;
using System.Text.RegularExpressions;

public static class HintStatusCommands
{
    public static async Task UpdateHintStatusAsync(string guildId, string channelId, List<HintStatus> hintstatusList)
    {
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();

            using (var transaction = connection.BeginTransaction())
            {
                foreach (var status in hintstatusList)
                {
                    using (var command = new SQLiteCommand(@"
                    UPDATE HintStatusTable
                    SET Found = @Found
                    WHERE GuildId = @GuildId 
                      AND ChannelId = @ChannelId
                      AND Finder = @Finder
                      AND Receiver = @Receiver
                      AND Item = @Item
                      AND Location = @Location
                      AND Game = @Game
                      AND Entrance = @Entrance;", connection, transaction))
                    {
                        command.Parameters.AddWithValue("@GuildId", guildId);
                        command.Parameters.AddWithValue("@ChannelId", channelId);
                        command.Parameters.AddWithValue("@Finder", status.Finder);
                        command.Parameters.AddWithValue("@Receiver", status.Receiver);
                        command.Parameters.AddWithValue("@Item", status.Item);
                        command.Parameters.AddWithValue("@Location", status.Location);
                        command.Parameters.AddWithValue("@Game", status.Game);
                        command.Parameters.AddWithValue("@Entrance", status.Entrance);
                        command.Parameters.AddWithValue("@Found", status.Found);

                        await command.ExecuteNonQueryAsync();
                    }
                }

                transaction.Commit();
            }
        }
    }

    public static async Task<List<HintStatus>> GetHintStatusForReceiver(string guildId, string channelId, string receiverId)
    {
        var hintStatuses = new List<HintStatus>();
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();
            using (var command = new SQLiteCommand(@"
                SELECT Finder, Receiver, Item, Location, Game, Entrance, Found
                FROM HintStatusTable
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Receiver = @Receiver;", connection))
            {
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                command.Parameters.AddWithValue("@Receiver", receiverId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var hintStatus = new HintStatus
                        {
                            Finder = reader["Finder"]?.ToString() ?? string.Empty,
                            Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                            Item = reader["Item"]?.ToString() ?? string.Empty,
                            Location = reader["Location"]?.ToString() ?? string.Empty,
                            Game = reader["Game"]?.ToString() ?? string.Empty,
                            Entrance = reader["Entrance"]?.ToString() ?? string.Empty,
                            Found = reader["Found"]?.ToString() ?? string.Empty
                        };
                        hintStatuses.Add(hintStatus);
                    }
                }
            }
        }
        return hintStatuses;
    }

    public static async Task<List<HintStatus>> GetHintStatusForFinder(string guildId, string channelId, string receiverId)
    {
        var hintStatuses = new List<HintStatus>();
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();
            using (var command = new SQLiteCommand(@"
                SELECT Finder, Receiver, Item, Location, Game, Entrance, Found
                FROM HintStatusTable
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Finder = @Finder;", connection))
            {
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                command.Parameters.AddWithValue("@Finder", receiverId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var hintStatus = new HintStatus
                        {
                            Finder = reader["Finder"]?.ToString() ?? string.Empty,
                            Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                            Item = reader["Item"]?.ToString() ?? string.Empty,
                            Location = reader["Location"]?.ToString() ?? string.Empty,
                            Game = reader["Game"]?.ToString() ?? string.Empty,
                            Entrance = reader["Entrance"]?.ToString() ?? string.Empty,
                            Found = reader["Found"]?.ToString() ?? string.Empty
                        };
                        hintStatuses.Add(hintStatus);
                    }
                }
            }
        }
        return hintStatuses;
    }

    public static async Task<List<HintStatus>> GetHintStatus(string guild, string channel)
    {
        var hintStatuses = new List<HintStatus>();
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();
            using (var command = new SQLiteCommand(@"
                SELECT Finder, Receiver, Item, Location, Game, Entrance, Found
                FROM HintStatusTable
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection))
            {
                command.Parameters.AddWithValue("@GuildId", guild);
                command.Parameters.AddWithValue("@ChannelId", channel);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var hintStatus = new HintStatus
                        {
                            Finder = reader["Finder"]?.ToString() ?? string.Empty,
                            Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                            Item = reader["Item"]?.ToString() ?? string.Empty,
                            Location = reader["Location"]?.ToString() ?? string.Empty,
                            Game = reader["Game"]?.ToString() ?? string.Empty,
                            Entrance = reader["Entrance"]?.ToString() ?? string.Empty,
                            Found = reader["Found"]?.ToString() ?? string.Empty
                        };
                        hintStatuses.Add(hintStatus);
                    }
                }
            }
        }
        return hintStatuses;
    }

    public static async Task AddHintStatusAsync(string guild, string channel, List<HintStatus> hintStatus)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var transaction = (SQLiteTransaction)await connection.BeginTransactionAsync())
                {
                    using (var command = new SQLiteCommand(@"
                        INSERT OR REPLACE INTO HintStatusTable (GuildId, ChannelId, Finder, Receiver, Item, Location, Game, Entrance, Found)
                        VALUES (@GuildId, @ChannelId, @Finder, @Receiver, @Item, @Location, @Game, @Entrance, @Found);", connection, transaction))
                    {
                        command.Parameters.Add("@GuildId", System.Data.DbType.String);
                        command.Parameters.Add("@ChannelId", System.Data.DbType.String);
                        command.Parameters.Add("@Finder", System.Data.DbType.String);
                        command.Parameters.Add("@Receiver", System.Data.DbType.String);
                        command.Parameters.Add("@Item", System.Data.DbType.String);
                        command.Parameters.Add("@Location", System.Data.DbType.String);
                        command.Parameters.Add("@Game", System.Data.DbType.String);
                        command.Parameters.Add("@Entrance", System.Data.DbType.String);
                        command.Parameters.Add("@Found", System.Data.DbType.String);

                        foreach (var status in hintStatus)
                        {
                            command.Parameters["@GuildId"].Value = guild;
                            command.Parameters["@ChannelId"].Value = channel;
                            command.Parameters["@Finder"].Value = status.Finder;
                            command.Parameters["@Receiver"].Value = status.Receiver;
                            command.Parameters["@Item"].Value = status.Item;
                            command.Parameters["@Location"].Value = status.Location;
                            command.Parameters["@Game"].Value = status.Game;
                            command.Parameters["@Entrance"].Value = status.Entrance;
                            command.Parameters["@Found"].Value = status.Found;

                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or replacing hint status: {ex.Message}");
        }
    }

    public static async Task DeleteDuplicateReceiversAliasAsync(string guildId, string channelId)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                var existingReceivers = new List<string>();
                using (var selectCommand = new SQLiteCommand(connection))
                {
                    selectCommand.CommandText = @"
                    SELECT Receiver
                    FROM HintStatusTable
                    WHERE GuildId = @GuildId
                      AND ChannelId = @ChannelId";
                    selectCommand.Parameters.AddWithValue("@GuildId", guildId);
                    selectCommand.Parameters.AddWithValue("@ChannelId", channelId);

                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            existingReceivers.Add(reader.GetString(0));
                        }
                    }
                }

                var receiversToDelete = new HashSet<string>();
                foreach (var name in existingReceivers)
                {
                    var match = Regex.Match(name, @"\((.+?)\)$");
                    if (match.Success)
                    {
                        var baseName = match.Groups[1].Value;
                        if (existingReceivers.Contains(baseName))
                        {
                            receiversToDelete.Add(baseName);
                        }
                    }
                }

                foreach (var receiverToDelete in receiversToDelete)
                {
                    using (var deleteCommand = new SQLiteCommand(connection))
                    {
                        deleteCommand.CommandText = @"
                        DELETE FROM HintStatusTable
                        WHERE GuildId = @GuildId
                          AND ChannelId = @ChannelId
                          AND Receiver = @Receiver";
                        deleteCommand.Parameters.AddWithValue("@GuildId", guildId);
                        deleteCommand.Parameters.AddWithValue("@ChannelId", channelId);
                        deleteCommand.Parameters.AddWithValue("@Receiver", receiverToDelete);
                        await deleteCommand.ExecuteNonQueryAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while deleting HintStatus: {ex.Message}");
        }
    }

    public static async Task DeleteDuplicateFindersAliasAsync(string guildId, string channelId)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                var existingFinders = new List<string>();
                using (var selectCommand = new SQLiteCommand(connection))
                {
                    selectCommand.CommandText = @"
                    SELECT Finder
                    FROM HintStatusTable
                    WHERE GuildId = @GuildId
                      AND ChannelId = @ChannelId";
                    selectCommand.Parameters.AddWithValue("@GuildId", guildId);
                    selectCommand.Parameters.AddWithValue("@ChannelId", channelId);

                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            existingFinders.Add(reader.GetString(0));
                        }
                    }
                }

                var findersToDelete = new HashSet<string>();
                foreach (var name in existingFinders)
                {
                    var match = Regex.Match(name, @"\((.+?)\)$");
                    if (match.Success)
                    {
                        var baseName = match.Groups[1].Value;
                        if (existingFinders.Contains(baseName))
                        {
                            findersToDelete.Add(baseName);
                        }
                    }
                }

                foreach (var finderToDelete in findersToDelete)
                {
                    using (var deleteCommand = new SQLiteCommand(connection))
                    {
                        deleteCommand.CommandText = @"
                        DELETE FROM GameStatusTable
                        WHERE GuildId = @GuildId
                          AND ChannelId = @ChannelId
                          AND Finder = @Finder";
                        deleteCommand.Parameters.AddWithValue("@GuildId", guildId);
                        deleteCommand.Parameters.AddWithValue("@ChannelId", channelId);
                        deleteCommand.Parameters.AddWithValue("@Finder", finderToDelete);
                        await deleteCommand.ExecuteNonQueryAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while deleting HintStatus: {ex.Message}");
        }
    }
}
