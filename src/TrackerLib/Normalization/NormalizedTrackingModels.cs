using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArchipelagoSphereTracker.Tracking.V2;

[Flags]
public enum SnapshotSections
{
    None = 0,
    Slots = 1 << 0,
    Items = 1 << 1,
    Hints = 1 << 2,
    Checks = 1 << 3,
    Goals = 1 << 4,
    PlayerStatuses = 1 << 5,
    RoomActivity = 1 << 6,
    Tracking = 1 << 7,
    All = Slots | Items | Hints | Checks | Goals | PlayerStatuses | RoomActivity | Tracking
}

public enum NormalizedPlayerStatus
{
    Unknown,
    Connected,
    Ready,
    Playing,
    GoalReached
}

public enum TrackingObservationState
{
    Healthy,
    Error
}

public sealed record NormalizedSlot(int Slot, string PlayerName, string Alias, string Game);

public sealed record NormalizedItemTransfer(
    int FinderSlot,
    int ReceiverSlot,
    long ItemId,
    long LocationId,
    int Flags,
    string FinderDisplayName,
    string ReceiverDisplayName,
    string ItemDisplayName,
    string LocationDisplayName,
    string FinderGame,
    string ReceiverGame);

public sealed record NormalizedHint(
    int FinderSlot,
    int ReceiverSlot,
    long ItemId,
    long LocationId,
    bool Found,
    string Entrance,
    string FinderDisplayName,
    string ReceiverDisplayName,
    string ItemDisplayName,
    string LocationDisplayName,
    string FinderGame,
    string ReceiverGame,
    int ItemFlags = 0,
    int Status = 0);

public sealed record NormalizedCheck(int Slot, long LocationId);

public sealed record NormalizedGoal(int Slot, string GoalId, bool Completed);

public sealed record NormalizedPlayerState(
    int Slot,
    NormalizedPlayerStatus Status,
    int CompletedChecks,
    int? TotalChecks,
    string? ActivityValue);

public sealed record NormalizedRoomSnapshot
{
    private NormalizedRoomSnapshot(
        string guildId,
        string channelId,
        ImmutableArray<NormalizedSlot> slots,
        ImmutableArray<NormalizedItemTransfer> items,
        ImmutableArray<NormalizedHint> hints,
        ImmutableArray<NormalizedCheck> checks,
        ImmutableArray<NormalizedGoal> goals,
        ImmutableArray<NormalizedPlayerState> playerStates,
        DateTimeOffset? lastActivityUtc,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset? lastSuccessfulSyncUtc,
        TrackingObservationState trackingState,
        string? trackingErrorCode,
        SnapshotSections completeSections)
    {
        GuildId = guildId;
        ChannelId = channelId;
        Slots = slots;
        Items = items;
        Hints = hints;
        Checks = checks;
        Goals = goals;
        PlayerStates = playerStates;
        LastActivityUtc = lastActivityUtc;
        CapturedAtUtc = capturedAtUtc;
        LastSuccessfulSyncUtc = lastSuccessfulSyncUtc;
        TrackingState = trackingState;
        TrackingErrorCode = trackingErrorCode;
        CompleteSections = completeSections;
    }

    public string GuildId { get; }
    public string ChannelId { get; }
    public ImmutableArray<NormalizedSlot> Slots { get; }
    public ImmutableArray<NormalizedItemTransfer> Items { get; }
    public ImmutableArray<NormalizedHint> Hints { get; }
    public ImmutableArray<NormalizedCheck> Checks { get; }
    public ImmutableArray<NormalizedGoal> Goals { get; }
    public ImmutableArray<NormalizedPlayerState> PlayerStates { get; }
    public DateTimeOffset? LastActivityUtc { get; }
    public DateTimeOffset CapturedAtUtc { get; }
    public DateTimeOffset? LastSuccessfulSyncUtc { get; }
    public TrackingObservationState TrackingState { get; }
    public string? TrackingErrorCode { get; }
    public SnapshotSections CompleteSections { get; }

    public bool IsComplete(SnapshotSections section) => (CompleteSections & section) == section;

    public string ContentHash => SnapshotContentHasher.Compute(this);

