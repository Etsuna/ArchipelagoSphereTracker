using System.Data.SQLite;
using System.Globalization;

namespace ArchipelagoSphereTracker.Tracking.Persistence;

public interface ITrackingDestinationAccessProbe
{
    Task CheckAccessAsync(string guildId, string channelId, CancellationToken cancellationToken);
}

internal sealed class TrackingDestinationAccessException(string errorCode) : InvalidOperationException(errorCode)
{
    internal string ErrorCode { get; } = errorCode;
}

internal sealed record TrackingAccessProbe(string GuildId, string ChannelId, string Token);

internal static class TrackingDeliveryAccessPolicy
{
    internal const int FailureThreshold = 3;
    internal static readonly TimeSpan ProbeInterval = TimeSpan.FromMinutes(15);
    private const string NotificationType = TrackingDestination.NotificationDestinationType;

    internal const string DeliveryTable = @"
CREATE TABLE IF NOT EXISTS EventDeliveries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventId INTEGER NOT NULL,
    DestinationType TEXT NOT NULL,
    DestinationId TEXT NOT NULL,
    Status TEXT NOT NULL CHECK (Status IN ('Pending', 'Delivering', 'Delivered', 'Failed', 'Suspended')),
    AttemptCount INTEGER NOT NULL DEFAULT 0,
    NextAttemptAtUtc TEXT NOT NULL,
    LeaseUntilUtc TEXT,
    LastAttemptAtUtc TEXT,
    DeliveredAtUtc TEXT,
    LastErrorCode TEXT,
    ExternalReceiptId TEXT,
    UNIQUE (EventId, DestinationType, DestinationId),
    FOREIGN KEY (EventId) REFERENCES TrackingEvents(Id) ON DELETE CASCADE
);";

    internal const string Schema = @"
