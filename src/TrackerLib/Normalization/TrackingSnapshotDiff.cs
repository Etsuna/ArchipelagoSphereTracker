using System.Collections.Immutable;

namespace ArchipelagoSphereTracker.Tracking.V2;

public static class TrackingSnapshotDiff
{
    public static ImmutableArray<NormalizedTrackingEvent> Diff(
        NormalizedRoomSnapshot? previous,
        NormalizedRoomSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (previous == null)
            return ImmutableArray<NormalizedTrackingEvent>.Empty;

        if (!string.Equals(previous.GuildId, current.GuildId, StringComparison.Ordinal) ||
            !string.Equals(previous.ChannelId, current.ChannelId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Snapshots must belong to the same guild and channel.", nameof(current));
        }

        var events = ImmutableArray.CreateBuilder<NormalizedTrackingEvent>();
        AddItemEvents(previous, current, events);
        AddHintEvents(previous, current, events);
        AddGoalEvents(previous, current, events);
        AddPlayerStatusEvents(previous, current, events);
        AddCheckEvents(previous, current, events);
        AddRoomActivityEvent(previous, current, events);
        AddTrackingEvents(previous, current, events);
        return events.ToImmutable();
    }

    private static bool CanCompare(
        NormalizedRoomSnapshot previous,
        NormalizedRoomSnapshot current,
        SnapshotSections section)
        => previous.IsComplete(section) && current.IsComplete(section);

    private static void AddItemEvents(
        NormalizedRoomSnapshot previous,
        NormalizedRoomSnapshot current,
        ImmutableArray<NormalizedTrackingEvent>.Builder events)
    {
        if (!CanCompare(previous, current, SnapshotSections.Items)) return;

        var known = previous.Items.Select(NormalizedRoomSnapshot.ItemIdentity).ToHashSet(StringComparer.Ordinal);
        foreach (var item in current.Items.Where(item => !known.Contains(NormalizedRoomSnapshot.ItemIdentity(item))))
        {
            events.Add(new ItemReceivedEvent(current.GuildId, current.ChannelId, current.CapturedAtUtc, item));
            events.Add(new ItemSentEvent(current.GuildId, current.ChannelId, current.CapturedAtUtc, item));
        }
    }

    private static void AddHintEvents(
        NormalizedRoomSnapshot previous,
        NormalizedRoomSnapshot current,
        ImmutableArray<NormalizedTrackingEvent>.Builder events)
    {
        if (!CanCompare(previous, current, SnapshotSections.Hints)) return;

        var known = previous.Hints.ToDictionary(NormalizedRoomSnapshot.HintIdentity, StringComparer.Ordinal);
        foreach (var hint in current.Hints)
        {
            var identity = NormalizedRoomSnapshot.HintIdentity(hint);
            if (!known.TryGetValue(identity, out var oldHint))
                events.Add(new HintCreatedEvent(current.GuildId, current.ChannelId, current.CapturedAtUtc, hint));
            else if (oldHint.Found != hint.Found ||
                     oldHint.Status != hint.Status ||
                     oldHint.ItemFlags != hint.ItemFlags)
                events.Add(new HintUpdatedEvent(
                    current.GuildId,
                    current.ChannelId,
                    current.CapturedAtUtc,
                    oldHint,
                    hint));
        }
    }

    private static void AddGoalEvents(
        NormalizedRoomSnapshot previous,
        NormalizedRoomSnapshot current,
        ImmutableArray<NormalizedTrackingEvent>.Builder events)
    {
        if (!CanCompare(previous, current, SnapshotSections.Goals)) return;

        var known = previous.Goals.ToDictionary(goal => $"{goal.Slot}:{goal.GoalId}", StringComparer.Ordinal);
        foreach (var goal in current.Goals.Where(goal => goal.Completed))
        {
            var identity = $"{goal.Slot}:{goal.GoalId}";
            if (!known.TryGetValue(identity, out var oldGoal) || !oldGoal.Completed)
                events.Add(new GoalReachedEvent(current.GuildId, current.ChannelId, current.CapturedAtUtc, goal));
        }
    }

    private static void AddPlayerStatusEvents(
        NormalizedRoomSnapshot previous,
        NormalizedRoomSnapshot current,
        ImmutableArray<NormalizedTrackingEvent>.Builder events)
    {
        if (!CanCompare(previous, current, SnapshotSections.PlayerStatuses)) return;

        var known = previous.PlayerStates.ToDictionary(state => state.Slot);
        foreach (var state in current.PlayerStates)
        {
            if (known.TryGetValue(state.Slot, out var oldState) && oldState.Status != state.Status)
            {
                events.Add(new PlayerStatusChangedEvent(
                    current.GuildId,
                    current.ChannelId,
                    current.CapturedAtUtc,
                    state.Slot,
                    oldState.Status,
                    state.Status));
            }
        }
    }

    private static void AddCheckEvents(
        NormalizedRoomSnapshot previous,
        NormalizedRoomSnapshot current,
        ImmutableArray<NormalizedTrackingEvent>.Builder events)
    {
        if (!CanCompare(previous, current, SnapshotSections.Checks)) return;

        var known = previous.Checks.Select(check => $"{check.Slot}:{check.LocationId}").ToHashSet(StringComparer.Ordinal);
        foreach (var check in current.Checks.Where(check => !known.Contains($"{check.Slot}:{check.LocationId}")))
            events.Add(new CheckCompletedEvent(current.GuildId, current.ChannelId, current.CapturedAtUtc, check));
    }

    private static void AddRoomActivityEvent(
        NormalizedRoomSnapshot previous,
        NormalizedRoomSnapshot current,
        ImmutableArray<NormalizedTrackingEvent>.Builder events)
    {
        if (!CanCompare(previous, current, SnapshotSections.RoomActivity) ||
            current.LastActivityUtc == null ||
            current.LastActivityUtc == previous.LastActivityUtc)
        {
            return;
        }

        events.Add(new RoomActivityChangedEvent(
            current.GuildId,
            current.ChannelId,
            current.CapturedAtUtc,
            previous.LastActivityUtc,
            current.LastActivityUtc.Value));
    }

    private static void AddTrackingEvents(
        NormalizedRoomSnapshot previous,
        NormalizedRoomSnapshot current,
        ImmutableArray<NormalizedTrackingEvent>.Builder events)
    {
        if (!CanCompare(previous, current, SnapshotSections.Tracking)) return;

        if (current.TrackingState == TrackingObservationState.Error &&
            (previous.TrackingState != TrackingObservationState.Error ||
             !string.Equals(previous.TrackingErrorCode, current.TrackingErrorCode, StringComparison.Ordinal)))
        {
            events.Add(new TrackingErrorEvent(
                current.GuildId,
                current.ChannelId,
                current.CapturedAtUtc,
                current.TrackingErrorCode ?? "UNKNOWN"));
        }
        else if (previous.TrackingState == TrackingObservationState.Error &&
                 current.TrackingState == TrackingObservationState.Healthy)
        {
            events.Add(new TrackingRecoveredEvent(
                current.GuildId,
                current.ChannelId,
                current.CapturedAtUtc,
                previous.TrackingErrorCode));
        }
    }
}
