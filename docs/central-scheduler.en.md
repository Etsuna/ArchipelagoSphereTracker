# PR 5 — Central scheduler and resilient WebHost client

## Architecture

The database version is now `5.0.8` with `RoomPollState`. It stores `NextPollAtUtc`, last attempt/success, the latest failure classification, consecutive failures, an optional breaker deadline, and latest latency. Restarts therefore restore durable due times instead of polling every room immediately.

`CentralRoomScheduler` uses one versioned `PriorityQueue`. One entry per room is logically active; older entries become inert after promotion or rescheduling. There is no per-room loop or timer. The queue periodically reloads from SQLite to discover additions, removals, and configuration changes.

Limits are enforced at two levels:

- process-wide budget, `TRACKING_GLOBAL_CONCURRENCY=10` by default;
- per HTTP origin budget, `TRACKING_ORIGIN_CONCURRENCY=2` by default.

Jitter is always positive and added after the due time. Failures receive bounded exponential backoff; a greater `Retry-After` wins. Transient errors feed a per-origin circuit breaker, while a room-specific 404 or partial response does not prevent other origins from progressing.

## WebHost client

`ResilientWebHostClient` is shared by room-status, runtime-tracker, and static-tracker endpoints. It applies endpoint-specific timeouts and classifies failures without logging full URLs:

- `NotFound` for 404;
- `RateLimited` with `Retry-After` for 429;
- `ServerError` for 5xx;
- `Timeout` and `Network`;
- `InvalidContentType` for HTML;
- `InvalidJson`;
- `PartialResponse` when the required minimum shape is absent.

## Lifecycle, commands, and rollback

`TrackingDataManager.StartTracking` is idempotent: Discord `Ready` and `Connected` events can no longer start concurrent schedulers. Shutdown cancels the token, waits for active requests, and starts no new poll. A manual frequency update promotes the room with a strict 30-second cooldown.

Immediate rollback: set `USE_LEGACY_TRACKING_SCHEDULER=true` and restart. The old minute scan remains available but uses the shared resilient HTTP client. Pending V2 events/outbox rows remain intact in SQLite.

## Metrics

The following metrics use no ID, room name, domain, or URL labels: queue depth, active polls, dispatch lag, duration, classified outcome, and open breaker count.

## Validation

`CentralRoomSchedulerTests` covers priority ordering, global/origin limits, failure isolation, breaker behavior, SQLite restart recovery, rate-limited promotion, orphan-free shutdown, HTTP classifications, and a structural load of 1,000 rooms.
