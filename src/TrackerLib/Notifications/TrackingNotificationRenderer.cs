using ArchipelagoSphereTracker.src.Resources;
using ArchipelagoSphereTracker.Tracking.Notifications;
using ArchipelagoSphereTracker.Tracking.V2;
using System.Text;

public static partial class TrackingDataManager
{
    internal sealed record RenderedNotification(TrackingDiscordNotification Notification, IReadOnlyList<string> EventKeys);
    private sealed record NotificationEntry(string Text, string Key);

    internal static async Task<IReadOnlyList<RenderedNotification>> RenderTrackingNotificationsAsync(
        IReadOnlyList<NormalizedTrackingEvent> events, NormalizedRoomSnapshot snapshot, bool silent,
        RoomNotificationPreferences? preferences = null)
    {
        preferences ??= RoomNotificationPreferences.FromLegacy(silent);
        var result = new List<RenderedNotification>();
        var items = new List<(ItemReceivedEvent Event, ItemNotification Line)>();
        foreach (var received in events.OfType<ItemReceivedEvent>())
        {
            if (!preferences.AllowsItem(received.Item.Flags)) continue;
            var line = await BuildV2ItemLineAsync(received, preferences.LinkedAliasesOnly);
            if (line != null) items.Add((received, line));
        }
        foreach (var receiver in items.GroupBy(value => value.Event.Item.ReceiverSlot))
        foreach (var recipients in receiver.GroupBy(value => string.Join(",", value.Line.MentionedUserIds)))
        {
            var values = recipients.ToArray();
            var ids = values[0].Line.MentionedUserIds;
            var recipientChunks = ids.Count == 0 ? new[] { Array.Empty<string>() } : ids.Chunk(25).ToArray();
            foreach (var recipientIds in recipientChunks)
            {
                var mentions = string.Join(" ", recipientIds.Select(id => $"<@{id}>"));
                var entries = values.Select(value => new NotificationEntry(
                        FormatItemEntry(value.Event.Item), value.Event.EventKey))
                    .ToArray();
                AddNotificationChunks(result, entries,
                    (page, count) => FormatNotificationHeader(Resource.TrackingItemsTitle,
                        values[0].Event.Item.ReceiverDisplayName, values.Length, mentions, page, count), recipientIds);
            }
        }

        if (preferences.HintsEnabled)
        {
            foreach (var updated in new[] { false, true })
            {
                var hints = events.Select(value => value switch
                {
                    HintCreatedEvent created when !updated => (Event: value, Hint: created.Hint),
                    HintUpdatedEvent changed when updated => (Event: value, Hint: changed.Current),
                    _ => (Event: value, Hint: (NormalizedHint?)null)
                }).Where(value => value.Hint != null && value.Hint.FinderSlot != value.Hint.ReceiverSlot &&
                    preferences.AllowsItem(value.Hint.ItemFlags));
                foreach (var receiver in hints.GroupBy(value => value.Hint!.ReceiverSlot))
                {
                    var values = receiver.ToArray();
                    var first = values[0].Hint!;
                    var users = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(
                        snapshot.GuildId, snapshot.ChannelId, first.ReceiverDisplayName);
                    var userIds = new HashSet<string>(users.Select(user => user.UserId));
                    var lines = new List<NotificationEntry>();
                    foreach (var value in values)
                    {
                        var hint = value.Hint!;
                        var finders = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(
                            snapshot.GuildId, snapshot.ChannelId, hint.FinderDisplayName);
                        foreach (var user in finders) userIds.Add(user.UserId);
                        var mentions = string.Join(" ", finders.Select(user => $"<@{user.UserId}>"));
                        lines.Add(new(string.Format(Resource.TrackingHintDetails,
                                StyledNotificationLabel(hint.ItemDisplayName),
                                StyledNotificationLabel(hint.FinderDisplayName),
                                string.IsNullOrEmpty(mentions) ? "" : " " + mentions,
                                StyledNotificationLabel(hint.LocationDisplayName)), value.Event.EventKey));
                    }
                    var receiverMentions = string.Join(" ", users.Select(user => $"<@{user.UserId}>"));
                    if (preferences.LinkedAliasesOnly && userIds.Count == 0) continue;
                    AddNotificationChunks(result, lines,
                        (page, count) => FormatNotificationHeader(
                            updated ? Resource.TrackingHintsUpdatedTitle : Resource.TrackingHintsNewTitle,
                            first.ReceiverDisplayName, values.Length, receiverMentions, page, count), userIds);
                }
            }
        }

        foreach (var goal in events.OfType<GoalReachedEvent>())
        {
            var notification = await RenderGoalNotificationAsync(goal, snapshot, preferences.LinkedAliasesOnly, legacy: false);
            if (notification != null) result.Add(new(notification, [goal.EventKey]));
        }
        return result;
    }

