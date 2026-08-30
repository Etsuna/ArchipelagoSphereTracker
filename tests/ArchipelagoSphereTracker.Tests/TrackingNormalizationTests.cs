using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArchipelagoSphereTracker.src.TrackerLib.Services;
using ArchipelagoSphereTracker.Tracking.V2;
using Xunit;

public sealed class TrackingNormalizationTests
{
    private static readonly DateTimeOffset InitialTime = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public void ReorderedPayload_ProducesSameNormalizedHashAndRawIdentities()
    {
        var totals = TrackerStreamParser.ParsePlayerLocationTotals(ReadFixture("tracker-static-current.json"));
        var current = LegacySnapshotAdapter.FromWebHostResponse(
            CreateContext(),
            ReadFixture("tracker-runtime-current.json"),
            totals,
            InitialTime,
            InitialTime.AddMinutes(-5),
            roomActivityKnown: true);
        var reordered = LegacySnapshotAdapter.FromWebHostResponse(
            CreateContext(),
            ReadFixture("tracker-runtime-reordered.json"),
            totals,
            InitialTime.AddHours(1),
            InitialTime.AddMinutes(-5),
            roomActivityKnown: true);

        Assert.Equal(current.ContentHash, reordered.ContentHash);
        Assert.Equal(SnapshotSections.Items, current.CompleteSections & SnapshotSections.Items);
        Assert.True(current.IsComplete(SnapshotSections.PlayerStatuses | SnapshotSections.Goals));
        Assert.Equal("Alice", current.Slots.Single(slot => slot.Slot == 1).Alias);
        Assert.Equal("Bobby", current.Slots.Single(slot => slot.Slot == 2).Alias);
        Assert.Equal(NormalizedPlayerStatus.GoalReached,
            current.PlayerStates.Single(state => state.Slot == 2).Status);
        Assert.True(current.Goals.Single(goal => goal.Slot == 2).Completed);
        Assert.Contains(current.Items, item =>
            item.FinderSlot == 2 && item.ReceiverSlot == 1 &&
            item.ItemId == 100 && item.LocationId == 200 &&
            item.ItemDisplayName == "Magic Sword");
        Assert.Contains(current.Items, item => item.ItemId == 101 && item.ItemDisplayName == "101");
        Assert.Equal(new long[] { 11, 12, 13 },
            current.Checks.Where(check => check.Slot == 1).Select(check => check.LocationId));
    }

    [Fact]
    public void FirstSnapshot_IsSilentBaseline()
    {
        var current = CreateSnapshot();

        Assert.Empty(TrackingSnapshotDiff.Diff(null, current));
    }

    [Fact]
    public void Diff_ProducesEveryDataEventWithDeterministicKeys()
    {
        var item = CreateItem();
        var previousHint = CreateHint(itemId: 200, locationId: 300, found: false);
        var updatedHint = previousHint with { Found = true };
        var createdHint = CreateHint(itemId: 201, locationId: 301, found: false);

        var previous = CreateSnapshot(
            hints: [previousHint],
            checks: [new NormalizedCheck(1, 10)],
            goals: [new NormalizedGoal(1, "completion", false)],
            playerStates: [new NormalizedPlayerState(1, NormalizedPlayerStatus.Playing, 1, 2, "00:01:00")],
            lastActivityUtc: InitialTime);
        var current = CreateSnapshot(
            items: [item],
            hints: [updatedHint, createdHint],
            checks: [new NormalizedCheck(1, 10), new NormalizedCheck(1, 11)],
            goals: [new NormalizedGoal(1, "completion", true)],
            playerStates: [new NormalizedPlayerState(1, NormalizedPlayerStatus.GoalReached, 2, 2, "00:02:00")],
            lastActivityUtc: InitialTime.AddMinutes(1));

        var firstDiff = TrackingSnapshotDiff.Diff(previous, current);
        var secondDiff = TrackingSnapshotDiff.Diff(previous, current);

        Assert.Collection(firstDiff,
            value => Assert.IsType<ItemReceivedEvent>(value),
            value => Assert.IsType<ItemSentEvent>(value),
            value => Assert.IsType<HintUpdatedEvent>(value),
            value => Assert.IsType<HintCreatedEvent>(value),
            value => Assert.IsType<GoalReachedEvent>(value),
            value => Assert.IsType<PlayerStatusChangedEvent>(value),
            value => Assert.IsType<CheckCompletedEvent>(value),
            value => Assert.IsType<RoomActivityChangedEvent>(value));
        Assert.Equal(firstDiff.Select(value => value.EventKey), secondDiff.Select(value => value.EventKey));
        Assert.All(firstDiff, value => Assert.Matches("^[0-9a-f]{64}$", value.EventKey));
        Assert.Equal(firstDiff.Length, firstDiff.Select(value => value.EventKey).Distinct().Count());
    }

    [Fact]
    public void TrackingTransitions_EmitErrorOnceThenRecovery()
    {
        var healthy = CreateSnapshot();
        var failed = LegacySnapshotAdapter.FromTrackingFailure(healthy, InitialTime.AddMinutes(1), "http_503");
        var repeatedFailure = LegacySnapshotAdapter.FromTrackingFailure(failed, InitialTime.AddMinutes(2), "HTTP_503");
        var recovered = CreateSnapshot(capturedAtUtc: InitialTime.AddMinutes(3));
        var failedAgain = LegacySnapshotAdapter.FromTrackingFailure(recovered, InitialTime.AddMinutes(4), "HTTP_503");

        var error = Assert.Single(TrackingSnapshotDiff.Diff(healthy, failed));
        var firstError = Assert.IsType<TrackingErrorEvent>(error);
        Assert.Equal("HTTP_503", firstError.ErrorCode);
        Assert.Empty(TrackingSnapshotDiff.Diff(failed, repeatedFailure));
        Assert.IsType<TrackingRecoveredEvent>(Assert.Single(TrackingSnapshotDiff.Diff(repeatedFailure, recovered)));
        var secondError = Assert.IsType<TrackingErrorEvent>(
            Assert.Single(TrackingSnapshotDiff.Diff(recovered, failedAgain)));
        Assert.NotEqual(firstError.EventKey, secondError.EventKey);
    }

