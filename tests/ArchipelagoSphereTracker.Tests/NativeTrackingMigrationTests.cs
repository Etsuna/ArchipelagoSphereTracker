using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.src.TrackerLib.Services;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;
using Xunit;

public sealed class NativeTrackingMigrationTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UtcNow;
    private static readonly NormalizedItemTransfer Item = new(2, 1, 100, 200, 1,
        "Sender", "Receiver", "Native Item", "Native Location", "Game B", "Game A");
    private static readonly NormalizedHint Hint = new(2, 1, 101, 201, false, "Entrance",
        "Sender", "Receiver", "Native Hint", "Hint Location", "Game B", "Game A");

    [Fact]
    public async Task Migration_preserves_legacy_reads_preferences_and_recaps_before_first_poll()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        await DBMigration_5.Migrate_5_0_16();
        await DBMigration_5.Migrate_5_0_16();
        Assert.Equal("Old Item", Assert.Single(await DisplayItemCommands.GetAliasItems("g", "c", "Receiver")).Item);
        Assert.Equal("Old Hint", Assert.Single(await HintStatusCommands.GetHintStatusForReceiver("g", "c", "Receiver")).Item);
        Assert.Equal("3", Assert.Single(await GameStatusCommands.GetGameStatusForGuildAndChannelAsync("g", "c")).Checks);
        Assert.Equal(new[] { "Receiver", "Sender" }, await AliasChoicesCommands.GetAliasesForGuildAndChannelAsync("g", "c"));
        var context = await ProcessingContextLoader.LoadOneShotAsync("g", "c", false);
        Assert.Equal(2, context.SlotIndex.Count);
        Assert.True(context.TryGetPlayerLocationsTotal(1, out var total));
        Assert.Equal(10, total);
        Assert.Equal("Old Recap", (await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync("g", "c", "100"))["Receiver"].Single().Item);
        Assert.Contains("Old Item", await ExcludedItemsCommands.GetExcludedItemsForUserByAliasAsync("g", "c", "100", "Receiver"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("ReceiverAliasesTable"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DisplayedItemTable"));
        Assert.Null(await ScalarAsync("SELECT ItemId FROM TrackingItems LIMIT 1;"));
    }

    [Fact]
    public async Task Command_reads_and_polling_continue_when_all_old_tracking_tables_are_unavailable()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        await DBMigration_5.Migrate_5_0_16();
        await ExecuteAsync("DROP TABLE DisplayedItemTable; DROP TABLE HintStatusTable; DROP TABLE GameStatusTable; DROP TABLE AliasChoicesTable;");
        var context = await ProcessingContextLoader.LoadOneShotAsync("g", "c", false);
        const string json = """{"player_items_received":[{"player":1,"items":[[100,200,2,1]]}],"hints":[{"player":1,"hints":[[1,2,201,101,false,"Entrance"]]}],"player_checks_done":[{"player":1,"locations":[200]}],"player_status":[{"player":1,"status":20},{"player":2,"status":20}]}""";
        var result = await TrackingDataManager.ProcessTrackerResponseAsync(context, json,
            new Dictionary<int,int> { [1] = 10, [2] = 10 }, silent: true, isAddUrl: false);
        Assert.True(result.Success);
        Assert.Single(await DisplayItemCommands.GetUserItemsGroupedAsync("g", "c", "Receiver"));
        Assert.Single(await HintStatusCommands.GetHintStatusForFinder("g", "c", "Sender"));
        Assert.Equal(2, (await GameStatusCommands.GetGameStatusForGuildAndChannelAsync("g", "c")).Count);
        Assert.Equal("Game A", await AliasChoicesCommands.GetGameForAliasAsync("g", "c", "Receiver"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries")); // first native baseline
    }

    [Fact]
    public async Task Migration_uses_active_v2_snapshot_instead_of_stale_legacy_rows()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot([Item], [Hint]), enableNotifications: true);
        await DBMigration_5.Migrate_5_0_16();
        var item = Assert.Single(await DisplayItemCommands.GetAliasItems("g", "c", "Receiver"));
        Assert.Equal("Native Item", item.Item);
        Assert.Equal(100, item.ItemId);
        Assert.Equal(2, item.FinderSlot);
        Assert.Equal("Native Hint", Assert.Single(await HintStatusCommands.GetHintStatus("g", "c")).Item);
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DisplayedItemTable")); // archive untouched
    }

    [Fact]
    public async Task Migration_is_atomic_and_retryable_after_import_failure()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        await ExecuteAsync("CREATE TRIGGER fail_import BEFORE INSERT ON TrackingItems BEGIN SELECT RAISE(ABORT,'simulated'); END;");
        await Assert.ThrowsAnyAsync<Exception>(() => DBMigration_5.Migrate_5_0_16());
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingReadModelMigrations"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingSlots"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DisplayedItemTable"));
        await ExecuteAsync("DROP TRIGGER fail_import;");
        await DBMigration_5.Migrate_5_0_16();
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("TrackingItems"));
    }

    [Fact]
    public async Task First_partial_native_poll_keeps_imported_progress_instead_of_stale_shadow_state()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        // Earlier shadow poll knew only one check; V1 subsequently reached three.
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot([Item]));
        await DBMigration_5.Migrate_5_0_16();
        var context = await ProcessingContextLoader.LoadOneShotAsync("g", "c", true);
        const string statuses = """{"player_status":[{"player":1,"status":30},{"player":2,"status":20}]}""";
        await TrackingDataManager.ProcessTrackerResponseAsync(context, statuses,
            new Dictionary<int,int> { [1]=10, [2]=10 }, true, false);
        var state = (await GameStatusCommands.GetGameStatusForGuildAndChannelAsync("g", "c")).Single(value => value.Slot == 1);
        Assert.Equal("3", state.Checks);
        Assert.Equal("Old Item", Assert.Single(await DisplayItemCommands.GetAliasItems("g", "c", "Receiver")).Item);
        // A later complete, empty checks section is real zero, not "missing".
        await TrackingDataManager.ProcessTrackerResponseAsync(context, """{"player_checks_done":[]}""",
            new Dictionary<int,int> { [1]=10, [2]=10 }, true, false);
        state = (await GameStatusCommands.GetGameStatusForGuildAndChannelAsync("g", "c")).Single(value => value.Slot == 1);
        Assert.Equal("0", state.Checks);
        Assert.Equal("GoalReached", await ScalarAsync("SELECT Status FROM TrackingPlayerStates WHERE Slot=1;"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    [Fact]
    public async Task Repeating_migration_never_resurrects_provisional_legacy_history()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        await DBMigration_5.Migrate_5_0_16();
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot(), enableNotifications: true);
        await DBMigration_5.Migrate_5_0_16();
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingItems"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DisplayedItemTable"));
    }

    [Fact]
    public async Task Recap_append_uses_unique_v2_transfers_and_preserves_personal_clears()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot());
        await RecapListCommands.AddOrEditRecapListAsync("g", "c", "100", "Receiver");
        var current = Snapshot([Item]);
        await store.ApplySnapshotAsync(current);
        await store.ApplySnapshotAsync(current);
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("RecapListItemsTable"));
        Assert.Equal("Native Item", (await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync("g", "c", "100"))["Receiver"].Single().Item);
        await RecapListCommands.DeleteAliasAndItemsForUserIdAsync("g", "c", "100");
        await store.ApplySnapshotAsync(current);
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("RecapListItemsTable"));
        await store.ApplySnapshotAsync(Snapshot([Item, Item with { ItemId = 102, LocationId = 202, ItemDisplayName = "New Item" }]));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("RecapListItemsTable"));
        Assert.Equal("New Item", (await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync("g", "c", "100"))["Receiver"].Single().Item);
    }

    [Fact]
    public async Task Failed_snapshot_rolls_back_command_views_recaps_events_and_outbox_together()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot(), enableNotifications: true);
        await RecapListCommands.AddOrEditRecapListAsync("g", "c", "100", "Receiver");
        var store = new TrackingV2Store((point, _) => point == TrackingV2FaultPoint.DeliveryInserted
            ? throw new InvalidOperationException("simulated") : Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ApplySnapshotAsync(
            Snapshot([Item], [Hint]), [TrackingDestination.DiscordNotification("c")], enableNotifications: true));
        Assert.Empty(await DisplayItemCommands.GetAliasItems("g", "c", "Receiver"));
        Assert.Empty(await HintStatusCommands.GetHintStatus("g", "c"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("RecapListItemsTable"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("RoomSnapshots"));
    }

    [Fact]
    public async Task Checks_advance_independently_when_client_status_is_absent()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        var store = new TrackingV2Store();
        var baseline = Snapshot([Item], [Hint], goal: true);
        await store.ApplySnapshotAsync(baseline);
        var partial = NormalizedRoomSnapshot.Create("g", "c", baseline.Slots, [], [],
            [new(1, 200), new(1, 201)], [],
            [new(1, NormalizedPlayerStatus.Unknown, 2, null, null), new(2, NormalizedPlayerStatus.Unknown, 0, null, null)],
            null, T0.AddMinutes(1), T0, TrackingObservationState.Healthy, null, SnapshotSections.Checks | SnapshotSections.Tracking);
        await store.ApplySnapshotAsync(partial);
        var player = (await GameStatusCommands.GetGameStatusForGuildAndChannelAsync("g", "c")).Single(value => value.Slot == 1);
        Assert.Equal("2", player.Checks);
        Assert.Equal("10", player.Total);
        Assert.Equal("GoalReached", await ScalarAsync("SELECT Status FROM TrackingPlayerStates WHERE Slot=1;"));
        Assert.Equal(1L, await ScalarAsync("SELECT GoalCompleted FROM TrackingPlayerStates WHERE Slot=1;"));
        Assert.Single(await HintStatusCommands.GetHintStatus("g", "c"));
        Assert.Single(await DisplayItemCommands.GetAliasItems("g", "c", "Receiver"));
    }

    [Fact]
    public async Task Activity_only_changes_do_not_rewrite_unchanged_item_history()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot([Item]));
        await ExecuteAsync("CREATE TABLE ItemWrites(Count INTEGER); INSERT INTO ItemWrites VALUES(0); CREATE TRIGGER track_item_insert AFTER INSERT ON TrackingItems BEGIN UPDATE ItemWrites SET Count=Count+1; END;");
        await store.ApplySnapshotAsync(Snapshot([Item], goal: true));
        Assert.Equal(0L, await ScalarAsync("SELECT Count FROM ItemWrites;"));
        Assert.Single(await DisplayItemCommands.GetAliasItems("g", "c", "Receiver"));
    }

    [Fact]
    public async Task Malformed_native_payload_cannot_overwrite_last_good_command_state()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot([Item]));
        var context = await ProcessingContextLoader.LoadOneShotAsync("g", "c", true);
        var result = await TrackingDataManager.ProcessTrackerResponseAsync(context,
            """{"player_items_received":[{"player":1,"items":[[100,200]]}]}""",
            new Dictionary<int,int> { [1] = 10 }, true, false);
        Assert.False(result.Success);
        Assert.Single(await DisplayItemCommands.GetAliasItems("g", "c", "Receiver"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("RoomSnapshots"));
    }

    [Fact]
    public async Task Deleting_room_cascades_to_all_native_reads()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot([Item], [Hint]));
        await DatabaseCommands.DeleteChannelDataAsync("g", "c");
        foreach (var table in new[] { "TrackingSlots", "TrackingItems", "TrackingHints", "TrackingPlayerStates" })
            Assert.Equal(0, await TestDatabaseScope.CountRowsAsync(table));
    }

    private static NormalizedRoomSnapshot Snapshot(IEnumerable<NormalizedItemTransfer>? items = null,
        IEnumerable<NormalizedHint>? hints = null, bool goal = false)
        => NormalizedRoomSnapshot.Create("g", "c",
            [new(1, "Receiver", "Display Receiver", "Game A"), new(2, "Sender", "Sender", "Game B")],
            items ?? [], hints ?? [], [new(1, 200)], [new(1, "completion", goal)],
            [new(1, goal ? NormalizedPlayerStatus.GoalReached : NormalizedPlayerStatus.Playing, 1, 10, "00:01:00"),
             new(2, NormalizedPlayerStatus.Playing, 0, 10, null)],
            null, T0, T0, TrackingObservationState.Healthy, null, SnapshotSections.All);

    private static Task SeedLegacyAsync() => ExecuteAsync("""
        INSERT INTO ChannelsAndUrlsTable(GuildId,ChannelId,BaseUrl,Room,Tracker,CheckFrequency,Silent)
        VALUES('g','c','https://example.test','room','tracker','5m',0);
        INSERT INTO AliasChoicesTable(GuildId,ChannelId,Slot,Alias,Game)
        VALUES('g','c',1,'Receiver','Game A'),('g','c',2,'Sender','Game B');
        INSERT INTO DisplayedItemTable(GuildId,ChannelId,Finder,Receiver,Item,Location,Game,Flag)
        VALUES('g','c','Sender','Receiver','Old Item','Old Location','Game B','1');
        INSERT INTO HintStatusTable(GuildId,ChannelId,Finder,Receiver,Item,Location,Game,Entrance,Flag)
        VALUES('g','c','Sender','Receiver','Old Hint','Hint Location','Game B','Vanilla','False');
        INSERT INTO GameStatusTable(GuildId,ChannelId,Name,Game,Checks,Total,LastActivity)
        VALUES('g','c','Receiver','Game A','3','10','00:02:00');
        INSERT INTO ReceiverAliasesTable(GuildId,ChannelId,UserId,Receiver,Flag) VALUES('g','c','100','Receiver','0');
        INSERT INTO RecapListTable(GuildId,ChannelId,UserId,Alias) VALUES('g','c','100','Receiver');
        INSERT INTO RecapListItemsTable(RecapListTableId,Item) VALUES(last_insert_rowid(),'Old Recap');
        INSERT INTO ExcludedItemTable(GuildId,ChannelId,UserId,Alias,Item) VALUES('g','c','100','Receiver','Old Item');
        """);

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
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }
}
