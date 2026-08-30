# PR 4 — V2 persistence, idempotent ledger, and outbox

## Result

The database version is now `5.0.7`. The migration keeps every V1 table and adds `TrackedRooms`, `RoomSnapshots`, `TrackingEvents`, and `EventDeliveries`. Before creating the required V1 unique constraints, it deterministically keeps the greatest `ChannelsAndUrlsTable.Id`, reattaches useful patch rows to it, and keeps the greatest hint `Id` for each complete identity including `Entrance`.

`Program.CheckBdd` still creates a SQLite backup before every version migration. `Migrate_5_0_7` is transactional and idempotent.

## Transaction and baseline

`TrackingV2Store.ApplySnapshotAsync` performs the following under one `BEGIN IMMEDIATE`:

1. read the latest known snapshot;
2. consolidate missing sections with the last complete state;
3. insert the snapshot;
4. calculate and idempotently insert events;
5. create one delivery per event/destination pair;
6. atomically move the room's logical current-state pointer.

An exception at any point rolls the whole transaction back. A room's first snapshot is a silent baseline: no historical event or delivery is created. Identical content only refreshes synchronization time and does not add another snapshot.

## Replayable delivery

`TrackingDeliveryWorker` claims a delivery under a lease, increments its attempt counter, then marks it `Delivered` or `Failed`. A `Delivering` row whose lease expires becomes claimable again. Failures use bounded exponential backoff.

The `ITrackingEventPublisher` contract requires `EventKey` to be used as the idempotency key. Outbox transport semantics are therefore at-least-once, with exactly one logical publication when the publisher honors that contract. This covers the boundary where external publication succeeds but the SQLite acknowledgement is interrupted.

This PR deliberately wires no V2 Discord publisher. With `ENABLE_TRACKING_V2=true`, the legacy scheduler only dual-writes and fills the outbox; Discord notifications continue to come exclusively from V1.

## Operations and rollback

- experimental activation: `ENABLE_TRACKING_V2=true`;
- immediate rollback: set `ENABLE_TRACKING_V2=false` and restart;
- V1 tables are neither dropped nor renamed;
- existing V2 rows may remain for diagnosis or later replay;
- V2 payloads store no room URL, Discord token, or other secret.

## Validation

`TrackingV2PersistenceTests` covers baseline behavior, deduplication, partial responses, twelve concurrent writes, rollback after snapshot/event/delivery, running the migration twice, and recovery from a simulated failure between publication and acknowledgement.

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet test ArchipelagoSphereTracker.sln --configuration Release --no-restore
```
