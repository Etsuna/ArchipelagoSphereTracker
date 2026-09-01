# PR 3 — Normalized V2 snapshots and events

## Problem

The legacy pipeline mixes WebHost parsing, comparison, SQLite, Discord rendering, and delivery. Its identities often rely on display names, so an alias or translation can accidentally change the identity of an item or hint.

This PR introduced a pure core without changing production notifications. PR 4 can now feed it through dual-write when `ENABLE_TRACKING_V2=true`, still without V2 Discord publication.

## Architecture

The `ArchipelagoSphereTracker.Tracking.V2` module contains:

- `NormalizedRoomSnapshot`, an immutable and sorted snapshot;
- normalized slot, transfer, hint, check, goal, and player-state values;
- `TrackingSnapshotDiff.Diff(previous, current)`, a pure function with no SQLite, network, or Discord dependency;
- all ten P0 events: `ItemReceived`, `ItemSent`, `HintCreated`, `HintUpdated`, `GoalReached`, `PlayerStatusChanged`, `CheckCompleted`, `RoomActivityChanged`, `TrackingError`, and `TrackingRecovered`;
- `LegacySnapshotAdapter`, a temporary adapter from `ProcessingContext` and the current WebHost JSON.

Legacy `DisplayedItem`, `HintStatus`, and `GameStatus` values now retain the required raw IDs. The historical pipeline continues to use their display fields as before.

## Normalization and completeness

Every collection is deduplicated and sorted before hashing. `CapturedAtUtc` and `LastSuccessfulSyncUtc` remain part of the snapshot but are excluded from its content hash, so collecting identical business data at a later time does not create new content.

`SnapshotSections` independently marks slots, items, hints, checks, goals, player statuses, room activity, and tracking state as complete. A missing or `null` section is not treated as an empty collection. The diff ignores incomplete sections, preventing partial responses from producing false destructive events.

The first snapshot is a silent baseline. No historical event is emitted when `previous` is `null`.

## Stable identities and keys

Identities use raw WebHost IDs:

- item: finder slot, receiver slot, item ID, and location ID;
- hint: finder slot, receiver slot, item ID, location ID, and raw entrance;
- check: slot and location ID;
- goal/status: slot and goal identifier or transition.

Aliases, localized names, and Discord text never participate in a key. Every event retains `OccurredAtUtc`. Each `EventKey` is a hexadecimal SHA-256 of the event type, guild, channel, and canonical identity. Intrinsically unique events (items, checks, goals) remain time-independent; repeatable transitions (status, error/recovery, and hint updates) include their observation time. The same diff therefore produces exactly the same keys without conflating two successive identical incidents.

The `Hint` tuple follows the official `(receiving_player, finding_player, location, item, found, entrance, item_flags, status)` contract. Goals are based on `player_status = CLIENT_GOAL`, not an estimate derived from check counts. References: [WebHost tracker API](https://github.com/ArchipelagoMW/Archipelago/blob/main/WebHostLib/api/tracker.py) and [`Hint` type](https://github.com/ArchipelagoMW/Archipelago/blob/main/NetUtils.py).

## Main files

- `src/TrackerLib/Normalization/NormalizedTrackingModels.cs`
- `src/TrackerLib/Normalization/TrackingSnapshotDiff.cs`
- `src/TrackerLib/Normalization/LegacySnapshotAdapter.cs`
- `src/TrackerLib/Services/TrackerStreamParser.cs`
- `tests/ArchipelagoSphereTracker.Tests/TrackingNormalizationTests.cs`

## Validation

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet test ArchipelagoSphereTracker.sln --configuration Release --no-restore
```

Tests cover JSON ordering, unknown or `null` fields, missing aliases, raw IDs, deduplication, canonical hashing, baseline behavior, every event type, error/recovery transitions, and key stability across translated display names.

## Risks and rollback

- The current WebHost representation is effectively single-team; the model preserves AST's slot IDs, and an explicit team ID will be needed if WebHost generalizes multiple teams.
- The adapter remains transitional; V2 persistence now consumes it without duplicating diff logic.
- The SQLite schema and outbox are documented in `tracking-v2-persistence.en.md`.
- Functional rollback: keep `ENABLE_TRACKING_V2=false`. The historical pipeline and its tables remain intact.
