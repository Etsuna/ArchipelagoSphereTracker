using System.Data.SQLite;
using System.Globalization;
using ArchipelagoSphereTracker.Tracking.V2;

namespace ArchipelagoSphereTracker.Tracking.Persistence;

// Query projections of the current V2 snapshot. They are committed with the
// snapshot and ledger, so commands never observe a half-applied poll.
internal static class TrackingReadModel
{
    internal const string Schema = @"
CREATE TABLE IF NOT EXISTS TrackingSlots (
 GuildId TEXT NOT NULL, ChannelId TEXT NOT NULL, Slot INTEGER NOT NULL,
 Alias TEXT NOT NULL, DisplayAlias TEXT NOT NULL DEFAULT '', Game TEXT NOT NULL DEFAULT '',
 PRIMARY KEY(GuildId, ChannelId, Slot),
 FOREIGN KEY(GuildId, ChannelId) REFERENCES TrackedRooms(GuildId, ChannelId) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS TrackingItems (
 GuildId TEXT NOT NULL, ChannelId TEXT NOT NULL, Identity TEXT NOT NULL,
 FinderSlot INTEGER, ReceiverSlot INTEGER, ItemId INTEGER, LocationId INTEGER,
 Finder TEXT, Receiver TEXT, Item TEXT, Location TEXT, Game TEXT, Flag TEXT,
 PRIMARY KEY(GuildId, ChannelId, Identity),
 FOREIGN KEY(GuildId, ChannelId) REFERENCES TrackedRooms(GuildId, ChannelId) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS TrackingHints (
 GuildId TEXT NOT NULL, ChannelId TEXT NOT NULL, Identity TEXT NOT NULL,
 FinderSlot INTEGER, ReceiverSlot INTEGER, ItemId INTEGER, LocationId INTEGER,
 Finder TEXT, Receiver TEXT, Item TEXT, Location TEXT, Game TEXT, Entrance TEXT, Flag TEXT,
 ItemFlags INTEGER NOT NULL DEFAULT 0, Status INTEGER NOT NULL DEFAULT 0,
 PRIMARY KEY(GuildId, ChannelId, Identity),
 FOREIGN KEY(GuildId, ChannelId) REFERENCES TrackedRooms(GuildId, ChannelId) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS TrackingPlayerStates (
 GuildId TEXT NOT NULL, ChannelId TEXT NOT NULL, Slot INTEGER NOT NULL,
 Name TEXT NOT NULL, Game TEXT NOT NULL DEFAULT '', Checks TEXT NOT NULL DEFAULT '0', Total TEXT,
 LastActivity TEXT, Status TEXT NOT NULL DEFAULT 'Unknown', GoalCompleted INTEGER NOT NULL DEFAULT 0,
 PRIMARY KEY(GuildId, ChannelId, Slot),
 FOREIGN KEY(GuildId, ChannelId) REFERENCES TrackedRooms(GuildId, ChannelId) ON DELETE CASCADE);
CREATE INDEX IF NOT EXISTS IX_TrackingItems_Receiver ON TrackingItems(GuildId,ChannelId,Receiver,Item);
CREATE INDEX IF NOT EXISTS IX_TrackingHints_Receiver ON TrackingHints(GuildId,ChannelId,Receiver);
CREATE INDEX IF NOT EXISTS IX_TrackingHints_Finder ON TrackingHints(GuildId,ChannelId,Finder);
CREATE TABLE IF NOT EXISTS TrackingReadModelMigrations (Version INTEGER PRIMARY KEY);
";

    internal static Task MigrateAsync(CancellationToken ct = default)
        => Db.WriteAsync(connection => MigrateAsync(connection, ct), ct);

    internal static async Task<int> MigrateAsync(SQLiteConnection connection, CancellationToken ct, bool refreshLegacy = false)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Schema;
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = "SELECT COUNT(*) FROM TrackingReadModelMigrations WHERE Version=16;";
        if (!refreshLegacy && Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 0) return 0;
        var hasLegacyTables = false;
        command.Parameters.Add("@Table", System.Data.DbType.String);
        foreach (var name in LegacyTrackingSchema.TableNames)
        {
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@Table;";
            command.Parameters["@Table"].Value = name;
            hasLegacyTables |= Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 0;
        }
        if (hasLegacyTables)
        {
            command.CommandText = LegacyTrackingSchema.Tables;
            await command.ExecuteNonQueryAsync(ct);
            // Old display-only rows have no trustworthy raw item/location IDs. Keep
            // them as provisional reads until the first complete native observation.
            command.CommandText = @"
    INSERT OR IGNORE INTO TrackedRooms(GuildId,ChannelId,CreatedAtUtc,UpdatedAtUtc)
     SELECT GuildId,ChannelId,@Now,@Now FROM (
     SELECT GuildId,ChannelId FROM ChannelsAndUrlsTable UNION SELECT GuildId,ChannelId FROM AliasChoicesTable
     UNION SELECT GuildId,ChannelId FROM DisplayedItemTable UNION SELECT GuildId,ChannelId FROM HintStatusTable
     UNION SELECT GuildId,ChannelId FROM GameStatusTable);
    CREATE TEMP TABLE LegacyTrackingImportRooms AS
     SELECT room.GuildId,room.ChannelId FROM TrackedRooms room JOIN (
     SELECT GuildId,ChannelId FROM AliasChoicesTable UNION SELECT GuildId,ChannelId FROM DisplayedItemTable
     UNION SELECT GuildId,ChannelId FROM HintStatusTable UNION SELECT GuildId,ChannelId FROM GameStatusTable
     ) legacy ON legacy.GuildId=room.GuildId AND legacy.ChannelId=room.ChannelId
     WHERE room.NotificationsV2Initialized=0;
    DELETE FROM TrackingSlots WHERE (GuildId,ChannelId) IN (SELECT GuildId,ChannelId FROM LegacyTrackingImportRooms);
    DELETE FROM TrackingItems WHERE (GuildId,ChannelId) IN (SELECT GuildId,ChannelId FROM LegacyTrackingImportRooms);
    DELETE FROM TrackingHints WHERE (GuildId,ChannelId) IN (SELECT GuildId,ChannelId FROM LegacyTrackingImportRooms);
    DELETE FROM TrackingPlayerStates WHERE (GuildId,ChannelId) IN (SELECT GuildId,ChannelId FROM LegacyTrackingImportRooms);
    INSERT OR IGNORE INTO TrackingSlots(GuildId,ChannelId,Slot,Alias,DisplayAlias,Game)
     SELECT GuildId,ChannelId,Slot,Alias,Alias,COALESCE(Game,'') FROM AliasChoicesTable
     WHERE Id IN (SELECT MAX(Id) FROM AliasChoicesTable GROUP BY GuildId,ChannelId,Slot);
    INSERT OR IGNORE INTO TrackingItems(GuildId,ChannelId,Identity,FinderSlot,ReceiverSlot,Finder,Receiver,Item,Location,Game,Flag)
     SELECT d.GuildId,d.ChannelId,'legacy:'||d.Id,f.Slot,r.Slot,d.Finder,d.Receiver,d.Item,d.Location,d.Game,d.Flag
     FROM DisplayedItemTable d
     LEFT JOIN TrackingSlots f ON f.GuildId=d.GuildId AND f.ChannelId=d.ChannelId AND f.Alias=d.Finder
     LEFT JOIN TrackingSlots r ON r.GuildId=d.GuildId AND r.ChannelId=d.ChannelId AND r.Alias=d.Receiver;
    INSERT OR IGNORE INTO TrackingHints(GuildId,ChannelId,Identity,FinderSlot,ReceiverSlot,Finder,Receiver,Item,Location,Game,Entrance,Flag)
     SELECT d.GuildId,d.ChannelId,'legacy:'||d.Id,f.Slot,r.Slot,d.Finder,d.Receiver,d.Item,d.Location,d.Game,d.Entrance,d.Flag
     FROM HintStatusTable d
     LEFT JOIN TrackingSlots f ON f.GuildId=d.GuildId AND f.ChannelId=d.ChannelId AND f.Alias=d.Finder
     LEFT JOIN TrackingSlots r ON r.GuildId=d.GuildId AND r.ChannelId=d.ChannelId AND r.Alias=d.Receiver;
    INSERT OR IGNORE INTO TrackingPlayerStates(GuildId,ChannelId,Slot,Name,Game,Checks,Total,LastActivity)
     SELECT d.GuildId,d.ChannelId,COALESCE(s.Slot,-d.Id),d.Name,COALESCE(d.Game,''),COALESCE(d.Checks,'0'),d.Total,d.LastActivity
     FROM GameStatusTable d LEFT JOIN TrackingSlots s
     ON s.GuildId=d.GuildId AND s.ChannelId=d.ChannelId AND s.Alias=d.Name;
    DROP TABLE LegacyTrackingImportRooms;";
            command.Parameters.AddWithValue("@Now", TrackingV2Store.Format(DateTimeOffset.UtcNow));
            await command.ExecuteNonQueryAsync(ct);
        }
        // Existing active V2 rooms already have an authoritative snapshot.
        var snapshotIds = new List<long>();
        command.CommandText = @"SELECT snapshot.Id FROM RoomSnapshots snapshot
            JOIN TrackedRooms room ON room.GuildId=snapshot.GuildId AND room.ChannelId=snapshot.ChannelId
            WHERE room.NotificationsV2Initialized=1 AND snapshot.Id=(SELECT MAX(latest.Id) FROM RoomSnapshots latest
                WHERE latest.GuildId=room.GuildId AND latest.ChannelId=room.ChannelId);";
        using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) snapshotIds.Add(reader.GetInt64(0));
        command.Parameters.Add("@Snapshot", System.Data.DbType.Int64);
        foreach (var snapshotId in snapshotIds)
        {
            command.CommandText = "SELECT PayloadJson FROM RoomSnapshots WHERE Id=@Snapshot;";
            command.Parameters["@Snapshot"].Value = snapshotId;
            var payload = (string)(await command.ExecuteScalarAsync(ct))!;
            await ApplyAsync(connection, TrackingSnapshotJson.Deserialize(payload), ct);
        }
        command.CommandText = "INSERT OR IGNORE INTO TrackingReadModelMigrations(Version) VALUES(16);";
        await command.ExecuteNonQueryAsync(ct);
        return 0;
    }

    internal static async Task EnsureRoomAsync(SQLiteConnection connection, string guild, string channel, CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"INSERT OR IGNORE INTO TrackedRooms
            (GuildId, ChannelId, CreatedAtUtc, UpdatedAtUtc) VALUES (@G,@C,@Now,@Now);";
        command.Parameters.AddWithValue("@G", guild);
        command.Parameters.AddWithValue("@C", channel);
        command.Parameters.AddWithValue("@Now", TrackingV2Store.Format(DateTimeOffset.UtcNow));
        await command.ExecuteNonQueryAsync(ct);
    }

    internal static async Task ApplyAsync(SQLiteConnection connection, NormalizedRoomSnapshot snapshot, CancellationToken ct,
        NormalizedRoomSnapshot? previous = null)
    {
        var slotsChanged = previous == null || !previous.IsComplete(SnapshotSections.Slots) || !previous.Slots.SequenceEqual(snapshot.Slots);
        if (snapshot.IsComplete(SnapshotSections.Slots) && slotsChanged)
        {
            await ClearAsync(connection, "TrackingSlots", snapshot, ct);
            foreach (var slot in snapshot.Slots)
                await InsertAsync(connection, "TrackingSlots", snapshot, ct,
                    ("Slot", slot.Slot), ("Alias", slot.PlayerName), ("DisplayAlias", slot.Alias), ("Game", slot.Game));
        }
        if (snapshot.IsComplete(SnapshotSections.Items) && (slotsChanged || previous == null ||
            !previous.IsComplete(SnapshotSections.Items) || !previous.Items.SequenceEqual(snapshot.Items)))
        {
            await ApplyItemsAsync(connection, snapshot, previous, ct);
        }
        if (snapshot.IsComplete(SnapshotSections.Hints) && (slotsChanged || previous == null ||
            !previous.IsComplete(SnapshotSections.Hints) || !previous.Hints.SequenceEqual(snapshot.Hints)))
        {
            await ClearAsync(connection, "TrackingHints", snapshot, ct);
            foreach (var hint in snapshot.Hints)
                await InsertAsync(connection, "TrackingHints", snapshot, ct,
                    ("Identity", NormalizedRoomSnapshot.HintIdentity(hint)),
                    ("FinderSlot", hint.FinderSlot), ("ReceiverSlot", hint.ReceiverSlot),
                    ("ItemId", hint.ItemId), ("LocationId", hint.LocationId),
                    ("Finder", CanonicalName(snapshot, hint.FinderSlot, hint.FinderDisplayName)),
                    ("Receiver", CanonicalName(snapshot, hint.ReceiverSlot, hint.ReceiverDisplayName)),
                    ("Item", hint.ItemDisplayName), ("Location", hint.LocationDisplayName),
                    ("Game", hint.FinderGame), ("Entrance", hint.Entrance), ("Flag", hint.Found.ToString()),
                    ("ItemFlags", hint.ItemFlags), ("Status", hint.Status));
        }
        // Checks and client status are independently optional in WebHost responses.
        if ((snapshot.IsComplete(SnapshotSections.Checks) || snapshot.IsComplete(SnapshotSections.PlayerStatuses)) &&
            (slotsChanged || previous == null || !previous.PlayerStates.SequenceEqual(snapshot.PlayerStates) ||
             !previous.Goals.SequenceEqual(snapshot.Goals) ||
             (previous.CompleteSections & (SnapshotSections.Checks | SnapshotSections.PlayerStatuses | SnapshotSections.Goals)) !=
             (snapshot.CompleteSections & (SnapshotSections.Checks | SnapshotSections.PlayerStatuses | SnapshotSections.Goals))))
        {
            var old = new List<StoredPlayerState>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT Slot,Name,Checks,Total,LastActivity,Status,GoalCompleted
                    FROM TrackingPlayerStates WHERE GuildId=@G AND ChannelId=@C;";
                command.Parameters.AddWithValue("@G", snapshot.GuildId);
                command.Parameters.AddWithValue("@C", snapshot.ChannelId);
                using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    old.Add(new(Convert.ToInt32(reader[0]), reader[1].ToString() ?? "", reader[2].ToString() ?? "0",
                        reader[3] is DBNull ? null : reader[3].ToString(), reader[4] is DBNull ? null : reader[4].ToString(),
                        reader[5].ToString() ?? "Unknown", Convert.ToInt32(reader[6])));
            }
            await ClearAsync(connection, "TrackingPlayerStates", snapshot, ct);
            foreach (var player in snapshot.PlayerStates)
            {
                var slot = snapshot.Slots.FirstOrDefault(value => value.Slot == player.Slot);
                var name = slot?.PlayerName ?? $"Player{player.Slot}";
                var stored = old.FirstOrDefault(value => value.Slot == player.Slot)
                    ?? old.FirstOrDefault(value => value.Name == name);
                await InsertAsync(connection, "TrackingPlayerStates", snapshot, ct,
                    ("Slot", player.Slot), ("Name", name), ("Game", slot?.Game ?? ""),
                    ("Checks", snapshot.IsComplete(SnapshotSections.Checks)
                        ? player.CompletedChecks.ToString(CultureInfo.InvariantCulture) : stored?.Checks ?? "0"),
                    ("Total", player.TotalChecks?.ToString(CultureInfo.InvariantCulture) ?? stored?.Total),
                    ("LastActivity", player.ActivityValue ?? stored?.Activity),
                    ("Status", snapshot.IsComplete(SnapshotSections.PlayerStatuses) ? player.Status.ToString() : stored?.Status ?? "Unknown"),
                    ("GoalCompleted", snapshot.IsComplete(SnapshotSections.Goals)
                        ? (snapshot.Goals.Any(goal => goal.Slot == player.Slot && goal.Completed) ? 1 : 0) : stored?.Goal ?? 0));
            }
        }
    }

