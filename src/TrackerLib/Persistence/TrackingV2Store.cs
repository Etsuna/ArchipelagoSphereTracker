using System.Data.SQLite;
using System.Globalization;
using ArchipelagoSphereTracker.Tracking.V2;

namespace ArchipelagoSphereTracker.Tracking.Persistence;

public sealed record TrackingDestination(string Type, string Id)
{
    public static TrackingDestination DiscordChannel(string channelId) => new("DiscordChannel", channelId);
    public const string NotificationDestinationType = "DiscordNotificationV2";
    public static TrackingDestination DiscordNotification(string channelId) => new(NotificationDestinationType, channelId);
}

public sealed record TrackingV2ApplyResult(
    long SnapshotId,
    bool BaselineCreated,
    bool SnapshotChanged,
    int EventsCreated,
    int DeliveriesCreated,
    IReadOnlyList<NormalizedTrackingEvent>? Events = null);

public enum TrackingV2FaultPoint
{
    SnapshotInserted,
    EventInserted,
    DeliveryInserted
}

public sealed class TrackingV2Store
{
    private const SnapshotSections StateSections =
        SnapshotSections.Slots |
        SnapshotSections.Items |
        SnapshotSections.Hints |
        SnapshotSections.Checks |
        SnapshotSections.Goals |
        SnapshotSections.PlayerStatuses |
        SnapshotSections.RoomActivity;

    private readonly Func<TrackingV2FaultPoint, CancellationToken, Task>? _transactionObserver;

    public TrackingV2Store(Func<TrackingV2FaultPoint, CancellationToken, Task>? transactionObserver = null)
    {
        _transactionObserver = transactionObserver;
    }