    public static NormalizedRoomSnapshot Create(
        string guildId,
        string channelId,
        IEnumerable<NormalizedSlot>? slots,
        IEnumerable<NormalizedItemTransfer>? items,
        IEnumerable<NormalizedHint>? hints,
        IEnumerable<NormalizedCheck>? checks,
        IEnumerable<NormalizedGoal>? goals,
        IEnumerable<NormalizedPlayerState>? playerStates,
        DateTimeOffset? lastActivityUtc,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset? lastSuccessfulSyncUtc,
        TrackingObservationState trackingState,
        string? trackingErrorCode,
        SnapshotSections completeSections)
    {
        if (string.IsNullOrWhiteSpace(guildId)) throw new ArgumentException("Guild ID is required.", nameof(guildId));
        if (string.IsNullOrWhiteSpace(channelId)) throw new ArgumentException("Channel ID is required.", nameof(channelId));

        return new NormalizedRoomSnapshot(
            guildId,
            channelId,
            (slots ?? [])
                .GroupBy(value => value.Slot)
                .Select(group => group.OrderBy(Canonical, StringComparer.Ordinal).First())
                .OrderBy(value => value.Slot)
                .ToImmutableArray(),
            (items ?? [])
                .GroupBy(ItemIdentity, StringComparer.Ordinal)
                .Select(group => group
                    .OrderBy(value => value.Flags)
                    .ThenBy(Canonical, StringComparer.Ordinal)
                    .First())
                .OrderBy(ItemIdentity, StringComparer.Ordinal)
                .ToImmutableArray(),
            (hints ?? [])
                .GroupBy(HintIdentity, StringComparer.Ordinal)
                .Select(group => group
                    .OrderByDescending(value => value.Found)
                    .ThenByDescending(value => value.Status)
                    .ThenByDescending(value => value.ItemFlags)
                    .ThenBy(Canonical, StringComparer.Ordinal)
                    .First())
                .OrderBy(HintIdentity, StringComparer.Ordinal)
                .ToImmutableArray(),
            (checks ?? [])
                .DistinctBy(value => (value.Slot, value.LocationId))
                .OrderBy(value => value.Slot)
                .ThenBy(value => value.LocationId)
                .ToImmutableArray(),
            (goals ?? [])
                .GroupBy(value => $"{value.Slot}:{value.GoalId}", StringComparer.Ordinal)
                .Select(group => group
                    .OrderByDescending(value => value.Completed)
                    .ThenBy(Canonical, StringComparer.Ordinal)
                    .First())
                .OrderBy(value => value.Slot)
                .ThenBy(value => value.GoalId, StringComparer.Ordinal)
                .ToImmutableArray(),
            (playerStates ?? [])
                .GroupBy(value => value.Slot)
                .Select(group => group
                    .OrderByDescending(value => value.Status)
                    .ThenBy(Canonical, StringComparer.Ordinal)
                    .First())
                .OrderBy(value => value.Slot)
                .ToImmutableArray(),
            lastActivityUtc?.ToUniversalTime(),
            capturedAtUtc.ToUniversalTime(),
            lastSuccessfulSyncUtc?.ToUniversalTime(),
            trackingState,
            NormalizeErrorCode(trackingErrorCode),
            completeSections);
    }

    public static string ItemIdentity(NormalizedItemTransfer item)
        => $"{item.FinderSlot}:{item.ReceiverSlot}:{item.ItemId}:{item.LocationId}";

    public static string HintIdentity(NormalizedHint hint)
        => $"{hint.FinderSlot}:{hint.ReceiverSlot}:{hint.ItemId}:{hint.LocationId}:{hint.Entrance}";

    private static string? NormalizeErrorCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string Canonical<T>(T value) => JsonSerializer.Serialize(value);
}

public abstract record NormalizedTrackingEvent(
    string GuildId,
    string ChannelId,
    DateTimeOffset OccurredAtUtc)
{
    public abstract string EventType { get; }
    protected abstract string StableIdentity { get; }
    public string EventKey => StableEventKey.Create(EventType, GuildId, ChannelId, StableIdentity);
}

public sealed record ItemReceivedEvent(string Guild, string Channel, DateTimeOffset OccurredAt, NormalizedItemTransfer Item)
    : NormalizedTrackingEvent(Guild, Channel, OccurredAt)
{
    public override string EventType => "ItemReceived";
    protected override string StableIdentity => NormalizedRoomSnapshot.ItemIdentity(Item);
}

public sealed record ItemSentEvent(string Guild, string Channel, DateTimeOffset OccurredAt, NormalizedItemTransfer Item)
    : NormalizedTrackingEvent(Guild, Channel, OccurredAt)
{
    public override string EventType => "ItemSent";
    protected override string StableIdentity => NormalizedRoomSnapshot.ItemIdentity(Item);
}

