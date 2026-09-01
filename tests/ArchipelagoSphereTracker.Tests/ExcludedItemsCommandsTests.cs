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

    [Fact]
    public async Task Native_add_and_delete_only_change_the_acting_users_exclusion()
    {
        using var scope = new TestDatabaseScope();
        await ExcludedItemsCommands.AddExcludedItemForUserAsync("g", "c", "user-1", "Slot", "Item");
        await ExcludedItemsCommands.AddExcludedItemForUserAsync("g", "c", "user-2", "Slot", "Item");

        await ExcludedItemsCommands.DeleteExcludedItemForUserAsync("g", "c", "user-1", "Slot", "Item");

        Assert.Empty(await ExcludedItemsCommands.GetExcludedItemsForUserByAliasAsync("g", "c", "user-1", "Slot"));
        Assert.Equal(["Item"], await ExcludedItemsCommands.GetExcludedItemsForUserByAliasAsync("g", "c", "user-2", "Slot"));
    }

    [Fact]
    public async Task Item_catalog_returns_every_item_for_the_exact_alias_game()
    {
        using var scope = new TestDatabaseScope();
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO AliasChoicesTable (GuildId, ChannelId, Slot, Alias, Game)
                VALUES ('g', 'c', 1, 'Slot%', 'Game A'),
                       ('g', 'c', 2, 'Slot Other', 'Game B');
                INSERT INTO DatapackageGameMap (GuildId, ChannelId, GameName, DatasetKey, ImportedAt)
                VALUES ('g', 'c', 'Game A', 'dataset-a', '2026-09-01T00:00:00Z'),
                       ('g', 'c', 'Game B', 'dataset-b', '2026-09-01T00:00:00Z');
                WITH RECURSIVE numbers(value) AS (
                    SELECT 1
                    UNION ALL
                    SELECT value + 1 FROM numbers WHERE value < 60
                )
                INSERT INTO DatapackageItems (GuildId, ChannelId, DatasetKey, Id, Name)
                SELECT 'g', 'c', 'dataset-a', value, printf('Item %03d', value) FROM numbers;
                INSERT INTO DatapackageItems (GuildId, ChannelId, DatasetKey, Id, Name)
                VALUES ('g', 'c', 'dataset-b', 1, 'Wrong game item');";
            await command.ExecuteNonQueryAsync();
        });

        var items = await ExcludedItemsCommands.GetItemNamesForAliasAsync("g", "c", "Slot%");

        Assert.Equal(60, items.Count);
        Assert.Equal("Item 001", items[0]);
        Assert.Equal("Item 060", items[^1]);
        Assert.DoesNotContain("Wrong game item", items);
    }
}
