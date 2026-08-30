# PR 5 — Scheduler central et client WebHost résilient

## Architecture

La base passe à `5.0.8` avec `RoomPollState`. Cette table conserve `NextPollAtUtc`, les derniers essais/succès, la classification de la dernière erreur, le nombre d'échecs consécutifs, l'ouverture éventuelle du breaker et la dernière latence. Le redémarrage recharge donc l'échéance durable au lieu de republier toutes les rooms immédiatement.

`CentralRoomScheduler` utilise une seule `PriorityQueue` versionnée. Une entrée par room est logiquement active ; les anciennes entrées deviennent inertes après promotion ou replanification. Il n'existe aucune boucle ni timer par room. La file est rechargée périodiquement depuis SQLite pour découvrir les ajouts, suppressions et changements de configuration.

Les limites sont appliquées à deux niveaux :

- budget global du processus, `TRACKING_GLOBAL_CONCURRENCY=10` par défaut ;
- budget par origine HTTP, `TRACKING_ORIGIN_CONCURRENCY=2` par défaut.

Le jitter est toujours positif et ajouté après l'échéance. Un échec reçoit un backoff exponentiel borné ; un `Retry-After` supérieur gagne. Les erreurs transitoires alimentent un circuit breaker par origine, tandis qu'une 404 ou une réponse partielle d'une room n'empêche pas les autres origines de progresser.

## Client WebHost

`ResilientWebHostClient` est partagé par les endpoints room status, tracker runtime et tracker statique. Il impose un timeout distinct par endpoint et classe sans journaliser l'URL complète :

- `NotFound` pour 404 ;
- `RateLimited` avec `Retry-After` pour 429 ;
- `ServerError` pour 5xx ;
- `Timeout` et `Network` ;
- `InvalidContentType` pour HTML ;
- `InvalidJson` ;
- `PartialResponse` quand la forme minimale attendue manque.

## Lifecycle, commandes et rollback

`TrackingDataManager.StartTracking` est idempotent : les événements Discord `Ready` et `Connected` ne peuvent plus lancer deux schedulers concurrents. L'arrêt annule le token, attend les requêtes actives et ne démarre aucun nouveau poll. Une mise à jour manuelle de fréquence promeut la room, avec un cooldown strict de 30 secondes.

Rollback immédiat : définir `USE_LEGACY_TRACKING_SCHEDULER=true` puis redémarrer. L'ancien scan minute reste disponible, mais utilise le client HTTP résilient partagé. Les événements/outbox V2 en attente restent intacts dans SQLite.

## Métriques

Les métriques suivantes n'utilisent aucun ID, nom de room, domaine ou URL comme label : profondeur de file, polls actifs, retard de dispatch, durée, résultat classifié et nombre de breakers ouverts.

## Validation

`CentralRoomSchedulerTests` couvre l'ordre de priorité, les limites globale/origine, l'isolation des échecs, le breaker, la reprise SQLite, la promotion limitée, l'arrêt sans tâche orpheline, les classifications HTTP et une charge structurelle de 1 000 rooms.
