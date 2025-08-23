using System.Data.SQLite;

public static class TelemetryCommands
{
    // ==========================
    // 🎯 Telemetry Has Been Sent Today (READ)
    // ==========================
    public static async Task<bool> HasTelemetryBeenSentTodayAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        try
        {
            await using var connection = await Db.OpenReadAsync(ct);
            using var command = new SQLiteCommand(
                "SELECT 1 FROM TelemetryTable WHERE Date = @Date LIMIT 1;", connection);
            command.Parameters.AddWithValue("@Date", today);

            var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return result != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while checking telemetry: {ex.Message}");
            return false;
        }
    }

    // ==========================
    // 🎯 Mark Telemetry As Sent (WRITE)
    // ==========================
    public static async Task MarkTelemetryAsSentAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        try
        {
            await Db.WriteAsync(async conn =>
            {
                using (var deleteCmd = new SQLiteCommand("DELETE FROM TelemetryTable;", conn))
                    await deleteCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                using (var insertCmd = new SQLiteCommand(
                    "INSERT INTO TelemetryTable (Date) VALUES (@Date);", conn))
                {
                    insertCmd.Parameters.AddWithValue("@Date", today);
                    await insertCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while saving telemetry: {ex.Message}");
        }
    }
}
