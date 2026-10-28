# Native V2 tracking migration

Database `5.0.16` makes V2 the data source for tracking, Discord commands, the portal, exclusion catalogs, spoiler analysis and metrics. The V1 comparison/publication pipeline and tracking dual-write are removed.

`WebHostSnapshotParser` parses raw Archipelago IDs and tuples directly. `ProcessingContext` loads slot definitions and datapackage dictionaries from the native read model. A snapshot transaction commits the snapshot, the `TrackingSlots`, `TrackingItems`, `TrackingHints` and `TrackingPlayerStates` projections, the event ledger/outbox, personal recap additions and the last-item timestamp together. Unchanged item and hint sections are not rewritten on activity-only polls.

Checks and client status are independently optional. Missing sections preserve known state; complete empty sections represent actual emptiness. User subscriptions, exclusions and personal recap contents remain separate user data. Canonical slot names remain stable when display aliases change. Clearing a recap does not clear room history or cause that history to reappear on the next poll.

## Existing installations

Normal startup creates a pre-migration backup. Migration `5.0.16` is transactional and repeatable:

- Legacy display rows are imported into provisional projections so commands remain usable before the first native poll.
- Missing raw item/location IDs stay SQL `NULL`, with provisional `legacy:Id` identities. No Archipelago IDs are fabricated.
- Active V2 rooms rebuild projections from their latest authoritative snapshot.
- Former V1/shadow rooms establish a silent native baseline. Imported sections remain readable until a complete live observation replaces them.
- Existing configuration, preferences, recaps, snapshots, events and deliveries are preserved. Import generates no historical notifications.
- A migration marker prevents reruns from resurrecting old display history.

Migration `5.0.18` completes the import and drops `AliasChoicesTable`, `DisplayedItemTable`, `GameStatusTable` and `HintStatusTable`. It refreshes the latest V1 data, including databases already upgraded to `5.0.16` or `5.0.17`. Active V2 snapshots remain authoritative. The final import and table removal share one transaction: failure preserves the source tables. Foreign keys from custom tables block retirement to preserve their data.

Empty legacy tables are removed too and startup no longer recreates them. Configuration, subscriptions, exclusions and personal recaps remain in their dedicated tables. No manual `DROP TABLE` is needed.

## Operation

`ENABLE_TRACKING_V2` is obsolete. Any old value, including `false`, is ignored with a startup notice. Native V2 is always active. `USE_LEGACY_TRACKING_SCHEDULER` remains independent and only selects scheduling behavior.

Grouped notifications, recipient filters, pagination and persisted recovery receipts remain in place, without visible technical footers. Private threads, public threads and existing text channels remain supported with no privileged intents.

Returning to an older executable requires its matching database backup: V1 tables have been removed from the active database. The pre-migration backup retains the old data on disk and is not restored automatically.

Migration `5.0.17` adds an [explicit notification replay queue](replay-items.en.md) for V1 and silent-baseline items, without changing business reception history or personal recaps.

Validation uses temporary databases and simulated Discord transport. No bot process was started and no running-instance database was migrated during testing. Tests cover migration preservation, repeatability, rollback, removal without recreation, custom references, backup retention, active-snapshot and pending-delivery preservation, partial first polls, recap clearing, independent check updates and cascade deletion.
