# PR 3 — Snapshots et événements normalisés V2

## Problème

Le pipeline historique mélange parsing WebHost, comparaison, SQLite, rendu Discord et livraison. Ses identités reposent souvent sur des noms affichés, donc un alias ou une traduction peut modifier artificiellement l'identité d'un item ou d'un hint.

Cette PR a introduit un noyau pur sans modifier les notifications de production. La PR 4 peut désormais l'alimenter en dual-write lorsque `ENABLE_TRACKING_V2=true`, toujours sans publication Discord V2.

## Architecture

Le module `ArchipelagoSphereTracker.Tracking.V2` contient :

- `NormalizedRoomSnapshot`, snapshot immuable et trié ;
- les valeurs normalisées des slots, transferts, hints, checks, objectifs et statuts ;
- `TrackingSnapshotDiff.Diff(previous, current)`, fonction pure sans SQLite, réseau ou Discord ;
- les dix événements P0 : `ItemReceived`, `ItemSent`, `HintCreated`, `HintUpdated`, `GoalReached`, `PlayerStatusChanged`, `CheckCompleted`, `RoomActivityChanged`, `TrackingError` et `TrackingRecovered` ;
- `LegacySnapshotAdapter`, adaptateur temporaire depuis `ProcessingContext` et le JSON WebHost actuel.

Les modèles historiques `DisplayedItem`, `HintStatus` et `GameStatus` conservent maintenant les IDs bruts nécessaires. Le comportement historique continue d'utiliser leurs champs d'affichage comme auparavant.

## Normalisation et complétude

Toutes les collections sont dédupliquées et triées avant calcul du hash. `CapturedAtUtc` et `LastSuccessfulSyncUtc` sont conservés dans le snapshot mais exclus du hash de contenu : une nouvelle collecte identique ne crée donc pas un nouveau contenu métier.

`SnapshotSections` indique séparément si les slots, items, hints, checks, objectifs, statuts, activité et état du suivi sont complets. Une section absente ou `null` n'est pas assimilée à une collection vide. Le diff ignore toute section incomplète ; une réponse partielle ne peut donc pas produire un faux événement destructif.

Le premier snapshot est une baseline silencieuse. Aucun historique n'est publié lorsque `previous` vaut `null`.

## Identités et clés stables

Les identités utilisent les IDs WebHost :

- item : finder slot, receiver slot, item ID et location ID ;
- hint : finder slot, receiver slot, item ID, location ID et entrance brute ;
- check : slot et location ID ;
- objectif/statut : slot et identifiant ou transition.

Les alias, noms localisés et textes Discord ne participent jamais aux clés. Chaque événement conserve `OccurredAtUtc`. Chaque `EventKey` est un SHA-256 hexadécimal du type d'événement, de la guilde, du salon et de son identité canonique. Les événements intrinsèquement uniques (item, check, objectif) restent indépendants de l'heure ; les transitions répétables (statut, erreur/rétablissement, mise à jour d'un hint) incluent l'heure d'observation. Le même diff produit donc exactement les mêmes clés sans confondre deux incidents successifs identiques.

L'ordre des tuples `Hint` suit le contrat officiel `(receiving_player, finding_player, location, item, found, entrance, item_flags, status)`. Les objectifs reposent sur `player_status = CLIENT_GOAL`, pas sur une estimation fondée sur le nombre de checks. Références : [API tracker WebHost](https://github.com/ArchipelagoMW/Archipelago/blob/main/WebHostLib/api/tracker.py) et [type `Hint`](https://github.com/ArchipelagoMW/Archipelago/blob/main/NetUtils.py).

## Fichiers principaux

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

Les tests couvrent l'ordre JSON, les champs inconnus ou `null`, les alias manquants, les IDs bruts, la déduplication, le hash canonique, la baseline, tous les types d'événements, les transitions erreur/rétablissement et la stabilité des clés malgré des noms traduits.

## Risques et rollback

- L'API WebHost expose actuellement une structure essentiellement mono-équipe ; le modèle conserve les IDs de slot utilisés par AST et l'ajout explicite du team ID restera nécessaire si le WebHost généralise plusieurs équipes.
- L'adaptateur reste transitoire ; la persistance V2 l'utilise désormais sans dupliquer la logique de diff.
- Le schéma SQLite et l'outbox sont décrits dans `tracking-v2-persistence.fr.md`.
- Rollback fonctionnel : laisser `ENABLE_TRACKING_V2=false`. Le pipeline historique et ses tables restent intacts.
