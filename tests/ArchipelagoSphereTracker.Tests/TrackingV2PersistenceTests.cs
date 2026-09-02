using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;
using Xunit;

public sealed class TrackingV2PersistenceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 30, 10, 0, 0, TimeSpan.Zero);
    private static readonly TrackingDestination Destination = TrackingDestination.DiscordChannel("channel-1");

    [Fact]
    public async Task Baseline_is_silent_and_identical_snapshot_is_not_stored_twice()
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        var baseline = Snapshot(T0);

        var first = await store.ApplySnapshotAsync(baseline, [Destination]);
        var duplicate = await store.ApplySnapshotAsync(Snapshot(T0.AddMinutes(1)), [Destination]);

        Assert.True(first.BaselineCreated);
        Assert.True(first.SnapshotChanged);
        Assert.Equal(0, first.EventsCreated);
        Assert.False(duplicate.SnapshotChanged);
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("RoomSnapshots"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    [Fact]
    public async Task Changed_snapshot_writes_snapshot_events_and_outbox_once()
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(T0), [Destination]);

        var changed = Snapshot(T0.AddMinutes(1), [Item(1001)]);
        var result = await store.ApplySnapshotAsync(changed, [Destination]);
        var replay = await store.ApplySnapshotAsync(changed, [Destination]);

        Assert.Equal(2, result.EventsCreated);
        Assert.Equal(2, result.DeliveriesCreated);
        Assert.False(replay.SnapshotChanged);
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("RoomSnapshots"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    [Fact]
    public async Task Partial_observation_carries_last_complete_section_and_does_not_lose_later_item()
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(T0, [Item(1001)]), [Destination]);

        var partialError = NormalizedRoomSnapshot.Create(
            "guild-1",
            "channel-1",
            [],
            [],
            [],
            [],
            [],
            [],
            null,
            T0.AddMinutes(1),
            T0,
            TrackingObservationState.Error,
            "timeout",
            SnapshotSections.Tracking);
        var errorResult = await store.ApplySnapshotAsync(partialError, [Destination]);

        var recovered = Snapshot(T0.AddMinutes(2), [Item(1001), Item(1002)]);
        var recoveredResult = await store.ApplySnapshotAsync(recovered, [Destination]);

        Assert.Equal(1, errorResult.EventsCreated);
        Assert.Equal(3, recoveredResult.EventsCreated); // recovered + received + sent
        Assert.Equal(4, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
    }

    [Fact]
    public async Task Concurrent_identical_updates_create_one_event_set()
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(T0), [Destination]);
        var changed = Snapshot(T0.AddMinutes(1), [Item(1001)]);

        await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => store.ApplySnapshotAsync(changed, [Destination])));

        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("RoomSnapshots"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    [Theory]
    [InlineData(TrackingV2FaultPoint.SnapshotInserted)]
    [InlineData(TrackingV2FaultPoint.EventInserted)]
    [InlineData(TrackingV2FaultPoint.DeliveryInserted)]
    public async Task Transaction_rolls_back_at_every_crash_boundary(TrackingV2FaultPoint faultPoint)
    {
        using var scope = new TestDatabaseScope();
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot(T0), [Destination]);
        var store = new TrackingV2Store((point, _) =>
            point == faultPoint
                ? Task.FromException(new InvalidOperationException("simulated crash"))
                : Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ApplySnapshotAsync(Snapshot(T0.AddMinutes(1), [Item(1001)]), [Destination]));

        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("RoomSnapshots"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    [Fact]
    public async Task Delivery_retry_reuses_event_key_after_post_publish_failure()
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(T0), [Destination]);
        await store.ApplySnapshotAsync(
            Snapshot(T0.AddMinutes(1), checks: [new NormalizedCheck(1, 5001)]),
            [Destination]);

        var time = new ManualTimeProvider(DateTimeOffset.UtcNow.AddMinutes(1));
        var publisher = new IdempotentPublisher();
        var eventKey = await ScalarAsync<string>("SELECT EventKey FROM TrackingEvents LIMIT 1;");
        publisher.SeedPublished(eventKey); // external publication succeeded before the process crashed

        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE EventDeliveries
                SET Status = 'Delivering',
                    AttemptCount = 1,
                    LastAttemptAtUtc = @LastAttemptAtUtc,
                    LeaseUntilUtc = @LeaseUntilUtc;";
            command.Parameters.AddWithValue(
                "@LastAttemptAtUtc",
                time.GetUtcNow().AddMinutes(-3).ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue(
                "@LeaseUntilUtc",
                time.GetUtcNow().AddMinutes(-1).ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync();
        });

        var retryWorker = new TrackingDeliveryWorker(publisher, time);
        Assert.True(await retryWorker.RunOnceAsync());

        Assert.Equal(1, publisher.Attempts);
        Assert.Single(publisher.UniquePublications);
        Assert.Equal("Delivered", await ScalarAsync<string>(
            "SELECT Status FROM EventDeliveries ORDER BY Id LIMIT 1;"));
        Assert.Equal(2L, await ScalarAsync<long>(
            "SELECT AttemptCount FROM EventDeliveries ORDER BY Id LIMIT 1;"));
    }

    [Fact]
    public async Task Migration_5_0_7_deduplicates_v1_rows_preserves_patches_and_is_idempotent()
    {
        using var scope = new TestDatabaseScope();
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                DROP INDEX uq_channels_guild_channel;
                DROP INDEX uq_hintstatus_unique;

                INSERT INTO ChannelsAndUrlsTable
                    (GuildId, ChannelId, BaseUrl, Room, Tracker, CheckFrequency, Silent, Port)
                VALUES
                    ('g', 'c', 'https://old.example', 'old', 'old', '5m', 0, '1'),
                    ('g', 'c', 'https://new.example', 'new', 'new', '5m', 0, '2');

                INSERT INTO UrlAndChannelPatchTable
                    (ChannelsAndUrlsTableId, Alias, GameName, Patch)
                VALUES
                    ((SELECT MIN(Id) FROM ChannelsAndUrlsTable WHERE GuildId='g' AND ChannelId='c'),
                     'old-slot', 'Old Game', 'old.ap'),
                    ((SELECT MAX(Id) FROM ChannelsAndUrlsTable WHERE GuildId='g' AND ChannelId='c'),
                     'new-slot', 'New Game', 'new.ap');

                INSERT INTO HintStatusTable
                    (GuildId, ChannelId, Finder, Receiver, Item, Location, Game, Entrance, Flag)
                VALUES
                    ('g', 'c', NULL, 'P1', 'Item', 'Loc', 'Game', NULL, 'False'),
                    ('g', 'c', NULL, 'P1', 'Item', 'Loc', 'Game', NULL, 'True');";
            await command.ExecuteNonQueryAsync();
        });

        await DBMigration_5.Migrate_5_0_7();
        await DBMigration_5.Migrate_5_0_7();

        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("ChannelsAndUrlsTable", "g", "c"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT COUNT(*) FROM UrlAndChannelPatchTable;"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("HintStatusTable", "g", "c"));
        Assert.Equal("new", await ScalarAsync<string>(
            "SELECT Room FROM ChannelsAndUrlsTable WHERE GuildId='g' AND ChannelId='c';"));
        Assert.Equal("True", await ScalarAsync<string>(
            "SELECT Flag FROM HintStatusTable WHERE GuildId='g' AND ChannelId='c';"));
    }

    private static NormalizedRoomSnapshot Snapshot(
        DateTimeOffset capturedAt,
        IEnumerable<NormalizedItemTransfer>? items = null,
        IEnumerable<NormalizedCheck>? checks = null)
        => NormalizedRoomSnapshot.Create(
            "guild-1",
            "channel-1",
            [new NormalizedSlot(1, "Player", "Alias", "Game")],
            items,
            [],
            checks,
            [new NormalizedGoal(1, "completion", false)],
            [new NormalizedPlayerState(1, NormalizedPlayerStatus.Playing, 0, 10, null)],
            null,
            capturedAt,
            capturedAt,
            TrackingObservationState.Healthy,
            null,
            SnapshotSections.All);

    private static NormalizedItemTransfer Item(long itemId)
        => new(
            1,
            1,
            itemId,
            itemId + 10_000,
            1,
            "Alias",
            "Alias",
            $"Item {itemId}",
            $"Location {itemId}",
            "Game",
            "Game");

    private static async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(value!, typeof(T));
    }

    private sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
    {
        private DateTimeOffset _current = current;
        public override DateTimeOffset GetUtcNow() => _current;
        public void Advance(TimeSpan duration) => _current = _current.Add(duration);
    }

    private sealed class IdempotentPublisher : ITrackingEventPublisher
    {
        private readonly ConcurrentDictionary<string, byte> _published = new(StringComparer.Ordinal);

        public int Attempts { get; private set; }
        public IReadOnlyCollection<string> UniquePublications => _published.Keys.ToArray();

        public void SeedPublished(string eventKey) => _published.TryAdd(eventKey, 0);

        public Task<TrackingPublicationResult> PublishAsync(
            TrackingDeliveryEnvelope delivery,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Attempts++;
            _published.TryAdd(delivery.EventKey, 0);
            return Task.FromResult(new TrackingPublicationResult($"receipt:{delivery.EventKey}"));
        }
    }
}
