using ArchipelagoSphereTracker.Tracking.Scheduling;
using System.Globalization;
using System.Text;
using ArchipelagoSphereTracker.src.Resources;

public static class TrackingControlCommands
{
    public static async Task<string> ExecuteRoomAsync(
        string commandName,
        string guildId,
        string channelId,
        CancellationToken cancellationToken = default)
    {
        return commandName switch
        {
            "ast-room-health" => FormatRoomHealth(TrackingDataManager.GetRoomHealth(guildId, channelId)),
            "ast-pause" => FormatControlOutcome(
                commandName,
                await TrackingDataManager.PauseRoomAsync(guildId, channelId, cancellationToken).ConfigureAwait(false)),
            "ast-resume" => FormatControlOutcome(
                commandName,
                await TrackingDataManager.ResumeRoomAsync(guildId, channelId, cancellationToken).ConfigureAwait(false)),
            "ast-sync-now" => FormatControlOutcome(
                commandName,
                await TrackingDataManager.ForceSyncRoomAsync(guildId, channelId, cancellationToken).ConfigureAwait(false)),
            _ => Unsupported()
        };
    }

    public static string GetGuildHealth(string guildId)
    {
        var rooms = TrackingDataManager.GetGuildHealth(guildId);
        if (rooms == null)
            return SchedulerUnavailable();
        if (rooms.Count == 0)
            return Resource.TrackingNoTrackedRoomsOnThisServer;

        var paused = rooms.Count(room => room.IsPaused);
        var errors = rooms.Count(room => !room.IsPaused && room.ConsecutiveFailures > 0);
        var running = rooms.Count(room => room.IsRunning);
        var slowed = rooms.Count(room =>
            !room.IsPaused && room.ConsecutiveFailures == 0 && room.EffectiveInterval > room.ConfiguredInterval);
        var active = rooms.Count - paused - errors - slowed;

        return string.Format(Resource.TrackingASTHealthRoomSActiveSlowedPausedInError, rooms.Count, active, slowed, paused, errors, running);
    }

    public static string FormatRoomHealth(RoomHealthSnapshot? health)
    {
        if (health == null)
            return Resource.TrackingRoomNotFoundOrCentralSchedulerUnavailable;

        var now = DateTimeOffset.UtcNow;
        var status = health.IsPaused
            ? (Resource.TrackingManuallyPaused)
            : health.IsRunning
                ? (Resource.TrackingSyncInProgress)
                : health.ConsecutiveFailures > 0
                    ? (Resource.TrackingInError)
                    : health.EffectiveInterval > health.ConfiguredInterval
                        ? (Resource.TrackingAutomaticallySlowed)
                        : (Resource.TrackingActive);

        var builder = new StringBuilder();
        builder.AppendLine(string.Format(Resource.TrackingRoomTracking, status));
        builder.AppendLine(health.PollingMode == RoomPollingMode.Automatic
            ? string.Format(Resource.TrackingAutomaticPollingMinimumMaximum, Duration(health.EffectiveInterval), Duration(health.ConfiguredInterval), Duration(health.MaximumPollInterval))
            : string.Format(Resource.TrackingFixedPolling, Duration(health.ConfiguredInterval)));
        builder.AppendLine(string.Format(Resource.TrackingLastSuccessfulSync, Relative(health.LastSuccessAtUtc, now)));
        builder.AppendLine(string.Format(Resource.TrackingLastDetectedActivity, Relative(health.LastChangeAtUtc, now)));
        builder.AppendLine(string.Format(
            Resource.TrackingNextRefresh,
            health.IsPaused ? Resource.TrackingPaused : RelativeFuture(health.NextPollAtUtc, now)));
        builder.AppendLine(string.Format(Resource.TrackingConsecutiveFailures, health.ConsecutiveFailures, Failure(health.LastFailureKind)));
        builder.Append(string.Format(Resource.TrackingEstimatedFreshnessWebHostLatencyMs, Freshness(health.LastSuccessAtUtc, now), Math.Round(health.LastLatencyMilliseconds).ToString(CultureInfo.InvariantCulture)));

        if (health.BreakerOpenUntilUtc is { } breaker && breaker > now)
        {
            builder.AppendLine();
            builder.Append(string.Format(Resource.TrackingWebHostCircuitProtectionActiveUntilUTC, breaker));
        }

        return builder.ToString();
    }

    private static string FormatControlOutcome(string commandName, TrackingControlOutcome outcome)
    {
        return (commandName, outcome) switch
        {
            ("ast-pause", TrackingControlOutcome.Accepted) => Resource.TrackingPauseAccepted,
            ("ast-pause", TrackingControlOutcome.AlreadyPaused) => Resource.TrackingAlreadyPaused,
            ("ast-resume", TrackingControlOutcome.Accepted) => Resource.TrackingResumeAccepted,
            ("ast-resume", TrackingControlOutcome.AlreadyRunning) => Resource.TrackingAlreadyRunning,
            ("ast-sync-now", TrackingControlOutcome.Accepted) => Resource.TrackingPrioritySyncQueued,
            ("ast-sync-now", TrackingControlOutcome.Paused) => Resource.TrackingRoomPausedResumeFirst,
            ("ast-sync-now", TrackingControlOutcome.Busy) => Resource.TrackingSyncAlreadyRunning,
            ("ast-sync-now", TrackingControlOutcome.RateLimited) => Resource.TrackingForcedSyncRateLimited,
            (_, TrackingControlOutcome.NotFound) => Resource.TrackingRoomNotRegisteredInScheduler,
            (_, TrackingControlOutcome.Unavailable) => SchedulerUnavailable(),
            _ => Resource.TrackingCommandCouldNotBeApplied
        };
    }

    private static string Relative(DateTimeOffset? value, DateTimeOffset now)
        => value == null
            ? (Resource.TrackingNever)
            : string.Format(Resource.TrackingAgo, Duration(now - value.Value));

    private static string RelativeFuture(DateTimeOffset value, DateTimeOffset now)
        => value <= now
            ? (Resource.TrackingAsSoonAsPossible)
            : string.Format(Resource.TrackingIn, Duration(value - now));

    private static string Freshness(DateTimeOffset? lastSuccess, DateTimeOffset now)
    {
        if (lastSuccess == null)
            return Resource.TrackingUnknown;
        var age = now - lastSuccess.Value;
        if (age <= TimeSpan.FromMinutes(15)) return Resource.TrackingFresh;
        if (age <= TimeSpan.FromHours(1)) return Resource.TrackingAging;
        return Resource.TrackingStale;
    }

    private static string Duration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        if (duration.TotalDays >= 1) return $"{Math.Floor(duration.TotalDays):0} d";
        if (duration.TotalHours >= 1) return $"{Math.Floor(duration.TotalHours):0} h {duration.Minutes:0} min";
        if (duration.TotalMinutes >= 1) return $"{Math.Floor(duration.TotalMinutes):0} min";
        return $"{Math.Max(0, Math.Round(duration.TotalSeconds)):0} s";
    }

    private static string Failure(PollFailureKind failure)
        => failure == PollFailureKind.None ? (Resource.TrackingNone) : failure.ToString();

    private static string SchedulerUnavailable()
        => Resource.TrackingTheCentralSchedulerIsUnavailableStillStartingOrLegacy;

    private static string Unsupported()
        => Resource.TrackingUnknownTrackingCommand;

}
