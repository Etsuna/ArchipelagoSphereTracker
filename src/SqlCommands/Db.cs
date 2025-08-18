using System;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;

public static class Db
{
    // 🔒 Un seul writer à la fois
    public static readonly SemaphoreSlim WriteGate = new(1, 1);

    private static string BuildConnString() =>
        $"Data Source={Declare.DatabaseFile};Version=3;Pooling=True;Journal Mode=WAL;Synchronous=NORMAL;BusyTimeout=5000;";

    public static async Task<SQLiteConnection> OpenAsync(CancellationToken ct = default)
    {
        var conn = new SQLiteConnection(BuildConnString());
        await conn.OpenAsync(Declare.CT);

        // PRAGMA idempotents (au cas où le provider ignore une partie de la CS)
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA foreign_keys=ON;
PRAGMA temp_store=MEMORY;
PRAGMA busy_timeout=5000;";
            cmd.ExecuteNonQuery();
        }

        return conn;
    }

    // Retry ciblé BUSY/LOCKED (backoff: 200,400,800,1600ms)
    public static async Task WithLockedRetryAsync(Func<Task> action, CancellationToken ct)
    {
        const int max = 4;
        int attempt = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await action();
                return;
            }
            catch (SQLiteException ex) when (IsBusyOrLocked(ex) && ++attempt < max)
            {
                var delayMs = 200 * (int)Math.Pow(2, attempt - 1);
                await Task.Delay(delayMs, ct);
            }
        }
    }

    private static bool IsBusyOrLocked(SQLiteException ex) =>
        ex.ErrorCode == (int)SQLiteErrorCode.Busy
        || ex.ErrorCode == (int)SQLiteErrorCode.Locked
        || ex.ResultCode == SQLiteErrorCode.Busy
        || ex.ResultCode == SQLiteErrorCode.Locked;
}