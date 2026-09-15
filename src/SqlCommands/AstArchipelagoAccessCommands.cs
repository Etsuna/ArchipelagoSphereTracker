using System.Data.SQLite;

public sealed record AstArchipelagoAccessDenial(
    string GuildId,
    string UserId,
    string DeniedByUserId,
    DateTimeOffset DeniedAtUtc);

public static class AstArchipelagoAccessCommands
{
    public static async Task<bool> IsDeniedAsync(
        string guildId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!IsSnowflake(guildId) || !IsSnowflake(userId))
            return false;

        await using var connection = await Db.OpenReadAsync().ConfigureAwait(false);
        using var command = new SQLiteCommand(@"
            SELECT COUNT(*)
            FROM AstArchipelagoAccessDenyTable
            WHERE GuildId = @GuildId AND UserId = @UserId;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@UserId", userId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
    }

    public static async Task<IReadOnlyList<AstArchipelagoAccessDenial>> GetDeniedUsersAsync(
        string guildId,
        CancellationToken cancellationToken = default)
    {
        if (!IsSnowflake(guildId))
            return [];

        var denials = new List<AstArchipelagoAccessDenial>();
        await using var connection = await Db.OpenReadAsync().ConfigureAwait(false);
        using var command = new SQLiteCommand(@"
            SELECT GuildId, UserId, DeniedByUserId, DeniedAtUtc
            FROM AstArchipelagoAccessDenyTable
            WHERE GuildId = @GuildId
            ORDER BY DeniedAtUtc, UserId;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!DateTimeOffset.TryParse(
                    reader.GetString(3),
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var deniedAt))
            {
                continue;
            }

            denials.Add(new AstArchipelagoAccessDenial(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                deniedAt));
        }

        return denials;
    }

    public static Task DenyAsync(
        string guildId,
        string userId,
        string deniedByUserId,
        CancellationToken cancellationToken = default)
    {
        ValidateSnowflake(guildId, nameof(guildId));
        ValidateSnowflake(userId, nameof(userId));
        ValidateSnowflake(deniedByUserId, nameof(deniedByUserId));

        return Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO AstArchipelagoAccessDenyTable
                    (GuildId, UserId, DeniedByUserId, DeniedAtUtc)
                VALUES
                    (@GuildId, @UserId, @DeniedByUserId, @DeniedAtUtc)
                ON CONFLICT(GuildId, UserId) DO UPDATE SET
                    DeniedByUserId = excluded.DeniedByUserId,
                    DeniedAtUtc = excluded.DeniedAtUtc;";
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@DeniedByUserId", deniedByUserId);
            command.Parameters.AddWithValue("@DeniedAtUtc", PortalAccessCommands.FormatTimestamp(DateTimeOffset.UtcNow));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        });
    }

    public static Task AllowAsync(
        string guildId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ValidateSnowflake(guildId, nameof(guildId));
        ValidateSnowflake(userId, nameof(userId));

        return Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM AstArchipelagoAccessDenyTable
                WHERE GuildId = @GuildId AND UserId = @UserId;";
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@UserId", userId);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        });
    }

    private static bool IsSnowflake(string value)
        => ulong.TryParse(value, out var parsed) && parsed > 0;

    private static void ValidateSnowflake(string value, string parameterName)
    {
        if (!IsSnowflake(value))
            throw new ArgumentException(ArchipelagoSphereTracker.src.Resources.Resource.DiscordSnowflakeRequired, parameterName);
    }
}
