using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.src.TrackerLib.Services;
using ArchipelagoSphereTracker.Tracking.Notifications;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;
using Xunit;

public sealed class TrackingV2NotificationTests : IDisposable
{
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentUICulture;

    public TrackingV2NotificationTests()
        => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose() => CultureInfo.CurrentUICulture = _originalCulture;

    private static readonly DateTimeOffset T0 = DateTimeOffset.UtcNow.AddMinutes(-10);
    private static readonly TrackingDestination Destination = TrackingDestination.DiscordNotification("456");
    private static readonly NormalizedItemTransfer Item = new(2, 1, 100, 200, 1,
        "Bob", "Alice", "Magic Sword", "Castle", "GameB", "GameA");
    private static readonly NormalizedHint Hint = new(2, 1, 100, 200, false, "Vanilla",
        "Bob", "Alice", "Magic Sword", "Castle", "GameB", "GameA");

    [Fact]
    public async Task Cutover_keeps_shadow_history_but_never_publishes_it()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        var store = new TrackingV2Store();
        var shadow = TrackingDestination.DiscordChannel("456");
        await store.ApplySnapshotAsync(Snapshot(), [shadow]);
        await store.ApplySnapshotAsync(Snapshot(items: [Item]), [shadow]);
        var baseline = await store.ApplySnapshotAsync(Snapshot(items: [Item, Item with { ItemId = 101 }]),
            [Destination], enableNotifications: true);
        Assert.True(baseline.BaselineCreated);
        Assert.Equal(0, baseline.DeliveriesCreated);