    public Task<TrackingV2ApplyResult> ApplySnapshotAsync(
        NormalizedRoomSnapshot snapshot,
        IEnumerable<TrackingDestination>? destinations = null,
        CancellationToken cancellationToken = default,
        bool enableNotifications = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var normalizedDestinations = (destinations ?? [])
            .Where(destination =>
                !string.IsNullOrWhiteSpace(destination.Type) &&
                !string.IsNullOrWhiteSpace(destination.Id))
            .Distinct()
            .ToArray();

        return Db.WriteAsync(async connection =>
        {
            var room = await ReadRoomAsync(connection, snapshot.GuildId, snapshot.ChannelId, cancellationToken);
            var previous = room.Exists && room.BaselineInitialized
                ? await ReadLatestSnapshotAsync(connection, snapshot.GuildId, snapshot.ChannelId, cancellationToken)
                : null;
            var effective = Consolidate(previous, snapshot);
            var now = DateTimeOffset.UtcNow;

            await EnsureRoomAsync(connection, effective, now, cancellationToken);

            // A former shadow snapshot may already have been announced by V1.
            // Establish a fresh baseline atomically when enabling Discord delivery.
            var notificationBaseline = false;
            if (enableNotifications)
            {
                using var activate = connection.CreateCommand();
                activate.CommandText = @"
                    UPDATE TrackedRooms SET NotificationsV2Initialized = 1
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId
                      AND NotificationsV2Initialized = 0;";
                activate.Parameters.AddWithValue("@GuildId", snapshot.GuildId);
                activate.Parameters.AddWithValue("@ChannelId", snapshot.ChannelId);
                notificationBaseline = await activate.ExecuteNonQueryAsync(cancellationToken) == 1;
                // A shadow snapshot may be older than the imported V1 command
                // state. Missing sections stay provisional until observed live.
                if (notificationBaseline) effective = snapshot;
            }

            // Consolidation and notification cutover can replace the input snapshot.
            // Hash the final state once, then reuse the same value for persistence.
            var contentHash = effective.ContentHash;
            if (!notificationBaseline && room.BaselineInitialized &&
                string.Equals(room.CurrentSnapshotHash, contentHash, StringComparison.Ordinal))
            {
                await TouchRoomAsync(connection, effective, now, cancellationToken);
                return new TrackingV2ApplyResult(
                    room.LatestSnapshotId,
                    BaselineCreated: false,
                    SnapshotChanged: false,
                    EventsCreated: 0,
                    DeliveriesCreated: 0);
            }

            var snapshotId = await InsertSnapshotAsync(connection, effective, contentHash, cancellationToken);
            await TrackingReadModel.ApplyAsync(connection, effective, cancellationToken, notificationBaseline ? null : previous);
            await ObserveAsync(TrackingV2FaultPoint.SnapshotInserted, cancellationToken);

            var baselineCreated = notificationBaseline || !room.BaselineInitialized || previous == null;
            var eventsCreated = 0;
            var deliveriesCreated = 0;
            var events = baselineCreated ? [] : TrackingSnapshotDiff.Diff(previous, effective).ToArray();
            var recordedEvents = new List<NormalizedTrackingEvent>();

            if (!baselineCreated)
            {
                foreach (var trackingEvent in events)
                {
                    var eventId = await InsertEventAsync(
                        connection,
                        snapshotId,
                        trackingEvent,
                        now,
                        cancellationToken);
                    if (eventId == null)
                        continue;

                    eventsCreated++;
                    recordedEvents.Add(trackingEvent);
                    if (trackingEvent is ItemReceivedEvent received)
                        await TrackingReadModel.AppendRecapsAsync(connection, received, effective, cancellationToken);
                    await ObserveAsync(TrackingV2FaultPoint.EventInserted, cancellationToken);

                    foreach (var destination in normalizedDestinations)
                    {
                        if (destination.Type == TrackingDestination.NotificationDestinationType &&
                            !IsDiscordNotification(trackingEvent))
                            continue;
                        if (await InsertDeliveryAsync(
                                connection,
                                eventId.Value,
                                destination,
                                now,
                                cancellationToken))
                        {
                            deliveriesCreated++;
                            await ObserveAsync(TrackingV2FaultPoint.DeliveryInserted, cancellationToken);
                        }
                    }
                }
            }

            await SetCurrentSnapshotAsync(connection, effective, contentHash, now, cancellationToken);
            if (recordedEvents.OfType<ItemReceivedEvent>().Any())
            {
                using var activity = connection.CreateCommand();
                activity.CommandText = @"INSERT INTO LastItemsCheckTable(GuildId,ChannelId,LastItemCheck)
                    VALUES(@G,@C,@Now) ON CONFLICT(GuildId,ChannelId) DO UPDATE SET LastItemCheck=excluded.LastItemCheck;";
                activity.Parameters.AddWithValue("@G", effective.GuildId);
                activity.Parameters.AddWithValue("@C", effective.ChannelId);
                activity.Parameters.AddWithValue("@Now", now.ToString("O"));
                await activity.ExecuteNonQueryAsync(cancellationToken);
            }
            return new TrackingV2ApplyResult(
                snapshotId,
                baselineCreated,
                SnapshotChanged: true,
                eventsCreated,
                deliveriesCreated,
                recordedEvents);
        }, cancellationToken);
    }

    internal static bool IsDiscordNotification(NormalizedTrackingEvent trackingEvent)
        => trackingEvent is ItemReceivedEvent or HintCreatedEvent or HintUpdatedEvent or GoalReachedEvent;

    private static NormalizedRoomSnapshot Consolidate(
        NormalizedRoomSnapshot? previous,
        NormalizedRoomSnapshot current)
    {
        if (previous == null)
            return current;

        var carriedSections = previous.CompleteSections & ~current.CompleteSections & StateSections;
        var oldStates = previous.PlayerStates.ToDictionary(value => value.Slot);
        var states = current.PlayerStates.Select(value =>
        {
            if (!oldStates.TryGetValue(value.Slot, out var old)) return value;
            return value with
            {
                Status = current.IsComplete(SnapshotSections.PlayerStatuses) ? value.Status : old.Status,
                CompletedChecks = current.IsComplete(SnapshotSections.Checks) ? value.CompletedChecks : old.CompletedChecks,
                TotalChecks = value.TotalChecks ?? old.TotalChecks,
                ActivityValue = value.ActivityValue ?? old.ActivityValue
            };
        }).ToArray();
        if (states.Length == 0) states = previous.PlayerStates.ToArray();

        return NormalizedRoomSnapshot.Create(
            current.GuildId,
            current.ChannelId,
            current.IsComplete(SnapshotSections.Slots) ? current.Slots : previous.Slots,
            current.IsComplete(SnapshotSections.Items) ? current.Items : previous.Items,
            current.IsComplete(SnapshotSections.Hints) ? current.Hints : previous.Hints,
            current.IsComplete(SnapshotSections.Checks) ? current.Checks : previous.Checks,
            current.IsComplete(SnapshotSections.Goals) ? current.Goals : previous.Goals,
            states,
            current.IsComplete(SnapshotSections.RoomActivity) ? current.LastActivityUtc : previous.LastActivityUtc,
            current.CapturedAtUtc,
            current.LastSuccessfulSyncUtc ?? previous.LastSuccessfulSyncUtc,
            current.TrackingState,
            current.TrackingErrorCode,
            current.CompleteSections | carriedSections);
    }