    private static async Task ApplyItemsAsync(SQLiteConnection connection, NormalizedRoomSnapshot snapshot,
        NormalizedRoomSnapshot? previous, CancellationToken ct)
    {
        var names = snapshot.Slots.ToDictionary(slot => slot.Slot, slot => slot.PlayerName);
        using var upsert = connection.CreateCommand();
        upsert.CommandText = @"
            INSERT INTO TrackingItems
                (GuildId,ChannelId,Identity,FinderSlot,ReceiverSlot,ItemId,LocationId,Finder,Receiver,Item,Location,Game,Flag)
            VALUES (@G,@C,@Identity,@FinderSlot,@ReceiverSlot,@ItemId,@LocationId,@Finder,@Receiver,@Item,@Location,@Game,@Flag)
            ON CONFLICT(GuildId,ChannelId,Identity) DO UPDATE SET
                FinderSlot=excluded.FinderSlot, ReceiverSlot=excluded.ReceiverSlot,
                ItemId=excluded.ItemId, LocationId=excluded.LocationId,
                Finder=excluded.Finder, Receiver=excluded.Receiver, Item=excluded.Item,
                Location=excluded.Location, Game=excluded.Game, Flag=excluded.Flag;";
        upsert.Parameters.AddWithValue("@G", snapshot.GuildId);
        upsert.Parameters.AddWithValue("@C", snapshot.ChannelId);
        foreach (var name in new[] { "@Identity", "@Finder", "@Receiver", "@Item", "@Location", "@Game", "@Flag" })
            upsert.Parameters.Add(name, System.Data.DbType.String);
        foreach (var name in new[] { "@FinderSlot", "@ReceiverSlot", "@ItemId", "@LocationId" })
            upsert.Parameters.Add(name, System.Data.DbType.Int64);

        async Task UpsertAsync(NormalizedItemTransfer item, string identity)
        {
            upsert.Parameters["@Identity"].Value = identity;
            upsert.Parameters["@FinderSlot"].Value = item.FinderSlot;
            upsert.Parameters["@ReceiverSlot"].Value = item.ReceiverSlot;
            upsert.Parameters["@ItemId"].Value = item.ItemId;
            upsert.Parameters["@LocationId"].Value = item.LocationId;
            upsert.Parameters["@Finder"].Value = (object?)(names.GetValueOrDefault(item.FinderSlot) ?? item.FinderDisplayName) ?? DBNull.Value;
            upsert.Parameters["@Receiver"].Value = (object?)(names.GetValueOrDefault(item.ReceiverSlot) ?? item.ReceiverDisplayName) ?? DBNull.Value;
            upsert.Parameters["@Item"].Value = (object?)item.ItemDisplayName ?? DBNull.Value;
            upsert.Parameters["@Location"].Value = (object?)item.LocationDisplayName ?? DBNull.Value;
            upsert.Parameters["@Game"].Value = (object?)item.FinderGame ?? DBNull.Value;
            upsert.Parameters["@Flag"].Value = item.Flags.ToString(CultureInfo.InvariantCulture);
            await upsert.ExecuteNonQueryAsync(ct);
        }

        // Without an authoritative previous inventory (first baseline, cutover or
        // migration), replace provisional rows, including legacy identities.
        if (previous == null || !previous.IsComplete(SnapshotSections.Items))
        {
            await ClearAsync(connection, "TrackingItems", snapshot, ct);
            foreach (var item in snapshot.Items)
                await UpsertAsync(item, NormalizedRoomSnapshot.ItemIdentity(item));
            return;
        }

        using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM TrackingItems WHERE GuildId=@G AND ChannelId=@C AND Identity=@Identity;";
        delete.Parameters.AddWithValue("@G", snapshot.GuildId);
        delete.Parameters.AddWithValue("@C", snapshot.ChannelId);
        delete.Parameters.Add("@Identity", System.Data.DbType.String);
        var oldNames = previous.Slots.ToDictionary(slot => slot.Slot, slot => slot.PlayerName);

        bool SameProjection(NormalizedItemTransfer old, NormalizedItemTransfer item)
            // Equal identities already guarantee the four raw IDs/slots match.
            // Only compare stored fields, including renamed canonical player names.
            => old.Flags == item.Flags && old.ItemDisplayName == item.ItemDisplayName &&
               old.LocationDisplayName == item.LocationDisplayName && old.FinderGame == item.FinderGame &&
               (oldNames.GetValueOrDefault(item.FinderSlot) ?? old.FinderDisplayName) ==
                   (names.GetValueOrDefault(item.FinderSlot) ?? item.FinderDisplayName) &&
               (oldNames.GetValueOrDefault(item.ReceiverSlot) ?? old.ReceiverDisplayName) ==
                   (names.GetValueOrDefault(item.ReceiverSlot) ?? item.ReceiverDisplayName);

        // Normalized snapshots are unique and ordinally sorted by ItemIdentity.
        // Merge them in one pass without copying the complete inventories into maps.
        var oldIndex = 0;
        var newIndex = 0;
        var oldIdentity = previous.Items.Length > 0 ? NormalizedRoomSnapshot.ItemIdentity(previous.Items[0]) : null;
        var newIdentity = snapshot.Items.Length > 0 ? NormalizedRoomSnapshot.ItemIdentity(snapshot.Items[0]) : null;
        while (oldIdentity != null || newIdentity != null)
        {
            ct.ThrowIfCancellationRequested();
            var comparison = oldIdentity == null ? 1 : newIdentity == null ? -1 : string.CompareOrdinal(oldIdentity, newIdentity);
            if (comparison < 0)
            {
                delete.Parameters["@Identity"].Value = oldIdentity!;
                await delete.ExecuteNonQueryAsync(ct);
            }
            else
            {
                var item = snapshot.Items[newIndex];
                if (comparison > 0 || !SameProjection(previous.Items[oldIndex], item))
                    await UpsertAsync(item, newIdentity!);
            }
            if (comparison <= 0)
                oldIdentity = ++oldIndex < previous.Items.Length ? NormalizedRoomSnapshot.ItemIdentity(previous.Items[oldIndex]) : null;
            if (comparison >= 0)
                newIdentity = ++newIndex < snapshot.Items.Length ? NormalizedRoomSnapshot.ItemIdentity(snapshot.Items[newIndex]) : null;
        }
    }

