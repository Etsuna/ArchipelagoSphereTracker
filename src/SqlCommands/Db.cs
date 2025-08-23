using System.Data.SQLite;

public static class Db
{
    public static readonly SemaphoreSlim WriteGate = new(1, 1);

    private static string Base =>
        $"Data Source={Declare.DatabaseFile};Version=3;Pooling=True;Journal Mode=WAL;Synchronous=NORMAL;BusyTimeout=5000;";

    private static string ReadCS => Base + "Read Only=True;";
    private static string WriteCS => Base;

    public static async Task<SQLiteConnection> OpenReadAsync()
    {
        var conn = new SQLiteConnection(ReadCS);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"PRAGMA foreign_keys=ON; PRAGMA temp_store=MEMORY; PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    public static async Task<SQLiteConnection> OpenWriteAsync()
    {
        var conn = new SQLiteConnection(WriteCS);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON; PRAGMA temp_store=MEMORY; PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    // Voie d’écriture sérialisée avec BEGIN IMMEDIATE
    public static async Task WriteAsync(Func<SQLiteConnection, Task> work)
    {
        await WriteGate.WaitAsync();
        try
        {
            await using var conn = await OpenWriteAsync();
            using (var begin = conn.CreateCommand()) { begin.CommandText = "BEGIN IMMEDIATE;"; begin.ExecuteNonQuery(); }

            try
            {
                await work(conn);
                using var commit = conn.CreateCommand(); commit.CommandText = "COMMIT;"; commit.ExecuteNonQuery();
            }
            catch
            {
                using var rb = conn.CreateCommand(); rb.CommandText = "ROLLBACK;"; rb.ExecuteNonQuery();
                throw;
            }
        }
        finally { WriteGate.Release(); }
    }
}