    [Fact]
    public void PartialPayload_DoesNotCreateEventsForMissingCollections()
    {
        var previous = CreateSnapshot(
            items: [CreateItem()],
            hints: [CreateHint(200, 300, false)],
            checks: [new NormalizedCheck(1, 10)],
            goals: [new NormalizedGoal(1, "completion", false)]);
        var partial = NormalizedRoomSnapshot.Create(
            "123",
            "456",
            previous.Slots,
            [],
            [],
            [],
            [],
            previous.PlayerStates,
            null,
            InitialTime.AddMinutes(1),
            InitialTime.AddMinutes(1),
            TrackingObservationState.Healthy,
            null,
            SnapshotSections.Slots | SnapshotSections.Tracking);

        Assert.Empty(TrackingSnapshotDiff.Diff(previous, partial));
    }

    [Fact]
    public void NullAndUnknownFields_AreMarkedIncompleteWithoutSyntheticData()
    {
        const string partialJson = """
            {
              "player_items_received": null,
              "hints": null,
              "future_collection": [1, 2, 3]
            }
            """;

        var snapshot = LegacySnapshotAdapter.FromWebHostResponse(
            CreateContext(),
            partialJson,
            new Dictionary<int, int>(),
            InitialTime);

        Assert.False(snapshot.IsComplete(SnapshotSections.Items));
        Assert.False(snapshot.IsComplete(SnapshotSections.Hints));
        Assert.False(snapshot.IsComplete(SnapshotSections.Checks));
        Assert.Empty(snapshot.Items);
        Assert.Empty(snapshot.Hints);
        Assert.Empty(snapshot.Checks);
    }

    [Fact]
    public void StableEventKeys_DoNotDependOnAliasesOrLocalizedNames()
    {
        var baseline = CreateSnapshot(items: []);
        var english = CreateSnapshot(items: [CreateItem()]);
        var french = CreateSnapshot(items:
        [
            CreateItem() with
            {
                FinderDisplayName = "Robert",
                ReceiverDisplayName = "Alice FR",
                ItemDisplayName = "Épée magique",
                LocationDisplayName = "Château"
            }
        ]);

        var englishKey = TrackingSnapshotDiff.Diff(baseline, english).OfType<ItemReceivedEvent>().Single().EventKey;
        var frenchKey = TrackingSnapshotDiff.Diff(baseline, french).OfType<ItemReceivedEvent>().Single().EventKey;

        Assert.Equal(englishKey, frenchKey);
    }

    [Fact]
    public void SnapshotFactory_DeduplicatesAndSortsCollections()
    {
        var duplicate = CreateItem();
        var snapshot = CreateSnapshot(
            items: [duplicate, duplicate],
            checks: [new NormalizedCheck(2, 20), new NormalizedCheck(1, 10), new NormalizedCheck(1, 10)]);

        Assert.Single(snapshot.Items);
        Assert.Equal(
            new[] { new NormalizedCheck(1, 10), new NormalizedCheck(2, 20) },
            snapshot.Checks);
    }

    private static NormalizedRoomSnapshot CreateSnapshot(
        IEnumerable<NormalizedItemTransfer>? items = null,
        IEnumerable<NormalizedHint>? hints = null,
        IEnumerable<NormalizedCheck>? checks = null,
        IEnumerable<NormalizedGoal>? goals = null,
        IEnumerable<NormalizedPlayerState>? playerStates = null,
        DateTimeOffset? lastActivityUtc = null,
        DateTimeOffset? capturedAtUtc = null)
    {
        return NormalizedRoomSnapshot.Create(
            "123",
            "456",
            [new NormalizedSlot(1, "Alice", "Alice", "GameA"), new NormalizedSlot(2, "Bob", "Bob", "GameB")],
            items ?? [],
            hints ?? [],
            checks ?? [],
            goals ?? [],
            playerStates ??
            [
                new NormalizedPlayerState(1, NormalizedPlayerStatus.Playing, 0, 2, null),
                new NormalizedPlayerState(2, NormalizedPlayerStatus.Playing, 0, 2, null)
            ],
            lastActivityUtc,
            capturedAtUtc ?? InitialTime,
            capturedAtUtc ?? InitialTime,
            TrackingObservationState.Healthy,
            null,
            SnapshotSections.All);
    }

    private static NormalizedItemTransfer CreateItem()
        => new(2, 1, 100, 200, 1, "Bob", "Alice", "Magic Sword", "Castle", "GameB", "GameA");

    private static NormalizedHint CreateHint(long itemId, long locationId, bool found)
        => new(1, 2, itemId, locationId, found, "Vanilla", "Alice", "Bob", itemId.ToString(), locationId.ToString(), "GameA", "GameB");

    private static ProcessingContext CreateContext()
    {
        var context = new ProcessingContext { GuildId = "123", ChannelId = "456" };
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
}
