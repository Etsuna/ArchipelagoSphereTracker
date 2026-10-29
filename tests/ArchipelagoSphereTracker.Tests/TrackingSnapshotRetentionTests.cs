using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;
using Xunit;

public sealed class TrackingSnapshotRetentionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly TrackingDestination Destination = TrackingDestination.DiscordChannel("c");

    [Fact]
    public async Task Delivered_history_releases_payloads_but_preserves_events_batches_and_current_baseline()
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        var first = await store.ApplySnapshotAsync(Snapshot(0), [Destination]);
        var middle = await store.ApplySnapshotAsync(Snapshot(1), [Destination]);
        var latest = await store.ApplySnapshotAsync(Snapshot(2), [Destination]);
        await ExecuteAsync($@"UPDATE EventDeliveries SET Status='Delivered';
            INSERT INTO DiscordNotificationBatches(BatchKey,SnapshotId,ChannelId,PayloadJson)
            VALUES('batch',{middle.SnapshotId},'c','{{""parts"":[]}}');");
        var eventKeys = await ScalarAsync("SELECT group_concat(EventKey) FROM TrackingEvents;");
        var currentHash = await ScalarAsync("SELECT CurrentSnapshotHash FROM TrackedRooms;");

        Assert.Equal(2, await TrackingSnapshotRetention.PruneBatchAsync());
        Assert.Equal("", await ScalarAsync($"SELECT PayloadJson FROM RoomSnapshots WHERE Id={first.SnapshotId};"));
        Assert.Equal("", await ScalarAsync($"SELECT PayloadJson FROM RoomSnapshots WHERE Id={middle.SnapshotId};"));
        var current = TrackingSnapshotJson.Deserialize((string)(await ScalarAsync(
            $"SELECT PayloadJson FROM RoomSnapshots WHERE Id={latest.SnapshotId};"))!);
        Assert.Equal(2, current.Checks.Length);
        Assert.Equal(3, await TestDatabaseScope.CountRowsAsync("RoomSnapshots"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DiscordNotificationBatches"));
        Assert.Equal(eventKeys, await ScalarAsync("SELECT group_concat(EventKey) FROM TrackingEvents;"));
        Assert.Equal(currentHash, await ScalarAsync("SELECT CurrentSnapshotHash FROM TrackedRooms;"));
        Assert.False((await store.ApplySnapshotAsync(Snapshot(2), [Destination])).SnapshotChanged);
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Null(await ScalarAsync("PRAGMA foreign_key_check;"));
        Assert.Equal(0, await TrackingSnapshotRetention.PruneBatchAsync());
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Failed")]
    [InlineData("Delivering")]
    public async Task Every_unfinished_delivery_protects_its_snapshot_even_when_another_destination_is_delivered(string status)
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(0));
        var protectedSnapshot = await store.ApplySnapshotAsync(Snapshot(1),
            [Destination, TrackingDestination.DiscordChannel("other")]);
        await store.ApplySnapshotAsync(Snapshot(2));
        await ExecuteAsync($@"UPDATE EventDeliveries SET Status='Delivered' WHERE DestinationId='other';
            UPDATE EventDeliveries SET Status='{status}' WHERE DestinationId='c';");
        Assert.Equal(1, await TrackingSnapshotRetention.PruneBatchAsync());
        Assert.Equal(0L, await ScalarAsync($"SELECT PayloadPruned FROM RoomSnapshots WHERE Id={protectedSnapshot.SnapshotId};"));
        Assert.NotEmpty((string)(await ScalarAsync($"SELECT PayloadJson FROM RoomSnapshots WHERE Id={protectedSnapshot.SnapshotId};"))!);
        await ExecuteAsync("UPDATE EventDeliveries SET Status='Delivered';");
        Assert.Equal(1, await TrackingSnapshotRetention.PruneBatchAsync());
        Assert.Equal(1L, await ScalarAsync($"SELECT PayloadPruned FROM RoomSnapshots WHERE Id={protectedSnapshot.SnapshotId};"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    [Fact]
    public async Task Latest_payload_is_preserved_for_each_room_and_batches_are_bounded()
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(0, "quiet-room"));
        for (var i = 0; i < 6; i++) await store.ApplySnapshotAsync(Snapshot(i));
        Assert.Equal(2, await TrackingSnapshotRetention.PruneBatchAsync(2));
        Assert.Equal(2, await TrackingSnapshotRetention.PruneBatchAsync(2));
        Assert.Equal(1, await TrackingSnapshotRetention.PruneBatchAsync(2));
        Assert.Equal(0, await TrackingSnapshotRetention.PruneBatchAsync(2));
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM RoomSnapshots WHERE PayloadPruned=0;"));
        Assert.False((await store.ApplySnapshotAsync(Snapshot(0, "quiet-room"))).SnapshotChanged);
        Assert.False((await store.ApplySnapshotAsync(Snapshot(5))).SnapshotChanged);
    }

    [Fact]
    public async Task Migration_from_old_schema_is_idempotent_and_does_not_prune_pending_payloads()
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(0));
        var pending = await store.ApplySnapshotAsync(Snapshot(1), [Destination]);
        await store.ApplySnapshotAsync(Snapshot(2));
        await ExecuteAsync(@"DROP INDEX idx_roomsnapshots_full_payload;
            DROP INDEX idx_trackingevents_snapshot;
            ALTER TABLE RoomSnapshots DROP COLUMN PayloadPruned;");
        await DBMigration_5.Migrate_5_0_19();
        await DBMigration_5.Migrate_5_0_19();
        Assert.Equal(1, await TrackingSnapshotRetention.PruneBatchAsync());
        Assert.Equal(0L, await ScalarAsync($"SELECT PayloadPruned FROM RoomSnapshots WHERE Id={pending.SnapshotId};"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        Assert.Null(await ScalarAsync("PRAGMA foreign_key_check;"));
    }

    [Fact]
    public async Task Failed_batch_rolls_back_all_payload_changes_and_can_be_retried()
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(0));
        var fail = await store.ApplySnapshotAsync(Snapshot(1));
        await store.ApplySnapshotAsync(Snapshot(2));
        await ExecuteAsync($@"CREATE TRIGGER fail_prune BEFORE UPDATE OF PayloadPruned ON RoomSnapshots
            WHEN OLD.Id={fail.SnapshotId} BEGIN SELECT RAISE(ABORT,'simulated'); END;");
        await Assert.ThrowsAnyAsync<Exception>(() => TrackingSnapshotRetention.PruneBatchAsync());
        Assert.Equal(0L, await ScalarAsync("SELECT SUM(PayloadPruned) FROM RoomSnapshots;"));
        await ExecuteAsync("DROP TRIGGER fail_prune;");
        Assert.Equal(2, await TrackingSnapshotRetention.PruneBatchAsync());
    }

    [Fact]
    public async Task Active_publication_keeps_payload_until_delivery_finishes()
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(0));
        var pending = await store.ApplySnapshotAsync(Snapshot(1), [Destination]);
        await store.ApplySnapshotAsync(Snapshot(2));
        var publisher = new BlockingPublisher();
        var deliveryTask = new TrackingDeliveryWorker(publisher).RunOnceAsync();
        await publisher.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.Equal(1, await TrackingSnapshotRetention.PruneBatchAsync());
            Assert.Equal(0L, await ScalarAsync($"SELECT PayloadPruned FROM RoomSnapshots WHERE Id={pending.SnapshotId};"));
        }
        finally { publisher.Finish.TrySetResult(true); }
        Assert.True(await deliveryTask);
        Assert.Equal("Delivered", await ScalarAsync("SELECT Status FROM EventDeliveries;"));
        Assert.Equal(1, await TrackingSnapshotRetention.PruneBatchAsync());
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
    }

    [Fact]
    public async Task Activity_only_history_releases_large_payloads_and_sqlite_can_reuse_the_pages()
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        var largeName = new string('x', 256 * 1024);
        for (var i = 0; i < 12; i++) await store.ApplySnapshotAsync(Snapshot(0, name: largeName, activity: i.ToString()));
        var bytesBefore = Convert.ToInt64(await ScalarAsync("SELECT SUM(length(PayloadJson)) FROM RoomSnapshots;"));
        Assert.True(bytesBefore > 3_000_000);
        while (await TrackingSnapshotRetention.PruneBatchAsync(3) > 0) { }
        var bytesAfter = Convert.ToInt64(await ScalarAsync("SELECT SUM(length(PayloadJson)) FROM RoomSnapshots;"));
        Assert.True(bytesAfter < bytesBefore / 10);
        Assert.True(Convert.ToInt64(await ScalarAsync("PRAGMA freelist_count;")) > 0);
        Assert.Equal(12, await TestDatabaseScope.CountRowsAsync("RoomSnapshots"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.False((await store.ApplySnapshotAsync(Snapshot(0, name: largeName, activity: "11"))).SnapshotChanged);
    }

    private static NormalizedRoomSnapshot Snapshot(int checks, string channel = "c", string name = "Player", string? activity = null)
        => NormalizedRoomSnapshot.Create("g", channel,
            [new NormalizedSlot(1, name, name, "Game")], [], [],
            Enumerable.Range(1, checks).Select(id => new NormalizedCheck(1, id)),
            [new NormalizedGoal(1, "completion", false)],
            [new NormalizedPlayerState(1, NormalizedPlayerStatus.Playing, checks, 100, activity)],
            null, T0.AddMinutes(checks), T0.AddMinutes(checks), TrackingObservationState.Healthy, null, SnapshotSections.All);

    private static Task ExecuteAsync(string sql) => Db.WriteAsync(async connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    });

    private static async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }

    private sealed class BlockingPublisher : ITrackingEventPublisher
    {
        internal readonly TaskCompletionSource<bool> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Finish = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<TrackingPublicationResult> PublishAsync(TrackingDeliveryEnvelope delivery, CancellationToken ct)
        {
            Assert.Single(TrackingSnapshotJson.Deserialize(delivery.SnapshotPayloadJson).Checks);
            Started.TrySetResult(true);
            await Finish.Task.WaitAsync(ct);
            return new TrackingPublicationResult("receipt");
        }
    }
}
