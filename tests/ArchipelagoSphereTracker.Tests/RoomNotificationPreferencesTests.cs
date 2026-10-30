using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.Tracking.Notifications;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;
using Discord;
using Xunit;

public sealed class RoomNotificationPreferencesTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddMinutes(-2);
    private static readonly NormalizedItemTransfer Item = new(2, 1, 100, 200, 1,
        "Bob", "Alice", "Sword", "Castle", "GameB", "GameA");
    private static readonly NormalizedHint Hint = new(2, 1, 100, 200, false, "Vanilla",
        "Bob", "Alice", "Sword", "Castle", "GameB", "GameA", 1);

    [Theory]
    [InlineData(RoomItemNotificationFilter.All, 0, true)]
    [InlineData(RoomItemNotificationFilter.All, 4, true)]
    [InlineData(RoomItemNotificationFilter.UsefulAndProgression, 0, false)]
    [InlineData(RoomItemNotificationFilter.UsefulAndProgression, 1, true)]
    [InlineData(RoomItemNotificationFilter.UsefulAndProgression, 2, true)]
    [InlineData(RoomItemNotificationFilter.UsefulAndProgression, 3, true)]
    [InlineData(RoomItemNotificationFilter.UsefulAndProgression, 4, false)]
    [InlineData(RoomItemNotificationFilter.ProgressionOnly, 0, false)]
    [InlineData(RoomItemNotificationFilter.ProgressionOnly, 1, true)]
    [InlineData(RoomItemNotificationFilter.ProgressionOnly, 2, false)]
    [InlineData(RoomItemNotificationFilter.ProgressionOnly, 3, true)]
    [InlineData(RoomItemNotificationFilter.ProgressionOnly, 4, false)]
    public void Item_categories_use_Archipelago_bit_flags(RoomItemNotificationFilter filter, int flags, bool expected)
        => Assert.Equal(expected, new RoomNotificationPreferences(false, filter, true).AllowsItem(flags));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_preserves_legacy_silent_behavior_and_can_run_twice(bool silent)
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync(silent);
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE RoomNotificationPreferences;";
            await command.ExecuteNonQueryAsync();
        });
        await DBMigration_5.Migrate_5_0_20();
        await DBMigration_5.Migrate_5_0_20();
        Assert.Equal(new(silent, RoomItemNotificationFilter.All, !silent),
            await RoomNotificationPreferencesStore.ReadAsync("123", "456"));
    }

    [Fact]
    public async Task Updating_one_option_preserves_the_others_and_is_scoped_to_guild_and_room()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync(true);
        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync("999", "456", "https://example.test", "other", "tracker", false, "5m", "0");
        Assert.True(await RoomNotificationPreferencesStore.SetOptionAsync("123", "456", "hints", "on"));
        Assert.True(await RoomNotificationPreferencesStore.SetOptionAsync("123", "456", "items", "ProgressionOnly"));
        Assert.True(await RoomNotificationPreferencesStore.SetOptionAsync("123", "456", "audience", "all"));
        Assert.Equal(new(false, RoomItemNotificationFilter.ProgressionOnly, true),
            await RoomNotificationPreferencesStore.ReadAsync("123", "456"));
        Assert.Equal(RoomNotificationPreferences.FromLegacy(false), await RoomNotificationPreferencesStore.ReadAsync("999", "456"));
        Assert.True(ChannelConfigCache.TryGet("123", "456", out var cached));
        Assert.False(cached.Silent);
        Assert.False(await RoomNotificationPreferencesStore.SetOptionAsync("123", "456", "items", "1"));
        Assert.False(await RoomNotificationPreferencesStore.SetOptionAsync("123", "missing", "hints", "off"));
        Assert.False(await RoomNotificationPreferencesStore.SetOptionAsync("123", "456", "unexpected", "off"));
        Assert.Equal(new(false, RoomItemNotificationFilter.ProgressionOnly, true),
            await RoomNotificationPreferencesStore.ReadAsync("123", "456"));
    }

    [Fact]
    public async Task Concurrent_option_updates_do_not_overwrite_each_other()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        await Task.WhenAll(
            RoomNotificationPreferencesStore.SetOptionAsync("123", "456", "items", "ProgressionOnly"),
            RoomNotificationPreferencesStore.SetOptionAsync("123", "456", "hints", "off"),
            RoomNotificationPreferencesStore.SetOptionAsync("123", "456", "audience", "linked"));
        Assert.Equal(new(true, RoomItemNotificationFilter.ProgressionOnly, false),
            await RoomNotificationPreferencesStore.ReadAsync("123", "456"));
    }

    [Theory]
    [InlineData("all", "All", "on", true)]
    [InlineData("linked", "ProgressionOnly", "off", true)]
    [InlineData("invalid", "All", "on", false)]
    [InlineData("all", "1", "on", false)]
    [InlineData("all", "All", "true", false)]
    [InlineData(null, null, null, false)]
    public void Form_values_are_validated_without_accepting_numeric_enum_values(string? audience, string? items, string? hints, bool valid)
        => Assert.Equal(valid, RoomNotificationPreferences.TryParse(audience, items, hints, out _));

    [Fact]
    public async Task Room_preferences_are_removed_with_the_room()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        await RoomNotificationPreferencesStore.SetAsync("123", "456", new(true, RoomItemNotificationFilter.ProgressionOnly, false));
        await DatabaseCommands.DeleteChannelDataAsync("123", "456");
        Assert.Null(await RoomNotificationPreferencesStore.ReadAsync("123", "456"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("RoomNotificationPreferences"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Hints_can_be_enabled_with_linked_aliases_only_and_disabled_for_all_players(bool linkedOnly)
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        if (linkedOnly) await SubscribeAsync("0");
        var events = new NormalizedTrackingEvent[]
        {
            new ItemReceivedEvent("123", "456", Now, Item),
            new HintCreatedEvent("123", "456", Now, Hint),
            new HintUpdatedEvent("123", "456", Now, Hint, Hint with { Found = true })
        };
        var enabled = await TrackingDataManager.RenderTrackingNotificationsAsync(events, Snapshot(), linkedOnly,
            new(linkedOnly, RoomItemNotificationFilter.All, true));
        Assert.Equal(3, enabled.Count);
        var disabled = await TrackingDataManager.RenderTrackingNotificationsAsync(events, Snapshot(), linkedOnly,
            new(linkedOnly, RoomItemNotificationFilter.All, false));
        Assert.Single(disabled);
        Assert.Equal(events[0].EventKey, Assert.Single(disabled[0].EventKeys));
    }

    [Fact]
    public async Task Linked_only_hints_require_a_linked_participant()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        Assert.Empty(await TrackingDataManager.RenderTrackingNotificationsAsync(
            [new HintCreatedEvent("123", "456", Now, Hint)], Snapshot(), true,
            new(true, RoomItemNotificationFilter.All, true)));
    }

    [Fact]
    public async Task Personal_item_preferences_can_narrow_but_never_expand_the_global_filter()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        await SubscribeAsync("4"); // Exclude useful items from this subscription.
        var events = Enumerable.Range(0, 5).Select(flags => new ItemReceivedEvent("123", "456", Now,
            Item with { ItemId = 100 + flags, LocationId = 200 + flags, Flags = flags, ItemDisplayName = $"Item-{flags}" })).ToArray();
        var messages = await TrackingDataManager.RenderTrackingNotificationsAsync(events, Snapshot(), false,
            new(false, RoomItemNotificationFilter.UsefulAndProgression, true));
        var message = Assert.Single(messages).Notification;
        Assert.Contains("**Item-1**", message.Content);
        Assert.Contains("**Item-3**", message.Content);
        foreach (var excluded in new[] { 0, 2, 4 }) Assert.DoesNotContain($"**Item-{excluded}**", message.Content);
        Assert.Equal(new[] { 111UL }, message.UserIds);
    }

    [Fact]
    public async Task Global_filter_applies_to_unsubscribed_players_and_hint_item_categories()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        var events = new NormalizedTrackingEvent[]
        {
            new ItemReceivedEvent("123", "456", Now, Item with { Flags = 0 }),
            new HintCreatedEvent("123", "456", Now, Hint with { ItemFlags = 2 }),
            new HintUpdatedEvent("123", "456", Now, Hint, Hint with { ItemFlags = 4, Found = true })
        };
        Assert.Empty(await TrackingDataManager.RenderTrackingNotificationsAsync(events, Snapshot(), false,
            new(false, RoomItemNotificationFilter.ProgressionOnly, true)));
    }

    [Fact]
    public async Task Filtered_notifications_leave_full_inventory_hints_and_ledger_in_the_database()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        var destination = TrackingDestination.DiscordNotification("456");
        var store = new TrackingV2Store();
        await store.ApplySnapshotAsync(Snapshot(), [destination], enableNotifications: true);
        await RoomNotificationPreferencesStore.SetAsync("123", "456", new(false, RoomItemNotificationFilter.ProgressionOnly, false));
        var items = Enumerable.Range(0, 5).Select(flags => Item with
        {
            ItemId = 100 + flags, LocationId = 200 + flags, Flags = flags, ItemDisplayName = $"Item-{flags}"
        }).ToArray();
        await store.ApplySnapshotAsync(Snapshot(items, [Hint]), [destination], enableNotifications: true);
        var transport = new RecordingTransport();
        var worker = new TrackingDeliveryWorker(new DiscordTrackingEventPublisher(transport),
            destinationType: TrackingDestination.NotificationDestinationType);
        while (await worker.RunOnceAsync()) { }
        var message = Assert.Single(transport.Sent);
        Assert.Contains("**Item-1**", message.Content);
        Assert.Contains("**Item-3**", message.Content);
        Assert.DoesNotContain("**Item-0**", message.Content);
        Assert.Equal(5, await TestDatabaseScope.CountRowsAsync("TrackingItems"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("TrackingHints"));
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT PayloadJson FROM RoomSnapshots WHERE GuildId='123' AND ChannelId='456' ORDER BY Id DESC LIMIT 1;";
        var snapshot = TrackingSnapshotJson.Deserialize((string)(await command.ExecuteScalarAsync())!);
        Assert.Equal(5, snapshot.Items.Count());
        Assert.Single(snapshot.Hints);
    }

    [Theory]
    [InlineData("fr", "Alias associés uniquement", "Progression uniquement")]
    [InlineData("en-US", "Linked aliases only", "Progression only")]
    public async Task Discord_and_web_settings_show_current_values_and_localized_labels(string language, string audience, string filter)
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        await RoomNotificationPreferencesStore.SetAsync("123", "456", new(true, RoomItemNotificationFilter.ProgressionOnly, true));
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            var session = new AstUiSession(Guid.NewGuid().ToString("N"), 111, 123, 456, 456,
                AstUiScreen.ManageMore, DateTimeOffset.UtcNow.AddMinutes(5));
            var view = await AstCommandCenter.RenderManageMoreAsync(session);
            var menus = view.Components.Components.OfType<ActionRowComponent>()
                .SelectMany(row => row.Components).OfType<SelectMenuComponent>().ToArray();
            Assert.Equal(3, menus.Length);
            Assert.Contains(menus[0].Options, option => option.Label == audience && option.Value == "linked");
            Assert.Contains(menus[1].Options, option => option.Label == filter && option.Value == "ProgressionOnly");
            Assert.Equal("linked", Assert.Single(menus[0].Options, option => option.IsDefault == true).Value);
            Assert.Equal("ProgressionOnly", Assert.Single(menus[1].Options, option => option.IsDefault == true).Value);
            Assert.Equal("on", Assert.Single(menus[2].Options, option => option.IsDefault == true).Value);
            var html = WebPortalThreadCommandsPage.Build();
            Assert.Contains(audience, html);
            Assert.Contains(filter, html);
            Assert.Contains("data-command=\"update-notifications\"", html);
            Assert.Contains("securedApiBase + '/notification-preferences'", html);
            Assert.Contains("fieldset disabled", html);
            Assert.DoesNotContain("data-command=\"update-silent-option\"", html);
            Assert.DoesNotContain("NotificationSettingsDescription", html);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public void Notification_updates_require_room_management_and_are_audited()
    {
        Assert.Equal(AstAuthorizationLevel.RoomManager,
            AstAuthorizationService.RequiredForDiscordCommand("update-notifications", false, true));
        Assert.Equal(SecurityAuditAction.RoomSettingsUpdate, SecurityAuditLog.ForCommand("update-notifications"));
    }

    private static Task SeedAsync(bool silent = false)
        => ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync("123", "456", "https://example.test", "room", "tracker", silent, "5m", "0");

    private static Task SubscribeAsync(string mask) => Db.WriteAsync(async connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO ReceiverAliasesTable(GuildId,ChannelId,Receiver,UserId,Flag) VALUES('123','456','Alice','111',@Mask);";
        command.Parameters.AddWithValue("@Mask", mask);
        await command.ExecuteNonQueryAsync();
    });

    private static NormalizedRoomSnapshot Snapshot(IEnumerable<NormalizedItemTransfer>? items = null, IEnumerable<NormalizedHint>? hints = null)
        => NormalizedRoomSnapshot.Create("123", "456", [new(1, "Alice", "Alice", "GameA"), new(2, "Bob", "Bob", "GameB")],
            items ?? [], hints ?? [], [], [], [], null, Now, Now, TrackingObservationState.Healthy, null, SnapshotSections.All);

    private sealed class RecordingTransport : ITrackingDiscordTransport
    {
        internal List<TrackingDiscordNotification> Sent { get; } = [];
        public Task<string?> FindPublicationAsync(string guild, string channel, string key, DateTimeOffset since,
            TrackingDiscordNotification notification, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string> SendAsync(string guild, string channel, string key, TrackingDiscordNotification notification, CancellationToken ct)
        { Sent.Add(notification); return Task.FromResult(Sent.Count.ToString()); }
    }
}
