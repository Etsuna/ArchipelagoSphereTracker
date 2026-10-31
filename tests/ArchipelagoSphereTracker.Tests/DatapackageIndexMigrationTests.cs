using System;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

public sealed class DatapackageIndexMigrationTests
{
    private const string ExtraIndexes = @"
        CREATE INDEX IF NOT EXISTS IX_DatapackageItems_GCD_Name
            ON DatapackageItems(GuildId,ChannelId,DatasetKey,Name);
        CREATE INDEX IF NOT EXISTS IX_DatapackageLocations_GCD_Name
            ON DatapackageLocations(GuildId,ChannelId,DatasetKey,Name);";

    [Theory]
    [InlineData("DatapackageItems")]
    [InlineData("DatapackageLocations")]
    public async Task Fresh_schema_keeps_name_uniqueness_and_indexed_lookup_without_a_duplicate_index(string table)
    {
        using var database = new TestDatabaseScope();
        Assert.Equal(0L, await ScalarAsync($"SELECT COUNT(*) FROM sqlite_master WHERE name='IX_{table}_GCD_Name';"));
        await ExecuteAsync($"INSERT INTO {table}(GuildId,ChannelId,DatasetKey,Id,Name) VALUES('g','c','data',1,'Name1');");
        await Assert.ThrowsAsync<SQLiteException>(() => ExecuteAsync(
            $"INSERT INTO {table}(GuildId,ChannelId,DatasetKey,Id,Name) VALUES('g','c','data',2,'Name1');"));
        await Assert.ThrowsAsync<SQLiteException>(() => ExecuteAsync(
            $"INSERT INTO {table}(GuildId,ChannelId,DatasetKey,Id,Name) VALUES('g','c','data',1,'Other name');"));
        await ExecuteAsync($@"INSERT INTO {table}(GuildId,ChannelId,DatasetKey,Id,Name) VALUES
            ('other','c','data',1,'Name1'),('g','other','data',1,'Name1'),('g','c','other',1,'Name1');");

        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"EXPLAIN QUERY PLAN SELECT Id FROM {table} WHERE GuildId='g' AND ChannelId='c' AND DatasetKey='data' AND Name='Name1';";
        using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var detail = reader["detail"].ToString()!;
        Assert.Contains("SEARCH", detail);
        Assert.Contains($"sqlite_autoindex_{table}_", detail);
        Assert.Equal(4, await TestDatabaseScope.CountRowsAsync(table));
    }

    [Theory]
    [InlineData("5.0.18")]
    [InlineData("5.0.19")]
    [InlineData("5.0.20")]
    public async Task Startup_migration_backs_up_old_indexes_and_preserves_data_constraints_and_groups(string version)
    {
        using var database = new TestDatabaseScope();
        using var backupDirectory = new BackupDirectoryScope(database.BaseDirectory);
        await SeedAsync();
        await ExecuteAsync(ExtraIndexes);
        await DBMigration.SetDbVersionAsync(version);
        var check = typeof(Program).GetMethod("CheckBdd", BindingFlags.Static | BindingFlags.NonPublic)!;
        await (Task)check.Invoke(null, null)!;
        Assert.Equal("5.0.21", await DBMigration.GetCurrentDbVersionAsync());
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name IN ('IX_DatapackageItems_GCD_Name','IX_DatapackageLocations_GCD_Name');"));
        Assert.Equal("Name1", await ScalarAsync("SELECT Name FROM DatapackageItems WHERE Id=1;"));
        Assert.Equal("Name1", await ScalarAsync("SELECT Name FROM DatapackageLocations WHERE Id=1;"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DatapackageItemGroups"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DatapackageLocationGroups"));
        Assert.Null(await ScalarAsync("PRAGMA foreign_key_check;"));

        var backup = Assert.Single(Directory.GetFiles(Declare.DatabaseBackupPath, $"AST-pre-{version}-*.db"));
        await using (var backupConnection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = backup, ReadOnly = true }.ToString()))
        {
            await backupConnection.OpenAsync();
            using var before = backupConnection.CreateCommand();
            before.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('IX_DatapackageItems_GCD_Name','IX_DatapackageLocations_GCD_Name');";
            Assert.Equal(2L, (long)(await before.ExecuteScalarAsync())!);
            before.CommandText = "SELECT Version FROM BddVersion WHERE Id=1;";
            Assert.Equal(version, await before.ExecuteScalarAsync());
        }

