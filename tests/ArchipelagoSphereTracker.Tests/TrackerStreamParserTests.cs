using ArchipelagoSphereTracker.src.TrackerLib.Services;
using Xunit;

public class TrackerStreamParserTests
{
    [Fact]
    public void ParseItems_ResolvesAliasesAndNames()
    {
        var ctx = new ProcessingContext();
        ctx.SlotIndex.Add(("Alice", "GameA"));
        ctx.SlotIndex.Add(("Bob", "GameB"));
        ctx.SetGameDataset("GameA", "dsA");
        ctx.SetGameDataset("GameB", "dsB");
        ctx.SetDatasetItems("dsA", new[] { (100L, "Magic Sword") });
        ctx.SetDatasetLocations("dsB", new[] { (200L, "Castle") });

        var json = "{" +
                   "\"player_items_received\":[{" +
                   "\"player\":1," +
                   "\"items\":[[100,200,2,1],[101,201,1,0]]" +
                   "}]}";

        var items = TrackerStreamParser.ParseItems(ctx, json);

        Assert.Equal(2, items.Count);
        Assert.Equal("Bob", items[0].Finder);
        Assert.Equal("Alice", items[0].Receiver);
        Assert.Equal("Magic Sword", items[0].Item);
        Assert.Equal("Castle", items[0].Location);
        Assert.Equal("GameB", items[0].Game);
        Assert.Equal("1", items[0].Flag);

        Assert.Equal("101", items[1].Item);
        Assert.Equal("201", items[1].Location);
    }

    [Fact]
    public void ParseHints_UsesEntranceFallbackAndFlags()
    {
        var ctx = new ProcessingContext();
        ctx.SlotIndex.Add(("Alice", "GameA"));
        ctx.SlotIndex.Add(("Bob", "GameB"));
        ctx.SetGameDataset("GameA", "dsA");
        ctx.SetGameDataset("GameB", "dsB");
        ctx.SetDatasetItems("dsA", new[] { (400L, "Potion") });
        ctx.SetDatasetLocations("dsB", new[] { (300L, "Forest") });

        var json = "{" +
                   "\"hints\":[{" +
                   "\"player\":1," +
                   "\"hints\":[[2,1,300,400,true,\"Entrance\"],[2,1,301,401,false,\"\"]]" +
                   "}]}";

        var hints = TrackerStreamParser.ParseHints(ctx, json);

        Assert.Equal(2, hints.Count);
        Assert.Equal("Entrance", hints[0].Entrance);
        Assert.Equal("True", hints[0].Flag);
        Assert.Equal("Vanilla", hints[1].Entrance);
        Assert.Equal("False", hints[1].Flag);
    }

    [Fact]
    public void ParseGameStatus_CombinesStaticAndRuntimeData()
    {
        var ctx = new ProcessingContext();
        ctx.SlotIndex.Add(("Alice", "GameA"));
        ctx.SlotIndex.Add(("Bob", "GameB"));

        var json = "{" +
                   "\"activity_timers\":[{\"player\":1,\"time\":\"1:00\"}]," +
                   "\"player_checks_done\":[{\"player\":1,\"locations\":[1,2,3]},{\"player\":2,\"locations\":[4]}]" +
                   "}";

        var jsonStatic = "{" +
                         "\"player_locations_total\":[{\"player\":1,\"total_locations\":10},{\"player\":2,\"total_locations\":8}]" +
                         "}";

        var status = TrackerStreamParser.ParseGameStatus(ctx, json, jsonStatic);

        Assert.Equal(2, status.Count);
        Assert.Equal("Alice", status[0].Name);
        Assert.Equal("3", status[0].Checks);
        Assert.Equal("10", status[0].Total);
        Assert.Equal("1:00", status[0].LastActivity);

        Assert.Equal("Bob", status[1].Name);
        Assert.Equal("1", status[1].Checks);
        Assert.Equal("8", status[1].Total);
        Assert.Equal(string.Empty, status[1].LastActivity);
    }
}
