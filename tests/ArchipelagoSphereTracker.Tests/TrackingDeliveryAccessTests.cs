using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;
using Discord;
using Discord.Net;
using Xunit;

public sealed class TrackingDeliveryAccessTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddMinutes(1);
    private const string Type = TrackingDestination.NotificationDestinationType;

    [Theory]
    [InlineData(403, 50001, true)]
    [InlineData(403, 50013, true)]
    [InlineData(404, 10003, true)]
    [InlineData(403, 0, false)]
    [InlineData(404, 10008, false)]
    [InlineData(429, 50001, false)]
    [InlineData(500, 50001, false)]
    public void Only_specific_access_refusals_trigger_the_policy(int status, int code, bool expected)
        => Assert.Equal(expected, TrackingDeliveryAccessPolicy.IsAccessFailure(HttpError(status, code)));

    [Fact]
    public async Task Three_consecutive_refusals_suspend_only_the_destination_and_survive_restart()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync("blocked", 2);
        await QueueAsync("healthy", 1);
        var clock = new Clock();
        var publisher = new Publisher { Failure = AccessError() };
        var worker = new TrackingDeliveryWorker(publisher, clock);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Assert.True(await worker.RunOnceAsync());
            Assert.Equal(attempt >= 3, await TrackingDeliveryAccessPolicy.IsSuspendedAsync("g", "blocked", default));
            clock.Advance(TimeSpan.FromMinutes(5));
            // Keep healthy traffic out of the first three attempts.
            await ExecuteAsync($"UPDATE EventDeliveries SET NextAttemptAtUtc='{clock.Current.AddDays(1):O}' WHERE DestinationId='healthy';");
        }
        Assert.False(await new TrackingDeliveryWorker(publisher, clock).RunOnceAsync());
        Assert.Equal(3, publisher.Deliveries.Count);
        Assert.Equal(3L, await ScalarAsync("SELECT ConsecutiveAccessFailures FROM NotificationDeliveryStates;"));
        publisher.Failure = null;
        await ExecuteAsync($"UPDATE EventDeliveries SET NextAttemptAtUtc='{clock.Current:O}' WHERE DestinationId='healthy';");
        Assert.True(await worker.RunOnceAsync());
        Assert.Equal("healthy", publisher.Deliveries.Last().DestinationId);
        Assert.False(await worker.RunOnceAsync());
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM EventDeliveries WHERE DestinationId='blocked' AND Status='Suspended';"));
    }

    [Fact]
    public async Task Transient_errors_and_success_reset_an_unconfirmed_failure_streak()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync("c", 2);
        var clock = new Clock();
        var publisher = new Publisher { Failure = AccessError() };
        var worker = new TrackingDeliveryWorker(publisher, clock);
        for (var i = 0; i < 2; i++) { Assert.True(await worker.RunOnceAsync()); clock.Advance(TimeSpan.FromMinutes(5)); }
        publisher.Failure = HttpError(429, 0);
        Assert.True(await worker.RunOnceAsync());
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("NotificationDeliveryStates"));
        clock.Advance(TimeSpan.FromMinutes(5));
        publisher.Failure = AccessError();
        Assert.True(await worker.RunOnceAsync());
        Assert.Equal(1L, await ScalarAsync("SELECT ConsecutiveAccessFailures FROM NotificationDeliveryStates;"));
        clock.Advance(TimeSpan.FromMinutes(5));
        publisher.Failure = null;
        Assert.True(await worker.RunOnceAsync());
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("NotificationDeliveryStates"));
        publisher.Failure = AccessError();
        Assert.True(await worker.RunOnceAsync());
        Assert.Equal(1L, await ScalarAsync("SELECT ConsecutiveAccessFailures FROM NotificationDeliveryStates;"));
    }

    [Fact]
    public async Task Non_notification_destinations_keep_the_existing_retry_policy()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync("c", 1);
        await ExecuteAsync("UPDATE EventDeliveries SET DestinationType='Other';");
        var clock = new Clock();
        var worker = new TrackingDeliveryWorker(new Publisher { Failure = AccessError() }, clock);
        for (var i = 0; i < 4; i++) { Assert.True(await worker.RunOnceAsync()); clock.Advance(TimeSpan.FromMinutes(5)); }
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("NotificationDeliveryStates"));
        Assert.Equal("Failed", await ScalarAsync("SELECT Status FROM EventDeliveries;"));
    }

    [Fact]
    public async Task Suspension_retains_every_delivery_and_receipt_indefinitely_and_respects_live_claims()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync("blocked", 205);
        await QueueAsync("healthy", 1);
        await ExecuteAsync($@"UPDATE EventDeliveries SET Status='Delivering',LeaseUntilUtc='{Now.AddMinutes(1):O}' WHERE Id=1;
            UPDATE EventDeliveries SET DestinationType='Other' WHERE Id=4;
            INSERT INTO DiscordNotificationBatches(BatchKey,SnapshotId,ChannelId,PayloadJson)
                VALUES('receipt',1,'blocked','{{""parts"":[]}}');");
        await SuspendAsync("blocked");
        Assert.Equal(203L, await ScalarAsync("SELECT COUNT(*) FROM EventDeliveries WHERE Status='Suspended';"));
        Assert.Equal("Delivering", await ScalarAsync("SELECT Status FROM EventDeliveries WHERE Id=1;"));
        Assert.Equal("Pending", await ScalarAsync("SELECT Status FROM EventDeliveries WHERE Id=4;"));
        Assert.Equal("Pending", await ScalarAsync("SELECT Status FROM EventDeliveries WHERE DestinationId='healthy';"));
        var publisher = new Publisher { ProbeFailure = AccessError() };
        var clock = new Clock();
        var worker = new TrackingDeliveryWorker(publisher, clock);
        await worker.RunMaintenanceOnceAsync();
        Assert.Equal("Delivering", await ScalarAsync("SELECT Status FROM EventDeliveries WHERE Id=1;"));
        clock.Advance(TimeSpan.FromDays(45));
        await worker.RunMaintenanceOnceAsync();
        Assert.Equal(204L, await ScalarAsync("SELECT COUNT(*) FROM EventDeliveries WHERE Status='Suspended' AND DeliveredAtUtc IS NULL;"));
        Assert.Equal(206, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Equal(206, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DiscordNotificationBatches"));
        Assert.Equal(0, await TrackingSnapshotRetention.PruneBatchAsync());
        Assert.Empty(publisher.Deliveries);
        Assert.Null(await ScalarAsync("PRAGMA foreign_key_check;"));
    }

    [Fact]
    public async Task Successful_probe_resumes_the_entire_queue_in_order_even_after_forty_five_days()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync("c", 2);
        await SuspendAsync("c");
        var clock = new Clock { Current = Now.AddDays(45) };
        var publisher = new Publisher();
        var worker = new TrackingDeliveryWorker(publisher, clock);
        Assert.False(await worker.RunOnceAsync());
        await worker.RunMaintenanceOnceAsync();
        Assert.Equal(1, publisher.Probes);
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("NotificationDeliveryStates"));
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM EventDeliveries WHERE Status='Pending';"));
        Assert.True(await worker.RunOnceAsync());
        Assert.True(await worker.RunOnceAsync());
        Assert.Equal(new long[] { 1, 2 }, publisher.Deliveries.Select(delivery => delivery.DeliveryId));
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM EventDeliveries WHERE Status='Delivered';"));
        Assert.False(await worker.RunOnceAsync());
    }

    [Fact]
    public async Task Read_only_probes_retry_every_fifteen_minutes_without_removing_any_notification()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync("c", 1);
        await SuspendAsync("c");
        var clock = new Clock();
        var publisher = new Publisher { ProbeFailure = AccessError() };
        var worker = new TrackingDeliveryWorker(publisher, clock);
        await worker.RunMaintenanceOnceAsync();
        Assert.Equal(0, publisher.Probes);
        clock.Advance(TimeSpan.FromMinutes(15));
        await worker.RunMaintenanceOnceAsync();
        Assert.Equal(1, publisher.Probes);
        Assert.Equal("Suspended", await ScalarAsync("SELECT Status FROM EventDeliveries;"));
        Assert.True(await TrackingDeliveryAccessPolicy.IsSuspendedAsync("g", "c", default));
        clock.Advance(TimeSpan.FromMinutes(14));
        await worker.RunMaintenanceOnceAsync();
        Assert.Equal(1, publisher.Probes);
        publisher.ProbeFailure = null;
        clock.Advance(TimeSpan.FromMinutes(1));
        await new TrackingDeliveryWorker(publisher, clock).RunMaintenanceOnceAsync();
        Assert.Equal(2, publisher.Probes);
        Assert.False(await TrackingDeliveryAccessPolicy.IsSuspendedAsync("g", "c", default));
        Assert.True(await worker.RunOnceAsync());
        Assert.Equal("Delivered", await ScalarAsync("SELECT Status FROM EventDeliveries;"));
        Assert.Single(publisher.Deliveries);
    }

    [Fact]
    public async Task Probe_claims_recover_after_crashes_and_stale_results_cannot_unlock_the_destination()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync("c", 1);
        await SuspendAsync("c");
        var due = Now.AddMinutes(15);
        var first = Assert.IsType<TrackingAccessProbe>(await TrackingDeliveryAccessPolicy.ClaimProbeAsync(due, default));
        Assert.Null(await TrackingDeliveryAccessPolicy.ClaimProbeAsync(due.AddMinutes(1), default));
        var replacement = Assert.IsType<TrackingAccessProbe>(await TrackingDeliveryAccessPolicy.ClaimProbeAsync(due.AddMinutes(2), default));
        Assert.False(await TrackingDeliveryAccessPolicy.CompleteProbeAsync(first, due.AddMinutes(2), null, default));
        Assert.True(await TrackingDeliveryAccessPolicy.IsSuspendedAsync("g", "c", default));
        await RecordFailureAsync("c", true, due.AddMinutes(2)); // newer refusal invalidates an in-flight probe
        Assert.False(await TrackingDeliveryAccessPolicy.CompleteProbeAsync(replacement, due.AddMinutes(2), null, default));
        var current = Assert.IsType<TrackingAccessProbe>(await TrackingDeliveryAccessPolicy.ClaimProbeAsync(due.AddMinutes(2), default));
        await ExecuteAsync("DELETE FROM TrackedRooms WHERE GuildId='g' AND ChannelId='c';");
        Assert.False(await TrackingDeliveryAccessPolicy.CompleteProbeAsync(current, due.AddMinutes(3), null, default));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("NotificationDeliveryStates"));
    }

    [Fact]
    public async Task A_slow_probe_resumes_all_notifications_without_an_age_cutoff()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync("c", 1);
        await SuspendAsync("c");
        var due = Now.AddMinutes(15);
        var probe = Assert.IsType<TrackingAccessProbe>(await TrackingDeliveryAccessPolicy.ClaimProbeAsync(due, default));
        Assert.True(await TrackingDeliveryAccessPolicy.CompleteProbeAsync(probe, due.AddDays(30), null, default));
        Assert.Equal("Pending", await ScalarAsync("SELECT Status FROM EventDeliveries;"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    [Fact]
    public async Task A_slow_probe_does_not_block_delivery_to_a_healthy_destination()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync("blocked", 1);
        await QueueAsync("healthy", 1);
        await SuspendAsync("blocked");
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publisher = new Publisher { Probe = async ct => { started.SetResult(true); await finish.Task.WaitAsync(ct); } };
        var worker = new TrackingDeliveryWorker(publisher, new Clock { Current = Now.AddMinutes(15) });
        var maintenance = worker.RunMaintenanceOnceAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.True(await worker.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal("healthy", Assert.Single(publisher.Deliveries).DestinationId);
        }
        finally { finish.TrySetResult(true); }
        await maintenance;
    }

    [Fact]
    public async Task Stale_delivery_failure_cannot_suspend_a_successful_retry()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync("c", 1);
        var clock = new Clock();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publisher = new Publisher { Publish = async ct => { started.SetResult(true); await finish.Task; throw AccessError(); } };
        var first = new TrackingDeliveryWorker(publisher, clock).RunOnceAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        clock.Advance(TimeSpan.FromMinutes(3));
        Assert.True(await new TrackingDeliveryWorker(new Publisher(), clock).RunOnceAsync());
        finish.SetResult(true);
        await first;
        Assert.Equal("Delivered", await ScalarAsync("SELECT Status FROM EventDeliveries;"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("NotificationDeliveryStates"));
    }

    [Fact]
    public async Task Metrics_separate_suspended_notifications_from_pending_and_success_and_reset_on_resume()
    {
        using var database = new TestDatabaseScope();
        await QueueAsync("c", 1);
        await SuspendAsync("c");
        await MetricsExporter.CollectOnce();
        using var stream = new MemoryStream();
        await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
        var metrics = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("ast_notification_destinations_suspended 1", metrics);
        Assert.Contains("ast_event_deliveries{state=\"suspended\"} 1", metrics);
        Assert.Contains("ast_event_deliveries{state=\"pending\"} 0", metrics);
        Assert.Contains("ast_event_deliveries{state=\"delivered\"} 0", metrics);
        var probe = Assert.IsType<TrackingAccessProbe>(await TrackingDeliveryAccessPolicy.ClaimProbeAsync(Now.AddMinutes(15), default));
        await TrackingDeliveryAccessPolicy.CompleteProbeAsync(probe, Now.AddMinutes(15), null, default);
        await MetricsExporter.CollectOnce();
        stream.SetLength(0);
        await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
        metrics = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("ast_notification_destinations_suspended 0", metrics);
        Assert.Contains("ast_event_deliveries{state=\"suspended\"} 0", metrics);
        Assert.Contains("ast_event_deliveries{state=\"pending\"} 1", metrics);
    }

    internal static async Task QueueAsync(string channel, int count)
    {
        var snapshot = NormalizedRoomSnapshot.Create("g", channel, [], [], [], [], [], [],
            null, Now, null, TrackingObservationState.Healthy, null, SnapshotSections.All);
        var stored = await new TrackingV2Store().ApplySnapshotAsync(snapshot);
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"WITH RECURSIVE numbers(n) AS (VALUES(1) UNION ALL SELECT n+1 FROM numbers WHERE n<@Count)
                INSERT INTO TrackingEvents(EventKey,GuildId,ChannelId,EventType,OccurredAtUtc,PayloadJson,SnapshotId,CreatedAtUtc)
                SELECT @Channel||'-'||n,'g',@Channel,'CheckCompleted',@Now,'{}',@Snapshot,@Now FROM numbers;
                INSERT INTO EventDeliveries(EventId,DestinationType,DestinationId,Status,NextAttemptAtUtc)
                SELECT Id,@Type,@Channel,'Pending',@Now FROM TrackingEvents WHERE ChannelId=@Channel;";
            command.Parameters.AddWithValue("@Count", count);
            command.Parameters.AddWithValue("@Channel", channel);
            command.Parameters.AddWithValue("@Snapshot", stored.SnapshotId);
            command.Parameters.AddWithValue("@Now", Now.ToString("O"));
            command.Parameters.AddWithValue("@Type", Type);
            await command.ExecuteNonQueryAsync();
        });
    }

    internal static async Task SuspendAsync(string channel)
    {
        for (var i = 0; i < 3; i++) await RecordFailureAsync(channel, true, Now);
    }

    private static Task RecordFailureAsync(string channel, bool accessFailure, DateTimeOffset now)
        => Db.WriteAsync(connection => TrackingDeliveryAccessPolicy.RecordFailureAsync(connection,
            new(1, "key", "CheckCompleted", "{}", Type, channel, 1, "", 1, "g", channel),
            accessFailure, "HTTP_403_DISCORD_50001", now, default));

    internal static Task ExecuteAsync(string sql) => Db.WriteAsync(async connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    });

    internal static async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }

    private static HttpException HttpError(int status, int code)
        => new((HttpStatusCode)status, null!, code == 0 ? null : (DiscordErrorCode)code, "test", []);
    private static HttpException AccessError() => HttpError(403, 50001);

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Current = Now;
        public override DateTimeOffset GetUtcNow() => Current;
        internal void Advance(TimeSpan duration) => Current += duration;
    }

    private sealed class Publisher : ITrackingEventPublisher, ITrackingDestinationAccessProbe
    {
        internal Exception? Failure, ProbeFailure;
        internal Func<CancellationToken, Task>? Probe;
        internal Func<CancellationToken, Task<TrackingPublicationResult>>? Publish;
        internal int Probes;
        internal readonly List<TrackingDeliveryEnvelope> Deliveries = [];
        public Task<TrackingPublicationResult> PublishAsync(TrackingDeliveryEnvelope delivery, CancellationToken ct)
        {
            Deliveries.Add(delivery);
            if (Publish != null) return Publish(ct);
            if (Failure != null) throw Failure;
            return Task.FromResult(new TrackingPublicationResult("receipt"));
        }
        public async Task CheckAccessAsync(string guild, string channel, CancellationToken ct)
        {
            Probes++;
            if (Probe != null) await Probe(ct);
            if (ProbeFailure != null) throw ProbeFailure;
        }
    }
}
