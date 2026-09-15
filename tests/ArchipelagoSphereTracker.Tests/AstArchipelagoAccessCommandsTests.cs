using System.Threading.Tasks;
using Xunit;

public sealed class AstArchipelagoAccessCommandsTests
{
    [Fact]
    public async Task Denial_IsPersistentGuildScopedAndRevocable()
    {
        using var scope = new TestDatabaseScope();

        await AstArchipelagoAccessCommands.DenyAsync("100", "200", "300");

        Assert.True(await AstArchipelagoAccessCommands.IsDeniedAsync("100", "200"));
        Assert.False(await AstArchipelagoAccessCommands.IsDeniedAsync("101", "200"));
        var denial = Assert.Single(await AstArchipelagoAccessCommands.GetDeniedUsersAsync("100"));
        Assert.Equal("300", denial.DeniedByUserId);

        await AstArchipelagoAccessCommands.AllowAsync("100", "200");

        Assert.False(await AstArchipelagoAccessCommands.IsDeniedAsync("100", "200"));
    }

    [Fact]
    public async Task DenyingAgain_UpdatesTheActorWithoutCreatingDuplicates()
    {
        using var scope = new TestDatabaseScope();

        await AstArchipelagoAccessCommands.DenyAsync("100", "200", "300");
        await AstArchipelagoAccessCommands.DenyAsync("100", "200", "400");

        var denial = Assert.Single(await AstArchipelagoAccessCommands.GetDeniedUsersAsync("100"));
        Assert.Equal("400", denial.DeniedByUserId);
    }

    [Fact]
    public async Task GuildCleanup_RemovesArchipelagoDenials()
    {
        using var scope = new TestDatabaseScope();
        await AstArchipelagoAccessCommands.DenyAsync("100", "200", "300");

        await DatabaseCommands.DeleteChannelDataByGuildIdAsync("100");

        Assert.Empty(await AstArchipelagoAccessCommands.GetDeniedUsersAsync("100"));
    }
}
