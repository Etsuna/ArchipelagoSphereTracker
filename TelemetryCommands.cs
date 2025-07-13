using System.Data.SQLite;

public static class TelemetryCommands
{
    // ==========================
    // 🎯 Telemetry Has Been Sent Today
    // ==========================
    public static async Task<bool> HasTelemetryBeenSentTodayAsync()
    {
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var command = new SQLiteCommand("SELECT 1 FROM TelemetryTable WHERE Date = @Date LIMIT 1", connection))
                {
                    command.Parameters.AddWithValue("@Date", today);

                    var result = await command.ExecuteScalarAsync();
                    return result != null;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la vérification télémétrie : {ex.Message}");
            return false;
        }
    }

    // ==========================
    // 🎯 Mark Telemetry As Sent
    // ==========================
    public static async Task MarkTelemetryAsSentAsync()
    {
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var command = new SQLiteCommand("INSERT OR IGNORE INTO TelemetryTable (Date) VALUES (@Date)", connection))
                {
                    command.Parameters.AddWithValue("@Date", today);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la sauvegarde télémétrie : {ex.Message}");
        }
    }
}
