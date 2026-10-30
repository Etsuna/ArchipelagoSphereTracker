using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.Tracking.Notifications;
using Discord;
using Xunit;

public sealed class TrackingDiscordTransportTests
{
    private const string Key = "aaaa1111";

    [Fact]
    public async Task Recovery_walks_past_the_first_page_and_only_accepts_the_bots_own_text()
    {
        using var scope = new TestDatabaseScope();
        var cursors = new List<ulong>();
        var observed = DateTimeOffset.UtcNow.AddHours(-3);
        var firstId = SnowflakeUtils.ToSnowflake(observed);
        var other = Enumerable.Range(1, 100).Select(index => Message(firstId + (ulong)index, 999, "other-key")).ToArray();
        other[0] = Message(firstId + 1, 999, Key); // same text from another account
        var own = Message(firstId + 101, 123, Key);
        var channel = Proxy<IMessageChannel>((method, args) =>
        {
            Assert.Equal("GetMessagesAsync", method.Name);
            cursors.Add((ulong)args![0]!);
            Assert.Equal(Direction.After, args[1]);
            Assert.Equal(100, args[2]);
            return Page(cursors.Count == 1 ? other : [own]);
        });
        var transport = new DiscordTrackingTransport((_, _, _) => Task.FromResult(channel), () => 123);
        Assert.Equal(own.Id.ToString(), await transport.FindPublicationAsync("1", "2", Key, observed, TrackingDiscordNotification.Create(Key, []), CancellationToken.None));
        Assert.Equal(new[] { SnowflakeUtils.ToSnowflake(observed.AddSeconds(-5)), firstId + 100 }, cursors);
    }

    [Fact]
    public async Task Send_uses_plain_grouped_text_without_embed_or_visible_identifiers()
    {
        var called = false;
        using var cts = new CancellationTokenSource();
        var channel = Proxy<IMessageChannel>((method, args) =>
        {
            Assert.Equal("SendMessageAsync", method.Name);
            called = true;
            Assert.Equal("test <@111> @everyone", args![0]);
            Assert.Null(args[2]); // no technical footer or embed
            var options = Assert.IsType<RequestOptions>(args[3]);
            Assert.Equal(cts.Token, options.CancelToken);
            Assert.Equal(RetryMode.RetryRatelimit, options.RetryMode);
            var mentions = Assert.IsType<AllowedMentions>(args[4]);
            Assert.Null(mentions.AllowedTypes); // never parse @everyone or arbitrary user mentions
            Assert.Equal(new[] { 111UL }, mentions.UserIds);
            return Task.FromResult(Proxy<IUserMessage>((_, _) => 42UL));
        });
        var transport = new DiscordTrackingTransport((_, _, _) => Task.FromResult(channel), () => 123);
        var notification = TrackingDiscordNotification.Create("test <@111> @everyone", ["111", "111", "invalid"]);
        Assert.Equal("42", await transport.SendAsync("1", "2", Key, notification, cts.Token));
        Assert.True(called);
    }

