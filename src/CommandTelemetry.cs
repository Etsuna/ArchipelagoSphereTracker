using Discord;
using Discord.WebSocket;
using Prometheus;
using System.Diagnostics;

public static class CommandTelemetry
{
    private static readonly Counter Invocations = Metrics.CreateCounter(
        "ast_command_invocations_total",
        "Commands and command-center actions received by AST.",
        new CounterConfiguration { LabelNames = ["surface", "command"] });

    private static readonly Counter Options = Metrics.CreateCounter(
        "ast_command_options_total",
        "Command options used, with privacy-safe bounded selections only.",
        new CounterConfiguration { LabelNames = ["surface", "command", "option", "selection"] });

    private static readonly Counter Outcomes = Metrics.CreateCounter(
        "ast_command_outcomes_total",
        "Discord slash-command processing outcomes.",
        new CounterConfiguration { LabelNames = ["surface", "command", "outcome"] });

    private static readonly Histogram Duration = Metrics.CreateHistogram(
        "ast_command_duration_seconds",
        "Command and command-center action processing duration.",
        new HistogramConfiguration
        {
            LabelNames = ["surface", "command"],
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 16)
        });

    private static readonly HashSet<string> KnownSlashCommands =
    [
        "ast", "ast-setup", "get-aliases", "add-alias", "delete-alias", "update-frequency-check",
        "add-url", "update-silent-option", "delete-url", "status-games-list", "ast-health",
        "ast-room-health", "ast-sync-now", "ast-pause", "ast-resume", "ast-polling", "info",
        "get-patch", "recap-all", "recap", "recap-and-clean", "clean", "clean-all",
        "hint-from-finder", "hint-for-receiver", "list-items", "analyze-spoiler-log",
        "send-spoiler-log", "apworlds-info", "discord", "excluded-item", "excluded-item-list",
        "delete-excluded-item", "ast-user-portal", "ast-room-portal", "ast-portal", "list-yamls",
        "list-apworld", "backup-yamls", "backup-apworld", "download-template", "delete-yaml",
        "clean-yamls", "send-yaml", "generate-with-zip", "send-apworld", "generate", "test-generate"
    ];

    private static readonly HashSet<string> KnownCommandCenterActions =
    [
        "home", "personal", "room", "manage", "admin", "admin-access", "admin-archipelago-access",
        "instance-admin", "help", "manage-polling", "manage-more", "archipelago-tools",
        "archipelago-yaml", "archipelago-templates", "archipelago-generation", "archipelago-apworld",
        "personal-slots", "personal-items", "personal-hints", "personal-recap", "personal-patch",
        "personal-advanced", "personal-exclusions", "personal-spoiler", "manage-spoiler",
        "personal-portal", "personal-portal-revoke-request", "room-games", "room-info",
        "room-associations", "sync-now", "pause", "resume", "room-portal", "room-portal-revoke-request",
        "admin-portal", "admin-portal-revoke-request", "admin-setup", "guild-health", "access-grant",
        "access-revoke", "arch-access-deny", "arch-access-restore", "select-room", "poll-policy",
        "notifications", "alias-add", "alias-delete", "alias-filter", "alias-add-manual",
        "alias-delete-manual", "patch-alias", "clean-select", "recap-clean-select", "clean-all-request",
        "confirm-clean", "cancel-pending", "exclude-add-alias", "exclude-delete-alias", "exclude-item-add",
        "exclude-item-delete", "confirm-exclusion-delete", "cancel-exclusion", "exclusion-search",
        "exclusion-clear-search", "exclusion-previous", "exclusion-next", "spoiler-alias", "spoiler-mode",
        "spoiler-hide", "spoiler-analyze", "spoiler-configure", "spoiler-reset-validation",
        "yaml-list", "yaml-backup", "yaml-delete-select", "yaml-template-download", "yaml-clean-request",
        "yaml-confirm-clean", "yaml-cancel", "yaml-confirm-delete", "yaml-cancel-delete", "apworld-list",
        "apworld-backup", "generation-run", "generation-test", "generation-skip", "delete-room-request",
        "confirm-delete-room", "cancel-delete-room", "confirm-portal-revoke", "cancel-portal-revoke",
        "selection-search", "selection-clear-search", "selection-previous", "selection-next", "output-previous",
        "output-next", "output-close", "instance-guild", "instance-room", "instance-back-guilds",
        "instance-back-rooms", "instance-sync", "instance-pause", "instance-resume",
        "instance-delete-guild-request", "instance-delete-room-request", "instance-confirm-delete",
        "instance-cancel-delete"
    ];

    private static readonly HashSet<string> KnownWebOptions =
    [
        "alias", "autoAddMembers", "checkFrequency", "file", "fileName", "hideItems", "maximumFrequency",
        "missingMode", "mode", "resetValidation", "silent", "skipProgBalancing", "sphere", "template",
        "threadName", "threadType", "url", "validateSphere"
    ];

    private static readonly HashSet<string> SafeSelections =
    [
        "0", "1", "16", "17", "21", "27", "31", "first", "full", "public", "private",
        "automatic", "fixed", "automatic|15m", "automatic|30m", "automatic|1h", "automatic|6h",
        "fixed|5m", "fixed|15m", "fixed|30m", "fixed|1h", "fixed|6h", "fixed|12h", "fixed|18h",
        "fixed|1d", "5m", "15m", "30m", "1h", "6h", "12h", "18h", "1d"
    ];

    private static readonly HashSet<string> BooleanOptions =
    [
        "auto-add-members", "hide-items", "reset-validation", "silent", "skip-prog-balancing"
    ];

    private static readonly HashSet<string> ChoiceOptions =
    [
        "check-frequency", "maximum-frequency", "missing-mode", "mode", "skip-mention-items", "thread-type"
    ];

    public static CommandOperation BeginSlash(SocketSlashCommand command)
    {
        var normalizedCommand = NormalizeSlashCommand(command.CommandName);
        var operation = Begin("discord_slash", normalizedCommand, recordOutcome: true);
        foreach (var option in command.Data.Options ?? [])
        {
            RecordOption(
                "discord_slash",
                normalizedCommand,
                CanonicalizeOptionName(option.Name),
                ClassifyOptionSelection(CanonicalizeOptionName(option.Name), option.Value));
        }

        return operation;
    }

    public static CommandOperation BeginCommandCenterAction(string component, string action, string? selected = null)
    {
        var normalizedAction = NormalizeCommandCenterAction(action);
        var surface = component switch
        {
            "button" => "ast_button",
            "select" => "ast_select",
            "modal" => "ast_modal",
            _ => "ast_other"
        };
        var operation = Begin(surface, normalizedAction, recordOutcome: false);
        if (selected != null)
            RecordOption(surface, normalizedAction, "selection", ClassifyCommandCenterSelection(normalizedAction, selected));
        return operation;
    }

    public static void RecordModalOption(string action, string option, bool provided)
    {
        var normalizedAction = NormalizeCommandCenterAction(action);
        RecordOption(
            "ast_modal",
            normalizedAction,
            CanonicalizeOptionName(option),
            provided ? "provided" : "empty");
    }

    public static CommandOperation BeginWebCommand(
        string command,
        IEnumerable<KeyValuePair<string, string?>> options,
        IEnumerable<KeyValuePair<string, string?>> files)
    {
        var normalizedCommand = NormalizeSlashCommand(command);
        var operation = Begin("web_portal", normalizedCommand, recordOutcome: false);
        foreach (var option in options)
        {
            if (!KnownWebOptions.Contains(option.Key))
                continue;
            RecordOption(
                "web_portal",
                normalizedCommand,
                CanonicalizeOptionName(option.Key),
                ClassifyOptionSelection(CanonicalizeOptionName(option.Key), option.Value));
        }
        foreach (var file in files)
        {
            if (!KnownWebOptions.Contains(file.Key))
                continue;
            RecordOption(
                "web_portal",
                normalizedCommand,
                CanonicalizeOptionName(file.Key),
                ClassifyAttachmentExtension(file.Value));
        }
        return operation;
    }

    public static string NormalizeSlashCommand(string? command)
        => command != null && KnownSlashCommands.Contains(command) ? command : "unknown";

    public static string NormalizeCommandCenterAction(string? action)
        => action != null && KnownCommandCenterActions.Contains(action) ? action : "unknown";

    public static string ClassifyCommandCenterSelection(string action, string? selected)
    {
        if (string.IsNullOrWhiteSpace(selected))
            return "empty";
        if (action is "notifications" or "generation-skip" or "spoiler-hide" &&
            bool.TryParse(selected, out var boolean))
            return boolean ? "true" : "false";
        if (action is "poll-policy" or "spoiler-mode" or "alias-filter")
        {
            var normalized = selected.Trim().ToLowerInvariant();
            return SafeSelections.Contains(normalized) ? normalized : "other";
        }
        return "provided";
    }

    public static string ClassifyOptionSelection(string option, object? value)
    {
        if (value == null)
            return "empty";
        if (value is bool boolean)
            return boolean ? "true" : "false";
        if (value is IAttachment attachment)
            return ClassifyAttachmentExtension(attachment.Filename);
        if (value is string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "empty";
            var normalized = text.Trim().ToLowerInvariant();
            if (BooleanOptions.Contains(option) && bool.TryParse(normalized, out var booleanText))
                return booleanText ? "true" : "false";
            return ChoiceOptions.Contains(option) && SafeSelections.Contains(normalized) ? normalized : "provided";
        }
        return "provided";
    }

    public static string ClassifyAttachmentExtension(string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty).TrimStart('.').ToLowerInvariant();
        return extension is "yaml" or "yml" or "zip" or "apworld" or "txt" or "json"
            ? extension
            : "other_file";
    }

    private static CommandOperation Begin(string surface, string command, bool recordOutcome)
    {
        Invocations.WithLabels(surface, command).Inc();
        return new CommandOperation(surface, command, recordOutcome);
    }

    private static void RecordOption(string surface, string command, string option, string selection)
        => Options.WithLabels(surface, command, option, selection).Inc();

    private static string CanonicalizeOptionName(string option)
        => option switch
        {
            "silencieux" => "silent",
            "nom-du-fil" => "thread-name",
            "type-de-fil" => "thread-type",
            "frequence-check" => "check-frequency",
            "ignorer_mentions_items" => "skip-mention-items",
            "skip_mention_items" => "skip-mention-items",
            "autoAddMembers" => "auto-add-members",
            "checkFrequency" => "check-frequency",
            "fileName" => "file-name",
            "hideItems" => "hide-items",
            "maximumFrequency" => "maximum-frequency",
            "missingMode" => "missing-mode",
            "resetValidation" => "reset-validation",
            "skipProgBalancing" => "skip-prog-balancing",
            "threadName" => "thread-name",
            "threadType" => "thread-type",
            "validateSphere" => "validate-sphere",
            "ast-slot-alias" or "ast-spoiler-alias" => "alias",
            "ast-spoiler-sphere" => "sphere",
            "ast-spoiler-validate" => "validate-sphere",
            "ast-selection-search" or "ast-exclusion-search" => "search",
            _ => option
        };

    public sealed class CommandOperation : IDisposable
    {
        private readonly string _surface;
        private readonly string _command;
        private readonly bool _recordOutcome;
        private readonly long _startedAt = Stopwatch.GetTimestamp();
        private string? _outcome;
        private int _disposed;

        internal CommandOperation(string surface, string command, bool recordOutcome)
        {
            _surface = surface;
            _command = command;
            _recordOutcome = recordOutcome;
        }

        public void Complete(string outcome = "completed")
            => _outcome = outcome is "completed" or "denied" or "invalid_context" or "failed"
                ? outcome
                : "failed";

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            Duration.WithLabels(_surface, _command).Observe(Stopwatch.GetElapsedTime(_startedAt).TotalSeconds);
            if (_recordOutcome)
                Outcomes.WithLabels(_surface, _command, _outcome ?? "failed").Inc();
        }
    }
}
