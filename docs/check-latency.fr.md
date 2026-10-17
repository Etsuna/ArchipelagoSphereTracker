# Réactivité des checks et notifications

## Constats

L'ancienne politique automatique partait de cinq minutes par défaut, puis doublait l'intervalle après trois réponses identiques, jusqu'à une heure. Une réponse identique pouvait simplement venir du cache WebHost. Une reprise du jeu attendait donc la prochaine échéance déjà ralentie. Le jitter ajoutait jusqu'à une minute.

L'[API WebHost](https://github.com/ArchipelagoMW/Archipelago/blob/main/WebHostLib/api/tracker.py) cache le tracker dynamique pendant 60 secondes et le statique pendant 300 secondes. Le tracker lit le multisave, dont les mises à jour ajoutent leur propre délai : ce n'est pas un flux temps réel.

[Cheese Tracker](https://github.com/cdhowie/cheese-trackers/blob/master/server/src/api/tracker.rs) appelle la synchronisation à la consultation. Son [état serveur](https://github.com/cdhowie/cheese-trackers/blob/master/server/src/state.rs) mutualise les synchronisations concurrentes par tracker et impose un intervalle minimal configurable. Cela ne prouve pas un délai précis sur l'instance publique. AST doit aussi surveiller les objets lorsque personne ne consulte son portail, pour ses notifications Discord.

## Politique actuelle

À la création, `add-url` propose `polling-mode` (automatique par défaut) et une fréquence facultative utilisée uniquement en mode fixe, cinq minutes par défaut. Les options Discord sont lues par nom. Le portail masque et désactive la fréquence en automatique ; l'assistant propose un choix automatique puis huit fréquences fixes. Le mode et la fréquence sont enregistrés dans la même écriture SQL. Le mode Big Async conserve son exception : suivi fixe à une heure. `update-frequency-check` enregistre la fréquence du mode fixe sans changer le mode de la room.

- Mode automatique : une minute de base, y compris pour les anciennes rooms configurées à cinq minutes ou davantage. La fréquence configurée continue de déterminer le mode fixe.
- Activité ou connexion dans les quinze dernières minutes : cadence de base, même si le hash reste identique.
- Timers absents, vides ou illisibles : activité inconnue, cadence de base.
- Timers valides anciens et trois succès identiques : ralentissement progressif à deux, quatre puis cinq minutes au maximum. Un changement de contenu ou une activité récente rétablit la cadence de base.
- Le plafond automatique de cinq minutes s'applique aussi aux anciens plafonds plus élevés. Les anciennes échéances saines devenues hors limites sont raccourcies au chargement ; pauses et échéances de backoff sont préservées.
- Jitter positif maximal de cinq secondes. L'intervalle commence après la fin du traitement de la room : les temps HTTP, SQLite, Discord et la file restent à ajouter.

Les lectures simultanées de la même URL et du même type d'endpoint sont mutualisées. Les réponses valides sont réutilisables pendant cinq secondes (room status et runtime) ou cinq minutes (statique), dans un cache limité à 128 Mio. Aucune erreur n'est mise en cache. Annuler un consommateur n'annule pas la lecture attendue par un autre ; la requête partagée conserve son timeout HTTP. Les origines, trackers et préfixes d'installation sont séparés par l'URL complète. Seule la réponse amont est partagée : chaque salon applique sa persistance et ses notifications.

Les objets sont traités avant les annonces de fin de jeu. Les limites globales et par origine, `Retry-After`, les circuits de protection et la déduplication existante restent appliqués. Cette évolution concerne le scheduler central, activé par défaut ; le scheduler historique conserve sa politique historique.

## Limites et exploitation

La minute est une cadence cible, pas une garantie de livraison en une minute. Le délai total comprend la sauvegarde Archipelago, le cache amont, l'attente du prochain poll, la file et l'envoi Discord. Une room inactive peut attendre jusqu'à cinq minutes supplémentaires avant la détection de sa reprise. Le mode fixe peut être volontairement plus lent.

Pour 75 rooms toutes actives et distinctes, le chemin normal demande environ 150 lectures HTTP par minute (room status et runtime), avant erreurs, durée de traitement et mutualisation. Les slots influent surtout sur le volume JSON, SQLite et les messages Discord. Surveiller le retard de file, la latence, les 429 et la fraîcheur via les métriques et `/ast-room-health` après déploiement. Aucun benchmark de production n'a été réalisé.

Le portail demande maintenant une synchronisation à la consultation si la dernière tentative remonte à une minute ou davantage. Les lecteurs d'une room partagent un budget durable d'une demande par minute ; les pauses, backoffs et circuits de protection sont respectés. La synchronisation est placée dans la file du scheduler central : la page affiche immédiatement les données disponibles, puis les relit après cinq secondes jusqu'à quatre fois pendant l'attente. Ensuite elle reprend une cadence d'une minute. Les lectures périodiques sont suspendues lorsque l'onglet est masqué et reprennent lorsqu'il redevient visible. Un lien révoqué ou expiré arrête les lectures périodiques.

Le portail affiche `LastSuccessAtUtc`, la dernière synchronisation réellement achevée, plutôt que l'heure de génération de sa réponse SQL. Il distingue attente, pause, indisponibilité et erreur en conservant les dernières données disponibles. Une synchronisation terminée pendant la construction de la réponse déclenche une relecture ; cette réponse conserve prudemment la date connue avant ses lectures SQL. Le champ historique `lastUpdated` reste l'heure de génération de la réponse pour compatibilité, tandis que `tracking.lastSuccessAtUtc` fournit la fraîcheur amont.

`/ast-sync-now` reste le contrôle manuel des organisateurs. Aucun WebSocket Archipelago ni changement d'archipelago.gg n'est requis.

## Validation

Les tests couvrent les réponses identiques en activité, l'inactivité, la réaccélération sans changement de hash, le redémarrage, le mode fixe, les limites HTTP, l'annulation des consommateurs, l'isolation des URLs et les erreurs non mises en cache.

```powershell
dotnet test tests/ArchipelagoSphereTracker.Tests/ArchipelagoSphereTracker.Tests.csproj -c Release --no-restore
```
