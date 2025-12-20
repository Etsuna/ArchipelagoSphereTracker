using ArchipelagoSphereTracker.src.Resources;
using ArchipelagoSphereTracker.src.TrackerLib.Services;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;

public static class DBMigration_5
{
    public static async Task Migrate_5_0_1(CancellationToken ct = default)
    {
        await using var conn = await Db.OpenWriteAsync();

        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = @"
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                PRAGMA foreign_keys=ON;
                PRAGMA temp_store=MEMORY;
            ";
            pragma.ExecuteNonQuery();
        }

        using (var transaction = conn.BeginTransaction())
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = transaction;

            cmd.CommandText = @"
-- ==========================
-- 🎯 ReceiverAliasesTable migration
-- ==========================

CREATE TABLE IF NOT EXISTS ReceiverAliasesTable_new (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId   TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    Receiver  TEXT NOT NULL,
    UserId    TEXT NOT NULL,
    Flag      TEXT NOT NULL
);

INSERT INTO ReceiverAliasesTable_new (Id, GuildId, ChannelId, Receiver, UserId, Flag)
SELECT
    Id,
    GuildId,
    ChannelId,
    Receiver,
    UserId,
    CASE
        WHEN IFNULL(IsEnabled, 0) = 0 THEN 0
        ELSE 1
    END AS Flag
FROM ReceiverAliasesTable;

DROP TABLE ReceiverAliasesTable;

ALTER TABLE ReceiverAliasesTable_new RENAME TO ReceiverAliasesTable;
";
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }

        using (var pragmaOn = conn.CreateCommand())
        {
            pragmaOn.CommandText = "PRAGMA foreign_keys = ON;";
            pragmaOn.ExecuteNonQuery();
        }

        await PostMigrationMaintenanceAsync();
    }

    private static async Task PostMigrationMaintenanceAsync()
    {
        await using var conn = await Db.OpenWriteAsync();

        using (var optimize = conn.CreateCommand())
        {
            optimize.CommandText = "PRAGMA optimize;";
            optimize.ExecuteNonQuery();
        }
        using (var analyze = conn.CreateCommand())
        {
            analyze.CommandText = "ANALYZE;";
            analyze.ExecuteNonQuery();
        }
        using (var vacuum = conn.CreateCommand())
        {
            vacuum.CommandText = "VACUUM;";
            vacuum.ExecuteNonQuery();
        }
    }
}
