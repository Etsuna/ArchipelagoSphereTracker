using System.Globalization;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.V2;
using Discord;
using Discord.WebSocket;

namespace ArchipelagoSphereTracker.Tracking.Notifications;

public sealed record TrackingDiscordNotification(string Content, IReadOnlyList<ulong> UserIds)
{
    public static TrackingDiscordNotification Create(string content, IEnumerable<string> userIds)
    {
        if (content.Length > 1900)
        {
            var length = char.IsHighSurrogate(content[1898]) ? 1898 : 1899;
            content = content[..length] + "…";
        }
        return new(content, userIds.Where(id => ulong.TryParse(id, out _))
            .Select(ulong.Parse).Distinct().Take(100).ToArray());
    }
}

public interface ITrackingDiscordTransport
{
    Task<string?> FindPublicationAsync(string guildId, string channelId, string eventKey,
        DateTimeOffset notBefore, TrackingDiscordNotification notification, CancellationToken cancellationToken);
    Task<string> SendAsync(string guildId, string channelId, string eventKey,
        TrackingDiscordNotification notification, CancellationToken cancellationToken);
}

public sealed class DiscordTrackingEventPublisher(ITrackingDiscordTransport transport) : ITrackingEventPublisher
{
    public async Task<TrackingPublicationResult> PublishAsync(
        TrackingDeliveryEnvelope delivery, CancellationToken cancellationToken)
    {
        if (delivery.DestinationType != TrackingDestination.NotificationDestinationType)
            throw new InvalidOperationException("Unexpected Discord delivery destination.");
        var trackingEvent = TrackingSnapshotJson.DeserializeEvent(delivery.EventType, delivery.EventPayloadJson);
        var snapshot = TrackingSnapshotJson.Deserialize(delivery.SnapshotPayloadJson);
        if (delivery.EventKey != trackingEvent.EventKey || delivery.DestinationId != trackingEvent.ChannelId ||
            snapshot.GuildId != trackingEvent.GuildId || snapshot.ChannelId != trackingEvent.ChannelId)
            throw new InvalidOperationException("Notification scope does not match its event.");

        var silent = await ReadSilentAsync(trackingEvent.GuildId, trackingEvent.ChannelId, cancellationToken);
        if (silent == null) return new("skipped:room-removed");
        var batch = await DiscordNotificationBatchStore.GetOrCreateAsync(delivery, snapshot, silent.Value, cancellationToken);
        if (batch.Parts.Count == 0) return new("skipped:notification-preferences");
        var receipts = new List<string>();
        for (var index = 0; index < batch.Parts.Count; index++)
        {
            var part = batch.Parts[index];
            var receipt = part.Receipt;
            if (receipt == null && part.FirstAttemptAtUtc is { } attemptedAt)
                receipt = await transport.FindPublicationAsync(trackingEvent.GuildId, trackingEvent.ChannelId,
                    part.Key, attemptedAt, part.Notification, cancellationToken);
            if (receipt == null)
            {
                // Persist the exact text and attempt boundary before making the
                // external request. A retry keeps the same groups and pages.
                await DiscordNotificationBatchStore.UpdatePartAsync(batch.Key, index,
                    value => value with { FirstAttemptAtUtc = value.FirstAttemptAtUtc ?? DateTimeOffset.UtcNow }, cancellationToken);
                receipt = await transport.SendAsync(trackingEvent.GuildId, trackingEvent.ChannelId,
                    part.Key, part.Notification, cancellationToken);
            }
            if (part.Receipt == null)
                await DiscordNotificationBatchStore.UpdatePartAsync(batch.Key, index,
                    value => value with { Receipt = receipt }, cancellationToken);
            receipts.Add(receipt);
        }
        return new(string.Join(",", receipts));
    }

    private static async Task<bool?> ReadSilentAsync(string guildId, string channelId, CancellationToken ct)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Silent FROM ChannelsAndUrlsTable WHERE GuildId = @Guild AND ChannelId = @Channel;";
        command.Parameters.AddWithValue("@Guild", guildId);
        command.Parameters.AddWithValue("@Channel", channelId);
        var result = await command.ExecuteScalarAsync(ct);
        return result == null ? null : result != DBNull.Value && Convert.ToBoolean(result, CultureInfo.InvariantCulture);
    }
}

