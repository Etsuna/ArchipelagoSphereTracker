using System.Data.SQLite;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

public static class PortalAccessCommands
{
    public static async Task<string> IssuePortalTokenAsync(
        string guildId,
        string channelId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var token = GenerateToken();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(Declare.PortalTokenLifetimeDays);

        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO PortalAccessTable
                    (GuildId, ChannelId, UserId, TokenHash, CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc)
                VALUES
                    (@GuildId, @ChannelId, @UserId, @TokenHash, @CreatedAtUtc, @ExpiresAtUtc, NULL)
                ON CONFLICT(GuildId, ChannelId, UserId) DO UPDATE SET
                    TokenHash = excluded.TokenHash,
                    CreatedAtUtc = excluded.CreatedAtUtc,
                    ExpiresAtUtc = excluded.ExpiresAtUtc,
                    RevokedAtUtc = NULL;";
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@TokenHash", HashToken(token));
            command.Parameters.AddWithValue("@CreatedAtUtc", FormatTimestamp(now));
            command.Parameters.AddWithValue("@ExpiresAtUtc", FormatTimestamp(expiresAt));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        });

        return token;
    }

    public static async Task RevokePortalTokenAsync(
        string guildId,
        string channelId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE PortalAccessTable
                SET RevokedAtUtc = @RevokedAtUtc
                WHERE GuildId = @GuildId
                  AND ChannelId = @ChannelId
                  AND UserId = @UserId
                  AND RevokedAtUtc IS NULL;";
            command.Parameters.AddWithValue("@RevokedAtUtc", FormatTimestamp(DateTimeOffset.UtcNow));
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@UserId", userId);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        });
    }

    public static async Task<string?> GetUserIdByTokenAsync(
        string guildId,
        string channelId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
            SELECT UserId
            FROM PortalAccessTable
            WHERE GuildId = @GuildId
              AND ChannelId = @ChannelId
              AND TokenHash = @TokenHash
              AND RevokedAtUtc IS NULL
              AND ExpiresAtUtc > @NowUtc;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@TokenHash", HashToken(token));
        command.Parameters.AddWithValue("@NowUtc", FormatTimestamp(DateTimeOffset.UtcNow));

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result?.ToString();
    }

    public static string HashToken(string token)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    public static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static string GenerateToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    }
}
