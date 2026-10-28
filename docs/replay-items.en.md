# Replay a player's item notifications

Database migration `5.0.17` adds `TrackingReplayRequests`. Replay works for items present in the silent baseline, including V1 history, without deleting inventory or fabricating new item-received events.

Start the new build to apply the migration. Then insert a request in its `AST.db`, replacing the guild ID, tracked channel/thread ID and exact canonical slot name:

```sql
INSERT INTO TrackingReplayRequests (GuildId, ChannelId, ReceiverSlot)
SELECT GuildId, ChannelId, Slot
FROM TrackingSlots
WHERE GuildId = 'GUILD_ID'
  AND ChannelId = 'CHANNEL_ID'
  AND Alias = 'Player1';
```

The bot may remain running while this request is committed. Each insertion intentionally requests another replay. An unmatched slot inserts no request.

The worker uses the latest complete native item snapshot. A newly migrated V1 room without a usable snapshot keeps its request pending until normal synchronization or **Sync now** in `/ast` provides one. Replay is scoped to one receiving slot and room. Grouping, pagination, silent mode, subscriptions, category filters and personal exclusions remain active.

Each request creates an independent `ItemsReplayRequested` delivery. It does not change room snapshots, inventory, checks, goals or personal recaps. Prepared pages and receipts persist for crash recovery. Separate requests for the same slot and snapshot remain distinct.

Inspect requests with:

```sql
SELECT r.Id, r.ReceiverSlot, r.Status AS RequestStatus,
       r.ItemCount, r.ReasonCode,
       d.Status AS DeliveryStatus, d.LastErrorCode, d.ExternalReceiptId
FROM TrackingReplayRequests r
LEFT JOIN EventDeliveries d ON d.EventId = r.EventId
WHERE r.GuildId = 'GUILD_ID' AND r.ChannelId = 'CHANNEL_ID'
ORDER BY r.Id DESC LIMIT 10;
```

`Pending` with `WAITING_FOR_ITEM_SNAPSHOT` needs a complete native sync. `Queued` means the request has a delivery: inspect its delivery status for progress. `Empty` means no eligible cross-slot transfers were found; `Rejected` means the configured room or slot is missing. `ItemCount` is measured before notification preferences; `skipped:notification-preferences` means no Discord message was sent.

Room-data deletion removes its requests. Automatic cleanup after all checks waits for pending replay requests and deliveries.