public sealed record HintCreatedEvent(string Guild, string Channel, DateTimeOffset OccurredAt, NormalizedHint Hint)
    : NormalizedTrackingEvent(Guild, Channel, OccurredAt)
{
    public override string EventType => "HintCreated";
    protected override string StableIdentity => NormalizedRoomSnapshot.HintIdentity(Hint);
}

public sealed record HintUpdatedEvent(
    string Guild,
    string Channel,
    DateTimeOffset OccurredAt,
    NormalizedHint Previous,
    NormalizedHint Current)
    : NormalizedTrackingEvent(Guild, Channel, OccurredAt)
{
    public override string EventType => "HintUpdated";
    protected override string StableIdentity
        => $"{NormalizedRoomSnapshot.HintIdentity(Current)}:" +
           $"{Previous.Found}->{Current.Found}:{Previous.Status}->{Current.Status}:" +
           $"{Previous.ItemFlags}->{Current.ItemFlags}:{OccurredAtUtc.UtcTicks}";
}

public sealed record GoalReachedEvent(string Guild, string Channel, DateTimeOffset OccurredAt, NormalizedGoal Goal)
    : NormalizedTrackingEvent(Guild, Channel, OccurredAt)
{
    public override string EventType => "GoalReached";
    protected override string StableIdentity => $"{Goal.Slot}:{Goal.GoalId}";
}

public sealed record PlayerStatusChangedEvent(
    string Guild,
    string Channel,
    DateTimeOffset OccurredAt,
    int Slot,
    NormalizedPlayerStatus Previous,
    NormalizedPlayerStatus Current)
    : NormalizedTrackingEvent(Guild, Channel, OccurredAt)
{
    public override string EventType => "PlayerStatusChanged";
    protected override string StableIdentity => $"{Slot}:{Previous}->{Current}:{OccurredAtUtc.UtcTicks}";
}

public sealed record CheckCompletedEvent(string Guild, string Channel, DateTimeOffset OccurredAt, NormalizedCheck Check)
    : NormalizedTrackingEvent(Guild, Channel, OccurredAt)
{
    public override string EventType => "CheckCompleted";
    protected override string StableIdentity => $"{Check.Slot}:{Check.LocationId}";
}

public sealed record RoomActivityChangedEvent(
    string Guild,
    string Channel,
    DateTimeOffset OccurredAt,
    DateTimeOffset? Previous,
    DateTimeOffset Current)
    : NormalizedTrackingEvent(Guild, Channel, OccurredAt)
{
    public override string EventType => "RoomActivityChanged";
    protected override string StableIdentity => $"{Previous?.UtcTicks ?? 0}:{Current.UtcTicks}";
}

public sealed record TrackingErrorEvent(string Guild, string Channel, DateTimeOffset OccurredAt, string ErrorCode)
    : NormalizedTrackingEvent(Guild, Channel, OccurredAt)
{
    public override string EventType => "TrackingError";
    protected override string StableIdentity => $"{ErrorCode}:{OccurredAtUtc.UtcTicks}";
}

public sealed record TrackingRecoveredEvent(
    string Guild,
    string Channel,
    DateTimeOffset OccurredAt,
    string? PreviousErrorCode)
    : NormalizedTrackingEvent(Guild, Channel, OccurredAt)
{
    public override string EventType => "TrackingRecovered";
    protected override string StableIdentity => $"{PreviousErrorCode ?? "UNKNOWN"}:{OccurredAtUtc.UtcTicks}";
}

internal static class StableEventKey
{
    public static string Create(string eventType, string guildId, string channelId, string identity)
    {
        var canonical = $"ast-v2\n{eventType}\n{guildId}\n{channelId}\n{identity}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}

internal static class SnapshotContentHasher
{
    public static string Compute(NormalizedRoomSnapshot snapshot)
    {
        var canonical = new
        {
            schema = 2,
            snapshot.GuildId,
            snapshot.ChannelId,
            completeSections = (int)snapshot.CompleteSections,
            snapshot.Slots,
            snapshot.Items,
            snapshot.Hints,
            snapshot.Checks,
            snapshot.Goals,
            snapshot.PlayerStates,
            lastActivityUtc = snapshot.LastActivityUtc?.ToString("O"),
            trackingState = snapshot.TrackingState.ToString(),
            snapshot.TrackingErrorCode
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(canonical);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
