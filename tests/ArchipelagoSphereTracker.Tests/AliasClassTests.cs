using System.Linq;
using System.Threading.Tasks;
using Xunit;

public sealed class AliasClassTests
{
    [Fact]
    public async Task Native_alias_actions_are_scoped_to_the_acting_user()
    {
        using var scope = new TestDatabaseScope();

        await AliasClass.AddAliasForUserAsync("Slot One", "0", "c", "g", "user-1");
        await AliasClass.AddAliasForUserAsync("Slot One", "0", "c", "g", "user-2");
        await AliasClass.AddAliasForUserAsync("Slot One", "0", "c", "g", "user-1");

        Assert.Equal(
            ["user-1", "user-2"],
            (await ReceiverAliasesCommands.GetAllUsersIds("g", "c", "Slot One")).OrderBy(value => value));
        await AliasClass.DeleteAliasForUserAsync("Slot One", "c", "g", "user-2");
        Assert.Equal(["user-1"], await ReceiverAliasesCommands.GetAllUsersIds("g", "c", "Slot One"));

        await AliasClass.DeleteAliasForUserAsync("Slot One", "c", "g", "user-1");

        Assert.Empty(await ReceiverAliasesCommands.GetAllUsersIds("g", "c", "Slot One"));
    }

    [Fact]
    public async Task Shared_slot_history_is_added_only_to_the_new_users_recap()
    {
        using var scope = new TestDatabaseScope();
        await DisplayItemCommands.AddItemsAsync(
            [new DisplayedItem
            {
                Finder = "Sender",
                Receiver = "Shared Slot",
                Item = "Existing Item",
                Location = "Location",
                Game = "Game",
                Flag = "0"
            }],
            "g",
            "c");

        await AliasClass.AddAliasForUserAsync("Shared Slot", "0", "c", "g", "user-1");
        await AliasClass.AddAliasForUserAsync("Shared Slot", "0", "c", "g", "user-2");

        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT r.UserId, COUNT(i.Id)
            FROM RecapListTable r
            LEFT JOIN RecapListItemsTable i ON i.RecapListTableId = r.Id
            WHERE r.GuildId = 'g' AND r.ChannelId = 'c' AND r.Alias = 'Shared Slot'
            GROUP BY r.UserId
            ORDER BY r.UserId;";
        using var reader = await command.ExecuteReaderAsync();
        var counts = new System.Collections.Generic.Dictionary<string, long>();
        while (await reader.ReadAsync())
            counts[reader.GetString(0)] = reader.GetInt64(1);

        Assert.Equal(1, counts["user-1"]);
        Assert.Equal(1, counts["user-2"]);
    }
}
