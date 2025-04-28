using System.Data.SQLite;
public static class GameStatusCommands
{
    public static async Task<List<GameStatus>> GetGameStatusForGuildAndChannelAsync(string guildId, string channelId)
    {
        var gameStatuses = new List<GameStatus>();

        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"
                    SELECT Hashtag, Name, Game, Status, Checks, Percent, LastActivity
                    FROM GameStatusTable
                    WHERE GuildId = @GuildId
                      AND ChannelId = @ChannelId";

                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var gameStatus = new GameStatus
                            {
                                Hashtag = reader["Hashtag"]?.ToString() ?? string.Empty,
                                Name = reader["Name"]?.ToString() ?? string.Empty,
                                Game = reader["Game"]?.ToString() ?? string.Empty,
                                Status = reader["Status"]?.ToString() ?? string.Empty,
                                Checks = reader["Checks"]?.ToString() ?? string.Empty,
                                Percent = reader["Percent"]?.ToString() ?? string.Empty,
                                LastActivity = reader["LastActivity"]?.ToString() ?? string.Empty
                            };

                            gameStatuses.Add(gameStatus);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la récupération des informations de statut du jeu : {ex.Message}");
        }

        return gameStatuses;
    }

    public static async Task<GameStatus> GetGameStatusByName(string guildId, string channelId, string name)
    {
        GameStatus gameStatus = new GameStatus();
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"
                    SELECT Hashtag, Name, Game, Status, Checks, Percent, LastActivity
                    FROM GameStatusTable
                    WHERE GuildId = @GuildId
                      AND ChannelId = @ChannelId
                      AND Name = @Name";
                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);
                    command.Parameters.AddWithValue("@Name", name);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            gameStatus = new GameStatus
                            {
                                Hashtag = reader["Hashtag"]?.ToString() ?? string.Empty,
                                Name = reader["Name"]?.ToString() ?? string.Empty,
                                Game = reader["Game"]?.ToString() ?? string.Empty,
                                Status = reader["Status"]?.ToString() ?? string.Empty,
                                Checks = reader["Checks"]?.ToString() ?? string.Empty,
                                Percent = reader["Percent"]?.ToString() ?? string.Empty,
                                LastActivity = reader["LastActivity"]?.ToString() ?? string.Empty
                            };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la récupération des informations de statut du jeu : {ex.Message}");
        }
        return gameStatus;
    }

    public static async Task<bool> IsGameExistForGuildAndChannelAsync(string guildId, string channelId, string game)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"
                    SELECT COUNT(1)
                    FROM GameStatusTable
                    WHERE GuildId = @GuildId
                      AND ChannelId = @ChannelId
                      AND Game = @Game";

                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);
                    command.Parameters.AddWithValue("@Game", game);

                    var result = await command.ExecuteScalarAsync();

                    return Convert.ToInt32(result) > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la vérification de l'existence du jeu : {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> IsNameExistForGuildAndChannelAsync(string guildId, string channelId, string name)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"
                    SELECT COUNT(1)
                    FROM GameStatusTable
                    WHERE GuildId = @GuildId
                      AND ChannelId = @ChannelId
                      AND Name = @Name";

                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);
                    command.Parameters.AddWithValue("@Name", name);

                    var result = await command.ExecuteScalarAsync();

                    return Convert.ToInt32(result) > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la vérification de l'existence du jeu : {ex.Message}");
            return false;
        }
    }

    public static async Task AddOrReplaceGameStatusAsync(string guildId, string channelId, List<GameStatus> gameStatuses)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var transaction = await connection.BeginTransactionAsync())
                {
                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                        INSERT OR REPLACE INTO GameStatusTable
                        (GuildId, ChannelId, Hashtag, Name, Game, Status, Checks, Percent, LastActivity)
                        VALUES (@GuildId, @ChannelId, @Hashtag, @Name, @Game, @Status, @Checks, @Percent, @LastActivity)";

                        command.Parameters.Add(new SQLiteParameter("@GuildId", guildId));
                        command.Parameters.Add(new SQLiteParameter("@ChannelId", channelId));
                        command.Parameters.Add(new SQLiteParameter("@Hashtag", System.Data.DbType.String));
                        command.Parameters.Add(new SQLiteParameter("@Name", System.Data.DbType.String));
                        command.Parameters.Add(new SQLiteParameter("@Game", System.Data.DbType.String));
                        command.Parameters.Add(new SQLiteParameter("@Status", System.Data.DbType.String));
                        command.Parameters.Add(new SQLiteParameter("@Checks", System.Data.DbType.String));
                        command.Parameters.Add(new SQLiteParameter("@Percent", System.Data.DbType.String));
                        command.Parameters.Add(new SQLiteParameter("@LastActivity", System.Data.DbType.String));

                        foreach (var gameStatus in gameStatuses)
                        {
                            command.Parameters["@Hashtag"].Value = gameStatus.Hashtag ?? (object)DBNull.Value;
                            command.Parameters["@Name"].Value = gameStatus.Name ?? (object)DBNull.Value;
                            command.Parameters["@Game"].Value = gameStatus.Game ?? (object)DBNull.Value;
                            command.Parameters["@Status"].Value = gameStatus.Status ?? (object)DBNull.Value;
                            command.Parameters["@Checks"].Value = gameStatus.Checks ?? (object)DBNull.Value;
                            command.Parameters["@Percent"].Value = gameStatus.Percent ?? (object)DBNull.Value;
                            command.Parameters["@LastActivity"].Value = gameStatus.LastActivity ?? (object)DBNull.Value;

                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'ajout ou remplacement des GameStatus : {ex.Message}");
        }
    }

    public static async Task UpdateGameStatusBatchAsync(string guildId, string channelId, List<GameStatus> gameStatuses)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var transaction = await connection.BeginTransactionAsync())
                {
                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                    UPDATE GameStatusTable
                    SET Status = @Status,
                        Percent = @Percent,
                        Checks = @Checks,
                        LastActivity = @LastActivity
                    WHERE GuildId = @GuildId
                    AND ChannelId = @ChannelId
                    AND Name = @Name";

                        command.Parameters.Add(new SQLiteParameter("@GuildId", guildId));
                        command.Parameters.Add(new SQLiteParameter("@ChannelId", channelId));
                        command.Parameters.Add(new SQLiteParameter("@Status", System.Data.DbType.String));
                        command.Parameters.Add(new SQLiteParameter("@Percent", System.Data.DbType.String));
                        command.Parameters.Add(new SQLiteParameter("@Checks", System.Data.DbType.String));
                        command.Parameters.Add(new SQLiteParameter("@LastActivity", System.Data.DbType.String));
                        command.Parameters.Add(new SQLiteParameter("@Name", System.Data.DbType.String));

                        foreach (var gameStatus in gameStatuses)
                        {
                            command.Parameters["@Name"].Value = gameStatus.Name ?? (object)DBNull.Value;
                            command.Parameters["@Status"].Value = gameStatus.Status ?? (object)DBNull.Value;
                            command.Parameters["@Percent"].Value = gameStatus.Percent ?? (object)DBNull.Value;
                            command.Parameters["@Checks"].Value = gameStatus.Checks ?? (object)DBNull.Value;
                            command.Parameters["@LastActivity"].Value = gameStatus.LastActivity ?? (object)DBNull.Value;

                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la mise à jour des GameStatus : {ex.Message}");
        }
    }
}

