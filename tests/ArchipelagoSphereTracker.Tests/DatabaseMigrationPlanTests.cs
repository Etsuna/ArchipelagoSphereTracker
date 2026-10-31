using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

public sealed class DatabaseMigrationPlanTests
{
    public static IEnumerable<object[]> SupportedVersions()
        => Enumerable.Range(0, 22).Select(patch => new object[] { patch });

    [Theory]
    [MemberData(nameof(SupportedVersions))]
    public void Every_supported_v2_version_runs_only_its_successors_in_numeric_order(int currentPatch)
    {
        var pending = DatabaseMigrations.GetPending($"5.0.{currentPatch}", "5.0.21");
        Assert.Equal(Enumerable.Range(currentPatch + 1, 21 - currentPatch).Select(patch => new Version(5, 0, patch)),
            pending.Select(step => step.Version));
        // Also verify registration points at each real migration, not just its label.
        Assert.All(pending, step => Assert.Equal($"Migrate_5_0_{step.Version.Build}", step.Apply.Method.Name));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("4.0.0")]
    [InlineData("4.9.12")]
    public void Unversioned_and_pre_v2_databases_start_with_the_legacy_conversion(string current)
    {
        var pending = DatabaseMigrations.GetPending(current, "5.0.21");
        Assert.Equal("Migrate_4_to_5Async", pending[0].Apply.Method.Name);
        Assert.Equal(Enumerable.Range(0, 22).Select(patch => new Version(5, 0, patch)), pending.Select(step => step.Version));
    }

    [Theory]
    [InlineData("5.0.22")]
    [InlineData("5.1.0")]
    [InlineData("6.0.0")]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("5.0")]
    [InlineData("5.0.21.1")]
    public void Unsupported_or_newer_versions_never_fall_back_to_v1_conversion(string current)
        => Assert.Throws<InvalidOperationException>(() => DatabaseMigrations.GetPending(current, "5.0.21"));

    [Fact]
    public void Target_version_must_be_registered_and_cannot_skip_later_steps()
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseMigrations.GetPending("5.0.20", "5.0.22"));
        var intermediate = Assert.Single(DatabaseMigrations.GetPending("5.0.19", "5.0.20"));
        Assert.Equal(new Version(5, 0, 20), intermediate.Version);
        Assert.Equal(new Version(Declare.BddVersion), DatabaseMigrations.GetPending("5.0.0", Declare.BddVersion)[^1].Version);
    }

    [Theory]
    [InlineData("5.0.21")]
    [InlineData("5.0.22")]
    [InlineData("unknown")]
    public async Task Current_or_unsupported_database_does_not_create_a_backup_or_modify_its_schema(string version)
    {
        using var database = new TestDatabaseScope();
        using var backups = new BackupDirectoryScope(database.BaseDirectory);
        await PrepareAsync(version);
        if (version == "5.0.21") await CheckAsync();
        else await Assert.ThrowsAsync<InvalidOperationException>(CheckAsync);
        Assert.False(Directory.Exists(Declare.DatabaseBackupPath));
        Assert.Equal(version, await DBMigration.GetCurrentDbVersionAsync());
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='IX_DatapackageItems_GCD_Name';"));
        Assert.Equal("Sword", await ScalarAsync("SELECT Name FROM DatapackageItems;"));
    }

    [Fact]
    public async Task A_failed_migration_keeps_the_original_version_and_backup()
    {
        using var database = new TestDatabaseScope();
        using var backups = new BackupDirectoryScope(database.BaseDirectory);
        await PrepareAsync("5.0.20");
        await ExecuteAsync(@"
            DROP TABLE DatapackageLocations;
            CREATE TABLE DatapackageLocations(
                GuildId TEXT,ChannelId TEXT,DatasetKey TEXT,Id INTEGER,Name TEXT);");
        await Assert.ThrowsAsync<InvalidOperationException>(CheckAsync);
        Assert.Equal("5.0.20", await DBMigration.GetCurrentDbVersionAsync());
        Assert.Single(Directory.GetFiles(Declare.DatabaseBackupPath, "AST-pre-5.0.20-*.db"));
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='IX_DatapackageItems_GCD_Name';"));
        Assert.Equal("Sword", await ScalarAsync("SELECT Name FROM DatapackageItems;"));
    }

    [Fact]
    public async Task A_failed_backup_stops_before_any_migration_or_version_update()
    {
        using var database = new TestDatabaseScope();
        using var backups = new BackupDirectoryScope(database.BaseDirectory);
        await PrepareAsync("5.0.20");
        var blocker = Path.Combine(database.BaseDirectory, "not-a-directory");
        await File.WriteAllTextAsync(blocker, "test");
        Declare.DatabaseBackupPath = Path.Combine(blocker, "backups");
        await Assert.ThrowsAnyAsync<IOException>(CheckAsync);
        Assert.Equal("5.0.20", await DBMigration.GetCurrentDbVersionAsync());
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='IX_DatapackageItems_GCD_Name';"));
        Assert.Equal("Sword", await ScalarAsync("SELECT Name FROM DatapackageItems;"));
    }

    private static async Task PrepareAsync(string version)
    {
        await DBMigration.SetDbVersionAsync(version);
        await ExecuteAsync(@"
            CREATE INDEX IX_DatapackageItems_GCD_Name ON DatapackageItems(GuildId,ChannelId,DatasetKey,Name);
            INSERT INTO DatapackageItems(GuildId,ChannelId,DatasetKey,Id,Name) VALUES('g','c','data',1,'Sword');");
    }

    private static Task CheckAsync()
        => (Task)typeof(Program).GetMethod("CheckBdd", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;

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