    private static void AddNotificationChunks(List<RenderedNotification> result,
        IReadOnlyList<NotificationEntry> lines, Func<int, int, string> header, IEnumerable<string> userIds)
    {
        var limit = 1900 - header(int.MaxValue, int.MaxValue).Length - "\n>>> ".Length;
        var chunks = new List<List<NotificationEntry>>();
        var current = new List<NotificationEntry>();
        var length = 0;
        foreach (var line in lines)
        {
            var added = line.Text.Length + 2 + (current.Count > 0 ? 1 : 0);
            if (current.Count > 0 && length + added > limit)
            {
                chunks.Add(current);
                current = [];
                length = 0;
                added = line.Text.Length + 2;
            }
            current.Add(line);
            length += added;
        }
        if (current.Count > 0) chunks.Add(current);
        for (var index = 0; index < chunks.Count; index++)
        {
            var content = ">>> " + header(index + 1, chunks.Count) + "\n" +
                string.Join("\n", chunks[index].Select(line => "• " + line.Text));
            // Never silently drop an item when splitting long batches.
            if (content.Length > 2000) throw new InvalidOperationException("Notification line exceeds Discord's message limit.");
            result.Add(new(new TrackingDiscordNotification(content, userIds.Where(id => ulong.TryParse(id, out _))
                .Select(ulong.Parse).Distinct().Take(100).ToArray()), chunks[index].Select(line => line.Key).ToArray()));
        }
    }

    private static string FormatNotificationHeader(string template, string player, int total,
        string mentions, int page, int pageCount)
        => string.Format(template, StyledNotificationLabel(player), total)
            + (pageCount > 1 ? $" · {page}/{pageCount}" : "")
            + (string.IsNullOrEmpty(mentions) ? "" : " " + mentions);

    private static string FormatItemEntry(NormalizedItemTransfer item)
        => string.Format(Resource.TrackingItemDetails, StyledNotificationLabel(item.FinderDisplayName),
            StyledNotificationLabel(item.ItemDisplayName), StyledNotificationLabel(item.LocationDisplayName));

    // Labels are external data, not Markdown. Bound the escaped text without
    // cutting a surrogate pair or leaving an unfinished escape sequence.
    private static string StyledNotificationLabel(string value)
    {
        value = value.Replace("@", "@\u200b").Replace('\r', ' ').Replace('\n', ' ');
        var result = new StringBuilder();
        foreach (var rune in value.EnumerateRunes())
        {
            var escaped = rune.IsAscii && "\\`*_~|>[]".Contains((char)rune.Value);
            if (result.Length + rune.Utf16SequenceLength + (escaped ? 1 : 0) > 179)
                return result.Append('…').ToString();
            if (escaped) result.Append('\\');
            result.Append(rune.ToString());
        }
        return result.ToString();
    }

    private static Task<ItemNotification?> BuildV2ItemLineAsync(ItemReceivedEvent received, bool silent)
    {
        var item = received.Item;
        if (item.FinderSlot == item.ReceiverSlot || item.ItemId < 0 || item.LocationId < 0)
            return Task.FromResult<ItemNotification?>(null);
        return BuildItemLineAsync(received.GuildId, received.ChannelId, item, silent);
    }

    private sealed record ItemNotification(string Text, IReadOnlyList<string> MentionedUserIds);

    private static async Task<ItemNotification?> BuildItemLineAsync(
        string guild, string channel, NormalizedItemTransfer item, bool silent)
    {
        var text = string.Format(Resource.TDPMEssageItemsNoMention,
            SafeNotificationLabel(item.FinderDisplayName), SafeNotificationLabel(item.ItemDisplayName),
            SafeNotificationLabel(item.ReceiverDisplayName), SafeNotificationLabel(item.LocationDisplayName));
        var users = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guild, channel, item.ReceiverDisplayName);
        if (users.Count == 0) return silent ? null : new(text, []);
        var excluded = await ExcludedItemsCommands.GetExcludedUserIdsAsync(guild, channel,
            item.ReceiverDisplayName, item.ItemDisplayName);
        var ids = users.Where(user => !string.IsNullOrWhiteSpace(user.UserId) && !excluded.Contains(user.UserId))
            .Where(user => string.IsNullOrWhiteSpace(item.ReceiverGame) || !int.TryParse(user.Flag, out var mask) ||
                (mask & (1 << item.Flags)) == 0)
            .Select(user => user.UserId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return ids.Length == 0 ? null : new(text, ids);
    }

