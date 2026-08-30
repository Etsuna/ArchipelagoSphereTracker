# PR 4 — Persistance V2, ledger idempotent et outbox

## Résultat

La version de base passe à `5.0.7`. La migration conserve toutes les tables V1 et ajoute `TrackedRooms`, `RoomSnapshots`, `TrackingEvents` et `EventDeliveries`. Avant de créer les contraintes uniques V1 indispensables, elle conserve déterministement la ligne `ChannelsAndUrlsTable` au plus grand `Id`, lui rattache les patches encore utiles, et conserve le hint au plus grand `Id` pour chaque identité complète incluant `Entrance`.

`Program.CheckBdd` crée toujours une sauvegarde SQLite avant toute migration de version. `Migrate_5_0_7` est transactionnelle et idempotente.

## Transaction et baseline

`TrackingV2Store.ApplySnapshotAsync` exécute sous un unique `BEGIN IMMEDIATE` :

1. lecture du dernier snapshot connu ;
2. consolidation des sections absentes avec le dernier état complet ;
3. insertion du snapshot ;
4. calcul et insertion idempotente des événements ;
5. création d'une livraison par couple événement/destination ;
6. déplacement atomique du pointeur logique de la room.

Une exception à n'importe quelle étape annule toute la transaction. Le premier snapshot d'une room devient une baseline silencieuse : aucun événement et aucune livraison historique ne sont créés. Un contenu identique met seulement à jour l'heure de synchronisation et n'ajoute pas un snapshot.

## Livraison rejouable

`TrackingDeliveryWorker` réclame une livraison avec un lease, incrémente son compteur de tentatives, puis la marque `Delivered` ou `Failed`. Une livraison `Delivering` dont le lease expire redevient réclamable. Les échecs utilisent un backoff exponentiel borné.

Le contrat `ITrackingEventPublisher` impose l'usage de `EventKey` comme clé d'idempotence. La sémantique de l'outbox est donc at-least-once au niveau transport et exactement une publication logique si l'éditeur respecte ce contrat. C'est indispensable pour la frontière « publication externe réussie, accusé SQLite interrompu ».

Cette PR ne branche volontairement aucun éditeur Discord V2. Avec `ENABLE_TRACKING_V2=true`, le scheduler historique effectue seulement le dual-write et remplit l'outbox ; les notifications Discord continuent de provenir exclusivement du pipeline V1.

## Exploitation et rollback

- activation expérimentale : `ENABLE_TRACKING_V2=true` ;
- rollback immédiat : `ENABLE_TRACKING_V2=false` puis redémarrage ;
- les tables V1 ne sont ni supprimées ni renommées ;
- les lignes V2 déjà écrites peuvent rester en place pour diagnostic ou rejeu ultérieur ;
- aucun token, URL de room ou secret Discord n'est stocké dans les payloads V2.

## Validation

La suite `TrackingV2PersistenceTests` couvre la baseline, la déduplication, les réponses partielles, douze écritures concurrentes, le rollback après snapshot/événement/livraison, la migration exécutée deux fois et la reprise après une panne simulée entre publication et accusé.

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet test ArchipelagoSphereTracker.sln --configuration Release --no-restore
```
