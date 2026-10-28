using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.Tracking.Notifications;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;
using Xunit;

public sealed class TrackingReplayTests
{
    private static readonly NormalizedItemTransfer First = new(2, 1, 100, 200, 1,
        "Player2", "Player1", "Old Sword", "Castle", "Game", "Game");

    [Fact]
    public async Task Imported_v1_baseline_items_can_be_replayed_without_existing_notifications()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        await RecapListCommands.AddOrEditRecapListAsync("g", "c", "111", "Player1");
        await RecapListCommands.AddOrEditRecapListItemsAsync("g", "c", "111", "Player1",
            await DisplayItemCommands.GetAliasItems("g", "c", "Player1"));
        var recapCount = await TestDatabaseScope.CountRowsAsync("RecapListItemsTable");
        var hash = await ScalarAsync("SELECT CurrentSnapshotHash FROM TrackedRooms;");
        var request = await RequestAsync("Player1");
        Assert.True(await TrackingReplayService.PrepareNextAsync());
        var transport = new FakeTransport();
        Assert.True(await Worker(transport).RunOnceAsync());
        var message = Assert.Single(transport.Messages.Values);
        Assert.Contains("Old Sword", message.Content);
        Assert.Contains("Old Shield", message.Content);
        Assert.Contains("(2)", message.Content);
        Assert.DoesNotContain("Other Player Item", message.Content);
        Assert.Equal("Queued", await ScalarAsync($"SELECT Status FROM TrackingReplayRequests WHERE Id={request};"));
        Assert.Equal("Delivered", await ScalarAsync("SELECT Status FROM EventDeliveries;"));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM TrackingEvents WHERE EventType='ItemReceived';"));
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM TrackingEvents WHERE EventType='ItemsReplayRequested';"));
        Assert.Equal(hash, await ScalarAsync("SELECT CurrentSnapshotHash FROM TrackedRooms;"));
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("RoomSnapshots"));
        Assert.Equal(3, await TestDatabaseScope.CountRowsAsync("TrackingItems"));
        Assert.Equal(recapCount, await TestDatabaseScope.CountRowsAsync("RecapListItemsTable"));
    }

    [Fact]
    public async Task Replay_request_waits_for_first_complete_native_snapshot()
    {
        using var scope = new TestDatabaseScope();
        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync("g", "c", "https://example.test", "room", "tracker", false, "5m", "0");
        await AliasChoicesCommands.AddOrReplaceAliasChoiceAsync("g", "c", [(1,"Player1","Game"),(2,"Player2","Game")]);
        await RequestAsync("Player1");
        Assert.True(await TrackingReplayService.PrepareNextAsync());
        Assert.Equal("Pending", await ScalarAsync("SELECT Status FROM TrackingReplayRequests;"));
        Assert.Equal("WAITING_FOR_ITEM_SNAPSHOT", await ScalarAsync("SELECT ReasonCode FROM TrackingReplayRequests;"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot(), enableNotifications: true);
        await ExecuteAsync("UPDATE TrackingReplayRequests SET NextAttemptAtUtc='2000-01-01T00:00:00.0000000+00:00';");
        await TrackingReplayService.PrepareNextAsync();
        var transport = new FakeTransport();
        await Worker(transport).RunOnceAsync();
        Assert.Single(transport.Messages);
    }

    [Fact]
    public async Task V1_import_without_item_events_supports_a_request_before_first_native_poll()
    {
        using var scope = new TestDatabaseScope(includeLegacyTrackingTables: true);
        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync("g", "c", "https://example.test", "room", "tracker", false, "5m", "0");
        await ExecuteAsync(@"INSERT INTO AliasChoicesTable(GuildId,ChannelId,Slot,Alias,Game)
            VALUES('g','c',1,'Player1','Game'),('g','c',2,'Player2','Game');
            INSERT INTO DisplayedItemTable(GuildId,ChannelId,Finder,Receiver,Item,Location,Game,Flag)
            VALUES('g','c','Player2','Player1','Old Sword','Castle','Game','1');");
        await DBMigration_5.Migrate_5_0_16();
        Assert.Equal("Old Sword", Assert.Single(await DisplayItemCommands.GetAliasItems("g", "c", "Player1")).Item);
        await RequestAsync("Player1");
        await TrackingReplayService.PrepareNextAsync();
        Assert.Equal("Pending", await ScalarAsync("SELECT Status FROM TrackingReplayRequests;"));
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot(), enableNotifications: true);
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        await ExecuteAsync("UPDATE TrackingReplayRequests SET NextAttemptAtUtc='2000-01-01T00:00:00.0000000+00:00';");
        await TrackingReplayService.PrepareNextAsync();
        var transport = new FakeTransport();
        await Worker(transport).RunOnceAsync();
        Assert.Contains("Old Sword", Assert.Single(transport.Messages.Values).Content);
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("DisplayedItemTable"));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM TrackingEvents WHERE EventType='ItemReceived';"));
    }

    [Fact]
    public async Task Separate_requests_for_the_same_snapshot_intentionally_send_twice()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        await RequestAsync("Player1");
        await RequestAsync("Player1");
        await TrackingReplayService.PrepareNextAsync();
        await TrackingReplayService.PrepareNextAsync();
        var transport = new FakeTransport();
        var worker = Worker(transport);
        while (await worker.RunOnceAsync()) { }
        Assert.Equal(2, transport.Messages.Count);
        Assert.Equal(2, await TestDatabaseScope.CountRowsAsync("DiscordNotificationBatches"));
        Assert.Single(transport.Messages.Values.Select(message => message.Content).Distinct());
    }

    [Fact]
    public async Task Preparing_a_request_twice_never_queues_a_second_delivery()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        await RequestAsync("Player1");
        Assert.True(await TrackingReplayService.PrepareNextAsync());
        Assert.False(await TrackingReplayService.PrepareNextAsync());
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    [Fact]
    public async Task Replay_keeps_silent_mode_and_personal_filters()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync(silent: true);
        await ExecuteAsync("INSERT INTO ReceiverAliasesTable(GuildId,ChannelId,UserId,Receiver,Flag) VALUES('g','c','111','Player1','0');");
        await ExcludedItemsCommands.AddExcludedItemForUserAsync("g", "c", "111", "Player1", "Old Sword");
        await RequestAsync("Player1");
        await TrackingReplayService.PrepareNextAsync();
        var transport = new FakeTransport();
        await Worker(transport).RunOnceAsync();
        var message = Assert.Single(transport.Messages.Values);
        Assert.DoesNotContain("Old Sword", message.Content);
        Assert.Contains("Old Shield", message.Content);
        Assert.Equal(new[] { 111UL }, message.UserIds);
    }

    [Fact]
    public async Task Empty_or_invalid_slots_do_not_create_notifications()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        await ExecuteAsync("INSERT INTO TrackingReplayRequests(GuildId,ChannelId,ReceiverSlot) VALUES('g','c',99);");
        await TrackingReplayService.PrepareNextAsync();
        Assert.Equal("Rejected", await ScalarAsync("SELECT Status FROM TrackingReplayRequests;"));
        var empty = NormalizedRoomSnapshot.Create("g", "c", Snapshot().Slots, [], [], [], [], [], null,
            DateTimeOffset.UtcNow, null, TrackingObservationState.Healthy, null, SnapshotSections.All);
        await new TrackingV2Store().ApplySnapshotAsync(empty, enableNotifications: true);
        await RequestAsync("Player1");
        await TrackingReplayService.PrepareNextAsync();
        Assert.Equal(1L, await ScalarAsync("SELECT COUNT(*) FROM TrackingReplayRequests WHERE Status='Empty';"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    [Fact]
    public async Task Failed_preparation_rolls_back_request_event_and_delivery_together()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        await RequestAsync("Player1");
        await ExecuteAsync("CREATE TRIGGER fail_replay BEFORE INSERT ON EventDeliveries BEGIN SELECT RAISE(ABORT,'simulated'); END;");
        await Assert.ThrowsAnyAsync<Exception>(() => TrackingReplayService.PrepareNextAsync());
        Assert.Equal("Pending", await ScalarAsync("SELECT Status FROM TrackingReplayRequests;"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingEvents"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
        await ExecuteAsync("DROP TRIGGER fail_replay;");
        await TrackingReplayService.PrepareNextAsync();
        Assert.Equal(1, await TestDatabaseScope.CountRowsAsync("EventDeliveries"));
    }

    [Fact]
    public async Task Retry_after_uncertain_replay_recovers_the_group_without_resending()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        await RequestAsync("Player1");
        await TrackingReplayService.PrepareNextAsync();
        var transport = new FakeTransport { LoseResponse = true };
        var time = new Clock();
        await Worker(transport, time).RunOnceAsync();
        Assert.Equal("Failed", await ScalarAsync("SELECT Status FROM EventDeliveries;"));
        time.Advance();
        await Worker(transport, time).RunOnceAsync();
        Assert.Equal("Delivered", await ScalarAsync("SELECT Status FROM EventDeliveries;"));
        Assert.Single(transport.Messages);
        Assert.Equal(1, transport.SendCount);
    }

    [Fact]
    public async Task A_different_rooms_inventory_is_never_included()
    {
        using var scope = new TestDatabaseScope();
        await SeedAsync();
        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync("g", "other", "https://example.test", "other", "other", false, "5m", "0");
        var original = Snapshot();
        var other = NormalizedRoomSnapshot.Create("g", "other", original.Slots,
            [First with { ItemDisplayName = "Other room secret" }], [], [], [], [], null, DateTimeOffset.UtcNow,
            null, TrackingObservationState.Healthy, null, SnapshotSections.All);
        await new TrackingV2Store().ApplySnapshotAsync(other, enableNotifications: true);
        await RequestAsync("Player1");
        await TrackingReplayService.PrepareNextAsync();
        var transport = new FakeTransport();
        await Worker(transport).RunOnceAsync();
        Assert.DoesNotContain("Other room secret", Assert.Single(transport.Messages.Values).Content);
    }

    [Fact]
    public async Task Migration_is_repeatable_and_room_deletion_removes_replay_requests()
    {
        using var scope = new TestDatabaseScope();
        await ExecuteAsync("DROP TABLE TrackingReplayRequests;");
        await DBMigration_5.Migrate_5_0_17();
        await DBMigration_5.Migrate_5_0_17();
        await SeedAsync();
        await RequestAsync("Player1");
        await DatabaseCommands.DeleteChannelDataAsync("g", "c");
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("TrackingReplayRequests"));
    }

    private static async Task SeedAsync(bool silent = false)
    {
        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync("g", "c", "https://example.test", "room", "tracker", silent, "5m", "0");
        await new TrackingV2Store().ApplySnapshotAsync(Snapshot(), enableNotifications: true);
    }

    private static NormalizedRoomSnapshot Snapshot() => NormalizedRoomSnapshot.Create("g", "c",
        [new(1,"Player1","Player1","Game"), new(2,"Player2","Player2","Game")],
        [First, First with { ItemId=101,LocationId=201,ItemDisplayName="Old Shield" },
         First with { FinderSlot=1,ReceiverSlot=2,FinderDisplayName="Player1",ReceiverDisplayName="Player2",ItemId=102,LocationId=202,ItemDisplayName="Other Player Item" }],
        [], [], [], [new(1,NormalizedPlayerStatus.Playing,2,10,null),new(2,NormalizedPlayerStatus.Playing,1,10,null)],
        null, DateTimeOffset.UtcNow, null, TrackingObservationState.Healthy, null, SnapshotSections.All);

    private static async Task<long> RequestAsync(string alias)
    {
        // Same statement supplied to the user; no delivery history is required.
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO TrackingReplayRequests(GuildId,ChannelId,ReceiverSlot)
                SELECT GuildId,ChannelId,Slot FROM TrackingSlots WHERE GuildId='g' AND ChannelId='c' AND Alias=@Alias;";
            command.Parameters.AddWithValue("@Alias", alias);
            await command.ExecuteNonQueryAsync();
        });
        return Convert.ToInt64(await ScalarAsync("SELECT MAX(Id) FROM TrackingReplayRequests;"));
    }

    private static TrackingDeliveryWorker Worker(FakeTransport transport, TimeProvider? time = null)
        => new(new DiscordTrackingEventPublisher(transport), time ?? new Clock(), destinationType: TrackingDestination.NotificationDestinationType);

    private static Task ExecuteAsync(string sql) => Db.WriteAsync(async connection =>
    {
        using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync();
    });

    private static async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand(); command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow.AddMinutes(1);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance() => _now = _now.AddMinutes(1);
    }

    private sealed class FakeTransport : ITrackingDiscordTransport
    {
        public readonly Dictionary<string,TrackingDiscordNotification> Messages = [];
        public bool LoseResponse;
        public int SendCount;
        public Task<string?> FindPublicationAsync(string guild, string channel, string key, DateTimeOffset notBefore,
            TrackingDiscordNotification notification, CancellationToken ct)
            => Task.FromResult(Messages.ContainsKey(key) ? "receipt" : null);
        public Task<string> SendAsync(string guild, string channel, string key, TrackingDiscordNotification notification, CancellationToken ct)
        {
            SendCount++; Messages.Add(key,notification);
            if (LoseResponse) throw new InvalidOperationException("accepted, response lost");
            return Task.FromResult("receipt");
        }
    }
}