    private static async Task<RoomState> ReadRoomAsync(
        SQLiteConnection connection,
        string guildId,
        string channelId,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT
                IsBaselineInitialized,
                CurrentSnapshotHash,
                COALESCE((
                    SELECT MAX(snapshot.Id)
                    FROM RoomSnapshots snapshot
                    WHERE snapshot.GuildId = room.GuildId
                      AND snapshot.ChannelId = room.ChannelId
                ), 0) AS LatestSnapshotId
            FROM TrackedRooms room
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new RoomState(false, false, null, 0);

        return new RoomState(
            true,
            Convert.ToInt32(reader["IsBaselineInitialized"], CultureInfo.InvariantCulture) == 1,
            reader["CurrentSnapshotHash"] is DBNull ? null : reader["CurrentSnapshotHash"].ToString(),
            Convert.ToInt64(reader["LatestSnapshotId"], CultureInfo.InvariantCulture));
    }

    private static async Task<NormalizedRoomSnapshot?> ReadLatestSnapshotAsync(
        SQLiteConnection connection,
        string guildId,
        string channelId,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT PayloadJson, PayloadPruned
            FROM RoomSnapshots
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId
            ORDER BY Id DESC
            LIMIT 1;";
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        if (Convert.ToInt32(reader["PayloadPruned"], CultureInfo.InvariantCulture) != 0)
            throw new InvalidOperationException("The current room snapshot payload must not be pruned.");
        return TrackingSnapshotJson.Deserialize(reader.GetString(0));
    }

