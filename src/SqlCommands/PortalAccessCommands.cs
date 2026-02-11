using System.Data.SQLite;

public static class PortalAccessCommands
{
    public static async Task<string> EnsurePortalTokenAsync(
        string guildId,
        string channelId,
        string userId)
    {
        var existing = await GetPortalTokenAsync(guildId, channelId, userId);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        await Db.WriteGate.WaitAsync();
        try
        {
            await using var conn = await Db.OpenWriteAsync();
            using (var begin = conn.CreateCommand())
            {
                begin.CommandText = "BEGIN IMMEDIATE;";
                begin.ExecuteNonQuery();
            }

            try
            {
                var token = GenerateToken();

                using (var insert = conn.CreateCommand())
                {
                    insert.CommandText = @"
                        INSERT OR IGNORE INTO PortalAccessTable (GuildId, ChannelId, UserId, Token)
                        VALUES (@GuildId, @ChannelId, @UserId, @Token);";
                    insert.Parameters.AddWithValue("@GuildId", guildId);
                    insert.Parameters.AddWithValue("@ChannelId", channelId);
                    insert.Parameters.AddWithValue("@UserId", userId);
                    insert.Parameters.AddWithValue("@Token", token);
                    await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                var persisted = await GetPortalTokenAsync(conn, guildId, channelId, userId);
                if (!string.IsNullOrWhiteSpace(persisted))
                {
                    using var commit = conn.CreateCommand();
                    commit.CommandText = "COMMIT;";
                    commit.ExecuteNonQuery();
                    return persisted;
                }

                token = GenerateToken();
                using (var fallback = conn.CreateCommand())
                {
                    fallback.CommandText = @"
                        INSERT OR IGNORE INTO PortalAccessTable (GuildId, ChannelId, UserId, Token)
                        VALUES (@GuildId, @ChannelId, @UserId, @Token);";
                    fallback.Parameters.AddWithValue("@GuildId", guildId);
                    fallback.Parameters.AddWithValue("@ChannelId", channelId);
                    fallback.Parameters.AddWithValue("@UserId", userId);
                    fallback.Parameters.AddWithValue("@Token", token);
                    await fallback.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                var fallbackToken = await GetPortalTokenAsync(conn, guildId, channelId, userId);
                using (var commit = conn.CreateCommand())
                {
                    commit.CommandText = "COMMIT;";
                    commit.ExecuteNonQuery();
                }

                return fallbackToken ?? token;
            }
            catch
            {
                using var rb = conn.CreateCommand();
                rb.CommandText = "ROLLBACK;";
                rb.ExecuteNonQuery();
                throw;
            }
        }
        finally
        {
            Db.WriteGate.Release();
        }
    }

    public static async Task<string?> GetUserIdByTokenAsync(
        string guildId,
        string channelId,
        string token)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
            SELECT UserId
            FROM PortalAccessTable
            WHERE GuildId = @GuildId
              AND ChannelId = @ChannelId
              AND Token = @Token;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Token", token);

        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return result?.ToString();
    }

    private static async Task<string?> GetPortalTokenAsync(
        string guildId,
        string channelId,
        string userId)
    {
        await using var connection = await Db.OpenReadAsync();
        return await GetPortalTokenAsync(connection, guildId, channelId, userId);
    }

    private static async Task<string?> GetPortalTokenAsync(
        SQLiteConnection connection,
        string guildId,
        string channelId,
        string userId)
    {
        using var command = new SQLiteCommand(@"
            SELECT Token
            FROM PortalAccessTable
            WHERE GuildId = @GuildId
              AND ChannelId = @ChannelId
              AND UserId = @UserId;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@UserId", userId);

        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return result?.ToString();
    }

    private static string GenerateToken()
    {
        return Guid.NewGuid().ToString("N");
    }
}
