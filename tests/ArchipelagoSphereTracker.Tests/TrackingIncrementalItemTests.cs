using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;
using Xunit;

public sealed class TrackingIncrementalItemTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly NormalizedSlot[] Slots =
        [new(1, "Alice", "Display Alice", "Game A"), new(2, "Bob", "Bob", "Game B"), new(3, "Charlie", "Charlie", "Game C")];

    [Fact]
    public async Task One_new_item_in_a_ten_thousand_item_room_only_inserts_one_row()
    {
        using var database = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(Enumerable.Range(1, 10_000).Select(Item)));
        var unchangedRow = await ScalarAsync("SELECT rowid FROM TrackingItems WHERE ItemId=101;");
        await AuditAsync();

        var result = await store.ApplySnapshotAsync(Snapshot(Enumerable.Range(1, 10_001).Select(Item)));
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM ItemWrites;"));
        Assert.Equal("insert", await ScalarAsync("SELECT Kind FROM ItemWrites;"));
        Assert.Equal(10_001, await TestDatabaseScope.CountRowsAsync("TrackingItems"));
        Assert.Equal(unchangedRow, await ScalarAsync("SELECT rowid FROM TrackingItems WHERE ItemId=101;"));
        Assert.Equal(2, result.EventsCreated); // Received and sent; the baseline stays silent.
    }

    [Fact]
    public async Task Mixed_delta_preserves_unchanged_rows_and_other_rooms_in_ordinal_identity_order()
    {
        using var database = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot([Item(1), Item(2), Item(10), Item(100)]));
        await store.ApplySnapshotAsync(Snapshot([Item(1)], channel: "other"));
        await store.ApplySnapshotAsync(Snapshot([Item(1)], guild: "other"));
        var unchanged = await ScalarAsync("SELECT rowid FROM TrackingItems WHERE GuildId='g' AND ChannelId='c' AND ItemId=101;");
        var changed = await ScalarAsync("SELECT rowid FROM TrackingItems WHERE GuildId='g' AND ChannelId='c' AND ItemId=102;");
        await AuditAsync();

        await store.ApplySnapshotAsync(Snapshot([Item(100), Item(11), Item(2) with { ItemDisplayName = "Updated sword" }, Item(1)]));
        Assert.Equal("delete,insert,update", await ScalarAsync("SELECT group_concat(Kind) FROM (SELECT Kind FROM ItemWrites ORDER BY Kind);"));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM ItemWrites WHERE GuildId<>'g' OR ChannelId<>'c';"));
        Assert.Equal(unchanged, await ScalarAsync("SELECT rowid FROM TrackingItems WHERE GuildId='g' AND ChannelId='c' AND ItemId=101;"));
        Assert.Equal(changed, await ScalarAsync("SELECT rowid FROM TrackingItems WHERE GuildId='g' AND ChannelId='c' AND ItemId=102;"));
        Assert.Equal("Updated sword", await ScalarAsync("SELECT Item FROM TrackingItems WHERE GuildId='g' AND ChannelId='c' AND ItemId=102;"));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM TrackingItems WHERE GuildId='g' AND ChannelId='c' AND ItemId=110;"));
        Assert.Equal(4, await TestDatabaseScope.CountRowsAsync("TrackingItems", "g", "c"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("TrackingItems", "g", "other"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("TrackingItems", "other", "c"));
    }

    [Theory]
    [InlineData("flag", "Flag", "4")]
    [InlineData("item", "Item", "New item")]
    [InlineData("location", "Location", "New location")]
    [InlineData("game", "Game", "New game")]
    public async Task Same_identity_updates_changed_projection_fields_without_recreating_the_row(string change, string column, string expected)
    {
        using var database = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot([Item(1), Item(2)]));
        var row = await ScalarAsync("SELECT rowid FROM TrackingItems WHERE ItemId=101;");
        await AuditAsync();
        var item = change switch
        {
            "flag" => Item(1) with { Flags = 4 },
            "item" => Item(1) with { ItemDisplayName = expected },
            "location" => Item(1) with { LocationDisplayName = expected },
            _ => Item(1) with { FinderGame = expected }
        };
        var result = await store.ApplySnapshotAsync(Snapshot([item, Item(2)]));
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM ItemWrites;"));
        Assert.Equal("update", await ScalarAsync("SELECT Kind FROM ItemWrites;"));
        Assert.Equal(row, await ScalarAsync("SELECT rowid FROM TrackingItems WHERE ItemId=101;"));
        Assert.Equal(expected, await ScalarAsync($"SELECT {column} FROM TrackingItems WHERE ItemId=101;"));
        Assert.Equal(0, result.EventsCreated);
    }

    [Fact]
    public async Task Player_rename_updates_only_items_using_that_canonical_player_name()
    {
        using var database = new TestDatabaseScope();
        var items = new[] { Item(1), Item(2) with { ReceiverSlot = 3 }, Item(3) with { FinderSlot = 3, ReceiverSlot = 3 } };
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(items));
        await AuditAsync();
        await store.ApplySnapshotAsync(Snapshot(items, slots: Slots.Select(slot => slot.Slot == 2 ? slot with { PlayerName = "Renamed Bob" } : slot)));
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM ItemWrites;"));
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM ItemWrites WHERE Kind='update';"));
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM TrackingItems WHERE Finder='Renamed Bob';"));
        Assert.Equal("Charlie", await ScalarAsync("SELECT Finder FROM TrackingItems WHERE ItemId=103;"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Adding_or_removing_a_named_slot_switches_between_canonical_and_fallback_names(bool remove)
    {
        using var database = new TestDatabaseScope();
        var withoutSender = Slots.Where(slot => slot.Slot != 2).ToArray();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot([Item(1)], slots: remove ? Slots : withoutSender));
        await AuditAsync();
        await store.ApplySnapshotAsync(Snapshot([Item(1)], slots: remove ? withoutSender : Slots));
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM ItemWrites;"));
        Assert.Equal("update", await ScalarAsync("SELECT Kind FROM ItemWrites;"));
        Assert.Equal(remove ? "Fallback sender" : "Bob", await ScalarAsync("SELECT Finder FROM TrackingItems;"));
    }

    [Theory]
    [InlineData("display-alias")]
    [InlineData("receiver-game")]
    [InlineData("fallback-names")]
    public async Task Changes_outside_the_stored_item_projection_do_not_write_inventory_rows(string change)
    {
        using var database = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot([Item(1)]));
        await AuditAsync();
        var item = change switch
        {
            "receiver-game" => Item(1) with { ReceiverGame = "Another game" },
            "fallback-names" => Item(1) with { FinderDisplayName = "Fallback Bob", ReceiverDisplayName = "Fallback Alice" },
            _ => Item(1)
        };
        var slots = change == "display-alias" ? Slots.Select(slot => slot with { Alias = "Another display alias" }) : Slots;
        Assert.True((await store.ApplySnapshotAsync(Snapshot([item], slots: slots))).SnapshotChanged);
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM ItemWrites;"));
        Assert.Equal("Bob", await ScalarAsync("SELECT Finder FROM TrackingItems;"));
        Assert.Equal("Alice", await ScalarAsync("SELECT Receiver FROM TrackingItems;"));
    }

    [Fact]
    public async Task Missing_items_section_preserves_inventory_but_a_complete_empty_section_removes_it()
    {
        using var database = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot([Item(1), Item(2)]));
        await store.ApplySnapshotAsync(Snapshot([Item(1)], channel: "other"));
        await AuditAsync();
        Assert.True((await store.ApplySnapshotAsync(Snapshot([], checks: [new(1, 777)],
            sections: SnapshotSections.Checks | SnapshotSections.Tracking))).SnapshotChanged);
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("TrackingItems", "g", "c"));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM ItemWrites;"));
        await store.ApplySnapshotAsync(Snapshot([]));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingItems", "g", "c"));
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM ItemWrites WHERE Kind='delete';"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("TrackingItems", "g", "other"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task First_complete_inventory_or_notification_cutover_replaces_provisional_rows(bool cutover)
    {
        using var database = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(cutover ? [Item(1)] : [],
            sections: cutover ? SnapshotSections.All : SnapshotSections.Slots | SnapshotSections.Tracking));
        await ExecuteAsync("INSERT INTO TrackingItems(GuildId,ChannelId,Identity,Item) VALUES('g','c','legacy:1','Old provisional item');");
        var result = await store.ApplySnapshotAsync(Snapshot([Item(1), Item(2)]), enableNotifications: cutover);
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM TrackingItems WHERE Identity LIKE 'legacy:%';"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("TrackingItems"));
        Assert.Equal(0, result.EventsCreated);
        Assert.Equal(0, result.DeliveriesCreated);
    }

    [Fact]
    public async Task Failure_mid_delta_rolls_back_items_snapshot_ledger_and_outbox_together()
    {
        using var database = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot([Item(1), Item(2)]));
        var hash = await ScalarAsync("SELECT CurrentSnapshotHash FROM TrackedRooms;");
        await AuditAsync();
        await ExecuteAsync("CREATE TRIGGER fail_item_insert AFTER INSERT ON TrackingItems WHEN NEW.ItemId=103 BEGIN SELECT RAISE(ABORT,'simulated failure'); END;");
        await Assert.ThrowsAnyAsync<Exception>(() => store.ApplySnapshotAsync(
            Snapshot([Item(2) with { ItemDisplayName = "Updated sword" }, Item(3)]),
            [TrackingDestination.DiscordNotification("c")]));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("TrackingItems"));
        Assert.Equal("Item 2", await ScalarAsync("SELECT Item FROM TrackingItems WHERE ItemId=102;"));
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM TrackingItems WHERE ItemId=101;"));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM TrackingItems WHERE ItemId=103;"));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM ItemWrites;"));
        Assert.Equal(hash, await ScalarAsync("SELECT CurrentSnapshotHash FROM TrackedRooms;"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("RoomSnapshots"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    private static NormalizedItemTransfer Item(int i) => new(2, 1, 100 + i, 200 + i, 1,
        "Fallback sender", "Fallback receiver", $"Item {i}", $"Location {i}", "Game B", "Game A");

    private static NormalizedRoomSnapshot Snapshot(IEnumerable<NormalizedItemTransfer> items,
        IEnumerable<NormalizedSlot>? slots = null, string guild = "g", string channel = "c", SnapshotSections sections = SnapshotSections.All,
        IEnumerable<NormalizedCheck>? checks = null)
        => NormalizedRoomSnapshot.Create(guild, channel, slots ?? Slots, items, [], checks ?? [], [], [], null, T0, T0,
            TrackingObservationState.Healthy, null, sections);

    private static Task AuditAsync() => ExecuteAsync(@"
        CREATE TABLE ItemWrites(Kind TEXT, GuildId TEXT, ChannelId TEXT, Identity TEXT);
        CREATE TRIGGER audit_item_insert AFTER INSERT ON TrackingItems BEGIN
            INSERT INTO ItemWrites VALUES('insert',NEW.GuildId,NEW.ChannelId,NEW.Identity); END;
        CREATE TRIGGER audit_item_update AFTER UPDATE ON TrackingItems BEGIN
            INSERT INTO ItemWrites VALUES('update',NEW.GuildId,NEW.ChannelId,NEW.Identity); END;
        CREATE TRIGGER audit_item_delete AFTER DELETE ON TrackingItems BEGIN
            INSERT INTO ItemWrites VALUES('delete',OLD.GuildId,OLD.ChannelId,OLD.Identity); END;");

    private static Task ExecuteAsync(string sql) => Db.WriteAsync(async connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    });

    private static async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
