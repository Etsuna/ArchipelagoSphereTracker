using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;
using Xunit;

public sealed class TrackingLegacyRetirementTests
{
    [Fact]
    public async Task Fresh_database_and_reinitialization_never_create_the_four_v1_tables()
    {
        using var scope = new TestDatabaseScope();
        foreach (var table in LegacyTrackingSchema.TableNames) Assert.False(await ExistsAsync(table));
        await DatabaseInitializer.InitializeDatabaseAsync();
        foreach (var table in LegacyTrackingSchema.TableNames) Assert.False(await ExistsAsync(table));
        foreach (var table in new[] { "TrackingSlots", "TrackingItems", "TrackingHints", "TrackingPlayerStates" })
            Assert.True(await ExistsAsync(table));
    }

    [Fact]
    public async Task Upgrade_imports_existing_v1_data_and_preferences_then_drops_only_v1_tracking_tables()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        await DBMigration_5.Migrate_5_0_18();
        foreach (var table in LegacyTrackingSchema.TableNames) Assert.False(await ExistsAsync(table));
        Assert.Equal("Legacy Item", Assert.Single(await DisplayItemCommands.GetAliasItems("g", "c", "Receiver")).Item);
        Assert.Equal("Legacy Hint", Assert.Single(await HintStatusCommands.GetHintStatus("g", "c")).Item);
        Assert.Equal("3", Assert.Single(await GameStatusCommands.GetGameStatusForGuildAndChannelAsync("g", "c")).Checks);
        Assert.Equal(2, (await AliasChoicesCommands.GetAliasesForGuildAndChannelAsync("g", "c")).Count);
        foreach (var table in new[] { "ChannelsAndUrlsTable", "ReceiverAliasesTable", "RecapListTable", "RecapListItemsTable", "ExcludedItemTable" })
            Assert.Equal(1, await TestDatabaseScope.CountRowsAsync(table));
        Assert.Equal("Personal Recap", (await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync("g", "c", "100"))["Receiver"].Single().Item);
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("TrackingReadModelMigrations"));
    }

    [Fact]
    public async Task Repeat_upgrade_and_restart_do_not_recreate_tables_or_duplicate_data()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        await DBMigration_5.Migrate_5_0_18();
        await DBMigration_5.Migrate_5_0_18();
        await DBMigration_5.Migrate_5_0_16();
        await DatabaseInitializer.InitializeDatabaseAsync();
        foreach (var table in LegacyTrackingSchema.TableNames) Assert.False(await ExistsAsync(table));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("TrackingItems"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("TrackingHints"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("RecapListItemsTable"));
    }

    [Fact]
    public async Task Empty_v1_tables_are_also_removed()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await DBMigration_5.Migrate_5_0_18();
        foreach (var table in LegacyTrackingSchema.TableNames) Assert.False(await ExistsAsync(table));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingItems"));
    }

    [Fact]
    public async Task Native_only_database_can_run_the_retirement_migration()
    {
        using var scope = new TestDatabaseScope();
        await DBMigration_5.Migrate_5_0_18();
        await DBMigration_5.Migrate_5_0_18();
        foreach (var table in LegacyTrackingSchema.TableNames) Assert.False(await ExistsAsync(table));
    }

    [Fact]
    public async Task Partially_missing_legacy_schema_still_imports_remaining_data()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        await ExecuteAsync("DROP TABLE HintStatusTable;");
        await DBMigration_5.Migrate_5_0_18();
        Assert.Single(await DisplayItemCommands.GetAliasItems("g", "c", "Receiver"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("TrackingSlots"));
        foreach (var table in LegacyTrackingSchema.TableNames) Assert.False(await ExistsAsync(table));
    }

    [Fact]
    public async Task Failed_import_rolls_back_and_keeps_all_source_tables_and_rows()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        await ExecuteAsync("CREATE TRIGGER fail_import BEFORE INSERT ON TrackingItems BEGIN SELECT RAISE(ABORT,'simulated'); END;");
        await Assert.ThrowsAnyAsync<Exception>(() => DBMigration_5.Migrate_5_0_18());
        foreach (var table in LegacyTrackingSchema.TableNames) Assert.True(await ExistsAsync(table));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DisplayedItemTable"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingItems"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingSlots"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingReadModelMigrations"));
        await ExecuteAsync("DROP TRIGGER fail_import;");
        await DBMigration_5.Migrate_5_0_18();
        Assert.Single(await DisplayItemCommands.GetAliasItems("g", "c", "Receiver"));
        foreach (var table in LegacyTrackingSchema.TableNames) Assert.False(await ExistsAsync(table));
    }

    [Fact]
    public async Task Unexpected_foreign_reference_blocks_retirement_instead_of_cascading_user_data()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        await ExecuteAsync(@"CREATE TABLE CustomConsumer(Id INTEGER PRIMARY KEY, ItemId INTEGER REFERENCES DisplayedItemTable(Id) ON DELETE CASCADE);
            INSERT INTO CustomConsumer(ItemId) SELECT Id FROM DisplayedItemTable;");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DBMigration_5.Migrate_5_0_18());
        Assert.Contains("CustomConsumer", error.Message);
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("CustomConsumer"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DisplayedItemTable"));
        foreach (var table in LegacyTrackingSchema.TableNames) Assert.True(await ExistsAsync(table));
    }

    [Fact]
    public async Task Upgrade_refreshes_provisional_import_when_v1_wrote_more_rows_after_5016()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        await DBMigration_5.Migrate_5_0_16();
        await ExecuteAsync(@"INSERT INTO DisplayedItemTable(GuildId,ChannelId,Finder,Receiver,Item,Location,Game,Flag)
            VALUES('g','c','Sender','Receiver','Later V1 Item','Later Location','Game B','1');");
        await DBMigration_5.Migrate_5_0_18();
        var items = await DisplayItemCommands.GetAliasItems("g", "c", "Receiver");
        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Item == "Later V1 Item");
        Assert.False(await ExistsAsync("DisplayedItemTable"));
    }

    [Fact]
    public async Task Mixed_v1_and_active_v2_rooms_keep_native_snapshots_and_pending_deliveries()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        var store = new TrackingV2Store();
        var now = DateTimeOffset.UtcNow;
        NormalizedRoomSnapshot Snapshot(bool received) => NormalizedRoomSnapshot.Create("g", "native",
            [new(1,"Receiver","Receiver","Game A"),new(2,"Sender","Sender","Game B")],
            received ? [new(2,1,100,200,1,"Sender","Receiver","Native Item","Location","Game B","Game A")] : [],
            [],[],[],[],null,now,null,TrackingObservationState.Healthy,null,SnapshotSections.All);
        await store.ApplySnapshotAsync(Snapshot(false), enableNotifications: true);
        await store.ApplySnapshotAsync(Snapshot(true), [TrackingDestination.DiscordNotification("native")], enableNotifications: true);
        var before = await ScalarAsync("SELECT EventKey FROM TrackingEvents WHERE EventType='ItemReceived';");
        await DBMigration_5.Migrate_5_0_18();
        Assert.Equal("Native Item", Assert.Single(await DisplayItemCommands.GetAliasItems("g", "native", "Receiver")).Item);
        Assert.Equal("Legacy Item", Assert.Single(await DisplayItemCommands.GetAliasItems("g", "c", "Receiver")).Item);
        Assert.Equal(before, await ScalarAsync("SELECT EventKey FROM TrackingEvents WHERE EventType='ItemReceived';"));
        Assert.Equal("Pending", await ScalarAsync("SELECT Status FROM EventDeliveries;"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("RoomSnapshots"));
        foreach (var table in LegacyTrackingSchema.TableNames) Assert.False(await ExistsAsync(table));
    }

    [Fact]
    public async Task Room_and_guild_cleanup_work_after_source_tables_are_removed()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        await DBMigration_5.Migrate_5_0_18();
        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync("other", "kept", "https://example.test", "other", "tracker", false, "5m", "0");
        await AliasChoicesCommands.AddOrReplaceAliasChoiceAsync("other", "kept", [(1,"Other Player","Other Game")]);
        await DatabaseCommands.DeleteChannelDataAsync("g", "c");
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingItems", "g", "c"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("ChannelsAndUrlsTable", "g", "c"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("TrackingSlots", "other", "kept"));
        await DatabaseCommands.DeleteChannelDataByGuildIdAsync("other");
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackedRooms"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("ChannelsAndUrlsTable"));
    }

    [Fact]
    public async Task Pre_migration_backup_keeps_v1_rows_after_live_tables_are_retired()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await SeedLegacyAsync();
        var backup = await DBMigration.CreatePreMigrationBackupAsync("5.0.17", Path.Combine(scope.BaseDirectory,"backups"));
        await DBMigration_5.Migrate_5_0_18();
        Assert.False(await ExistsAsync("DisplayedItemTable"));
        await using var connection = new SQLiteConnection($"Data Source={backup};Version=3;Read Only=True;");
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Item FROM DisplayedItemTable;";
        Assert.Equal("Legacy Item", await command.ExecuteScalarAsync());
    }

    private static Task SeedLegacyAsync() => ExecuteAsync(@"
        INSERT INTO ChannelsAndUrlsTable(GuildId,ChannelId,BaseUrl,Room,Tracker,CheckFrequency,Silent)
        VALUES('g','c','https://example.test','room','tracker','5m',0);
        INSERT INTO AliasChoicesTable(GuildId,ChannelId,Slot,Alias,Game)
        VALUES('g','c',1,'Receiver','Game A'),('g','c',2,'Sender','Game B');
        INSERT INTO DisplayedItemTable(GuildId,ChannelId,Finder,Receiver,Item,Location,Game,Flag)
        VALUES('g','c','Sender','Receiver','Legacy Item','Location','Game B','1');
        INSERT INTO HintStatusTable(GuildId,ChannelId,Finder,Receiver,Item,Location,Game,Entrance,Flag)
        VALUES('g','c','Sender','Receiver','Legacy Hint','Hint Location','Game B','Vanilla','False');
        INSERT INTO GameStatusTable(GuildId,ChannelId,Name,Game,Checks,Total)
        VALUES('g','c','Receiver','Game A','3','10');
        INSERT INTO ReceiverAliasesTable(GuildId,ChannelId,UserId,Receiver,Flag) VALUES('g','c','100','Receiver','0');
        INSERT INTO RecapListTable(GuildId,ChannelId,UserId,Alias) VALUES('g','c','100','Receiver');
        INSERT INTO RecapListItemsTable(RecapListTableId,Item) VALUES(last_insert_rowid(),'Personal Recap');
        INSERT INTO ExcludedItemTable(GuildId,ChannelId,UserId,Alias,Item) VALUES('g','c','100','Receiver','Excluded Item');
    ");

    private static Task ExecuteAsync(string sql) => Db.WriteAsync(async connection =>
    {
        using var command = connection.CreateCommand(); command.CommandText=sql; await command.ExecuteNonQueryAsync();
    });

    private static async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand(); command.CommandText=sql; return await command.ExecuteScalarAsync();
    }

    private static async Task<bool> ExistsAsync(string table)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@Table;";
        command.Parameters.AddWithValue("@Table", table);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) != 0;
    }
}
