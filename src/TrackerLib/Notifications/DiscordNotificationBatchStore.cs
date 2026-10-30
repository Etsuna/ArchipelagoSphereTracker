using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;

namespace ArchipelagoSphereTracker.Tracking.Notifications;

internal sealed record DiscordNotificationPart(
    string Key, TrackingDiscordNotification Notification, DateTimeOffset? FirstAttemptAtUtc = null, string? Receipt = null);

internal sealed record DiscordNotificationBatch(string Key, IReadOnlyList<DiscordNotificationPart> Parts);

internal static class DiscordNotificationBatchStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<DiscordNotificationBatch> GetOrCreateAsync(
        TrackingDeliveryEnvelope delivery, NormalizedRoomSnapshot snapshot, bool silent, CancellationToken ct,
        RoomNotificationPreferences? preferences = null)
    {
        if (delivery.SnapshotId <= 0) throw new InvalidOperationException("Delivery snapshot is missing.");
        var replay = delivery.EventType == "ItemsReplayRequested"
            ? (ItemsReplayRequestedEvent)TrackingSnapshotJson.DeserializeEvent(delivery.EventType, delivery.EventPayloadJson)
            : null;
        var key = $"{delivery.SnapshotId}:{delivery.DestinationType}:{delivery.DestinationId}:{delivery.EventType}";
        if (replay != null) key += ":" + replay.EventKey;
        var existing = await ReadAsync(key, ct);
        if (existing != null) return existing;

        var fresh = new List<NormalizedTrackingEvent>();
        var attempted = new List<NormalizedTrackingEvent>();
        if (replay != null)
        {
            fresh.AddRange(snapshot.Items.Where(item => item.ReceiverSlot == replay.ReceiverSlot)
                .Select(item => new ItemReceivedEvent(snapshot.GuildId, snapshot.ChannelId, replay.OccurredAtUtc, item)));
        }
        else
        {
            await using var connection = await Db.OpenReadAsync();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT event.PayloadJson, delivery.Id, delivery.AttemptCount
                FROM EventDeliveries delivery JOIN TrackingEvents event ON event.Id = delivery.EventId
                WHERE event.SnapshotId = @Snapshot AND event.EventType = @EventType
                  AND delivery.DestinationType = @Type AND delivery.DestinationId = @Channel
                  AND delivery.Status <> 'Delivered' ORDER BY delivery.Id;";
            command.Parameters.AddWithValue("@Snapshot", delivery.SnapshotId);
            command.Parameters.AddWithValue("@EventType", delivery.EventType);
            command.Parameters.AddWithValue("@Type", delivery.DestinationType);
            command.Parameters.AddWithValue("@Channel", delivery.DestinationId);
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var value = TrackingSnapshotJson.DeserializeEvent(delivery.EventType, reader.GetString(0));
                if (value.GuildId != snapshot.GuildId || value.ChannelId != snapshot.ChannelId)
                    throw new InvalidOperationException("Batch event scope does not match its snapshot.");
                var id = Convert.ToInt64(reader[1]);
                var attempts = Convert.ToInt32(reader[2]);
                // The current claim already increments AttemptCount. Isolate only
                // older, uncertain individual sends during the format upgrade.
                var wasAttempted = id == delivery.DeliveryId ? attempts > 1 : attempts > 0;
                (wasAttempted ? attempted : fresh).Add(value);
            }
        }

        var parts = new List<DiscordNotificationPart>();
        foreach (var value in attempted)
        {
            var notification = await TrackingDataManager.RenderTrackingNotificationAsync(value, snapshot, silent);
            if (notification != null)
                parts.Add(new(value.EventKey, notification, value.OccurredAtUtc));
        }
        var rendered = await TrackingDataManager.RenderTrackingNotificationsAsync(fresh, snapshot, silent, preferences);
        foreach (var value in rendered)
        {
            var partKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{key}:{parts.Count}"))).ToLowerInvariant();
            parts.Add(new(partKey, value.Notification));
        }
        var batch = new DiscordNotificationBatch(key, parts);
        return await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT OR IGNORE INTO DiscordNotificationBatches
                (BatchKey, SnapshotId, ChannelId, PayloadJson) VALUES (@Key, @Snapshot, @Channel, @Payload);
                SELECT PayloadJson FROM DiscordNotificationBatches WHERE BatchKey = @Key;";
            command.Parameters.AddWithValue("@Key", key);
            command.Parameters.AddWithValue("@Snapshot", delivery.SnapshotId);
            command.Parameters.AddWithValue("@Channel", delivery.DestinationId);
            command.Parameters.AddWithValue("@Payload", JsonSerializer.Serialize(batch, JsonOptions));
            return Deserialize((string)(await command.ExecuteScalarAsync(ct))!);
        }, ct);
    }

    public static async Task<DiscordNotificationBatch?> ReadAsync(string key, CancellationToken ct)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT PayloadJson FROM DiscordNotificationBatches WHERE BatchKey = @Key;";
        command.Parameters.AddWithValue("@Key", key);
        return await command.ExecuteScalarAsync(ct) is string payload ? Deserialize(payload) : null;
    }

    public static Task UpdatePartAsync(string batchKey, int index,
        Func<DiscordNotificationPart, DiscordNotificationPart> update, CancellationToken ct)
        => Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT PayloadJson FROM DiscordNotificationBatches WHERE BatchKey = @Key;";
            command.Parameters.AddWithValue("@Key", batchKey);
            if (await command.ExecuteScalarAsync(ct) is not string payload)
                throw new InvalidOperationException("Notification batch was removed.");
            var batch = Deserialize(payload);
            var parts = batch.Parts.ToArray();
            parts[index] = update(parts[index]);
            command.CommandText = "UPDATE DiscordNotificationBatches SET PayloadJson = @Payload WHERE BatchKey = @Key;";
            command.Parameters.AddWithValue("@Payload", JsonSerializer.Serialize(batch with { Parts = parts }, JsonOptions));
            return await command.ExecuteNonQueryAsync(ct);
        }, ct);

    public static async Task<bool> IsReceiptUsedAsync(string channelId, string receipt, CancellationToken ct)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT EXISTS(SELECT 1 FROM EventDeliveries
            WHERE DestinationId = @Channel AND ExternalReceiptId = @Receipt);
            ";
        command.Parameters.AddWithValue("@Channel", channelId);
        command.Parameters.AddWithValue("@Receipt", receipt);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 0) return true;
        command.CommandText = "SELECT PayloadJson FROM DiscordNotificationBatches WHERE ChannelId = @Channel;";
        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            if (Deserialize(reader.GetString(0)).Parts.Any(part => part.Receipt == receipt)) return true;
        return false;
    }

    private static DiscordNotificationBatch Deserialize(string json)
        => JsonSerializer.Deserialize<DiscordNotificationBatch>(json, JsonOptions)
           ?? throw new JsonException("Notification batch is empty.");
}
