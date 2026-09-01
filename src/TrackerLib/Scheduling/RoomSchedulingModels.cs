namespace ArchipelagoSphereTracker.Tracking.Scheduling;

public enum PollFailureKind
{
    None,
    NotFound,
    RateLimited,
    ServerError,
    Timeout,
    Network,
    InvalidContentType,
    InvalidJson,
    PartialResponse,
    CircuitOpen,
    Unexpected
}

public enum RoomPollingMode
{
    Automatic,
    Fixed
}

public sealed record RoomPollResult(
    bool Success,
    PollFailureKind FailureKind = PollFailureKind.None,
    TimeSpan? RetryAfter = null,
    bool AffectsOriginBreaker = false,
    bool RemoveRoom = false,
    string? ContentHash = null)
{
    public static RoomPollResult Ok(string? contentHash = null) => new(true, ContentHash: contentHash);
    public static RoomPollResult Removed() => new(true, RemoveRoom: true);

    public static RoomPollResult Failed(
        PollFailureKind kind,
        TimeSpan? retryAfter = null,
        bool affectsOriginBreaker = false)
        => new(false, kind, retryAfter, affectsOriginBreaker);
}

public sealed record ScheduledRoomDefinition(
    string GuildId,
    string ChannelId,
    string Origin,
    string Tracker,
    string BaseUrl,
    string Room,
    bool Silent,
    string Port,
    TimeSpan PollInterval,
    DateTimeOffset InitialNextPollAtUtc,
    RoomPollingMode PollingMode = RoomPollingMode.Automatic,
    TimeSpan? MaximumPollInterval = null)
{
    public string Key => $"{GuildId}:{ChannelId}";
}

public sealed record RoomScheduleState(
    string GuildId,
    string ChannelId,
    DateTimeOffset NextPollAtUtc,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? LastSuccessAtUtc,
    int ConsecutiveFailures,
    PollFailureKind LastFailureKind,
    DateTimeOffset? BreakerOpenUntilUtc,
    double LastLatencyMilliseconds,
    bool IsPaused = false,
    DateTimeOffset? PausedAtUtc = null,
    DateTimeOffset? LastForcedSyncAtUtc = null,
    string? LastContentHash = null,
    int UnchangedSuccessCount = 0,
    double EffectiveIntervalSeconds = 0,
    DateTimeOffset? LastChangeAtUtc = null);

public enum TrackingControlOutcome
{
    Accepted,
    NotFound,
    AlreadyPaused,
    AlreadyRunning,
    Paused,
    Busy,
    RateLimited,
    Unavailable
}

public sealed record RoomHealthSnapshot(
    string GuildId,
    string ChannelId,
    bool IsPaused,
    bool IsRunning,
    DateTimeOffset NextPollAtUtc,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? LastSuccessAtUtc,
    int ConsecutiveFailures,
    PollFailureKind LastFailureKind,
    DateTimeOffset? BreakerOpenUntilUtc,
    TimeSpan ConfiguredInterval,
    TimeSpan EffectiveInterval,
    int UnchangedSuccessCount,
    DateTimeOffset? LastChangeAtUtc,
    double LastLatencyMilliseconds,
    RoomPollingMode PollingMode,
    TimeSpan MaximumPollInterval);

public sealed record ScheduledRoomRegistration(
    ScheduledRoomDefinition Definition,
    RoomScheduleState? State);

public interface IRoomScheduleStore
{
    Task<IReadOnlyList<ScheduledRoomRegistration>> LoadAsync(CancellationToken cancellationToken);
    Task SaveStateAsync(RoomScheduleState state, CancellationToken cancellationToken);
}

public delegate Task<RoomPollResult> RoomPollExecutor(
    ScheduledRoomDefinition room,
    CancellationToken cancellationToken);

public interface ICentralSchedulerMetrics
{
    void SetQueueDepth(int value);
    void SetActivePolls(int value);
    void ObserveQueueLag(TimeSpan lag);
    void ObservePoll(TimeSpan duration, RoomPollResult result);
    void SetOpenBreakers(int value);
}

public sealed class NullCentralSchedulerMetrics : ICentralSchedulerMetrics
{
    public static NullCentralSchedulerMetrics Instance { get; } = new();

    public void SetQueueDepth(int value) { }
    public void SetActivePolls(int value) { }
    public void ObserveQueueLag(TimeSpan lag) { }
    public void ObservePoll(TimeSpan duration, RoomPollResult result) { }
    public void SetOpenBreakers(int value) { }
}
