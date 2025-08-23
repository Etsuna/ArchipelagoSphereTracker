using System.Data.SQLite;

public static class AliasChoicesCommands
{
    public static async Task<string?> GetGameForAliasAsync(
        string guildId,
        string channelId,
        string alias,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = await Db.OpenReadAsync(ct);

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
            // Facul.: command.Prepare();

            var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
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
        List<Dictionary<string, string>> gameStatus,
        CancellationToken ct = default)
    {
        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var command = conn.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO AliasChoicesTable (GuildId, ChannelId, Alias, Game)
                    VALUES (@GuildId, @ChannelId, @Alias, @Game);";

                // Params fixes
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);

                // Params variables (valeurs changées en boucle)
                var aliasParam = command.Parameters.Add("@Alias", System.Data.DbType.String);
                var gameParam = command.Parameters.Add("@Game", System.Data.DbType.String);

                command.Prepare();

                foreach (var games in gameStatus)
                {
                    foreach (var kv in games)
                    {
                        ct.ThrowIfCancellationRequested();
                        aliasParam.Value = kv.Key;
                        gameParam.Value = kv.Value;
                        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                }
            }, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or replacing the alias choice: {ex.Message}");
        }
    }

    public static async Task<List<string>> GetAliasesForGuildAndChannelAsync(
        string guildId,
        string channelId,
        CancellationToken ct = default)
    {
        var aliases = new List<string>();

        try
        {
            await using var connection = await Db.OpenReadAsync(ct);

            const string query = @"
                SELECT Alias
                FROM AliasChoicesTable
                WHERE GuildId   = @GuildId
                  AND ChannelId = @ChannelId;";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            // Facul.: command.Prepare();

            using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
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
}
