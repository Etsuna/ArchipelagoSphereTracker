public static class DBMigration_5
{
    public static async Task Migrate_5_0_1(CancellationToken ct = default)
    {
        Console.WriteLine("Migrating to DB version 5.0.1: Updating ReceiverAliasesTable schema.");

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

    public static async Task Migrate_5_0_2(CancellationToken ct = default)
    {
        Console.WriteLine("Migrating to DB version 5.0.2: Adding Port column to ChannelsAndUrlsTable and updating existing entries.");

        var guildList = await GetAllGuildChannelMappingsAsync();
        await Task.Delay(1000, ct);

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
            ALTER TABLE ChannelsAndUrlsTable
            ADD COLUMN Port TEXT;";
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }

        using (var pragmaOn = conn.CreateCommand())
        {
            pragmaOn.CommandText = "PRAGMA foreign_keys = ON;";
            pragmaOn.ExecuteNonQuery();
        }

        await PostMigrationMaintenanceAsync();

        foreach (var guild in guildList)
        {
            Console.WriteLine($"Migrate Guild: {guild.GuildId}, Channel: {guild.ChannelId}, Room: {guild.Room}");

            var roomInfo = await UrlClass.RoomInfo(guild.BaseUrl, guild.Room);
            if (roomInfo == null)
            {
                continue;
            }
            await ChannelsAndUrlsCommands.UpdateChannelPortAsync(guild.GuildId, guild.ChannelId, roomInfo.LastPort.ToString());
        }
    }

    public static async Task<List<GuildChannelMapping>> GetAllGuildChannelMappingsAsync()
    {
        var list = new List<GuildChannelMapping>();
        await using var conn = await Db.OpenReadAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT GuildId, ChannelId, BaseUrl, Room, Silent
        FROM ChannelsAndUrlsTable
        ORDER BY GuildId, ChannelId;";
        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(new GuildChannelMapping
            {
                GuildId = reader["GuildId"]?.ToString() ?? string.Empty,
                ChannelId = reader["ChannelId"]?.ToString() ?? string.Empty,
                BaseUrl = reader["BaseUrl"]?.ToString() ?? string.Empty,
                Room = reader["Room"]?.ToString() ?? string.Empty,
            });
        }
        return list;
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
    public class GuildChannelMapping
    {
        public string GuildId { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
    }

    public static async Task Migrate_5_0_3(CancellationToken ct = default)
    {
        Console.WriteLine("Migrating to DB version 5.0.3: Delete Telemetry.");

        var guildList = await GetAllGuildChannelMappingsAsync();
        await Task.Delay(1000, ct);

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
            DROP TABLE IF EXISTS [TelemetryTable];
            DROP TABLE IF EXISTS [ProgramIdTable];";
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

    public static async Task Migrate_5_0_4(CancellationToken ct = default)
    {
        Console.WriteLine("Migrating to DB version 5.0.4: Dropping ApWorldItemTable and ApWorldListTable as they are no longer used.");

        var guildList = await GetAllGuildChannelMappingsAsync();
        await Task.Delay(1000, ct);

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
            DROP TABLE IF EXISTS ApWorldItemTable;;
            DROP TABLE IF EXISTS ApWorldListTable;
            DROP INDEX IF EXISTS idx_apworldlist_title;
            DROP INDEX IF EXISTS idx_apworlditem_listid;
            DROP INDEX IF EXISTS idx_displayeditem_guild_channel;
            DROP INDEX IF EXISTS idx_displayeditem_receiver;
            DROP INDEX IF EXISTS idx_displayeditem_finder;
            DROP INDEX IF EXISTS idx_displayeditem_game_item;
            DROP INDEX IF EXISTS idx_recapitems_tableid;
            DROP INDEX IF EXISTS idx_receiveraliases_gcu;
            DROP INDEX IF EXISTS idx_displayeditem_gci; ";
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

    public static async Task Migrate_5_0_5(CancellationToken ct = default)
    {
        Console.WriteLine("Migrating to DB version 5.0.5: Dropping ApWorldItemTable and ApWorldListTable as they are no longer used.");

        var guildList = await GetAllGuildChannelMappingsAsync();
        await Task.Delay(1000, ct);

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
            DROP TABLE IF EXISTS ApWorldItemTable;;
            DROP TABLE IF EXISTS ApWorldListTable;
            DROP INDEX IF EXISTS idx_apworldlist_title;
            DROP INDEX IF EXISTS idx_apworlditem_listid;
            DROP INDEX IF EXISTS idx_displayeditem_guild_channel;
            DROP INDEX IF EXISTS idx_displayeditem_receiver;
            DROP INDEX IF EXISTS idx_displayeditem_finder;
            DROP INDEX IF EXISTS idx_displayeditem_game_item;
            DROP INDEX IF EXISTS idx_recapitems_tableid;
            DROP INDEX IF EXISTS idx_receiveraliases_gcu;
            DROP INDEX IF EXISTS idx_displayeditem_gci; ";
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

    public static async Task Migrate_5_0_6(CancellationToken ct = default)
    {
        Console.WriteLine("Migrating to DB version 5.0.6: Adding PortalAccessTable for web portal access tokens.");

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
            CREATE TABLE IF NOT EXISTS PortalAccessTable (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GuildId   TEXT NOT NULL,
                ChannelId TEXT NOT NULL,
                UserId    TEXT NOT NULL,
                Token     TEXT NOT NULL,
                UNIQUE (GuildId, ChannelId, UserId),
                UNIQUE (Token)
            );";
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

}
