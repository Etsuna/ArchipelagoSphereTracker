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
    Help
}

public sealed record AstUiSession(
    string Id,
    ulong OwnerUserId,
    ulong GuildId,
    ulong SourceChannelId,
    ulong? RoomChannelId,
    AstUiScreen Screen,
    DateTimeOffset ExpiresAtUtc);

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
        if (!TryParseCustomId(component.Data.CustomId, out var sessionId, out var action) || action != "select-room")
            return;
        if (component.GuildId is not { } guildId || component.ChannelId is not { } sourceChannelId ||
            component.Data.Values.FirstOrDefault() is not { } selected || !ulong.TryParse(selected, out var roomChannelId) ||
            !Sessions.TryGetAuthorized(sessionId, component.User.Id, guildId, sourceChannelId, out var session))
        {
            await component.RespondAsync(IsFrench ? "Cette interface a expiré. Relancez `/ast`." : "This interface expired. Run `/ast` again.", ephemeral: true);
            return;
        }

        await component.DeferAsync(ephemeral: true);
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
            "personal-slots" or "personal-items" or "personal-hints" or "personal-recap" or "personal-advanced" or
            "manage-polling" or "manage-more" or "admin-setup" or "admin-portal" or "admin-yaml" or "admin-generation"
                => IsFrench ? "Cet écran sera branché à l’étape suivante de la PR 9." : "This screen will be connected in the next PR 9 step.",
            _ => AstAuthorizationService.DeniedMessage
        };
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
