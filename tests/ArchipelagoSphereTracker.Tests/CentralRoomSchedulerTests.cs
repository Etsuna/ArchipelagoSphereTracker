using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.Tracking.Scheduling;
using Xunit;

public sealed class CentralRoomSchedulerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Due_rooms_are_dispatched_in_priority_order()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([
            Registration("late", "https://a.example", T0.AddMinutes(2)),
            Registration("first", "https://a.example", T0.AddMinutes(-2)),
            Registration("second", "https://b.example", T0.AddMinutes(-1))
        ]);
        var order = new List<string>();
        var scheduler = Scheduler(store, (room, _) =>
        {
            order.Add(room.ChannelId);
            return Task.FromResult(RoomPollResult.Ok());
        }, time, global: 1);
        await scheduler.InitializeAsync();

        Assert.Equal(2, await scheduler.RunDueOnceAsync());
        Assert.Equal(0, await scheduler.RunDueOnceAsync());

        Assert.Equal(["first", "second"], order);
    }

    [Fact]
    public async Task Global_and_origin_concurrency_limits_are_never_exceeded()
    {
        var time = new ManualTimeProvider(T0);
        var registrations = Enumerable.Range(0, 12)
            .Select(index => Registration(
                $"c{index}",
                index % 2 == 0 ? "https://a.example" : "https://b.example",
                T0))
            .ToArray();
        var store = new MemoryScheduleStore(registrations);
        var activeGlobal = 0;
        var maximumGlobal = 0;
        var activeByOrigin = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var maximumByOrigin = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var scheduler = Scheduler(store, async (room, cancellationToken) =>
        {
            var global = Interlocked.Increment(ref activeGlobal);
            UpdateMaximum(ref maximumGlobal, global);
            var origin = activeByOrigin.AddOrUpdate(room.Origin, 1, (_, current) => current + 1);
            maximumByOrigin.AddOrUpdate(room.Origin, origin, (_, current) => Math.Max(current, origin));
            await Task.Delay(25, cancellationToken);
            activeByOrigin.AddOrUpdate(room.Origin, 0, (_, current) => current - 1);
            Interlocked.Decrement(ref activeGlobal);
            return RoomPollResult.Ok();
        }, time, global: 4, perOrigin: 2);
        await scheduler.InitializeAsync();

        while (await scheduler.RunDueOnceAsync() > 0) { }

        Assert.InRange(maximumGlobal, 1, 4);
        Assert.All(maximumByOrigin.Values, value => Assert.InRange(value, 1, 2));
        Assert.Equal(12, store.States.Count);
    }

    [Fact]
    public async Task Slow_origin_does_not_block_due_rooms_on_healthy_origins()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([
            Registration("slow-1", "https://slow.example", T0.AddMinutes(-4)),
            Registration("slow-2", "https://slow.example", T0.AddMinutes(-3)),
            Registration("slow-3", "https://slow.example", T0.AddMinutes(-2)),
            Registration("healthy", "https://healthy.example", T0.AddMinutes(-1))
        ]);
        var slowStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSlow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var healthyStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduler = Scheduler(store, async (room, cancellationToken) =>
        {
            if (room.Origin == "https://slow.example")
            {
                slowStarted.TrySetResult();
                await releaseSlow.Task.WaitAsync(cancellationToken);
            }
            else
            {
                healthyStarted.TrySetResult();
            }

            return RoomPollResult.Ok();
        }, time, global: 3, perOrigin: 1);
        await scheduler.InitializeAsync();

        var run = scheduler.RunDueOnceAsync();
        await slowStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        try
        {
            await healthyStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            releaseSlow.TrySetResult();
            await run.WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(4, await run);
    }

    [Fact]
    public async Task Failure_of_one_room_does_not_stop_other_rooms()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([
            Registration("bad", "https://bad.example", T0),
            Registration("good", "https://good.example", T0)
        ]);
        var executed = new ConcurrentBag<string>();
        var scheduler = Scheduler(store, (room, _) =>
        {
            executed.Add(room.ChannelId);
            return Task.FromResult(room.ChannelId == "bad"
                ? RoomPollResult.Failed(PollFailureKind.ServerError, affectsOriginBreaker: true)
                : RoomPollResult.Ok());
        }, time, global: 2);
        await scheduler.InitializeAsync();

        Assert.Equal(2, await scheduler.RunDueOnceAsync());

        Assert.Equal(2, executed.Count);
        Assert.Equal(PollFailureKind.ServerError, store.States["g:bad"].LastFailureKind);
        Assert.Equal(PollFailureKind.None, store.States["g:good"].LastFailureKind);
    }

    [Fact]
    public async Task Restart_respects_persisted_next_poll_and_promotion_is_rate_limited()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([Registration("c", "https://a.example", T0)]);
        var executions = 0;
        var first = Scheduler(store, (_, _) =>
        {
            executions++;
            return Task.FromResult(RoomPollResult.Ok());
        }, time, global: 1);
        await first.InitializeAsync();
        Assert.Equal(1, await first.RunDueOnceAsync());

        var restarted = Scheduler(store, (_, _) =>
        {
            executions++;
            return Task.FromResult(RoomPollResult.Ok());
        }, time, global: 1);
        await restarted.InitializeAsync();
        Assert.Equal(0, await restarted.RunDueOnceAsync());
        Assert.True(await restarted.PromoteAsync("g", "c"));
        Assert.False(await restarted.PromoteAsync("g", "c"));
        Assert.Equal(1, await restarted.RunDueOnceAsync());
        Assert.Equal(2, executions);
    }

    [Fact]
    public async Task Unchanged_room_slows_down_and_new_activity_restores_configured_interval()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([Registration("c", "https://a.example", T0)]);
        var hash = "snapshot-a";
        var scheduler = Scheduler(store, (_, _) => Task.FromResult(RoomPollResult.Ok(hash, hasRecentActivity: false)), time, global: 1);
        await scheduler.InitializeAsync();

        Assert.Equal(1, await scheduler.RunDueOnceAsync()); // establish the baseline
        for (var unchanged = 0; unchanged < 3; unchanged++)
        {
            time.Advance(TimeSpan.FromMinutes(5));
            Assert.Equal(1, await scheduler.RunDueOnceAsync());
        }

        var slowed = Assert.IsType<RoomHealthSnapshot>(scheduler.GetHealth("g", "c"));
        Assert.Equal(TimeSpan.FromMinutes(2), slowed.EffectiveInterval);
        Assert.Equal(3, slowed.UnchangedSuccessCount);

        hash = "snapshot-b";
        time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(1, await scheduler.RunDueOnceAsync());

        var active = Assert.IsType<RoomHealthSnapshot>(scheduler.GetHealth("g", "c"));
        Assert.Equal(TimeSpan.FromMinutes(1), active.EffectiveInterval);
        Assert.Equal(0, active.UnchangedSuccessCount);
        Assert.Equal(time.GetUtcNow(), active.LastChangeAtUtc);
    }

    [Fact]
    public async Task Automatic_mode_respects_room_maximum_and_fixed_mode_never_slows_down()
    {
        var automaticTime = new ManualTimeProvider(T0);
        var automaticStore = new MemoryScheduleStore([
            Registration("automatic", "https://a.example", T0, maximum: TimeSpan.FromMinutes(10))
        ]);
        var automatic = Scheduler(
            automaticStore,
            (_, _) => Task.FromResult(RoomPollResult.Ok("unchanged", hasRecentActivity: false)),
            automaticTime,
            global: 1);
        await automatic.InitializeAsync();
        Assert.Equal(1, await automatic.RunDueOnceAsync());
        for (var poll = 0; poll < 9; poll++)
        {
            automaticTime.Advance(automatic.GetHealth("g", "automatic")!.EffectiveInterval);
            Assert.Equal(1, await automatic.RunDueOnceAsync());
        }
        var capped = automatic.GetHealth("g", "automatic")!;
        Assert.Equal(RoomPollingMode.Automatic, capped.PollingMode);
        Assert.Equal(TimeSpan.FromMinutes(5), capped.MaximumPollInterval);
        Assert.Equal(TimeSpan.FromMinutes(5), capped.EffectiveInterval);

        var fixedTime = new ManualTimeProvider(T0);
        var fixedStore = new MemoryScheduleStore([
            Registration("fixed", "https://a.example", T0, RoomPollingMode.Fixed, TimeSpan.FromHours(1))
        ]);
        var fixedScheduler = Scheduler(
            fixedStore,
            (_, _) => Task.FromResult(RoomPollResult.Ok("unchanged", hasRecentActivity: false)),
            fixedTime,
            global: 1);
        await fixedScheduler.InitializeAsync();
        for (var poll = 0; poll < 5; poll++)
        {
            Assert.Equal(1, await fixedScheduler.RunDueOnceAsync());
            fixedTime.Advance(TimeSpan.FromMinutes(5));
        }
        var fixedHealth = fixedScheduler.GetHealth("g", "fixed")!;
        Assert.Equal(RoomPollingMode.Fixed, fixedHealth.PollingMode);
        Assert.Equal(TimeSpan.FromMinutes(5), fixedHealth.EffectiveInterval);
        Assert.Equal(0, fixedHealth.UnchangedSuccessCount);
    }

    [Fact]
    public async Task Pause_survives_restart_and_resume_schedules_an_immediate_poll()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([Registration("c", "https://a.example", T0)]);
        var executions = 0;
        var first = Scheduler(store, (_, _) =>
        {
            executions++;
            return Task.FromResult(RoomPollResult.Ok("snapshot"));
        }, time, global: 1);
        await first.InitializeAsync();

        Assert.Equal(TrackingControlOutcome.Accepted, await first.PauseAsync("g", "c"));
        Assert.Equal(TrackingControlOutcome.AlreadyPaused, await first.PauseAsync("g", "c"));
        Assert.Equal(0, await first.RunDueOnceAsync());

        var restarted = Scheduler(store, (_, _) =>
        {
            executions++;
            return Task.FromResult(RoomPollResult.Ok("snapshot"));
        }, time, global: 1);
        await restarted.InitializeAsync();
        Assert.True(restarted.GetHealth("g", "c")!.IsPaused);
        Assert.Equal(0, await restarted.RunDueOnceAsync());

        Assert.Equal(TrackingControlOutcome.Accepted, await restarted.ResumeAsync("g", "c"));
        Assert.Equal(TrackingControlOutcome.AlreadyRunning, await restarted.ResumeAsync("g", "c"));
        Assert.Equal(1, await restarted.RunDueOnceAsync());
        Assert.Equal(1, executions);
        Assert.False(store.States["g:c"].IsPaused);
    }

    [Fact]
    public async Task Forced_sync_cooldown_survives_scheduler_restart()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([Registration("c", "https://a.example", T0.AddHours(1))]);
        var first = Scheduler(store, (_, _) => Task.FromResult(RoomPollResult.Ok()), time, global: 1);
        await first.InitializeAsync();

        Assert.Equal(TrackingControlOutcome.Accepted, await first.ForceSyncAsync("g", "c"));
        Assert.Equal(T0, store.States["g:c"].LastForcedSyncAtUtc);

        var restarted = Scheduler(store, (_, _) => Task.FromResult(RoomPollResult.Ok()), time, global: 1);
        await restarted.InitializeAsync();
        Assert.Equal(TrackingControlOutcome.RateLimited, await restarted.ForceSyncAsync("g", "c"));

        time.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(TrackingControlOutcome.Accepted, await restarted.ForceSyncAsync("g", "c"));
        Assert.Equal(T0.AddSeconds(31), store.States["g:c"].LastForcedSyncAtUtc);
    }

    [Fact]
    public async Task Guild_health_returns_only_rooms_from_the_requested_guild()
    {
        var store = new MemoryScheduleStore([
            Registration("one", "https://a.example", T0),
            Registration("two", "https://b.example", T0) with
            {
                Definition = Registration("two", "https://b.example", T0).Definition with { GuildId = "other" }
            }
        ]);
        var scheduler = Scheduler(store, (_, _) => Task.FromResult(RoomPollResult.Ok()), new ManualTimeProvider(T0));
        await scheduler.InitializeAsync();

        var health = Assert.Single(scheduler.GetGuildHealth("g"));
        Assert.Equal("one", health.ChannelId);
    }

    [Fact]
    public async Task Completion_observer_receives_activity_counts_and_next_schedule()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([Registration("observed", "https://a.example", T0)]);
        RoomPollCompletion? completion = null;
        var scheduler = new CentralRoomScheduler(
            store,
            (_, _) => Task.FromResult(RoomPollResult.Ok(
                "snapshot",
                newItemCount: 3,
                newHintCount: 2,
                updatedHintCount: 1,
                completedGoalCount: 1)),
            Options(global: 1, perOrigin: 1),
            timeProvider: time,
            completionObserver: value => completion = value);
        await scheduler.InitializeAsync();

        Assert.Equal(1, await scheduler.RunDueOnceAsync());

        var observed = Assert.IsType<RoomPollCompletion>(completion);
        Assert.Equal("observed", observed.Room.ChannelId);
        Assert.Equal(3, observed.Result.NewItemCount);
        Assert.Equal(2, observed.Result.NewHintCount);
        Assert.Equal(1, observed.Result.UpdatedHintCount);
        Assert.Equal(1, observed.Result.CompletedGoalCount);
        Assert.Equal(T0.AddMinutes(1), observed.State!.NextPollAtUtc);
    }

    [Fact]
    public async Task Completion_observer_reports_when_tracking_is_removed()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([Registration("removed", "https://a.example", T0)]);
        RoomPollCompletion? completion = null;
        var scheduler = new CentralRoomScheduler(
            store,
            (_, _) => Task.FromResult(RoomPollResult.Removed()),
            Options(global: 1, perOrigin: 1),
            timeProvider: time,
            completionObserver: value => completion = value);
        await scheduler.InitializeAsync();

        Assert.Equal(1, await scheduler.RunDueOnceAsync());

        var observed = Assert.IsType<RoomPollCompletion>(completion);
        Assert.True(observed.Result.RemoveRoom);
        Assert.Null(observed.State);
        Assert.Equal(0, scheduler.RoomCount);
    }

    [Fact]
    public async Task Open_breaker_delays_other_rooms_on_the_same_origin()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([
            Registration("first", "https://a.example", T0),
            Registration("second", "https://a.example", T0)
        ]);
        var executions = 0;
        var scheduler = new CentralRoomScheduler(
            store,
            (_, _) =>
            {
                executions++;
                return Task.FromResult(RoomPollResult.Failed(
                    PollFailureKind.ServerError,
                    affectsOriginBreaker: true));
            },
            Options(global: 1, perOrigin: 1) with
            {
                BreakerFailureThreshold = 1,
                BreakerDuration = TimeSpan.FromMinutes(2)
            },
            time);
        await scheduler.InitializeAsync();

        Assert.Equal(1, await scheduler.RunDueOnceAsync());
        Assert.Equal(0, await scheduler.RunDueOnceAsync());
        Assert.Equal(1, executions);

        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, await scheduler.RunDueOnceAsync());
        Assert.Equal(2, executions);
    }

    [Fact]
    public async Task Cancellation_stops_without_starting_another_request()
    {
        var store = new MemoryScheduleStore([Registration("c", "https://a.example", DateTimeOffset.UtcNow)]);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executions = 0;
        var scheduler = new CentralRoomScheduler(
            store,
            async (_, cancellationToken) =>
            {
                Interlocked.Increment(ref executions);
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return RoomPollResult.Ok();
            },
            Options(global: 1, perOrigin: 1) with { MaximumJitter = TimeSpan.Zero });
        using var cancellation = new CancellationTokenSource();

        var run = scheduler.RunAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await run.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(25);

        Assert.Equal(1, executions);
    }

    [Fact]
    public async Task A_scheduler_instance_can_only_run_once()
    {
        var scheduler = new CentralRoomScheduler(
            new MemoryScheduleStore([]),
            (_, _) => Task.FromResult(RoomPollResult.Ok()),
            Options(global: 1, perOrigin: 1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await scheduler.RunAsync(cancellation.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scheduler.RunAsync(CancellationToken.None));
    }

    [Fact]
    public async Task One_thousand_rooms_load_into_one_queue_without_poll_tasks()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore(Enumerable.Range(0, 1000)
            .Select(index => Registration($"c{index}", $"https://h{index % 10}.example", T0.AddHours(1)))
            .ToArray());
        var executions = 0;
        var scheduler = Scheduler(store, (_, _) =>
        {
            executions++;
            return Task.FromResult(RoomPollResult.Ok());
        }, time);

        await scheduler.InitializeAsync();

        Assert.Equal(1000, scheduler.RoomCount);
        Assert.Equal(1000, scheduler.QueueDepth);
        Assert.Equal(0, executions);
        Assert.Equal(0, await scheduler.RunDueOnceAsync());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "{}", "application/json", PollFailureKind.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError, "{}", "application/json", PollFailureKind.ServerError)]
    [InlineData(HttpStatusCode.OK, "<html>oops</html>", "text/html", PollFailureKind.InvalidContentType)]
    [InlineData(HttpStatusCode.OK, "not-json", "application/json", PollFailureKind.InvalidJson)]
    [InlineData(HttpStatusCode.OK, "{}", "application/json", PollFailureKind.PartialResponse)]
    public async Task WebHost_client_classifies_responses(
        HttpStatusCode status,
        string body,
        string contentType,
        PollFailureKind expected)
    {
        using var http = new HttpClient(new DelegateHandler((_, _) => Task.FromResult(Response(status, body, contentType))));
        var client = new ResilientWebHostClient(http);

        var result = await client.FetchJsonAsync(
            new Uri("https://example.test/api/tracker/id"),
            WebHostEndpointKind.RuntimeTracker,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expected, result.PollResult.FailureKind);
    }

    [Fact]
    public async Task WebHost_client_honors_retry_after_and_concurrency_budgets()
    {
        var activeGlobal = 0;
        var maxGlobal = 0;
        var activeHosts = new ConcurrentDictionary<string, int>();
        var maxHosts = new ConcurrentDictionary<string, int>();
        using var http = new HttpClient(new DelegateHandler(async (request, cancellationToken) =>
        {
            var global = Interlocked.Increment(ref activeGlobal);
            UpdateMaximum(ref maxGlobal, global);
            var host = request.RequestUri!.Host;
            var hostActive = activeHosts.AddOrUpdate(host, 1, (_, current) => current + 1);
            maxHosts.AddOrUpdate(host, hostActive, (_, current) => Math.Max(current, hostActive));
            await Task.Delay(25, cancellationToken);
            activeHosts.AddOrUpdate(host, 0, (_, current) => current - 1);
            Interlocked.Decrement(ref activeGlobal);
            return Response(HttpStatusCode.OK, "{\"player_status\":[]}", "application/json");
        }));
        var client = new ResilientWebHostClient(http, new WebHostClientOptions
        {
            GlobalConcurrency = 3,
            PerOriginConcurrency = 2
        });

        var requests = Enumerable.Range(0, 12).Select(index => client.FetchJsonAsync(
            new Uri($"https://{(index % 2 == 0 ? "a" : "b")}.example/api/tracker/{index}"),
            WebHostEndpointKind.RuntimeTracker,
            CancellationToken.None));
        var results = await Task.WhenAll(requests);

        Assert.All(results, result => Assert.True(result.Success));
        Assert.InRange(maxGlobal, 1, 3);
        Assert.All(maxHosts.Values, value => Assert.InRange(value, 1, 2));

        using var rateLimitedHttp = new HttpClient(new DelegateHandler((_, _) =>
        {
            var response = Response((HttpStatusCode)429, "{}", "application/json");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
            return Task.FromResult(response);
        }));
        var rateLimited = await new ResilientWebHostClient(rateLimitedHttp).FetchJsonAsync(
            new Uri("https://a.example/api/tracker/id"),
            WebHostEndpointKind.RuntimeTracker,
            CancellationToken.None);
        Assert.Equal(PollFailureKind.RateLimited, rateLimited.PollResult.FailureKind);
        Assert.Equal(TimeSpan.FromSeconds(42), rateLimited.PollResult.RetryAfter);
    }

    [Fact]
    public async Task WebHost_client_applies_endpoint_timeout_without_canceling_the_caller()
    {
        using var http = new HttpClient(new DelegateHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response(HttpStatusCode.OK, "{\"player_status\":[]}", "application/json");
        }));
        var client = new ResilientWebHostClient(http, new WebHostClientOptions
        {
            RuntimeTrackerTimeout = TimeSpan.FromMilliseconds(20)
        });

        var result = await client.FetchJsonAsync(
            new Uri("https://a.example/api/tracker/id"),
            WebHostEndpointKind.RuntimeTracker,
            CancellationToken.None);

        Assert.Equal(PollFailureKind.Timeout, result.PollResult.FailureKind);
        Assert.True(result.PollResult.AffectsOriginBreaker);
    }

    [Fact]
    public async Task Sqlite_store_round_trips_durable_schedule_state_and_migration_is_idempotent()
    {
        using var scope = new TestDatabaseScope();
        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(
            "g",
            "c",
            "https://example.test",
            "room",
            "tracker",
            false,
            "5m",
            "0");
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                DROP TABLE RoomPollState;
                ALTER TABLE ChannelsAndUrlsTable DROP COLUMN PollingMode;
                ALTER TABLE ChannelsAndUrlsTable DROP COLUMN MaximumCheckFrequency;";
            await command.ExecuteNonQueryAsync();
            return true;
        });
        await DBMigration_5.Migrate_5_0_8();
        await DBMigration_5.Migrate_5_0_8();
        await DBMigration_5.Migrate_5_0_9();
        await DBMigration_5.Migrate_5_0_9();
        await DBMigration_5.Migrate_5_0_10();
        await DBMigration_5.Migrate_5_0_10();
        var store = new SqliteRoomScheduleStore(new ManualTimeProvider(T0));
        var state = new RoomScheduleState(
            "g",
            "c",
            T0.AddMinutes(3),
            T0,
            null,
            2,
            PollFailureKind.Timeout,
            T0.AddMinutes(2),
            123.5,
            true,
            T0.AddMinutes(-1),
            T0.AddMinutes(-2),
            "content-hash",
            6,
            TimeSpan.FromMinutes(20).TotalSeconds,
            T0.AddMinutes(-3));

        await store.SaveStateAsync(state, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        var registration = Assert.Single(loaded);
        Assert.Equal(state, registration.State);
        Assert.Equal(T0, registration.Definition.InitialNextPollAtUtc);
        Assert.Equal(RoomPollingMode.Automatic, registration.Definition.PollingMode);
        Assert.Equal(TimeSpan.FromHours(1), registration.Definition.MaximumPollInterval);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public async Task Active_or_unknown_rooms_keep_minute_cadence_even_when_snapshot_is_unchanged(bool? activity)
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([Registration("c", "https://a.example", T0)]);
        var scheduler = Scheduler(store, (_, _) => Task.FromResult(
            RoomPollResult.Ok("unchanged", hasRecentActivity: activity)), time);
        await scheduler.InitializeAsync();
        for (var i = 0; i < 15; i++)
        {
            Assert.Equal(1, await scheduler.RunDueOnceAsync());
            Assert.Equal(TimeSpan.FromMinutes(1), scheduler.GetHealth("g", "c")!.EffectiveInterval);
            time.Advance(TimeSpan.FromMinutes(1));
        }
    }

    [Fact]
    public async Task Recent_activity_reaccelerates_an_idle_room_without_a_content_change()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([Registration("c", "https://a.example", T0)]);
        var activity = false;
        var scheduler = Scheduler(store, (_, _) => Task.FromResult(
            RoomPollResult.Ok("unchanged", hasRecentActivity: activity)), time);
        await scheduler.InitializeAsync();
        for (var i = 0; i < 10; i++)
        {
            await scheduler.RunDueOnceAsync();
            time.Advance(scheduler.GetHealth("g", "c")!.EffectiveInterval);
        }
        Assert.Equal(TimeSpan.FromMinutes(5), scheduler.GetHealth("g", "c")!.EffectiveInterval);
        activity = true;
        await scheduler.RunDueOnceAsync();
        Assert.Equal(TimeSpan.FromMinutes(1), scheduler.GetHealth("g", "c")!.EffectiveInterval);
    }

    [Fact]
    public async Task Restart_shortens_legacy_hour_delay_but_preserves_failure_backoff()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([Registration("ok", "https://a.example", T0),
            Registration("error", "https://a.example", T0)]);
        foreach (var channel in new[] { "ok", "error" })
            store.States[$"g:{channel}"] = new RoomScheduleState("g", channel, T0.AddHours(1), T0, T0,
                channel == "error" ? 1 : 0, channel == "error" ? PollFailureKind.RateLimited : PollFailureKind.None,
                null, 0, EffectiveIntervalSeconds: 3600);
        var scheduler = Scheduler(store, (_, _) => Task.FromResult(RoomPollResult.Ok()), time);
        await scheduler.InitializeAsync();
        Assert.Equal(T0.AddMinutes(1), scheduler.GetHealth("g", "ok")!.NextPollAtUtc);
        Assert.Equal(T0.AddHours(1), scheduler.GetHealth("g", "error")!.NextPollAtUtc);
    }

    [Fact]
    public async Task Shared_snapshot_survives_one_consumer_cancelling_and_remains_isolated_by_uri()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var http = new HttpClient(new DelegateHandler(async (_, _) =>
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult(true);
            await release.Task;
            return Response(HttpStatusCode.OK, "{\"player_status\":[]}", "application/json");
        }));
        var client = new ResilientWebHostClient(http);
        var uri = new Uri("https://a.example/api/tracker/one");
        using var cancellation = new CancellationTokenSource();
        var first = client.FetchJsonAsync(uri, WebHostEndpointKind.RuntimeTracker, cancellation.Token);
        await entered.Task;
        var second = client.FetchJsonAsync(uri, WebHostEndpointKind.RuntimeTracker, CancellationToken.None);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        release.SetResult(true);
        Assert.True((await second).Success);
        Assert.True((await client.FetchJsonAsync(uri, WebHostEndpointKind.RuntimeTracker, CancellationToken.None)).Success);
        Assert.Equal(1, calls);
        await client.FetchJsonAsync(new Uri("https://b.example/api/tracker/one"), WebHostEndpointKind.RuntimeTracker, CancellationToken.None);
        await client.FetchJsonAsync(new Uri("https://a.example/api/tracker/two"), WebHostEndpointKind.RuntimeTracker, CancellationToken.None);
        Assert.Equal(3, calls);
    }

    [Theory]
    [InlineData("{}", null)]
    [InlineData("{\"activity_timers\":[]}", null)]
    [InlineData("{\"activity_timers\":[null,42]}", null)]
    [InlineData("{\"activity_timers\":[{\"time\":null}]}", null)]
    [InlineData("{\"activity_timers\":[{\"time\":\"invalid\"}]}", null)]
    [InlineData("{\"activity_timers\":[{\"time\":\"Sun, 30 Aug 2026 11:59:00 GMT\"}]}", true)]
    [InlineData("{\"activity_timers\":[{\"time\":\"Sun, 30 Aug 2026 10:00:00 GMT\"}]}", false)]
    [InlineData("{\"activity_timers\":[{\"time\":\"Sun, 30 Aug 2026 10:00:00 GMT\"}],\"connection_timers\":[{\"time\":\"Sun, 30 Aug 2026 12:00:00 GMT\"}]}", true)]
    public void Activity_uses_upstream_timestamps_and_treats_missing_data_as_unknown(string json, bool? expected)
        => Assert.Equal(expected, WebHostActivity.HasRecentActivity(json, T0));

    [Fact]
    public async Task Failed_shared_fetch_is_retried_instead_of_replaying_an_error()
    {
        var calls = 0;
        using var http = new HttpClient(new DelegateHandler((_, _) => Task.FromResult(
            Interlocked.Increment(ref calls) == 1
                ? Response(HttpStatusCode.ServiceUnavailable, "{}", "application/json")
                : Response(HttpStatusCode.OK, "{\"player_status\":[]}", "application/json"))));
        var client = new ResilientWebHostClient(http);
        var uri = new Uri("https://a.example/api/tracker/one");
        Assert.False((await client.FetchJsonAsync(uri, WebHostEndpointKind.RuntimeTracker, CancellationToken.None)).Success);
        Assert.True((await client.FetchJsonAsync(uri, WebHostEndpointKind.RuntimeTracker, CancellationToken.None)).Success);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Portal_refresh_is_shared_throttled_durable_and_cannot_resume_a_paused_room()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([Registration("c", "https://a.example", T0.AddHours(1))]);
        var scheduler = Scheduler(store, (_, _) => Task.FromResult(RoomPollResult.Ok()), time);
        await scheduler.InitializeAsync();
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => scheduler.RefreshIfStaleAsync("g", "c")));
        Assert.Single(outcomes, value => value == TrackingControlOutcome.Accepted);
        Assert.Equal(19, outcomes.Count(value => value == TrackingControlOutcome.RateLimited));
        Assert.Equal(T0, scheduler.GetHealth("g", "c")!.NextPollAtUtc);
        var restarted = Scheduler(store, (_, _) => Task.FromResult(RoomPollResult.Ok()), time);
        await restarted.InitializeAsync();
        Assert.Equal(TrackingControlOutcome.RateLimited, await restarted.RefreshIfStaleAsync("g", "c"));
        await restarted.RunDueOnceAsync();
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(TrackingControlOutcome.RateLimited, await restarted.RefreshIfStaleAsync("g", "c"));
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TrackingControlOutcome.Accepted, await restarted.RefreshIfStaleAsync("g", "c"));
        await restarted.PauseAsync("g", "c");
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(TrackingControlOutcome.Paused, await restarted.RefreshIfStaleAsync("g", "c"));
        Assert.Equal(TrackingControlOutcome.NotFound, await restarted.RefreshIfStaleAsync("other", "c"));
    }

    [Fact]
    public async Task Portal_refresh_respects_retry_after_and_reports_a_running_sync()
    {
        var time = new ManualTimeProvider(T0);
        var store = new MemoryScheduleStore([Registration("c", "https://a.example", T0)]);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduler = Scheduler(store, async (_, _) =>
        {
            entered.SetResult(true);
            await release.Task;
            return RoomPollResult.Failed(PollFailureKind.RateLimited, TimeSpan.FromMinutes(10));
        }, time);
        await scheduler.InitializeAsync();
        var cycle = scheduler.RunDueOnceAsync();
        await entered.Task;
        Assert.Equal(TrackingControlOutcome.Busy, await scheduler.RefreshIfStaleAsync("g", "c"));
        release.SetResult(true);
        await cycle;
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(TrackingControlOutcome.RateLimited, await scheduler.RefreshIfStaleAsync("g", "c"));
        Assert.Equal(T0.AddMinutes(10), scheduler.GetHealth("g", "c")!.NextPollAtUtc);
    }

    private static CentralRoomScheduler Scheduler(
        MemoryScheduleStore store,
        RoomPollExecutor executor,
        TimeProvider time,
        int global = 10,
        int perOrigin = 2)
        => new(store, executor, Options(global, perOrigin), time);

    private static CentralRoomSchedulerOptions Options(int global, int perOrigin)
        => new()
        {
            GlobalConcurrency = global,
            PerOriginConcurrency = perOrigin,
            MaximumJitter = TimeSpan.Zero,
            ReloadInterval = TimeSpan.FromMinutes(1),
            MaximumIdleDelay = TimeSpan.FromMilliseconds(25)
        };

    private static ScheduledRoomRegistration Registration(
        string channel,
        string origin,
        DateTimeOffset due,
        RoomPollingMode pollingMode = RoomPollingMode.Automatic,
        TimeSpan? maximum = null)
        => new(new ScheduledRoomDefinition(
            "g",
            channel,
            origin,
            "tracker",
            origin,
            "room",
            false,
            "0",
            TimeSpan.FromMinutes(5),
            due,
            pollingMode,
            maximum), null);

    private static HttpResponseMessage Response(HttpStatusCode status, string body, string contentType)
        => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType)
        };

    private static void UpdateMaximum(ref int target, int value)
    {
        var observed = Volatile.Read(ref target);
        while (value > observed)
        {
            var previous = Interlocked.CompareExchange(ref target, value, observed);
            if (previous == observed) return;
            observed = previous;
        }
    }

    private sealed class MemoryScheduleStore(IEnumerable<ScheduledRoomRegistration> registrations)
        : IRoomScheduleStore
    {
        private readonly ScheduledRoomRegistration[] _registrations = registrations.ToArray();
        public ConcurrentDictionary<string, RoomScheduleState> States { get; } = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<ScheduledRoomRegistration>> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<ScheduledRoomRegistration> result = _registrations
                .Select(registration => registration with
                {
                    State = States.GetValueOrDefault(registration.Definition.Key) ?? registration.State
                })
                .ToArray();
            return Task.FromResult(result);
        }

        public Task SaveStateAsync(RoomScheduleState state, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            States[$"{state.GuildId}:{state.ChannelId}"] = state;
            return Task.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
    {
        private DateTimeOffset _current = current;
        public override DateTimeOffset GetUtcNow() => _current;
        public void Advance(TimeSpan duration) => _current = _current.Add(duration);
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
