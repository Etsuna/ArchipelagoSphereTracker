using System.Threading.Tasks;
using Xunit;

public sealed class AliasClassTests
{
    [Fact]
    public async Task Native_alias_actions_are_scoped_to_the_acting_user()
    {
        using var scope = new TestDatabaseScope();

        await AliasClass.AddAliasForUserAsync("Slot One", "0", "c", "g", "user-1");

        Assert.Equal(["user-1"], await ReceiverAliasesCommands.GetAllUsersIds("g", "c", "Slot One"));
        await AliasClass.DeleteAliasForUserAsync("Slot One", "c", "g", "user-2");
        Assert.Equal(["user-1"], await ReceiverAliasesCommands.GetAllUsersIds("g", "c", "Slot One"));

        await AliasClass.DeleteAliasForUserAsync("Slot One", "c", "g", "user-1");

        Assert.Empty(await ReceiverAliasesCommands.GetAllUsersIds("g", "c", "Slot One"));
    }
}
