using System.Globalization;
using System.Text.Json;

namespace ArchipelagoSphereTracker.Tracking.Scheduling;

public static class WebHostActivity
{
    // A cached snapshot or missing timer is not evidence that players stopped playing.
    public static bool? HasRecentActivity(string json, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json);
        var known = false;
        foreach (var name in new[] { "activity_timers", "connection_timers" })
        {
            if (!document.RootElement.TryGetProperty(name, out var timers) ||
                timers.ValueKind != JsonValueKind.Array) continue;
            foreach (var timer in timers.EnumerateArray())
            {
                if (timer.ValueKind != JsonValueKind.Object ||
                    !timer.TryGetProperty("time", out var value) || value.ValueKind != JsonValueKind.String ||
                    !DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var timestamp)) continue;
                known = true;
                if (timestamp >= now.AddMinutes(-15)) return true;
            }
        }
        return known ? false : null;
    }
}
