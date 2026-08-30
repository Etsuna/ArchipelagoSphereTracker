using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;
using Xunit;

public class PortalSecurityPersistenceTests
{
    [Fact]
    public async Task PreMigrationBackup_CreatesReadableSQLiteCopy()
    {
        using var scope = new TestDatabaseScope();
        var backupDirectory = Path.Combine(scope.BaseDirectory, "backups");

        var backupPath = await DBMigration.CreatePreMigrationBackupAsync("5.0.5", backupDirectory);

        Assert.True(File.Exists(backupPath));
        await using var connection = new SQLiteConnection($"Data Source={backupPath};Version=3;Read Only=True;");
        await connection.OpenAsync();
        using var command = new SQLiteCommand(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='PortalAccessTable';",
            connection);
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public void LegacyPortalCleanup_DeletesOnlyTokenDirectories()
    {
        var portalRoot = Path.Combine(Path.GetTempPath(), $"ast-portal-{Guid.NewGuid():N}");
        var legacyTokenFolder = Path.Combine(
            portalRoot,
            "123",
            "456",
            "0123456789abcdef0123456789abcdef");
        var preservedFolder = Path.Combine(portalRoot, "123", "456", "not-a-token");
        Directory.CreateDirectory(legacyTokenFolder);
        Directory.CreateDirectory(preservedFolder);
        File.WriteAllText(Path.Combine(legacyTokenFolder, "index.html"), "legacy");
        File.WriteAllText(Path.Combine(portalRoot, "commands.html"), "shared");

        try
        {
            Assert.Equal(1, WebPortalPages.DeleteLegacyUserPages(portalRoot));
            Assert.False(Directory.Exists(legacyTokenFolder));
            Assert.True(Directory.Exists(preservedFolder));
            Assert.True(File.Exists(Path.Combine(portalRoot, "commands.html")));
        }
        finally
        {
            if (Directory.Exists(portalRoot))
                Directory.Delete(portalRoot, recursive: true);
        }
    }

    [Fact]
    public async Task IssuingNewToken_RotatesPreviousTokenAndStoresOnlyHash()
    {
        using var scope = new TestDatabaseScope();

        var first = await PortalAccessCommands.IssuePortalTokenAsync("guild", "channel", "user");
        Assert.Equal("user", await PortalAccessCommands.GetUserIdByTokenAsync("guild", "channel", first));

        var second = await PortalAccessCommands.IssuePortalTokenAsync("guild", "channel", "user");

        Assert.NotEqual(first, second);
        Assert.Null(await PortalAccessCommands.GetUserIdByTokenAsync("guild", "channel", first));
        Assert.Equal("user", await PortalAccessCommands.GetUserIdByTokenAsync("guild", "channel", second));

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand("SELECT TokenHash FROM PortalAccessTable;", connection);
        var persisted = (await command.ExecuteScalarAsync())?.ToString();
        Assert.Equal(PortalAccessCommands.HashToken(second), persisted);
        Assert.DoesNotContain(second, persisted ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RevokedOrExpiredToken_IsRejected()
    {
        using var scope = new TestDatabaseScope();

        var revoked = await PortalAccessCommands.IssuePortalTokenAsync("guild", "channel", "user");
        await PortalAccessCommands.RevokePortalTokenAsync("guild", "channel", "user");
        Assert.Null(await PortalAccessCommands.GetUserIdByTokenAsync("guild", "channel", revoked));

        var expired = await PortalAccessCommands.IssuePortalTokenAsync("guild", "channel", "user");
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE PortalAccessTable SET ExpiresAtUtc = @Expired;";
            command.Parameters.AddWithValue("@Expired", PortalAccessCommands.FormatTimestamp(DateTimeOffset.UtcNow.AddMinutes(-1)));
            await command.ExecuteNonQueryAsync();
        });

        Assert.Null(await PortalAccessCommands.GetUserIdByTokenAsync("guild", "channel", expired));
    }

    [Fact]
    public async Task Migration506_HashesLegacyTokensAndIsIdempotent()
    {
        using var scope = new TestDatabaseScope();
        const string legacyToken = "0123456789abcdef0123456789abcdef";

        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                DROP TABLE PortalAccessTable;
                CREATE TABLE PortalAccessTable (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GuildId TEXT NOT NULL,
                    ChannelId TEXT NOT NULL,
                    UserId TEXT NOT NULL,
                    Token TEXT NOT NULL,
                    UNIQUE (GuildId, ChannelId, UserId),
                    UNIQUE (Token)
                );
                INSERT INTO PortalAccessTable (GuildId, ChannelId, UserId, Token)
                VALUES ('guild', 'channel', 'user', @Token);";
            command.Parameters.AddWithValue("@Token", legacyToken);
            await command.ExecuteNonQueryAsync();
        });

        await DBMigration_5.Migrate_5_0_6();
        await DBMigration_5.Migrate_5_0_6();

        Assert.Equal("user", await PortalAccessCommands.GetUserIdByTokenAsync("guild", "channel", legacyToken));

        await using var connection = await Db.OpenReadAsync();
        var columns = new List<string>();
        using (var command = new SQLiteCommand("PRAGMA table_info(PortalAccessTable);", connection))
        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                columns.Add(reader["name"]?.ToString() ?? string.Empty);
        }

        Assert.Contains("TokenHash", columns);
        Assert.DoesNotContain("Token", columns);
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("PortalAccessTable"));
    }

    [Fact]
    public async Task AuditLog_PersistsSafeStructuredFieldsAndCleansExpiredRows()
    {
        using var scope = new TestDatabaseScope();

        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO SecurityAuditLogTable
                    (OccurredAtUtc, CorrelationId, Source, ActorUserId, GuildId, ChannelId, Action, Outcome)
                VALUES
                    (@OccurredAtUtc, 'old', 'Web', 'user', 'guild', 'channel', 'RoomDelete', 'Succeeded');";
            command.Parameters.AddWithValue(
                "@OccurredAtUtc",
                PortalAccessCommands.FormatTimestamp(DateTimeOffset.UtcNow.AddDays(-Declare.AuditRetentionDays - 1)));
            await command.ExecuteNonQueryAsync();
        });

        await SecurityAuditLog.WriteAsync(
            "correlation",
            SecurityAuditSource.Discord,
            "user",
            "guild",
            "channel",
            SecurityAuditAction.RoomDelete,
            SecurityAuditOutcome.Succeeded);

        var entries = await SecurityAuditLog.GetRecentAsync("guild");
        var entry = Assert.Single(entries);
        Assert.Equal("correlation", entry.CorrelationId);
        Assert.Equal(SecurityAuditAction.RoomDelete, entry.Action);
        Assert.Equal(SecurityAuditOutcome.Succeeded, entry.Outcome);
        Assert.Equal(SecurityAuditSource.Discord, entry.Source);
    }
}
