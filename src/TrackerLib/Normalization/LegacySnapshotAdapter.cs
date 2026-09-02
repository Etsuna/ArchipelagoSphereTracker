using System.Text.Json;
using ArchipelagoSphereTracker.src.TrackerLib.Services;

namespace ArchipelagoSphereTracker.Tracking.V2;

public static class LegacySnapshotAdapter
{
    public static NormalizedRoomSnapshot FromWebHostResponse(
        ProcessingContext context,
        string runtimeJson,
        IReadOnlyDictionary<int, int> totalsBySlot,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset? roomLastActivityUtc = null,
        bool roomActivityKnown = false,
        IReadOnlyDictionary<int, NormalizedPlayerStatus>? playerStatuses = null,
        IReadOnlyDictionary<int, string>? playerNames = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(runtimeJson);
        ArgumentNullException.ThrowIfNull(totalsBySlot);

        using var document = JsonDocument.Parse(runtimeJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("Tracker payload root must be an object.");

        var root = document.RootElement;
        var completeSections = SnapshotSections.Slots | SnapshotSections.Tracking;
        if (HasArray(root, "player_items_received")) completeSections |= SnapshotSections.Items;
        if (HasArray(root, "hints")) completeSections |= SnapshotSections.Hints;
        if (HasArray(root, "player_checks_done")) completeSections |= SnapshotSections.Checks;
        if (roomActivityKnown) completeSections |= SnapshotSections.RoomActivity;

        var rawItems = TrackerStreamParser.ParseItems(context, runtimeJson);
        var rawHints = TrackerStreamParser.ParseHints(context, runtimeJson);
        var rawStatuses = TrackerStreamParser.ParseGameStatus(context, runtimeJson, totalsBySlot);
        var checkedLocations = TrackerStreamParser.ParseCheckedLocations(runtimeJson);
        var runtimeAliases = TrackerStreamParser.ParsePlayerAliases(runtimeJson);
        var runtimeStatuses = TrackerStreamParser.ParsePlayerStatuses(runtimeJson);

        var effectiveStatuses = playerStatuses ?? runtimeStatuses.ToDictionary(
            entry => entry.Key,
            entry => MapClientStatus(entry.Value));
        var statusesComplete = (playerStatuses != null || HasArray(root, "player_status")) &&
                               Enumerable.Range(1, context.SlotIndex.Count).All(effectiveStatuses.ContainsKey);
        if (statusesComplete)
            completeSections |= SnapshotSections.PlayerStatuses | SnapshotSections.Goals;

        var slots = context.SlotIndex.Select((entry, index) =>
        {
            var slot = index + 1;
            var playerName = playerNames?.GetValueOrDefault(slot);
            if (string.IsNullOrWhiteSpace(playerName)) playerName = entry.Alias;
            var alias = runtimeAliases.GetValueOrDefault(slot);
            if (string.IsNullOrWhiteSpace(alias)) alias = playerName;
            return new NormalizedSlot(slot, playerName, alias, entry.Game);
        });

        var items = rawItems.Select(item => new NormalizedItemTransfer(
            item.FinderSlot,
            item.ReceiverSlot,
            item.ItemId,
            item.LocationId,
            int.TryParse(item.Flag, out var flags) ? flags : 0,
            item.Finder,
            item.Receiver,
            item.Item,
            item.Location,
            context.SlotGame(item.FinderSlot),
            context.SlotGame(item.ReceiverSlot)));

        var hints = rawHints.Select(hint => new NormalizedHint(
            hint.FinderSlot,
            hint.ReceiverSlot,
            hint.ItemId,
            hint.LocationId,
            bool.TryParse(hint.Flag, out var found) && found,
            string.IsNullOrWhiteSpace(hint.Entrance) ? "Vanilla" : hint.Entrance,
            hint.Finder,
            hint.Receiver,
            hint.Item,
            hint.Location,
            context.SlotGame(hint.FinderSlot),
            context.SlotGame(hint.ReceiverSlot),
            hint.ItemFlags,
            hint.Status));

        var checks = checkedLocations.SelectMany(entry =>
            entry.Value.Select(locationId => new NormalizedCheck(entry.Key, locationId)));

        var checksCountBySlot = checkedLocations.ToDictionary(entry => entry.Key, entry => entry.Value.Count);
        var goals = Enumerable.Range(1, context.SlotIndex.Count).Select(slot => new NormalizedGoal(
            slot,
            "completion",
            effectiveStatuses.GetValueOrDefault(slot) == NormalizedPlayerStatus.GoalReached));

        var rawStatusBySlot = rawStatuses.ToDictionary(status => status.Slot);
        var states = Enumerable.Range(1, context.SlotIndex.Count).Select(slot =>
        {
            rawStatusBySlot.TryGetValue(slot, out var rawStatus);
            return new NormalizedPlayerState(
                slot,
                effectiveStatuses.GetValueOrDefault(slot),
                checksCountBySlot.GetValueOrDefault(slot),
                totalsBySlot.TryGetValue(slot, out var total) ? total : null,
                string.IsNullOrWhiteSpace(rawStatus?.LastActivity) ? null : rawStatus.LastActivity);
        });

        return NormalizedRoomSnapshot.Create(
            context.GuildId,
            context.ChannelId,
            slots,
            items,
            hints,
            checks,
            goals,
            states,
            roomLastActivityUtc,
            capturedAtUtc,
            capturedAtUtc,
            TrackingObservationState.Healthy,
            null,
            completeSections);
    }

    public static NormalizedRoomSnapshot FromTrackingFailure(
        NormalizedRoomSnapshot previous,
        DateTimeOffset capturedAtUtc,
        string errorCode)
    {
        ArgumentNullException.ThrowIfNull(previous);

        return NormalizedRoomSnapshot.Create(
            previous.GuildId,
            previous.ChannelId,
            previous.Slots,
            previous.Items,
            previous.Hints,
            previous.Checks,
            previous.Goals,
            previous.PlayerStates,
            previous.LastActivityUtc,
            capturedAtUtc,
            previous.LastSuccessfulSyncUtc,
            TrackingObservationState.Error,
            errorCode,
            SnapshotSections.Tracking);
    }

    private static bool HasArray(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array;

    private static NormalizedPlayerStatus MapClientStatus(int status)
    {
        return status switch
        {
            5 => NormalizedPlayerStatus.Connected,
            10 => NormalizedPlayerStatus.Ready,
            20 => NormalizedPlayerStatus.Playing,
            30 => NormalizedPlayerStatus.GoalReached,
            _ => NormalizedPlayerStatus.Unknown
        };
    }
}
