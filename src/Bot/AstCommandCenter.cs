using System.Collections.Concurrent;
using System.Globalization;
using Discord;
using Discord.WebSocket;

public enum AstUiScreen
{
    Home,
    Personal,
    Room,
    Manage,
    Administration,
    Help,
    Polling,
    ManageMore,
    Yaml,
    Generation,
    Apworld,
    Slots,
    Advanced,
    Exclusions,
    SpoilerAnalysis
}

public sealed record AstUiSession(
    string Id,
    ulong OwnerUserId,
    ulong GuildId,
    ulong SourceChannelId,
    ulong? RoomChannelId,
    AstUiScreen Screen,
    DateTimeOffset ExpiresAtUtc,
    string AliasMentionFlag = "0",
    string? PendingAction = null,
    string? PendingAlias = null,
    string? PendingItem = null,
    string? SpoilerAlias = null,
    int? SpoilerSphereLimit = null,
    string SpoilerMissingMode = "first",
    bool SpoilerHideItems = true,
    bool GenerationSkipProgBalancing = false,
    IReadOnlyList<string>? OutputPages = null,
    int OutputPageIndex = 0,
    int SelectionPageIndex = 0,
    string? SelectionSearch = null,
    int ExclusionPageIndex = 0,
    string? ExclusionSearch = null);

public sealed class AstUiSessionStore
{
    private readonly ConcurrentDictionary<string, AstUiSession> _sessions = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _lifetime;

    public AstUiSessionStore(TimeProvider? timeProvider = null, TimeSpan? lifetime = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lifetime = lifetime ?? TimeSpan.FromMinutes(15);
        if (_lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lifetime));
    }

    public AstUiSession Start(ulong ownerUserId, ulong guildId, ulong sourceChannelId, ulong? roomChannelId)
    {
        CleanupExpired();
        foreach (var pair in _sessions.Where(pair =>
                     pair.Value.OwnerUserId == ownerUserId &&
                     pair.Value.GuildId == guildId &&
                     pair.Value.SourceChannelId == sourceChannelId))
        {
            _sessions.TryRemove(pair.Key, out _);
        }

        var session = new AstUiSession(
            Guid.NewGuid().ToString("N"),
            ownerUserId,
            guildId,
            sourceChannelId,
            roomChannelId,
            AstUiScreen.Home,
            _timeProvider.GetUtcNow().Add(_lifetime));
        _sessions[session.Id] = session;
        return session;
    }

    public bool TryGetAuthorized(
        string id,
        ulong ownerUserId,
        ulong guildId,
        ulong sourceChannelId,
        out AstUiSession session)
    {
        session = default!;
        if (!_sessions.TryGetValue(id, out var current)) return false;
        if (current.ExpiresAtUtc <= _timeProvider.GetUtcNow())
        {
            _sessions.TryRemove(id, out _);
            return false;
        }
        if (current.OwnerUserId != ownerUserId || current.GuildId != guildId ||
            current.SourceChannelId != sourceChannelId)
        {
            return false;
        }

        session = current;
        return true;
    }

    public bool TryUpdateScreen(
        string id,
        ulong ownerUserId,
        ulong guildId,
        ulong sourceChannelId,
        AstUiScreen screen,
        out AstUiSession session)
    {
        session = default!;
        while (TryGetAuthorized(id, ownerUserId, guildId, sourceChannelId, out var current))
        {
            var updated = current with
            {
                Screen = screen,
                SelectionPageIndex = 0,
                SelectionSearch = null,
                ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime)
            };
            if (_sessions.TryUpdate(id, updated, current))
            {
                session = updated;
                return true;
            }
        }
        return false;
    }

    public bool TrySelectRoom(
        string id,
        ulong ownerUserId,
        ulong guildId,
        ulong sourceChannelId,
        ulong roomChannelId,
        out AstUiSession session)
    {
        session = default!;
        while (TryGetAuthorized(id, ownerUserId, guildId, sourceChannelId, out var current))
        {
            var updated = current with
            {
                RoomChannelId = roomChannelId,
                Screen = AstUiScreen.Home,
                SelectionPageIndex = 0,
                SelectionSearch = null,
                ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime)
            };
            if (_sessions.TryUpdate(id, updated, current))
            {
                session = updated;
                return true;
            }
        }
        return false;
    }

    public bool TrySetAliasMentionFlag(
        string id, ulong ownerUserId, ulong guildId, ulong sourceChannelId, string flag, out AstUiSession session)
    {
        session = default!;
        if (flag is not ("0" or "1" or "16" or "17" or "21" or "27" or "31")) return false;
        while (TryGetAuthorized(id, ownerUserId, guildId, sourceChannelId, out var current))
        {
            var updated = current with { AliasMentionFlag = flag, ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime) };
            if (_sessions.TryUpdate(id, updated, current)) { session = updated; return true; }
        }
        return false;
    }

    public bool TrySetPending(
        string id, ulong ownerUserId, ulong guildId, ulong sourceChannelId,
        string? action, string? alias, out AstUiSession session, string? item = null)
    {
        session = default!;
        while (TryGetAuthorized(id, ownerUserId, guildId, sourceChannelId, out var current))
        {
            var updated = current with
            {
                PendingAction = action,
                PendingAlias = alias,
                PendingItem = item,
                SelectionPageIndex = 0,
                SelectionSearch = null,
                ExclusionPageIndex = 0,
                ExclusionSearch = null,
                ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime)
            };
            if (_sessions.TryUpdate(id, updated, current)) { session = updated; return true; }
        }
        return false;
    }

    public bool TrySetExclusionSearch(
        string id,
        ulong ownerUserId,
        ulong guildId,
        ulong sourceChannelId,
        string? search,
        out AstUiSession session)
    {
        session = default!;
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > 100) return false;
        while (TryGetAuthorized(id, ownerUserId, guildId, sourceChannelId, out var current))
        {
            var updated = current with
            {
                ExclusionSearch = search,
                ExclusionPageIndex = 0,
                ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime)
            };
            if (_sessions.TryUpdate(id, updated, current))
            {
                session = updated;
                return true;
            }
        }
        return false;
    }

    public bool TryMoveExclusionPage(
        string id,
        ulong ownerUserId,
        ulong guildId,
        ulong sourceChannelId,
        int delta,
        int totalItems,
        out AstUiSession session)
    {
        session = default!;
        var lastPage = Math.Max(0, (Math.Max(0, totalItems) - 1) / 25);
        while (TryGetAuthorized(id, ownerUserId, guildId, sourceChannelId, out var current))
        {
            var updated = current with
            {
                ExclusionPageIndex = Math.Clamp(current.ExclusionPageIndex + delta, 0, lastPage),
                ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime)
            };
            if (_sessions.TryUpdate(id, updated, current))
            {
                session = updated;
                return true;
            }
        }
        return false;
    }

    public bool TryMoveSelectionPage(
        string id,
        ulong ownerUserId,
        ulong guildId,
        ulong sourceChannelId,
        int delta,
        int totalItems,
        out AstUiSession session)
    {
        session = default!;
        var lastPage = Math.Max(0, (Math.Max(0, totalItems) - 1) / 25);
        while (TryGetAuthorized(id, ownerUserId, guildId, sourceChannelId, out var current))
        {
            var updated = current with
            {
                SelectionPageIndex = Math.Clamp(current.SelectionPageIndex + delta, 0, lastPage),
                ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime)
            };
            if (_sessions.TryUpdate(id, updated, current))
            {
                session = updated;
                return true;
            }
        }
        return false;
    }

    public bool TrySetSelectionSearch(
        string id,
        ulong ownerUserId,
        ulong guildId,
        ulong sourceChannelId,
        string? search,
        out AstUiSession session)
    {
        session = default!;
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > 100) return false;
        while (TryGetAuthorized(id, ownerUserId, guildId, sourceChannelId, out var current))
        {
            var updated = current with
            {
                SelectionSearch = search,
                SelectionPageIndex = 0,
                ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime)
            };
            if (_sessions.TryUpdate(id, updated, current))
            {
                session = updated;
                return true;
            }
        }
        return false;
    }

    public bool TrySetSpoilerOptions(
        string id, ulong ownerUserId, ulong guildId, ulong sourceChannelId,
        out AstUiSession session, string? alias = null, bool setAlias = false,
        int? sphereLimit = null, bool setSphereLimit = false,
        string? missingMode = null, bool? hideItems = null)
    {
        session = default!;
        if (missingMode != null && missingMode is not ("first" or "full")) return false;
        if (sphereLimit < 0) return false;
        while (TryGetAuthorized(id, ownerUserId, guildId, sourceChannelId, out var current))
        {
            var updated = current with
            {
                SpoilerAlias = setAlias ? alias : current.SpoilerAlias,
                SpoilerSphereLimit = setSphereLimit ? sphereLimit : current.SpoilerSphereLimit,
                SpoilerMissingMode = missingMode ?? current.SpoilerMissingMode,
                SpoilerHideItems = hideItems ?? current.SpoilerHideItems,
                ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime)
            };
            if (_sessions.TryUpdate(id, updated, current)) { session = updated; return true; }
        }
        return false;
    }

    public bool TrySetGenerationSkipProgBalancing(
        string id, ulong ownerUserId, ulong guildId, ulong sourceChannelId, bool skip, out AstUiSession session)
    {
        session = default!;
        while (TryGetAuthorized(id, ownerUserId, guildId, sourceChannelId, out var current))
        {
            var updated = current with
            {
                GenerationSkipProgBalancing = skip,
                ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime)
            };
            if (_sessions.TryUpdate(id, updated, current)) { session = updated; return true; }
        }
        return false;
    }

    public bool TrySetOutputPages(
        string id, ulong ownerUserId, ulong guildId, ulong sourceChannelId,
        IReadOnlyList<string>? pages, out AstUiSession session)
    {
        session = default!;
        if (pages is { Count: 0 }) pages = null;
        while (TryGetAuthorized(id, ownerUserId, guildId, sourceChannelId, out var current))
        {
            var updated = current with
            {
                OutputPages = pages,
                OutputPageIndex = 0,
                ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime)
            };
            if (_sessions.TryUpdate(id, updated, current)) { session = updated; return true; }
        }
        return false;
    }

    public bool TryMoveOutputPage(
        string id, ulong ownerUserId, ulong guildId, ulong sourceChannelId,
        int delta, out AstUiSession session)
    {
        session = default!;
        while (TryGetAuthorized(id, ownerUserId, guildId, sourceChannelId, out var current))
        {
            if (current.OutputPages is not { Count: > 0 } pages) return false;
            var index = Math.Clamp(current.OutputPageIndex + delta, 0, pages.Count - 1);
            var updated = current with
            {
                OutputPageIndex = index,
                ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime)
            };
            if (_sessions.TryUpdate(id, updated, current)) { session = updated; return true; }
        }
        return false;
    }

    public int CleanupExpired()
    {
        var now = _timeProvider.GetUtcNow();
        var removed = 0;
        foreach (var pair in _sessions.Where(pair => pair.Value.ExpiresAtUtc <= now))
            if (_sessions.TryRemove(pair.Key, out _)) removed++;
        return removed;
    }
}

public static class AstCommandCenter
{
    public const string CustomIdPrefix = "astui";
    public static IReadOnlyDictionary<string, string> LegacyCommandCoverage { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["get-aliases"] = "room-associations",
            ["add-alias"] = "personal-slots",
            ["delete-alias"] = "personal-slots",
            ["update-frequency-check"] = "manage-polling",
            ["add-url"] = "admin-setup",
            ["ast-setup"] = "admin-setup",
            ["update-silent-option"] = "manage-more",
            ["delete-url"] = "manage-more",
            ["status-games-list"] = "room-games",
            ["ast-health"] = "guild-health",
            ["ast-room-health"] = "room",
            ["ast-sync-now"] = "sync-now",
            ["ast-pause"] = "pause",
            ["ast-resume"] = "resume",
            ["ast-polling"] = "manage-polling",
            ["info"] = "room-info",
            ["get-patch"] = "personal-patch",
            ["recap-all"] = "personal-recap",
            ["recap"] = "personal-recap",
            ["recap-and-clean"] = "personal-advanced",
            ["clean"] = "personal-advanced",
            ["clean-all"] = "personal-advanced",
            ["hint-from-finder"] = "personal-hints",
            ["hint-for-receiver"] = "personal-hints",
            ["list-items"] = "personal-items",
            ["analyze-spoiler-log"] = "manage-spoiler",
            ["send-spoiler-log"] = "ast-file",
            ["apworlds-info"] = "help",
            ["ast-user-portal"] = "personal-portal",
            ["ast-room-portal"] = "room-portal",
            ["ast-portal"] = "admin-portal",
            ["discord"] = "help",
            ["excluded-item"] = "personal-exclusions",
            ["excluded-item-list"] = "personal-exclusions",
            ["delete-excluded-item"] = "personal-exclusions",
            ["list-yamls"] = "yaml-list",
            ["list-apworld"] = "apworld-list",
            ["backup-yamls"] = "yaml-backup",
            ["backup-apworld"] = "apworld-backup",
            ["download-template"] = "yaml-template-download",
            ["delete-yaml"] = "yaml-delete-select",
            ["clean-yamls"] = "yaml-clean-request",
            ["send-yaml"] = "ast-file",
            ["generate-with-zip"] = "ast-file",
            ["send-apworld"] = "ast-file",
            ["generate"] = "generation-run",
            ["test-generate"] = "generation-test"
        };
    private const string SpoilerAliasInputId = "ast-spoiler-alias";
    private const string SpoilerSphereInputId = "ast-spoiler-sphere";
    private const string SpoilerValidateInputId = "ast-spoiler-validate";
    private const string SlotAliasInputId = "ast-slot-alias";
    private const string SelectionSearchInputId = "ast-selection-search";
    private const string ExclusionSearchInputId = "ast-exclusion-search";
    private static readonly AstUiSessionStore Sessions = new();
    private static bool IsFrench => string.Equals(Declare.Language, "fr", StringComparison.OrdinalIgnoreCase);

    public static async Task StartAsync(SocketSlashCommand command)
    {
        if (command.GuildId is not { } guildId || command.ChannelId is not { } channelId)
        {
            await command.RespondAsync(
                IsFrench ? "`/ast` doit être utilisé dans un serveur Discord." : "`/ast` must be used in a Discord server.",
                ephemeral: true);
            return;
        }

        if (command.Data.Options?.FirstOrDefault(option => option.Name == "file")?.Value is IAttachment attachment)
        {
            await HandleUploadAsync(command, guildId, channelId, attachment).ConfigureAwait(false);
            return;
        }

        var authorization = await AstAuthorizationService.CreateDiscordContextAsync(
            guildId.ToString(CultureInfo.InvariantCulture),
            channelId.ToString(CultureInfo.InvariantCulture),
            command.User.Id,
            command.User as IGuildUser).ConfigureAwait(false);
        if (authorization == null || !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, authorization))
        {
            await command.RespondAsync(AstAuthorizationService.DeniedMessage, ephemeral: true);
            return;
        }

