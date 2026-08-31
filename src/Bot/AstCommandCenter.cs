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
    bool GenerationSkipProgBalancing = false);

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
                ExpiresAtUtc = _timeProvider.GetUtcNow().Add(_lifetime)
            };
            if (_sessions.TryUpdate(id, updated, current)) { session = updated; return true; }
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
    private const string SpoilerAliasInputId = "ast-spoiler-alias";
    private const string SpoilerSphereInputId = "ast-spoiler-sphere";
    private const string SpoilerValidateInputId = "ast-spoiler-validate";
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
        string result;
        switch (extension)
        {
            case ".yaml" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization):
                result = await YamlClass.SendYaml(command, channelIdText).ConfigureAwait(false);
                break;
            case ".apworld" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.InstanceOwner, authorization):
                result = await ApworldClass.SendApworld(command).ConfigureAwait(false);
                break;
            case ".zip" when AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization):
                result = await GenerationClass.GenerateWithZip(command, channelIdText).ConfigureAwait(false);
                break;
            case ".txt" or ".json" when
                AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization) &&
                command.Channel is IThreadChannel &&
                await IsTrackedRoomAsync(guildId, channelId).ConfigureAwait(false):
                result = await SpoilerLogClass.SendSpoilerLog(command, channelIdText).ConfigureAwait(false);
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
                        await RecapListCommands.DeleteAliasAndItemsForUserIdAsync(guildText, roomText, userText).ConfigureAwait(false);
                        result = IsFrench ? "Tous vos récaps ont été vidés." : "All your recaps were cleared.";
                    }
                    else if (session.PendingAlias != null && session.PendingAction is "clean" or "recap-clean")
                    {
                        var recap = session.PendingAction == "recap-clean"
                            ? await BuildPersonalItemsAsync(guildText, roomText, component.User.Id).ConfigureAwait(false)
                            : string.Empty;
                        await RecapListCommands.DeleteRecapListAsync(guildText, roomText, userText, session.PendingAlias).ConfigureAwait(false);
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
                if (session.RoomChannelId is not { } exclusionRoom)
                {
                    await SetErrorAsync(component, AstAuthorizationService.DeniedMessage).ConfigureAwait(false);
                    return;
                }
                var result = string.Empty;
                if (action == "confirm-exclusion-delete" && session.PendingAlias != null && session.PendingItem != null)
                {
                    result = await ExcludedItemsCommands.DeleteExcludedItemForUserAsync(
                        guildId.ToString(CultureInfo.InvariantCulture), exclusionRoom.ToString(CultureInfo.InvariantCulture),
                        component.User.Id.ToString(CultureInfo.InvariantCulture), session.PendingAlias, session.PendingItem).ConfigureAwait(false);
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
                var result = await UrlClass.DeleteUrl(
                    component.User as IGuildUser,
                    deleteRoom.ToString(CultureInfo.InvariantCulture),
                    guildId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
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
                var spoilerView = await RenderSpoilerAnalysisAsync(session).ConfigureAwait(false);
                await component.ModifyOriginalResponseAsync(p =>
                {
                    p.Content = Clamp(result);
                    p.Embed = spoilerView.Embed;
                    p.Components = spoilerView.Components;
                }).ConfigureAwait(false);
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
                    var error = action == "yaml-backup"
                        ? await YamlClass.BackupYamlsToFileAsync(session.SourceChannelId.ToString(CultureInfo.InvariantCulture), tempPath).ConfigureAwait(false)
                        : await ApworldClass.BackupApworldToFileAsync(tempPath).ConfigureAwait(false);
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
                    result = YamlClass.CleanYamls(session.SourceChannelId.ToString(CultureInfo.InvariantCulture));
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
                    ? YamlClass.DeleteYamlByName(session.SourceChannelId.ToString(CultureInfo.InvariantCulture), session.PendingItem)
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
                    var result = await GenerationClass.TestGenerateAsyncForWeb(generationChannel).ConfigureAwait(false);
                    await SetErrorAsync(component, Clamp(result)).ConfigureAwait(false);
                    return;
                }
                var generation = await GenerationClass.GenerateAsyncForWeb(
                    generationChannel, session.GenerationSkipProgBalancing).ConfigureAwait(false);
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

            var outcome = await ExecuteImmediateActionAsync(action, session, authorization).ConfigureAwait(false);
            if (outcome == null)
            {
                await SetErrorAsync(component, IsFrench ? "Action inconnue." : "Unknown action.").ConfigureAwait(false);
                return;
            }

            var refreshed = await RenderAsync(session, authorization).ConfigureAwait(false);
            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = outcome;
                properties.Embed = refreshed.Embed;
                properties.Components = refreshed.Components;
            }).ConfigureAwait(false);
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
                var error = YamlClass.DownloadTemplateToFile(selected, tempPath);
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
            string? result = null;
            if (action == "exclude-add-alias")
                Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, "exclude-add", selected, out session);
            else if (action == "exclude-delete-alias")
                Sessions.TrySetPending(session.Id, component.User.Id, guildId, sourceChannelId, "exclude-delete", selected, out session);
            else if (action == "exclude-item-add" && session.PendingAlias != null)
            {
                result = await ExcludedItemsCommands.AddExcludedItemForUserAsync(guildText, roomText, userText, session.PendingAlias, selected).ConfigureAwait(false);
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
                result = await AliasClass.AddAliasForUserAsync(
                    selected, session.AliasMentionFlag, roomText, guildText,
                    component.User.Id.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            }
            else
            {
                result = await AliasClass.DeleteAliasForUserAsync(
                    selected, roomText, guildText, component.User.Id.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
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
                    ? await ChannelsAndUrlsCommands.UpdatePollingPolicyFromWeb(parts[0], parts[1], roomText, guildText).ConfigureAwait(false)
                    : (IsFrench ? "Choix de polling invalide." : "Invalid polling choice.");
            }
            else
            {
                result = await ChannelsAndUrlsCommands.UpdateSilentOptionFromWeb(selected, roomText, guildText).ConfigureAwait(false);
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
        if (!TryParseCustomId(modal.Data.CustomId, out var sessionId, out var action) || action != "spoiler-configure")
            return;
        if (modal.GuildId is not { } guildId || modal.ChannelId is not { } sourceChannelId ||
            !Sessions.TryGetAuthorized(sessionId, modal.User.Id, guildId, sourceChannelId, out var session) ||
            session.RoomChannelId is not { } roomChannelId)
        {
            await modal.RespondAsync(IsFrench ? "Cette interface a expiré. Relancez `/ast`." : "This interface expired. Run `/ast` again.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var authorization = await AstAuthorizationService.CreateDiscordContextAsync(
            guildId.ToString(CultureInfo.InvariantCulture), roomChannelId.ToString(CultureInfo.InvariantCulture),
            modal.User.Id, modal.User as IGuildUser).ConfigureAwait(false);
        if (authorization == null || !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, authorization))
        {
            await modal.RespondAsync(AstAuthorizationService.DeniedMessage, ephemeral: true).ConfigureAwait(false);
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
        var view = await RenderSpoilerAnalysisAsync(session).ConfigureAwait(false);
        await modal.UpdateAsync(properties =>
        {
            properties.Content = Clamp(result);
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
            var roomMenu = await BuildRoomMenuAsync(session, authorization).ConfigureAwait(false);
            if (roomMenu != null) components.WithSelectMenu(roomMenu, row: 1);
            else builder.AddField(IsFrench ? "Rooms" : "Rooms", IsFrench ? "Aucune room accessible sur ce serveur." : "No accessible room on this server.");
        }
        return new AstUiView(null, builder.Build(), components.Build());
    }

    private static async Task<SelectMenuBuilder?> BuildRoomMenuAsync(
        AstUiSession session,
        AstAuthorizationContext sourceAuthorization)
    {
        var guildId = session.GuildId.ToString(CultureInfo.InvariantCulture);
        var channelIds = await DatabaseCommands.GetAllChannelsAsync(guildId, "ChannelsAndUrlsTable").ConfigureAwait(false);
        var menu = new SelectMenuBuilder()
            .WithCustomId(Id(session, "select-room"))
            .WithPlaceholder(IsFrench ? "Choisir une room…" : "Choose a room…")
            .WithMinValues(1)
            .WithMaxValues(1);
        var count = 0;
        foreach (var channelId in channelIds.Distinct(StringComparer.Ordinal).Take(100))
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
            if (!allowed) continue;
            menu.AddOption(Safe(channel.Name)[..Math.Min(Safe(channel.Name).Length, 100)], channelId);
            if (++count == 25) break;
        }
        return count == 0 ? null : menu;
    }

    private static AstUiView RenderPersonal(AstUiSession session)
        => Screen(session,
            IsFrench ? "👤 Mon espace" : "👤 My space",
            IsFrench ? "Vos données et préférences personnelles." : "Your personal data and preferences.",
            [(IsFrench ? "Mes slots" : "My slots", "personal-slots"),
             (IsFrench ? "Mes objets" : "My items", "personal-items"),
             ("Hints", "personal-hints"),
             (IsFrench ? "Mon récap" : "My recap", "personal-recap"),
             (IsFrench ? "Mon patch" : "My patch", "personal-patch"),
             (IsFrench ? "Mes exclusions" : "My exclusions", "personal-exclusions"),
             (IsFrench ? "Mon portail" : "My portal", "personal-portal"),
             (IsFrench ? "Avancé" : "Advanced", "personal-advanced")]);

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
        var actions = new List<(string, string)>
        {
            (IsFrench ? "Configurer une room" : "Configure room", "admin-setup"),
            (IsFrench ? "Santé AST" : "AST health", "guild-health"),
            (IsFrench ? "Portail" : "Portal", "admin-portal")
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
            foreach (var alias in aliases.Take(25))
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
        var selectedAlias = string.IsNullOrWhiteSpace(session.SpoilerAlias)
            ? (IsFrench ? "aucun" : "none")
            : Safe(session.SpoilerAlias);
        var sphere = session.SpoilerSphereLimit?.ToString(CultureInfo.InvariantCulture) ?? (IsFrench ? "toutes" : "all");
        var description = IsFrench
            ? $"Slot : **{selectedAlias}**\nSphère maximale : **{sphere}**\nMode : **{(session.SpoilerMissingMode == "full" ? "complet" : "premier blocage")}**\nObjets : **{(session.SpoilerHideItems ? "masqués" : "visibles")}**\n\n« Configurer » permet aussi de saisir un slot au-delà des 25 premiers et de valider manuellement une sphère."
            : $"Slot: **{selectedAlias}**\nMaximum sphere: **{sphere}**\nMode: **{(session.SpoilerMissingMode == "full" ? "full" : "first blocker")}**\nItems: **{(session.SpoilerHideItems ? "hidden" : "visible")}**\n\nConfigure also lets you enter a slot beyond the first 25 and manually validate a sphere.";
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
        var yamls = YamlClass.GetYamlFileNames(channelId);
        var templates = YamlClass.GetTemplateFileNames();
        var components = new ComponentBuilder()
            .WithButton(IsFrench ? "Lister" : "List", Id(session, "yaml-list"), ButtonStyle.Primary, row: 0)
            .WithButton(IsFrench ? "Sauvegarder" : "Backup", Id(session, "yaml-backup"), ButtonStyle.Success, row: 0)
            .WithButton(IsFrench ? "Tout nettoyer" : "Clean all", Id(session, "yaml-clean-request"), ButtonStyle.Danger, row: 0)
            .WithButton(IsFrench ? "Portail" : "Portal", Id(session, "admin-portal"), ButtonStyle.Secondary, row: 0)
            .WithButton(IsFrench ? "Retour" : "Back", Id(session, "admin"), ButtonStyle.Secondary, row: 0);
        if (yamls.Count > 0)
        {
            var delete = new SelectMenuBuilder().WithCustomId(Id(session, "yaml-delete-select"))
                .WithPlaceholder(IsFrench ? "Supprimer un YAML…" : "Delete a YAML…");
            foreach (var file in yamls.Take(25)) delete.AddOption(file[..Math.Min(file.Length, 100)], file);
            components.WithSelectMenu(delete, row: 1);
        }
        if (templates.Count > 0)
        {
            var download = new SelectMenuBuilder().WithCustomId(Id(session, "yaml-template-download"))
                .WithPlaceholder(IsFrench ? "Télécharger un modèle…" : "Download a template…");
            foreach (var file in templates.Take(25)) download.AddOption(file[..Math.Min(file.Length, 100)], file);
            components.WithSelectMenu(download, row: 2);
        }
        var description = IsFrench
            ? $"{yamls.Count} fichier(s) YAML pour ce salon. Les actions sont exécutées directement depuis Discord."
            : $"{yamls.Count} YAML file(s) for this channel. Actions execute directly from Discord.";
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
        var ownAliases = (await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId).ConfigureAwait(false)).Keys
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        var available = new List<string>();
        foreach (var alias in allAliases.Distinct(StringComparer.OrdinalIgnoreCase))
            if ((await ReceiverAliasesCommands.GetAllUsersIds(guildId, channelId, alias).ConfigureAwait(false)).Count == 0)
                available.Add(alias);

        var components = new ComponentBuilder()
            .WithButton(IsFrench ? "Retour" : "Back", Id(session, "personal"), ButtonStyle.Primary, row: 0);
        if (available.Count > 0)
        {
            var add = new SelectMenuBuilder().WithCustomId(Id(session, "alias-add"))
                .WithPlaceholder(IsFrench ? "Associer un slot…" : "Associate a slot…");
            foreach (var alias in available.Take(25)) add.AddOption(Safe(alias)[..Math.Min(Safe(alias).Length, 100)], alias);
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
        if (ownAliases.Length > 0)
        {
            var delete = new SelectMenuBuilder().WithCustomId(Id(session, "alias-delete"))
                .WithPlaceholder(IsFrench ? "Dissocier un de mes slots…" : "Dissociate one of my slots…");
            foreach (var alias in ownAliases.Take(25)) delete.AddOption(Safe(alias)[..Math.Min(Safe(alias).Length, 100)], alias);
            components.WithSelectMenu(delete, row: 3);
        }
        var description = ownAliases.Length == 0
            ? (IsFrench ? "Aucun slot associé." : "No associated slot.")
            : string.Join("\n", ownAliases.Select(alias => $"• {Safe(alias)}"));
        return new AstUiView(null, BaseEmbed(IsFrench ? "👤 Mes slots" : "👤 My slots", description).Build(), components.Build());
    }

    private static async Task<AstUiView> RenderAdvancedAsync(AstUiSession session)
    {
        var guildId = session.GuildId.ToString(CultureInfo.InvariantCulture);
        var channelId = session.RoomChannelId!.Value.ToString(CultureInfo.InvariantCulture);
        var aliases = (await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(
            guildId, channelId, session.OwnerUserId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false)).Keys
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).Take(25).ToArray();
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
            foreach (var alias in aliases) { clean.AddOption(Safe(alias), alias); recapClean.AddOption(Safe(alias), alias); }
            components.WithSelectMenu(clean, row: 1).WithSelectMenu(recapClean, row: 2);
        }
        return new AstUiView(null, BaseEmbed(IsFrench ? "🧹 Récaps avancés" : "🧹 Advanced recaps",
            IsFrench ? "Toutes les suppressions demandent une confirmation." : "Every deletion requires confirmation.").Build(), components.Build());
    }

    private static async Task<AstUiView> RenderExclusionsAsync(AstUiSession session)
    {
        var guildId = session.GuildId.ToString(CultureInfo.InvariantCulture);
        var channelId = session.RoomChannelId!.Value.ToString(CultureInfo.InvariantCulture);
        var userId = session.OwnerUserId.ToString(CultureInfo.InvariantCulture);
        var aliases = (await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId).ConfigureAwait(false)).Keys
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).Take(25).ToArray();
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
            var items = session.PendingAction == "exclude-add"
                ? await ExcludedItemsCommands.GetItemNamesForAliasAsync(guildId, channelId, session.PendingAlias).ConfigureAwait(false)
                : await ExcludedItemsCommands.GetExcludedItemsForUserByAliasAsync(guildId, channelId, userId, session.PendingAlias).ConfigureAwait(false);
            var menu = new SelectMenuBuilder()
                .WithCustomId(Id(session, session.PendingAction == "exclude-add" ? "exclude-item-add" : "exclude-item-delete"))
                .WithPlaceholder(IsFrench ? "Choisir un objet…" : "Choose an item…");
            foreach (var item in items.Distinct(StringComparer.OrdinalIgnoreCase).Take(25)) menu.AddOption(Safe(item)[..Math.Min(Safe(item).Length, 100)], item);
            if (items.Count > 0) components.WithSelectMenu(menu, row: 1);
        }
        else if (aliases.Length > 0)
        {
            var add = new SelectMenuBuilder().WithCustomId(Id(session, "exclude-add-alias")).WithPlaceholder(IsFrench ? "Ajouter une exclusion au slot…" : "Add an exclusion for slot…");
            var delete = new SelectMenuBuilder().WithCustomId(Id(session, "exclude-delete-alias")).WithPlaceholder(IsFrench ? "Retirer une exclusion du slot…" : "Remove an exclusion from slot…");
            foreach (var alias in aliases) { add.AddOption(Safe(alias), alias); delete.AddOption(Safe(alias), alias); }
            components.WithSelectMenu(add, row: 1).WithSelectMenu(delete, row: 2);
        }
        return new AstUiView(null, BaseEmbed(IsFrench ? "🚫 Mes exclusions" : "🚫 My exclusions",
            IsFrench ? "Ajoutez ou retirez les exclusions de vos propres slots." : "Add or remove exclusions for your own slots.").Build(), components.Build());
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

    private static async Task<string> BuildPersonalItemsAsync(string guildId, string channelId, ulong userId)
    {
        var aliases = await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(
            guildId, channelId, userId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        if (aliases.Count == 0)
            return IsFrench ? "Aucun slot n’est associé à votre compte dans cette room." : "No slot is associated with your account in this room.";
        var output = new System.Text.StringBuilder(IsFrench ? "**Mes objets**" : "**My items**");
        foreach (var pair in aliases.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            output.AppendLine().AppendLine($"**{Safe(pair.Key)}**");
            foreach (var item in pair.Value.Take(30)) output.AppendLine($"• {Safe(item.Item)}");
            if (pair.Value.Count > 30) output.AppendLine(IsFrench ? $"… et {pair.Value.Count - 30} autre(s)" : $"… and {pair.Value.Count - 30} more");
        }
        return Clamp(output.ToString());
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
        var unique = hints.DistinctBy(hint => $"{hint.Finder}\0{hint.Receiver}\0{hint.Item}\0{hint.Location}").Take(30).ToArray();
        if (unique.Length == 0) return IsFrench ? "Aucun hint non trouvé pour vos slots." : "No unfound hint for your slots.";
        return Clamp((IsFrench ? "**Mes hints non trouvés**" : "**My unfound hints**") +
                     string.Concat(unique.Select(hint => $"\n• {Safe(hint.Item)} — {Safe(hint.Location)} ({Safe(hint.Finder)} → {Safe(hint.Receiver)})")));
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
            foreach (var item in items.Take(20)) output.AppendLine($"• {Safe(item)}");
            count += items.Count;
        }
        if (count == 0) output.AppendLine().Append(IsFrench ? "Aucune exclusion personnelle." : "No personal exclusion.");
        return Clamp(output.ToString());
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
        foreach (var patch in patches.Take(20))
            output.AppendLine($"• **{Safe(patch.Alias)}** · {Safe(patch.GameName)}\n  {Safe(patch.Patch)}");
        return Clamp(output.ToString());
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
