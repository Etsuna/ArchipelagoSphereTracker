# PR 6 — Adaptive polling and room health

## Problem addressed

Quiet rooms were polled as often as active rooms, while the scheduler's internal state could not be directly inspected or controlled. This change keeps the PR 5 central scheduler and adds an adaptive policy, durable pauses, and operational commands.

AST remains exclusively a client of public WebHost APIs. No Archipelago protocol connection is introduced.

## Architecture

Every successful WebHost read now produces the normalized snapshot hash, even when V2 dual-write is disabled. The scheduler compares it with the previous hash:

- the first success establishes the baseline at the configured minimum frequency;
- every run of three unchanged successes doubles the interval;
- the interval is capped at one hour and never becomes shorter than the configured frequency;
- any content change immediately restores the configured frequency;
- failures still use the PR 5 backoff, `Retry-After`, and circuit breaker.

The database version becomes `5.0.9`. `RoomPollState` stores pause state, the last forced sync, previous hash, unchanged-success count, effective interval, and last detected activity. Migration `5.0.9` is idempotent and all state is restored after restart.

## Commands and authorization

- `/ast-health`: sensitive-ID-free summary for all guild rooms; guild manager in a channel, authorized member in a thread.
- `/ast-room-health`: status, freshness, last sync/activity, next due time, errors, and latency; thread member.
- `/ast-sync-now`: immediate queue promotion; room organizer, durable 30-second cooldown.
- `/ast-pause` and `/ast-resume`: durable pause and immediate resume; room organizer.

Mutating operations use centralized authorization and are written to the audit log without tracker URLs. The same controls are available from the room portal, which is already restricted to room organizers.

A pause requested during a poll does not interrupt it: that request may finish and persist its result, after which no new poll starts.

## Validation

```powershell
dotnet build ArchipelagoSphereTracker.csproj -c Release --no-restore
dotnet test tests\ArchipelagoSphereTracker.Tests\ArchipelagoSphereTracker.Tests.csproj -c Release --no-restore
```

Tests cover slowdown and immediate acceleration, pause/resume across restart, forced-sync cooldown across restart, guild isolation, repeated migration, and complete SQLite round trips.

## Risks and rollback

The normalized hash is calculated for every successful WebHost response, adding CPU work proportional to response size but no network request. The existing administrator frequency becomes the automatic mode floor; the initial ceiling is deliberately conservative at one hour.

Immediate rollback: set `USE_LEGACY_TRACKING_SCHEDULER=true` and restart. The additive SQLite columns may remain. Legacy mode does not enforce persisted pauses and makes control commands unavailable, so it should only be used as a temporary rollback.
