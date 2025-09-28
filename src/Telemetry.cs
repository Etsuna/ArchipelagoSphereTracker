using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public static class Telemetry
{
    private static readonly HttpClient httpClient = new HttpClient();

    private const string DefaultEncodedUrl =
        "aHR0cHM6Ly9hcmNoaXBlbGFnb3NwaGVyZXRyYWNrZXIuYWx3YXlzZGF0YS5uZXQvdGVsZW1ldHJ5LnBocA==";

    private static string GetDecodedUrl()
    {
        var b64 = Environment.GetEnvironmentVariable("TELEMETRY_URL_BASE64") ?? DefaultEncodedUrl;
        var bytes = Convert.FromBase64String(b64);
        return Encoding.UTF8.GetString(bytes);
    }

    private static int GetRateLimitSeconds()
    {
        var v = Environment.GetEnvironmentVariable("RATE_LIMIT_SECONDS");
        return int.TryParse(v, out var s) && s > 0 ? s : 21600; 
    }

    private static string ComputeSignature(string body)
    {
        var secret = Environment.GetEnvironmentVariable("TELEMETRY_SECRET");
        if (string.IsNullOrEmpty(secret)) return null!;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(sig);
    }

    public static async Task SendTelemetryAsync(string programId, bool check = true)
    {
        if (!Declare.TelemetryEnabled) return;

        try
        {
            if (check && await TelemetryCommands.HasTelemetryBeenSentWithinAsync(GetRateLimitSeconds()))
                return;

            var (guildCount, channelCount) =
                await DatabaseCommands.GetDistinctGuildsAndChannelsCountAsync("ChannelsAndUrlsTable");

            var payload = new
            {
                id = programId,
                timestamp = DateTime.UtcNow.ToString("o"),
                guilds = guildCount,
                channels = channelCount,
                version = Declare.Version,
                astversion = Declare.BotVersion
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"ArchipelagoSphereTracker/{Declare.BotVersion}");

            var sig = ComputeSignature(json);
            if (!string.IsNullOrEmpty(sig))
                httpClient.DefaultRequestHeaders.Remove("X-Signature");
            if (!string.IsNullOrEmpty(sig))
                httpClient.DefaultRequestHeaders.Add("X-Signature", sig);

            var url = GetDecodedUrl();
            using var response = await httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
                await TelemetryCommands.MarkTelemetryAsSentAsync();
        }
        catch
        {
            // silencieux par design
        }
    }
}
