using System.Data;
using System.Data.SQLite;
using System.Diagnostics;

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
        List<(int slot, string alias, string game)> gameStatus
        )
    {
        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var command = conn.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO AliasChoicesTable (GuildId, ChannelId, Slot, Alias, Game)
                    VALUES (@GuildId, @ChannelId, @Slot, @Alias, @Game);";

                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);

                var slotParam = command.Parameters.Add("@Slot", System.Data.DbType.Int32);
                var aliasParam = command.Parameters.Add("@Alias", System.Data.DbType.String);
                var gameParam = command.Parameters.Add("@Game", System.Data.DbType.String);

                command.Prepare();

                foreach (var games in gameStatus)
                {
                    command.Parameters.AddWithValue("@Slot", games.slot);
                    command.Parameters.AddWithValue("@Alias", games.alias);
                    command.Parameters.AddWithValue("@Game", games.game);
                    

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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

    public static async Task<Dictionary<string, string>> LoadAliasToGameAsync(
    string guildId,
    string channelId)
    {
        var aliasToGame = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var connection = await Db.OpenReadAsync().ConfigureAwait(false);

            const string query = @"
            SELECT Alias, Game
            FROM AliasChoicesTable
            WHERE GuildId   = @GuildId
              AND ChannelId = @ChannelId;";

            await using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);

            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var alias = reader["Alias"]?.ToString();
                var game = reader["Game"]?.ToString();

                if (!string.IsNullOrWhiteSpace(alias))
                {
                    aliasToGame[alias] = game ?? string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving alias→game mapping: {ex.Message}");
        }

        return aliasToGame;
    }

    public static async Task<List<(string Name, string Game)>> LoadRoomPlayersIndexedAsync(string guildId, string channelId)
    {
        const string sql = @"
            SELECT Slot, Alias, Game
            FROM AliasChoicesTable
            WHERE GuildId=@G AND ChannelId=@C
            ORDER BY Slot;";

        await using var conn = await Db.OpenReadAsync().ConfigureAwait(false);
        await using var cmd = new SQLiteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@G", guildId);
        cmd.Parameters.AddWithValue("@C", channelId);

        var rows = new List<(int Slot, string Alias, string Game)>();
        await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await r.ReadAsync().ConfigureAwait(false))
        {
            var slotLong = (r["Slot"] is long L) ? L : Convert.ToInt64(r["Slot"]);
            var slot = (int)slotLong; // les slots sont petits en pratique (1..n)
            var alias = r["Alias"]?.ToString() ?? $"Player{slot}";
            var game = r["Game"]?.ToString() ?? "";
            rows.Add((slot, alias, game));
        }

        if (rows.Count == 0)
            return new List<(string Name, string Game)>(); // vide → les enrichisseurs fallback sur "PlayerX"

        var maxSlot = 0;
        foreach (var row in rows) if (row.Slot > maxSlot) maxSlot = row.Slot;

        var list = new List<(string Name, string Game)>(new (string, string)[maxSlot]); // rempli de (null,null)
        for (int i = 0; i < maxSlot; i++)
            list[i] = ($"Player{i + 1}", ""); // valeurs par défaut

        foreach (var row in rows)
        {
            var idx = row.Slot - 1;
            if (idx >= 0 && idx < list.Count)
                list[idx] = (row.Alias, row.Game);
        }

        return list;
    }

    
}
