using System.Collections.Concurrent;
using System.Diagnostics;

namespace ArchipelagoSphereTracker.Tracking.Scheduling;

public sealed record CentralRoomSchedulerOptions
{
    public int GlobalConcurrency { get; init; } = 10;
    public int PerOriginConcurrency { get; init; } = 2;
    public int BreakerFailureThreshold { get; init; } = 5;
    public TimeSpan BreakerDuration { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan MaximumBackoff { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan MaximumJitter { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan ReloadInterval { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan PromotionCooldown { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaximumIdleDelay { get; init; } = TimeSpan.FromSeconds(5);
    public int UnchangedPollsBeforeSlowdown { get; init; } = 3;
    public double AdaptiveIntervalMultiplier { get; init; } = 2;
    public TimeSpan MaximumAdaptiveInterval { get; init; } = TimeSpan.FromHours(1);
}

public sealed class CentralRoomScheduler
{
    private readonly IRoomScheduleStore _store;
    private readonly RoomPollExecutor _executor;
    private readonly CentralRoomSchedulerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ICentralSchedulerMetrics _metrics;
    private readonly SemaphoreSlim _cycleGate = new(1, 1);
    private readonly SemaphoreSlim _globalGate;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _originGates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeRoom> _rooms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OriginHealth> _origins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastPromotions = new(StringComparer.Ordinal);
    private readonly PriorityQueue<QueueEntry, (long DueTicks, long Sequence)> _queue = new();
    private readonly object _sync = new();
    private long _sequence;
    private int _runStarted;
    private int _activePolls;
    private DateTimeOffset _nextReloadAtUtc;

    public CentralRoomScheduler(
        IRoomScheduleStore store,
        RoomPollExecutor executor,
        CentralRoomSchedulerOptions? options = null,
        TimeProvider? timeProvider = null,
        ICentralSchedulerMetrics? metrics = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _options = options ?? new CentralRoomSchedulerOptions();
        ValidateOptions(_options);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _metrics = metrics ?? NullCentralSchedulerMetrics.Instance;
        _globalGate = new SemaphoreSlim(_options.GlobalConcurrency, _options.GlobalConcurrency);
    }

    public int RoomCount
    {
        get { lock (_sync) return _rooms.Count; }
    }

    public int QueueDepth
    {
        get { lock (_sync) return _rooms.Values.Count(room => !room.Running && !room.State.IsPaused); }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
        _nextReloadAtUtc = _timeProvider.GetUtcNow().Add(_options.ReloadInterval);
    }

    public Task ReloadConfigurationAsync(CancellationToken cancellationToken = default)
        => ReloadAsync(cancellationToken);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _runStarted, 1) != 0)
            throw new InvalidOperationException("This scheduler instance is already running or has already stopped.");

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                var now = _timeProvider.GetUtcNow();
                if (now >= _nextReloadAtUtc)
                {
                    await ReloadAsync(cancellationToken).ConfigureAwait(false);
                    _nextReloadAtUtc = now.Add(_options.ReloadInterval);
                }

                var processed = await RunDueOnceAsync(cancellationToken).ConfigureAwait(false);
                if (processed > 0)
                    continue;

                var delay = GetNextDelay(_timeProvider.GetUtcNow());
                await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async Task<int> RunDueOnceAsync(CancellationToken cancellationToken = default)
    {
        await _cycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var running = new List<RunningPoll>();
        var activeByOrigin = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var processed = 0;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var now = _timeProvider.GetUtcNow();
                if (now >= _nextReloadAtUtc)
                    await ReloadAsync(cancellationToken).ConfigureAwait(false);

                var available = _options.GlobalConcurrency - running.Count;
                if (available > 0)
                {
                    var due = TakeDueRooms(now, available, activeByOrigin);
                    foreach (var room in due)
                    {
                        activeByOrigin.TryGetValue(room.Definition.Origin, out var activeForOrigin);
                        activeByOrigin[room.Definition.Origin] = activeForOrigin + 1;
                        running.Add(new RunningPoll(
                            room.Definition.Origin,
                            ExecuteRoomAsync(room, cancellationToken)));
                        processed++;
                    }
                }

                if (running.Count == 0)
                    return processed;

                await Task.WhenAny(running.Select(poll => poll.Task)).ConfigureAwait(false);
                var completedPolls = running.Where(poll => poll.Task.IsCompleted).ToArray();
                foreach (var completed in completedPolls)
                {
                    running.Remove(completed);
                    if (activeByOrigin[completed.Origin] == 1)
                        activeByOrigin.Remove(completed.Origin);
                    else
                        activeByOrigin[completed.Origin]--;
                    await completed.Task.ConfigureAwait(false);
                }
            }
        }
        finally
        {
            // Observe every poll already dispatched before releasing the cycle gate. This
            // matters when cancellation or durable-state persistence fails mid-cycle.
            foreach (var poll in running)
            {
                try
                {
                    await poll.Task.ConfigureAwait(false);
                }
                catch
                {
                }
            }
            _cycleGate.Release();
        }
    }

    public async Task<bool> PromoteAsync(
        string guildId,
        string channelId,
        CancellationToken cancellationToken = default)
    {
        var key = $"{guildId}:{channelId}";
        var now = _timeProvider.GetUtcNow();
        RoomScheduleState? state = null;

        lock (_sync)
        {
            if (!_rooms.TryGetValue(key, out var room) || room.Running)
                return false;

            if (_lastPromotions.TryGetValue(key, out var last) && now - last < _options.PromotionCooldown)
                return false;

            _lastPromotions[key] = now;
            room.State = room.State with { NextPollAtUtc = now };
            EnqueueLocked(room);
            state = room.State;
        }

        await _store.SaveStateAsync(state, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<TrackingControlOutcome> PauseAsync(
        string guildId,
        string channelId,
        CancellationToken cancellationToken = default)
    {
        var key = $"{guildId}:{channelId}";
        RoomScheduleState state;
        lock (_sync)
        {
            if (!_rooms.TryGetValue(key, out var room))
                return TrackingControlOutcome.NotFound;
            if (room.State.IsPaused)
                return TrackingControlOutcome.AlreadyPaused;

            var now = _timeProvider.GetUtcNow();
            room.State = room.State with { IsPaused = true, PausedAtUtc = now };
            room.Version++; // invalidate any queued entry without enqueuing another one
            state = room.State;
            UpdateMetricsLocked(now);
        }

        await _store.SaveStateAsync(state, cancellationToken).ConfigureAwait(false);
        return TrackingControlOutcome.Accepted;
    }

    public async Task<TrackingControlOutcome> ResumeAsync(
        string guildId,
        string channelId,
        CancellationToken cancellationToken = default)
    {
        var key = $"{guildId}:{channelId}";
        RoomScheduleState state;
        lock (_sync)
        {
            if (!_rooms.TryGetValue(key, out var room))
                return TrackingControlOutcome.NotFound;
            if (!room.State.IsPaused)
                return TrackingControlOutcome.AlreadyRunning;

            var now = _timeProvider.GetUtcNow();
            room.State = room.State with
            {
                IsPaused = false,
                PausedAtUtc = null,
                NextPollAtUtc = now
            };
            EnqueueLocked(room);
            state = room.State;
            UpdateMetricsLocked(now);
        }

        await _store.SaveStateAsync(state, cancellationToken).ConfigureAwait(false);
        return TrackingControlOutcome.Accepted;
    }

    public async Task<TrackingControlOutcome> ForceSyncAsync(
        string guildId,
        string channelId,
        CancellationToken cancellationToken = default)
    {
        var key = $"{guildId}:{channelId}";
        RoomScheduleState state;
        lock (_sync)
        {
            if (!_rooms.TryGetValue(key, out var room))
                return TrackingControlOutcome.NotFound;
            if (room.State.IsPaused)
                return TrackingControlOutcome.Paused;
            if (room.Running)
                return TrackingControlOutcome.Busy;

            var now = _timeProvider.GetUtcNow();
            if (room.State.LastForcedSyncAtUtc is { } lastForced &&
                now - lastForced < _options.PromotionCooldown)
            {
                return TrackingControlOutcome.RateLimited;
            }

            room.State = room.State with
            {
                NextPollAtUtc = now,
                LastForcedSyncAtUtc = now
            };
            EnqueueLocked(room);
            state = room.State;
            UpdateMetricsLocked(now);
        }

        await _store.SaveStateAsync(state, cancellationToken).ConfigureAwait(false);
        return TrackingControlOutcome.Accepted;
    }

    public RoomHealthSnapshot? GetHealth(string guildId, string channelId)
    {
        var key = $"{guildId}:{channelId}";
        lock (_sync)
        {
            if (!_rooms.TryGetValue(key, out var room))
                return null;

            return CreateHealthSnapshotLocked(room);
        }
    }

    public IReadOnlyList<RoomHealthSnapshot> GetGuildHealth(string guildId)
    {
        lock (_sync)
        {
            return _rooms.Values
                .Where(room => string.Equals(room.Definition.GuildId, guildId, StringComparison.Ordinal))
                .Select(CreateHealthSnapshotLocked)
                .OrderBy(snapshot => snapshot.ChannelId, StringComparer.Ordinal)
                .ToArray();
        }
    }

    private RoomHealthSnapshot CreateHealthSnapshotLocked(RuntimeRoom room)
    {
        var effective = room.State.EffectiveIntervalSeconds > 0
            ? TimeSpan.FromSeconds(room.State.EffectiveIntervalSeconds)
            : room.Definition.PollInterval;
        return new RoomHealthSnapshot(
            room.Definition.GuildId,
            room.Definition.ChannelId,
            room.State.IsPaused,
            room.Running,
            room.State.NextPollAtUtc,
            room.State.LastAttemptAtUtc,
            room.State.LastSuccessAtUtc,
            room.State.ConsecutiveFailures,
            room.State.LastFailureKind,
            room.State.BreakerOpenUntilUtc,
            room.Definition.PollInterval,
            effective,
            room.State.UnchangedSuccessCount,
            room.State.LastChangeAtUtc,
            room.State.LastLatencyMilliseconds,
            room.Definition.PollingMode,
            GetMaximumInterval(room.Definition));
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var registrations = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        var seen = registrations.Select(registration => registration.Definition.Key)
            .ToHashSet(StringComparer.Ordinal);

        lock (_sync)
        {
            foreach (var registration in registrations)
            {
                if (_rooms.TryGetValue(registration.Definition.Key, out var existing))
                {
                    var configurationChanged = existing.Definition.PollInterval != registration.Definition.PollInterval ||
                                               existing.Definition.PollingMode != registration.Definition.PollingMode ||
                                               existing.Definition.MaximumPollInterval != registration.Definition.MaximumPollInterval;
                    existing.Definition = registration.Definition;
                    if (configurationChanged)
                    {
                        existing.State = existing.State with
                        {
                            NextPollAtUtc = now,
                            EffectiveIntervalSeconds = registration.Definition.PollInterval.TotalSeconds,
                            UnchangedSuccessCount = 0
                        };
                        if (!existing.State.IsPaused && !existing.Running)
                            EnqueueLocked(existing);
                    }
                    existing.RemovedOnReload = false;
                    continue;
                }

                var state = registration.State ?? new RoomScheduleState(
                    registration.Definition.GuildId,
                    registration.Definition.ChannelId,
                    registration.Definition.InitialNextPollAtUtc,
                    null,
                    null,
                    0,
                    PollFailureKind.None,
                    null,
                    0,
                    EffectiveIntervalSeconds: registration.Definition.PollInterval.TotalSeconds);
                if (state.EffectiveIntervalSeconds <= 0)
                    state = state with { EffectiveIntervalSeconds = registration.Definition.PollInterval.TotalSeconds };
                if (registration.Definition.PollingMode == RoomPollingMode.Fixed ||
                    state.EffectiveIntervalSeconds < registration.Definition.PollInterval.TotalSeconds ||
                    state.EffectiveIntervalSeconds > GetMaximumInterval(registration.Definition).TotalSeconds)
                {
                    state = state with
                    {
                        EffectiveIntervalSeconds = registration.Definition.PollInterval.TotalSeconds,
                        UnchangedSuccessCount = 0
                    };
                }
                var room = new RuntimeRoom(registration.Definition, state);
                _rooms.Add(registration.Definition.Key, room);
                if (state.BreakerOpenUntilUtc is { } persistedBreaker && persistedBreaker > now)
                {
                    if (!_origins.TryGetValue(registration.Definition.Origin, out var health) ||
                        health.OpenUntilUtc < persistedBreaker)
                    {
                        _origins[registration.Definition.Origin] = new OriginHealth
                        {
                            OpenUntilUtc = persistedBreaker
                        };
                    }
                }
                if (!state.IsPaused)
                    EnqueueLocked(room);
            }

            foreach (var removed in _rooms.Keys.Where(key => !seen.Contains(key)).ToArray())
            {
                if (!_rooms[removed].Running)
                    _rooms.Remove(removed);
                else
                    _rooms[removed].RemovedOnReload = true;
            }

            _nextReloadAtUtc = now.Add(_options.ReloadInterval);
            UpdateMetricsLocked(now);
        }
    }

    private List<RuntimeRoom> TakeDueRooms(
        DateTimeOffset now,
        int maximum,
        IReadOnlyDictionary<string, int> activeByOrigin)
    {
        var due = new List<RuntimeRoom>(maximum);
        var deferred = new List<(QueueEntry Entry, (long DueTicks, long Sequence) Priority)>();
        var reservedByOrigin = new Dictionary<string, int>(activeByOrigin, StringComparer.OrdinalIgnoreCase);
        lock (_sync)
        {
            while (due.Count < maximum && _queue.TryPeek(out var entry, out var priority))
            {
                if (priority.DueTicks > now.UtcTicks)
                    break;

                _queue.Dequeue();
                if (!_rooms.TryGetValue(entry.Key, out var room) ||
                    room.Version != entry.Version ||
                    room.Running ||
                    room.State.IsPaused)
                {
                    continue;
                }

                if (TryGetOpenBreakerUntilLocked(room.Definition.Origin, now, out var breakerUntil))
                {
                    room.State = room.State with
                    {
                        NextPollAtUtc = breakerUntil,
                        BreakerOpenUntilUtc = breakerUntil,
                        LastFailureKind = PollFailureKind.CircuitOpen
                    };
                    EnqueueLocked(room);
                    continue;
                }

                reservedByOrigin.TryGetValue(room.Definition.Origin, out var reservedForOrigin);
                if (reservedForOrigin >= _options.PerOriginConcurrency)
                {
                    deferred.Add((entry, priority));
                    continue;
                }

                room.Running = true;
                due.Add(room);
                reservedByOrigin[room.Definition.Origin] = reservedForOrigin + 1;
                _metrics.ObserveQueueLag(now - room.State.NextPollAtUtc);
            }

            foreach (var item in deferred)
                _queue.Enqueue(item.Entry, item.Priority);

            UpdateMetricsLocked(now);
        }

        return due;
    }

    private async Task ExecuteRoomAsync(RuntimeRoom room, CancellationToken cancellationToken)
    {
        var originGate = _originGates.GetOrAdd(
            room.Definition.Origin,
            _ => new SemaphoreSlim(_options.PerOriginConcurrency, _options.PerOriginConcurrency));
        await _globalGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await originGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var startedAt = _timeProvider.GetUtcNow();
                Interlocked.Increment(ref _activePolls);
                _metrics.SetActivePolls(Volatile.Read(ref _activePolls));
                RoomPollResult result;
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    result = await _executor(room.Definition, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    result = RoomPollResult.Failed(
                        PollFailureKind.Unexpected,
                        affectsOriginBreaker: true);
                }
                finally
                {
                    stopwatch.Stop();
                    Interlocked.Decrement(ref _activePolls);
                    _metrics.SetActivePolls(Volatile.Read(ref _activePolls));
                }

                var finishedAt = _timeProvider.GetUtcNow();
                var duration = finishedAt > startedAt
                    ? finishedAt - startedAt
                    : stopwatch.Elapsed;
                _metrics.ObservePoll(duration, result);
                await CompleteRoomAsync(room, result, finishedAt, duration, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                originGate.Release();
            }
        }
        finally
        {
            _globalGate.Release();
        }
    }

    private async Task CompleteRoomAsync(
        RuntimeRoom room,
        RoomPollResult result,
        DateTimeOffset now,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        RoomScheduleState state;
        lock (_sync)
        {
            if (result.RemoveRoom || room.RemovedOnReload)
            {
                _rooms.Remove(room.Definition.Key);
                _lastPromotions.Remove(room.Definition.Key);
                UpdateMetricsLocked(now);
                return;
            }

            var failures = result.Success ? 0 : room.State.ConsecutiveFailures + 1;
            DateTimeOffset nextPoll;
            DateTimeOffset? breakerUntil = null;
            var lastContentHash = room.State.LastContentHash;
            var unchangedSuccesses = room.State.UnchangedSuccessCount;
            var effectiveInterval = room.State.EffectiveIntervalSeconds > 0
                ? TimeSpan.FromSeconds(room.State.EffectiveIntervalSeconds)
                : room.Definition.PollInterval;
            var lastChangeAt = room.State.LastChangeAtUtc;

            if (result.Success)
            {
                ResetOriginLocked(room.Definition.Origin);
                if (!string.IsNullOrWhiteSpace(result.ContentHash))
                {
                    if (string.IsNullOrWhiteSpace(lastContentHash))
                    {
                        unchangedSuccesses = 0;
                        effectiveInterval = room.Definition.PollInterval;
                        lastChangeAt ??= now;
                    }
                    else if (string.Equals(lastContentHash, result.ContentHash, StringComparison.Ordinal))
                    {
                        if (room.Definition.PollingMode == RoomPollingMode.Automatic)
                        {
                            unchangedSuccesses++;
                            effectiveInterval = ComputeAdaptiveInterval(room.Definition, unchangedSuccesses);
                        }
                    }
                    else
                    {
                        unchangedSuccesses = 0;
                        effectiveInterval = room.Definition.PollInterval;
                        lastChangeAt = now;
                    }
                    lastContentHash = result.ContentHash;
                }
                else
                {
                    unchangedSuccesses = 0;
                    effectiveInterval = room.Definition.PollInterval;
                }
                if (room.Definition.PollingMode == RoomPollingMode.Fixed)
                {
                    unchangedSuccesses = 0;
                    effectiveInterval = room.Definition.PollInterval;
                }
                nextPoll = now
                    .Add(effectiveInterval)
                    .Add(ComputePositiveJitter(room.Definition.Key, now));
            }
            else
            {
                if (result.AffectsOriginBreaker)
                    breakerUntil = RecordOriginFailureLocked(room.Definition.Origin, now);

                var delay = ComputeBackoff(failures);
                if (result.RetryAfter is { } retryAfter && retryAfter > delay)
                    delay = retryAfter;
                if (result.FailureKind == PollFailureKind.NotFound && room.Definition.PollInterval > delay)
                    delay = room.Definition.PollInterval;
                if (breakerUntil is { } openUntil && openUntil > now.Add(delay))
                    delay = openUntil - now;
                nextPoll = now.Add(delay).Add(ComputePositiveJitter(room.Definition.Key, now));
            }

            state = room.State = new RoomScheduleState(
                room.Definition.GuildId,
                room.Definition.ChannelId,
                nextPoll,
                now,
                result.Success ? now : room.State.LastSuccessAtUtc,
                failures,
                result.Success ? PollFailureKind.None : result.FailureKind,
                breakerUntil,
                Math.Max(0, duration.TotalMilliseconds),
                room.State.IsPaused,
                room.State.PausedAtUtc,
                room.State.LastForcedSyncAtUtc,
                lastContentHash,
                unchangedSuccesses,
                effectiveInterval.TotalSeconds,
                lastChangeAt);
            room.Running = false;
            if (!state.IsPaused)
                EnqueueLocked(room);
            UpdateMetricsLocked(now);
        }

        await _store.SaveStateAsync(state, cancellationToken).ConfigureAwait(false);
    }

    private DateTimeOffset? RecordOriginFailureLocked(string origin, DateTimeOffset now)
    {
        if (!_origins.TryGetValue(origin, out var health))
            health = new OriginHealth();
        health.ConsecutiveFailures++;
        if (health.ConsecutiveFailures >= _options.BreakerFailureThreshold)
        {
            health.OpenUntilUtc = now.Add(_options.BreakerDuration);
            health.ConsecutiveFailures = 0;
        }
        _origins[origin] = health;
        return health.OpenUntilUtc;
    }

    private void ResetOriginLocked(string origin)
        => _origins.Remove(origin);

    private bool TryGetOpenBreakerUntilLocked(
        string origin,
        DateTimeOffset now,
        out DateTimeOffset openUntil)
    {
        if (_origins.TryGetValue(origin, out var health) && health.OpenUntilUtc > now)
        {
            openUntil = health.OpenUntilUtc.Value;
            return true;
        }

        if (health?.OpenUntilUtc <= now)
            _origins.Remove(origin);
        openUntil = default;
        return false;
    }

    private TimeSpan ComputeBackoff(int failureCount)
    {
        var exponent = Math.Min(Math.Max(failureCount - 1, 0), 20);
        var seconds = Math.Min(Math.Pow(2, exponent), _options.MaximumBackoff.TotalSeconds);
        return TimeSpan.FromSeconds(Math.Max(1, seconds));
    }

    private TimeSpan ComputeAdaptiveInterval(ScheduledRoomDefinition room, int unchangedSuccesses)
    {
        var steps = unchangedSuccesses / _options.UnchangedPollsBeforeSlowdown;
        if (steps <= 0)
            return room.PollInterval;
        var factor = Math.Pow(_options.AdaptiveIntervalMultiplier, Math.Min(steps, 20));
        var seconds = Math.Min(
            room.PollInterval.TotalSeconds * factor,
            GetMaximumInterval(room).TotalSeconds);
        return TimeSpan.FromSeconds(Math.Max(room.PollInterval.TotalSeconds, seconds));
    }

    private TimeSpan GetMaximumInterval(ScheduledRoomDefinition room)
    {
        var configuredMaximum = room.MaximumPollInterval ?? _options.MaximumAdaptiveInterval;
        return configuredMaximum < room.PollInterval ? room.PollInterval : configuredMaximum;
    }

    private TimeSpan ComputePositiveJitter(string key, DateTimeOffset now)
    {
        if (_options.MaximumJitter <= TimeSpan.Zero)
            return TimeSpan.Zero;

        var hash = HashCode.Combine(StringComparer.Ordinal.GetHashCode(key), now.UtcTicks / TimeSpan.TicksPerMinute);
        var fraction = (uint)hash / (double)uint.MaxValue;
        return TimeSpan.FromTicks((long)(_options.MaximumJitter.Ticks * fraction));
    }

    private TimeSpan GetNextDelay(DateTimeOffset now)
    {
        lock (_sync)
        {
            var delay = _options.MaximumIdleDelay;
            while (_queue.TryPeek(out var entry, out var priority))
            {
                if (!_rooms.TryGetValue(entry.Key, out var room) || room.Version != entry.Version || room.Running)
                {
                    _queue.Dequeue();
                    continue;
                }

                var untilDue = TimeSpan.FromTicks(Math.Max(0, priority.DueTicks - now.UtcTicks));
                if (untilDue < delay) delay = untilDue;
                break;
            }

            var untilReload = _nextReloadAtUtc - now;
            if (untilReload > TimeSpan.Zero && untilReload < delay) delay = untilReload;
            return delay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(10) : delay;
        }
    }

    private void EnqueueLocked(RuntimeRoom room)
    {
        room.Version++;
        var sequence = Interlocked.Increment(ref _sequence);
        _queue.Enqueue(
            new QueueEntry(room.Definition.Key, room.Version),
            (room.State.NextPollAtUtc.UtcTicks, sequence));
    }

    private void UpdateMetricsLocked(DateTimeOffset now)
    {
        _metrics.SetQueueDepth(_rooms.Values.Count(room => !room.Running && !room.State.IsPaused));
        _metrics.SetOpenBreakers(_origins.Values.Count(origin => origin.OpenUntilUtc > now));
    }

    private static void ValidateOptions(CentralRoomSchedulerOptions options)
    {
        if (options.GlobalConcurrency <= 0) throw new ArgumentOutOfRangeException(nameof(options.GlobalConcurrency));
        if (options.PerOriginConcurrency <= 0) throw new ArgumentOutOfRangeException(nameof(options.PerOriginConcurrency));
        if (options.BreakerFailureThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(options.BreakerFailureThreshold));
        if (options.BreakerDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.BreakerDuration));
        if (options.MaximumBackoff <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.MaximumBackoff));
        if (options.MaximumJitter < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.MaximumJitter));
        if (options.ReloadInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.ReloadInterval));
        if (options.PromotionCooldown < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.PromotionCooldown));
        if (options.MaximumIdleDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.MaximumIdleDelay));
        if (options.UnchangedPollsBeforeSlowdown <= 0) throw new ArgumentOutOfRangeException(nameof(options.UnchangedPollsBeforeSlowdown));
        if (options.AdaptiveIntervalMultiplier <= 1) throw new ArgumentOutOfRangeException(nameof(options.AdaptiveIntervalMultiplier));
        if (options.MaximumAdaptiveInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.MaximumAdaptiveInterval));
    }

    private sealed class RuntimeRoom(ScheduledRoomDefinition definition, RoomScheduleState state)
    {
        public ScheduledRoomDefinition Definition { get; set; } = definition;
        public RoomScheduleState State { get; set; } = state;
        public long Version { get; set; }
        public bool Running { get; set; }
        public bool RemovedOnReload { get; set; }
    }

    private sealed class OriginHealth
    {
        public int ConsecutiveFailures { get; set; }
        public DateTimeOffset? OpenUntilUtc { get; set; }
    }

    private readonly record struct QueueEntry(string Key, long Version);
    private sealed record RunningPoll(string Origin, Task Task);
}