        // Re-running migration and normal startup must not recreate the duplicates.
        await DBMigration_5.Migrate_5_0_21();
        await DatabaseInitializer.InitializeDatabaseAsync();
        await DatabaseInitializer.InitializeDatabaseAsync();
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name IN ('IX_DatapackageItems_GCD_Name','IX_DatapackageLocations_GCD_Name');"));
        await Assert.ThrowsAsync<SQLiteException>(() => ExecuteAsync(
            "INSERT INTO DatapackageItems(GuildId,ChannelId,DatasetKey,Id,Name) VALUES('g','c','data',2,'Name1');"));
        await ExecuteAsync("DELETE FROM DatapackageItems WHERE GuildId='g' AND ChannelId='c' AND DatasetKey='data' AND Id=1;");
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("DatapackageItemGroups"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DatapackageLocationGroups"));
    }

    [Fact]
    public async Task Migration_retains_both_extra_indexes_when_an_equivalent_unique_constraint_is_missing()
    {
        using var database = new TestDatabaseScope();
        await ExecuteAsync(@"
            DROP TABLE DatapackageLocations;
            CREATE TABLE DatapackageLocations(
                GuildId TEXT NOT NULL,ChannelId TEXT NOT NULL,DatasetKey TEXT NOT NULL,Id INTEGER NOT NULL,Name TEXT NOT NULL,
                PRIMARY KEY(GuildId,ChannelId,DatasetKey,Id));" + ExtraIndexes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => DBMigration_5.Migrate_5_0_21());
        Assert.Equal(2L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name IN ('IX_DatapackageItems_GCD_Name','IX_DatapackageLocations_GCD_Name');"));
    }

    [Fact]
    public async Task Dropping_duplicate_indexes_releases_reusable_pages_without_deleting_datapackages()
    {
        using var database = new TestDatabaseScope();
        await ExecuteAsync(ExtraIndexes + @"
            WITH RECURSIVE numbers(n) AS (VALUES(1) UNION ALL SELECT n+1 FROM numbers WHERE n<5000)
            INSERT INTO DatapackageItems(GuildId,ChannelId,DatasetKey,Id,Name)
            SELECT 'g','c','data',n,printf('%0300d',n) FROM numbers;
            INSERT INTO DatapackageLocations SELECT * FROM DatapackageItems;");
        var before = (long)(await ScalarAsync("PRAGMA freelist_count;"))!;
        await DBMigration_5.Migrate_5_0_21();
        var after = (long)(await ScalarAsync("PRAGMA freelist_count;"))!;
        Assert.True(after > before);
        Assert.Equal(5000, await TestDatabaseScope.CountRowsAsync("DatapackageItems"));
        Assert.Equal(5000, await TestDatabaseScope.CountRowsAsync("DatapackageLocations"));
        Assert.Equal("ok", await ScalarAsync("PRAGMA quick_check;"));
    }

    private static Task SeedAsync() => ExecuteAsync(@"
        INSERT INTO DatapackageItems(GuildId,ChannelId,DatasetKey,Id,Name) VALUES('g','c','data',1,'Name1');
        INSERT INTO DatapackageLocations(GuildId,ChannelId,DatasetKey,Id,Name) VALUES('g','c','data',1,'Name1');
        INSERT INTO DatapackageItemGroups(GuildId,ChannelId,DatasetKey,GroupName,ItemId) VALUES('g','c','data','Group',1);
        INSERT INTO DatapackageLocationGroups(GuildId,ChannelId,DatasetKey,GroupName,LocationId) VALUES('g','c','data','Group',1);");

    private sealed class BackupDirectoryScope : IDisposable
    {
        private readonly string _original = Declare.DatabaseBackupPath;
        public BackupDirectoryScope(string directory) => Declare.DatabaseBackupPath = Path.Combine(directory, "backups");
        public void Dispose() => Declare.DatabaseBackupPath = _original;
    }

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
