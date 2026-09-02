using System.Data.SQLite;

public enum SecurityAuditSource
{
    Discord,
    Web
}

public enum SecurityAuditOutcome
{
    Started,
    Succeeded,
    Failed,
    Denied
}

public enum SecurityAuditAction
{
    RoomAdd,
    RoomDelete,
    RoomSettingsUpdate,
    PortalAccessIssue,
    PortalAccessRevoke,
    AliasAdd,
    AliasDelete,
    PatchAccess,
    SpoilerUpload,
    YamlUpload,
    YamlDelete,
    YamlCleanup,
    YamlBackup,
    YamlDownload,
    ApworldUpload,
    ApworldBackup,
    Generation,
    DataCleanup
}

public sealed record SecurityAuditEntry(
    long Id,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    SecurityAuditSource Source,
    string ActorUserId,
    string GuildId,
    string? ChannelId,
    SecurityAuditAction Action,
    SecurityAuditOutcome Outcome);

public static class SecurityAuditLog
{
    public static SecurityAuditAction? ForCommand(string commandName)
    {
        return commandName switch
        {
            "add-url" or "ast-setup" => SecurityAuditAction.RoomAdd,
            "delete-url" => SecurityAuditAction.RoomDelete,
            "update-frequency-check" or "update-silent-option" or "excluded-item" or "delete-excluded-item" or
                "ast-sync-now" or "ast-pause" or "ast-resume" or "ast-polling" or "analyze-spoiler-log"
                => SecurityAuditAction.RoomSettingsUpdate,
            "ast-user-portal" or "ast-room-portal" or "ast-portal" => SecurityAuditAction.PortalAccessIssue,
            "add-alias" => SecurityAuditAction.AliasAdd,
            "delete-alias" => SecurityAuditAction.AliasDelete,
            "get-patch" => SecurityAuditAction.PatchAccess,
            "send-spoiler-log" => SecurityAuditAction.SpoilerUpload,
            "send-yaml" => SecurityAuditAction.YamlUpload,
            "delete-yaml" => SecurityAuditAction.YamlDelete,
            "clean-yamls" => SecurityAuditAction.YamlCleanup,
            "backup-yamls" => SecurityAuditAction.YamlBackup,
            "download-yaml" or "download-template" => SecurityAuditAction.YamlDownload,
            "send-apworld" => SecurityAuditAction.ApworldUpload,
            "backup-apworld" => SecurityAuditAction.ApworldBackup,
            "generate" or "test-generate" or "generate-with-zip" => SecurityAuditAction.Generation,
            "clean" or "clean-all" or "recap-and-clean" => SecurityAuditAction.DataCleanup,
            _ => null
        };
    }

    public static async Task WriteAsync(
        string correlationId,
        SecurityAuditSource source,
        string actorUserId,
        string guildId,
        string? channelId,
        SecurityAuditAction action,
        SecurityAuditOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var retentionCutoff = occurredAt.AddDays(-Declare.AuditRetentionDays);

        await Db.WriteAsync(async connection =>
        {
            using (var insert = connection.CreateCommand())
            {
                insert.CommandText = @"
                    INSERT INTO SecurityAuditLogTable
                        (OccurredAtUtc, CorrelationId, Source, ActorUserId, GuildId, ChannelId, Action, Outcome)
                    VALUES
                        (@OccurredAtUtc, @CorrelationId, @Source, @ActorUserId, @GuildId, @ChannelId, @Action, @Outcome);";
                insert.Parameters.AddWithValue("@OccurredAtUtc", PortalAccessCommands.FormatTimestamp(occurredAt));
                insert.Parameters.AddWithValue("@CorrelationId", correlationId);
                insert.Parameters.AddWithValue("@Source", source.ToString());
                insert.Parameters.AddWithValue("@ActorUserId", actorUserId);
                insert.Parameters.AddWithValue("@GuildId", guildId);
                insert.Parameters.AddWithValue("@ChannelId", (object?)channelId ?? DBNull.Value);
                insert.Parameters.AddWithValue("@Action", action.ToString());
                insert.Parameters.AddWithValue("@Outcome", outcome.ToString());
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            using var cleanup = connection.CreateCommand();
            cleanup.CommandText = "DELETE FROM SecurityAuditLogTable WHERE OccurredAtUtc < @RetentionCutoff;";
            cleanup.Parameters.AddWithValue("@RetentionCutoff", PortalAccessCommands.FormatTimestamp(retentionCutoff));
            await cleanup.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        });
    }

    public static async Task<List<SecurityAuditEntry>> GetRecentAsync(
        string guildId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var entries = new List<SecurityAuditEntry>();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
            SELECT Id, OccurredAtUtc, CorrelationId, Source, ActorUserId,
                   GuildId, ChannelId, Action, Outcome
            FROM SecurityAuditLogTable
            WHERE GuildId = @GuildId
            ORDER BY Id DESC
            LIMIT @Limit;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@Limit", safeLimit);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!DateTimeOffset.TryParse(reader["OccurredAtUtc"]?.ToString(), out var occurredAt) ||
                !Enum.TryParse<SecurityAuditSource>(reader["Source"]?.ToString(), out var source) ||
                !Enum.TryParse<SecurityAuditAction>(reader["Action"]?.ToString(), out var action) ||
                !Enum.TryParse<SecurityAuditOutcome>(reader["Outcome"]?.ToString(), out var outcome))
            {
                continue;
            }

            entries.Add(new SecurityAuditEntry(
                Convert.ToInt64(reader["Id"]),
                occurredAt,
                reader["CorrelationId"]?.ToString() ?? string.Empty,
                source,
                reader["ActorUserId"]?.ToString() ?? string.Empty,
                reader["GuildId"]?.ToString() ?? string.Empty,
                reader["ChannelId"] == DBNull.Value ? null : reader["ChannelId"]?.ToString(),
                action,
                outcome));
        }

        return entries;
    }
}

public sealed class SecurityAuditScope : IAsyncDisposable
{
    private readonly string _correlationId;
    private readonly SecurityAuditSource _source;
    private readonly string _actorUserId;
    private readonly string _guildId;
    private readonly string? _channelId;
    private readonly SecurityAuditAction? _action;
    private bool _succeeded;

    private SecurityAuditScope(
        string correlationId,
        SecurityAuditSource source,
        string actorUserId,
        string guildId,
        string? channelId,
        SecurityAuditAction? action)
    {
        _correlationId = correlationId;
        _source = source;
        _actorUserId = actorUserId;
        _guildId = guildId;
        _channelId = channelId;
        _action = action;
    }

    public static async Task<SecurityAuditScope> StartAsync(
        SecurityAuditSource source,
        string actorUserId,
        string guildId,
        string? channelId,
        SecurityAuditAction? action)
    {
        var scope = new SecurityAuditScope(
            Guid.NewGuid().ToString("N"),
            source,
            actorUserId,
            guildId,
            channelId,
            action);

        if (action != null)
        {
            await SecurityAuditLog.WriteAsync(
                scope._correlationId,
                source,
                actorUserId,
                guildId,
                channelId,
                action.Value,
                SecurityAuditOutcome.Started);
        }

        return scope;
    }

    public void Succeed()
    {
        _succeeded = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_action == null)
            return;

        await SecurityAuditLog.WriteAsync(
            _correlationId,
            _source,
            _actorUserId,
            _guildId,
            _channelId,
            _action.Value,
            _succeeded ? SecurityAuditOutcome.Succeeded : SecurityAuditOutcome.Failed);
    }
}