    private sealed record StoredPlayerState(int Slot, string Name, string Checks, string? Total, string? Activity, string Status, int Goal);

    internal static async Task AppendRecapsAsync(SQLiteConnection connection, ItemReceivedEvent received,
        NormalizedRoomSnapshot snapshot, CancellationToken ct)
    {
        var item = received.Item;
        if (item.FinderSlot == item.ReceiverSlot) return;
        using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO RecapListItemsTable (RecapListTableId, Item)
            SELECT Id, @Item FROM RecapListTable WHERE GuildId=@G AND ChannelId=@C AND Alias=@Alias;";
        command.Parameters.AddWithValue("@G", snapshot.GuildId);
        command.Parameters.AddWithValue("@C", snapshot.ChannelId);
        command.Parameters.AddWithValue("@Alias", CanonicalName(snapshot, item.ReceiverSlot, item.ReceiverDisplayName));
        command.Parameters.AddWithValue("@Item", item.ItemDisplayName);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string CanonicalName(NormalizedRoomSnapshot snapshot, int slot, string fallback)
        => snapshot.Slots.FirstOrDefault(value => value.Slot == slot)?.PlayerName ?? fallback;

    private static async Task ClearAsync(SQLiteConnection connection, string table, NormalizedRoomSnapshot snapshot, CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {table} WHERE GuildId=@G AND ChannelId=@C;";
        command.Parameters.AddWithValue("@G", snapshot.GuildId);
        command.Parameters.AddWithValue("@C", snapshot.ChannelId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertAsync(SQLiteConnection connection, string table, NormalizedRoomSnapshot snapshot,
        CancellationToken ct, params (string Name, object? Value)[] values)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO {table} (GuildId,ChannelId,{string.Join(',', values.Select(value => value.Name))}) " +
            $"VALUES (@G,@C,{string.Join(',', values.Select((_, index) => $"@P{index}"))});";
        command.Parameters.AddWithValue("@G", snapshot.GuildId);
        command.Parameters.AddWithValue("@C", snapshot.ChannelId);
        for (var index = 0; index < values.Length; index++)
            command.Parameters.AddWithValue($"@P{index}", values[index].Value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }
}
