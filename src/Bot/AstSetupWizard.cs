using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using ArchipelagoSphereTracker.src.Resources;

public sealed record AstSetupDraft(
    string SessionId,
    ulong OwnerUserId,
    ulong GuildId,
    ulong SourceChannelId,
    ulong TargetChannelId,
    DateTimeOffset ExpiresAtUtc,
    string? RoomUrl = null,
    string? ThreadTitle = null,
    string ThreadType = "Private",
    bool AutoAddMembers = false,
    bool Silent = false,
    string CheckFrequency = "5m")
{
    public override string ToString()
        => $"AstSetupDraft(Session={SessionId}, Guild={GuildId}, Channel={SourceChannelId}, Owner={OwnerUserId})";
}

public sealed class AstSetupSessionStore
{
    private readonly ConcurrentDictionary<string, AstSetupDraft> _sessions = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _lifetime;

    public AstSetupSessionStore(TimeProvider? timeProvider = null, TimeSpan? lifetime = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lifetime = lifetime ?? TimeSpan.FromMinutes(15);
        if (_lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lifetime));
    }

    public AstSetupDraft Start(ulong ownerUserId, ulong guildId, ulong sourceChannelId)
    {
        CleanupExpired();
        foreach (var existing in _sessions.Where(pair =>
                     pair.Value.OwnerUserId == ownerUserId &&
                     pair.Value.GuildId == guildId &&
                     pair.Value.SourceChannelId == sourceChannelId))
        {
            _sessions.TryRemove(existing.Key, out _);
        }

        var draft = new AstSetupDraft(
            Guid.NewGuid().ToString("N"),
            ownerUserId,
            guildId,
            sourceChannelId,
            sourceChannelId,
            _timeProvider.GetUtcNow().Add(_lifetime));
        _sessions[draft.SessionId] = draft;
        return draft;
    }

    public bool TryGetAuthorized(
        string sessionId,
        ulong ownerUserId,
        ulong guildId,
        ulong sourceChannelId,
        out AstSetupDraft draft)
    {
        draft = default!;
        if (!_sessions.TryGetValue(sessionId, out var existing)) return false;
        if (existing.ExpiresAtUtc <= _timeProvider.GetUtcNow())
        {
            _sessions.TryRemove(sessionId, out _);
            return false;
        }
        if (existing.OwnerUserId != ownerUserId ||
            existing.GuildId != guildId ||
            existing.SourceChannelId != sourceChannelId)
        {
            return false;
        }

        draft = existing;
        return true;
    }

    public bool TryUpdate(
        string sessionId,
        ulong ownerUserId,
        ulong guildId,
        ulong sourceChannelId,
        Func<AstSetupDraft, AstSetupDraft> update,
        out AstSetupDraft draft)
    {
        ArgumentNullException.ThrowIfNull(update);
        draft = default!;
        while (TryGetAuthorized(sessionId, ownerUserId, guildId, sourceChannelId, out var current))
        {
            var updated = update(current) with { ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime) };
            if (_sessions.TryUpdate(sessionId, updated, current))
            {
                draft = updated;
                return true;
            }
        }
        return false;
    }

    public bool TryTake(
        string sessionId,
        ulong ownerUserId,
        ulong guildId,
        ulong sourceChannelId,
        out AstSetupDraft draft)
    {
        draft = default!;
        if (!TryGetAuthorized(sessionId, ownerUserId, guildId, sourceChannelId, out var current))
            return false;
        if (!_sessions.TryRemove(new KeyValuePair<string, AstSetupDraft>(sessionId, current)))
            return false;
        draft = current;
        return true;
    }

    public bool Cancel(string sessionId, ulong ownerUserId, ulong guildId, ulong sourceChannelId)
        => TryTake(sessionId, ownerUserId, guildId, sourceChannelId, out _);

    public int CleanupExpired()
    {
        var now = _timeProvider.GetUtcNow();
        var removed = 0;
        foreach (var pair in _sessions.Where(pair => pair.Value.ExpiresAtUtc <= now))
        {
            if (_sessions.TryRemove(pair.Key, out _)) removed++;
        }
        return removed;
    }
}

