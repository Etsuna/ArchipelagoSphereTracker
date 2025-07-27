using System.Data.SQLite;
using System.Text.RegularExpressions;

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

    public static async Task<Dictionary<string, GameStatus>> GetStatusesByNamesAsync(string guildId, string channelId, List<string> names)
    {
        var statuses = new Dictionary<string, GameStatus>();

        if (names == null || names.Count == 0)
            return statuses;

        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                var parameters = string.Join(", ", names.Select((_, i) => $"@Name{i}"));

                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = $@"
                    SELECT Hashtag, Name, Game, Status, Checks, Percent, LastActivity
                    FROM GameStatusTable
                    WHERE GuildId = @GuildId
                      AND ChannelId = @ChannelId
                      AND Name IN ({parameters})";

                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);

                    for (int i = 0; i < names.Count; i++)
                    {
                        command.Parameters.AddWithValue($"@Name{i}", names[i]);
                    }

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var status = new GameStatus
                            {
                                Hashtag = reader["Hashtag"]?.ToString() ?? string.Empty,
                                Name = reader["Name"]?.ToString() ?? string.Empty,
                                Game = reader["Game"]?.ToString() ?? string.Empty,
                                Status = reader["Status"]?.ToString() ?? string.Empty,
                                Checks = reader["Checks"]?.ToString() ?? string.Empty,
                                Percent = reader["Percent"]?.ToString() ?? string.Empty,
                                LastActivity = reader["LastActivity"]?.ToString() ?? string.Empty
                            };

                            statuses[status.Name] = status;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la récupération des GameStatus multiples : {ex.Message}");
        }

        return statuses;
    }

    public static async Task<HashSet<string>> GetExistingGameNamesAsync(string guildId, string channelId, List<string> games)
    {
        var existingGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (games == null || games.Count == 0)
            return existingGames;

        try
        {
            using var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;");
            await connection.OpenAsync();

            var parameterNames = games.Select((_, index) => $"@Game{index}").ToArray();
            var inClause = string.Join(", ", parameterNames);

            var commandText = $@"
            SELECT Game
            FROM GameStatusTable
            WHERE GuildId = @GuildId
              AND ChannelId = @ChannelId
              AND Game IN ({inClause})";

            using var command = new SQLiteCommand(commandText, connection);

            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);

            for (int i = 0; i < games.Count; i++)
            {
                command.Parameters.AddWithValue($"@Game{i}", games[i]);
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var gameName = reader.GetString(0);
                existingGames.Add(gameName);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la récupération des jeux existants : {ex.Message}");
        }

        return existingGames;
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

    public static async Task DeleteDuplicateAliasAsync(string guildId, string channelId)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                var existingNames = new List<string>();
                using (var selectCommand = new SQLiteCommand(connection))
                {
                    selectCommand.CommandText = @"
                    SELECT Name
                    FROM GameStatusTable
                    WHERE GuildId = @GuildId
                      AND ChannelId = @ChannelId";
                    selectCommand.Parameters.AddWithValue("@GuildId", guildId);
                    selectCommand.Parameters.AddWithValue("@ChannelId", channelId);

                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            existingNames.Add(reader.GetString(0));
                        }
                    }
                }

                var namesToDelete = new HashSet<string>();
                foreach (var name in existingNames)
                {
                    var match = Regex.Match(name, @"\((.+?)\)$");
                    if (match.Success)
                    {
                        var baseName = match.Groups[1].Value;
                        if (existingNames.Contains(baseName))
                        {
                            namesToDelete.Add(baseName);
                        }
                    }
                }

                foreach (var nameToDelete in namesToDelete)
                {
                    using (var deleteCommand = new SQLiteCommand(connection))
                    {
                        deleteCommand.CommandText = @"
                        DELETE FROM GameStatusTable
                        WHERE GuildId = @GuildId
                          AND ChannelId = @ChannelId
                          AND Name = @Name";
                        deleteCommand.Parameters.AddWithValue("@GuildId", guildId);
                        deleteCommand.Parameters.AddWithValue("@ChannelId", channelId);
                        deleteCommand.Parameters.AddWithValue("@Name", nameToDelete);
                        await deleteCommand.ExecuteNonQueryAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la suppression des GameStatus : {ex.Message}");
        }
    }
}

