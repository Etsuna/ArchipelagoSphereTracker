# Mémoire et notifications dans Grafana

Le dashboard AST comporte deux nouvelles sections : **Memory & SQLite** et
**Notification backlog & collector**. Les 61 panneaux existants sont conservés.
Les 15 nouveaux panneaux utilisent uniquement des métriques déjà exposées par le
bot corrigé : aucun redéploiement supplémentaire du bot n'est nécessaire.

## Importer la version préparée

Le fichier `artifacts/Grafana-AST-memory-v2.json` est un dashboard complet au même
format `dashboard.grafana.app/v2` que l'export fourni. Il conserve les panneaux,
filtres, référence de source Prometheus et identité de cet export.

Dans Grafana, utiliser **Dashboards → New → Import**, puis charger ce JSON et
vérifier le dashboard de destination.

Pour l'éditeur **JSON Model** qui attend `annotations`, `cursorSync`, `elements`,
`layout`, `title`, etc. directement à la racine, utiliser plutôt
`artifacts/Grafana-AST-memory-editor.json`. Ce fichier contient uniquement le
contenu de `spec`, avec les mêmes 76 panneaux issus de l'export utilisateur.
Ne pas coller l'enveloppe `apiVersion`/`metadata`/`spec` dans cet éditeur : elle
provoquerait les erreurs « Missing property » sur les champs du dashboard.

Le fichier `wiki/Grafana-Dashboard.json` contient la version de référence du dépôt
(son contenu `spec`, sans l'enveloppe de métadonnées de l'instance Grafana).

Sélectionner le job et l'instance avec **AST job (health panels)** et
**AST instance (health panels)**. Ces choix filtrent les deux nouvelles sections ;
les anciens panneaux conservent leurs filtres guilde/room/joueur. Les options
proviennent d'`ast_info` pour distinguer les instances AST des autres applications
supervisées. L'option All regroupe les valeurs trouvées, sans ouvrir le filtre à
tous les jobs Prometheus.

## Mesures affichées

| Panneau | Mesure / interprétation |
| --- | --- |
| Process resident memory (RSS) | `process_working_set_bytes`, RAM résidente du processus |
| Managed objects (estimate) | `dotnet_total_memory_bytes`, estimation des objets .NET alloués |
| GC committed memory | `ast_gc_committed_bytes`, mémoire engagée au dernier GC |
| SQLite native allocations | `ast_sqlite_memory_bytes`, allocations suivies par SQLite |
| Memory over time | Comparaison des quatre mesures et de `ast_gc_fragmented_bytes` |
| GC collections per minute | `rate(dotnet_collection_count_total[...]) * 60`, par génération |
| Process RSS growth | Tendance linéaire de la RAM sur 30 minutes, en Mio/heure |
| Pending / failed / in progress | `ast_event_deliveries`, par état (`pending`, `failed`, `delivering`) |
| Database metrics age | Temps depuis `ast_metrics_collection_last_success_timestamp_seconds` |
| Unfinished deliveries over time | Historique des états non terminés, sans le cumul des livraisons terminées |
| Process uptime | Temps depuis `process_start_time_seconds` |
| Backlog net growth | Tendance nette de la file sur 30 minutes, en événements/minute |

Le panneau de tendance de la file utilise `deriv()` sur les gauges : négatif =
rattrapage net, positif = accumulation. Il ne donne **pas** le débit réel d'envoi.
Les nouveaux événements et les suppressions de rooms influencent cette pente.
Un `rate()` sur le nombre de livraisons terminées serait trompeur, car cette
métrique est une gauge du contenu de la base, pas un compteur monotone.

## Lire les résultats correctement

- Les courbes mémoire **ne doivent pas être additionnées** : elles se recouvrent
  ou utilisent des comptabilités différentes.
- Le RSS n'est pas la consommation totale du service systemd : le cache de
  fichiers et les autres charges du cgroup nécessitent un exporteur système
  distinct. Ne pas comparer directement les 667 Mio RSS et les 2,5 Go du service.
- Les mesures du GC correspondent à sa dernière collecte ; elles peuvent rester
  constantes entre deux collectes.
- Les métriques mémoire sont mises à jour à chaque scrape Prometheus, mais les
  gauges SQL (livraisons, rooms...) sont actualisées par AST **environ toutes les
  cinq minutes**. Augmenter le rafraîchissement Grafana ne change pas ce délai.
- Une métrique absente ou indisponible reste **No data**, pas un zéro artificiel.
- La taille de la base, les pages libres et le nombre de snapshots purgés ne sont
  pas encore exposés par le bot. Aucun panneau n'invente ces métriques.
- Le JSON contient des suggestions de seuils, sans créer de règles d'alerte.
  Configurer les alertes Grafana séparément, avec des seuils adaptés à la charge.

Les JSON sont contrôlés pour les identifiants uniques, les références de panneaux,
l'absence de chevauchement, la préservation des panneaux existants et les noms de
métriques. Le rendu et l'exécution des requêtes doivent être vérifiés après import
dans l'instance Grafana réelle.