public static class AstSetupWizard
{
    public const string CustomIdPrefix = "astsetup";
    private const string RoomUrlInputId = "room-url";
    private const string ThreadTitleInputId = "thread-title";
    private static readonly AstSetupSessionStore Sessions = new();
    public static async Task StartAsync(SocketSlashCommand command)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var guildId = command.GuildId ?? 0;
        var channelId = command.ChannelId ?? 0;
        try
        {
            if (guildId == 0 || channelId == 0 ||
                command.Channel is IThreadChannel ||
                command.Channel is not ITextChannel)
            {
                await command.RespondAsync(
                    Resource.AstSetupUseAstSetupInTheTextChannelThatWill,
                    ephemeral: true);
                return;
            }

            var authorization = await AstAuthorizationService.CreateDiscordContextAsync(
                guildId.ToString(),
                channelId.ToString(),
                command.User.Id,
                command.User as IGuildUser);
            if (authorization == null ||
                !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization))
            {
                await SecurityAuditLog.WriteAsync(
                    correlationId,
                    SecurityAuditSource.Discord,
                    command.User.Id.ToString(),
                    guildId.ToString(),
                    channelId.ToString(),
                    SecurityAuditAction.RoomAdd,
                    SecurityAuditOutcome.Denied);
                await command.RespondAsync(AstAuthorizationService.DeniedMessage, ephemeral: true);
                return;
            }