CREATE TABLE IF NOT EXISTS NotificationDeliveryStates (
    GuildId TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    ConsecutiveAccessFailures INTEGER NOT NULL DEFAULT 0,
    FirstFailureAtUtc TEXT NOT NULL,
    LastErrorCode TEXT NOT NULL,
    SuspendedAtUtc TEXT,
    NextAccessCheckAtUtc TEXT,
    LastAccessCheckAtUtc TEXT,
    LastProbeErrorCode TEXT,
    ProbeLeaseUntilUtc TEXT,
    ProbeToken TEXT,
    PRIMARY KEY (GuildId, ChannelId),
    FOREIGN KEY (GuildId, ChannelId) REFERENCES TrackedRooms(GuildId, ChannelId) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_notification_access_due
    ON NotificationDeliveryStates(NextAccessCheckAtUtc) WHERE SuspendedAtUtc IS NOT NULL;";

    internal static bool IsAccessFailure(Exception exception) => exception switch
    {
        TrackingDestinationAccessException => true,
        Discord.Net.HttpException http when (int)http.HttpCode == 403 =>
            http.DiscordCode is { } code && (int)code is 50001 or 50013,
        Discord.Net.HttpException http when (int)http.HttpCode == 404 =>
            http.DiscordCode is { } code && (int)code == 10003,
        _ => false
    };

    internal static Task InitializeAsync(CancellationToken ct = default) => Db.WriteAsync(async connection =>
    {
        var columns = new HashSet<string>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(EventDeliveries);";
        using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) columns.Add(reader["name"].ToString()!);
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='EventDeliveries';";
        var sql = (string?)await command.ExecuteScalarAsync(ct) ?? "";
        if (!sql.Contains("'Suspended'", StringComparison.Ordinal))
            await RebuildDeliveriesAsync(connection, columns, ct);
        command.CommandText = Schema + "DROP INDEX IF EXISTS idx_eventdeliveries_unfinished_destination;" + TrackingDeliveryWorker.Indexes;
        await command.ExecuteNonQueryAsync(ct);
        return 0;
    }, ct);

    private static async Task RebuildDeliveriesAsync(SQLiteConnection connection, HashSet<string> columns, CancellationToken ct)
    {
        string[] knownColumns = ["Id", "EventId", "DestinationType", "DestinationId", "Status", "AttemptCount",
            "NextAttemptAtUtc", "LeaseUntilUtc", "LastAttemptAtUtc", "DeliveredAtUtc", "LastErrorCode", "ExternalReceiptId"];
        if (columns.Except(knownColumns, StringComparer.Ordinal).Any())
            throw new InvalidOperationException("EventDeliveries has unsupported custom columns; migration stopped before rebuilding it.");

        using var command = connection.CreateCommand();
        // Rebuilding must not cascade into an unknown consumer of delivery IDs.
        command.CommandText = @"SELECT child.name FROM sqlite_master child
            JOIN pragma_foreign_key_list(child.name) reference
            WHERE child.type='table' AND reference.[table]='EventDeliveries' LIMIT 1;";
        if (await command.ExecuteScalarAsync(ct) is string consumer)
            throw new InvalidOperationException($"Cannot rebuild EventDeliveries: foreign reference from {consumer}.");

        command.CommandText = "SELECT seq FROM sqlite_sequence WHERE name='EventDeliveries';";
        var sequence = Convert.ToInt64(await command.ExecuteScalarAsync(ct) ?? 0, CultureInfo.InvariantCulture);
        var objects = new List<string>();
        command.CommandText = @"SELECT sql FROM sqlite_master WHERE tbl_name='EventDeliveries'
            AND type IN ('index','trigger') AND sql IS NOT NULL
            AND name <> 'idx_eventdeliveries_unfinished_destination';";
        using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) objects.Add(reader.GetString(0));

        command.CommandText = DeliveryTable.Replace("EventDeliveries", "EventDeliveries_v22", StringComparison.Ordinal) + @"
            INSERT INTO EventDeliveries_v22
                (Id,EventId,DestinationType,DestinationId,Status,AttemptCount,NextAttemptAtUtc,
                 LeaseUntilUtc,LastAttemptAtUtc,DeliveredAtUtc,LastErrorCode,ExternalReceiptId)
            SELECT delivery.Id,delivery.EventId,delivery.DestinationType,delivery.DestinationId,
                   delivery.Status,delivery.AttemptCount,delivery.NextAttemptAtUtc,delivery.LeaseUntilUtc,
                   delivery.LastAttemptAtUtc,delivery.DeliveredAtUtc,delivery.LastErrorCode,delivery.ExternalReceiptId
            FROM EventDeliveries delivery;
            DROP TABLE EventDeliveries;
            ALTER TABLE EventDeliveries_v22 RENAME TO EventDeliveries;";
        await command.ExecuteNonQueryAsync(ct);
        command.Parameters.Clear();
        command.Parameters.AddWithValue("@Sequence", sequence);
        command.CommandText = @"UPDATE sqlite_sequence SET seq=MAX(seq,@Sequence) WHERE name='EventDeliveries';
            INSERT INTO sqlite_sequence(name,seq) SELECT 'EventDeliveries',@Sequence
            WHERE NOT EXISTS (SELECT 1 FROM sqlite_sequence WHERE name='EventDeliveries');";
        await command.ExecuteNonQueryAsync(ct);
        foreach (var definition in objects)
        {
            command.CommandText = definition;
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    internal static async Task<bool> RecordFailureAsync(SQLiteConnection connection, TrackingDeliveryEnvelope delivery,
        bool accessFailure, string errorCode, DateTimeOffset now, CancellationToken ct)
    {
        if (delivery.DestinationType != NotificationType) return false;
        using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("@Guild", delivery.SnapshotGuildId);
        command.Parameters.AddWithValue("@Channel", delivery.SnapshotChannelId);
        if (!accessFailure)
        {
            command.CommandText = "DELETE FROM NotificationDeliveryStates WHERE GuildId=@Guild AND ChannelId=@Channel AND SuspendedAtUtc IS NULL;";
            await command.ExecuteNonQueryAsync(ct);
            await SuspendQueuedDeliveriesAsync(connection, delivery.SnapshotGuildId, delivery.SnapshotChannelId, ct);
            return false;
        }
        command.Parameters.AddWithValue("@Now", TrackingV2Store.Format(now));
        command.Parameters.AddWithValue("@Next", TrackingV2Store.Format(now + ProbeInterval));
        command.Parameters.AddWithValue("@Error", errorCode);
        command.Parameters.AddWithValue("@Threshold", FailureThreshold);
        command.CommandText = @"INSERT INTO NotificationDeliveryStates
                (GuildId,ChannelId,ConsecutiveAccessFailures,FirstFailureAtUtc,LastErrorCode)
            VALUES(@Guild,@Channel,1,@Now,@Error)
            ON CONFLICT(GuildId,ChannelId) DO UPDATE SET
                ConsecutiveAccessFailures=ConsecutiveAccessFailures+1,LastErrorCode=@Error,
                ProbeToken=NULL,ProbeLeaseUntilUtc=NULL;";
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = @"UPDATE NotificationDeliveryStates SET SuspendedAtUtc=@Now,NextAccessCheckAtUtc=@Next
            WHERE GuildId=@Guild AND ChannelId=@Channel AND SuspendedAtUtc IS NULL AND ConsecutiveAccessFailures>=@Threshold;";
        var newlySuspended = await command.ExecuteNonQueryAsync(ct) == 1;
        await SuspendQueuedDeliveriesAsync(connection, delivery.SnapshotGuildId, delivery.SnapshotChannelId, ct);
        return newlySuspended;
    }

    internal static async Task RecordSuccessAsync(SQLiteConnection connection, TrackingDeliveryEnvelope delivery, CancellationToken ct)
    {
        if (delivery.DestinationType != NotificationType) return;
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM NotificationDeliveryStates WHERE GuildId=@Guild AND ChannelId=@Channel AND SuspendedAtUtc IS NULL;";
        command.Parameters.AddWithValue("@Guild", delivery.SnapshotGuildId);
        command.Parameters.AddWithValue("@Channel", delivery.SnapshotChannelId);
        await command.ExecuteNonQueryAsync(ct);
    }

    internal static async Task<bool> IsSuspendedAsync(string guildId, string channelId, CancellationToken ct)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM NotificationDeliveryStates WHERE GuildId=@Guild AND ChannelId=@Channel AND SuspendedAtUtc IS NOT NULL;";
        command.Parameters.AddWithValue("@Guild", guildId);
        command.Parameters.AddWithValue("@Channel", channelId);
        return await command.ExecuteScalarAsync(ct) != null;
    }

    private static async Task SuspendQueuedDeliveriesAsync(SQLiteConnection connection,
        string guild, string channel, CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE EventDeliveries
            SET Status='Suspended',LeaseUntilUtc=NULL
            WHERE DestinationType='DiscordNotificationV2' AND Status IN ('Pending','Failed')
              AND EventId IN (SELECT Id FROM TrackingEvents WHERE GuildId=@Guild AND ChannelId=@Channel)
              AND EXISTS (SELECT 1 FROM NotificationDeliveryStates
                  WHERE GuildId=@Guild AND ChannelId=@Channel AND SuspendedAtUtc IS NOT NULL);";
        command.Parameters.AddWithValue("@Guild", guild);
        command.Parameters.AddWithValue("@Channel", channel);
        await command.ExecuteNonQueryAsync(ct);
    }

    internal static Task ParkExpiredClaimsAsync(DateTimeOffset now, CancellationToken ct)
        => Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            // Let live publishers acknowledge normally. A crashed/expired attempt
            // stays durable and will use the usual receipt recovery on resume.
            command.CommandText = @"UPDATE EventDeliveries SET Status='Suspended',LeaseUntilUtc=NULL
                WHERE DestinationType='DiscordNotificationV2' AND Status='Delivering'
                  AND (LeaseUntilUtc IS NULL OR LeaseUntilUtc<=@Now)
                  AND EXISTS (SELECT 1 FROM NotificationDeliveryStates state
                    JOIN TrackingEvents event ON event.Id=EventDeliveries.EventId
                    WHERE state.GuildId=event.GuildId AND state.ChannelId=event.ChannelId AND state.SuspendedAtUtc IS NOT NULL);";
            command.Parameters.AddWithValue("@Now", TrackingV2Store.Format(now));
            await command.ExecuteNonQueryAsync(ct);
            return 0;
        }, ct);

    internal static Task<TrackingAccessProbe?> ClaimProbeAsync(DateTimeOffset now, CancellationToken ct)
        => Db.WriteAsync<TrackingAccessProbe?>(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT GuildId,ChannelId FROM NotificationDeliveryStates
                WHERE SuspendedAtUtc IS NOT NULL AND NextAccessCheckAtUtc<=@Now
                  AND (ProbeLeaseUntilUtc IS NULL OR ProbeLeaseUntilUtc<=@Now)
                ORDER BY NextAccessCheckAtUtc,GuildId,ChannelId LIMIT 1;";
            command.Parameters.AddWithValue("@Now", TrackingV2Store.Format(now));
            string guild, channel;
            using (var reader = await command.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct)) return null;
                guild = reader.GetString(0);
                channel = reader.GetString(1);
            }
            var token = Guid.NewGuid().ToString("N");
            command.CommandText = @"UPDATE NotificationDeliveryStates SET ProbeLeaseUntilUtc=@Lease,ProbeToken=@Token
                WHERE GuildId=@Guild AND ChannelId=@Channel;";
            command.Parameters.AddWithValue("@Lease", TrackingV2Store.Format(now.AddMinutes(2)));
            command.Parameters.AddWithValue("@Token", token);
            command.Parameters.AddWithValue("@Guild", guild);
            command.Parameters.AddWithValue("@Channel", channel);
            await command.ExecuteNonQueryAsync(ct);
            return new TrackingAccessProbe(guild, channel, token);
        }, ct);

    internal static Task<bool> CompleteProbeAsync(TrackingAccessProbe probe, DateTimeOffset now, string? errorCode, CancellationToken ct)
        => Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM NotificationDeliveryStates WHERE GuildId=@Guild AND ChannelId=@Channel AND ProbeToken=@Token;";
            command.Parameters.AddWithValue("@Guild", probe.GuildId);
            command.Parameters.AddWithValue("@Channel", probe.ChannelId);
            command.Parameters.AddWithValue("@Token", probe.Token);
            if (await command.ExecuteScalarAsync(ct) == null) return false;
            if (errorCode == null)
            {
                // Keep IDs, attempt counts, frozen pages and receipts: resume the
                // durable queue from its oldest unfinished delivery, regardless of age.
                command.CommandText = @"UPDATE EventDeliveries SET Status='Pending',NextAttemptAtUtc=@Now,LeaseUntilUtc=NULL
                    WHERE DestinationType='DiscordNotificationV2' AND Status IN ('Suspended','Failed')
                      AND EventId IN (SELECT Id FROM TrackingEvents WHERE GuildId=@Guild AND ChannelId=@Channel);
                    DELETE FROM NotificationDeliveryStates WHERE GuildId=@Guild AND ChannelId=@Channel AND ProbeToken=@Token;";
            }
            else
            {
                command.CommandText = @"UPDATE NotificationDeliveryStates SET NextAccessCheckAtUtc=@Next,
                    LastAccessCheckAtUtc=@Now,LastProbeErrorCode=@Error,ProbeLeaseUntilUtc=NULL,ProbeToken=NULL
                    WHERE GuildId=@Guild AND ChannelId=@Channel AND ProbeToken=@Token;";
            }
            command.Parameters.AddWithValue("@Now", TrackingV2Store.Format(now));
            command.Parameters.AddWithValue("@Next", TrackingV2Store.Format(now + ProbeInterval));
            command.Parameters.AddWithValue("@Error", (object?)errorCode ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(ct);
            return true;
        }, ct);
}
