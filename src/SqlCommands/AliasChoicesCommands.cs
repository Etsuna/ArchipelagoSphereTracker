using System.Data.SQLite;

public static class AliasChoicesCommands
{
    public static async Task<string?> GetGameForAliasAsync(
        string guildId,
        string channelId,
        string alias
        )
    {
        try
        {
            await using var connection = await Db.OpenReadAsync();

            const string query = @"
                SELECT Game
                FROM AliasChoicesTable
                WHERE GuildId   = @GuildId
                  AND ChannelId = @ChannelId
                  AND Alias LIKE @Alias;";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@Alias", $"%{alias}%");

            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            return result?.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving the Game for the alias: {ex.Message}");
            return null;
        }
    }

    public static async Task AddOrReplaceAliasChoiceAsync(
        string guildId,
        string channelId,
        List<Dictionary<string, string>> gameStatus
        )
    {
        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var command = conn.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO AliasChoicesTable (GuildId, ChannelId, Alias, Game)
                    VALUES (@GuildId, @ChannelId, @Alias, @Game);";

                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);

                var aliasParam = command.Parameters.Add("@Alias", System.Data.DbType.String);
                var gameParam = command.Parameters.Add("@Game", System.Data.DbType.String);

                command.Prepare();

                foreach (var games in gameStatus)
                {
                    foreach (var kv in games)
                    {
                        aliasParam.Value = kv.Key;
                        gameParam.Value = kv.Value;
                        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or replacing the alias choice: {ex.Message}");
        }
    }

    public static async Task<List<string>> GetAliasesForGuildAndChannelAsync(
        string guildId,
        string channelId
        )
    {
        var aliases = new List<string>();

        try
        {
            await using var connection = await Db.OpenReadAsync();

            const string query = @"
                SELECT Alias
                FROM AliasChoicesTable
                WHERE GuildId   = @GuildId
                  AND ChannelId = @ChannelId;";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var alias = reader["Alias"]?.ToString();
                if (!string.IsNullOrEmpty(alias))
                    aliases.Add(alias);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving aliases: {ex.Message}");
        }

        return aliases;
    }

    public static async Task<List<(string Alias, string? Game)>> GetAliasGameListAsync(
    string guildId,
    string channelId
)
    {
        var items = new List<(string Alias, string? Game)>();

        try
        {
            await using var connection = await Db.OpenReadAsync();

            const string query = @"
            SELECT Alias, Game
            FROM AliasChoicesTable
            WHERE GuildId   = @GuildId
              AND ChannelId = @ChannelId
            ORDER BY Alias COLLATE NOCASE;";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var alias = reader.IsDBNull(0) ? null : reader.GetString(0);
                var game = reader.IsDBNull(1) ? null : reader.GetString(1);

                if (!string.IsNullOrEmpty(alias))
                    items.Add((alias!, game));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving alias+game: {ex.Message}");
        }

        return items;
    }
}
