# Notifications Discord suspendues et reprise sans perte (base 5.0.22)

Le suivi doit conserver chaque notification jusqu'à son envoi. Un salon auquel
Discord refuse l'accès passe en suspension : ses notifications sortent de la
file active et restent en SQLite, sans limite de durée et sans abandon automatique.
La politique concerne uniquement `DiscordNotificationV2`, par couple guilde/salon.

## Suspension

- Trois refus d'accès **consécutifs** suspendent les envois : Discord
  `403/50001` (Missing Access), `403/50013` (Missing Permissions),
  `404/10003` (Unknown Channel), ou l'absence constatée d'une permission nécessaire
  dans le salon/fil. Les timeouts, erreurs réseau, HTTP 429 et HTTP 5xx ne
  déclenchent pas cette suspension. Avant confirmation, un succès ou une autre
  erreur remet le compteur de refus à zéro.
- Les notifications déjà en attente passent de `Pending`/`Failed` à `Suspended`.
  Les nouveaux objets, indices et demandes de replay continuent d'être enregistrés ;
  leurs notifications sont directement créées en `Suspended`.
- Les objets et récaps continuent à se mettre à jour. Une room terminée n'est
  pas automatiquement supprimée pendant la suspension. Les autres salons
  continuent de recevoir leurs notifications normalement.
- Une tentative ayant encore un bail valide peut terminer et être confirmée.
  En cas de crash, son bail expiré permet de la placer également en suspension.
  Identifiants, tentatives, lots figés, pages et reçus sont conservés.

Un ancien `AttemptCount=226` ne remplace pas trois refus d'accès confirmés avec
cette version. La suspension peut donc nécessiter trois nouvelles tentatives,
avec le backoff existant.

## Vérification et reprise

La première vérification est programmée 15 minutes après la suspension, puis
15 minutes après chaque vérification échouée. Le worker d'entretien vérifie les
échéances chaque minute, indépendamment du worker d'envoi.

La vérification consulte le salon, les permissions nécessaires (voir le salon,
lire l'historique, envoyer des messages ou envoyer dans les fils, verrouillage
du fil) et demande un seul message d'historique via REST. Aucun message de test
n'est publié. Il s'agit de vérifier l'accès à **ce salon**, pas uniquement que
la connexion globale du bot à Discord fonctionne.

Lorsque l'accès revient, une transaction remet toutes les livraisons suspendues
en `Pending` et enlève la suspension du salon. Le worker reprend par identifiant,
du plus ancien au plus récent, sans sauter les notifications de plusieurs jours
ou semaines. Les reçus des pages déjà confirmées évitent de les renvoyer ; pour
un envoi dont le résultat est incertain, le mécanisme habituel de récupération
dans l'historique reste utilisé.

L'état et les baux survivent aux redémarrages. Un résultat de vérification devenu
obsolète ne peut pas lever une suspension plus récente. La reprise reste soumise
aux permissions et aux réponses Discord : si les droits changent à nouveau,
les notifications sont conservées pour une nouvelle tentative.

Supprimer explicitement le suivi d'un salon conserve son comportement habituel
et supprime ses données associées, y compris la suspension.

## RAM et stockage

La file est dans SQLite : elle n'est pas chargée entièrement en RAM. La sélection
des livraisons lit d'abord leurs métadonnées ; un salon suspendu ne provoque pas
de lecture répétée de ses gros snapshots ni de tentative d'envoi à chaque tour.
La vérification d'accès ne charge pas les corps des notifications.

Cette suspension réduit les tentatives, les requêtes et les allocations inutiles,
mais changer un état SQL ne garantit pas une grosse baisse du RSS. Les snapshots
nécessaires à une notification suspendue restent protégés par la rétention,
comme ceux d'une livraison `Pending`, `Failed` ou `Delivering`. Le dernier snapshot,
les événements, récaps et reçus sont conservés. Une longue indisponibilité peut
donc augmenter la taille de la base sur disque. Il n'y a aucune expiration des
notifications destinée à faire baisser artificiellement la file ou le stockage.

## Déploiement

Arrêter le service, déployer le nouveau binaire, puis démarrer normalement.
Le mécanisme de migration crée d'abord une sauvegarde dans
`extern/database-backups/`. `--updatebdd` peut également être exécuté service
arrêté, avant le démarrage normal.

La migration **5.0.22** reconstruit transactionnellement `EventDeliveries` pour
ajouter `Suspended` aux états autorisés, puis crée `NotificationDeliveryStates`
et adapte l'index des livraisons non terminées. Les identifiants, livraisons,
tentatives, dates de reprise, baux, reçus, unicité et clés étrangères sont
conservés. Des colonnes personnalisées inconnues ou une table tierce référençant
les identifiants de livraison provoquent un arrêt explicite avant reconstruction.
Conserver la sauvegarde pour revenir à un ancien binaire. Aucun `VACUUM` n'est lancé.

## Vérification

Depuis le répertoire contenant `AST.db` :

```bash
sqlite3 -readonly -header -column ./AST.db "
SELECT Version FROM BddVersion;
SELECT Status, COUNT(*) AS livraisons FROM EventDeliveries GROUP BY Status;
SELECT GuildId, ChannelId, ConsecutiveAccessFailures, LastErrorCode,
       SuspendedAtUtc, NextAccessCheckAtUtc, LastProbeErrorCode
FROM NotificationDeliveryStates;
"
```

Les métriques SQL se rafraîchissent au rythme habituel du collecteur
(environ cinq minutes). Les notifications suspendues disposent de leur propre
état ; elles ne sont comptées ni comme `pending`, ni comme `delivered` :

```promql
ast_event_deliveries{state="suspended"}
```

```promql
ast_notification_destinations_suspended
```

Pour le total **non terminé**, utiliser `state=~"pending|delivering|failed|suspended"`.
Un déplacement de `pending` vers `suspended` ne constitue pas une livraison.
Les logs signalent la suspension et le retour de l'accès ; le dashboard de
référence conserve les suspensions dans l'historique du backlog.