            var draft = Sessions.Start(command.User.Id, guildId, channelId);
            await command.RespondAsync(
                BuildSummary(draft),
                components: BuildComponents(draft),
                ephemeral: true);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Setup:{correlationId}] Failed to start ({exception.GetType().Name}).");
            if (!command.HasResponded)
            {
                await command.RespondAsync(SafeFailureMessage(), ephemeral: true);
            }
        }
    }

    public static async Task StartFromCommandCenterAsync(SocketMessageComponent component)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var guildId = component.GuildId ?? 0;
        var channelId = component.ChannelId ?? 0;
        try
        {
            if (guildId == 0 || channelId == 0 ||
                component.Channel is IThreadChannel ||
                component.Channel is not ITextChannel)
            {
                await component.ModifyOriginalResponseAsync(properties =>
                {
                    properties.Content = Resource.AstSetupOpenAstInTheTextChannelThatWillHost;
                    properties.Embed = null;
                    properties.Components = new ComponentBuilder().Build();
                }).ConfigureAwait(false);
                return;
            }

            var authorization = await AstAuthorizationService.CreateDiscordContextAsync(
                guildId.ToString(), channelId.ToString(), component.User.Id, component.User as IGuildUser)
                .ConfigureAwait(false);
            if (authorization == null ||
                !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization))
            {
                await SecurityAuditLog.WriteAsync(
                    correlationId,
                    SecurityAuditSource.Discord,
                    component.User.Id.ToString(),
                    guildId.ToString(),
                    channelId.ToString(),
                    SecurityAuditAction.RoomAdd,
                    SecurityAuditOutcome.Denied).ConfigureAwait(false);
                await component.ModifyOriginalResponseAsync(properties =>
                {
                    properties.Content = AstAuthorizationService.DeniedMessage;
                    properties.Embed = null;
                    properties.Components = new ComponentBuilder().Build();
                }).ConfigureAwait(false);
                return;
            }

            var draft = Sessions.Start(component.User.Id, guildId, channelId);
            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = BuildSummary(draft);
                properties.Embed = null;
                properties.Components = BuildComponents(draft);
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Setup:{correlationId}] Failed to start from command center ({exception.GetType().Name}).");
            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = SafeFailureMessage();
                properties.Embed = null;
                properties.Components = new ComponentBuilder().Build();
            }).ConfigureAwait(false);
        }
    }

    public static async Task HandleComponentAsync(SocketMessageComponent component)
    {
        if (!TryParseCustomId(component.Data.CustomId, out var sessionId, out var action)) return;
        var guildId = component.GuildId ?? 0;
        var channelId = component.ChannelId ?? 0;
        if (!Sessions.TryGetAuthorized(sessionId, component.User.Id, guildId, channelId, out var draft))
        {
            await component.RespondAsync(SessionUnavailableMessage(), ephemeral: true);
            return;
        }

        if (action == "details")
        {
            await component.RespondWithModalAsync(BuildDetailsModal(draft));
            return;
        }

        if (action == "cancel")
        {
            Sessions.Cancel(sessionId, component.User.Id, guildId, channelId);
            await component.UpdateAsync(properties =>
            {
                properties.Content = Resource.AstSetupSetupCancelled;
                properties.Components = new ComponentBuilder().Build();
            });
            return;
        }

        if (action == "confirm")
        {
            await ConfirmAsync(component, draft);
            return;
        }

        var updated = draft;
        var value = component.Data.Values.FirstOrDefault();
        var changed = action switch
        {
            "channel" when ulong.TryParse(value, out var targetChannelId) &&
                           Declare.Client.GetChannel(targetChannelId) is ITextChannel targetChannel &&
                           targetChannel is IGuildChannel targetGuildChannel &&
                           targetGuildChannel.GuildId == guildId &&
                           targetChannel is not IThreadChannel =>
                Sessions.TryUpdate(sessionId, component.User.Id, guildId, channelId,
                    current => current with { TargetChannelId = targetChannelId }, out updated),
            "thread" when value is "private" or "public" or "public-auto" =>
                Sessions.TryUpdate(sessionId, component.User.Id, guildId, channelId,
                    current => current with
                    {
                        ThreadType = value == "private" ? "Private" : "Public",
                        AutoAddMembers = value == "public-auto"
                    }, out updated),
            "notifications" when value is "normal" or "silent" =>
                Sessions.TryUpdate(sessionId, component.User.Id, guildId, channelId,
                    current => current with { Silent = value == "silent" }, out updated),
            "frequency" when value is "5m" or "15m" or "30m" or "1h" or "6h" or "12h" or "18h" or "1d" =>
                Sessions.TryUpdate(sessionId, component.User.Id, guildId, channelId,
                    current => current with { CheckFrequency = value }, out updated),
            "preview" => Sessions.TryUpdate(sessionId, component.User.Id, guildId, channelId,
                current => current, out updated),
            _ => false
        };

        if (!changed)
        {
            await component.RespondAsync(SessionUnavailableMessage(), ephemeral: true);
            return;
        }

        await component.UpdateAsync(properties =>
        {
            properties.Content = BuildSummary(updated);
            properties.Components = BuildComponents(updated);
        });
    }

    public static async Task HandleModalAsync(SocketModal modal)
    {
        if (!TryParseCustomId(modal.Data.CustomId, out var sessionId, out var action) || action != "details")
            return;

        var guildId = modal.GuildId ?? 0;
        var channelId = modal.ChannelId ?? 0;
        var url = modal.Data.Components.FirstOrDefault(component => component.CustomId == RoomUrlInputId)?.Value?.Trim();
        var title = modal.Data.Components.FirstOrDefault(component => component.CustomId == ThreadTitleInputId)?.Value?.Trim();
        if (!Sessions.TryUpdate(
                sessionId,
                modal.User.Id,
                guildId,
                channelId,
                current => current with { RoomUrl = url, ThreadTitle = title },
                out var updated))
        {
            await modal.RespondAsync(SessionUnavailableMessage(), ephemeral: true);
            return;
        }

        await modal.UpdateAsync(properties =>
        {
            properties.Content = BuildSummary(updated);
            properties.Components = BuildComponents(updated);
        });
    }

    public static bool TryParseCustomId(string? customId, out string sessionId, out string action)
    {
        sessionId = string.Empty;
        action = string.Empty;
        if (string.IsNullOrEmpty(customId)) return false;
        var parts = customId.Split(':');
        if (parts.Length != 3 ||
            !string.Equals(parts[0], CustomIdPrefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(parts[1], "N", out _))
        {
            return false;
        }
        sessionId = parts[1];
        action = parts[2];
        return action.Length > 0;
    }

    public static string BuildSummary(AstSetupDraft draft)
    {
        var urlStatus = Resource.AstSetupNotConfigured;
        if (!string.IsNullOrWhiteSpace(draft.RoomUrl))
        {
            urlStatus = ArchipelagoUrlSecurity.TryParseRoomUrl(draft.RoomUrl, out var parsed) && parsed != null
                ? parsed.Host
                : Resource.AstSetupInvalidFormat;
        }

        var title = string.IsNullOrWhiteSpace(draft.ThreadTitle)
            ? Resource.AstSetupNotConfigured2
            : SafePreviewValue(draft.ThreadTitle);
        var thread = draft.ThreadType == "Private"
            ? Resource.AstSetupPrivate
            : draft.AutoAddMembers
                ? Resource.AstSetupPublicMembersAdded
                : Resource.AstSetupPublic;
        var notifications = draft.Silent
            ? Resource.AstSetupSilent
            : Resource.AstSetupNormal;
        var ready = IsReady(draft)
            ? Resource.AstSetupReadyToConfirm
            : Resource.AstSetupConfigureTheURLAndThreadName;

        return string.Format(Resource.AstSetupASTSetupAssistantTargetChannelWebHostThreadNotificationsMinimum, draft.TargetChannelId, urlStatus, title, thread, notifications, draft.CheckFrequency, ready);
    }

    public static MessageComponent BuildComponents(AstSetupDraft draft)
    {
        var id = draft.SessionId;
        var builder = new ComponentBuilder()
            .WithButton(Resource.AstSetupConfigureRoom, CustomId(id, "details"), ButtonStyle.Primary, row: 0)
            .WithButton(Resource.AstSetupPreview, CustomId(id, "preview"), ButtonStyle.Secondary, row: 0)
            .WithButton(Resource.AstCenterConfirm, CustomId(id, "confirm"), ButtonStyle.Success, disabled: !IsReady(draft), row: 0)
            .WithButton(Resource.AstCenterCancel, CustomId(id, "cancel"), ButtonStyle.Danger, row: 0)
            .WithSelectMenu(ChannelMenu(draft), row: 1)
            .WithSelectMenu(ThreadMenu(draft), row: 2)
            .WithSelectMenu(NotificationMenu(draft), row: 3)
            .WithSelectMenu(FrequencyMenu(draft), row: 4);
        return builder.Build();
    }

    public static bool IsReady(AstSetupDraft draft)
        => !string.IsNullOrWhiteSpace(draft.ThreadTitle) &&
           draft.ThreadTitle.Length <= 100 &&
           !string.IsNullOrWhiteSpace(draft.RoomUrl) &&
           ArchipelagoUrlSecurity.TryParseRoomUrl(draft.RoomUrl, out _);

    private static async Task ConfirmAsync(SocketMessageComponent component, AstSetupDraft draft)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var guildId = component.GuildId ?? 0;
        var sourceChannelId = component.ChannelId ?? 0;
        if (!IsReady(draft) ||
            !Sessions.TryTake(draft.SessionId, component.User.Id, guildId, sourceChannelId, out draft))
        {
            await component.RespondAsync(SessionUnavailableMessage(), ephemeral: true);
            return;
        }

        var channelId = draft.TargetChannelId;
        await component.UpdateAsync(properties =>
        {
            properties.Content = Resource.AstSetupValidatingTheRoomAndCreatingTheThread;
            properties.Components = new ComponentBuilder().Build();
        });

        try
        {
            var authorization = await AstAuthorizationService.CreateDiscordContextAsync(
                guildId.ToString(),
                channelId.ToString(),
                component.User.Id,
                component.User as IGuildUser);
            if (authorization == null ||
                !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization) ||
                Declare.Client.GetChannel(channelId) is not ITextChannel channel ||
                channel is not IGuildChannel guildChannel ||
                guildChannel.GuildId != guildId ||
                channel is IThreadChannel)
            {
                await SecurityAuditLog.WriteAsync(
                    correlationId,
                    SecurityAuditSource.Discord,
                    component.User.Id.ToString(),
                    guildId.ToString(),
                    channelId.ToString(),
                    SecurityAuditAction.RoomAdd,
                    SecurityAuditOutcome.Denied);
                await component.ModifyOriginalResponseAsync(properties =>
                    properties.Content = AstAuthorizationService.DeniedMessage);
                return;
            }

            await SecurityAuditLog.WriteAsync(
                correlationId,
                SecurityAuditSource.Discord,
                component.User.Id.ToString(),
                guildId.ToString(),
                channelId.ToString(),
                SecurityAuditAction.RoomAdd,
                SecurityAuditOutcome.Started);

            var result = await UrlClass.AddUrlFromSetupAsync(
                new UrlClass.UrlAddOptions(
                    draft.RoomUrl!,
                    draft.ThreadTitle!,
                    draft.ThreadType,
                    draft.AutoAddMembers,
                    draft.Silent,
                    draft.CheckFrequency,
                    component.User as IGuildUser),
                channelId.ToString(),
                guildId.ToString(),
                channel);

            await SecurityAuditLog.WriteAsync(
                correlationId,
                SecurityAuditSource.Discord,
                component.User.Id.ToString(),
                guildId.ToString(),
                result.ThreadChannelId ?? channelId.ToString(),
                SecurityAuditAction.RoomAdd,
                result.Success ? SecurityAuditOutcome.Succeeded : SecurityAuditOutcome.Failed);
            await component.ModifyOriginalResponseAsync(properties => properties.Content = result.Message);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Setup:{correlationId}] Confirmation failed ({exception.GetType().Name}).");
            try
            {
                await SecurityAuditLog.WriteAsync(
                    correlationId,
                    SecurityAuditSource.Discord,
                    component.User.Id.ToString(),
                    guildId.ToString(),
                    channelId.ToString(),
                    SecurityAuditAction.RoomAdd,
                    SecurityAuditOutcome.Failed);
            }
            catch (Exception auditException)
            {
                Console.WriteLine($"[Audit] Setup failure could not be recorded ({auditException.GetType().Name}).");
            }
            await component.ModifyOriginalResponseAsync(properties => properties.Content = SafeFailureMessage());
        }
    }

    private static Modal BuildDetailsModal(AstSetupDraft draft)
    {
        var urlInput = new TextInputBuilder()
            .WithLabel(Resource.AstSetupArchipelagoRoomURL)
            .WithCustomId(RoomUrlInputId)
            .WithStyle(TextInputStyle.Short)
            .WithPlaceholder("https://archipelago.example/room/…")
            .WithMinLength(12)
            .WithMaxLength(1000)
            .WithRequired(true);
        if (!string.IsNullOrWhiteSpace(draft.RoomUrl)) urlInput.WithValue(draft.RoomUrl);

        var titleInput = new TextInputBuilder()
            .WithLabel(Resource.WebThreadName)
            .WithCustomId(ThreadTitleInputId)
            .WithStyle(TextInputStyle.Short)
            .WithPlaceholder("Archipelago")
            .WithMinLength(1)
            .WithMaxLength(100)
            .WithRequired(true);
        if (!string.IsNullOrWhiteSpace(draft.ThreadTitle)) titleInput.WithValue(draft.ThreadTitle);

        return new ModalBuilder()
            .WithTitle(Resource.AstSetupConfigureRoom)
            .WithCustomId(CustomId(draft.SessionId, "details"))
            .AddTextInput(urlInput, row: 0)
            .AddTextInput(titleInput, row: 1)
            .Build();
    }

    private static SelectMenuBuilder ThreadMenu(AstSetupDraft draft)
        => new SelectMenuBuilder()
            .WithCustomId(CustomId(draft.SessionId, "thread"))
            .WithPlaceholder(Resource.WebThreadType)
            .WithMinValues(1)
            .WithMaxValues(1)
            .AddOption(Option(Resource.SCThreadPrivate, "private", draft.ThreadType == "Private"))
            .AddOption(Option(Resource.SCThreadPublic, "public", draft.ThreadType == "Public" && !draft.AutoAddMembers))
            .AddOption(Option(Resource.AstSetupPublicAddMembers, "public-auto", draft.ThreadType == "Public" && draft.AutoAddMembers));

    private static SelectMenuBuilder ChannelMenu(AstSetupDraft draft)
        => new SelectMenuBuilder()
            .WithCustomId(CustomId(draft.SessionId, "channel"))
            .WithPlaceholder(Resource.AstSetupChannelThatWillHostTheRoom)
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithType(ComponentType.ChannelSelect)
            .WithChannelTypes(ChannelType.Text)
            .AddDefaultValue(draft.TargetChannelId, SelectDefaultValueType.Channel);

    private static SelectMenuBuilder NotificationMenu(AstSetupDraft draft)
        => new SelectMenuBuilder()
            .WithCustomId(CustomId(draft.SessionId, "notifications"))
            .WithPlaceholder(Resource.AstSetupNotifications)
            .WithMinValues(1)
            .WithMaxValues(1)
            .AddOption(Option(Resource.AstSetupNormal2, "normal", !draft.Silent))
            .AddOption(Option(Resource.AstSetupSilent2, "silent", draft.Silent));

    private static SelectMenuBuilder FrequencyMenu(AstSetupDraft draft)
    {
        var menu = new SelectMenuBuilder()
            .WithCustomId(CustomId(draft.SessionId, "frequency"))
            .WithPlaceholder(Resource.AstSetupMinimumFrequency)
            .WithMinValues(1)
            .WithMaxValues(1);
        foreach (var frequency in new[] { "5m", "15m", "30m", "1h", "6h", "12h", "18h", "1d" })
            menu.AddOption(Option(frequency, frequency, draft.CheckFrequency == frequency));
        return menu;
    }

    private static SelectMenuOptionBuilder Option(string label, string value, bool isDefault)
        => new SelectMenuOptionBuilder()
            .WithLabel(label)
            .WithValue(value)
            .WithDefault(isDefault);

    private static string CustomId(string sessionId, string action)
        => $"{CustomIdPrefix}:{sessionId}:{action}";

    private static string SafePreviewValue(string value)
        => value.Replace('`', '\'').Replace("@", "@\u200b", StringComparison.Ordinal);

    private static string SessionUnavailableMessage()
        => Resource.AstSetupThisSessionExpiredOrDoesNotBelongToYou;

    private static string SafeFailureMessage()
        => Resource.AstSetupSetupFailedPleaseRetryOrContactAnASTAdministrator;
}
