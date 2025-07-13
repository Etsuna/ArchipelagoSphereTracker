using System.Text;
using System.Text.Json;

public static class Telemetry
{
    private static readonly HttpClient httpClient = new HttpClient();
    private const string EncodedUrl = "aHR0cHM6Ly9hcmNoaXBlbGFnb3NwaGVyZXRyYWNrZXIuYWx3YXlzZGF0YS5uZXQvdGVsZW1ldHJ5LnBocA==";

    private static string GetDecodedUrl()
    {
        var bytes = Convert.FromBase64String(EncodedUrl);
        return Encoding.UTF8.GetString(bytes);
    }

    public static async Task SendDailyTelemetryAsync(string programId)
    {
        try
        {
            if (await TelemetryCommands.HasTelemetryBeenSentTodayAsync())
                return;

            var (guildCount, channelCount) = await DatabaseCommands.GetDistinctGuildsAndChannelsCountAsync("ChannelsAndUrlsTable");

            var payload = new
            {
                id = programId,
                timestamp = DateTime.UtcNow.ToString("o"),
                guilds = guildCount,
                channels = channelCount
            };

            string json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = GetDecodedUrl();
            var response = await httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                await TelemetryCommands.MarkTelemetryAsSentAsync();
            }
        }
        catch
        {
            // silencieux pour ne pas déranger l'utilisateur
        }
    }
}
