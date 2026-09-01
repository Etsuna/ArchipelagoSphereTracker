using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.src.TrackerLib.Services;
using TrackerLib.Models;
using Xunit;

public sealed class WebHostCharacterizationTests
{
    [Fact]
    public void RoomStatusFixture_DeserializesKnownFieldsAndIgnoresUnknownFields()
    {
        var json = ReadFixture("room-status-current.json");

        var status = JsonSerializer.Deserialize<RoomStatus>(json);

        Assert.NotNull(status);
        Assert.Equal("tracker-anonymized", status!.Tracker);
        Assert.Equal(38281, status.LastPort);
        Assert.Equal(2, status.Players.Count);
        Assert.Equal(("Alice", "GameA"), (status.Players[0].Name, status.Players[0].Game));
        Assert.Equal(2, status.Downloads.Count);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), status.LastActivity);
    }

    [Fact]
    public void TrackerFixtures_ParseItemsHintsAndStatusesWithFallbacks()
    {
        var context = CreateContext();
        var runtime = ReadFixture("tracker-runtime-current.json");
        var staticTracker = ReadFixture("tracker-static-current.json");

        var items = TrackerStreamParser.ParseItems(context, runtime);
        var hints = TrackerStreamParser.ParseHints(context, runtime);
        var totals = TrackerStreamParser.ParsePlayerLocationTotals(staticTracker);
        var statuses = TrackerStreamParser.ParseGameStatus(context, runtime, totals);

        Assert.Equal(2, items.Count);
        Assert.Equal("Magic Sword", items[0].Item);
        Assert.Equal("Castle", items[0].Location);
        Assert.Equal("101", items[1].Item);
        Assert.Equal("201", items[1].Location);

        Assert.Equal(2, hints.Count);
        Assert.Equal("Vanilla", hints[0].Entrance);
        Assert.Equal("False", hints[0].Flag);
        Assert.Equal("Entrance", hints[1].Entrance);
        Assert.Equal("True", hints[1].Flag);

        Assert.Collection(statuses,
            alice =>
            {
                Assert.Equal("Alice", alice.Name);
                Assert.Equal("3", alice.Checks);
                Assert.Equal("10", alice.Total);
            },
            bob =>
            {
                Assert.Equal("Bob", bob.Name);
                Assert.Equal("1", bob.Checks);
                Assert.Equal("8", bob.Total);
                Assert.Equal(string.Empty, bob.LastActivity);
            });
    }

    [Fact]
    public void ReorderedTrackerFixture_ProducesEquivalentSemanticData()
    {
        var currentContext = CreateContext();
        var reorderedContext = CreateContext();
        var current = ReadFixture("tracker-runtime-current.json");
        var reordered = ReadFixture("tracker-runtime-reordered.json");
        var totals = TrackerStreamParser.ParsePlayerLocationTotals(ReadFixture("tracker-static-current.json"));

        var currentItems = TrackerStreamParser.ParseItems(currentContext, current).Select(ItemKey).Order().ToArray();
        var reorderedItems = TrackerStreamParser.ParseItems(reorderedContext, reordered).Select(ItemKey).Order().ToArray();
        var currentHints = TrackerStreamParser.ParseHints(currentContext, current).Select(HintKey).Order().ToArray();
        var reorderedHints = TrackerStreamParser.ParseHints(reorderedContext, reordered).Select(HintKey).Order().ToArray();
        var currentStatuses = TrackerStreamParser.ParseGameStatus(currentContext, current, totals).Select(StatusKey).Order().ToArray();
        var reorderedStatuses = TrackerStreamParser.ParseGameStatus(reorderedContext, reordered, totals).Select(StatusKey).Order().ToArray();

        Assert.Equal(currentItems, reorderedItems);
        Assert.Equal(currentHints, reorderedHints);
        Assert.Equal(currentStatuses, reorderedStatuses);
    }

    [Fact]
    public async Task ReimportingObservedItems_DoesNotDuplicatePersistedHistory()
    {
        using var scope = new TestDatabaseScope();
        var context = CreateContext();
        var items = TrackerStreamParser.ParseItems(context, ReadFixture("tracker-runtime-current.json"));

        await DisplayItemCommands.AddItemsAsync(items, "guild-fixture", "channel-fixture");
        await DisplayItemCommands.AddItemsAsync(items, "guild-fixture", "channel-fixture");

        Assert.Equal(items.Count, await TestDatabaseScope.CountRowsAsync(
            "DisplayedItemTable",
            "guild-fixture",
            "channel-fixture"));
    }

    private static ProcessingContext CreateContext()
    {
        var context = new ProcessingContext();
        context.SlotIndex.Add(("Alice", "GameA"));
        context.SlotIndex.Add(("Bob", "GameB"));
        context.SetGameDataset("GameA", "dataset-a");
        context.SetGameDataset("GameB", "dataset-b");
        context.SetDatasetItems("dataset-a", new[] { (100L, "Magic Sword") });
        context.SetDatasetLocations("dataset-b", new[] { (200L, "Castle") });
        return context;
    }

    private static string ReadFixture(string name)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "WebHost", name));

    private static string ItemKey(DisplayedItem item)
        => $"{item.Finder}|{item.Receiver}|{item.Item}|{item.Location}|{item.Game}|{item.Flag}";

    private static string HintKey(HintStatus hint)
        => $"{hint.Finder}|{hint.Receiver}|{hint.Item}|{hint.Location}|{hint.Game}|{hint.Entrance}|{hint.Flag}";

    private static string StatusKey(GameStatus status)
        => $"{status.Name}|{status.Game}|{status.Checks}|{status.Total}|{status.LastActivity}";
}
