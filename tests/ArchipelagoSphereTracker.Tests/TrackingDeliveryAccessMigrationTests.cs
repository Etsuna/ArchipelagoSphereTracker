using System;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.Tracking.Persistence;
using Xunit;
using static TrackingDeliveryAccessTests;

public sealed class TrackingDeliveryAccessMigrationTests
{
    private const string LegacyTable = @"CREATE TABLE EventDeliveries (
        Id INTEGER PRIMARY KEY AUTOINCREMENT, EventId INTEGER NOT NULL,
        DestinationType TEXT NOT NULL, DestinationId TEXT NOT NULL,
        Status TEXT NOT NULL CHECK(Status IN ('Pending','Delivering','Delivered','Failed')),
        AttemptCount INTEGER NOT NULL DEFAULT 0,NextAttemptAtUtc TEXT NOT NULL,
        LeaseUntilUtc TEXT,LastAttemptAtUtc TEXT,DeliveredAtUtc TEXT,LastErrorCode TEXT,ExternalReceiptId TEXT,
        UNIQUE(EventId,DestinationType,DestinationId),
        FOREIGN KEY(EventId) REFERENCES TrackingEvents(Id) ON DELETE CASCADE);";

    [Fact]
    public async Task Startup_migration_preserves_the_outbox_receipts_constraints_indexes_triggers_and_sequence()
    {
        using var database = new TestDatabaseScope();
        await PrepareLegacyAsync();
        await ExecuteAsync(@"UPDATE EventDeliveries SET Status=CASE Id WHEN 1 THEN 'Pending' WHEN 2 THEN 'Failed'
                WHEN 3 THEN 'Delivering' ELSE 'Delivered' END,
                AttemptCount=Id+5,LastErrorCode='HTTP_403_DISCORD_50001',ExternalReceiptId='receipt-'||Id,
                LastAttemptAtUtc='2026-11-06T00:00:00.0000000+00:00',LeaseUntilUtc='2026-11-08T00:00:00.0000000+00:00';
            CREATE INDEX custom_delivery_index ON EventDeliveries(ExternalReceiptId);
            CREATE TABLE DeliveryAudit(Value INTEGER);
            CREATE TRIGGER delivery_audit AFTER UPDATE OF Status ON EventDeliveries
                BEGIN INSERT INTO DeliveryAudit VALUES(NEW.Id); END;
            INSERT INTO EventDeliveries(Id,EventId,DestinationType,DestinationId,Status,NextAttemptAtUtc)
                SELECT 900,EventId,'Removed','c','Pending',NextAttemptAtUtc FROM EventDeliveries LIMIT 1;
            DELETE FROM EventDeliveries WHERE Id=900;
            INSERT INTO DiscordNotificationBatches(BatchKey,SnapshotId,ChannelId,PayloadJson)
                VALUES('acknowledged',1,'c','frozen receipts');");
        var before = await ScalarAsync(@"SELECT group_concat(Id||':'||EventId||':'||Status||':'||AttemptCount||':'||
            NextAttemptAtUtc||':'||LeaseUntilUtc||':'||LastAttemptAtUtc||':'||LastErrorCode||':'||ExternalReceiptId,'|') FROM EventDeliveries;");
        var originalBackupPath = Declare.DatabaseBackupPath;
        Declare.DatabaseBackupPath = Path.Combine(database.BaseDirectory, "backups");
        try
        {
            await (Task)typeof(Program).GetMethod("CheckBdd", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
            var backup = Assert.Single(Directory.GetFiles(Declare.DatabaseBackupPath, "AST-pre-5.0.21-*.db"));
            await using var backupConnection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = backup, ReadOnly = true }.ToString());
            await backupConnection.OpenAsync();
            using var command = backupConnection.CreateCommand();
            command.CommandText = "SELECT instr(sql,'''Suspended''') FROM sqlite_master WHERE name='EventDeliveries';";
            Assert.Equal(0L, await command.ExecuteScalarAsync());
        }
        finally { Declare.DatabaseBackupPath = originalBackupPath; }
        Assert.Equal("5.0.22", await DBMigration.GetCurrentDbVersionAsync());
        Assert.Equal(before, await ScalarAsync(@"SELECT group_concat(Id||':'||EventId||':'||Status||':'||AttemptCount||':'||
            NextAttemptAtUtc||':'||LeaseUntilUtc||':'||LastAttemptAtUtc||':'||LastErrorCode||':'||ExternalReceiptId,'|') FROM EventDeliveries;"));
        Assert.Equal(12L, await ScalarAsync("SELECT COUNT(*) FROM pragma_table_info('EventDeliveries');"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("DeliveryAudit"));
        Assert.Equal("frozen receipts", await ScalarAsync("SELECT PayloadJson FROM DiscordNotificationBatches;"));
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='custom_delivery_index';"));
        Assert.DoesNotContain("<> 'Delivered'", (string)(await ScalarAsync("SELECT sql FROM sqlite_master WHERE name='idx_eventdeliveries_unfinished_destination';"))!);
        Assert.Equal(900L, await ScalarAsync("SELECT seq FROM sqlite_sequence WHERE name='EventDeliveries';"));
        await Assert.ThrowsAsync<SQLiteException>(() => ExecuteAsync(@"INSERT INTO EventDeliveries(EventId,DestinationType,DestinationId,Status,NextAttemptAtUtc)
            SELECT EventId,DestinationType,DestinationId,'Pending',NextAttemptAtUtc FROM EventDeliveries LIMIT 1;"));
        await ExecuteAsync(@"UPDATE EventDeliveries SET Status='Suspended' WHERE Id=1;
            INSERT INTO EventDeliveries(EventId,DestinationType,DestinationId,Status,NextAttemptAtUtc)
                SELECT EventId,'New','c','Pending',NextAttemptAtUtc FROM EventDeliveries LIMIT 1;");
        Assert.Equal(901L, await ScalarAsync("SELECT MAX(Id) FROM EventDeliveries;"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DeliveryAudit"));
        await DBMigration_5.Migrate_5_0_22();
        await DatabaseInitializer.InitializeDatabaseAsync();
        Assert.Equal(5, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        Assert.Null(await ScalarAsync("PRAGMA foreign_key_check;"));
        Assert.Equal("ok", await ScalarAsync("PRAGMA quick_check;"));
        await ExecuteAsync("DELETE FROM TrackedRooms;");
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unsupported_custom_schema_stops_before_rebuilding_any_rows(bool foreignConsumer)
    {
        using var database = new TestDatabaseScope();
        await PrepareLegacyAsync();
        await ExecuteAsync(foreignConsumer
            ? "CREATE TABLE DeliveryConsumer(DeliveryId INTEGER REFERENCES EventDeliveries(Id) ON DELETE CASCADE); INSERT INTO DeliveryConsumer VALUES(1);"
            : "ALTER TABLE EventDeliveries ADD COLUMN CustomPayload TEXT; UPDATE EventDeliveries SET CustomPayload='must keep';");
        await Assert.ThrowsAsync<InvalidOperationException>(() => DBMigration_5.Migrate_5_0_22());
        Assert.Equal(4, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        Assert.Equal(0L, await ScalarAsync("SELECT instr(sql,'''Suspended''') FROM sqlite_master WHERE name='EventDeliveries';"));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='EventDeliveries_v22';"));
        Assert.Equal("5.0.21", await DBMigration.GetCurrentDbVersionAsync());
        if (foreignConsumer) Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DeliveryConsumer"));
        else Assert.Equal(4L, await ScalarAsync("SELECT COUNT(*) FROM EventDeliveries WHERE CustomPayload='must keep';"));
    }

    [Fact]
    public async Task Rebuild_failure_rolls_back_the_original_table_rows_and_indexes()
    {
        using var database = new TestDatabaseScope();
        await PrepareLegacyAsync();
        // Reserved staging table collision simulates a failure during the copy.
        await ExecuteAsync("CREATE TABLE EventDeliveries_v22(Unexpected INTEGER);");
        await Assert.ThrowsAsync<SQLiteException>(() => DBMigration_5.Migrate_5_0_22());
        Assert.Equal(4, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        Assert.Equal(0L, await ScalarAsync("SELECT instr(sql,'''Suspended''') FROM sqlite_master WHERE name='EventDeliveries';"));
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='idx_eventdeliveries_unfinished_destination';"));
        await ExecuteAsync("DROP TABLE EventDeliveries_v22;");
        await DBMigration_5.Migrate_5_0_22();
        Assert.Equal(4, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        Assert.Null(await ScalarAsync("PRAGMA foreign_key_check;"));
    }

    [Fact]
    public async Task Earlier_initialization_can_run_before_the_5_0_22_table_rebuild()
    {
        using var database = new TestDatabaseScope();
        await PrepareLegacyAsync();
        await DatabaseInitializer.InitializeDatabaseAsync();
        Assert.Equal(0L, await ScalarAsync("SELECT instr(sql,'''Suspended''') FROM sqlite_master WHERE name='EventDeliveries';"));
        await DBMigration_5.Migrate_5_0_22();
        await DBMigration_5.Migrate_5_0_22();
        Assert.True(Convert.ToInt64(await ScalarAsync("SELECT instr(sql,'''Suspended''') FROM sqlite_master WHERE name='EventDeliveries';")) > 0);
    }

    private static async Task PrepareLegacyAsync()
    {
        await QueueAsync("c", 4);
        // Save only the original columns, then restore the exact pre-5.0.22 CHECK.
        await ExecuteAsync(@"CREATE TEMP TABLE old_deliveries AS SELECT Id,EventId,DestinationType,DestinationId,Status,
                AttemptCount,NextAttemptAtUtc,LeaseUntilUtc,LastAttemptAtUtc,DeliveredAtUtc,LastErrorCode,ExternalReceiptId
                FROM EventDeliveries;
            DROP TABLE EventDeliveries;" + LegacyTable + @"
            INSERT INTO EventDeliveries SELECT * FROM old_deliveries;
            DROP TABLE old_deliveries;
            CREATE INDEX idx_eventdeliveries_due ON EventDeliveries(Status,NextAttemptAtUtc,LeaseUntilUtc,Id);
            CREATE INDEX idx_eventdeliveries_unfinished_destination ON EventDeliveries(DestinationType,DestinationId,Id)
                WHERE Status <> 'Delivered';");
        await DBMigration.SetDbVersionAsync("5.0.21");
    }
}
