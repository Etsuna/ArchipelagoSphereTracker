using System.Data.SQLite;

public static class TelemetryCommands
{
    // ==========================
    // 🎯 Telemetry Has Been Sent Today
    // ==========================
    public static async Task<bool> HasTelemetryBeenSentTodayAsync(CancellationToken ct = default)
    {
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        try
        {
            using var connection = await Db.OpenAsync(Declare.CT);

            using (var command = new SQLiteCommand("SELECT 1 FROM TelemetryTable WHERE Date = @Date LIMIT 1", connection))
            {
                command.Parameters.AddWithValue("@Date", today);

                var result = await command.ExecuteScalarAsync();
                return result != null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while checking telemetry: {ex.Message}");
            return false;
        }
    }

    // ==========================
    // 🎯 Mark Telemetry As Sent
    // ==========================
    public static async Task MarkTelemetryAsSentAsync(CancellationToken ct = default)
    {
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        try
        {
            using var connection = await Db.OpenAsync(Declare.CT);

            using (var deleteCmd = new SQLiteCommand("DELETE FROM TelemetryTable", connection))
            {
                await deleteCmd.ExecuteNonQueryAsync();
            }

            using (var insertCmd = new SQLiteCommand("INSERT INTO TelemetryTable (Date) VALUES (@Date)", connection))
            {
                insertCmd.Parameters.AddWithValue("@Date", today);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while saving telemetry: {ex.Message}");
        }
    }
}
