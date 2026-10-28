using System.Text.Json;
using ArchipelagoSphereTracker.src.TrackerLib.Services;

namespace ArchipelagoSphereTracker.Tracking.V2;

public static class WebHostSnapshotParser
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

        var items = new List<NormalizedItemTransfer>();
        foreach (var entry in Array(root, "player_items_received"))
        {
            var receiver = (int)Number(entry.GetProperty("player"));
            foreach (var tuple in Array(entry, "items"))
            {
                if (tuple.ValueKind != JsonValueKind.Array || tuple.GetArrayLength() < 4)
                    throw new JsonException("Incomplete item transfer.");
                var itemId = Number(tuple[0]);
                var locationId = Number(tuple[1]);
                var finder = (int)Number(tuple[2]);
                var finderGame = context.SlotGame(finder);
                var receiverGame = context.SlotGame(receiver);
                items.Add(new(finder, receiver, itemId, locationId, (int)Number(tuple[3]),
                    context.SlotAlias(finder), context.SlotAlias(receiver),
                    context.TryGetItemName(receiverGame, itemId, out var itemName) ? itemName : itemId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    context.TryGetLocationName(finderGame, locationId, out var locationName) ? locationName : locationId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    finderGame, receiverGame));
            }
        }
        var hints = new List<NormalizedHint>();
        foreach (var entry in Array(root, "hints"))
        foreach (var tuple in Array(entry, "hints"))
        {
            if (tuple.ValueKind != JsonValueKind.Array || tuple.GetArrayLength() < 6)
                throw new JsonException("Incomplete hint.");
            var receiver = (int)Number(tuple[0]);
            var finder = (int)Number(tuple[1]);
            var locationId = Number(tuple[2]);
            var itemId = Number(tuple[3]);
            var finderGame = context.SlotGame(finder);
            var receiverGame = context.SlotGame(receiver);
            var entrance = tuple[5].ValueKind == JsonValueKind.String ? tuple[5].GetString() : null;
            hints.Add(new(finder, receiver, itemId, locationId, Boolean(tuple[4]),
                string.IsNullOrWhiteSpace(entrance) ? "Vanilla" : entrance,
                context.SlotAlias(finder), context.SlotAlias(receiver),
                context.TryGetItemName(receiverGame, itemId, out var itemName) ? itemName : itemId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                context.TryGetLocationName(finderGame, locationId, out var locationName) ? locationName : locationId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                finderGame, receiverGame,
                tuple.GetArrayLength() > 6 ? (int)Number(tuple[6]) : 0,
                tuple.GetArrayLength() > 7 ? (int)Number(tuple[7]) : 0));
        }

        var checks = checkedLocations.SelectMany(entry =>
            entry.Value.Select(locationId => new NormalizedCheck(entry.Key, locationId)));

        var checksCountBySlot = checkedLocations.ToDictionary(entry => entry.Key, entry => entry.Value.Count);
        var goals = Enumerable.Range(1, context.SlotIndex.Count).Select(slot => new NormalizedGoal(
            slot,
            "completion",
            effectiveStatuses.GetValueOrDefault(slot) == NormalizedPlayerStatus.GoalReached));

        var activity = Array(root, "activity_timers").ToDictionary(
            entry => (int)Number(entry.GetProperty("player")),
            entry => entry.TryGetProperty("time", out var time) && time.ValueKind == JsonValueKind.String ? time.GetString() : null);
        var states = Enumerable.Range(1, context.SlotIndex.Count).Select(slot =>
        {

            return new NormalizedPlayerState(
                slot,
                effectiveStatuses.GetValueOrDefault(slot),
                checksCountBySlot.GetValueOrDefault(slot),
                totalsBySlot.TryGetValue(slot, out var total) ? total : null,
                activity.GetValueOrDefault(slot));
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

    private static IEnumerable<JsonElement> Array(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().ToArray() : [];

    private static long Number(JsonElement value)
        => value.ValueKind == JsonValueKind.Number ? value.GetInt64()
            : value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var number)
                ? number : throw new JsonException("Invalid numeric tracker identity.");

    private static bool Boolean(JsonElement value)
        => value.ValueKind == JsonValueKind.True ||
           (value.ValueKind == JsonValueKind.Number && value.GetInt64() != 0) ||
           (value.ValueKind == JsonValueKind.String && (value.GetString() == "1" || bool.TryParse(value.GetString(), out var found) && found));

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
