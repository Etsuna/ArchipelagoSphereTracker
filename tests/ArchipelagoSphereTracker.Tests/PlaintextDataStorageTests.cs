using System;
using System.Data.SQLite;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

public class PlaintextDataStorageTests
{
    private const string KeyEnvironmentVariable = "AST_DATA_PROTECTION_KEY";

    [Fact]
    public async Task NewDatabase_DoesNotCreateDataProtectionMetadata()
    {
        using var scope = new TestDatabaseScope();

        await using var connection = await Db.OpenReadAsync();
        Assert.False(await TableExistsAsync(connection, "DataProtectionMetadata"));
        Assert.False(await TableExistsAsync(connection, "DataProtectionRecoveryMetadata"));
    }

    [Fact]
    public async Task Migration5012_DecryptsLegacyValuesAndRemovesKeyMetadata()
    {
        using var scope = new TestDatabaseScope();
        var key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var previousKey = Environment.GetEnvironmentVariable(KeyEnvironmentVariable);
        Environment.SetEnvironmentVariable(KeyEnvironmentVariable, Convert.ToBase64String(key));

        try
        {
            await SeedLegacyEncryptedDatabaseAsync(key);

            await DBMigration_5.Migrate_5_0_12();

            await using var connection = await Db.OpenReadAsync();
            using (var command = new SQLiteCommand(@"
                       SELECT Room, Tracker,
                              (SELECT Patch FROM UrlAndChannelPatchTable LIMIT 1)
                       FROM ChannelsAndUrlsTable
                       WHERE GuildId = 'guild';", connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                Assert.True(await reader.ReadAsync());
                Assert.Equal("legacy-room", reader.GetString(0));
                Assert.Equal("legacy-tracker", reader.GetString(1));
                Assert.Equal("https://example.test/patch", reader.GetString(2));
            }

            Assert.False(await TableExistsAsync(connection, "DataProtectionMetadata"));
            Assert.False(await TableExistsAsync(connection, "DataProtectionRecoveryMetadata"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(KeyEnvironmentVariable, previousKey);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public async Task Migration5012_RollsBackWhenTheLegacyKeyIsWrong()
    {
        using var scope = new TestDatabaseScope();
        var key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var wrongKey = Enumerable.Repeat((byte)42, 32).ToArray();
        var originalRoom = Encrypt("legacy-room", "ChannelsAndUrlsTable.Room", key);
        var previousKey = Environment.GetEnvironmentVariable(KeyEnvironmentVariable);

        try
        {
            await SeedLegacyEncryptedDatabaseAsync(key, originalRoom);
            Environment.SetEnvironmentVariable(KeyEnvironmentVariable, Convert.ToBase64String(wrongKey));

            await Assert.ThrowsAnyAsync<CryptographicException>(() =>
                DBMigration_5.Migrate_5_0_12());

            await using var connection = await Db.OpenReadAsync();
            using (var command = new SQLiteCommand(
                       "SELECT Room FROM ChannelsAndUrlsTable WHERE GuildId = 'guild';",
                       connection))
            {
                Assert.Equal(originalRoom, (await command.ExecuteScalarAsync())?.ToString());
            }
            Assert.True(await TableExistsAsync(connection, "DataProtectionMetadata"));
            Assert.True(await TableExistsAsync(connection, "DataProtectionRecoveryMetadata"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(KeyEnvironmentVariable, previousKey);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(wrongKey);
        }
    }

    [Fact]
    public async Task Migration5012_AcceptsPlaintextWithoutAnyKey()
    {
        using var scope = new TestDatabaseScope();
        var previousKey = Environment.GetEnvironmentVariable(KeyEnvironmentVariable);
        Environment.SetEnvironmentVariable(KeyEnvironmentVariable, null);

        try
        {
            await Db.WriteAsync(async connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE DataProtectionMetadata (
                        Id INTEGER PRIMARY KEY,
                        KeyCheck TEXT NOT NULL
                    );
                    INSERT INTO ChannelsAndUrlsTable
                        (GuildId, ChannelId, BaseUrl, Room, Tracker, CheckFrequency, Silent, Port)
                    VALUES
                        ('guild', 'channel', 'https://example.test', 'plain-room', 'plain-tracker', '5m', 0, '0');";
                await command.ExecuteNonQueryAsync();
            });

            await DBMigration_5.Migrate_5_0_12();

            await using var connection = await Db.OpenReadAsync();
            using var command = new SQLiteCommand(
                "SELECT Room, Tracker FROM ChannelsAndUrlsTable WHERE GuildId = 'guild';",
                connection);
            using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("plain-room", reader.GetString(0));
            Assert.Equal("plain-tracker", reader.GetString(1));
            Assert.False(await TableExistsAsync(connection, "DataProtectionMetadata"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(KeyEnvironmentVariable, previousKey);
        }
    }

    private static async Task SeedLegacyEncryptedDatabaseAsync(byte[] key, string? room = null)
    {
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE DataProtectionMetadata (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    KeyCheck TEXT NOT NULL
                );
                CREATE TABLE DataProtectionRecoveryMetadata (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    Algorithm TEXT NOT NULL,
                    PublicKeyFingerprint TEXT NOT NULL,
                    WrappedDataProtectionKey TEXT NOT NULL
                );
                INSERT INTO DataProtectionMetadata (Id, KeyCheck) VALUES (1, @KeyCheck);
                INSERT INTO DataProtectionRecoveryMetadata
                    (Id, Algorithm, PublicKeyFingerprint, WrappedDataProtectionKey)
                VALUES (1, 'RSA-OAEP-SHA256', 'fingerprint', 'wrapped');
                INSERT INTO ChannelsAndUrlsTable
                    (GuildId, ChannelId, BaseUrl, Room, Tracker, CheckFrequency, Silent, Port)
                VALUES
                    ('guild', 'channel', 'https://example.test', @Room, @Tracker, '5m', 0, '0');
                INSERT INTO UrlAndChannelPatchTable
                    (ChannelsAndUrlsTableId, Alias, GameName, Patch)
                VALUES
                    (last_insert_rowid(), 'Player', 'Game', @Patch);";
            command.Parameters.AddWithValue(
                "@KeyCheck",
                Encrypt("AST-DATA-PROTECTION-V1", "DataProtectionMetadata.KeyCheck", key));
            command.Parameters.AddWithValue(
                "@Room",
                room ?? Encrypt("legacy-room", "ChannelsAndUrlsTable.Room", key));
            command.Parameters.AddWithValue(
                "@Tracker",
                Encrypt("legacy-tracker", "ChannelsAndUrlsTable.Tracker", key));
            command.Parameters.AddWithValue(
                "@Patch",
                Encrypt("https://example.test/patch", "UrlAndChannelPatchTable.Patch", key));
            await command.ExecuteNonQueryAsync();
        });
    }

    private static string Encrypt(string value, string purpose, byte[] key)
    {
        var plaintext = Encoding.UTF8.GetBytes(value);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, 16))
            aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(purpose));

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);
        return "astenc:v1:" + Convert.ToBase64String(payload);
    }

    private static async Task<bool> TableExistsAsync(SQLiteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = @TableName;";
        command.Parameters.AddWithValue("@TableName", tableName);
        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1;
    }
}
