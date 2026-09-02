using System;
using System.Data.SQLite;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Xunit;

public class SensitiveDataProtectionTests
{
    [Fact]
    public void AesGcmEnvelope_RoundTripsWithRandomNonceAndPurposeBinding()
    {
        var key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var protector = new AesGcmDataProtector(key);

        var first = protector.Protect("private-room-token", SensitiveDataPurposes.Room);
        var second = protector.Protect("private-room-token", SensitiveDataPurposes.Room);

        Assert.StartsWith(AesGcmDataProtector.EnvelopePrefix, first, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
        Assert.DoesNotContain("private-room-token", first, StringComparison.Ordinal);
        Assert.Equal("private-room-token", protector.Unprotect(first, SensitiveDataPurposes.Room));
        Assert.Equal("legacy-plaintext", protector.Unprotect("legacy-plaintext", SensitiveDataPurposes.Room));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(first, SensitiveDataPurposes.Tracker));
    }

    [Fact]
    public void AesGcmEnvelope_RejectsWrongKeyAndMalformedPayload()
    {
        var first = new AesGcmDataProtector(new byte[32]);
        var second = new AesGcmDataProtector(Enumerable.Repeat((byte)1, 32).ToArray());
        var protectedValue = first.Protect("secret", SensitiveDataPurposes.Patch);

        Assert.ThrowsAny<CryptographicException>(() => second.Unprotect(protectedValue, SensitiveDataPurposes.Patch));
        Assert.ThrowsAny<CryptographicException>(() =>
            first.Unprotect(AesGcmDataProtector.EnvelopePrefix + "not-base64", SensitiveDataPurposes.Patch));
    }

    [Fact]
    public async Task Migration5011_EncryptsLegacyValuesAndIsIdempotent()
    {
        using var scope = new TestDatabaseScope();

        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ChannelsAndUrlsTable
                    (GuildId, ChannelId, BaseUrl, Room, Tracker, CheckFrequency, Silent, Port)
                VALUES
                    ('guild', 'channel', 'https://example.test', 'legacy-room', 'legacy-tracker', '5m', 0, '0');
                INSERT INTO UrlAndChannelPatchTable
                    (ChannelsAndUrlsTableId, Alias, GameName, Patch)
                VALUES
                    (last_insert_rowid(), 'Player', 'Game', 'https://example.test/patch/private');";
            await command.ExecuteNonQueryAsync();
        });

        await DBMigration_5.Migrate_5_0_11();
        await DBMigration_5.Migrate_5_0_11();

        await using (var connection = await Db.OpenReadAsync())
        {
            using var command = new SQLiteCommand(@"
                SELECT Room, Tracker,
                       (SELECT Patch FROM UrlAndChannelPatchTable LIMIT 1) AS Patch
                FROM ChannelsAndUrlsTable
                WHERE GuildId = 'guild' AND ChannelId = 'channel';", connection);
            using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(SensitiveDataProtector.IsProtected(reader["Room"]?.ToString()));
            Assert.True(SensitiveDataProtector.IsProtected(reader["Tracker"]?.ToString()));
            Assert.True(SensitiveDataProtector.IsProtected(reader["Patch"]?.ToString()));
        }

        var config = await ChannelsAndUrlsCommands.GetChannelConfigAsync("guild", "channel");
        Assert.Equal("legacy-room", config.room);
        Assert.Equal("legacy-tracker", config.tracker);
        Assert.Equal(
            "Game : https://example.test/patch/private",
            await ChannelsAndUrlsCommands.GetPatchAndGameNameForAlias("guild", "channel", "Player"));
        Assert.Equal(
            "channel",
            await ChannelsAndUrlsCommands.GetChannelIdForRoomAsync(
                "guild",
                "https://example.test/room/legacy-room",
                "legacy-room"));
    }

    [Fact]
    public async Task DataProtectionMetadata_PersistsOnlyEncryptedKeyCheck()
    {
        using var scope = new TestDatabaseScope();
        await SensitiveDataProtectionStore.EnsureReadyAsync();

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(
            "SELECT KeyCheck FROM DataProtectionMetadata WHERE Id = 1;",
            connection);
        var stored = (await command.ExecuteScalarAsync())?.ToString();

        Assert.True(SensitiveDataProtector.IsProtected(stored));
        Assert.DoesNotContain(SensitiveDataProtectionStore.KeyCheckPlaintext, stored ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DataProtectionMetadata_RejectsPlaintextKeyCheck()
    {
        using var scope = new TestDatabaseScope();
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO DataProtectionMetadata (Id, KeyCheck)
                VALUES (1, @KeyCheck);";
            command.Parameters.AddWithValue("@KeyCheck", SensitiveDataProtectionStore.KeyCheckPlaintext);
            await command.ExecuteNonQueryAsync();
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SensitiveDataProtectionStore.EnsureReadyAsync());
    }

    [Fact]
    public async Task Migration5011_RollsBackWhenExistingEnvelopeIsCorrupt()
    {
        using var scope = new TestDatabaseScope();
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ChannelsAndUrlsTable
                    (GuildId, ChannelId, BaseUrl, Room, Tracker, CheckFrequency, Silent, Port)
                VALUES
                    ('guild', 'channel', 'https://example.test', @Room, 'legacy-tracker', '5m', 0, '0');";
            command.Parameters.AddWithValue("@Room", AesGcmDataProtector.EnvelopePrefix + "not-base64");
            await command.ExecuteNonQueryAsync();
        });

        await Assert.ThrowsAnyAsync<CryptographicException>(() => DBMigration_5.Migrate_5_0_11());

        await using var connection = await Db.OpenReadAsync();
        using (var command = new SQLiteCommand(
                   "SELECT Tracker FROM ChannelsAndUrlsTable WHERE GuildId = 'guild';",
                   connection))
        {
            Assert.Equal("legacy-tracker", (await command.ExecuteScalarAsync())?.ToString());
        }
        using (var command = new SQLiteCommand(
                   "SELECT COUNT(*) FROM DataProtectionMetadata;",
                   connection))
        {
            Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        }
    }
}
