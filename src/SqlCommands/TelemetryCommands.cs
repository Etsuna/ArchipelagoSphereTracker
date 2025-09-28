using System.Data.SQLite;

public static class TelemetryCommands
{
    public static async Task<bool> HasTelemetryBeenSentWithinAsync(int seconds)
    {
        try
        {
            await using var connection = await Db.OpenReadAsync();
            using var cmd = new SQLiteCommand(
                "SELECT Date FROM TelemetryTable LIMIT 1;", connection);

            var val = await cmd.ExecuteScalarAsync().ConfigureAwait(false) as string;
            if (string.IsNullOrEmpty(val)) return false;

            if (!DateTime.TryParse(val, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var last))
                return false;

            var elapsed = DateTime.UtcNow - last;
            return elapsed.TotalSeconds < seconds;
        }
        catch
        {
            return false;
        }
    }

    public static async Task MarkTelemetryAsSentAsync()
    {
        var nowIso = DateTime.UtcNow.ToString("o");
        try
        {
            await Db.WriteAsync(async conn =>
            {
                using (var create = new SQLiteCommand(
                    "CREATE TABLE IF NOT EXISTS TelemetryTable (Date TEXT NOT NULL);", conn))
                    await create.ExecuteNonQueryAsync().ConfigureAwait(false);

                using (var clear = new SQLiteCommand("DELETE FROM TelemetryTable;", conn))
                    await clear.ExecuteNonQueryAsync().ConfigureAwait(false);

                using (var insert = new SQLiteCommand(
                    "INSERT INTO TelemetryTable (Date) VALUES (@v);", conn))
                {
                    insert.Parameters.AddWithValue("@v", nowIso);
                    await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            });
        }
        catch
        {
            // silencieux
        }
    }
}