public sealed class DiscordTrackingTransport : ITrackingDiscordTransport
{
    private readonly Func<string, string, CancellationToken, Task<IMessageChannel>> _resolve;
    private readonly Func<ulong> _botUserId;

    public DiscordTrackingTransport(DiscordSocketClient client)
    {
        _botUserId = () => client.CurrentUser.Id;
        _resolve = async (guildId, channelId, ct) =>
        {
            var channel = await client.Rest.GetChannelAsync(ulong.Parse(channelId), Options(ct));
            if (channel is not IMessageChannel messages || channel is not IGuildChannel guildChannel ||
                guildChannel.GuildId != ulong.Parse(guildId))
                throw new InvalidOperationException("Discord notification channel is unavailable in this guild.");
            // Discord can return an empty history when this permission is missing.
            // Verify it explicitly before treating an empty result as proof of no send.
            var permissionChannel = guildChannel;
            if (channel is Discord.Rest.RestThreadChannel thread)
            {
                // Thread permissions are inherited from the parent. The REST
                // thread payload itself has no permission_overwrites field.
                permissionChannel = await client.Rest.GetChannelAsync(thread.ParentChannelId, Options(ct)) as IGuildChannel
                    ?? throw new InvalidOperationException("Discord thread parent is unavailable.");
                if (permissionChannel.GuildId != guildChannel.GuildId)
                    throw new InvalidOperationException("Discord thread parent belongs to another guild.");
            }
            var permissions = client.GetGuild(guildChannel.GuildId)?.CurrentUser.GetPermissions(permissionChannel);
            if (permissions?.ViewChannel != true || permissions?.ReadMessageHistory != true)
                throw new InvalidOperationException("Discord notification recovery requires Read Message History.");
            return messages;
        };
    }

    internal DiscordTrackingTransport(
        Func<string, string, CancellationToken, Task<IMessageChannel>> resolve, Func<ulong> botUserId)
    {
        _resolve = resolve;
        _botUserId = botUserId;
    }

    public async Task<string?> FindPublicationAsync(string guildId, string channelId, string eventKey,
        DateTimeOffset notBefore, TrackingDiscordNotification notification, CancellationToken cancellationToken)
    {
        var channel = await _resolve(guildId, channelId, cancellationToken);
        // A small clock tolerance covers a request crossing a second boundary.
        // Receipt reuse is excluded, including identical text from earlier batches.
        var cursor = SnowflakeUtils.ToSnowflake(notBefore.AddSeconds(-5));
        while (true)
        {
            var page = (await channel.GetMessagesAsync(cursor, Direction.After, 100,
                options: Options(cancellationToken)).FlattenAsync()).ToArray();
            foreach (var message in page.OrderBy(message => message.Id))
                if (message.Author.Id == _botUserId() &&
                    (message.Content == notification.Content ||
                     message.Embeds.Any(embed => embed.Footer?.Text == $"AST • {eventKey}")))
                {
                    var receipt = message.Id.ToString(CultureInfo.InvariantCulture);
                    if (!await DiscordNotificationBatchStore.IsReceiptUsedAsync(channelId, receipt, cancellationToken))
                        return receipt;
                }
            if (page.Length < 100) return null;
            var next = page.Max(message => message.Id);
            if (next <= cursor) throw new InvalidOperationException("Discord history pagination did not advance.");
            cursor = next;
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public async Task<string> SendAsync(string guildId, string channelId, string eventKey,
        TrackingDiscordNotification notification, CancellationToken cancellationToken)
    {
        var gate = TrackingDataManager.RateLimitGuards.GetGuildSendGate(ulong.Parse(guildId));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var channel = await _resolve(guildId, channelId, cancellationToken);
            var message = await channel.SendMessageAsync(notification.Content,
                allowedMentions: new AllowedMentions { UserIds = notification.UserIds.ToList() },
                options: Options(cancellationToken));
            return message.Id.ToString(CultureInfo.InvariantCulture);
        }
        finally { gate.Release(); }
    }

    // Do not let SDK timeout/502 retries repeat an uncertain POST invisibly.
    // The outbox owns retries and checks history before sending again.
    private static RequestOptions Options(CancellationToken ct)
        => new() { CancelToken = ct, RetryMode = RetryMode.RetryRatelimit };
}
