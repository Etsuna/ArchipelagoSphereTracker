using System.Threading.Tasks;
using Xunit;

public sealed class ExcludedItemsCommandsTests
{
    [Fact]
    public async Task Personal_exclusion_query_never_returns_another_users_items()
    {
        using var scope = new TestDatabaseScope();
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ExcludedItemTable (GuildId, ChannelId, UserId, Alias, Item)
                VALUES ('g', 'c', 'user-1', 'Slot', 'Private One'),
                       ('g', 'c', 'user-2', 'Slot', 'Private Two');";
            await command.ExecuteNonQueryAsync();
        });

        var items = await ExcludedItemsCommands.GetExcludedItemsForUserByAliasAsync("g", "c", "user-1", "Slot");

        Assert.Equal(["Private One"], items);
    }
}
