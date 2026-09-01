using System.Data.SQLite;

public static class SensitiveDataProtectionStore
{
    public const string KeyCheckPlaintext = "AST-DATA-PROTECTION-V1";

    public static async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        _ = await Db.WriteAsync(
            async connection =>
            {
                await EnsureReadyAsync(connection, transaction: null, cancellationToken).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task EnsureReadyAsync(
        SQLiteConnection connection,
        SQLiteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        using (var create = connection.CreateCommand())
        {
            create.Transaction = transaction;
            create.CommandText = @"
                CREATE TABLE IF NOT EXISTS DataProtectionMetadata (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    KeyCheck TEXT NOT NULL
                );";
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        string? stored;
        using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT KeyCheck FROM DataProtectionMetadata WHERE Id = 1;";
            stored = (await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))?.ToString();
        }

        if (string.IsNullOrEmpty(stored))
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT INTO DataProtectionMetadata (Id, KeyCheck)
                VALUES (1, @KeyCheck);";
            insert.Parameters.AddWithValue(
                "@KeyCheck",
                SensitiveDataProtector.Protect(KeyCheckPlaintext, SensitiveDataPurposes.KeyCheck));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!SensitiveDataProtector.IsProtected(stored))
            throw new InvalidOperationException("The AST data-protection key check is invalid.");

        var plaintext = SensitiveDataProtector.Unprotect(stored, SensitiveDataPurposes.KeyCheck);
        if (!string.Equals(plaintext, KeyCheckPlaintext, StringComparison.Ordinal))
            throw new InvalidOperationException("The configured AST data-protection key does not match this database.");
    }
}
