using System.Text.RegularExpressions;
using Discord;

namespace ArchipelagoSphereTracker.Tracking.Notifications;

internal static partial class NotificationDetailsFormatting
{
    // Reserve the full custom-emoji length when paginating. The publisher replaces
    // this with the arrow belonging to the connected application before sending.
    internal const string DetailsMarker = "<:arrow_down_right:1565701680195182622>";
    internal const string FallbackMarker = "➥";
    internal const ulong TestArrowId = 1565718646989520957;
    internal const ulong RandophinaArrowId = 1565718735447269406;
    internal const ulong AstArrowId = 1565701680195182622;

    internal static TrackingDiscordNotification Prepare(TrackingDiscordNotification notification, string marker = DetailsMarker)
    {
        // Repair only generated detail lines, including batches frozen by older
        // versions. Escaped labels and unrelated user text are left untouched.
        var content = GluedItemEmoji().Replace(notification.Content, "**\n  " + marker + " ");
        content = DetailsLineEmoji().Replace(content, "${quote}${indent}" + marker + " ");
        content = ItemBullet().Replace(content, "${quote}• ");
        return content == notification.Content ? notification : notification with { Content = content };
    }

    [GeneratedRegex(@"(?m)^(?<quote>>>> )?(?<indent>[ \t]*)(?:<a?:arrow_down_right:\d+>|<::arrow_down_right::\d+>|:arrow_down_right:|↳|➥)[ \t]+(?=De \*\*|From \*\*|\*)")]
    private static partial Regex DetailsLineEmoji();

    [GeneratedRegex(@"(?<!\\)\*\*[ \t]*(?:<a?:arrow_down_right:\d+>|<::arrow_down_right::\d+>|:arrow_down_right:|↳|➥)[ \t]+(?=(?:De|From) \*\*)")]
    private static partial Regex GluedItemEmoji();

    [GeneratedRegex(@"(?m)^(?<quote>>>> )?- (?=\*\*)")]
    private static partial Regex ItemBullet();
}

internal sealed class ApplicationDetailsMarkerResolver(
    Func<CancellationToken, Task<IReadOnlyCollection<Emote>>> lookup, TimeProvider? timeProvider = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private DateTimeOffset _refreshAt = DateTimeOffset.MinValue;
    private string _marker = NotificationDetailsFormatting.FallbackMarker;

    internal async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_time.GetUtcNow() < _refreshAt) return _marker;
            Emote? arrow;
            try
            {
                // Only select from the connected application's own emojis.
                // An emoji from another bot must never be used as a fallback.
                arrow = (await lookup(cancellationToken)).FirstOrDefault(emote =>
                    emote.Name == "arrow_down_right" && emote.Id is
                        NotificationDetailsFormatting.TestArrowId or
                        NotificationDetailsFormatting.RandophinaArrowId or
                        NotificationDetailsFormatting.AstArrowId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error) when (error is Discord.Net.HttpException or HttpRequestException
                or TimeoutException or OperationCanceledException)
            {
                Console.WriteLine($"[Tracking] Application arrow lookup failed ({error.GetType().Name}); using ➥.");
                arrow = null;
            }
            _marker = arrow?.ToString() ?? NotificationDetailsFormatting.FallbackMarker;
            _refreshAt = _time.GetUtcNow().Add(arrow == null ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(15));
            Console.WriteLine(arrow == null
                ? "[Tracking] No configured arrow found in this bot application; using ➥."
                : $"[Tracking] Application arrow selected: {_marker}");
            return _marker;
        }
        finally { _gate.Release(); }
    }
}