        var transport = new FakeTransport();
        Assert.False(await Worker(transport).RunOnceAsync());
        Assert.Empty(transport.Sent);
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));

        var current = Snapshot(items: [Item, Item with { ItemId = 101 }, Item with { ItemId = 102 }]);
        Assert.Equal(1, (await store.ApplySnapshotAsync(current, [Destination], enableNotifications: true)).DeliveriesCreated);
        Assert.True(await Worker(transport).RunOnceAsync());
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task Restart_reuses_baseline_and_queues_each_new_notification_once()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot(), [Destination], enableNotifications: true);
        var current = Snapshot(items: [Item], hints: [Hint], completed: true);
        var result = await new TrackingV2Store().ApplySnapshotAsync(current, [Destination], enableNotifications: true);
        Assert.Equal(3, result.DeliveriesCreated); // sent/check/status remain ledger-only
        Assert.Equal(5, result.EventsCreated);
        Assert.False((await new TrackingV2Store().ApplySnapshotAsync(current, [Destination], enableNotifications: true)).SnapshotChanged);
        var transport = new FakeTransport();
        var worker = Worker(transport);
        while (await worker.RunOnceAsync()) { }
        Assert.Equal(3, transport.Sent.Count);
        Assert.Equal(3, transport.Sent.Keys.Distinct().Count());
    }

    [Fact]
    public async Task Items_from_one_poll_are_grouped_by_receiver_with_a_total_count()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(), [Destination], enableNotifications: true);
        var items = Enumerable.Range(1, 12).Select(index => Item with
        {
            ItemId = 100 + index, LocationId = 200 + index,
            ItemDisplayName = $"Sword {index}", LocationDisplayName = $"Castle {index}"
        }).ToArray();
        await store.ApplySnapshotAsync(Snapshot(items: items), [Destination], enableNotifications: true);
        var transport = new FakeTransport();
        var worker = Worker(transport);
        while (await worker.RunOnceAsync()) { }
        var message = Assert.Single(transport.Sent.Values);
        Assert.Contains("(12)", message.Content);
        Assert.Equal(12, message.Content.Split('\n').Count(line => line.StartsWith("• ") || line.StartsWith(">>> • ")));
        Assert.Equal(12, message.Content.Split("Bob").Length - 1);
        Assert.DoesNotContain("arrow_down_right", message.Content);
        Assert.Equal(1, transport.SendCalls);
        Assert.Equal("12", await ScalarAsync("SELECT COUNT(*) FROM EventDeliveries WHERE Status = 'Delivered';"));
        Assert.DoesNotContain("AST •", message.Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Interleaved_items_keep_their_order_with_a_sender_on_each_line(bool identicalNames)
    {
        using var scope = new TestDatabaseScope();
        var secondName = identicalNames ? "Bob" : "Charlie";
        var transfers = new[]
        {
            Item with { ItemId = 101, ItemDisplayName = "Sword A", LocationDisplayName = "Castle A" },
            Item with { ItemId = 102, FinderSlot = 3, FinderDisplayName = secondName,
                ItemDisplayName = "Sword B", LocationDisplayName = "Castle B" },
            Item with { ItemId = 103, ItemDisplayName = "Sword C", LocationDisplayName = "Castle C" }
        };
        var events = transfers.Select(item => new ItemReceivedEvent("123", "456", T0, item)).ToArray();
        var rendered = Assert.Single(await TrackingDataManager.RenderTrackingNotificationsAsync(events, Snapshot(), silent: false));
        var content = rendered.Notification.Content;
        Assert.Contains("(3)", content);
        Assert.Contains("Bob sent **Sword A** (Castle A)", content);
        Assert.Contains($"{secondName} sent **Sword B** (Castle B)", content);
        Assert.Contains("Bob sent **Sword C** (Castle C)", content);
        Assert.True(content.IndexOf("Sword A", StringComparison.Ordinal) < content.IndexOf("Sword B", StringComparison.Ordinal));
        Assert.True(content.IndexOf("Sword B", StringComparison.Ordinal) < content.IndexOf("Sword C", StringComparison.Ordinal));
        Assert.DoesNotContain("arrow_down_right", content);
        Assert.Equal(identicalNames ? 3 : 2, content.Split("Bob").Length - 1);
        Assert.Equal(3, rendered.EventKeys.Distinct().Count());
        Assert.DoesNotContain("\n\n", content);
    }

    [Theory]
    [InlineData("fr-FR", "Objets pour Alice", "Bob a envoyé", "Nouveaux indices", "Indices actualisés", "Objectif atteint")]
    [InlineData("en-US", "Items for Alice", "Bob sent", "New hints", "Updated hints", "Goal completed")]
    public async Task Compact_layout_is_localized_and_keeps_mentions_and_details(
        string culture, string itemsTitle, string sender, string hintsTitle, string updatedTitle, string goalTitle)
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        await SubscribeAsync("111", "Alice");
        await SubscribeAsync("222", "Bob");
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var messages = await TrackingDataManager.RenderTrackingNotificationsAsync(
                [new ItemReceivedEvent("123", "456", T0, Item),
                 new HintCreatedEvent("123", "456", T0, Hint),
                 new HintUpdatedEvent("123", "456", T0, Hint, Hint with { Found = true }),
                 new GoalReachedEvent("123", "456", T0, new(1, "completion", true))],
                Snapshot(completed: true), silent: false);
            Assert.Equal(4, messages.Count);
            var items = messages[0].Notification;
            Assert.Equal($">>> **{itemsTitle} (1)** <@111>\n• {sender} **Magic Sword** (Castle)", items.Content);
            Assert.Equal(new[] { 111UL }, items.UserIds);
            Assert.Contains(hintsTitle, messages[1].Notification.Content);
            Assert.Contains(updatedTitle, messages[2].Notification.Content);
            Assert.Contains("(Castle)", messages[1].Notification.Content);
            Assert.Contains("<@222>", messages[1].Notification.Content);
            Assert.Equal(new[] { 111UL, 222UL }, messages[1].Notification.UserIds);
            Assert.All(messages.Take(3), value =>
            {
                Assert.StartsWith(">>> **", value.Notification.Content);
                Assert.Contains("<@111>\n• ", value.Notification.Content);
                Assert.DoesNotContain("arrow_down_right", value.Notification.Content);
                Assert.Equal(2, value.Notification.Content.Split('\n').Length);
                Assert.DoesNotContain("\n\n", value.Notification.Content);
                Assert.DoesNotContain("↳", value.Notification.Content);
            });
            var goal = messages[3].Notification;
            Assert.Contains(goalTitle, goal.Content);
            Assert.Contains("**Alice**", goal.Content);
            Assert.Contains("🎮 *GameA*", goal.Content);
            Assert.Empty(goal.UserIds);
            Assert.All(messages, value =>
            {
                Assert.Contains(">>> ", value.Notification.Content);
                Assert.DoesNotContain("@everyone", value.Notification.Content);
            });
        }
        finally { CultureInfo.CurrentUICulture = previousCulture; }
    }

    [Fact]
    public async Task Compact_layout_escapes_external_markdown_and_bounds_emoji_labels()
    {
        using var scope = new TestDatabaseScope();
        var item = Item with
        {
            ItemDisplayName = "**Sword** @everyone\n# Fake heading",
            FinderDisplayName = string.Concat(Enumerable.Repeat("🙂", 200)),
            LocationDisplayName = "[Castle](url) `test` _room_"
        };
        var message = Assert.Single(await TrackingDataManager.RenderTrackingNotificationsAsync(
            [new ItemReceivedEvent("123", "456", T0, item)], Snapshot(), silent: false)).Notification;
        Assert.Contains("\\*\\*Sword\\*\\*", message.Content);
        Assert.Contains("@\u200beveryone", message.Content);
        Assert.DoesNotContain("\n# Fake heading", message.Content);
        Assert.Contains("\\[Castle\\]", message.Content);
        Assert.Contains("\\`test\\`", message.Content);
        Assert.Contains("🙂", message.Content);
        Assert.Contains("…", message.Content);
        Assert.DoesNotContain("\uFFFD", message.Content);
        Assert.True(message.Content.Length <= 1900);
        Assert.Empty(message.UserIds);
    }

    [Fact]
    public async Task Frozen_old_layout_is_retained_and_recovered_after_an_uncertain_send()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        await QueueItemAsync();
        var snapshotId = long.Parse(await ScalarAsync("SELECT MAX(Id) FROM RoomSnapshots;"));
        var key = $"{snapshotId}:{TrackingDestination.NotificationDestinationType}:456:ItemReceived";
        const string oldText = "**Items for Alice (1):**\n>>> - Bob sent Magic Sword to Alice (Castle)";
        var frozen = new DiscordNotificationBatch(key,
            [new("old-layout-page", new TrackingDiscordNotification(oldText, []))]);
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO DiscordNotificationBatches(BatchKey,SnapshotId,ChannelId,PayloadJson)
                VALUES(@Key,@Snapshot,'456',@Payload);";
            command.Parameters.AddWithValue("@Key", key);
            command.Parameters.AddWithValue("@Snapshot", snapshotId);
            command.Parameters.AddWithValue("@Payload", System.Text.Json.JsonSerializer.Serialize(frozen,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
            await command.ExecuteNonQueryAsync();
        });
        var time = new ManualTimeProvider();
        var transport = new FakeTransport { ThrowAfterSend = true };
        await Worker(transport, time).RunOnceAsync();
        Assert.Equal(oldText, Assert.Single(transport.Sent.Values).Content);
        time.Advance();
        await Worker(transport, time).RunOnceAsync();
        Assert.Equal(1, transport.SendCalls);
        Assert.Equal(1, transport.FindCalls);
        Assert.Equal("Delivered", await ScalarAsync("SELECT Status FROM EventDeliveries;"));
    }

    [Fact]
    public async Task Compact_messages_are_recovered_after_an_uncertain_send_without_a_duplicate()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        await QueueItemAsync();
        var transport = new FakeTransport { ThrowAfterSend = true };
        var time = new ManualTimeProvider();
        await Worker(transport, time).RunOnceAsync();
        var message = Assert.Single(transport.Sent.Values);
        Assert.Equal(">>> **Items for Alice (1)**\n• Bob sent **Magic Sword** (Castle)", message.Content);
        time.Advance();
        await Worker(transport, time).RunOnceAsync();
        Assert.Equal(1, transport.SendCalls);
        Assert.Equal(1, transport.FindCalls);
        Assert.Equal("Delivered", await ScalarAsync("SELECT Status FROM EventDeliveries;"));
    }

    [Fact]
    public async Task Shared_slot_batches_keep_separate_groups_for_different_recipient_filters()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync(silent: true);
        await SubscribeAsync("111", "Alice");
        await SubscribeAsync("222", "Alice");
        await ExcludedItemsCommands.AddExcludedItemForUserAsync("123", "456", "111", "Alice", "Excluded Sword");
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(), [Destination], enableNotifications: true);
        await store.ApplySnapshotAsync(Snapshot(items:
        [
            Item with { ItemDisplayName = "Common Sword" },
            Item with { ItemId = 101, ItemDisplayName = "Other Sword" },
            Item with { ItemId = 102, ItemDisplayName = "Excluded Sword" }
        ]), [Destination], enableNotifications: true);
        var transport = new FakeTransport();
        var worker = Worker(transport);
        while (await worker.RunOnceAsync()) { }
        Assert.Equal(2, transport.Sent.Count);
        var common = transport.Sent.Values.Single(value => value.UserIds.Count == 2);
        Assert.Contains("Common Sword", common.Content);
        Assert.Contains("Other Sword", common.Content);
        Assert.Contains("(2)", common.Content);
        Assert.DoesNotContain("Excluded Sword", common.Content);
        var excluded = transport.Sent.Values.Single(value => value.UserIds.Count == 1);
        Assert.Equal(new[] { 222UL }, excluded.UserIds);
        Assert.Contains("Excluded Sword", excluded.Content);
    }

    [Fact]
    public async Task Different_receivers_never_share_a_batch_message()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(), [Destination], enableNotifications: true);
        await store.ApplySnapshotAsync(Snapshot(items:
        [
            Item,
            Item with { ItemId = 101, ReceiverSlot = 2, FinderSlot = 1,
                ReceiverDisplayName = "Bob", FinderDisplayName = "Alice", ItemDisplayName = "Bob Sword" }
        ]), [Destination], enableNotifications: true);
        var transport = new FakeTransport();
        var worker = Worker(transport);
        while (await worker.RunOnceAsync()) { }
        Assert.Equal(2, transport.Sent.Count);
        Assert.DoesNotContain(transport.Sent.Values, value => value.Content.Contains("Magic Sword") && value.Content.Contains("Bob Sword"));
    }

    [Fact]
    public async Task Long_batches_split_without_losing_items_or_resending_earlier_pages_after_a_crash()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(), [Destination], enableNotifications: true);
        var items = Enumerable.Range(1, 35).Select(index => Item with
        {
            ItemId = 100 + index, LocationId = 200 + index,
            ItemDisplayName = $"UniqueSword-{index:D2}:" + new string('s', 120),
            LocationDisplayName = new string('l', 140)
        }).ToArray();
        await store.ApplySnapshotAsync(Snapshot(items: items), [Destination], enableNotifications: true);
        var transport = new FakeTransport { ThrowOnSendCall = 2 };
        var time = new ManualTimeProvider();
        await Worker(transport, time).RunOnceAsync();
        Assert.Equal(2, transport.Sent.Count); // second page accepted, response lost
        time.Advance();
        var worker = Worker(transport, time); // restart with frozen groups and receipts
        while (await worker.RunOnceAsync()) { }
        Assert.True(transport.Sent.Count > 2);
        Assert.Equal(transport.Sent.Count, transport.SendCalls);
        Assert.All(transport.Sent.Values, value => Assert.True(value.Content.Length <= 1900));
        Assert.All(transport.Sent.Values, value =>
        {
            var rows = value.Content.Split('\n').Skip(1).ToArray();
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.Contains("• Bob sent **UniqueSword-", row));
            Assert.DoesNotContain("arrow_down_right", value.Content);
        });
        var text = string.Join("\n", transport.Sent.Values.Select(value => value.Content));
        for (var index = 1; index <= 35; index++)
            Assert.Equal(1, text.Split($"UniqueSword-{index:D2}:").Length - 1);
        Assert.Equal("35", await ScalarAsync("SELECT COUNT(*) FROM EventDeliveries WHERE Status = 'Delivered';"));
    }

    [Fact]
    public async Task Already_delivered_individual_notifications_are_not_republished_when_grouping_pending_items()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(), [Destination], enableNotifications: true);
        await store.ApplySnapshotAsync(Snapshot(items:
        [Item, Item with { ItemId = 101, ItemDisplayName = "Pending Sword" }]), [Destination], enableNotifications: true);
        await ExecuteAsync("UPDATE EventDeliveries SET Status = 'Delivered', ExternalReceiptId = 'old-receipt' WHERE Id = (SELECT MIN(Id) FROM EventDeliveries);");
        var transport = new FakeTransport();
        var worker = Worker(transport);
        while (await worker.RunOnceAsync()) { }
        var message = Assert.Single(transport.Sent.Values);
        Assert.Contains("Pending Sword", message.Content);
        Assert.DoesNotContain("Magic Sword", message.Content);
    }

    [Fact]
    public async Task Uncertain_send_is_recovered_after_restart_without_sending_twice()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        await QueueItemAsync();
        var time = new ManualTimeProvider();
        var transport = new FakeTransport { ThrowAfterSend = true };
        Assert.True(await Worker(transport, time).RunOnceAsync());
        Assert.Equal("Failed", await ScalarAsync("SELECT Status FROM EventDeliveries LIMIT 1;"));
        time.Advance();
        Assert.True(await Worker(transport, time).RunOnceAsync());
        Assert.Equal(1, transport.SendCalls);
        Assert.Equal(1, transport.FindCalls);
        Assert.Equal("Delivered", await ScalarAsync("SELECT Status FROM EventDeliveries LIMIT 1;"));
        Assert.Equal("receipt-1", await ScalarAsync("SELECT ExternalReceiptId FROM EventDeliveries LIMIT 1;"));
    }

    [Fact]
    public async Task Failed_history_lookup_keeps_delivery_retryable_instead_of_sending_a_duplicate()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        await QueueItemAsync();
        var time = new ManualTimeProvider();
        var transport = new FakeTransport { ThrowAfterSend = true };
        await Worker(transport, time).RunOnceAsync();
        transport.ThrowOnFind = true;
        time.Advance();
        await Worker(transport, time).RunOnceAsync();
        Assert.Equal(1, transport.SendCalls);
        Assert.Equal("Failed", await ScalarAsync("SELECT Status FROM EventDeliveries LIMIT 1;"));
        transport.ThrowOnFind = false;
        time.Advance();
        await Worker(transport, time).RunOnceAsync();
        Assert.Equal("Delivered", await ScalarAsync("SELECT Status FROM EventDeliveries LIMIT 1;"));
    }

    [Fact]
    public async Task Transport_failure_before_send_is_retried_and_not_acknowledged_early()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        await QueueItemAsync();
        var time = new ManualTimeProvider();
        var transport = new FakeTransport { ThrowBeforeSend = true };
        await Worker(transport, time).RunOnceAsync();
        Assert.Empty(transport.Sent);
        Assert.Equal("Failed", await ScalarAsync("SELECT Status FROM EventDeliveries LIMIT 1;"));
        Assert.False(await Worker(transport, time).RunOnceAsync());
        transport.ThrowBeforeSend = false;
        time.Advance();
        await Worker(transport, time).RunOnceAsync();
        Assert.Single(transport.Sent);
        Assert.Equal("Delivered", await ScalarAsync("SELECT Status FROM EventDeliveries LIMIT 1;"));
    }

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 1)]
    public async Task Item_delivery_respects_silent_mode_and_subscriptions(bool silent, bool subscribed, int expected)
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync(silent);
        if (subscribed) await SubscribeAsync("111", "Alice");
        await QueueItemAsync();
        var transport = new FakeTransport();
        await Worker(transport).RunOnceAsync();
        Assert.Equal(expected, transport.Sent.Count);
        if (subscribed) Assert.Contains(111UL, Assert.Single(transport.Sent.Values).UserIds);
        Assert.Equal("Delivered", await ScalarAsync("SELECT Status FROM EventDeliveries LIMIT 1;"));
    }

    [Fact]
    public async Task Shared_slot_mentions_only_users_without_personal_exclusion_or_category_filter()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync(silent: true);
        await SubscribeAsync("111", "Alice");
        await SubscribeAsync("222", "Alice");
        await SubscribeAsync("333", "Alice", "2"); // excludes flag 1
        await ExcludedItemsCommands.AddExcludedItemForUserAsync("123", "456", "111", "Alice", "Magic Sword");
        await QueueItemAsync();
        var transport = new FakeTransport();
        await Worker(transport).RunOnceAsync();
        Assert.Equal(new[] { 222UL }, Assert.Single(transport.Sent.Values).UserIds);
    }

    [Theory]
    [InlineData(false, false, 2)]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 1)]
    public async Task Hint_and_goal_preferences_are_preserved(bool silent, bool subscribed, int expected)
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync(silent);
        if (subscribed) await SubscribeAsync("111", "Alice");
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(), [Destination], enableNotifications: true);
        await store.ApplySnapshotAsync(Snapshot(hints: [Hint], completed: true), [Destination], enableNotifications: true);
        var transport = new FakeTransport();
        var worker = Worker(transport);
        while (await worker.RunOnceAsync()) { }
        Assert.Equal(expected, transport.Sent.Count);
    }

    [Fact]
    public async Task Hint_update_mentions_both_players_and_uses_current_hint()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        await SubscribeAsync("111", "Alice");
        await SubscribeAsync("222", "Bob");
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(hints: [Hint]), [Destination], enableNotifications: true);
        await store.ApplySnapshotAsync(Snapshot(hints: [Hint with { Found = true }]), [Destination], enableNotifications: true);
        var transport = new FakeTransport();
        await Worker(transport).RunOnceAsync();
        var message = Assert.Single(transport.Sent.Values);
        Assert.Contains("Magic Sword", message.Content);
        Assert.Equal(new[] { 111UL, 222UL }, message.UserIds);
    }

    [Fact]
    public async Task Goal_keeps_subscriptions_when_a_runtime_alias_changes()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync(silent: true);
        await SubscribeAsync("111", "Alice");
        var snapshot = Snapshot(completed: true);
        snapshot = NormalizedRoomSnapshot.Create("123", "456",
            [new(1, "Alice", "Renamed Alice", "GameA")], [], [], [], snapshot.Goals,
            snapshot.PlayerStates, null, T0, T0, TrackingObservationState.Healthy, null, SnapshotSections.All);
        var message = await TrackingDataManager.RenderTrackingNotificationAsync(
            new GoalReachedEvent("123", "456", T0, new(1, "completion", true)), snapshot, silent: true);
        Assert.NotNull(message);
        Assert.Contains("Renamed Alice", message.Content);
    }

    [Fact]
    public async Task Item_identity_uses_raw_slot_and_item_ids_instead_of_display_names()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        var item = Item with { FinderDisplayName = "Alice", ReceiverDisplayName = "Alice", ItemDisplayName = "-1" };
        var message = await TrackingDataManager.RenderTrackingNotificationAsync(
            new ItemReceivedEvent("123", "456", T0, item), Snapshot(), silent: false);
        Assert.NotNull(message); // distinct slots, a legitimate name that looks like a negative ID
        Assert.Null(await TrackingDataManager.RenderTrackingNotificationAsync(
            new ItemReceivedEvent("123", "456", T0, item with { LocationId = -1 }), Snapshot(), silent: false));
    }

    [Fact]
    public async Task A_failed_delivery_blocks_later_messages_in_its_destination_until_recovered()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        await QueueItemAsync();
        var time = new ManualTimeProvider();
        var transport = new FakeTransport { ThrowBeforeSend = true };
        await Worker(transport, time).RunOnceAsync();
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot(items: [Item, Item with { ItemId = 101 }]),
            [Destination], enableNotifications: true);
        Assert.False(await Worker(transport, time).RunOnceAsync());
        transport.ThrowBeforeSend = false;
        time.Advance();
        var worker = Worker(transport, time);
        while (await worker.RunOnceAsync()) { }
        Assert.Equal(2, transport.Sent.Count);
        Assert.Contains("Magic Sword", transport.Sent.Values.First().Content);
    }

    [Fact]
    public async Task End_to_end_poll_updates_command_tables_but_only_outbox_publishes()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        var context = new ProcessingContext { GuildId = "123", ChannelId = "456" };
        context.SlotIndex.Add(("Alice", "GameA"));
        context.SlotIndex.Add(("Bob", "GameB"));
        var totals = new Dictionary<int, int> { [1] = 10, [2] = 10 };
        const string baseline = """{"player_items_received":[],"hints":[],"player_status":[{"player":1,"status":20},{"player":2,"status":20}],"player_checks_done":[{"player":1,"locations":[]},{"player":2,"locations":[]}]}""";
        const string changed = """{"player_items_received":[{"player":1,"items":[[100,200,2,1]]}],"hints":[],"player_status":[{"player":1,"status":30},{"player":2,"status":20}],"player_checks_done":[{"player":1,"locations":[200]},{"player":2,"locations":[]}]}""";
        await TrackingDataManager.ProcessTrackerResponseAsync(context, baseline, totals, false, false);
        var result = await TrackingDataManager.ProcessTrackerResponseAsync(context, changed, totals, false, false);
        Assert.Equal(1, result.NewItemCount);
        Assert.Equal(1, result.CompletedGoalCount); // CLIENT_GOAL even without 100% checks
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("TrackingItems"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("TrackingPlayerStates"));
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        var replay = await TrackingDataManager.ProcessTrackerResponseAsync(context, changed, totals, false, false);
        Assert.Equal(0, replay.NewItemCount);
        var transport = new FakeTransport();
        var worker = Worker(transport);
        while (await worker.RunOnceAsync()) { }
        Assert.Equal(2, transport.Sent.Count);
    }

    [Fact]
    public async Task Removing_room_cancels_pending_deliveries_and_a_new_room_gets_a_fresh_baseline()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        await QueueItemAsync();
        await DatabaseCommands.DeleteChannelDataAsync("123", "456");
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("DiscordNotificationBatches"));
        Assert.False(await Worker(new FakeTransport()).RunOnceAsync());
        await SeedRoomAsync();
        var result = await new TrackingV2Store().ApplySnapshotAsync(Snapshot(items: [Item]), [Destination], enableNotifications: true);
        Assert.True(result.BaselineCreated);
        Assert.Equal(0, result.DeliveriesCreated);
    }

    [Fact]
    public async Task Completion_does_not_delete_notifications_before_the_worker_can_deliver_them()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        var context = new ProcessingContext { GuildId = "123", ChannelId = "456" };
        context.SlotIndex.Add(("Alice", "GameA"));
        context.SlotIndex.Add(("Bob", "GameB"));
        var totals = new Dictionary<int, int> { [1] = 1, [2] = 1 };
        const string baseline = """{"player_items_received":[],"hints":[],"player_checks_done":[],"player_status":[{"player":1,"status":20},{"player":2,"status":20}]}""";
        const string completed = """{"player_items_received":[{"player":1,"items":[[100,200,2,1]]}],"hints":[],"player_checks_done":[{"player":1,"locations":[200]},{"player":2,"locations":[201]}],"player_status":[{"player":1,"status":30},{"player":2,"status":30}]}""";
        await TrackingDataManager.ProcessTrackerResponseAsync(context, baseline, totals, false, false);
        var result = await TrackingDataManager.ProcessTrackerResponseAsync(context, completed, totals, false, false);
        Assert.False(result.RemoveRoom);
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("ChannelsAndUrlsTable"));
        Assert.Equal(3, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        var transport = new FakeTransport();
        var worker = Worker(transport);
        while (await worker.RunOnceAsync()) { }
        Assert.Equal(3, transport.Sent.Count);
    }

    [Fact]
    public async Task Migration_from_5013_is_repeatable_and_preserves_shadow_data()
    {
        using var scope = new TestDatabaseScope();
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot());
        await ExecuteAsync("ALTER TABLE TrackedRooms DROP COLUMN NotificationsV2Initialized;");
        await DBMigration_5.Migrate_5_0_14();
        await DBMigration_5.Migrate_5_0_14();
        Assert.Equal("0", await ScalarAsync("SELECT NotificationsV2Initialized FROM TrackedRooms;"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("RoomSnapshots"));
    }

    [Fact]
    public async Task Migration_from_5014_preserves_pending_notifications_and_can_run_twice()
    {
        using var scope = new TestDatabaseScope();
        await SeedRoomAsync();
        await QueueItemAsync();
        await ExecuteAsync("DROP TABLE DiscordNotificationBatches;");
        await DBMigration_5.Migrate_5_0_15();
        await DBMigration_5.Migrate_5_0_15();
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("DiscordNotificationBatches"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        var transport = new FakeTransport();
        await Worker(transport).RunOnceAsync();
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task Cutover_is_atomic_when_snapshot_transaction_fails()
    {
        using var scope = new TestDatabaseScope();
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot());
        var store = new TrackingV2Store((point, _) => point == TrackingV2FaultPoint.SnapshotInserted
            ? throw new InvalidOperationException("simulated crash") : Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ApplySnapshotAsync(
            Snapshot(items: [Item]), [Destination], enableNotifications: true));
        Assert.Equal("0", await ScalarAsync("SELECT NotificationsV2Initialized FROM TrackedRooms;"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("RoomSnapshots"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    [Fact]
    public async Task Partial_observation_cannot_create_a_false_goal_or_lose_future_changes()
    {
        using var scope = new TestDatabaseScope();
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(), [Destination], enableNotifications: true);
        var partial = NormalizedRoomSnapshot.Create("123", "456", [], [], [], [], [], [], null,
            T0.AddMinutes(1), T0, TrackingObservationState.Healthy, null, SnapshotSections.Tracking);
        Assert.Equal(0, (await store.ApplySnapshotAsync(partial, [Destination], enableNotifications: true)).DeliveriesCreated);
        Assert.Equal(2, (await store.ApplySnapshotAsync(Snapshot(items: [Item], completed: true),
            [Destination], enableNotifications: true)).DeliveriesCreated);
    }

    private static NormalizedRoomSnapshot Snapshot(IEnumerable<NormalizedItemTransfer>? items = null,
        IEnumerable<NormalizedHint>? hints = null, bool completed = false)
        => NormalizedRoomSnapshot.Create("123", "456",
            [new(1, "Alice", "Alice", "GameA"), new(2, "Bob", "Bob", "GameB")],
            items ?? [], hints ?? [], [], [new(1, "completion", completed)],
            [new(1, completed ? NormalizedPlayerStatus.GoalReached : NormalizedPlayerStatus.Playing, 0, 10, null)],
            null, T0, T0, TrackingObservationState.Healthy, null, SnapshotSections.All);

    private static async Task QueueItemAsync()
    {
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(), [Destination], enableNotifications: true);
        await store.ApplySnapshotAsync(Snapshot(items: [Item]), [Destination], enableNotifications: true);
    }

    private static async Task SeedRoomAsync(bool silent = false)
    {
        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync("123", "456", "https://example.test", "room", "tracker", silent, "5m", "0");
        await AliasChoicesCommands.AddOrReplaceAliasChoiceAsync("123", "456", [(1, "Alice", "GameA"), (2, "Bob", "GameB")]);
    }

    private static Task SubscribeAsync(string id, string alias, string flag = "0")
        => ExecuteAsync($"INSERT INTO ReceiverAliasesTable (GuildId, ChannelId, Receiver, UserId, Flag) VALUES ('123', '456', '{alias}', '{id}', '{flag}');");

    private static TrackingDeliveryWorker Worker(FakeTransport transport, TimeProvider? time = null)
        => new(new DiscordTrackingEventPublisher(transport), time ?? new ManualTimeProvider(),
            destinationType: TrackingDestination.NotificationDestinationType);

    private static Task ExecuteAsync(string sql) => Db.WriteAsync(async connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    });

    private static async Task<string> ScalarAsync(string sql)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync())?.ToString() ?? "";
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow.AddMinutes(1);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance() => _now = _now.AddMinutes(1);
    }

    private sealed class FakeTransport : ITrackingDiscordTransport
    {

        public readonly Dictionary<string, TrackingDiscordNotification> Sent = [];
        private readonly Dictionary<string, string> _receipts = [];
        public bool ThrowBeforeSend;
        public bool ThrowAfterSend;
        public bool ThrowOnFind;
        public int SendCalls;
        public int FindCalls;
        public int ThrowOnSendCall;

        public Task<string?> FindPublicationAsync(string guild, string channel, string key, DateTimeOffset occurred, TrackingDiscordNotification notification, CancellationToken ct)
        {
            FindCalls++;
            if (ThrowOnFind) throw new InvalidOperationException("history unavailable");
            return Task.FromResult(_receipts.GetValueOrDefault(key));
        }

        public Task<string> SendAsync(string guild, string channel, string key, TrackingDiscordNotification notification, CancellationToken ct)
        {
            SendCalls++;
            if (ThrowBeforeSend) throw new InvalidOperationException("Discord unavailable");
            Sent.Add(key, notification);
            var receipt = $"receipt-{SendCalls}";
            _receipts.Add(key, receipt);
            if (ThrowAfterSend || SendCalls == ThrowOnSendCall) throw new InvalidOperationException("response interrupted");
            return Task.FromResult(receipt);
        }
    }
}
