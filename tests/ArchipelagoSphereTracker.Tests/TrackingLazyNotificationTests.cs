using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.Tracking.Notifications;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;
using Xunit;

public sealed class TrackingLazyNotificationTests
{
    private static readonly TrackingDestination Destination = TrackingDestination.DiscordNotification("456");

    [Theory]
    [InlineData(20)]
    [InlineData(100)]
    public async Task Shared_batch_reads_the_large_snapshot_once_across_worker_restarts(int itemCount)
    {
        using var database = new TestDatabaseScope();
        await QueueAsync(itemCount);
        var transport = new Transport();
        using var reads = new SnapshotReads();
        for (var i = 0; i < itemCount; i++)
            Assert.True(await Worker(transport).RunOnceAsync());

        Assert.False(await Worker(transport).RunOnceAsync());
        Assert.Equal(1, reads.Count);
        Assert.Equal(itemCount, await ScalarAsync("SELECT COUNT(*) FROM EventDeliveries WHERE Status='Delivered';"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DiscordNotificationBatches"));
        Assert.NotEmpty(transport.Messages);
        Assert.Equal(transport.Messages.Count, transport.SendCalls);
    }

    [Fact]
    public async Task Partial_uncertain_send_recovers_frozen_pages_without_reading_the_snapshot_again()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync(40);
        var transport = new Transport { LoseResponseOnCall = 2 };
        var clock = new Clock();
        using var reads = new SnapshotReads();
        Assert.True(await Worker(transport, clock).RunOnceAsync());
        Assert.Equal(2, transport.SendCalls);
        Assert.Equal(1, reads.Count);
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM EventDeliveries WHERE Status='Failed';"));

        // A malformed body makes any accidental deserialization fail. The frozen batch
        // must survive a new worker/publisher and recover the uncertain second page.
        await ExecuteAsync("UPDATE RoomSnapshots SET PayloadJson='not valid JSON' WHERE Id=(SELECT MAX(Id) FROM RoomSnapshots);");
        clock.Current = clock.Current.AddMinutes(1);
        while (await Worker(transport, clock).RunOnceAsync()) { }
        Assert.Equal(1, reads.Count);
        Assert.Equal(1, transport.FindCalls);
        Assert.Equal(transport.Messages.Count, transport.SendCalls);
        Assert.Equal(40, await ScalarAsync("SELECT COUNT(*) FROM EventDeliveries WHERE Status='Delivered';"));
    }

    [Fact]
    public async Task New_batch_requires_a_valid_snapshot_and_keeps_failed_deliveries_retryable()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync(1);
        await ExecuteAsync("UPDATE RoomSnapshots SET PayloadJson='not valid JSON' WHERE Id=(SELECT MAX(Id) FROM RoomSnapshots);");
        var transport = new Transport();
        using var reads = new SnapshotReads();
        Assert.True(await Worker(transport).RunOnceAsync());
        Assert.Equal(1, reads.Count);
        Assert.Empty(transport.Messages);
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("DiscordNotificationBatches"));
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM EventDeliveries WHERE Status='Failed';"));
    }

    [Fact]
    public async Task Frozen_batch_does_not_bypass_event_scope_validation()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync(2);
        var transport = new Transport();
        Assert.True(await Worker(transport).RunOnceAsync());
        var sends = transport.SendCalls;
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE TrackingEvents SET PayloadJson=@Payload WHERE Id=(SELECT EventId FROM EventDeliveries WHERE Status='Pending' LIMIT 1);";
            command.Parameters.AddWithValue("@Payload", TrackingSnapshotJson.SerializeEvent(
                new ItemReceivedEvent("other-guild", "456", DateTimeOffset.UtcNow, Item(2))));
            await command.ExecuteNonQueryAsync();
        });
        using var reads = new SnapshotReads();
        Assert.True(await Worker(transport).RunOnceAsync());
        Assert.Equal(0, reads.Count);
        Assert.Equal(sends, transport.SendCalls);
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM EventDeliveries WHERE Status='Failed';"));
    }

    [Fact]
    public async Task Deferred_load_is_protected_from_retention_and_rejects_a_stale_attempt()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync(1);
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot(2), [Destination], enableNotifications: true);
        var publisher = new InspectingPublisher(async (delivery, ct) =>
        {
            Assert.Empty(delivery.SnapshotPayloadJson);
            Assert.Equal(1, await TrackingSnapshotRetention.PruneBatchAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                (delivery with { AttemptNumber = delivery.AttemptNumber + 1 }).ReadSnapshotPayloadAsync(ct));
            var snapshot = TrackingSnapshotJson.Deserialize(await delivery.ReadSnapshotPayloadAsync(ct));
            Assert.Single(snapshot.Items);
            return new TrackingPublicationResult("receipt");
        });
        Assert.True(await new TrackingDeliveryWorker(publisher, new Clock(),
            destinationType: TrackingDestination.NotificationDestinationType).RunOnceAsync());
        Assert.Equal(1, await TrackingSnapshotRetention.PruneBatchAsync());
    }

    private static async Task QueueAsync(int count)
    {
        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync("123", "456", "https://example.test", "room", "tracker", false, "5m", "0");
        await AliasChoicesCommands.AddOrReplaceAliasChoiceAsync("123", "456", [(1, "Alice", "Game"), (2, "Bob", "Game")]);
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(0), [Destination], enableNotifications: true);
        await store.ApplySnapshotAsync(Snapshot(count), [Destination], enableNotifications: true);
    }

    private static NormalizedItemTransfer Item(int index) => new(2, 1, 100 + index, 200 + index, 1,
        "Bob", "Alice", $"Sword {index} {new string('x', 100)}", "Castle", "Game", "Game");

    private static NormalizedRoomSnapshot Snapshot(int count) => NormalizedRoomSnapshot.Create("123", "456",
        [new(1, "Alice", "Alice", "Game"), new(2, "Bob", "Bob", "Game")],
        Enumerable.Range(1, count).Select(Item), [], [], [],
        [new(1, NormalizedPlayerStatus.Playing, 0, 100, new string('x', 1024 * 1024))],
        null, DateTimeOffset.UtcNow, null, TrackingObservationState.Healthy, null, SnapshotSections.All);

    private static TrackingDeliveryWorker Worker(Transport transport, Clock? clock = null)
        => new(new DiscordTrackingEventPublisher(transport), clock ?? new Clock(),
            destinationType: TrackingDestination.NotificationDestinationType);

    private static Task ExecuteAsync(string sql) => Db.WriteAsync(async connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    });

    private static async Task<long> ScalarAsync(string sql)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Current = DateTimeOffset.UtcNow.AddMinutes(1);
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private sealed class InspectingPublisher(Func<TrackingDeliveryEnvelope, CancellationToken, Task<TrackingPublicationResult>> publish)
        : ITrackingEventPublisher
    {
        public Task<TrackingPublicationResult> PublishAsync(TrackingDeliveryEnvelope delivery, CancellationToken ct)
            => publish(delivery, ct);
    }

    // Observe actual SQL column access rather than inferring reads from timing.
    private sealed class SnapshotReads : IDisposable
    {
        public int Count;
        public SnapshotReads() => SQLiteConnection.Changed += OnConnectionChanged;
        private void OnConnectionChanged(object sender, ConnectionEventArgs args)
        {
            if (args.EventType == SQLiteConnectionEventType.Opened && sender is SQLiteConnection connection)
                connection.Authorize += OnAuthorize;
        }
        private void OnAuthorize(object sender, AuthorizerEventArgs args)
        {
            if (args.ActionCode == SQLiteAuthorizerActionCode.Read &&
                args.Argument1 == "RoomSnapshots" && args.Argument2 == "PayloadJson") Count++;
        }
        public void Dispose() => SQLiteConnection.Changed -= OnConnectionChanged;
    }

    private sealed class Transport : ITrackingDiscordTransport
    {
        public readonly Dictionary<string, string> Messages = [];
        public int SendCalls;
        public int FindCalls;
        public int LoseResponseOnCall;
        public Task<string?> FindPublicationAsync(string guild, string channel, string key, DateTimeOffset notBefore,
            TrackingDiscordNotification notification, CancellationToken ct)
        {
            FindCalls++;
            return Task.FromResult(Messages.GetValueOrDefault(key));
        }
        public Task<string> SendAsync(string guild, string channel, string key,
            TrackingDiscordNotification notification, CancellationToken ct)
        {
            SendCalls++;
            var receipt = $"receipt-{SendCalls}";
            Messages.Add(key, receipt);
            if (SendCalls == LoseResponseOnCall) throw new InvalidOperationException("Lost response after send.");
            return Task.FromResult(receipt);
        }
    }
}
