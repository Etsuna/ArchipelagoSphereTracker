using System.Data.SQLite;

public static class AliasChoicesCommands
{
    public static async Task<string?> GetGameForAliasAsync(string guildId, string channelId, string alias)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                var query = @"
            SELECT Game 
            FROM AliasChoicesTable 
            WHERE GuildId = @GuildId 
              AND ChannelId = @ChannelId 
              AND Alias LIKE @Alias";

                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);
                    command.Parameters.AddWithValue("@Alias", $"%{alias}%");

                    var result = await command.ExecuteScalarAsync();

                    return result?.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving the Game for the alias: {ex.Message}");
            return null;
        }
    }

    public static async Task AddOrReplaceAliasChoiceAsync(string guildId, string channelId, List<Dictionary<string, string>> gameStatus)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var transaction = await connection.BeginTransactionAsync())
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"
                    INSERT OR REPLACE INTO AliasChoicesTable (GuildId, ChannelId, Alias, Game)
                    VALUES (@GuildId, @ChannelId, @Alias, @Game)";

                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);
                    var aliasParam = command.Parameters.Add("@Alias", System.Data.DbType.String);
                    var gameParam = command.Parameters.Add("@Game", System.Data.DbType.String);

                    foreach (var games in gameStatus)
                    {
                        foreach (var game in games)
                        {
                            aliasParam.Value = game.Key;
                            gameParam.Value = game.Value;
                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or replacing the alias choice: {ex.Message}");
        }
    }

    public static async Task<List<string>> GetAliasesForGuildAndChannelAsync(string guildId, string channelId)
    {
        var aliases = new List<string>();

        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"
                    SELECT Alias
                    FROM AliasChoicesTable
                    WHERE GuildId = @GuildId
                      AND ChannelId = @ChannelId";

                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var alias = reader["Alias"].ToString();
                            if (!string.IsNullOrEmpty(alias))
                            {
                                aliases.Add(alias);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving aliases: {ex.Message}");
        }

        return aliases;
    }

}
