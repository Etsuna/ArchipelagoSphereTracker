using ArchipelagoSphereTracker.Tracking.Scheduling;
using System.Globalization;
using System.Text;

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
            return IsFrench ? "Aucune room suivie sur ce serveur." : "No tracked rooms on this server.";

        var paused = rooms.Count(room => room.IsPaused);
        var errors = rooms.Count(room => !room.IsPaused && room.ConsecutiveFailures > 0);
        var running = rooms.Count(room => room.IsRunning);
        var slowed = rooms.Count(room =>
            !room.IsPaused && room.ConsecutiveFailures == 0 && room.EffectiveInterval > room.ConfiguredInterval);
        var active = rooms.Count - paused - errors - slowed;

        return IsFrench
            ? $"📊 Santé AST — {rooms.Count} room(s) : {active} active(s), {slowed} ralentie(s), {paused} suspendue(s), {errors} en erreur, {running} synchronisation(s) en cours."
            : $"📊 AST health — {rooms.Count} room(s): {active} active, {slowed} slowed, {paused} paused, {errors} in error, {running} sync(s) running.";
    }

    public static string FormatRoomHealth(RoomHealthSnapshot? health)
    {
        if (health == null)
            return IsFrench
                ? "Room introuvable ou ordonnanceur central indisponible."
                : "Room not found or central scheduler unavailable.";

        var now = DateTimeOffset.UtcNow;
        var status = health.IsPaused
            ? (IsFrench ? "suspendu manuellement" : "manually paused")
            : health.IsRunning
                ? (IsFrench ? "synchronisation en cours" : "sync in progress")
                : health.ConsecutiveFailures > 0
                    ? (IsFrench ? "en erreur" : "in error")
                    : health.EffectiveInterval > health.ConfiguredInterval
                        ? (IsFrench ? "ralenti automatiquement" : "automatically slowed")
                        : (IsFrench ? "actif" : "active");

        var builder = new StringBuilder();
        builder.AppendLine(IsFrench ? $"🩺 Suivi de la room : **{status}**" : $"🩺 Room tracking: **{status}**");
        builder.AppendLine(health.PollingMode == RoomPollingMode.Automatic
            ? IsFrench
                ? $"Polling automatique : {Duration(health.EffectiveInterval)} (minimum {Duration(health.ConfiguredInterval)}, maximum {Duration(health.MaximumPollInterval)})"
                : $"Automatic polling: {Duration(health.EffectiveInterval)} ({Duration(health.ConfiguredInterval)} minimum, {Duration(health.MaximumPollInterval)} maximum)"
            : IsFrench
                ? $"Polling fixe : {Duration(health.ConfiguredInterval)}"
                : $"Fixed polling: {Duration(health.ConfiguredInterval)}");
        builder.AppendLine(IsFrench
            ? $"Dernière synchronisation réussie : {Relative(health.LastSuccessAtUtc, now)}"
            : $"Last successful sync: {Relative(health.LastSuccessAtUtc, now)}");
        builder.AppendLine(IsFrench
            ? $"Dernière activité détectée : {Relative(health.LastChangeAtUtc, now)}"
            : $"Last detected activity: {Relative(health.LastChangeAtUtc, now)}");
        builder.AppendLine(IsFrench
            ? $"Prochain rafraîchissement : {(health.IsPaused ? "suspendu" : RelativeFuture(health.NextPollAtUtc, now))}"
            : $"Next refresh: {(health.IsPaused ? "paused" : RelativeFuture(health.NextPollAtUtc, now))}");
        builder.AppendLine(IsFrench
            ? $"Échecs consécutifs : {health.ConsecutiveFailures} ({Failure(health.LastFailureKind)})"
            : $"Consecutive failures: {health.ConsecutiveFailures} ({Failure(health.LastFailureKind)})");
        builder.Append(IsFrench
            ? $"Fraîcheur estimée : {Freshness(health.LastSuccessAtUtc, now)} · latence WebHost : {Math.Round(health.LastLatencyMilliseconds).ToString(CultureInfo.InvariantCulture)} ms"
            : $"Estimated freshness: {Freshness(health.LastSuccessAtUtc, now)} · WebHost latency: {Math.Round(health.LastLatencyMilliseconds).ToString(CultureInfo.InvariantCulture)} ms");

        if (health.BreakerOpenUntilUtc is { } breaker && breaker > now)
        {
            builder.AppendLine();
            builder.Append(IsFrench
                ? $"Protection WebHost active jusqu’à {breaker:yyyy-MM-dd HH:mm:ss} UTC."
                : $"WebHost circuit protection active until {breaker:yyyy-MM-dd HH:mm:ss} UTC.");
        }

        return builder.ToString();
    }

    private static string FormatControlOutcome(string commandName, TrackingControlOutcome outcome)
    {
        if (IsFrench)
        {
            return (commandName, outcome) switch
            {
                ("ast-pause", TrackingControlOutcome.Accepted) => "⏸️ Suivi suspendu. Une synchronisation déjà en cours peut se terminer, mais aucun nouveau rafraîchissement ne sera lancé.",
                ("ast-pause", TrackingControlOutcome.AlreadyPaused) => "Le suivi de cette room est déjà suspendu.",
                ("ast-resume", TrackingControlOutcome.Accepted) => "▶️ Suivi repris. Une synchronisation a été placée en tête de file.",
                ("ast-resume", TrackingControlOutcome.AlreadyRunning) => "Le suivi de cette room est déjà actif.",
                ("ast-sync-now", TrackingControlOutcome.Accepted) => "🔄 Synchronisation prioritaire mise en file.",
                ("ast-sync-now", TrackingControlOutcome.Paused) => "La room est suspendue. Utilisez d’abord `/ast-resume`.",
                ("ast-sync-now", TrackingControlOutcome.Busy) => "Une synchronisation est déjà en cours pour cette room.",
                ("ast-sync-now", TrackingControlOutcome.RateLimited) => "Synchronisation forcée limitée à une fois toutes les 30 secondes. Réessayez plus tard.",
                (_, TrackingControlOutcome.NotFound) => "Cette room n’est pas enregistrée dans l’ordonnanceur.",
                (_, TrackingControlOutcome.Unavailable) => SchedulerUnavailable(),
                _ => "La commande de suivi n’a pas pu être appliquée."
            };
        }

        return (commandName, outcome) switch
        {
            ("ast-pause", TrackingControlOutcome.Accepted) => "⏸️ Tracking paused. A sync already in progress may finish, but no new refresh will start.",
            ("ast-pause", TrackingControlOutcome.AlreadyPaused) => "Tracking for this room is already paused.",
            ("ast-resume", TrackingControlOutcome.Accepted) => "▶️ Tracking resumed. A sync was placed at the front of the queue.",
            ("ast-resume", TrackingControlOutcome.AlreadyRunning) => "Tracking for this room is already active.",
            ("ast-sync-now", TrackingControlOutcome.Accepted) => "🔄 Priority sync queued.",
            ("ast-sync-now", TrackingControlOutcome.Paused) => "The room is paused. Use `/ast-resume` first.",
            ("ast-sync-now", TrackingControlOutcome.Busy) => "A sync is already running for this room.",
            ("ast-sync-now", TrackingControlOutcome.RateLimited) => "Forced sync is limited to once every 30 seconds. Try again later.",
            (_, TrackingControlOutcome.NotFound) => "This room is not registered in the scheduler.",
            (_, TrackingControlOutcome.Unavailable) => SchedulerUnavailable(),
            _ => "The tracking command could not be applied."
        };
    }

    private static string Relative(DateTimeOffset? value, DateTimeOffset now)
        => value == null
            ? (IsFrench ? "jamais" : "never")
            : IsFrench ? $"il y a {Duration(now - value.Value)}" : $"{Duration(now - value.Value)} ago";

    private static string RelativeFuture(DateTimeOffset value, DateTimeOffset now)
        => value <= now
            ? (IsFrench ? "dès que possible" : "as soon as possible")
            : IsFrench ? $"dans {Duration(value - now)}" : $"in {Duration(value - now)}";

    private static string Freshness(DateTimeOffset? lastSuccess, DateTimeOffset now)
    {
        if (lastSuccess == null)
            return IsFrench ? "inconnue" : "unknown";
        var age = now - lastSuccess.Value;
        if (age <= TimeSpan.FromMinutes(15)) return IsFrench ? "récente" : "fresh";
        if (age <= TimeSpan.FromHours(1)) return IsFrench ? "à surveiller" : "aging";
        return IsFrench ? "ancienne" : "stale";
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
        => failure == PollFailureKind.None ? (IsFrench ? "aucune" : "none") : failure.ToString();

    private static string SchedulerUnavailable()
        => IsFrench
            ? "L’ordonnanceur central est indisponible (démarrage en cours ou mode historique actif)."
            : "The central scheduler is unavailable (still starting or legacy mode is active).";

    private static string Unsupported()
        => IsFrench ? "Commande de suivi inconnue." : "Unknown tracking command.";

    private static bool IsFrench => string.Equals(Declare.Language, "fr", StringComparison.OrdinalIgnoreCase);
}
