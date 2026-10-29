# Correctif de croissance des snapshots V2 (base 5.0.19)

La V2 enregistrait un état JSON complet à chaque changement, y compris les variations
d'activité. Les événements référençaient ces snapshots sans limite de rétention.
La taille de l'historique pouvait donc croître de plusieurs Gio, même lorsque le
nombre de rooms restait stable.

## Comportement du correctif

- Le dernier snapshot de **chaque** room reste complet (suivi, commandes et replay).
- Tous les snapshots nécessaires aux livraisons `Pending`, `Failed` ou `Delivering`
  restent complets, quelle que soit leur ancienneté ou leur destination.
- Les autres anciens corps JSON sont vidés et signalés par `PayloadPruned=1`.
  Les identifiants, empreintes, métadonnées, événements, clés de déduplication,
  livraisons et lots de notifications sont conservés.
- Un worker commence le rattrapage au lancement du suivi, puis repasse chaque minute.
  Il valide huit lignes à la fois et libère le verrou d'écriture entre les lots.
  La décision et la purge sont dans la même transaction que celle utilisée pour
  sérialiser les écritures du suivi et les prises en charge des notifications.
- La migration ajoute seulement une colonne et des index : elle ne réécrit pas
  d'un coup les anciens corps JSON et peut être relancée sans perte de données.
- Le cache des connexions SQLite de lecture est ramené à 4 Mio, et chaque pool
  conserve au maximum dix connexions inactives. Ce plafond ne limite pas le nombre
  de connexions simultanément ouvertes.
- Le `VACUUM` systématique au démarrage est retiré.

La purge borne la conservation des **gros corps JSON**, pas la taille de l'historique
des événements et de ses métadonnées. Un backlog durable de notifications bloque
volontairement la purge des snapshots correspondants. Les changements d'activité
restent enregistrés : ce correctif ne modifie pas la sémantique des empreintes ou
des événements.

## Déploiement sur le VPS

Publier et déployer la version corrigée habituelle, autonome pour `linux-x64`.
Aucune installation de .NET n'est nécessaire sur le VPS. La base et le `.env`
doivent être conservés. Au premier démarrage, AST sauvegarde la base avant la
migration 5.0.19 : prévoir l'espace disque nécessaire pour cette copie.

Le rattrapage s'exécute en arrière-plan. Une fois terminé, le journal contient :

```text
[TrackingStorage] Released ... historical snapshot payloads; events and deliveries retained.
```

Pour vérifier l'avancement sans lire les gros corps JSON :

```bash
sqlite3 -readonly -header -column /home/ubuntu/AST/AST.db \
  'SELECT PayloadPruned, COUNT(*) AS snapshots FROM RoomSnapshots GROUP BY PayloadPruned;'
```

`PayloadPruned=1` désigne les corps libérés. Le fichier `.db` peut encore mesurer
8,8 Gio : les pages libérées sont d'abord réutilisées pour les écritures suivantes.

## Réduire physiquement le fichier après le rattrapage

Effectuer ce compactage pendant une maintenance, avec une sauvegarde vérifiée
(celle créée avant la migration convient). `VACUUM` peut nécessiter de l'espace
temporaire supplémentaire, jusqu'à deux fois la taille du fichier avant compactage.
Vérifier l'espace disponible avant de lancer cette opération.

```bash
df -h /home/ubuntu/AST
sudo systemctl stop AST.service
systemctl is-active AST.service
```

Continuer seulement lorsque le service est arrêté (`inactive`) :

```bash
sqlite3 -bail /home/ubuntu/AST/AST.db \
  'PRAGMA wal_checkpoint(TRUNCATE); VACUUM; PRAGMA wal_checkpoint(TRUNCATE); PRAGMA quick_check;'
```

Le contrôle doit renvoyer `ok`. Redémarrer ensuite :

```bash
sudo systemctl start AST.service
ls -lh /home/ubuntu/AST/AST.db*
```

Ne pas supprimer les fichiers `-wal`/`-shm` et ne pas exécuter de `DELETE` des
snapshots : leurs clés étrangères entraînent la suppression des événements associés.
Ne pas revenir à un ancien binaire après migration sans restaurer la sauvegarde
correspondante, car les anciennes versions ne connaissent pas la version 5.0.19.

## Mesurer la mémoire sans outil à installer

L'export Prometheus fournit désormais, à chaque collecte :

- `ast_gc_committed_bytes` : mémoire engagée du GC lors de sa dernière collecte ;
- `ast_gc_fragmented_bytes` : fragmentation lors de sa dernière collecte ;
- `ast_sqlite_memory_bytes` : allocations natives suivies par SQLite, hors cache
  de fichiers du système (`NaN` si le compteur n'est pas disponible).

Comparer ces valeurs à `process_working_set_bytes` et `dotnet_total_memory_bytes`.
Elles ne forment pas une décomposition exacte de la RAM résidente : mémoire engagée,
résidente et allocations suivies sont des mesures différentes. La réduction du
stockage ne garantit pas une réduction proportionnelle de la RAM.