    internal static async Task<TrackingDiscordNotification?> RenderTrackingNotificationAsync(
        NormalizedTrackingEvent trackingEvent,
        NormalizedRoomSnapshot snapshot,
        bool silent)
    {
        var guild = trackingEvent.GuildId;
        var channel = trackingEvent.ChannelId;
        switch (trackingEvent)
        {
            case ItemReceivedEvent received:
            {
                var item = received.Item;
                var notification = await BuildV2ItemLineAsync(received, silent);
                if (notification == null) return null;
                var mentions = string.Join(" ", notification.MentionedUserIds.Select(id => $"<@{id}>"));
                return TrackingDiscordNotification.Create(
                    $"**{Resource.ItemFor} {SafeNotificationLabel(item.ReceiverDisplayName)} {mentions}:**\n>>> {notification.Text}",
                    notification.MentionedUserIds);
            }
            case HintCreatedEvent created when !silent:
                return await RenderHintNotificationAsync(guild, channel, created.Hint, updated: false);
            case HintUpdatedEvent updated when !silent:
                return await RenderHintNotificationAsync(guild, channel, updated.Current, updated: true);
            case GoalReachedEvent reached:
                return await RenderGoalNotificationAsync(reached, snapshot, silent, legacy: true);
            default:
                return null;
        }
    }

    private static async Task<TrackingDiscordNotification?> RenderGoalNotificationAsync(
        GoalReachedEvent reached, NormalizedRoomSnapshot snapshot, bool silent, bool legacy)
    {
        var slot = snapshot.Slots.FirstOrDefault(value => value.Slot == reached.Goal.Slot);
        if (slot == null) throw new InvalidOperationException("Goal slot is missing from snapshot.");
        var users = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(reached.GuildId, reached.ChannelId, slot.PlayerName);
        if (silent && users.Count == 0) return null;
        // Individual rendering is retained verbatim for pre-batch send recovery.
        var text = legacy
            ? string.Format(Resource.TDMGoalComplete, SafeNotificationLabel(slot.Alias), SafeNotificationLabel(slot.Game))
            : Resource.TrackingGoalTitle + "\n\n>>> " + string.Format(Resource.TrackingGoalDescription,
                StyledNotificationLabel(slot.Alias), StyledNotificationLabel(slot.Game));
        return TrackingDiscordNotification.Create(text, []);
    }

    private static async Task<TrackingDiscordNotification?> RenderHintNotificationAsync(
        string guild, string channel, NormalizedHint hint, bool updated)
    {
        if (hint.FinderSlot == hint.ReceiverSlot) return null;
        var receivers = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guild, channel, hint.ReceiverDisplayName);
        var finders = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guild, channel, hint.FinderDisplayName);
        var receiverMentions = string.Join(" ", receivers.Select(user => $"<@{user.UserId}>"));
        var finderMentions = string.Join(" ", finders.Select(user => $"<@{user.UserId}>"));
        var line = string.Format(updated ? Resource.HintItemUpdated : Resource.HintItemNew,
            SafeNotificationLabel(hint.ItemDisplayName), SafeNotificationLabel(hint.LocationDisplayName),
            $"{SafeNotificationLabel(hint.FinderDisplayName)} {finderMentions}");
        return TrackingDiscordNotification.Create(
            $"{(updated ? Resource.HintUpdated : Resource.HintNew)}: {SafeNotificationLabel(hint.ReceiverDisplayName)} {receiverMentions}:\n{line}",
            receivers.Concat(finders).Select(user => user.UserId));
    }

    private static string SafeNotificationLabel(string value)
    {
        value = value.Replace("@", "@\u200b").Replace('\r', ' ').Replace('\n', ' ');
        if (value.Length <= 180) return value;
        var length = char.IsHighSurrogate(value[178]) ? 178 : 179;
        return value[..length] + "…";
    }
}
