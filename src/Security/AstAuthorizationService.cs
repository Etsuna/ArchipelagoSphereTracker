using Discord;

public enum AstAuthorizationLevel
{
    GuildMember,
    RoomManager,
    GuildManager,
    InstanceOwner
}

public sealed record AstAuthorizationContext(
    bool IsGuildMember,
    bool IsThreadOwner,
    bool CanManageThreads,
    bool CanManageGuild,
    bool IsAdministrator,
    bool IsGuildOwner,
    bool IsInstanceOwner);

public sealed record AstPortalActor(string UserId, IGuildUser User, AstAuthorizationContext Authorization);

public static class AstAuthorizationService
{
    public static string DeniedMessage => Declare.Language == "fr"
        ? "Vous n’avez pas les permissions requises pour cette action."
        : "You do not have permission to perform this action.";

    public static bool IsAllowed(AstAuthorizationLevel required, AstAuthorizationContext context)
    {
        if (!context.IsGuildMember)
            return false;

        return required switch
        {
            AstAuthorizationLevel.GuildMember => true,
            AstAuthorizationLevel.RoomManager =>
                context.IsThreadOwner || context.CanManageThreads || IsGuildManager(context),
            AstAuthorizationLevel.GuildManager => IsGuildManager(context),
            AstAuthorizationLevel.InstanceOwner => context.IsInstanceOwner,
            _ => false
        };
    }

    public static AstAuthorizationLevel RequiredForDiscordCommand(string commandName, bool isThread)
    {
        if (isThread)
        {
            return commandName switch
            {
                "delete-url" or
                "analyze-spoiler-log" or
                "send-spoiler-log" or
                "ast-room-portal" or
                "update-frequency-check" or
                "excluded-item" or
                "delete-excluded-item" or
                "update-silent-option" => AstAuthorizationLevel.RoomManager,
                _ => AstAuthorizationLevel.GuildMember
            };
        }

        return commandName switch
        {
            "list-apworld" or
            "backup-apworld" or
            "send-apworld" => AstAuthorizationLevel.InstanceOwner,
            "discord" or "apworlds-info" => AstAuthorizationLevel.GuildMember,
            _ => AstAuthorizationLevel.GuildManager
        };
    }

    public static async Task<AstAuthorizationContext?> CreateDiscordContextAsync(
        string guildId,
        string channelId,
        ulong userId,
        IGuildUser? knownUser = null)
    {
        if (!ulong.TryParse(guildId, out var guildIdValue))
            return null;

        var guild = Declare.Client.GetGuild(guildIdValue);
        if (guild == null)
            return null;

        IGuildUser? user = knownUser;
        user ??= guild.GetUser(userId);
        if (user == null)
        {
            try
            {
                user = await Declare.Client.Rest.GetGuildUserAsync(guildIdValue, userId);
            }
            catch (Discord.Net.HttpException)
            {
                return null;
            }
        }
        if (user == null)
            return null;

        if (!ulong.TryParse(channelId, out var channelIdValue) ||
            Declare.Client.GetChannel(channelIdValue) is not IGuildChannel guildChannel)
        {
            return null;
        }

        var thread = guildChannel as IThreadChannel;
        var isThreadOwner = thread?.OwnerId == userId;

        var configuredOwner = Declare.InstanceOwnerUserId;
        var isGuildOwner = guild.OwnerId == userId;
        var isInstanceOwner = !string.IsNullOrWhiteSpace(configuredOwner)
            ? string.Equals(configuredOwner, userId.ToString(), StringComparison.Ordinal)
            : isGuildOwner;

        var isGuildManager = isGuildOwner ||
                             user.GuildPermissions.Administrator ||
                             user.GuildPermissions.ManageGuild ||
                             user.GuildPermissions.ManageThreads ||
                             isInstanceOwner;
        var hasChannelAccess = user.GetPermissions(guildChannel).ViewChannel;
        if (hasChannelAccess && thread?.Type == ThreadType.PrivateThread && !isThreadOwner && !isGuildManager)
            hasChannelAccess = await IsThreadMemberAsync(thread, userId);

        return new AstAuthorizationContext(
            IsGuildMember: hasChannelAccess,
            IsThreadOwner: isThreadOwner,
            CanManageThreads: user.GuildPermissions.ManageThreads,
            CanManageGuild: user.GuildPermissions.ManageGuild,
            IsAdministrator: user.GuildPermissions.Administrator,
            IsGuildOwner: isGuildOwner,
            IsInstanceOwner: isInstanceOwner);
    }

    public static async Task<AstPortalActor?> ResolvePortalActorAsync(
        string guildId,
        string channelId,
        string token)
    {
        var userId = await PortalAccessCommands.GetUserIdByTokenAsync(guildId, channelId, token);
        if (string.IsNullOrWhiteSpace(userId) || !ulong.TryParse(userId, out var userIdValue))
            return null;

        var guildIdValue = ulong.Parse(guildId);
        var guild = Declare.Client.GetGuild(guildIdValue);
        if (guild == null)
            return null;

        IGuildUser? user = guild?.GetUser(userIdValue);
        if (user == null)
        {
            try
            {
                user = await Declare.Client.Rest.GetGuildUserAsync(guildIdValue, userIdValue);
            }
            catch (Discord.Net.HttpException)
            {
                return null;
            }
        }

        var context = await CreateDiscordContextAsync(guildId, channelId, userIdValue, user);
        return user == null || context == null ? null : new AstPortalActor(userId, user, context);
    }

    private static bool IsGuildManager(AstAuthorizationContext context)
    {
        return context.IsGuildOwner ||
               context.IsAdministrator ||
               context.CanManageGuild ||
               context.IsInstanceOwner;
    }

    private static async Task<bool> IsThreadMemberAsync(IThreadChannel thread, ulong userId)
    {
        try
        {
            await foreach (var batch in thread.GetUsersAsync())
            {
                if (batch.Any(user => user.Id == userId))
                    return true;
            }
        }
        catch (Discord.Net.HttpException)
        {
            return false;
        }

        return false;
    }
}
