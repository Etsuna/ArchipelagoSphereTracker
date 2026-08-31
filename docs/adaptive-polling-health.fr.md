# PR 6 — Polling adaptatif et santé des rooms

## Problème traité

Une room calme était interrogée aussi souvent qu'une room active et l'état interne du scheduler n'était pas directement consultable ou contrôlable. Cette évolution conserve le scheduler central de la PR 5 et lui ajoute une politique adaptative, une pause durable et des commandes d'exploitation.

AST reste exclusivement client des API publiques du WebHost. Aucune connexion au protocole Archipelago n'est ajoutée.

## Architecture

Chaque lecture WebHost produit désormais le hash du snapshot normalisé, même lorsque le dual-write V2 est désactivé. Le scheduler compare ce hash au précédent :

- le premier succès établit la référence à la fréquence minimale configurée ;
- chaque série de trois succès sans changement double l'intervalle ;
- l'intervalle est plafonné à une heure et ne descend jamais sous la fréquence configurée ;
- tout changement de contenu rétablit immédiatement la fréquence configurée ;
- une erreur utilise toujours le backoff, `Retry-After` et le circuit breaker de la PR 5.

La version de base passe à `5.0.9`. `RoomPollState` conserve la pause, la dernière synchronisation forcée, le dernier hash, le nombre de succès inchangés, l'intervalle effectif et la dernière activité détectée. La migration `5.0.9` est idempotente et ces données sont rechargées après redémarrage.

## Commandes et autorisations

- `/ast-health` : résumé sans identifiant sensible de toutes les rooms du serveur ; gestionnaire du serveur dans un salon, membre autorisé dans un thread.
- `/ast-room-health` : état, fraîcheur, dernières synchronisation/activité, prochaine échéance, erreurs et latence ; membre du thread.
- `/ast-sync-now` : promotion immédiate dans la file ; organisateur de room, cooldown durable de 30 secondes.
- `/ast-pause` et `/ast-resume` : pause durable et reprise immédiate ; organisateur de room.

Les actions mutantes passent par l'autorisation centralisée et sont inscrites dans le journal d'audit sans URL de tracker. Les mêmes contrôles sont disponibles dans le portail de room, déjà réservé aux organisateurs.

Une pause demandée pendant une requête ne l'interrompt pas : cette requête peut finir et sauvegarder son résultat, puis aucune nouvelle lecture n'est lancée.

## Validation

```powershell
dotnet build ArchipelagoSphereTracker.csproj -c Release --no-restore
dotnet test tests\ArchipelagoSphereTracker.Tests\ArchipelagoSphereTracker.Tests.csproj -c Release --no-restore
```

Les tests couvrent le ralentissement et la réaccélération, la pause/reprise après redémarrage, le cooldown forcé après redémarrage, l'isolation par serveur, la migration répétée et le round-trip complet SQLite.

## Risques et rollback

Le hash normalisé est calculé sur chaque succès WebHost, ce qui ajoute un coût CPU proportionnel à la taille de la réponse mais aucune requête réseau. La fréquence administrateur existante devient le plancher du mode automatique ; le plafond initial est volontairement conservateur à une heure.

Rollback immédiat : définir `USE_LEGACY_TRACKING_SCHEDULER=true` puis redémarrer. Les nouvelles colonnes SQLite sont additives et peuvent rester en place. Le mode historique ne sait toutefois pas appliquer les pauses persistées et rend les commandes de contrôle indisponibles ; il doit donc rester un rollback temporaire.
