using System.Threading.Tasks;
using Xunit;

public sealed class AstRoleBindingsCommandsTests
{
    [Fact]
    public async Task GuildManagerBinding_IsPersistentScopedAndRevocable()
    {
        using var scope = new TestDatabaseScope();

        await AstRoleBindingsCommands.GrantGuildManagerAsync("100", "200", "300");

        Assert.True(await AstRoleBindingsCommands.IsGuildManagerAsync("100", "200"));
        Assert.False(await AstRoleBindingsCommands.IsGuildManagerAsync("101", "200"));
        var binding = Assert.Single(await AstRoleBindingsCommands.GetGuildManagersAsync("100"));
        Assert.Equal("300", binding.GrantedByUserId);

        await AstRoleBindingsCommands.RevokeGuildManagerAsync("100", "200");

        Assert.False(await AstRoleBindingsCommands.IsGuildManagerAsync("100", "200"));
    }

    [Fact]
    public async Task GrantingAgain_UpdatesTheGrantorWithoutCreatingDuplicates()
    {
        using var scope = new TestDatabaseScope();

        await AstRoleBindingsCommands.GrantGuildManagerAsync("100", "200", "300");
        await AstRoleBindingsCommands.GrantGuildManagerAsync("100", "200", "400");

        var binding = Assert.Single(await AstRoleBindingsCommands.GetGuildManagersAsync("100"));
        Assert.Equal("400", binding.GrantedByUserId);
    }

    [Fact]
    public async Task GuildCleanup_RemovesDelegatedRights()
    {
        using var scope = new TestDatabaseScope();
        await AstRoleBindingsCommands.GrantGuildManagerAsync("100", "200", "300");

        await DatabaseCommands.DeleteChannelDataByGuildIdAsync("100");

        Assert.Empty(await AstRoleBindingsCommands.GetGuildManagersAsync("100"));
    }
}
