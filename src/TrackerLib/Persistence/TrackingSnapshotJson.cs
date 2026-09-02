using System.Text.Json;
using ArchipelagoSphereTracker.Tracking.V2;

namespace ArchipelagoSphereTracker.Tracking.Persistence;

internal static class TrackingSnapshotJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(NormalizedRoomSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return JsonSerializer.Serialize(new StoredSnapshot(
            snapshot.GuildId,
            snapshot.ChannelId,
            snapshot.Slots.ToArray(),
            snapshot.Items.ToArray(),
            snapshot.Hints.ToArray(),
            snapshot.Checks.ToArray(),
            snapshot.Goals.ToArray(),
            snapshot.PlayerStates.ToArray(),
            snapshot.LastActivityUtc,
            snapshot.CapturedAtUtc,
            snapshot.LastSuccessfulSyncUtc,
            snapshot.TrackingState,
            snapshot.TrackingErrorCode,
            snapshot.CompleteSections), Options);
    }

    public static NormalizedRoomSnapshot Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Snapshot JSON is required.", nameof(json));

        var stored = JsonSerializer.Deserialize<StoredSnapshot>(json, Options)
                     ?? throw new JsonException("Snapshot JSON was empty.");

        return NormalizedRoomSnapshot.Create(
            stored.GuildId,
            stored.ChannelId,
            stored.Slots,
            stored.Items,
            stored.Hints,
            stored.Checks,
            stored.Goals,
            stored.PlayerStates,
            stored.LastActivityUtc,
            stored.CapturedAtUtc,
            stored.LastSuccessfulSyncUtc,
            stored.TrackingState,
            stored.TrackingErrorCode,
            stored.CompleteSections);
    }

    public static string SerializeEvent(NormalizedTrackingEvent trackingEvent)
    {
        ArgumentNullException.ThrowIfNull(trackingEvent);
        return JsonSerializer.Serialize(trackingEvent, trackingEvent.GetType(), Options);
    }

    private sealed record StoredSnapshot(
        string GuildId,
        string ChannelId,
        NormalizedSlot[] Slots,
        NormalizedItemTransfer[] Items,
        NormalizedHint[] Hints,
        NormalizedCheck[] Checks,
        NormalizedGoal[] Goals,
        NormalizedPlayerState[] PlayerStates,
        DateTimeOffset? LastActivityUtc,
        DateTimeOffset CapturedAtUtc,
        DateTimeOffset? LastSuccessfulSyncUtc,
        TrackingObservationState TrackingState,
        string? TrackingErrorCode,
        SnapshotSections CompleteSections);
}