    [Fact]
    public async Task Recovery_failure_propagates_instead_of_being_treated_as_empty_history()
    {
        var transport = new DiscordTrackingTransport((_, _, _) => throw new InvalidOperationException("no access"), () => 123);
        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.FindPublicationAsync("1", "2", Key,
            DateTimeOffset.UtcNow, TrackingDiscordNotification.Create("test", []), CancellationToken.None));
    }

    [Fact]
    public async Task Recovery_does_not_reuse_a_message_already_acknowledged_for_another_batch()
    {
        using var scope = new TestDatabaseScope();
        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync("1", "2", "https://example.test", "room", "tracker", false, "5m", "0");
        var snapshot = ArchipelagoSphereTracker.Tracking.V2.NormalizedRoomSnapshot.Create("1", "2", [], [], [], [], [], [],
            null, DateTimeOffset.UtcNow, null, ArchipelagoSphereTracker.Tracking.V2.TrackingObservationState.Healthy,
            null, ArchipelagoSphereTracker.Tracking.V2.SnapshotSections.All);
        var stored = await new ArchipelagoSphereTracker.Tracking.Persistence.TrackingV2Store().ApplySnapshotAsync(snapshot);
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO DiscordNotificationBatches (BatchKey, SnapshotId, ChannelId, PayloadJson)
                VALUES ('old', @Snapshot, '2', @Payload);";
            command.Parameters.AddWithValue("@Snapshot", stored.SnapshotId);
            command.Parameters.AddWithValue("@Payload", System.Text.Json.JsonSerializer.Serialize(
                new DiscordNotificationBatch("old", [new("old-part", TrackingDiscordNotification.Create(Key, []), Receipt: "41")]),
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
            await command.ExecuteNonQueryAsync();
        });
        var channel = Proxy<IMessageChannel>((_, _) => Page([Message(41, 123, Key), Message(42, 123, Key)]));
        var transport = new DiscordTrackingTransport((_, _, _) => Task.FromResult(channel), () => 123);
        Assert.Equal("42", await transport.FindPublicationAsync("1", "2", Key, DateTimeOffset.UtcNow,
            TrackingDiscordNotification.Create(Key, []), CancellationToken.None));
    }

    [Fact]
    public async Task Upgrade_can_recover_an_uncertain_old_message_without_adding_any_new_footer()
    {
        using var scope = new TestDatabaseScope();
        var message = Proxy<IMessage>((method, _) => method.Name switch
        {
            "get_Id" => 42UL,
            "get_Author" => Proxy<IUser>((_, _) => 123UL),
            "get_Content" => "old individual message",
            "get_Embeds" => new IEmbed[] { new EmbedBuilder().WithFooter($"AST • {Key}").Build() },
            _ => throw new InvalidOperationException(method.Name)
        });
        var channel = Proxy<IMessageChannel>((_, _) => Page([message]));
        var transport = new DiscordTrackingTransport((_, _, _) => Task.FromResult(channel), () => 123);
        Assert.Equal("42", await transport.FindPublicationAsync("1", "2", Key, DateTimeOffset.UtcNow,
            TrackingDiscordNotification.Create("different format", []), CancellationToken.None));
    }

    [Fact]
    public async Task Recovery_recognizes_an_image_notification_sent_before_image_titles_were_removed()
    {
        using var scope = new TestDatabaseScope();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Key))).ToLowerInvariant();
        IMessage Uploaded(ulong id, ulong author, string filename) => Proxy<IMessage>((method, _) => method.Name switch
        {
            "get_Id" => id,
            "get_Author" => Proxy<IUser>((_, _) => author),
            "get_Content" => "",
            "get_Attachments" => new[] { Proxy<IAttachment>((_, _) => filename) },
            "get_Embeds" => Array.Empty<IEmbed>(),
            _ => throw new InvalidOperationException(method.Name)
        });
        var channel = Proxy<IMessageChannel>((_, _) => Page([
            Uploaded(40, 999, $"ast-banner-{hash}.png"),
            Uploaded(41, 123, "different-message.png"),
            Uploaded(42, 123, $"ast-banner-{hash}.png")]));
        var transport = new DiscordTrackingTransport((_, _, _) => Task.FromResult(channel), () => 123);
        Assert.Equal("42", await transport.FindPublicationAsync("1", "2", Key, DateTimeOffset.UtcNow,
            TrackingDiscordNotification.Create("original text", []), CancellationToken.None));
    }

    [Fact]
    public async Task Stored_image_metadata_is_ignored_and_the_original_text_is_sent()
    {
        var notification = System.Text.Json.JsonSerializer.Deserialize<TrackingDiscordNotification>(
            """{"Content":"original text","UserIds":[111],"Banner":{"ThemeId":"halloween","Title":"old title","Body":"old body","Language":"en"}}""")!;
        var channel = Proxy<IMessageChannel>((method, args) =>
        {
            Assert.Equal("SendMessageAsync", method.Name);
            Assert.Equal("original text", args![0]);
            Assert.Null(args[2]);
            Assert.Null(args[6]); // No image or Components V2 layout.
            Assert.Equal(new[] { 111UL }, Assert.IsType<AllowedMentions>(args[4]).UserIds);
            return Task.FromResult(Proxy<IUserMessage>((_, _) => 42UL));
        });
        var transport = new DiscordTrackingTransport((_, _, _) => Task.FromResult(channel), () => 123);
        Assert.Equal("42", await transport.SendAsync("1", "2", Key, notification, CancellationToken.None));
    }

    [Fact]
    public void Long_messages_fit_Discord_limits_without_splitting_surrogate_pairs()
    {
        var message = TrackingDiscordNotification.Create(new string('x', 1898) + "😀" + new string('x', 300), []);
        Assert.True(message.Content.Length <= 1900);
        Assert.False(char.IsHighSurrogate(message.Content[^2]));
    }

    private static IMessage Message(ulong id, ulong author, string content)
        => Proxy<IMessage>((method, _) => method.Name switch
        {
            "get_Id" => id,
            "get_Author" => Proxy<IUser>((_, _) => author),
            "get_Content" => content,
            "get_Embeds" => Array.Empty<IEmbed>(),
            _ => throw new InvalidOperationException(method.Name)
        });

    private static async IAsyncEnumerable<IReadOnlyCollection<IMessage>> Page(IReadOnlyCollection<IMessage> messages)
    {
        await Task.CompletedTask;
        yield return messages;
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class
    {
        var proxy = DispatchProxy.Create<T, RoomDestinationDiscordProxy>();
        ((RoomDestinationDiscordProxy)(object)proxy).Handler = invoke;
        return proxy;
    }
}
