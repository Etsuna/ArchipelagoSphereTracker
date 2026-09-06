using System.Data.SQLite;

public sealed record AstGuildManagerBinding(
    string GuildId,
    string UserId,
    string GrantedByUserId,
    DateTimeOffset GrantedAtUtc);

public static class AstRoleBindingsCommands
{
    private const string GuildManagerRole = "GuildManager";

    public static async Task<bool> IsGuildManagerAsync(
        string guildId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!IsSnowflake(guildId) || !IsSnowflake(userId))
            return false;

        await using var connection = await Db.OpenReadAsync().ConfigureAwait(false);
        using var command = new SQLiteCommand(@"
            SELECT COUNT(*)
            FROM AstRoleBindingsTable
            WHERE GuildId = @GuildId AND UserId = @UserId AND Role = @Role;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Role", GuildManagerRole);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
    }

    public static async Task<IReadOnlyList<AstGuildManagerBinding>> GetGuildManagersAsync(
        string guildId,
        CancellationToken cancellationToken = default)
    {
        if (!IsSnowflake(guildId))
            return [];

        var bindings = new List<AstGuildManagerBinding>();
        await using var connection = await Db.OpenReadAsync().ConfigureAwait(false);
        using var command = new SQLiteCommand(@"
            SELECT GuildId, UserId, GrantedByUserId, GrantedAtUtc
            FROM AstRoleBindingsTable
            WHERE GuildId = @GuildId AND Role = @Role
            ORDER BY GrantedAtUtc, UserId;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@Role", GuildManagerRole);
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!DateTimeOffset.TryParse(
                    reader.GetString(3),
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var grantedAt))
            {
                continue;
            }

            bindings.Add(new AstGuildManagerBinding(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                grantedAt));
        }

        return bindings;
    }

    public static Task GrantGuildManagerAsync(
        string guildId,
        string userId,
        string grantedByUserId,
        CancellationToken cancellationToken = default)
    {
        ValidateSnowflake(guildId, nameof(guildId));
        ValidateSnowflake(userId, nameof(userId));
        ValidateSnowflake(grantedByUserId, nameof(grantedByUserId));

        return Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO AstRoleBindingsTable
                    (GuildId, UserId, Role, GrantedByUserId, GrantedAtUtc)
                VALUES
                    (@GuildId, @UserId, @Role, @GrantedByUserId, @GrantedAtUtc)
                ON CONFLICT(GuildId, UserId, Role) DO UPDATE SET
                    GrantedByUserId = excluded.GrantedByUserId,
                    GrantedAtUtc = excluded.GrantedAtUtc;";
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Role", GuildManagerRole);
            command.Parameters.AddWithValue("@GrantedByUserId", grantedByUserId);
            command.Parameters.AddWithValue("@GrantedAtUtc", PortalAccessCommands.FormatTimestamp(DateTimeOffset.UtcNow));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        });
    }

    public static Task RevokeGuildManagerAsync(
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
                DELETE FROM AstRoleBindingsTable
                WHERE GuildId = @GuildId AND UserId = @UserId AND Role = @Role;";
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Role", GuildManagerRole);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        });
    }

    private static bool IsSnowflake(string value)
        => ulong.TryParse(value, out var parsed) && parsed > 0;

    private static void ValidateSnowflake(string value, string parameterName)
    {
        if (!IsSnowflake(value))
            throw new ArgumentException("A valid Discord snowflake is required.", parameterName);
    }
}