    private static async Task EnsureRoomAsync(
        SQLiteConnection connection,
        NormalizedRoomSnapshot snapshot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR IGNORE INTO TrackedRooms
                (GuildId, ChannelId, CreatedAtUtc, UpdatedAtUtc, LastSuccessfulSyncUtc,
                 CurrentSnapshotHash, IsBaselineInitialized)
            VALUES
                (@GuildId, @ChannelId, @Now, @Now, @LastSuccessfulSyncUtc, NULL, 0);";
        command.Parameters.AddWithValue("@GuildId", snapshot.GuildId);
        command.Parameters.AddWithValue("@ChannelId", snapshot.ChannelId);
        command.Parameters.AddWithValue("@Now", Format(now));
        command.Parameters.AddWithValue(
            "@LastSuccessfulSyncUtc",
            snapshot.LastSuccessfulSyncUtc is { } synced ? Format(synced) : DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task TouchRoomAsync(
        SQLiteConnection connection,
        NormalizedRoomSnapshot snapshot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE TrackedRooms
            SET UpdatedAtUtc = @Now,
                LastSuccessfulSyncUtc = COALESCE(@LastSuccessfulSyncUtc, LastSuccessfulSyncUtc)
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
        AddRoomParameters(command, snapshot, now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> InsertSnapshotAsync(
        SQLiteConnection connection,
        NormalizedRoomSnapshot snapshot,
        string contentHash,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO RoomSnapshots
                (GuildId, ChannelId, ContentHash, CapturedAtUtc, LastSuccessfulSyncUtc,
                 CompleteSections, TrackingState, PayloadJson)
            VALUES
                (@GuildId, @ChannelId, @ContentHash, @CapturedAtUtc, @LastSuccessfulSyncUtc,
                 @CompleteSections, @TrackingState, @PayloadJson);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("@GuildId", snapshot.GuildId);
        command.Parameters.AddWithValue("@ChannelId", snapshot.ChannelId);
        command.Parameters.AddWithValue("@ContentHash", contentHash);
        command.Parameters.AddWithValue("@CapturedAtUtc", Format(snapshot.CapturedAtUtc));
        command.Parameters.AddWithValue(
            "@LastSuccessfulSyncUtc",
            snapshot.LastSuccessfulSyncUtc is { } synced ? Format(synced) : DBNull.Value);
        command.Parameters.AddWithValue("@CompleteSections", (int)snapshot.CompleteSections);
        command.Parameters.AddWithValue("@TrackingState", snapshot.TrackingState.ToString());
        command.Parameters.AddWithValue("@PayloadJson", TrackingSnapshotJson.Serialize(snapshot));
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
    }

    private static async Task<long?> InsertEventAsync(
        SQLiteConnection connection,
        long snapshotId,
        NormalizedTrackingEvent trackingEvent,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR IGNORE INTO TrackingEvents
                (EventKey, GuildId, ChannelId, EventType, OccurredAtUtc,
                 PayloadJson, SnapshotId, CreatedAtUtc)
            VALUES
                (@EventKey, @GuildId, @ChannelId, @EventType, @OccurredAtUtc,
                 @PayloadJson, @SnapshotId, @CreatedAtUtc);";
        command.Parameters.AddWithValue("@EventKey", trackingEvent.EventKey);
        command.Parameters.AddWithValue("@GuildId", trackingEvent.GuildId);
        command.Parameters.AddWithValue("@ChannelId", trackingEvent.ChannelId);
        command.Parameters.AddWithValue("@EventType", trackingEvent.EventType);
        command.Parameters.AddWithValue("@OccurredAtUtc", Format(trackingEvent.OccurredAtUtc));
        command.Parameters.AddWithValue("@PayloadJson", TrackingSnapshotJson.SerializeEvent(trackingEvent));
        command.Parameters.AddWithValue("@SnapshotId", snapshotId);
        command.Parameters.AddWithValue("@CreatedAtUtc", Format(now));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            return null;

        using var id = connection.CreateCommand();
        id.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(await id.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<bool> InsertDeliveryAsync(
        SQLiteConnection connection,
        long eventId,
        TrackingDestination destination,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR IGNORE INTO EventDeliveries
                (EventId, DestinationType, DestinationId, Status, AttemptCount, NextAttemptAtUtc)
            VALUES
                (@EventId, @DestinationType, @DestinationId, 'Pending', 0, @NextAttemptAtUtc);";
        command.Parameters.AddWithValue("@EventId", eventId);
        command.Parameters.AddWithValue("@DestinationType", destination.Type);
        command.Parameters.AddWithValue("@DestinationId", destination.Id);
        command.Parameters.AddWithValue("@NextAttemptAtUtc", Format(now));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task SetCurrentSnapshotAsync(
        SQLiteConnection connection,
        NormalizedRoomSnapshot snapshot,
        string contentHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE TrackedRooms
            SET UpdatedAtUtc = @Now,
                LastSuccessfulSyncUtc = COALESCE(@LastSuccessfulSyncUtc, LastSuccessfulSyncUtc),
                CurrentSnapshotHash = @CurrentSnapshotHash,
                IsBaselineInitialized = 1
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
        AddRoomParameters(command, snapshot, now);
        command.Parameters.AddWithValue("@CurrentSnapshotHash", contentHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddRoomParameters(
        SQLiteCommand command,
        NormalizedRoomSnapshot snapshot,
        DateTimeOffset now)
    {
        command.Parameters.AddWithValue("@GuildId", snapshot.GuildId);
        command.Parameters.AddWithValue("@ChannelId", snapshot.ChannelId);
        command.Parameters.AddWithValue("@Now", Format(now));
        command.Parameters.AddWithValue(
            "@LastSuccessfulSyncUtc",
            snapshot.LastSuccessfulSyncUtc is { } synced ? Format(synced) : DBNull.Value);
    }

    private Task ObserveAsync(TrackingV2FaultPoint point, CancellationToken cancellationToken)
        => _transactionObserver?.Invoke(point, cancellationToken) ?? Task.CompletedTask;

    internal static string Format(DateTimeOffset value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private sealed record RoomState(
        bool Exists,
        bool BaselineInitialized,
        string? CurrentSnapshotHash,
        long LatestSnapshotId);
}
