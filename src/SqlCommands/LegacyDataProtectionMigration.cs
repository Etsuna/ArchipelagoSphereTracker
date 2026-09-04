using System.Data.SQLite;
using System.Security.Cryptography;
using System.Text;

internal sealed record LegacyDataProtectionMigrationResult(int ChannelCount, int PatchCount);

internal static class LegacyDataProtectionMigration
{
    private const string EnvelopePrefix = "astenc:v1:";
    private const string KeyEnvironmentVariable = "AST_DATA_PROTECTION_KEY";
    private const string LocalKeyFileName = "AST.data-protection.key";
    private const string RoomPurpose = "ChannelsAndUrlsTable.Room";
    private const string TrackerPurpose = "ChannelsAndUrlsTable.Tracker";
    private const string PatchPurpose = "UrlAndChannelPatchTable.Patch";
    private const string KeyCheckPurpose = "DataProtectionMetadata.KeyCheck";
    private const string KeyCheckPlaintext = "AST-DATA-PROTECTION-V1";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static async Task<LegacyDataProtectionMigrationResult> DecryptAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        CancellationToken cancellationToken)
    {
        using (var secureDelete = connection.CreateCommand())
        {
            secureDelete.Transaction = transaction;
            secureDelete.CommandText = "PRAGMA secure_delete=ON;";
            await secureDelete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var hasEncryptedValues = await HasEncryptedValuesAsync(
            connection,
            transaction,
            cancellationToken).ConfigureAwait(false);

        if (!hasEncryptedValues)
        {
            await DropMetadataAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            return new LegacyDataProtectionMigrationResult(0, 0);
        }

        var key = LoadExistingKey();
        try
        {
            await VerifyKeyCheckAsync(connection, transaction, key, cancellationToken).ConfigureAwait(false);
            var channelCount = await DecryptChannelsAsync(
                connection,
                transaction,
                key,
                cancellationToken).ConfigureAwait(false);
            var patchCount = await DecryptPatchesAsync(
                connection,
                transaction,
                key,
                cancellationToken).ConfigureAwait(false);
            await DropMetadataAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            return new LegacyDataProtectionMigrationResult(channelCount, patchCount);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static async Task<bool> HasEncryptedValuesAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            SELECT CASE WHEN
                EXISTS (
                    SELECT 1 FROM ChannelsAndUrlsTable
                    WHERE Room LIKE 'astenc:v1:%' OR Tracker LIKE 'astenc:v1:%'
                )
                OR EXISTS (
                    SELECT 1 FROM UrlAndChannelPatchTable
                    WHERE Patch LIKE 'astenc:v1:%'
                )
            THEN 1 ELSE 0 END;";
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) == 1;
    }

    private static async Task VerifyKeyCheckAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        byte[] key,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(
                connection,
                transaction,
                "DataProtectionMetadata",
                cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                "Encrypted values were found, but the legacy data-protection metadata is missing.");
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT KeyCheck FROM DataProtectionMetadata WHERE Id = 1;";
        var stored = (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))?.ToString();
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(EnvelopePrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("The legacy data-protection key check is missing or invalid.");

        var plaintext = Decrypt(stored, KeyCheckPurpose, key);
        if (!string.Equals(plaintext, KeyCheckPlaintext, StringComparison.Ordinal))
            throw new InvalidOperationException("The legacy AST data-protection key does not match this database.");
    }

    private static async Task<int> DecryptChannelsAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        byte[] key,
        CancellationToken cancellationToken)
    {
        var rows = new List<(long Id, string Room, string Tracker)>();
        using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT Id, Room, Tracker FROM ChannelsAndUrlsTable;";
            using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((
                    reader.GetInt64(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
            }
        }

        var changed = 0;
        foreach (var row in rows)
        {
            var room = Decrypt(row.Room, RoomPurpose, key);
            var tracker = Decrypt(row.Tracker, TrackerPurpose, key);
            if (string.Equals(room, row.Room, StringComparison.Ordinal) &&
                string.Equals(tracker, row.Tracker, StringComparison.Ordinal))
            {
                continue;
            }

            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE ChannelsAndUrlsTable
                SET Room = @Room, Tracker = @Tracker
                WHERE Id = @Id;";
            update.Parameters.AddWithValue("@Room", room);
            update.Parameters.AddWithValue("@Tracker", tracker);
            update.Parameters.AddWithValue("@Id", row.Id);
            if (await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                throw new InvalidOperationException($"Unable to decrypt ChannelsAndUrlsTable row {row.Id}.");
            changed++;
        }

        return changed;
    }

    private static async Task<int> DecryptPatchesAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        byte[] key,
        CancellationToken cancellationToken)
    {
        var rows = new List<(long Id, string Patch)>();
        using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT Id, Patch FROM UrlAndChannelPatchTable WHERE Patch IS NOT NULL;";
            using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                rows.Add((reader.GetInt64(0), reader.GetString(1)));
        }

        var changed = 0;
        foreach (var row in rows)
        {
            var patch = Decrypt(row.Patch, PatchPurpose, key);
            if (string.Equals(patch, row.Patch, StringComparison.Ordinal))
                continue;

            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE UrlAndChannelPatchTable SET Patch = @Patch WHERE Id = @Id;";
            update.Parameters.AddWithValue("@Patch", patch);
            update.Parameters.AddWithValue("@Id", row.Id);
            if (await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                throw new InvalidOperationException($"Unable to decrypt UrlAndChannelPatchTable row {row.Id}.");
            changed++;
        }

        return changed;
    }

    private static string Decrypt(string value, string purpose, byte[] key)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith(EnvelopePrefix, StringComparison.Ordinal))
            return value;

        byte[]? payload = null;
        byte[]? plaintext = null;
        try
        {
            payload = Convert.FromBase64String(value[EnvelopePrefix.Length..]);
            if (payload.Length < NonceSize + TagSize)
                throw new CryptographicException("Invalid legacy protected-data envelope.");

            plaintext = new byte[payload.Length - NonceSize - TagSize];
            using (var aes = new AesGcm(key, TagSize))
            {
                aes.Decrypt(
                    payload.AsSpan(0, NonceSize),
                    payload.AsSpan(NonceSize + TagSize),
                    payload.AsSpan(NonceSize, TagSize),
                    plaintext,
                    Encoding.UTF8.GetBytes(purpose));
            }

            return Encoding.UTF8.GetString(plaintext);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("Invalid legacy protected-data envelope.", exception);
        }
        finally
        {
            if (plaintext is not null)
                CryptographicOperations.ZeroMemory(plaintext);
            if (payload is not null)
                CryptographicOperations.ZeroMemory(payload);
        }
    }

    private static byte[] LoadExistingKey()
    {
        var configured = Environment.GetEnvironmentVariable(KeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
            return ParseKey(configured, KeyEnvironmentVariable);

        var databaseDirectory = Path.GetDirectoryName(Path.GetFullPath(Declare.DatabaseFile))
                                ?? Environment.CurrentDirectory;
        var keyPath = Path.Combine(databaseDirectory, LocalKeyFileName);
        if (!File.Exists(keyPath))
        {
            throw new InvalidOperationException(
                "This database still contains legacy encrypted values, but AST.data-protection.key is missing. " +
                "Restore the old key before upgrading to database version 5.0.12.");
        }

        return ParseKey(File.ReadAllText(keyPath), LocalKeyFileName);
    }

    private static byte[] ParseKey(string value, string source)
    {
        try
        {
            var key = Convert.FromBase64String(value.Trim());
            if (key.Length != 32)
            {
                CryptographicOperations.ZeroMemory(key);
                throw new InvalidOperationException($"{source} must decode to exactly 32 bytes.");
            }

            return key;
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException($"{source} must be a Base64-encoded 32-byte key.", exception);
        }
    }

    private static async Task DropMetadataAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            DROP TABLE IF EXISTS DataProtectionRecoveryMetadata;
            DROP TABLE IF EXISTS DataProtectionMetadata;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> TableExistsAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        string tableName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = @TableName;";
        command.Parameters.AddWithValue("@TableName", tableName);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) == 1;
    }
}
