using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;
using Xunit;

public sealed class TrackingDeliveryQueueTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Theory]
    [InlineData("Pending")]
    [InlineData("Failed")]
    [InlineData("Delivering")]
    public async Task Delayed_head_blocks_only_its_own_destination_and_resumes_in_order(string status)
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(0));
        await store.ApplySnapshotAsync(Snapshot(1), [TrackingDestination.DiscordChannel("a"), TrackingDestination.DiscordChannel("b")]);
        await store.ApplySnapshotAsync(Snapshot(2), [TrackingDestination.DiscordChannel("a")]);
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"UPDATE EventDeliveries SET NextAttemptAtUtc=@Past;
                UPDATE EventDeliveries SET Status=@Status, AttemptCount=1,
                NextAttemptAtUtc=@Future, LeaseUntilUtc=@Future
                WHERE Id=(SELECT MIN(Id) FROM EventDeliveries);";
            command.Parameters.AddWithValue("@Status", status);
            command.Parameters.AddWithValue("@Past", Now.AddHours(-1).ToString("O"));
            command.Parameters.AddWithValue("@Future", Now.AddMinutes(1).ToString("O"));
            await command.ExecuteNonQueryAsync();
        });
        var clock = new Clock();
        var publisher = new RecordingPublisher();
        var worker = new TrackingDeliveryWorker(publisher, clock);
        Assert.True(await worker.RunOnceAsync());
        Assert.Equal("b", Assert.Single(publisher.Deliveries).DestinationId);
        Assert.False(await worker.RunOnceAsync());
        clock.Current = Now.AddMinutes(2);
        Assert.True(await worker.RunOnceAsync());
        Assert.True(await worker.RunOnceAsync());
        Assert.False(await worker.RunOnceAsync());
        Assert.Equal(new[] { "b", "a", "a" }, publisher.Deliveries.Select(delivery => delivery.DestinationId));
        Assert.True(publisher.Deliveries[1].DeliveryId < publisher.Deliveries[2].DeliveryId);
        Assert.Equal(2, publisher.Deliveries[1].AttemptNumber);
    }

    [Fact]
    public async Task Startup_on_an_existing_5_0_19_database_restores_the_queue_index_and_planner_uses_it()
    {
        using var scope = new TestDatabaseScope();
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DROP INDEX idx_eventdeliveries_unfinished_destination;";
            await command.ExecuteNonQueryAsync();
        });
        // Index-only upgrade: no new database version or snapshot rewriting is needed.
        await DatabaseInitializer.InitializeDatabaseAsync();
        await DatabaseInitializer.InitializeDatabaseAsync();
        await using var connection = await Db.OpenReadAsync();
        using var explain = connection.CreateCommand();
        explain.CommandText = "EXPLAIN QUERY PLAN " + TrackingDeliveryWorker.SelectNextDeliverySql;
        explain.Parameters.AddWithValue("@Now", Now.ToString("O"));
        explain.Parameters.AddWithValue("@DestinationType", TrackingDestination.NotificationDestinationType);
        using var reader = await explain.ExecuteReaderAsync();
        var plan = new List<string>();
        while (await reader.ReadAsync()) plan.Add(reader["detail"].ToString()!);
        Assert.Contains(plan, detail => detail.Contains("idx_eventdeliveries_unfinished_destination", StringComparison.Ordinal));
        Assert.DoesNotContain(plan, detail => detail.Contains("snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Large_outbox_skips_delivered_history_and_claims_the_oldest_due_event_once()
    {
        using var scope = new TestDatabaseScope();
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot(0));
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                WITH RECURSIVE numbers(n) AS (VALUES(1) UNION ALL SELECT n+1 FROM numbers WHERE n<14583)
                INSERT INTO TrackingEvents(EventKey,GuildId,ChannelId,EventType,OccurredAtUtc,PayloadJson,SnapshotId,CreatedAtUtc)
                SELECT 'event-'||n,'g','c','CheckCompleted',@Past,'{}',1,@Past FROM numbers;
                INSERT INTO EventDeliveries(EventId,DestinationType,DestinationId,Status,AttemptCount,NextAttemptAtUtc)
                SELECT Id,@Type,CAST(Id%82 AS TEXT),CASE WHEN Id<=9433 THEN 'Delivered' ELSE 'Pending' END,0,@Past
                FROM TrackingEvents ORDER BY Id;";
            command.Parameters.AddWithValue("@Past", Now.AddHours(-1).ToString("O"));
            command.Parameters.AddWithValue("@Type", TrackingDestination.NotificationDestinationType);
            await command.ExecuteNonQueryAsync();
        });
        var publisher = new RecordingPublisher();
        var worker = new TrackingDeliveryWorker(publisher, new Clock(), destinationType: TrackingDestination.NotificationDestinationType);
        Assert.True(await worker.RunOnceAsync());
        Assert.True(await worker.RunOnceAsync());
        Assert.Equal(new[] { "event-9434", "event-9435" }, publisher.Deliveries.Select(delivery => delivery.EventKey));
        Assert.All(publisher.Deliveries, delivery =>
        {
            Assert.Empty(delivery.SnapshotPayloadJson);
            Assert.Equal("g", delivery.SnapshotGuildId);
            Assert.Equal("c", delivery.SnapshotChannelId);
            Assert.Equal(1, delivery.SnapshotId);
        });
        Assert.Equal(14583, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    private static NormalizedRoomSnapshot Snapshot(int checks) => NormalizedRoomSnapshot.Create("g", "c",
        [new NormalizedSlot(1, "Player", "Player", "Game")], [], [],
        Enumerable.Range(1, checks).Select(id => new NormalizedCheck(1, id)), [],
        [new NormalizedPlayerState(1, NormalizedPlayerStatus.Playing, checks, 10, null)],
        null, Now, Now, TrackingObservationState.Healthy, null, SnapshotSections.All);

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Current = Now.AddSeconds(1);
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private sealed class RecordingPublisher : ITrackingEventPublisher
    {
        internal readonly List<TrackingDeliveryEnvelope> Deliveries = [];
        public Task<TrackingPublicationResult> PublishAsync(TrackingDeliveryEnvelope delivery, CancellationToken ct)
        {
            Deliveries.Add(delivery);
            return Task.FromResult(new TrackingPublicationResult($"receipt:{delivery.EventKey}"));
        }
    }
}
