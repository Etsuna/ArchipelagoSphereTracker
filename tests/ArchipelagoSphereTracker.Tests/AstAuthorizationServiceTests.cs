using System;
using Xunit;

public class AstAuthorizationServiceTests
{
    [Fact]
    public void GuildMember_CanOnlyUseMemberActions()
    {
        var member = Context();

        Assert.True(AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, member));
        Assert.False(AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, member));
        Assert.False(AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, member));
        Assert.False(AstAuthorizationService.IsAllowed(AstAuthorizationLevel.InstanceOwner, member));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ThreadOwnerOrManager_CanManageRoom(bool isThreadOwner, bool canManageThreads)
    {
        var actor = Context(isThreadOwner: isThreadOwner, canManageThreads: canManageThreads);

        Assert.True(AstAuthorizationService.IsAllowed(AstAuthorizationLevel.RoomManager, actor));
        Assert.False(AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, actor));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void GuildAuthority_CanManageGuild(bool isOwner, bool isAdministrator, bool canManageGuild)
    {
        var actor = Context(isGuildOwner: isOwner, isAdministrator: isAdministrator, canManageGuild: canManageGuild);

        Assert.True(AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, actor));
        Assert.False(AstAuthorizationService.IsAllowed(AstAuthorizationLevel.InstanceOwner, actor));
    }

    [Fact]
    public void InstanceOwner_CanPerformEveryLevel()
    {
        var owner = Context(isInstanceOwner: true);

        foreach (var level in Enum.GetValues<AstAuthorizationLevel>())
            Assert.True(AstAuthorizationService.IsAllowed(level, owner));
    }

    [Fact]
    public void NonMember_IsAlwaysDenied()
    {
        var actor = Context(isGuildMember: false, isInstanceOwner: true, isGuildOwner: true);

        foreach (var level in Enum.GetValues<AstAuthorizationLevel>())
            Assert.False(AstAuthorizationService.IsAllowed(level, actor));
    }

    [Theory]
    [InlineData("delete-url", true, AstAuthorizationLevel.RoomManager)]
    [InlineData("status-games-list", true, AstAuthorizationLevel.GuildMember)]
    [InlineData("ast-room-health", true, AstAuthorizationLevel.GuildMember)]
    [InlineData("ast-sync-now", true, AstAuthorizationLevel.RoomManager)]
    [InlineData("ast-pause", true, AstAuthorizationLevel.RoomManager)]
    [InlineData("ast-resume", true, AstAuthorizationLevel.RoomManager)]
    [InlineData("ast-polling", true, AstAuthorizationLevel.RoomManager)]
    [InlineData("ast-health", false, AstAuthorizationLevel.GuildManager)]
    [InlineData("send-apworld", false, AstAuthorizationLevel.InstanceOwner)]
    [InlineData("generate", false, AstAuthorizationLevel.GuildManager)]
    public void CommandMatrix_IsExplicit(string command, bool isThread, AstAuthorizationLevel expected)
    {
        Assert.Equal(expected, AstAuthorizationService.RequiredForDiscordCommand(command, isThread));
    }

    [Theory]
    [InlineData("ast-sync-now")]
    [InlineData("ast-pause")]
    [InlineData("ast-resume")]
    [InlineData("ast-polling")]
    public void MutatingTrackingCommands_AreAuditedAsRoomSettings(string command)
    {
        Assert.Equal(SecurityAuditAction.RoomSettingsUpdate, SecurityAuditLog.ForCommand(command));
    }

    [Theory]
    [InlineData("ast-health")]
    [InlineData("ast-room-health")]
    public void ReadOnlyTrackingCommands_DoNotCreateAuditNoise(string command)
    {
        Assert.Null(SecurityAuditLog.ForCommand(command));
    }

    private static AstAuthorizationContext Context(
        bool isGuildMember = true,
        bool isThreadOwner = false,
        bool canManageThreads = false,
        bool canManageGuild = false,
        bool isAdministrator = false,
        bool isGuildOwner = false,
        bool isInstanceOwner = false)
    {
        return new AstAuthorizationContext(
            isGuildMember,
            isThreadOwner,
            canManageThreads,
            canManageGuild,
            isAdministrator,
            isGuildOwner,
            isInstanceOwner);
    }
}
