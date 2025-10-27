using ArchipelagoSphereTracker.src.Resources;
using Discord;

public static class UpdateReminder
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
    private static CancellationTokenSource? _cts;

    private static class UpdateAlertsCommands
    {
        public static Task<(string? LatestTag, DateTimeOffset? LastSentUtc)?> GetAsync(string guild, string channel)
            => CheckUpdateCommands.GetUpdateAlertAsync(guild, channel);
        public static Task UpsertAsync(string guild, string channel, string latestTag, DateTimeOffset lastSentUtc)
            => CheckUpdateCommands.UpsertUpdateAlertAsync(guild, channel, latestTag, lastSentUtc);
    }

    public static void Start(string owner = "Etsuna", string repo = "ArchipelagoSphereTracker")
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                if (Declare.Client.ConnectionState != ConnectionState.Connected)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                    continue;
                }

                var guilds = (await DatabaseCommands.GetAllGuildsAsync("ChannelsAndUrlsTable")).Distinct().ToList();

                await Parallel.ForEachAsync(guilds, new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = token },
                    async (guild, ctG) =>
                    {
                        var channels = (await DatabaseCommands.GetAllChannelsAsync(guild, "ChannelsAndUrlsTable")).Distinct().ToList();
                        foreach (var channel in channels)
                            await MaybeNotifyDailyAsync(guild, channel, owner, repo, ctG);
                    });

                await Task.Delay(TimeSpan.FromHours(1), token);
            }
        }, token);
    }

    public static async Task MaybeNotifyDailyAsync(string guild, string channel, string owner, string repo, CancellationToken ct)
    {
        var (newer, current, latest, asset) = await CheckUpdate.TryGetLatestAsync(owner, repo, ct);
        if (!newer) return;

        var rec = await UpdateAlertsCommands.GetAsync(guild, channel);
        var todayLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Tz).Date;

        if (rec is { LatestTag: var lastTag, LastSentUtc: var last } &&
            string.Equals(lastTag, latest, StringComparison.Ordinal) &&
            TimeZoneInfo.ConvertTime(last.Value, Tz).Date == todayLocal)
            return;

        var msg = $"{Resource.UpdateAvailable} {current} → {latest}" +
                  (asset is null ? "" : $"\n{Resource.DownloadUpdate} {asset.browser_download_url}");

        ulong guildId = ulong.Parse(guild);
        await TrackingDataManager.RateLimitGuards.GetGuildSendGate(guildId).WaitAsync(ct);
        if(Declare.TelemetryName != "AST")
        {
            try
            {
                await BotCommands.SendMessageAsync(msg, channel);
            }
            finally
            {
                TrackingDataManager.RateLimitGuards.GetGuildSendGate(guildId).Release();
            }
        }

        await UpdateAlertsCommands.UpsertAsync(guild, channel, latest, DateTimeOffset.UtcNow);
        await Task.Delay(1100, ct);
    }
}
