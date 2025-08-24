using System.Data.SQLite;
using System.Text.RegularExpressions;

public static class GameStatusCommands
{
    public static async Task<List<GameStatus>> GetGameStatusForGuildAndChannelAsync(
        string guildId,
        string channelId
        )
    {
        var gameStatuses = new List<GameStatus>();

        try
        {
            await using var connection = await Db.OpenReadAsync();

            using var command = new SQLiteCommand(@"
                SELECT Hashtag, Name, Game, Status, Checks, Percent, LastActivity
                FROM GameStatusTable
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);

            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                gameStatuses.Add(new GameStatus
                {
                    Hashtag = reader["Hashtag"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    Game = reader["Game"]?.ToString() ?? string.Empty,
                    Status = reader["Status"]?.ToString() ?? string.Empty,
                    Checks = reader["Checks"]?.ToString() ?? string.Empty,
                    Percent = reader["Percent"]?.ToString() ?? string.Empty,
                    LastActivity = reader["LastActivity"]?.ToString() ?? string.Empty
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving game status information: {ex.Message}");
        }

        return gameStatuses;
    }

    public static async Task<Dictionary<string, GameStatus>> GetStatusesByNamesAsync(
        string guildId,
        string channelId,
        List<string> names
        )
    {
        var statuses = new Dictionary<string, GameStatus>();
        if (names is null || names.Count == 0) return statuses;

        try
        {
            await using var connection = await Db.OpenReadAsync();

            var paramNames = names.Select((_, i) => $"@Name{i}").ToArray();
            var inClause = string.Join(", ", paramNames);

            using var command = new SQLiteCommand($@"
                SELECT Hashtag, Name, Game, Status, Checks, Percent, LastActivity
                FROM GameStatusTable
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId
                  AND Name IN ({inClause});", connection);

            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            for (int i = 0; i < names.Count; i++)
                command.Parameters.AddWithValue(paramNames[i], names[i]);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
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
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving multiple GameStatus entries: {ex.Message}");
        }

        return statuses;
    }

    public static async Task<HashSet<string>> GetExistingGameNamesAsync(
        string guildId,
        string channelId,
        List<string> games
        )
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (games is null || games.Count == 0) return existing;

        try
        {
            await using var connection = await Db.OpenReadAsync();

            var paramNames = games.Select((_, i) => $"@Game{i}").ToArray();
            var inClause = string.Join(", ", paramNames);

            using var command = new SQLiteCommand($@"
                SELECT Game
                FROM GameStatusTable
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId
                  AND Game IN ({inClause});", connection);

            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            for (int i = 0; i < games.Count; i++)
                command.Parameters.AddWithValue(paramNames[i], games[i]);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                    existing.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving existing games: {ex.Message}");
        }

        return existing;
    }

    public static async Task AddOrReplaceGameStatusAsync(
        string guildId,
        string channelId,
        List<GameStatus> gameStatuses
        )
    {
        if (gameStatuses is null || gameStatuses.Count == 0) return;

        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var command = conn.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO GameStatusTable
                        (GuildId, ChannelId, Hashtag, Name, Game, Status, Checks, Percent, LastActivity)
                    VALUES
                        (@GuildId, @ChannelId, @Hashtag, @Name, @Game, @Status, @Checks, @Percent, @LastActivity);";

                command.Parameters.Add(new SQLiteParameter("@GuildId", guildId));
                command.Parameters.Add(new SQLiteParameter("@ChannelId", channelId));
                command.Parameters.Add(new SQLiteParameter("@Hashtag", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@Name", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@Game", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@Status", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@Checks", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@Percent", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@LastActivity", System.Data.DbType.String));

                command.Prepare();

                foreach (var gs in gameStatuses)
                {
                    command.Parameters["@Hashtag"].Value = (object?)gs.Hashtag ?? DBNull.Value;
                    command.Parameters["@Name"].Value = (object?)gs.Name ?? DBNull.Value;
                    command.Parameters["@Game"].Value = (object?)gs.Game ?? DBNull.Value;
                    command.Parameters["@Status"].Value = (object?)gs.Status ?? DBNull.Value;
                    command.Parameters["@Checks"].Value = (object?)gs.Checks ?? DBNull.Value;
                    command.Parameters["@Percent"].Value = (object?)gs.Percent ?? DBNull.Value;
                    command.Parameters["@LastActivity"].Value = (object?)gs.LastActivity ?? DBNull.Value;

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or replacing GameStatus: {ex.Message}");
        }
    }

    public static async Task UpdateGameStatusBatchAsync(
        string guildId,
        string channelId,
        List<GameStatus> gameStatuses
        )
    {
        if (gameStatuses is null || gameStatuses.Count == 0) return;

        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var command = conn.CreateCommand();
                command.CommandText = @"
                    UPDATE GameStatusTable
                    SET Status = @Status,
                        Percent = @Percent,
                        Checks = @Checks,
                        LastActivity = @LastActivity
                    WHERE GuildId = @GuildId
                      AND ChannelId = @ChannelId
                      AND Name = @Name;";

                command.Parameters.Add(new SQLiteParameter("@GuildId", guildId));
                command.Parameters.Add(new SQLiteParameter("@ChannelId", channelId));
                command.Parameters.Add(new SQLiteParameter("@Status", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@Percent", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@Checks", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@LastActivity", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@Name", System.Data.DbType.String));

                command.Prepare();

                foreach (var gs in gameStatuses)
                {
                    command.Parameters["@Name"].Value = (object?)gs.Name ?? DBNull.Value;
                    command.Parameters["@Status"].Value = (object?)gs.Status ?? DBNull.Value;
                    command.Parameters["@Percent"].Value = (object?)gs.Percent ?? DBNull.Value;
                    command.Parameters["@Checks"].Value = (object?)gs.Checks ?? DBNull.Value;
                    command.Parameters["@LastActivity"].Value = (object?)gs.LastActivity ?? DBNull.Value;

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while updating GameStatus: {ex.Message}");
        }
    }

    public static async Task DeleteDuplicateAliasAsync(
        string guildId,
        string channelId
        )
    {
        try
        {
            // On fait tout dans une écriture transactionnelle (lecture + delete),
            // pour éviter des races avec d'autres writers.
            await Db.WriteAsync(async conn =>
            {
                // 1) Lire tous les Name existants pour ce couple guild/channel
                var existingNames = new List<string>();
                using (var select = new SQLiteCommand(@"
                    SELECT Name
                    FROM GameStatusTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", conn))
                {
                    select.Parameters.AddWithValue("@GuildId", guildId);
                    select.Parameters.AddWithValue("@ChannelId", channelId);

                    using var reader = await select.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        if (!reader.IsDBNull(0))
                            existingNames.Add(reader.GetString(0));
                    }
                }

                // 2) Déterminer les noms à supprimer (ceux qui existent en double sans suffixe "(...)")
                var namesToDelete = new HashSet<string>(StringComparer.Ordinal);
                foreach (var name in existingNames)
                {
                    var match = Regex.Match(name, @"\((.+?)\)$");
                    if (match.Success)
                    {
                        var baseName = match.Groups[1].Value;
                        if (existingNames.Contains(baseName))
                            namesToDelete.Add(baseName);
                    }
                }

                // 3) Supprimer
                using (var delete = new SQLiteCommand(@"
                    DELETE FROM GameStatusTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Name = @Name;", conn))
                {
                    delete.Parameters.AddWithValue("@GuildId", guildId);
                    delete.Parameters.AddWithValue("@ChannelId", channelId);
                    var pName = delete.Parameters.Add("@Name", System.Data.DbType.String);

                    foreach (var name in namesToDelete)
                    {
                        pName.Value = name;
                        await delete.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while deleting GameStatus: {ex.Message}");
        }
    }
}
