using ArchipelagoSphereTracker.src.TrackerLib.Services;
using Xunit;

public class ProcessingContextTests
{
    [Fact]
    public void SlotHelpers_ReturnDefaultsWhenMissing()
    {
        var ctx = new ProcessingContext();

        Assert.Equal("Player1", ctx.SlotAlias(1));
        Assert.Equal("Player2", ctx.SlotAlias(2));
        Assert.Equal(string.Empty, ctx.SlotGame(1));
    }

    [Fact]
    public void SlotHelpers_UseConfiguredAliases()
    {
        var ctx = new ProcessingContext();
        ctx.SlotIndex.Add(("Alice", "GameA"));
        ctx.SlotIndex.Add(("Bob", "GameB"));

        Assert.Equal("Alice", ctx.SlotAlias(1));
        Assert.Equal("GameB", ctx.SlotGame(2));
        Assert.Equal(("Bob", "GameB"), ctx.SlotAliasGame(2));
    }

    [Fact]
    public void DatasetLookups_ReturnNamesWhenPresent()
    {
        var ctx = new ProcessingContext();
        ctx.SetGameDataset("GameA", "dsA");
        ctx.SetDatasetItems("dsA", new[] { (101L, "Hookshot") });
        ctx.SetDatasetLocations("dsA", new[] { (202L, "Mountain") });

        Assert.True(ctx.TryGetItemName("GameA", 101L, out var itemName));
        Assert.Equal("Hookshot", itemName);

        Assert.True(ctx.TryGetLocationName("GameA", 202L, out var locationName));
        Assert.Equal("Mountain", locationName);

        Assert.False(ctx.TryGetItemName("GameA", 999L, out _));
        Assert.False(ctx.TryGetLocationName("GameA", 999L, out _));
    }
}