        var isRoom = command.Channel is IThreadChannel &&
                     await IsTrackedRoomAsync(guildId, channelId).ConfigureAwait(false);
        var session = Sessions.Start(command.User.Id, guildId, channelId, isRoom ? channelId : null);
        var view = await RenderAsync(session, authorization).ConfigureAwait(false);
        await command.RespondAsync(view.Content, embed: view.Embed, components: view.Components, ephemeral: true);
    }

    private static async Task HandleUploadAsync(
        SocketSlashCommand command,
        ulong guildId,
        ulong channelId,
        IAttachment attachment)
    {
        await command.DeferAsync(ephemeral: true).ConfigureAwait(false);
        var guildIdText = guildId.ToString(CultureInfo.InvariantCulture);
        var channelIdText = channelId.ToString(CultureInfo.InvariantCulture);
        var authorization = await AstAuthorizationService.CreateDiscordContextAsync(
            guildIdText, channelIdText, command.User.Id, command.User as IGuildUser).ConfigureAwait(false);
        if (authorization == null)
        {
            await command.FollowupAsync(AstAuthorizationService.DeniedMessage, ephemeral: true).ConfigureAwait(false);
            return;
        }

        var extension = Path.GetExtension(attachment.Filename).ToLowerInvariant();
        if (!IsUploadExtensionAvailable(extension, Declare.IsArchipelagoMode))
        {
            await command.FollowupAsync(IsFrench
                ? "Ce type de fichier est réservé au mode Archipelago. En mode Normal, `/ast file:` accepte uniquement les spoilers `.txt` et `.json`."
                : "This file type is only available in Archipelago mode. In Normal mode, `/ast file:` only accepts `.txt` and `.json` spoiler logs.",
                ephemeral: true).ConfigureAwait(false);
            return;
        }
        string result;
        switch (extension)
        {
            case ".yaml" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization):
                result = await AuditedAsync(command.User.Id, guildId, channelId, SecurityAuditAction.YamlUpload,
                    () => YamlClass.SendYaml(command, channelIdText)).ConfigureAwait(false);
                break;
            case ".apworld" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.InstanceOwner, authorization):
                result = await AuditedAsync(command.User.Id, guildId, channelId, SecurityAuditAction.ApworldUpload,
                    () => ApworldClass.SendApworld(command)).ConfigureAwait(false);
                break;
            case ".zip" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization):
                result = await AuditedAsync(command.User.Id, guildId, channelId, SecurityAuditAction.Generation,
                    () => GenerationClass.GenerateWithZip(command, channelIdText)).ConfigureAwait(false);
                break;
            case ".txt" or ".json" when
                AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization) &&
                command.Channel is IThreadChannel &&
                await IsTrackedRoomAsync(guildId, channelId).ConfigureAwait(false):
                result = await AuditedAsync(command.User.Id, guildId, channelId, SecurityAuditAction.SpoilerUpload,
                    () => SpoilerLogClass.SendSpoilerLog(command, channelIdText)).ConfigureAwait(false);
                break;
            case ".yaml" or ".apworld" or ".zip" or ".txt" or ".json":
                result = AstAuthorizationService.DeniedMessage;
                break;
            default:
                result = IsFrench
                    ? "Format non pris en charge. Utilisez un fichier `.yaml`, `.zip`, `.apworld`, `.txt` ou `.json`."
                    : "Unsupported format. Use a `.yaml`, `.zip`, `.apworld`, `.txt`, or `.json` file.";
                break;
        }
        if (!string.IsNullOrWhiteSpace(result))
            await command.FollowupAsync(Clamp(result), ephemeral: true).ConfigureAwait(false);
    }

    public static bool IsUploadExtensionAvailable(string? extension, bool archipelagoMode)
    {
        extension = extension?.Trim().ToLowerInvariant();
        return archipelagoMode || extension is not (".yaml" or ".apworld" or ".zip");
    }

    public static async Task HandleButtonAsync(SocketMessageComponent component)
    {
        if (!TryParseCustomId(component.Data.CustomId, out var sessionId, out var action)) return;
        if (component.GuildId is not { } guildId || component.ChannelId is not { } sourceChannelId ||
            !Sessions.TryGetAuthorized(sessionId, component.User.Id, guildId, sourceChannelId, out var session))
        {
            await component.RespondAsync(
                IsFrench ? "Cette interface a expiré. Relancez `/ast`." : "This interface expired. Run `/ast` again.",
                ephemeral: true);
            return;
        }

        if (action == "spoiler-configure")
        {
            await component.RespondWithModalAsync(BuildSpoilerConfigModal(session)).ConfigureAwait(false);
            return;
        }
        if (action is "alias-add-manual" or "alias-delete-manual")
        {
            await component.RespondWithModalAsync(BuildSlotAliasModal(session, action)).ConfigureAwait(false);
            return;
        }
        if (action == "selection-search")
        {
            await component.RespondWithModalAsync(BuildSelectionSearchModal(session)).ConfigureAwait(false);
            return;
        }
        if (action == "exclusion-search" &&
            session.PendingAlias != null &&
            session.PendingAction is "exclude-add" or "exclude-delete")
        {
            await component.RespondWithModalAsync(BuildExclusionSearchModal(session)).ConfigureAwait(false);
            return;
        }

        await component.DeferAsync(ephemeral: true);
        try
        {
            var authorizationChannelId = session.RoomChannelId ?? session.SourceChannelId;
            var authorization = await AstAuthorizationService.CreateDiscordContextAsync(
                guildId.ToString(CultureInfo.InvariantCulture),
                authorizationChannelId.ToString(CultureInfo.InvariantCulture),
                component.User.Id,
                component.User as IGuildUser).ConfigureAwait(false);
            if (authorization == null)
            {
                await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                return;
            }

            if (action is "output-previous" or "output-next" or "output-close")
            {
                var updated = action == "output-close"
                    ? Sessions.TrySetOutputPages(session.Id, component.User.Id, guildId, sourceChannelId, null, out session)
                    : Sessions.TryMoveOutputPage(session.Id, component.User.Id, guildId, sourceChannelId,
                        action == "output-previous" ? -1 : 1, out session);
                if (!updated)
                {
                    await SetErrorAsync(component, IsFrench ? "Cette page a expiré." : "This page expired.").ConfigureAwait(false);
                    return;
                }
                await SetViewAsync(component, await RenderAsync(session, authorization).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (action is "selection-previous" or "selection-next")
            {
                if (!CanOpen(session.Screen, authorization, session.RoomChannelId != null))
                {
                    await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                var totalItems = await GetSelectionItemCountAsync(session, authorization).ConfigureAwait(false);
                if (!Sessions.TryMoveSelectionPage(
                        session.Id,
                        component.User.Id,
                        guildId,
                        sourceChannelId,
                        action == "selection-previous" ? -1 : 1,
                        totalItems,
                        out session))
                {
                    await SetErrorAsync(component, IsFrench ? "Cette page a expiré." : "This page expired.").ConfigureAwait(false);
                    return;
                }
                await SetViewAsync(component, await RenderAsync(session, authorization).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (action == "selection-clear-search")
            {
                if (!CanOpen(session.Screen, authorization, session.RoomChannelId != null))
                {
                    await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                if (!Sessions.TrySetSelectionSearch(
                        session.Id, component.User.Id, guildId, sourceChannelId, null, out session))
                {
                    await SetErrorAsync(component, IsFrench ? "Cette recherche a expiré." : "This search expired.").ConfigureAwait(false);
                    return;
                }
                await SetViewAsync(component, await RenderAsync(session, authorization).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (action is "exclusion-previous" or "exclusion-next")
            {
                if (session.RoomChannelId is not { } exclusionRoom ||
                    session.PendingAlias == null ||
                    session.PendingAction is not ("exclude-add" or "exclude-delete") ||
                    !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, authorization))
                {
                    await SetErrorAsync(component, IsFrench ? "Sélection expirée." : "Selection expired.").ConfigureAwait(false);
                    return;
                }
                var choices = await GetExclusionChoicesAsync(
                    session,
                    guildId.ToString(CultureInfo.InvariantCulture),
                    exclusionRoom.ToString(CultureInfo.InvariantCulture),
                    component.User.Id.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                if (!Sessions.TryMoveExclusionPage(
                        session.Id,
                        component.User.Id,
                        guildId,
                        sourceChannelId,
                        action == "exclusion-previous" ? -1 : 1,
                        choices.Count,
                        out session))
                {
                    await SetErrorAsync(component, IsFrench ? "Cette page a expiré." : "This page expired.").ConfigureAwait(false);
                    return;
                }
                await SetViewAsync(component, await RenderExclusionsAsync(session).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (action == "exclusion-clear-search")
            {
                if (!Sessions.TrySetExclusionSearch(
                        session.Id, component.User.Id, guildId, sourceChannelId, null, out session))
                {
                    await SetErrorAsync(component, IsFrench ? "Cette recherche a expiré." : "This search expired.").ConfigureAwait(false);
                    return;
                }
                await SetViewAsync(component, await RenderExclusionsAsync(session).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (action == "admin-setup")
            {
                if (!AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization))
                {
                    await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                await AstSetupWizard.StartFromCommandCenterAsync(component).ConfigureAwait(false);
                return;
            }

            if (action is "personal-portal-revoke-request" or "room-portal-revoke-request" or "admin-portal-revoke-request" or
                "confirm-portal-revoke" or "cancel-portal-revoke")
            {
                if (action.EndsWith("-request", StringComparison.Ordinal))
                {
                    var pending = action switch
                    {
                        "personal-portal-revoke-request" => "revoke-personal-portal",
                        "room-portal-revoke-request" => "revoke-room-portal",
                        _ => "revoke-admin-portal"
                    };
                    var required = pending switch
                    {
                        "revoke-room-portal" => AstAuthorizationLevel.RoomManager,
                        "revoke-admin-portal" => AstAuthorizationLevel.GuildManager,
                        _ => AstAuthorizationLevel.GuildMember
                    };
                    if (!AstAuthorizationService.IsAllowed(required, authorization))
                    {
                        await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                        return;
                    }
                    Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, pending, null, out session);
                    await SetViewAsync(component, await RenderAsync(session, authorization).ConfigureAwait(false)).ConfigureAwait(false);
                    return;
                }
                if (action == "cancel-portal-revoke")
                {
                    Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, null, null, out session);
                    await SetViewAsync(component, await RenderAsync(session, authorization).ConfigureAwait(false)).ConfigureAwait(false);
                    return;
                }
                var requiredLevel = session.PendingAction switch
                {
                    "revoke-room-portal" => AstAuthorizationLevel.RoomManager,
                    "revoke-admin-portal" => AstAuthorizationLevel.GuildManager,
                    "revoke-personal-portal" => AstAuthorizationLevel.GuildMember,
                    _ => (AstAuthorizationLevel?)null
                };
                var portalChannel = session.PendingAction == "revoke-admin-portal"
                    ? session.SourceChannelId
                    : session.RoomChannelId;
                if (requiredLevel == null || portalChannel == null || !AstAuthorizationService.IsAllowed(requiredLevel.Value, authorization))
                {
                    await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                await AuditedAsync(session, portalChannel.Value, SecurityAuditAction.PortalAccessRevoke,
                    () => PortalAccessCommands.RevokePortalTokenAsync(
                        guildId.ToString(CultureInfo.InvariantCulture), portalChannel.Value.ToString(CultureInfo.InvariantCulture),
                        component.User.Id.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
                Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, null, null, out session);
                var portalView = await RenderAsync(session, authorization).ConfigureAwait(false);
                await component.ModifyOriginalResponseAsync(p =>
                {
                    p.Content = IsFrench ? "Le lien du portail a été révoqué." : "The portal link was revoked.";
                    p.Embed = portalView.Embed;
                    p.Components = portalView.Components;
                }).ConfigureAwait(false);
                return;
            }

            if (action is "clean-all-request" or "confirm-clean" or "cancel-pending")
            {
                if (session.RoomChannelId is not { } cleanupRoom ||
                    !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, authorization))
                {
                    await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                if (action == "clean-all-request")
                    Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, "clean-all", null, out session);
                else if (action == "cancel-pending")
                    Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, null, null, out session);
                else
                {
                    var guildText = guildId.ToString(CultureInfo.InvariantCulture);
                    var roomText = cleanupRoom.ToString(CultureInfo.InvariantCulture);
                    var userText = component.User.Id.ToString(CultureInfo.InvariantCulture);
                    string result;
                    if (session.PendingAction == "clean-all")
                    {
                        await AuditedAsync(session, cleanupRoom, SecurityAuditAction.DataCleanup,
                            () => RecapListCommands.DeleteAliasAndItemsForUserIdAsync(guildText, roomText, userText)).ConfigureAwait(false);
                        result = IsFrench ? "Tous vos récaps ont été vidés." : "All your recaps were cleared.";
                    }
                    else if (session.PendingAlias != null && session.PendingAction is "clean" or "recap-clean")
                    {
                        var recap = session.PendingAction == "recap-clean"
                            ? await BuildPersonalItemsAsync(guildText, roomText, component.User.Id, session.PendingAlias).ConfigureAwait(false)
                            : string.Empty;
                        await AuditedAsync(session, cleanupRoom, SecurityAuditAction.DataCleanup,
                            () => RecapListCommands.DeleteRecapListAsync(guildText, roomText, userText, session.PendingAlias)).ConfigureAwait(false);
                        result = string.IsNullOrWhiteSpace(recap)
                            ? (IsFrench ? $"Récap de **{Safe(session.PendingAlias)}** vidé." : $"Recap for **{Safe(session.PendingAlias)}** cleared.")
                            : recap + (IsFrench ? "\n\nRécap vidé." : "\n\nRecap cleared.");
                    }
                    else result = IsFrench ? "Confirmation expirée." : "Confirmation expired.";
                    Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, null, null, out session);
                    var advanced = await RenderAdvancedAsync(session).ConfigureAwait(false);
                    await component.ModifyOriginalResponseAsync(p => { p.Content = Clamp(result); p.Embed = advanced.Embed; p.Components = advanced.Components; }).ConfigureAwait(false);
                    return;
                }
                var confirmation = await RenderAdvancedAsync(session).ConfigureAwait(false);
                await SetViewAsync(component, confirmation).ConfigureAwait(false);
                return;
            }

            if (action is "confirm-exclusion-delete" or "cancel-exclusion")
            {
                if (session.RoomChannelId is not { } exclusionRoom ||
                    !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, authorization))
                {
                    await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                var result = string.Empty;
                if (action == "confirm-exclusion-delete" && session.PendingAlias != null && session.PendingItem != null)
                {
                    result = await AuditedAsync(session, exclusionRoom, SecurityAuditAction.RoomSettingsUpdate,
                        () => ExcludedItemsCommands.DeleteExcludedItemForUserAsync(
                            guildId.ToString(CultureInfo.InvariantCulture), exclusionRoom.ToString(CultureInfo.InvariantCulture),
                            component.User.Id.ToString(CultureInfo.InvariantCulture), session.PendingAlias, session.PendingItem)).ConfigureAwait(false);
                }
                Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, null, null, out session);
                var exclusions = await RenderExclusionsAsync(session).ConfigureAwait(false);
                await component.ModifyOriginalResponseAsync(p => { p.Content = string.IsNullOrWhiteSpace(result) ? null : result; p.Embed = exclusions.Embed; p.Components = exclusions.Components; }).ConfigureAwait(false);
                return;
            }

            if (action is "delete-room-request" or "confirm-delete-room" or "cancel-delete-room")
            {
                if (!AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization) || session.RoomChannelId is not { } deleteRoom)
                {
                    await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                if (action == "delete-room-request")
                {
                    Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, "delete-room", null, out session);
                    await SetViewAsync(component, RenderManageMore(session)).ConfigureAwait(false);
                    return;
                }
                if (action == "cancel-delete-room")
                {
                    Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, null, null, out session);
                    await SetViewAsync(component, RenderManageMore(session)).ConfigureAwait(false);
                    return;
                }
                var result = await AuditedAsync(session, deleteRoom, SecurityAuditAction.RoomDelete,
                    () => UrlClass.DeleteUrl(
                        component.User as IGuildUser,
                        deleteRoom.ToString(CultureInfo.InvariantCulture),
                        guildId.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
                await component.ModifyOriginalResponseAsync(p =>
                {
                    p.Content = result;
                    p.Embed = null;
                    p.Components = new ComponentBuilder().Build();
                }).ConfigureAwait(false);
                return;
            }

            if (action is "spoiler-analyze" or "spoiler-reset-validation")
            {
                if (!AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization) ||
                    session.RoomChannelId is not { } spoilerRoom || string.IsNullOrWhiteSpace(session.SpoilerAlias))
                {
                    await SetErrorAsync(component, string.IsNullOrWhiteSpace(session.SpoilerAlias)
                        ? (IsFrench ? "Choisissez d’abord un slot à analyser." : "Choose a slot to analyze first.")
                        : AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                var result = await SpoilerAnalysisClass.AnalyzeSpoilerLogAsync(
                    spoilerRoom.ToString(CultureInfo.InvariantCulture),
                    guildId.ToString(CultureInfo.InvariantCulture),
                    session.SpoilerAlias,
                    session.SpoilerSphereLimit,
                    session.SpoilerMissingMode == "full",
                    session.SpoilerHideItems,
                    resetValidation: action == "spoiler-reset-validation").ConfigureAwait(false);
                await ShowOutcomeAsync(component, session, authorization, result).ConfigureAwait(false);
                return;
            }

            if (action is "yaml-backup" or "apworld-backup")
            {
                var allowed = action == "yaml-backup"
                    ? AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization)
                    : AstAuthorizationService.IsAllowed(AstAuthorizationLevel.InstanceOwner, authorization);
                if (!allowed)
                {
                    await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                var fileName = action == "yaml-backup"
                    ? $"backup_yaml_{session.SourceChannelId}.zip"
                    : "backup_apworld.zip";
                var tempPath = Path.Combine(Path.GetTempPath(), $"ast-{Guid.NewGuid():N}-{fileName}");
                try
                {
                    var backupAuditAction = action == "yaml-backup" ? SecurityAuditAction.YamlBackup : SecurityAuditAction.ApworldBackup;
                    var error = await AuditedAsync(session, session.SourceChannelId, backupAuditAction, () =>
                        action == "yaml-backup"
                            ? YamlClass.BackupYamlsToFileAsync(session.SourceChannelId.ToString(CultureInfo.InvariantCulture), tempPath)
                            : ApworldClass.BackupApworldToFileAsync(tempPath)).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(error) || !File.Exists(tempPath))
                    {
                        await SetErrorAsync(component, string.IsNullOrWhiteSpace(error) ? Unavailable() : error).ConfigureAwait(false);
                        return;
                    }
                    await component.FollowupWithFileAsync(tempPath, fileName,
                        text: IsFrench ? "Sauvegarde privée prête." : "Private backup ready.", ephemeral: true).ConfigureAwait(false);
                    await SetErrorAsync(component, IsFrench ? "La sauvegarde a été envoyée ci-dessous." : "The backup was sent below.").ConfigureAwait(false);
                }
                finally
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                return;
            }

            if (action is "yaml-clean-request" or "yaml-confirm-clean" or "yaml-cancel")
            {
                if (!AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization))
                {
                    await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                string? result = null;
                if (action == "yaml-clean-request")
                    Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, "yaml-clean", null, out session);
                else if (action == "yaml-confirm-clean" && session.PendingAction == "yaml-clean")
                {
                    result = await AuditedAsync(session, session.SourceChannelId, SecurityAuditAction.YamlCleanup,
                        () => Task.FromResult(YamlClass.CleanYamls(session.SourceChannelId.ToString(CultureInfo.InvariantCulture)))).ConfigureAwait(false);
                    Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, null, null, out session);
                }
                else
                    Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, null, null, out session);
                var yamlView = await RenderYamlAsync(session).ConfigureAwait(false);
                await component.ModifyOriginalResponseAsync(p =>
                {
                    p.Content = string.IsNullOrWhiteSpace(result) ? null : Clamp(result);
                    p.Embed = yamlView.Embed;
                    p.Components = yamlView.Components;
                }).ConfigureAwait(false);
                return;
            }

            if (action is "yaml-confirm-delete" or "yaml-cancel-delete")
            {
                if (!AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization))
                {
                    await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                var result = action == "yaml-confirm-delete" && session.PendingAction == "yaml-delete" && session.PendingItem != null
                    ? await AuditedAsync(session, session.SourceChannelId, SecurityAuditAction.YamlDelete,
                        () => Task.FromResult(YamlClass.DeleteYamlByName(
                            session.SourceChannelId.ToString(CultureInfo.InvariantCulture), session.PendingItem))).ConfigureAwait(false)
                    : null;
                Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, null, null, out session);
                var yamlView = await RenderYamlAsync(session).ConfigureAwait(false);
                await component.ModifyOriginalResponseAsync(p =>
                {
                    p.Content = string.IsNullOrWhiteSpace(result) ? null : Clamp(result);
                    p.Embed = yamlView.Embed;
                    p.Components = yamlView.Components;
                }).ConfigureAwait(false);
                return;
            }

            if (action is "generation-run" or "generation-test")
            {
                if (!AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization))
                {
                    await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                var generationChannel = session.SourceChannelId.ToString(CultureInfo.InvariantCulture);
                if (action == "generation-test")
                {
                    var result = await AuditedAsync(session, session.SourceChannelId, SecurityAuditAction.Generation,
                        () => GenerationClass.TestGenerateAsyncForWeb(generationChannel)).ConfigureAwait(false);
                    await SetErrorAsync(component, Clamp(result)).ConfigureAwait(false);
                    return;
                }
                var generation = await AuditedAsync(session, session.SourceChannelId, SecurityAuditAction.Generation,
                    () => GenerationClass.GenerateAsyncForWeb(
                        generationChannel, session.GenerationSkipProgBalancing)).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(generation.Message) || string.IsNullOrWhiteSpace(generation.ZipPath) || !File.Exists(generation.ZipPath))
                {
                    await SetErrorAsync(component, string.IsNullOrWhiteSpace(generation.Message) ? Unavailable() : Clamp(generation.Message)).ConfigureAwait(false);
                    return;
                }
                await component.FollowupWithFileAsync(generation.ZipPath, Path.GetFileName(generation.ZipPath),
                    text: IsFrench ? "Génération privée terminée." : "Private generation completed.", ephemeral: true).ConfigureAwait(false);
                await SetErrorAsync(component, IsFrench ? "La génération a été envoyée ci-dessous." : "The generation was sent below.").ConfigureAwait(false);
                return;
            }

            if (TryScreen(action, out var screen))
            {
                if (!CanOpen(screen, authorization, session.RoomChannelId != null) ||
                    !Sessions.TryUpdateScreen(session.Id, component.User.Id, guildId, sourceChannelId, screen, out session))
                {
                    await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                var view = await RenderAsync(session, authorization).ConfigureAwait(false);
                await SetViewAsync(component, view).ConfigureAwait(false);
                return;
            }

            var auditAction = AuditForUiAction(action, authorization);
            var outcome = auditAction == null
                ? await ExecuteImmediateActionAsync(action, session, authorization).ConfigureAwait(false)
                : await AuditedAsync(session, session.RoomChannelId ?? session.SourceChannelId, auditAction.Value,
                    () => ExecuteImmediateActionAsync(action, session, authorization)).ConfigureAwait(false);
            if (outcome == null)
            {
                await SetErrorAsync(component, IsFrench ? "Action inconnue." : "Unknown action.").ConfigureAwait(false);
                return;
            }

            await ShowOutcomeAsync(component, session, authorization, outcome).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[AstUI] Interaction failed ({exception.GetType().Name}).");
            await SetErrorAsync(component, IsFrench
                ? "Le centre de commandes est temporairement indisponible."
                : "The command center is temporarily unavailable.").ConfigureAwait(false);
        }
    }

    public static async Task HandleSelectMenuAsync(SocketMessageComponent component)
    {
        if (!TryParseCustomId(component.Data.CustomId, out var sessionId, out var action) ||
            action is not ("select-room" or "poll-policy" or "notifications" or "alias-add" or "alias-delete" or "alias-filter" or "clean-select" or "recap-clean-select" or "exclude-add-alias" or "exclude-delete-alias" or "exclude-item-add" or "exclude-item-delete" or "spoiler-alias" or "spoiler-mode" or "spoiler-hide" or "yaml-delete-select" or "yaml-template-download" or "generation-skip"))
            return;
        if (component.GuildId is not { } guildId || component.ChannelId is not { } sourceChannelId ||
            component.Data.Values.FirstOrDefault() is not { } selected ||
            !Sessions.TryGetAuthorized(sessionId, component.User.Id, guildId, sourceChannelId, out var session))
        {
            await component.RespondAsync(IsFrench ? "Cette interface a expiré. Relancez `/ast`." : "This interface expired. Run `/ast` again.", ephemeral: true);
            return;
        }

        await component.DeferAsync(ephemeral: true);
        if (action == "generation-skip")
        {
            var generationAuthorization = await AstAuthorizationService.CreateDiscordContextAsync(
                guildId.ToString(CultureInfo.InvariantCulture), sourceChannelId.ToString(CultureInfo.InvariantCulture),
                component.User.Id, component.User as IGuildUser).ConfigureAwait(false);
            if (generationAuthorization == null ||
                !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, generationAuthorization) ||
                !bool.TryParse(selected, out var skip) ||
                !Sessions.TrySetGenerationSkipProgBalancing(session.Id, component.User.Id, guildId, sourceChannelId, skip, out session))
            {
                await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                return;
            }
            await SetViewAsync(component, RenderGeneration(session)).ConfigureAwait(false);
            return;
        }
        if (action is "yaml-delete-select" or "yaml-template-download")
        {
            var yamlAuthorization = await AstAuthorizationService.CreateDiscordContextAsync(
                guildId.ToString(CultureInfo.InvariantCulture), sourceChannelId.ToString(CultureInfo.InvariantCulture),
                component.User.Id, component.User as IGuildUser).ConfigureAwait(false);
            if (yamlAuthorization == null || !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, yamlAuthorization))
            {
                await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                return;
            }
            if (action == "yaml-delete-select")
            {
                Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, "yaml-delete", null, out session, selected);
                await SetViewAsync(component, await RenderYamlAsync(session).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }
            var tempPath = Path.Combine(Path.GetTempPath(), $"ast-{Guid.NewGuid():N}-{Path.GetFileName(selected)}");
            try
            {
                var error = await AuditedAsync(session, session.SourceChannelId, SecurityAuditAction.YamlDownload,
                    () => Task.FromResult(YamlClass.DownloadTemplateToFile(selected, tempPath))).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(error) || !File.Exists(tempPath))
                {
                    await SetErrorAsync(component, string.IsNullOrWhiteSpace(error) ? Unavailable() : error).ConfigureAwait(false);
                    return;
                }
                await component.FollowupWithFileAsync(tempPath, Path.GetFileName(selected),
                    text: IsFrench ? "Modèle YAML privé." : "Private YAML template.", ephemeral: true).ConfigureAwait(false);
                await SetErrorAsync(component, IsFrench ? "Le modèle a été envoyé ci-dessous." : "The template was sent below.").ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            return;
        }
        if (action is "spoiler-alias" or "spoiler-mode" or "spoiler-hide")
        {
            if (session.RoomChannelId is not { } spoilerRoom)
            {
                await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                return;
            }
            var managerAuthorization = await AstAuthorizationService.CreateDiscordContextAsync(
                guildId.ToString(CultureInfo.InvariantCulture), spoilerRoom.ToString(CultureInfo.InvariantCulture),
                component.User.Id, component.User as IGuildUser).ConfigureAwait(false);
            if (managerAuthorization == null || !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, managerAuthorization))
            {
                await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                return;
            }
            var updated = action switch
            {
                "spoiler-alias" => Sessions.TrySetSpoilerOptions(session.Id, component.User.Id, guildId, sourceChannelId, out session, alias: selected, setAlias: true),
                "spoiler-mode" => Sessions.TrySetSpoilerOptions(session.Id, component.User.Id, guildId, sourceChannelId, out session, missingMode: selected),
                _ => bool.TryParse(selected, out var hide) && Sessions.TrySetSpoilerOptions(session.Id, component.User.Id, guildId, sourceChannelId, out session, hideItems: hide)
            };
            if (!updated)
            {
                await SetErrorAsync(component, IsFrench ? "Réglage invalide." : "Invalid setting.").ConfigureAwait(false);
                return;
            }
            await SetViewAsync(component, await RenderSpoilerAnalysisAsync(session).ConfigureAwait(false)).ConfigureAwait(false);
            return;
        }
        if (action is "exclude-add-alias" or "exclude-delete-alias" or "exclude-item-add" or "exclude-item-delete")
        {
            if (session.RoomChannelId is not { } exclusionRoom)
            {
                await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                return;
            }
            var guildText = guildId.ToString(CultureInfo.InvariantCulture);
            var roomText = exclusionRoom.ToString(CultureInfo.InvariantCulture);
            var userText = component.User.Id.ToString(CultureInfo.InvariantCulture);
            var memberAuthorization = await AstAuthorizationService.CreateDiscordContextAsync(
                guildText, roomText, component.User.Id, component.User as IGuildUser).ConfigureAwait(false);
            if (memberAuthorization == null ||
                !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, memberAuthorization))
            {
                await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                return;
            }
            string? result = null;
            if (action == "exclude-add-alias")
                Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, "exclude-add", selected, out session);
            else if (action == "exclude-delete-alias")
                Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, "exclude-delete", selected, out session);
            else if (action == "exclude-item-add" && session.PendingAlias != null)
            {
                result = await AuditedAsync(session, exclusionRoom, SecurityAuditAction.RoomSettingsUpdate,
                    () => ExcludedItemsCommands.AddExcludedItemForUserAsync(
                        guildText, roomText, userText, session.PendingAlias, selected)).ConfigureAwait(false);
                Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, null, null, out session);
            }
            else if (action == "exclude-item-delete" && session.PendingAlias != null)
                Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, "exclude-delete-confirm", session.PendingAlias, out session, selected);
            var exclusions = await RenderExclusionsAsync(session).ConfigureAwait(false);
            await component.ModifyOriginalResponseAsync(p => { p.Content = result; p.Embed = exclusions.Embed; p.Components = exclusions.Components; }).ConfigureAwait(false);
            return;
        }
        if (action is "clean-select" or "recap-clean-select")
        {
            var pending = action == "clean-select" ? "clean" : "recap-clean";
            if (!Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, pending, selected, out session))
            {
                await SetErrorAsync(component, IsFrench ? "Sélection expirée." : "Selection expired.").ConfigureAwait(false);
                return;
            }
            var confirmation = await RenderAdvancedAsync(session).ConfigureAwait(false);
            await SetViewAsync(component, confirmation).ConfigureAwait(false);
            return;
        }
        if (action is "alias-add" or "alias-delete" or "alias-filter")
        {
            if (session.RoomChannelId is not { } aliasRoomId)
            {
                await SetErrorAsync(component, IsFrench ? "Aucune room sélectionnée." : "No room selected.").ConfigureAwait(false);
                return;
            }
            var guildText = guildId.ToString(CultureInfo.InvariantCulture);
            var roomText = aliasRoomId.ToString(CultureInfo.InvariantCulture);
            var memberAuthorization = await AstAuthorizationService.CreateDiscordContextAsync(
                guildText, roomText, component.User.Id, component.User as IGuildUser).ConfigureAwait(false);
            if (memberAuthorization == null || !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, memberAuthorization))
            {
                await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                return;
            }
            string result;
            if (action == "alias-filter")
            {
                result = Sessions.TrySetAliasMentionFlag(session.Id, component.User.Id, guildId, sourceChannelId, selected, out session)
                    ? (IsFrench ? "Filtre de mentions mis à jour." : "Mention filter updated.")
                    : (IsFrench ? "Filtre invalide." : "Invalid filter.");
            }
            else if (action == "alias-add")
            {
                result = await AuditedAsync(session, aliasRoomId, SecurityAuditAction.AliasAdd,
                    () => AliasClass.AddAliasForUserAsync(
                        selected, session.AliasMentionFlag, roomText, guildText,
                        component.User.Id.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
            }
            else
            {
                result = await AuditedAsync(session, aliasRoomId, SecurityAuditAction.AliasDelete,
                    () => AliasClass.DeleteAliasForUserAsync(
                        selected, roomText, guildText, component.User.Id.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
            }
            var slotsView = await RenderSlotsAsync(session).ConfigureAwait(false);
            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = result;
                properties.Embed = slotsView.Embed;
                properties.Components = slotsView.Components;
            }).ConfigureAwait(false);
            return;
        }

        if (action is "poll-policy" or "notifications")
        {
            if (session.RoomChannelId is not { } selectedRoomId)
            {
                await SetErrorAsync(component, IsFrench ? "Aucune room sélectionnée." : "No room selected.").ConfigureAwait(false);
                return;
            }
            var guildText = guildId.ToString(CultureInfo.InvariantCulture);
            var roomText = selectedRoomId.ToString(CultureInfo.InvariantCulture);
            var managerAuthorization = await AstAuthorizationService.CreateDiscordContextAsync(
                guildText, roomText, component.User.Id, component.User as IGuildUser).ConfigureAwait(false);
            if (managerAuthorization == null || !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, managerAuthorization))
            {
                await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                return;
            }
            string result;
            if (action == "poll-policy")
            {
                var parts = selected.Split('|', 2);
                result = parts.Length == 2
                    ? await AuditedAsync(session, selectedRoomId, SecurityAuditAction.RoomSettingsUpdate,
                        () => ChannelsAndUrlsCommands.UpdatePollingPolicyFromWeb(parts[0], parts[1], roomText, guildText)).ConfigureAwait(false)
                    : (IsFrench ? "Choix de polling invalide." : "Invalid polling choice.");
            }
            else
            {
                result = await AuditedAsync(session, selectedRoomId, SecurityAuditAction.RoomSettingsUpdate,
                    () => ChannelsAndUrlsCommands.UpdateSilentOptionFromWeb(selected, roomText, guildText)).ConfigureAwait(false);
            }
            var refreshed = await RenderAsync(session, managerAuthorization).ConfigureAwait(false);
            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = result;
                properties.Embed = refreshed.Embed;
                properties.Components = refreshed.Components;
            }).ConfigureAwait(false);
            return;
        }

        if (!ulong.TryParse(selected, out var roomChannelId))
        {
            await SetErrorAsync(component, IsFrench ? "Sélection invalide." : "Invalid selection.").ConfigureAwait(false);
            return;
        }
        var guildIdText = guildId.ToString(CultureInfo.InvariantCulture);
        var roomIdText = roomChannelId.ToString(CultureInfo.InvariantCulture);
        if (!await IsTrackedRoomAsync(guildId, roomChannelId).ConfigureAwait(false))
        {
            await SetErrorAsync(component, IsFrench ? "Cette room n’est plus suivie." : "This room is no longer tracked.").ConfigureAwait(false);
            return;
        }

        var authorization = await AstAuthorizationService.CreateDiscordContextAsync(
            guildIdText, roomIdText, component.User.Id, component.User as IGuildUser).ConfigureAwait(false);
        if (authorization == null || !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, authorization) ||
            !Sessions.TrySelectRoom(session.Id, component.User.Id, guildId, sourceChannelId, roomChannelId, out session))
        {
            await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
            return;
        }

        var view = await RenderAsync(session, authorization).ConfigureAwait(false);
        await SetViewAsync(component, view).ConfigureAwait(false);
    }

    public static async Task HandleModalAsync(SocketModal modal)
    {
        if (!TryParseCustomId(modal.Data.CustomId, out var sessionId, out var action) ||
            action is not ("spoiler-configure" or "alias-add-manual" or "alias-delete-manual" or "exclusion-search" or "selection-search"))
            return;
        if (modal.GuildId is not { } guildId || modal.ChannelId is not { } sourceChannelId ||
            !Sessions.TryGetAuthorized(sessionId, modal.User.Id, guildId, sourceChannelId, out var session))
        {
            await modal.RespondAsync(IsFrench ? "Cette interface a expiré. Relancez `/ast`." : "This interface expired. Run `/ast` again.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var authorizationChannelId = session.RoomChannelId ?? session.SourceChannelId;
        var authorization = await AstAuthorizationService.CreateDiscordContextAsync(
            guildId.ToString(CultureInfo.InvariantCulture), authorizationChannelId.ToString(CultureInfo.InvariantCulture),
            modal.User.Id, modal.User as IGuildUser).ConfigureAwait(false);
        var requiredLevel = action == "spoiler-configure"
            ? AstAuthorizationLevel.RoomManager
            : AstAuthorizationLevel.GuildMember;
        if (authorization == null || !AstAuthorizationService.IsAllowed(requiredLevel, authorization) ||
            action == "selection-search" && !CanOpen(session.Screen, authorization, session.RoomChannelId != null))
        {
            await modal.RespondAsync(AstAuthorizationService.DeniedMessage, ephemeral: true).ConfigureAwait(false);
            return;
        }

        if (action == "selection-search")
        {
            var search = modal.Data.Components
                .FirstOrDefault(component => component.CustomId == SelectionSearchInputId)?.Value;
            if (!Sessions.TrySetSelectionSearch(
                    session.Id, modal.User.Id, guildId, sourceChannelId, search, out session))
            {
                await modal.RespondAsync(IsFrench ? "Recherche invalide." : "Invalid search.", ephemeral: true).ConfigureAwait(false);
                return;
            }
            var filteredView = await RenderAsync(session, authorization).ConfigureAwait(false);
            await modal.UpdateAsync(properties =>
            {
                properties.Content = null;
                properties.Embed = filteredView.Embed;
                properties.Components = filteredView.Components;
            }).ConfigureAwait(false);
            return;
        }

        if (session.RoomChannelId is not { } roomChannelId)
        {
            await modal.RespondAsync(IsFrench ? "Cette interface a expiré. Relancez `/ast`." : "This interface expired. Run `/ast` again.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        if (action == "exclusion-search")
        {
            if (session.PendingAlias == null || session.PendingAction is not ("exclude-add" or "exclude-delete"))
            {
                await modal.RespondAsync(IsFrench ? "Cette recherche a expiré." : "This search expired.", ephemeral: true).ConfigureAwait(false);
                return;
            }
            var search = modal.Data.Components
                .FirstOrDefault(component => component.CustomId == ExclusionSearchInputId)?.Value;
            if (!Sessions.TrySetExclusionSearch(
                    session.Id, modal.User.Id, guildId, sourceChannelId, search, out session))
            {
                await modal.RespondAsync(IsFrench ? "Recherche invalide." : "Invalid search.", ephemeral: true).ConfigureAwait(false);
                return;
            }
            var exclusions = await RenderExclusionsAsync(session).ConfigureAwait(false);
            await modal.UpdateAsync(properties =>
            {
                properties.Content = null;
                properties.Embed = exclusions.Embed;
                properties.Components = exclusions.Components;
            }).ConfigureAwait(false);
            return;
        }

        if (action is "alias-add-manual" or "alias-delete-manual")
        {
            var requestedAlias = modal.Data.Components.FirstOrDefault(c => c.CustomId == SlotAliasInputId)?.Value?.Trim();
            var slotAliases = await AliasChoicesCommands.GetAliasesForGuildAndChannelAsync(
                guildId.ToString(CultureInfo.InvariantCulture), roomChannelId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            var canonicalSlotAlias = slotAliases.FirstOrDefault(value => string.Equals(value, requestedAlias, StringComparison.OrdinalIgnoreCase));
            if (canonicalSlotAlias == null)
            {
                await modal.RespondAsync(IsFrench ? "Ce slot n’existe pas dans cette room." : "This slot does not exist in this room.", ephemeral: true).ConfigureAwait(false);
                return;
            }
            var slotResult = action == "alias-add-manual"
                ? await AuditedAsync(session, roomChannelId, SecurityAuditAction.AliasAdd,
                    () => AliasClass.AddAliasForUserAsync(
                        canonicalSlotAlias, session.AliasMentionFlag, roomChannelId.ToString(CultureInfo.InvariantCulture),
                        guildId.ToString(CultureInfo.InvariantCulture), modal.User.Id.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false)
                : await AuditedAsync(session, roomChannelId, SecurityAuditAction.AliasDelete,
                    () => AliasClass.DeleteAliasForUserAsync(
                        canonicalSlotAlias, roomChannelId.ToString(CultureInfo.InvariantCulture),
                        guildId.ToString(CultureInfo.InvariantCulture), modal.User.Id.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
            var slotView = await RenderSlotsAsync(session).ConfigureAwait(false);
            await modal.UpdateAsync(properties =>
            {
                properties.Content = PaginateOutput(slotResult)[0];
                properties.Embed = slotView.Embed;
                properties.Components = slotView.Components;
            }).ConfigureAwait(false);
            return;
        }

        var alias = modal.Data.Components.FirstOrDefault(c => c.CustomId == SpoilerAliasInputId)?.Value?.Trim();
        var sphereRaw = modal.Data.Components.FirstOrDefault(c => c.CustomId == SpoilerSphereInputId)?.Value?.Trim();
        var validateRaw = modal.Data.Components.FirstOrDefault(c => c.CustomId == SpoilerValidateInputId)?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(alias) || !TryOptionalNonNegativeInt(sphereRaw, out var sphereLimit) ||
            !TryOptionalNonNegativeInt(validateRaw, out var sphereToValidate))
        {
            await modal.RespondAsync(IsFrench
                ? "Indiquez un slot et utilisez uniquement des entiers positifs ou zéro pour les sphères."
                : "Enter a slot and use only non-negative integers for spheres.", ephemeral: true).ConfigureAwait(false);
            return;
        }
        var aliases = await AliasChoicesCommands.GetAliasesForGuildAndChannelAsync(
            guildId.ToString(CultureInfo.InvariantCulture), roomChannelId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        var canonicalAlias = aliases.FirstOrDefault(value => string.Equals(value, alias, StringComparison.OrdinalIgnoreCase));
        if (canonicalAlias == null)
        {
            await modal.RespondAsync(IsFrench ? "Ce slot n’existe pas dans cette room." : "This slot does not exist in this room.", ephemeral: true).ConfigureAwait(false);
            return;
        }
        if (!Sessions.TrySetSpoilerOptions(
                session.Id, modal.User.Id, guildId, sourceChannelId, out session,
                alias: canonicalAlias, setAlias: true, sphereLimit: sphereLimit, setSphereLimit: true))
        {
            await modal.RespondAsync(IsFrench ? "Cette interface a expiré." : "This interface expired.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var result = sphereToValidate.HasValue
            ? await SpoilerAnalysisClass.AnalyzeSpoilerLogAsync(
                roomChannelId.ToString(CultureInfo.InvariantCulture), guildId.ToString(CultureInfo.InvariantCulture),
                canonicalAlias, sphereLimit, session.SpoilerMissingMode == "full", session.SpoilerHideItems,
                sphereToValidate).ConfigureAwait(false)
            : (IsFrench ? "Réglages d’analyse mis à jour." : "Analysis settings updated.");
        var pages = PaginateOutput(result);
        AstUiView view;
        if (pages.Count > 1 && Sessions.TrySetOutputPages(
                session.Id, modal.User.Id, guildId, sourceChannelId, pages, out session))
            view = RenderPagedOutput(session);
        else
            view = await RenderSpoilerAnalysisAsync(session).ConfigureAwait(false);
        await modal.UpdateAsync(properties =>
        {
            properties.Content = pages[0];
            properties.Embed = view.Embed;
            properties.Components = view.Components;
        }).ConfigureAwait(false);
    }

    public static bool TryParseCustomId(string? customId, out string sessionId, out string action)
    {
        sessionId = string.Empty;
        action = string.Empty;
        if (string.IsNullOrWhiteSpace(customId)) return false;
        var parts = customId.Split(':', 3);
        if (parts.Length != 3 || !string.Equals(parts[0], CustomIdPrefix, StringComparison.Ordinal) ||
            parts[1].Length != 32 || parts[2].Length == 0)
        {
            return false;
        }
        sessionId = parts[1];
        action = parts[2];
        return true;
    }

    private static async Task<AstUiView> RenderAsync(AstUiSession session, AstAuthorizationContext authorization)
    {
        if (session.OutputPages is { Count: > 0 }) return RenderPagedOutput(session);
        var roomName = session.RoomChannelId is { } roomId
            ? (Declare.Client.GetChannel(roomId) as IChannel)?.Name
            : null;
        return session.Screen switch
        {
            AstUiScreen.Home => await RenderHomeAsync(session, authorization, roomName).ConfigureAwait(false),
            AstUiScreen.Personal => RenderPersonal(session),
            AstUiScreen.Room => await RenderRoomAsync(session, roomName).ConfigureAwait(false),
            AstUiScreen.Manage => RenderManage(session),
            AstUiScreen.Administration => RenderAdministration(session, authorization),
            AstUiScreen.Help => RenderHelp(session),
            AstUiScreen.Polling => RenderPolling(session),
            AstUiScreen.ManageMore => RenderManageMore(session),
            AstUiScreen.Yaml => await RenderYamlAsync(session).ConfigureAwait(false),
            AstUiScreen.Generation => RenderGeneration(session),
            AstUiScreen.Apworld => RenderApworld(session),
            AstUiScreen.Slots => await RenderSlotsAsync(session).ConfigureAwait(false),
            AstUiScreen.Advanced => await RenderAdvancedAsync(session).ConfigureAwait(false),
            AstUiScreen.Exclusions => await RenderExclusionsAsync(session).ConfigureAwait(false),
            AstUiScreen.SpoilerAnalysis => await RenderSpoilerAnalysisAsync(session).ConfigureAwait(false),
            _ => await RenderHomeAsync(session, authorization, roomName).ConfigureAwait(false)
        };
    }

    private static async Task<AstUiView> RenderHomeAsync(AstUiSession session, AstAuthorizationContext authorization, string? roomName)
    {
        var builder = BaseEmbed(roomName == null
                ? (IsFrench ? "Centre de commandes AST" : "AST command center")
                : $"🌐 {Safe(roomName)}",
            roomName == null
                ? (IsFrench ? "Choisissez l’espace que vous souhaitez ouvrir." : "Choose the area you want to open.")
                : (IsFrench ? "Interface personnelle de cette room." : "Your personal interface for this room."));
        var components = new ComponentBuilder();
        if (session.RoomChannelId != null)
        {
            components.WithButton(IsFrench ? "Mon espace" : "My space", Id(session, "personal"), ButtonStyle.Primary, emote: new Emoji("👤"));
            components.WithButton(IsFrench ? "La room" : "The room", Id(session, "room"), ButtonStyle.Primary, emote: new Emoji("🌐"));
            if (AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization))
                components.WithButton(IsFrench ? "Gérer" : "Manage", Id(session, "manage"), ButtonStyle.Secondary, emote: new Emoji("⚙️"));
        }
        if (AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization))
            components.WithButton(IsFrench ? "Administration" : "Administration", Id(session, "admin"), ButtonStyle.Secondary, emote: new Emoji("🛠️"));
        components.WithButton(IsFrench ? "Aide" : "Help", Id(session, "help"), ButtonStyle.Secondary, emote: new Emoji("❓"));
        if (session.RoomChannelId == null)
        {
            var accessibleRooms = (await GetAccessibleRoomsAsync(session, authorization).ConfigureAwait(false))
                .Where(room => MatchesSelectionSearch(session, room.Name, room.ChannelId))
                .ToArray();
            var roomMenu = BuildRoomMenu(session, accessibleRooms);
            if (roomMenu != null)
            {
                components.WithSelectMenu(roomMenu, row: 1);
                AddSelectionNavigation(components, session, accessibleRooms.Length, row: 2);
                AddPageField(builder, session, accessibleRooms.Length);
            }
            else
            {
                if (session.SelectionSearch != null)
                    AddSelectionNavigation(components, session, accessibleRooms.Length, row: 2);
                builder.AddField(IsFrench ? "Rooms" : "Rooms", session.SelectionSearch == null
                    ? (IsFrench ? "Aucune room accessible sur ce serveur." : "No accessible room on this server.")
                    : (IsFrench ? $"Aucune room ne correspond à « {Safe(session.SelectionSearch)} »." : $"No room matches “{Safe(session.SelectionSearch)}”."));
            }
        }
        return new AstUiView(null, builder.Build(), components.Build());
    }

    private static SelectMenuBuilder? BuildRoomMenu(
        AstUiSession session,
        IReadOnlyList<(string ChannelId, string Name)> rooms)
    {
        var page = PageValues(rooms, session.SelectionPageIndex);
        if (page.Count == 0) return null;
        var menu = new SelectMenuBuilder()
            .WithCustomId(Id(session, "select-room"))
            .WithPlaceholder(IsFrench ? "Choisir une room…" : "Choose a room…")
            .WithMinValues(1)
            .WithMaxValues(1);
        foreach (var room in page)
            menu.AddOption(Safe(room.Name)[..Math.Min(Safe(room.Name).Length, 100)], room.ChannelId);
        return menu;
    }

    private static async Task<IReadOnlyList<(string ChannelId, string Name)>> GetAccessibleRoomsAsync(
        AstUiSession session,
        AstAuthorizationContext sourceAuthorization)
    {
        var guildId = session.GuildId.ToString(CultureInfo.InvariantCulture);
        var channelIds = await DatabaseCommands.GetAllChannelsAsync(guildId, "ChannelsAndUrlsTable").ConfigureAwait(false);
        var rooms = new List<(string ChannelId, string Name)>();
        foreach (var channelId in channelIds.Distinct(StringComparer.Ordinal))
        {
            if (!ulong.TryParse(channelId, out var channelSnowflake) || Declare.Client.GetChannel(channelSnowflake) is not IChannel channel)
                continue;
            var allowed = AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, sourceAuthorization);
            if (!allowed)
            {
                var roomAuthorization = await AstAuthorizationService.CreateDiscordContextAsync(
                    guildId, channelId, session.OwnerUserId).ConfigureAwait(false);
                allowed = roomAuthorization != null && AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, roomAuthorization);
            }
            if (allowed) rooms.Add((channelId, channel.Name));
        }
        return rooms.OrderBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(room => room.ChannelId, StringComparer.Ordinal)
            .ToArray();
    }

    private static AstUiView RenderPersonal(AstUiSession session)
    {
        if (session.PendingAction == "revoke-personal-portal") return PortalRevokeConfirmation(session);
        return Screen(session,
            IsFrench ? "👤 Mon espace" : "👤 My space",
            IsFrench ? "Vos données et préférences personnelles." : "Your personal data and preferences.",
            [(IsFrench ? "Mes slots" : "My slots", "personal-slots"),
             (IsFrench ? "Mes objets" : "My items", "personal-items"),
             ("Hints", "personal-hints"),
             (IsFrench ? "Mon récap" : "My recap", "personal-recap"),
             (IsFrench ? "Mon patch" : "My patch", "personal-patch"),
             (IsFrench ? "Mes exclusions" : "My exclusions", "personal-exclusions"),
             (IsFrench ? "Mon portail" : "My portal", "personal-portal"),
             (IsFrench ? "Révoquer portail" : "Revoke portal", "personal-portal-revoke-request"),
             (IsFrench ? "Avancé" : "Advanced", "personal-advanced")]);
    }

    private static async Task<AstUiView> RenderRoomAsync(AstUiSession session, string? roomName)
    {
        var guildId = session.GuildId.ToString(CultureInfo.InvariantCulture);
        var channelId = session.RoomChannelId!.Value.ToString(CultureInfo.InvariantCulture);
        var health = TrackingControlCommands.FormatRoomHealth(TrackingDataManager.GetRoomHealth(guildId, channelId));
        var view = Screen(session,
            $"🌐 {Safe(roomName ?? (IsFrench ? "Room" : "Room"))}",
            health,
            [(IsFrench ? "Progression" : "Progress", "room-games"),
             (IsFrench ? "Informations" : "Information", "room-info"),
             (IsFrench ? "Associations" : "Associations", "room-associations")]);
        return await Task.FromResult(view);
    }

    private static AstUiView RenderManage(AstUiSession session)
        => Screen(session,
            IsFrench ? "⚙️ Gérer la room" : "⚙️ Manage room",
            IsFrench ? "Actions réservées aux gestionnaires de cette room." : "Actions restricted to this room's managers.",
            [(IsFrench ? "Synchroniser" : "Sync now", "sync-now"),
             (IsFrench ? "Suspendre" : "Pause", "pause"),
             (IsFrench ? "Reprendre" : "Resume", "resume"),
             ("Polling", "manage-polling"),
             (IsFrench ? "Plus…" : "More…", "manage-more")]);

    private static AstUiView RenderAdministration(AstUiSession session, AstAuthorizationContext authorization)
    {
        if (session.PendingAction == "revoke-admin-portal") return PortalRevokeConfirmation(session);
        var actions = new List<(string, string)>
        {
            (IsFrench ? "Configurer une room" : "Configure room", "admin-setup"),
            (IsFrench ? "Santé AST" : "AST health", "guild-health"),
            (IsFrench ? "Portail" : "Portal", "admin-portal"),
            (IsFrench ? "Révoquer portail" : "Revoke portal", "admin-portal-revoke-request")
        };
        if (Declare.IsArchipelagoMode)
        {
            actions.Add(("YAML", "admin-yaml"));
            actions.Add((IsFrench ? "Génération" : "Generation", "admin-generation"));
            if (AstAuthorizationService.IsAllowed(AstAuthorizationLevel.InstanceOwner, authorization))
                actions.Add(("APWorld", "admin-apworld"));
        }
        return Screen(session,
            IsFrench ? "🛠️ Administration AST" : "🛠️ AST administration",
            AstAuthorizationService.IsAllowed(AstAuthorizationLevel.InstanceOwner, authorization)
                ? (IsFrench ? "Accès propriétaire de l’instance." : "Instance-owner access.")
                : (IsFrench ? "Accès gestionnaire du serveur." : "Guild-manager access."),
            actions);
    }

    private static AstUiView RenderHelp(AstUiSession session)
        => Screen(session,
            IsFrench ? "❓ Aide et liens" : "❓ Help and links",
            IsFrench
                ? $"Utilisez les boutons pour naviguer.\n\n{ArchipelagoSphereTracker.src.Resources.Resource.Discord}\n{string.Format(ArchipelagoSphereTracker.src.Resources.Resource.ApworldInfo, Declare.ApworldInfoSheet)}"
                : $"Use the buttons to navigate.\n\n{ArchipelagoSphereTracker.src.Resources.Resource.Discord}\n{string.Format(ArchipelagoSphereTracker.src.Resources.Resource.ApworldInfo, Declare.ApworldInfoSheet)}",
            []);

    private static AstUiView RenderPolling(AstUiSession session)
    {
        var menu = new SelectMenuBuilder()
            .WithCustomId(Id(session, "poll-policy"))
            .WithPlaceholder(IsFrench ? "Choisir le mode et la fréquence…" : "Choose mode and interval…")
            .AddOption(IsFrench ? "Automatique · 15 min max" : "Automatic · 15 min max", "automatic|15m")
            .AddOption(IsFrench ? "Automatique · 30 min max" : "Automatic · 30 min max", "automatic|30m")
            .AddOption(IsFrench ? "Automatique · 1 h max" : "Automatic · 1 h max", "automatic|1h")
            .AddOption(IsFrench ? "Automatique · 6 h max" : "Automatic · 6 h max", "automatic|6h")
            .AddOption(IsFrench ? "Fixe · 5 min" : "Fixed · 5 min", "fixed|5m")
            .AddOption(IsFrench ? "Fixe · 15 min" : "Fixed · 15 min", "fixed|15m")
            .AddOption(IsFrench ? "Fixe · 30 min" : "Fixed · 30 min", "fixed|30m")
            .AddOption(IsFrench ? "Fixe · 1 h" : "Fixed · 1 h", "fixed|1h")
            .AddOption(IsFrench ? "Fixe · 6 h" : "Fixed · 6 h", "fixed|6h")
            .AddOption(IsFrench ? "Fixe · 12 h" : "Fixed · 12 h", "fixed|12h")
            .AddOption(IsFrench ? "Fixe · 18 h" : "Fixed · 18 h", "fixed|18h")
            .AddOption(IsFrench ? "Fixe · 1 jour" : "Fixed · 1 day", "fixed|1d");
        var components = new ComponentBuilder()
            .WithButton(IsFrench ? "Retour" : "Back", Id(session, "manage"), ButtonStyle.Primary, emote: new Emoji("↩️"), row: 0)
            .WithSelectMenu(menu, row: 1)
            .Build();
        return new AstUiView(null, BaseEmbed("⚙️ Polling", IsFrench
            ? "Ce réglage est appliqué directement par Discord."
            : "This setting is applied directly from Discord.").Build(), components);
    }

    private static AstUiView RenderManageMore(AstUiSession session)
    {
        if (session.PendingAction == "revoke-room-portal") return PortalRevokeConfirmation(session);
        if (session.PendingAction == "delete-room")
        {
            var confirmation = new ComponentBuilder()
                .WithButton(IsFrench ? "Supprimer définitivement" : "Delete permanently", Id(session, "confirm-delete-room"), ButtonStyle.Danger)
                .WithButton(IsFrench ? "Annuler" : "Cancel", Id(session, "cancel-delete-room"), ButtonStyle.Secondary)
                .Build();
            return new AstUiView(null, BaseEmbed("⚠️ " + (IsFrench ? "Supprimer la room" : "Delete room"),
                IsFrench ? "Cette action supprime le suivi et les données locales de la room. Elle est irréversible."
                    : "This removes room tracking and its local data. It cannot be undone.").Build(), confirmation);
        }
        var notifications = new SelectMenuBuilder()
            .WithCustomId(Id(session, "notifications"))
            .WithPlaceholder(IsFrench ? "Mode de notification…" : "Notification mode…")
            .AddOption(IsFrench ? "Notifications normales" : "Normal notifications", "false")
            .AddOption(IsFrench ? "Mode silencieux" : "Silent mode", "true");
        var components = new ComponentBuilder()
            .WithButton(IsFrench ? "Analyser le spoiler" : "Analyze spoiler", Id(session, "manage-spoiler"), ButtonStyle.Primary, row: 0)
            .WithButton(IsFrench ? "Portail de la room" : "Room portal", Id(session, "room-portal"), ButtonStyle.Secondary, row: 0)
            .WithButton(IsFrench ? "Révoquer portail" : "Revoke portal", Id(session, "room-portal-revoke-request"), ButtonStyle.Secondary, row: 0)
            .WithButton(IsFrench ? "Supprimer la room" : "Delete room", Id(session, "delete-room-request"), ButtonStyle.Danger, row: 0)
            .WithButton(IsFrench ? "Retour" : "Back", Id(session, "manage"), ButtonStyle.Primary, row: 0)
            .WithSelectMenu(notifications, row: 1)
            .Build();
        return new AstUiView(null, BaseEmbed(IsFrench ? "⚙️ Réglages avancés" : "⚙️ Advanced settings",
            IsFrench ? "Les réglages ci-dessous sont exécutés directement dans Discord." : "The settings below execute directly in Discord.").Build(), components);
    }

    private static async Task<AstUiView> RenderSpoilerAnalysisAsync(AstUiSession session)
    {
        var guildId = session.GuildId.ToString(CultureInfo.InvariantCulture);
        var channelId = session.RoomChannelId!.Value.ToString(CultureInfo.InvariantCulture);
        var aliases = (await AliasChoicesCommands.GetAliasesForGuildAndChannelAsync(guildId, channelId).ConfigureAwait(false))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(alias => MatchesSelectionSearch(session, alias))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var components = new ComponentBuilder()
            .WithButton(IsFrench ? "Analyser" : "Analyze", Id(session, "spoiler-analyze"), ButtonStyle.Success, row: 0)
            .WithButton(IsFrench ? "Configurer" : "Configure", Id(session, "spoiler-configure"), ButtonStyle.Primary, row: 0)
            .WithButton(IsFrench ? "Réinitialiser validation" : "Reset validation", Id(session, "spoiler-reset-validation"), ButtonStyle.Danger, row: 0)
            .WithButton(IsFrench ? "Retour" : "Back", Id(session, "manage-more"), ButtonStyle.Secondary, row: 0);
        if (aliases.Length > 0)
        {
            var aliasMenu = new SelectMenuBuilder().WithCustomId(Id(session, "spoiler-alias"))
                .WithPlaceholder(IsFrench ? "Choisir un slot…" : "Choose a slot…");
            foreach (var alias in PageValues(aliases, session.SelectionPageIndex))
                aliasMenu.AddOption(Safe(alias)[..Math.Min(Safe(alias).Length, 100)], alias,
                    isDefault: string.Equals(alias, session.SpoilerAlias, StringComparison.OrdinalIgnoreCase));
            components.WithSelectMenu(aliasMenu, row: 1);
        }
        var mode = new SelectMenuBuilder().WithCustomId(Id(session, "spoiler-mode"))
            .WithPlaceholder(IsFrench ? "Étendue des checks manquantes…" : "Missing checks scope…")
            .AddOption(IsFrench ? "Première sphère bloquante" : "First blocking sphere", "first", isDefault: session.SpoilerMissingMode == "first")
            .AddOption(IsFrench ? "Rapport complet" : "Full report", "full", isDefault: session.SpoilerMissingMode == "full");
        var hide = new SelectMenuBuilder().WithCustomId(Id(session, "spoiler-hide"))
            .WithPlaceholder(IsFrench ? "Affichage des objets…" : "Item display…")
            .AddOption(IsFrench ? "Masquer les objets" : "Hide items", "true", isDefault: session.SpoilerHideItems)
            .AddOption(IsFrench ? "Afficher les objets" : "Show items", "false", isDefault: !session.SpoilerHideItems);
        components.WithSelectMenu(mode, row: 2).WithSelectMenu(hide, row: 3);
        AddSelectionNavigation(components, session, aliases.Length, row: 4);
        var selectedAlias = string.IsNullOrWhiteSpace(session.SpoilerAlias)
            ? (IsFrench ? "aucun" : "none")
            : Safe(session.SpoilerAlias);
        var sphere = session.SpoilerSphereLimit?.ToString(CultureInfo.InvariantCulture) ?? (IsFrench ? "toutes" : "all");
        var description = IsFrench
            ? $"Slot : **{selectedAlias}**\nSphère maximale : **{sphere}**\nMode : **{(session.SpoilerMissingMode == "full" ? "complet" : "premier blocage")}**\nObjets : **{(session.SpoilerHideItems ? "masqués" : "visibles")}**\n\n{PageLabel(session, aliases.Length)} « Configurer » permet aussi de saisir directement un slot et de valider manuellement une sphère."
            : $"Slot: **{selectedAlias}**\nMaximum sphere: **{sphere}**\nMode: **{(session.SpoilerMissingMode == "full" ? "full" : "first blocker")}**\nItems: **{(session.SpoilerHideItems ? "hidden" : "visible")}**\n\n{PageLabel(session, aliases.Length)} Configure also lets you enter a slot directly and manually validate a sphere.";
        return new AstUiView(null, BaseEmbed(IsFrench ? "🔎 Analyse du spoiler" : "🔎 Spoiler analysis", description).Build(), components.Build());
    }

    private static Task<AstUiView> RenderYamlAsync(AstUiSession session)
    {
        if (session.PendingAction == "yaml-clean")
        {
            var confirmation = new ComponentBuilder()
                .WithButton(IsFrench ? "Tout supprimer" : "Delete all", Id(session, "yaml-confirm-clean"), ButtonStyle.Danger)
                .WithButton(IsFrench ? "Annuler" : "Cancel", Id(session, "yaml-cancel"), ButtonStyle.Secondary)
                .Build();
            return Task.FromResult(new AstUiView(null, BaseEmbed("⚠️ " + (IsFrench ? "Nettoyer les YAML" : "Clean YAML files"),
                IsFrench ? "Tous les YAML et les données de génération de ce salon seront supprimés."
                    : "All YAML files and generation data for this channel will be deleted.").Build(), confirmation));
        }
        if (session.PendingAction == "yaml-delete" && session.PendingItem != null)
        {
            var confirmation = new ComponentBuilder()
                .WithButton(IsFrench ? "Supprimer" : "Delete", Id(session, "yaml-confirm-delete"), ButtonStyle.Danger)
                .WithButton(IsFrench ? "Annuler" : "Cancel", Id(session, "yaml-cancel-delete"), ButtonStyle.Secondary)
                .Build();
            return Task.FromResult(new AstUiView(null, BaseEmbed("⚠️ " + (IsFrench ? "Supprimer un YAML" : "Delete YAML"),
                IsFrench ? $"Confirmer la suppression de **{Safe(session.PendingItem)}** ?"
                    : $"Confirm deletion of **{Safe(session.PendingItem)}**?").Build(), confirmation));
        }

        var channelId = session.SourceChannelId.ToString(CultureInfo.InvariantCulture);
        var yamls = YamlClass.GetYamlFileNames(channelId)
            .Where(file => MatchesSelectionSearch(session, file)).ToArray();
        var templates = YamlClass.GetTemplateFileNames()
            .Where(file => MatchesSelectionSearch(session, file)).ToArray();
        var components = new ComponentBuilder()
            .WithButton(IsFrench ? "Lister" : "List", Id(session, "yaml-list"), ButtonStyle.Primary, row: 0)
            .WithButton(IsFrench ? "Sauvegarder" : "Backup", Id(session, "yaml-backup"), ButtonStyle.Success, row: 0)
            .WithButton(IsFrench ? "Tout nettoyer" : "Clean all", Id(session, "yaml-clean-request"), ButtonStyle.Danger, row: 0)
            .WithButton(IsFrench ? "Portail" : "Portal", Id(session, "admin-portal"), ButtonStyle.Secondary, row: 0)
            .WithButton(IsFrench ? "Retour" : "Back", Id(session, "admin"), ButtonStyle.Secondary, row: 0);
        var yamlPage = PageValues(yamls, session.SelectionPageIndex);
        if (yamlPage.Count > 0)
        {
            var delete = new SelectMenuBuilder().WithCustomId(Id(session, "yaml-delete-select"))
                .WithPlaceholder(IsFrench ? "Supprimer un YAML…" : "Delete a YAML…");
            foreach (var file in yamlPage) delete.AddOption(file[..Math.Min(file.Length, 100)], file);
            components.WithSelectMenu(delete, row: 1);
        }
        var templatePage = PageValues(templates, session.SelectionPageIndex);
        if (templatePage.Count > 0)
        {
            var download = new SelectMenuBuilder().WithCustomId(Id(session, "yaml-template-download"))
                .WithPlaceholder(IsFrench ? "Télécharger un modèle…" : "Download a template…");
            foreach (var file in templatePage) download.AddOption(file[..Math.Min(file.Length, 100)], file);
            components.WithSelectMenu(download, row: 2);
        }
        var selectionCount = Math.Max(yamls.Length, templates.Length);
        AddSelectionNavigation(components, session, selectionCount, row: 3);
        var description = IsFrench
            ? $"{yamls.Length} fichier(s) YAML pour ce salon. {PageLabel(session, selectionCount)} Les actions sont exécutées directement depuis Discord."
            : $"{yamls.Length} YAML file(s) for this channel. {PageLabel(session, selectionCount)} Actions execute directly from Discord.";
        return Task.FromResult(new AstUiView(null, BaseEmbed("YAML", description).Build(), components.Build()));
    }

    private static AstUiView RenderGeneration(AstUiSession session)
    {
        var skip = new SelectMenuBuilder().WithCustomId(Id(session, "generation-skip"))
            .WithPlaceholder(IsFrench ? "Équilibrage de progression…" : "Progression balancing…")
            .AddOption(IsFrench ? "Équilibrage normal" : "Normal balancing", "false", isDefault: !session.GenerationSkipProgBalancing)
            .AddOption(IsFrench ? "Ignorer l’équilibrage" : "Skip balancing", "true", isDefault: session.GenerationSkipProgBalancing);
        var components = new ComponentBuilder()
            .WithButton(IsFrench ? "Générer" : "Generate", Id(session, "generation-run"), ButtonStyle.Success, row: 0)
            .WithButton(IsFrench ? "Tester" : "Test", Id(session, "generation-test"), ButtonStyle.Primary, row: 0)
            .WithButton(IsFrench ? "Portail" : "Portal", Id(session, "admin-portal"), ButtonStyle.Secondary, row: 0)
            .WithButton(IsFrench ? "Retour" : "Back", Id(session, "admin"), ButtonStyle.Secondary, row: 0)
            .WithSelectMenu(skip, row: 1)
            .Build();
        var description = IsFrench
            ? "Générez depuis les YAML du salon ou testez-les sans produire de sortie. Un ZIP envoyé avec `/ast file:` déclenche aussi la génération native."
            : "Generate from the channel YAML files or test them without output. A ZIP sent with `/ast file:` also starts native generation.";
        return new AstUiView(null, BaseEmbed(IsFrench ? "Génération" : "Generation", description).Build(), components);
    }

    private static AstUiView RenderApworld(AstUiSession session)
        => Screen(session, "APWorld", IsFrench ? "Gestion Discord native des APWorld." : "Native Discord APWorld management.",
            [(IsFrench ? "Lister" : "List", "apworld-list"),
             (IsFrench ? "Sauvegarder" : "Backup", "apworld-backup"),
             (IsFrench ? "Portail" : "Portal", "admin-portal")]);

    private static async Task<AstUiView> RenderSlotsAsync(AstUiSession session)
    {
        var guildId = session.GuildId.ToString(CultureInfo.InvariantCulture);
        var channelId = session.RoomChannelId!.Value.ToString(CultureInfo.InvariantCulture);
        var userId = session.OwnerUserId.ToString(CultureInfo.InvariantCulture);
        var allAliases = await AliasChoicesCommands.GetAliasesForGuildAndChannelAsync(guildId, channelId).ConfigureAwait(false);
        var allOwnAliases = (await ReceiverAliasesCommands.GetReceiversForUserAsync(guildId, channelId, userId).ConfigureAwait(false))
            .ToArray();
        var ownAliases = allOwnAliases
            .Where(alias => MatchesSelectionSearch(session, alias))
            .ToArray();
        var associated = await ReceiverAliasesCommands.GetAssociatedReceiversAsync(guildId, channelId).ConfigureAwait(false);
        var available = allAliases.Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(alias => !associated.Contains(alias))
            .Where(alias => MatchesSelectionSearch(session, alias))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var components = new ComponentBuilder()
            .WithButton(IsFrench ? "Associer par nom" : "Associate by name", Id(session, "alias-add-manual"), ButtonStyle.Success, row: 0)
            .WithButton(IsFrench ? "Dissocier par nom" : "Dissociate by name", Id(session, "alias-delete-manual"), ButtonStyle.Danger, row: 0,
                disabled: allOwnAliases.Length == 0)
            .WithButton(IsFrench ? "Retour" : "Back", Id(session, "personal"), ButtonStyle.Primary, row: 0);
        var availablePage = PageValues(available, session.SelectionPageIndex);
        if (availablePage.Count > 0)
        {
            var add = new SelectMenuBuilder().WithCustomId(Id(session, "alias-add"))
                .WithPlaceholder(IsFrench ? "Associer un slot…" : "Associate a slot…");
            foreach (var alias in availablePage) add.AddOption(Safe(alias)[..Math.Min(Safe(alias).Length, 100)], alias);
            components.WithSelectMenu(add, row: 1);
        }
        var filter = new SelectMenuBuilder().WithCustomId(Id(session, "alias-filter"))
            .WithPlaceholder(IsFrench ? "Filtrer les mentions inutiles…" : "Filter unnecessary mentions…")
            .AddOption(IsFrench ? "Aucun filtre" : "No filter", "0", isDefault: session.AliasMentionFlag == "0")
            .AddOption(IsFrench ? "Filler" : "Filler", "1", isDefault: session.AliasMentionFlag == "1")
            .AddOption(IsFrench ? "Pièges" : "Traps", "16", isDefault: session.AliasMentionFlag == "16")
            .AddOption(IsFrench ? "Filler + pièges" : "Filler + traps", "17", isDefault: session.AliasMentionFlag == "17")
            .AddOption(IsFrench ? "Jusqu’aux utiles" : "Through useful", "21", isDefault: session.AliasMentionFlag == "21")
            .AddOption(IsFrench ? "Jusqu’aux requis" : "Through required", "27", isDefault: session.AliasMentionFlag == "27")
            .AddOption(IsFrench ? "Tout filtrer" : "Filter all", "31", isDefault: session.AliasMentionFlag == "31");
        components.WithSelectMenu(filter, row: 2);
        var ownAliasPage = PageValues(ownAliases, session.SelectionPageIndex);
        if (ownAliasPage.Count > 0)
        {
            var delete = new SelectMenuBuilder().WithCustomId(Id(session, "alias-delete"))
                .WithPlaceholder(IsFrench ? "Dissocier un de mes slots…" : "Dissociate one of my slots…");
            foreach (var alias in ownAliasPage) delete.AddOption(Safe(alias)[..Math.Min(Safe(alias).Length, 100)], alias);
            components.WithSelectMenu(delete, row: 3);
        }
        var selectionCount = Math.Max(available.Count, ownAliases.Length);
        AddSelectionNavigation(components, session, selectionCount, row: 4);
        var description = allOwnAliases.Length == 0
            ? (IsFrench ? "Aucun slot associé." : "No associated slot.")
            : ownAliasPage.Count == 0
                ? PageLabel(session, selectionCount) + " " + (IsFrench ? "Aucun slot associé sur cette page." : "No associated slot on this page.")
                : PageLabel(session, selectionCount) + "\n" + string.Join("\n", ownAliasPage.Select(alias => $"• {Safe(alias)}"));
        return new AstUiView(null, BaseEmbed(IsFrench ? "👤 Mes slots" : "👤 My slots", description).Build(), components.Build());
    }

    private static async Task<AstUiView> RenderAdvancedAsync(AstUiSession session)
    {
        var guildId = session.GuildId.ToString(CultureInfo.InvariantCulture);
        var channelId = session.RoomChannelId!.Value.ToString(CultureInfo.InvariantCulture);
        var aliases = (await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(
            guildId, channelId, session.OwnerUserId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false)).Keys
            .Where(alias => MatchesSelectionSearch(session, alias))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        var components = new ComponentBuilder();
        if (session.PendingAction != null)
        {
            var target = session.PendingAlias == null ? (IsFrench ? "tous vos récaps" : "all your recaps") : $"**{Safe(session.PendingAlias)}**";
            components.WithButton(IsFrench ? "Confirmer" : "Confirm", Id(session, "confirm-clean"), ButtonStyle.Danger)
                .WithButton(IsFrench ? "Annuler" : "Cancel", Id(session, "cancel-pending"), ButtonStyle.Secondary);
            return new AstUiView(null, BaseEmbed(IsFrench ? "⚠️ Confirmation" : "⚠️ Confirmation",
                IsFrench ? $"Confirmer le nettoyage de {target} ?" : $"Confirm clearing {target}?").Build(), components.Build());
        }
        components.WithButton(IsFrench ? "Tout vider" : "Clear all", Id(session, "clean-all-request"), ButtonStyle.Danger, row: 0)
            .WithButton(IsFrench ? "Retour" : "Back", Id(session, "personal"), ButtonStyle.Primary, row: 0);
        if (aliases.Length > 0)
        {
            var clean = new SelectMenuBuilder().WithCustomId(Id(session, "clean-select")).WithPlaceholder(IsFrench ? "Vider un récap…" : "Clear one recap…");
            var recapClean = new SelectMenuBuilder().WithCustomId(Id(session, "recap-clean-select")).WithPlaceholder(IsFrench ? "Afficher puis vider…" : "Show then clear…");
            foreach (var alias in PageValues(aliases, session.SelectionPageIndex)) { clean.AddOption(Safe(alias), alias); recapClean.AddOption(Safe(alias), alias); }
            components.WithSelectMenu(clean, row: 1).WithSelectMenu(recapClean, row: 2);
        }
        AddSelectionNavigation(components, session, aliases.Length, row: 3);
        return new AstUiView(null, BaseEmbed(IsFrench ? "🧹 Récaps avancés" : "🧹 Advanced recaps",
            (IsFrench ? "Toutes les suppressions demandent une confirmation. " : "Every deletion requires confirmation. ") +
            PageLabel(session, aliases.Length)).Build(), components.Build());
    }

    private static async Task<AstUiView> RenderExclusionsAsync(AstUiSession session)
    {
        var guildId = session.GuildId.ToString(CultureInfo.InvariantCulture);
        var channelId = session.RoomChannelId!.Value.ToString(CultureInfo.InvariantCulture);
        var userId = session.OwnerUserId.ToString(CultureInfo.InvariantCulture);
        var aliases = (await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId).ConfigureAwait(false)).Keys
            .Where(alias => MatchesSelectionSearch(session, alias))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        var components = new ComponentBuilder();
        if (session.PendingAction == "exclude-delete-confirm" && session.PendingAlias != null && session.PendingItem != null)
        {
            components.WithButton(IsFrench ? "Confirmer le retrait" : "Confirm removal", Id(session, "confirm-exclusion-delete"), ButtonStyle.Danger)
                .WithButton(IsFrench ? "Annuler" : "Cancel", Id(session, "cancel-exclusion"), ButtonStyle.Secondary);
            return new AstUiView(null, BaseEmbed("⚠️ Confirmation", IsFrench
                ? $"Retirer **{Safe(session.PendingItem)}** des exclusions de **{Safe(session.PendingAlias)}** ?"
                : $"Remove **{Safe(session.PendingItem)}** from **{Safe(session.PendingAlias)}** exclusions?").Build(), components.Build());
        }
        components.WithButton(IsFrench ? "Retour" : "Back", Id(session, "personal"), ButtonStyle.Primary, row: 0)
            .WithButton(IsFrench ? "Annuler" : "Cancel", Id(session, "cancel-exclusion"), ButtonStyle.Secondary, row: 0);
        if (session.PendingAlias != null && session.PendingAction is "exclude-add" or "exclude-delete")
        {
            components.WithButton(IsFrench ? "Rechercher" : "Search", Id(session, "exclusion-search"), ButtonStyle.Secondary, row: 0);
            if (session.ExclusionSearch != null)
                components.WithButton(IsFrench ? "Effacer le filtre" : "Clear filter", Id(session, "exclusion-clear-search"), ButtonStyle.Secondary, row: 0);
            var items = await GetExclusionChoicesAsync(session, guildId, channelId, userId).ConfigureAwait(false);
            const int pageSize = 25;
            var pageCount = Math.Max(1, (items.Count + pageSize - 1) / pageSize);
            var pageIndex = Math.Clamp(session.ExclusionPageIndex, 0, pageCount - 1);
            var pageItems = items.Skip(pageIndex * pageSize).Take(pageSize).ToArray();
            var menu = new SelectMenuBuilder()
                .WithCustomId(Id(session, session.PendingAction == "exclude-add" ? "exclude-item-add" : "exclude-item-delete"))
                .WithPlaceholder(IsFrench ? "Choisir un objet…" : "Choose an item…");
            foreach (var item in pageItems)
                menu.AddOption(Safe(item)[..Math.Min(Safe(item).Length, 100)], item);
            if (pageItems.Length > 0) components.WithSelectMenu(menu, row: 1);
            if (pageCount > 1)
            {
                components
                    .WithButton(IsFrench ? "Précédent" : "Previous", Id(session, "exclusion-previous"),
                        ButtonStyle.Secondary, disabled: pageIndex == 0, row: 2)
                    .WithButton(IsFrench ? "Suivant" : "Next", Id(session, "exclusion-next"),
                        ButtonStyle.Secondary, disabled: pageIndex == pageCount - 1, row: 2);
            }
            var filterDescription = session.ExclusionSearch == null
                ? string.Empty
                : IsFrench ? $" Filtre : « {Safe(session.ExclusionSearch)} »." : $" Filter: “{Safe(session.ExclusionSearch)}”.";
            var pageDescription = IsFrench
                ? $"{items.Count} objets disponibles — page {pageIndex + 1}/{pageCount}.{filterDescription}"
                : $"{items.Count} available items — page {pageIndex + 1}/{pageCount}.{filterDescription}";
            return new AstUiView(null, BaseEmbed(IsFrench ? "🚫 Mes exclusions" : "🚫 My exclusions", pageDescription).Build(), components.Build());
        }
        else if (aliases.Length > 0)
        {
            var add = new SelectMenuBuilder().WithCustomId(Id(session, "exclude-add-alias")).WithPlaceholder(IsFrench ? "Ajouter une exclusion au slot…" : "Add an exclusion for slot…");
            var delete = new SelectMenuBuilder().WithCustomId(Id(session, "exclude-delete-alias")).WithPlaceholder(IsFrench ? "Retirer une exclusion du slot…" : "Remove an exclusion from slot…");
            foreach (var alias in PageValues(aliases, session.SelectionPageIndex)) { add.AddOption(Safe(alias), alias); delete.AddOption(Safe(alias), alias); }
            components.WithSelectMenu(add, row: 1).WithSelectMenu(delete, row: 2);
        }
        if (session.PendingAlias == null)
            AddSelectionNavigation(components, session, aliases.Length, row: 3);
        return new AstUiView(null, BaseEmbed(IsFrench ? "🚫 Mes exclusions" : "🚫 My exclusions",
            (IsFrench ? "Ajoutez ou retirez les exclusions de vos propres slots. " : "Add or remove exclusions for your own slots. ") +
            PageLabel(session, aliases.Length)).Build(), components.Build());
    }

    private static async Task<List<string>> GetExclusionChoicesAsync(
        AstUiSession session,
        string guildId,
        string channelId,
        string userId)
    {
        if (session.PendingAlias == null)
            return [];
        var items = session.PendingAction == "exclude-add"
            ? await ExcludedItemsCommands.GetItemNamesForAliasAsync(guildId, channelId, session.PendingAlias).ConfigureAwait(false)
            : await ExcludedItemsCommands.GetExcludedItemsForUserByAliasAsync(
                guildId, channelId, userId, session.PendingAlias).ConfigureAwait(false);
        return items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(item => session.ExclusionSearch == null ||
                           item.Contains(session.ExclusionSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<T> PageValues<T>(IReadOnlyList<T> values, int requestedPage)
    {
        if (values.Count == 0) return [];
        var lastPage = (values.Count - 1) / 25;
        var page = Math.Clamp(requestedPage, 0, lastPage);
        return values.Skip(page * 25).Take(25).ToArray();
    }

    private static void AddSelectionNavigation(
        ComponentBuilder components,
        AstUiSession session,
        int totalItems,
        int row)
    {
        components.WithButton(IsFrench ? "Rechercher" : "Search", Id(session, "selection-search"),
            ButtonStyle.Secondary, row: row);
        if (session.SelectionSearch != null)
            components.WithButton(IsFrench ? "Effacer le filtre" : "Clear filter", Id(session, "selection-clear-search"),
                ButtonStyle.Secondary, row: row);
        var pageCount = Math.Max(1, (Math.Max(0, totalItems) + 24) / 25);
        if (pageCount <= 1) return;
        var page = Math.Clamp(session.SelectionPageIndex, 0, pageCount - 1);
        components
            .WithButton(IsFrench ? "Précédent" : "Previous", Id(session, "selection-previous"),
                ButtonStyle.Secondary, disabled: page == 0, row: row)
            .WithButton(IsFrench ? "Suivant" : "Next", Id(session, "selection-next"),
                ButtonStyle.Secondary, disabled: page == pageCount - 1, row: row);
    }

    private static string PageLabel(AstUiSession session, int totalItems)
    {
        var pageCount = Math.Max(1, (Math.Max(0, totalItems) + 24) / 25);
        var page = Math.Clamp(session.SelectionPageIndex, 0, pageCount - 1);
        var filter = session.SelectionSearch == null
            ? string.Empty
            : IsFrench ? $"Filtre : « {Safe(session.SelectionSearch)} ». " : $"Filter: “{Safe(session.SelectionSearch)}”. ";
        var pagination = pageCount <= 1 ? string.Empty : $"Page {page + 1}/{pageCount}.";
        return filter + pagination;
    }

    private static bool MatchesSelectionSearch(AstUiSession session, params string[] values)
        => session.SelectionSearch == null ||
           values.Any(value => value.Contains(session.SelectionSearch, StringComparison.OrdinalIgnoreCase));

    private static void AddPageField(EmbedBuilder builder, AstUiSession session, int totalItems)
    {
        var label = PageLabel(session, totalItems);
        if (!string.IsNullOrEmpty(label)) builder.AddField(IsFrench ? "Navigation" : "Navigation", label);
    }

    private static async Task<int> GetSelectionItemCountAsync(
        AstUiSession session,
        AstAuthorizationContext authorization)
    {
        var guildId = session.GuildId.ToString(CultureInfo.InvariantCulture);
        var channelId = session.RoomChannelId?.ToString(CultureInfo.InvariantCulture);
        return session.Screen switch
        {
            AstUiScreen.Home when session.RoomChannelId == null
                => (await GetAccessibleRoomsAsync(session, authorization).ConfigureAwait(false))
                    .Count(room => MatchesSelectionSearch(session, room.Name, room.ChannelId)),
            AstUiScreen.Yaml => Math.Max(
                YamlClass.GetYamlFileNames(session.SourceChannelId.ToString(CultureInfo.InvariantCulture))
                    .Count(file => MatchesSelectionSearch(session, file)),
                YamlClass.GetTemplateFileNames().Count(file => MatchesSelectionSearch(session, file))),
            AstUiScreen.Slots when channelId != null
                => await GetSlotSelectionCountAsync(session, guildId, channelId, session.OwnerUserId).ConfigureAwait(false),
            AstUiScreen.Advanced or AstUiScreen.Exclusions when channelId != null
                => (await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(
                    guildId, channelId, session.OwnerUserId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false)).Keys
                    .Count(alias => MatchesSelectionSearch(session, alias)),
            AstUiScreen.SpoilerAnalysis when channelId != null
                => (await AliasChoicesCommands.GetAliasesForGuildAndChannelAsync(guildId, channelId).ConfigureAwait(false))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(alias => MatchesSelectionSearch(session, alias)),
            _ => 0
        };
    }

    private static async Task<int> GetSlotSelectionCountAsync(
        AstUiSession session,
        string guildId,
        string channelId,
        ulong userId)
    {
        var allAliases = await AliasChoicesCommands.GetAliasesForGuildAndChannelAsync(guildId, channelId).ConfigureAwait(false);
        var ownAliases = await ReceiverAliasesCommands.GetReceiversForUserAsync(
            guildId, channelId, userId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        var associated = await ReceiverAliasesCommands.GetAssociatedReceiversAsync(guildId, channelId).ConfigureAwait(false);
        var availableCount = allAliases.Distinct(StringComparer.OrdinalIgnoreCase)
            .Count(alias => !associated.Contains(alias) && MatchesSelectionSearch(session, alias));
        var ownCount = ownAliases.Count(alias => MatchesSelectionSearch(session, alias));
        return Math.Max(availableCount, ownCount);
    }

    private static AstUiView Screen(
        AstUiSession session,
        string title,
        string description,
        IEnumerable<(string Label, string Action)> actions)
    {
        var components = new ComponentBuilder();
        foreach (var (label, action) in actions.Take(20))
            components.WithButton(label, Id(session, action), ButtonStyle.Secondary);
        components.WithButton(IsFrench ? "Retour" : "Back", Id(session, "home"), ButtonStyle.Primary, emote: new Emoji("↩️"));
        return new AstUiView(null, BaseEmbed(title, Clamp(description, 4000)).Build(), components.Build());
    }

    private static async Task<string?> ExecuteImmediateActionAsync(
        string action,
        AstUiSession session,
        AstAuthorizationContext authorization)
    {
        var guildId = session.GuildId.ToString(CultureInfo.InvariantCulture);
        var channelId = session.RoomChannelId?.ToString(CultureInfo.InvariantCulture);
        if (action == "guild-health")
        {
            return AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization)
                ? TrackingControlCommands.GetGuildHealth(guildId)
                : AstAuthorizationService.DeniedMessage;
        }
        if (action == "admin-portal")
        {
            if (!AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization))
                return AstAuthorizationService.DeniedMessage;
            return await BuildPortalResponseAsync(
                () => WebPortalPages.EnsureCommandsPageAsync(
                    guildId,
                    session.SourceChannelId.ToString(CultureInfo.InvariantCulture),
                    session.OwnerUserId.ToString(CultureInfo.InvariantCulture)),
                IsFrench ? "Portail privé d’administration" : "Private administration portal").ConfigureAwait(false);
        }
        if (action == "yaml-list")
            return AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization)
                ? YamlClass.ListYamls(session.SourceChannelId.ToString(CultureInfo.InvariantCulture))
                : AstAuthorizationService.DeniedMessage;
        if (action == "apworld-list")
            return AstAuthorizationService.IsAllowed(AstAuthorizationLevel.InstanceOwner, authorization)
                ? ApworldClass.ListApworld()
                : AstAuthorizationService.DeniedMessage;
        if (channelId == null) return Unavailable();

        return action switch
        {
            "room-games" => await HelperClass.StatusGameList(channelId, guildId).ConfigureAwait(false),
            "room-info" => await HelperClass.Info(channelId, guildId).ConfigureAwait(false),
            "room-associations" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization)
                => await AliasClass.GetAlias(channelId, guildId).ConfigureAwait(false),
            "room-associations" => IsFrench
                ? "La liste complète des associations Discord est réservée aux gestionnaires. Vos associations seront disponibles dans « Mon espace »."
                : "The complete Discord association list is manager-only. Your associations will be available under My space.",
            "sync-now" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization)
                => await TrackingControlCommands.ExecuteRoomAsync("ast-sync-now", guildId, channelId).ConfigureAwait(false),
            "pause" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization)
                => await TrackingControlCommands.ExecuteRoomAsync("ast-pause", guildId, channelId).ConfigureAwait(false),
            "resume" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization)
                => await TrackingControlCommands.ExecuteRoomAsync("ast-resume", guildId, channelId).ConfigureAwait(false),
            "personal-items" or "personal-recap" => await BuildPersonalItemsAsync(guildId, channelId, session.OwnerUserId).ConfigureAwait(false),
            "personal-hints" => await BuildPersonalHintsAsync(guildId, channelId, session.OwnerUserId).ConfigureAwait(false),
            "personal-patch" => await BuildPersonalPatchesAsync(guildId, channelId, session.OwnerUserId, authorization).ConfigureAwait(false),
            "personal-portal" => await BuildPortalResponseAsync(
                () => WebPortalPages.EnsureUserPageAsync(guildId, channelId, session.OwnerUserId.ToString(CultureInfo.InvariantCulture)),
                IsFrench ? "Mon portail privé" : "My private portal").ConfigureAwait(false),
            "room-portal" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization)
                => await BuildPortalResponseAsync(() => WebPortalPages.EnsureThreadCommandsPageAsync(guildId, channelId, session.OwnerUserId.ToString(CultureInfo.InvariantCulture)), IsFrench ? "Portail privé de la room" : "Private room portal").ConfigureAwait(false),
            _ => AstAuthorizationService.DeniedMessage
        };
    }

    private static async Task<string> BuildPersonalSlotsAsync(string guildId, string channelId, ulong userId)
    {
        var aliases = await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(
            guildId, channelId, userId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        if (aliases.Count == 0)
            return IsFrench ? "Aucun slot n’est associé à votre compte dans cette room." : "No slot is associated with your account in this room.";
        return (IsFrench ? "**Mes slots associés**" : "**My associated slots**") +
               string.Concat(aliases.Keys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).Select(alias => $"\n• {Safe(alias)}"));
    }

    private static async Task<string> BuildPersonalItemsAsync(
        string guildId, string channelId, ulong userId, string? specificAlias = null)
    {
        var aliases = await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(
            guildId, channelId, userId.ToString(CultureInfo.InvariantCulture), specificAlias ?? string.Empty).ConfigureAwait(false);
        if (aliases.Count == 0)
            return IsFrench ? "Aucun slot n’est associé à votre compte dans cette room." : "No slot is associated with your account in this room.";
        var output = new System.Text.StringBuilder(IsFrench ? "**Mes objets**" : "**My items**");
        foreach (var pair in aliases.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            output.AppendLine().AppendLine($"**{Safe(pair.Key)}**");
            foreach (var group in pair.Value.GroupBy(item => item.Flag).OrderBy(group => RecapFlagRank(group.Key)))
            {
                output.AppendLine($"__{RecapFlagLabel(group.Key)}__");
                foreach (var item in group.GroupBy(value => value.Item, StringComparer.OrdinalIgnoreCase)
                             .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
                    output.AppendLine(item.Count() > 1
                        ? $"• {Safe(item.Key)} × {item.Count()}"
                        : $"• {Safe(item.Key)}");
            }
        }
        return output.ToString();
    }

    private static async Task<string> BuildPersonalHintsAsync(string guildId, string channelId, ulong userId)
    {
        var aliases = (await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(
            guildId, channelId, userId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false)).Keys.ToArray();
        if (aliases.Length == 0)
            return IsFrench ? "Aucun slot n’est associé à votre compte dans cette room." : "No slot is associated with your account in this room.";
        var hints = new List<HintStatus>();
        foreach (var alias in aliases)
        {
            hints.AddRange(await HintStatusCommands.GetHintStatusForReceiver(guildId, channelId, alias).ConfigureAwait(false));
            hints.AddRange(await HintStatusCommands.GetHintStatusForFinder(guildId, channelId, alias).ConfigureAwait(false));
        }
        var unique = hints.DistinctBy(hint => $"{hint.Finder}\0{hint.Receiver}\0{hint.Item}\0{hint.Location}").ToArray();
        if (unique.Length == 0) return IsFrench ? "Aucun hint non trouvé pour vos slots." : "No unfound hint for your slots.";
        return (IsFrench ? "**Mes hints non trouvés**" : "**My unfound hints**") +
               string.Concat(unique.Select(hint => $"\n• {Safe(hint.Item)} — {Safe(hint.Location)} ({Safe(hint.Finder)} → {Safe(hint.Receiver)})"));
    }

    private static async Task<string> BuildPersonalAdvancedAsync(string guildId, string channelId, ulong userId)
    {
        var aliases = (await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(
            guildId, channelId, userId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false)).Keys.ToArray();
        if (aliases.Length == 0)
            return IsFrench ? "Aucun slot n’est associé à votre compte dans cette room." : "No slot is associated with your account in this room.";
        var output = new System.Text.StringBuilder(IsFrench ? "**Mes exclusions**" : "**My exclusions**");
        var count = 0;
        foreach (var alias in aliases)
        {
            var items = await ExcludedItemsCommands.GetExcludedItemsForUserByAliasAsync(
                guildId, channelId, userId.ToString(CultureInfo.InvariantCulture), alias).ConfigureAwait(false);
            if (items.Count == 0) continue;
            output.AppendLine().AppendLine($"**{Safe(alias)}**");
            foreach (var item in items) output.AppendLine($"• {Safe(item)}");
            count += items.Count;
        }
        if (count == 0) output.AppendLine().Append(IsFrench ? "Aucune exclusion personnelle." : "No personal exclusion.");
        return output.ToString();
    }

    private static async Task<string> BuildPersonalPatchesAsync(
        string guildId,
        string channelId,
        ulong userId,
        AstAuthorizationContext authorization)
    {
        var patches = await ChannelsAndUrlsCommands.GetPatchesForChannelAsync(guildId, channelId).ConfigureAwait(false);
        if (!AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization))
        {
            var aliases = (await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(
                guildId, channelId, userId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false)).Keys
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            patches = patches.Where(patch => aliases.Contains(patch.Alias)).ToList();
        }
        if (patches.Count == 0)
            return IsFrench ? "Aucun patch autorisé n’est disponible pour vos slots." : "No authorized patch is available for your slots.";
        var output = new System.Text.StringBuilder(IsFrench ? "**Mes patches**" : "**My patches**");
        foreach (var patch in patches)
            output.AppendLine($"• **{Safe(patch.Alias)}** · {Safe(patch.GameName)}\n  {Safe(patch.Patch)}");
        return output.ToString();
    }

    private static async Task<string> BuildPortalResponseAsync(Func<Task<string?>> factory, string label)
    {
        if (!Declare.EnableWebPortal)
            return IsFrench ? "Le portail Web est désactivé." : "The Web portal is disabled.";
        var url = await factory().ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(url)
            ? (IsFrench ? "Le portail est temporairement indisponible." : "The portal is temporarily unavailable.")
            : $"**{label}**\n{url}";
    }

    private static SecurityAuditAction? AuditForUiAction(string action, AstAuthorizationContext authorization)
        => action switch
        {
            "sync-now" or "pause" or "resume"
                when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization)
                => SecurityAuditAction.RoomSettingsUpdate,
            "personal-patch" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, authorization)
                => SecurityAuditAction.PatchAccess,
            "personal-portal" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, authorization)
                => SecurityAuditAction.PortalAccessIssue,
            "room-portal" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization)
                => SecurityAuditAction.PortalAccessIssue,
            "admin-portal" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization)
                => SecurityAuditAction.PortalAccessIssue,
            _ => null
        };

    private static Task<T> AuditedAsync<T>(
        AstUiSession session,
        ulong channelId,
        SecurityAuditAction action,
        Func<Task<T>> operation)
        => AuditedAsync(session.OwnerUserId, session.GuildId, channelId, action, operation);

    private static async Task<T> AuditedAsync<T>(
        ulong actorUserId,
        ulong guildId,
        ulong channelId,
        SecurityAuditAction action,
        Func<Task<T>> operation)
    {
        await using var audit = await SecurityAuditScope.StartAsync(
            SecurityAuditSource.Discord,
            actorUserId.ToString(CultureInfo.InvariantCulture),
            guildId.ToString(CultureInfo.InvariantCulture),
            channelId.ToString(CultureInfo.InvariantCulture),
            action).ConfigureAwait(false);
        var result = await operation().ConfigureAwait(false);
        audit.Succeed();
        return result;
    }

    private static async Task AuditedAsync(
        AstUiSession session,
        ulong channelId,
        SecurityAuditAction action,
        Func<Task> operation)
    {
        await using var audit = await SecurityAuditScope.StartAsync(
            SecurityAuditSource.Discord,
            session.OwnerUserId.ToString(CultureInfo.InvariantCulture),
            session.GuildId.ToString(CultureInfo.InvariantCulture),
            channelId.ToString(CultureInfo.InvariantCulture),
            action).ConfigureAwait(false);
        await operation().ConfigureAwait(false);
        audit.Succeed();
    }

    private static bool TryScreen(string action, out AstUiScreen screen)
    {
        screen = action switch
        {
            "home" => AstUiScreen.Home,
            "personal" => AstUiScreen.Personal,
            "room" => AstUiScreen.Room,
            "manage" => AstUiScreen.Manage,
            "admin" => AstUiScreen.Administration,
            "help" => AstUiScreen.Help,
            "manage-polling" => AstUiScreen.Polling,
            "manage-more" => AstUiScreen.ManageMore,
            "admin-yaml" => AstUiScreen.Yaml,
            "admin-generation" => AstUiScreen.Generation,
            "admin-apworld" => AstUiScreen.Apworld,
            "personal-slots" => AstUiScreen.Slots,
            "personal-advanced" => AstUiScreen.Advanced,
            "personal-exclusions" => AstUiScreen.Exclusions,
            "manage-spoiler" => AstUiScreen.SpoilerAnalysis,
            _ => (AstUiScreen)(-1)
        };
        return (int)screen >= 0;
    }

    private static bool CanOpen(AstUiScreen screen, AstAuthorizationContext authorization, bool hasRoom)
        => screen switch
        {
            AstUiScreen.Personal or AstUiScreen.Room => hasRoom && AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, authorization),
            AstUiScreen.Manage => hasRoom && AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization),
            AstUiScreen.Administration => AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization),
            AstUiScreen.Polling or AstUiScreen.ManageMore => hasRoom && AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization),
            AstUiScreen.Yaml or AstUiScreen.Generation => AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization),
            AstUiScreen.Apworld => AstAuthorizationService.IsAllowed(AstAuthorizationLevel.InstanceOwner, authorization),
            AstUiScreen.Slots => hasRoom && AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, authorization),
            AstUiScreen.Advanced => hasRoom && AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, authorization),
            AstUiScreen.Exclusions => hasRoom && AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, authorization),
            AstUiScreen.SpoilerAnalysis => hasRoom && AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization),
            _ => AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, authorization)
        };

    private static Task<bool> IsTrackedRoomAsync(ulong guildId, ulong channelId)
        => DatabaseCommands.CheckIfChannelExistsAsync(
            guildId.ToString(CultureInfo.InvariantCulture),
            channelId.ToString(CultureInfo.InvariantCulture),
            "ChannelsAndUrlsTable");

    private static EmbedBuilder BaseEmbed(string title, string description)
        => new EmbedBuilder().WithTitle(title).WithDescription(description).WithColor(Color.Blue)
            .WithFooter(IsFrench ? "AST · interface privée" : "AST · private interface");

    private static string Id(AstUiSession session, string action) => $"{CustomIdPrefix}:{session.Id}:{action}";
    private static string Safe(string value) => value.Replace("@", "@\u200b", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');
    private static string Clamp(string value, int max = 1900) => value.Length <= max ? value : value[..(max - 1)] + "…";
    private static string Unavailable() => IsFrench ? "Action indisponible dans ce contexte." : "Action unavailable in this context.";
    private static int RecapFlagRank(long? flag) => flag switch { 3 => 0, 1 => 1, 2 => 2, 0 => 3, 4 => 4, _ => 5 };
    private static string RecapFlagLabel(long? flag) => flag switch
    {
        3 => IsFrench ? "Requis" : "Required",
        1 => "Progression",
        2 => IsFrench ? "Utile" : "Useful",
        0 => IsFrench ? "Remplissage" : "Filler",
        4 => IsFrench ? "Piège" : "Trap",
        _ => IsFrench ? "Non classé" : "Unclassified"
    };

    private static Modal BuildSpoilerConfigModal(AstUiSession session)
    {
        var alias = new TextInputBuilder()
            .WithLabel(IsFrench ? "Slot à analyser" : "Slot to analyze")
            .WithCustomId(SpoilerAliasInputId)
            .WithStyle(TextInputStyle.Short)
            .WithMinLength(1)
            .WithMaxLength(100)
            .WithRequired(true);
        if (!string.IsNullOrWhiteSpace(session.SpoilerAlias)) alias.WithValue(session.SpoilerAlias);
        var sphere = new TextInputBuilder()
            .WithLabel(IsFrench ? "Sphère maximale (vide = toutes)" : "Maximum sphere (blank = all)")
            .WithCustomId(SpoilerSphereInputId)
            .WithStyle(TextInputStyle.Short)
            .WithMaxLength(10)
            .WithRequired(false);
        if (session.SpoilerSphereLimit.HasValue)
            sphere.WithValue(session.SpoilerSphereLimit.Value.ToString(CultureInfo.InvariantCulture));
        var validate = new TextInputBuilder()
            .WithLabel(IsFrench ? "Sphère à valider (facultatif)" : "Sphere to validate (optional)")
            .WithCustomId(SpoilerValidateInputId)
            .WithStyle(TextInputStyle.Short)
            .WithMaxLength(10)
            .WithRequired(false);
        return new ModalBuilder()
            .WithTitle(IsFrench ? "Configurer l’analyse" : "Configure analysis")
            .WithCustomId(Id(session, "spoiler-configure"))
            .AddTextInput(alias, row: 0)
            .AddTextInput(sphere, row: 1)
            .AddTextInput(validate, row: 2)
            .Build();
    }

    private static Modal BuildSlotAliasModal(AstUiSession session, string action)
    {
        var input = new TextInputBuilder()
            .WithLabel(IsFrench ? "Nom exact du slot" : "Exact slot name")
            .WithCustomId(SlotAliasInputId)
            .WithStyle(TextInputStyle.Short)
            .WithMinLength(1)
            .WithMaxLength(100)
            .WithRequired(true);
        return new ModalBuilder()
            .WithTitle(action == "alias-add-manual"
                ? (IsFrench ? "Associer un slot" : "Associate a slot")
                : (IsFrench ? "Dissocier un slot" : "Dissociate a slot"))
            .WithCustomId(Id(session, action))
            .AddTextInput(input)
            .Build();
    }

    private static Modal BuildSelectionSearchModal(AstUiSession session)
    {
        var input = new TextInputBuilder()
            .WithLabel(IsFrench ? "Nom ou partie du nom" : "Full or partial name")
            .WithCustomId(SelectionSearchInputId)
            .WithStyle(TextInputStyle.Short)
            .WithMaxLength(100)
            .WithRequired(false);
        if (!string.IsNullOrWhiteSpace(session.SelectionSearch))
            input.WithValue(session.SelectionSearch);
        return new ModalBuilder()
            .WithTitle(IsFrench ? "Rechercher dans la liste" : "Search the list")
            .WithCustomId(Id(session, "selection-search"))
            .AddTextInput(input)
            .Build();
    }

    private static Modal BuildExclusionSearchModal(AstUiSession session)
    {
        var input = new TextInputBuilder()
            .WithLabel(IsFrench ? "Nom ou partie du nom de l’objet" : "Full or partial item name")
            .WithCustomId(ExclusionSearchInputId)
            .WithStyle(TextInputStyle.Short)
            .WithMaxLength(100)
            .WithRequired(false);
        if (!string.IsNullOrWhiteSpace(session.ExclusionSearch))
            input.WithValue(session.ExclusionSearch);
        return new ModalBuilder()
            .WithTitle(IsFrench ? "Rechercher un objet" : "Search for an item")
            .WithCustomId(Id(session, "exclusion-search"))
            .AddTextInput(input)
            .Build();
    }

    private static AstUiView PortalRevokeConfirmation(AstUiSession session)
    {
        var components = new ComponentBuilder()
            .WithButton(IsFrench ? "Révoquer le lien" : "Revoke link", Id(session, "confirm-portal-revoke"), ButtonStyle.Danger)
            .WithButton(IsFrench ? "Annuler" : "Cancel", Id(session, "cancel-portal-revoke"), ButtonStyle.Secondary)
            .Build();
        return new AstUiView(null, BaseEmbed("⚠️ " + (IsFrench ? "Révoquer le portail" : "Revoke portal"),
            IsFrench ? "Les URL précédemment émises pour ce contexte cesseront immédiatement de fonctionner."
                : "Previously issued URLs for this context will immediately stop working.").Build(), components);
    }

    public static IReadOnlyList<string> PaginateOutput(string value, int maxLength = 1900)
    {
        if (maxLength <= 0) throw new ArgumentOutOfRangeException(nameof(maxLength));
        var remaining = (value ?? string.Empty).Replace("@", "@\u200b", StringComparison.Ordinal).Trim();
        if (remaining.Length == 0) return [string.Empty];
        var pages = new List<string>();
        while (remaining.Length > maxLength)
        {
            var split = remaining.LastIndexOf('\n', maxLength - 1, maxLength);
            if (split <= 0) split = maxLength;
            pages.Add(remaining[..split].TrimEnd());
            remaining = remaining[split..].TrimStart('\r', '\n');
        }
        pages.Add(remaining);
        return pages;
    }

    private static AstUiView RenderPagedOutput(AstUiSession session)
    {
        var pages = session.OutputPages!;
        var index = Math.Clamp(session.OutputPageIndex, 0, pages.Count - 1);
        var components = new ComponentBuilder()
            .WithButton(IsFrench ? "Précédent" : "Previous", Id(session, "output-previous"), ButtonStyle.Secondary,
                disabled: index == 0)
            .WithButton(IsFrench ? "Suivant" : "Next", Id(session, "output-next"), ButtonStyle.Secondary,
                disabled: index == pages.Count - 1)
            .WithButton(IsFrench ? "Fermer" : "Close", Id(session, "output-close"), ButtonStyle.Primary)
            .Build();
        return new AstUiView(pages[index], BaseEmbed(
            IsFrench ? "Résultat paginé" : "Paginated result",
            IsFrench ? $"Page {index + 1} sur {pages.Count}." : $"Page {index + 1} of {pages.Count}.").Build(), components);
    }

    private static async Task ShowOutcomeAsync(
        SocketMessageComponent component,
        AstUiSession session,
        AstAuthorizationContext authorization,
        string outcome)
    {
        var pages = PaginateOutput(outcome);
        if (pages.Count > 1)
        {
            if (!Sessions.TrySetOutputPages(
                    session.Id, session.OwnerUserId, session.GuildId, session.SourceChannelId, pages, out session))
            {
                await SetErrorAsync(component, IsFrench ? "Cette interface a expiré." : "This interface expired.").ConfigureAwait(false);
                return;
            }
            await SetViewAsync(component, RenderPagedOutput(session)).ConfigureAwait(false);
            return;
        }
        var refreshed = await RenderAsync(session, authorization).ConfigureAwait(false);
        await component.ModifyOriginalResponseAsync(properties =>
        {
            properties.Content = pages[0];
            properties.Embed = refreshed.Embed;
            properties.Components = refreshed.Components;
        }).ConfigureAwait(false);
    }

    private static bool TryOptionalNonNegativeInt(string? value, out int? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 0)
            return false;
        parsed = number;
        return true;
    }

    private static Task SetViewAsync(SocketMessageComponent component, AstUiView view)
        => component.ModifyOriginalResponseAsync(properties =>
        {
            properties.Content = view.Content;
            properties.Embed = view.Embed;
            properties.Components = view.Components;
        });

    private static Task SetErrorAsync(SocketMessageComponent component, string message)
        => component.ModifyOriginalResponseAsync(properties => properties.Content = Clamp(message));

    private sealed record AstUiView(string? Content, Embed Embed, MessageComponent Components);
}
