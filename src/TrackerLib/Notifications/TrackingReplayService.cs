using System.Globalization;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;

namespace ArchipelagoSphereTracker.Tracking.Notifications;

public static class TrackingReplayService
{
    internal const string Schema = @"
CREATE TABLE IF NOT EXISTS TrackingReplayRequests (
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 GuildId TEXT NOT NULL, ChannelId TEXT NOT NULL, ReceiverSlot INTEGER NOT NULL,
 RequestedAtUtc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 Status TEXT NOT NULL DEFAULT 'Pending' CHECK(Status IN ('Pending','Queued','Empty','Rejected')),
 NextAttemptAtUtc TEXT NOT NULL DEFAULT '2000-01-01T00:00:00.0000000+00:00',
 EventId INTEGER, ItemCount INTEGER NOT NULL DEFAULT 0, ReasonCode TEXT,
 FOREIGN KEY(GuildId,ChannelId) REFERENCES TrackedRooms(GuildId,ChannelId) ON DELETE CASCADE,
 FOREIGN KEY(EventId) REFERENCES TrackingEvents(Id) ON DELETE SET NULL);
CREATE INDEX IF NOT EXISTS IX_TrackingReplay_Pending ON TrackingReplayRequests(Status,NextAttemptAtUtc,Id);
";

    internal static Task InitializeAsync(CancellationToken ct = default) => Db.WriteAsync(async connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = Schema;
        return await command.ExecuteNonQueryAsync(ct);
    }, ct);

    // Requests may also be inserted by an operator using INSERT ... SELECT from
    // TrackingSlots. They never delete or fabricate item-received history.
    public static Task<bool> PrepareNextAsync(CancellationToken ct = default) => Db.WriteAsync(async connection =>
    {
        var now = DateTimeOffset.UtcNow;
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT Id,GuildId,ChannelId,ReceiverSlot FROM TrackingReplayRequests
            WHERE Status='Pending' AND NextAttemptAtUtc <= @Now ORDER BY Id LIMIT 1;";
        command.Parameters.AddWithValue("@Now", TrackingV2Store.Format(now));
        long id;
        string guild;
        string channel;
        int slot;
        using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return false;
            id = Convert.ToInt64(reader[0], CultureInfo.InvariantCulture);
            guild = reader.GetString(1);
            channel = reader.GetString(2);
            slot = Convert.ToInt32(reader[3], CultureInfo.InvariantCulture);
        }
        command.Parameters.Clear();
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@G", guild);
        command.Parameters.AddWithValue("@C", channel);
        command.Parameters.AddWithValue("@Slot", slot);
        command.CommandText = @"SELECT EXISTS(SELECT 1 FROM ChannelsAndUrlsTable WHERE GuildId=@G AND ChannelId=@C)
            AND EXISTS(SELECT 1 FROM TrackingSlots WHERE GuildId=@G AND ChannelId=@C AND Slot=@Slot);";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) == 0)
        {
            command.CommandText = "UPDATE TrackingReplayRequests SET Status='Rejected',ReasonCode='ROOM_OR_SLOT_MISSING' WHERE Id=@Id;";
            await command.ExecuteNonQueryAsync(ct);
            return true;
        }
        command.CommandText = @"SELECT snapshot.Id,snapshot.PayloadJson FROM RoomSnapshots snapshot
            JOIN TrackedRooms room ON room.GuildId=snapshot.GuildId AND room.ChannelId=snapshot.ChannelId
            WHERE snapshot.GuildId=@G AND snapshot.ChannelId=@C AND room.IsBaselineInitialized=1 AND room.NotificationsV2Initialized=1
            ORDER BY snapshot.Id DESC LIMIT 1;";
        long snapshotId = 0;
        NormalizedRoomSnapshot? snapshot = null;
        using (var reader = await command.ExecuteReaderAsync(ct))
            if (await reader.ReadAsync(ct))
            {
                snapshotId = Convert.ToInt64(reader[0], CultureInfo.InvariantCulture);
                snapshot = TrackingSnapshotJson.Deserialize(reader.GetString(1));
            }
        if (snapshot == null || !snapshot.IsComplete(SnapshotSections.Items) || !snapshot.Slots.Any(value => value.Slot == slot))
        {
            command.CommandText = @"UPDATE TrackingReplayRequests SET NextAttemptAtUtc=@Retry,
                ReasonCode='WAITING_FOR_ITEM_SNAPSHOT' WHERE Id=@Id;";
            command.Parameters.AddWithValue("@Retry", TrackingV2Store.Format(now.AddSeconds(10)));
            await command.ExecuteNonQueryAsync(ct);
            return true;
        }
        var count = snapshot.Items.Count(item => item.ReceiverSlot == slot && item.FinderSlot != slot &&
            item.ItemId >= 0 && item.LocationId >= 0);
        if (count == 0)
        {
            command.CommandText = "UPDATE TrackingReplayRequests SET Status='Empty',ReasonCode='NO_REPLAYABLE_ITEMS' WHERE Id=@Id;";
            await command.ExecuteNonQueryAsync(ct);
            return true;
        }
        var replay = new ItemsReplayRequestedEvent(guild, channel, now, id, slot);
        command.CommandText = @"INSERT INTO TrackingEvents
            (EventKey,GuildId,ChannelId,EventType,OccurredAtUtc,PayloadJson,SnapshotId,CreatedAtUtc)
            VALUES(@Key,@G,@C,@Type,@Now,@Payload,@Snapshot,@Now);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("@Key", replay.EventKey);
        command.Parameters.AddWithValue("@Type", replay.EventType);
        command.Parameters.AddWithValue("@Now", TrackingV2Store.Format(now));
        command.Parameters.AddWithValue("@Payload", TrackingSnapshotJson.SerializeEvent(replay));
        command.Parameters.AddWithValue("@Snapshot", snapshotId);
        var eventId = Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        command.Parameters.AddWithValue("@Event", eventId);
        command.Parameters.AddWithValue("@Destination", TrackingDestination.NotificationDestinationType);
        command.Parameters.AddWithValue("@Count", count);
        command.CommandText = @"INSERT INTO EventDeliveries(EventId,DestinationType,DestinationId,Status,AttemptCount,NextAttemptAtUtc)
            VALUES(@Event,@Destination,@C,'Pending',0,@Now);
            UPDATE TrackingReplayRequests SET Status='Queued',EventId=@Event,ItemCount=@Count,ReasonCode=NULL WHERE Id=@Id;";
        await command.ExecuteNonQueryAsync(ct);
        return true;
    }, ct);
}
