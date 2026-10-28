# V2 tracking and Discord notifications

Tracking is now native V2 and always active. `ENABLE_TRACKING_V2` is obsolete and ignored. The scheduler choice (`USE_LEGACY_TRACKING_SCHEDULER`) remains independent. See [native migration `5.0.16`](tracking-v2-native.en.md).

## Cutover

SQLite migration `5.0.14` adds `TrackedRooms.NotificationsV2Initialized`. Migration `5.0.15` adds `DiscordNotificationBatches` to preserve groups, pages and their delivery receipts. Normal startup backs up the database before migration. Existing data and preferences are preserved.

The first V2 poll of each room establishes a silent baseline, including rooms previously tracked by the experimental dual-write. Changes before this baseline are not announced. Experimental `DiscordChannel` deliveries remain available for diagnosis; the new worker only claims `DiscordNotificationV2` deliveries.

Subsequent polls atomically persist snapshots, events and notifications. A persistence failure fails the poll without falling back to V1 publication. Commands read native V2 projections committed with the snapshot. Personal recaps are appended once per transfer in the same transaction. Initial hint exports during room setup remain available.

## Notifications

- Cross-slot transfers retain their `ItemReceived` events; notifications from the same poll are grouped by receiving slot and exact recipient set, with a total count. Long groups are split into pages without losing items. `ItemSent` remains ledger-only.
- Created and updated hints mention the relevant players and remain suppressed in silent mode.
- Goals use Archipelago's `CLIENT_GOAL` status, independently of the check percentage. Silent rooms announce goals only for subscribed slots.
- Item subscriptions, personal exclusions and category filters remain per recipient, including shared slots.
- Individual checks, player statuses and activity events remain ledger-only, without additional Discord messages.

Preferences are read before preparing the first send. Message text and mentions are then frozen so retries resume the same groups and pages. Filtered notifications are acknowledged with a `skipped:notification-preferences` receipt. Names and messages are bounded to fit Discord limits. Only explicit user IDs are allowed to trigger mentions.

Private threads, public threads and existing text channels are supported. The only gateway intent remains `Guilds`; no privileged intents are needed.

### New message layout

Group titles show the receiving player and item count. Mentions have their own line, followed by bold item names with their sender and location underneath. The body retains the vertical quote bar on the left. Example without subscriptions:

```text
📦 **Items for Player2** · **(2)**

>>> - **Spring Gum**
  ↳ From **Player1** · *Municipal Control 002*
- **Double Jump**
  ↳ From **Player3** · *Old Tower*
```

New hints (`🔎`), updated hints (`🧭`) and goals (`🏁`) use the same layout. Split groups append `1/2`, `2/2`, etc. to their titles. External names are escaped to preserve formatting; filters and mentions are unchanged.

The layout applies to new groups and new replay requests. Already frozen groups retain their exact text so interrupted sends can be recovered. Existing Discord messages are not edited.

## Recovery

The delivery worker starts and stops with tracking, waits for a Discord connection and retries failed or expired deliveries. Delivery order is maintained per destination, without blocking other rooms.

Notifications are plain text with no visible technical identifier or footer. Groups and Discord message IDs remain in SQLite. Each page has its own persisted receipt, so an interrupted batch does not resend acknowledged pages.

After an uncertain send, recovery walks history from the start of the attempt, comparing exact text only in the bot’s own messages and excluding receipts already assigned to another batch. Old footers can still be recognized to acknowledge interrupted sends from before the upgrade; no new footer is added. History read failures remain retryable failures. SDK timeout/502 POST retries are disabled so the outbox controls uncertain retries.

The bot requires View Channel, Read Message History, Embed Links and the appropriate sending permissions, already included in the recommended invitation. Thread permissions are checked on the parent channel. Recovery uses the bot's own messages and does not require Message Content, under [Discord's own-message exception](https://docs.discord.com/developers/events/gateway#message-content-intent).

Transport remains at least once: deleting or editing recovery messages can permit duplicates after an incident. Another simultaneous bot send with exactly the same text can also make recovery ambiguous. Ambiguous network failures cannot provide an absolute exactly-once guarantee. Processing has a bounded lease, and acknowledgements are fenced by the current attempt number.

Automatic cleanup after all checks are complete waits for outstanding notifications. Explicitly stopping tracking or deleting its data cancels remaining deliveries through existing SQLite cascades.

## Rollback and validation

Deploy and start AST normally. Migrations through `5.0.18` run with a pre-migration backup; the last one finishes the V1 import and removes its four old tracking tables. No bot process or live Discord test was started for this change.

The environment-variable rollback to V1 is removed. Returning to an older executable requires its corresponding database backup because V1 tables have been removed from the active database.

Tests cover migration, shadow cutover, rollback, transaction failures, recipient preferences, partial responses, paginated history recovery and failure after an accepted send. Discord transport is simulated; no messages are sent to a real server.

References: [Discord message API](https://docs.discord.com/developers/resources/message), [Discord.Net 3.18 channel implementation](https://github.com/discord-net/Discord.Net/blob/3.18.0/src/Discord.Net.Rest/Entities/Channels/ChannelHelper.cs).
