# Data and migrations

[English](#english) · [Français](#français)

## English

### Where AST stores its data

The main storage is the `AST.db` SQLite file, created next to the executable. It contains, among other things:

- Guild IDs, Channel IDs, and room settings;
- room/tracker identifiers and patch links;
- slots associated with users and their filters;
- items, hints, game states, recaps, and exclusions;
- durable scheduler state: pauses, due times, errors, and effective intervals;
- delegated AST managers;
- portal token hashes and expiration dates;
- the security audit log.
- active room preparations: players, YAML content and validation, input snapshots, delivery receipts and reminder cooldowns.

Additional files use `extern/`, with preparation files under `data/`:

```text
AST.db
.env
extern/
├── Archipelago/          # local installation used in Archipelago mode
├── database-backups/     # automatic backups created before migration
├── portal/               # shared Web resources
├── portal-downloads/     # protected downloads
└── upload-quarantine/    # temporary uploads being validated
data/
└── room-preparations/    # preparation jobs and temporary fallback ZIPs
```

Standalone room data—active spoiler, generation-folder YAML files and protected downloads—is cleaned up with that tracked room. Global custom worlds are not. Preparations have their own shorter lifecycle below.

### Preparation storage and deletion

Startup creates `RoomPreparations`, `PreparationParticipants`, `PreparationFiles`, `PreparationRuns`, `PreparationArchiveDeliveries` and `PreparationReminders` if absent. This additive initialization uses the existing `AST.db`; it requires no extra database server and does not reset tracking history or existing check dates.

Preparation YAML content and frozen input snapshots are stored in SQLite. Job inputs/outputs use `data/room-preparations/<preparation>/<job>/`. Cancellation deletes the preparation rows with their dependent data and removes its working directory. A successful generation does the same after the ZIP or its download link has actually been posted to Discord, freeing the channel for another preparation.

When Discord cannot accept the archive, a separate ZIP copy under `data/room-preparations/downloads/` remains downloadable for 24 hours without keeping the preparation rows. Its random link grants access to anyone possessing it. Expired copies are removed at startup, hourly while the portal runs, and on attempted access. If delivery itself fails, the preparation remains available for retry.

General portal access records, technical audit logs, existing backups and tracked rooms are not deleted by preparation cleanup. SQLite row deletion is logical deletion, not a rewrite of existing backups. See [[Room preparation|Room-preparation]] for restart behavior and player permissions.

### Native V2 tracking migration

Migration selection uses one ordered registry instead of a branch per starting version. Only subsequent registered steps run; unknown or newer versions are refused before migration. The pre-migration backup and final version update are preserved. See [the migration ordering guide (French)](../docs/database-migrations.fr.md).

Migration `5.0.18` imports the latest V1 tracking rows into the native V2 tables, then drops `AliasChoicesTable`, `DisplayedItemTable`, `GameStatusTable` and `HintStatusTable` in the same transaction. Active V2 snapshots remain authoritative. Startup backs up the database first and does not recreate the old tables. Configuration, subscriptions, exclusions and personal recaps are preserved. See [the migration guide](../docs/tracking-v2-native.en.md).

### V2 snapshot storage retention

Migration `5.0.19` keeps snapshot identifiers and event history while releasing obsolete JSON bodies in small background batches. The latest snapshot of every room and all snapshots needed by unfinished deliveries stay complete. SQLite can reuse freed pages immediately; shrinking the file requires an explicit offline `VACUUM`. GC and native SQLite memory metrics are now available through Prometheus. See [the deployment and compaction guide (French)](../docs/tracking-v2-storage.fr.md).

### Datapackage index cleanup

Migration `5.0.21` removes two explicit name indexes already covered by the `UNIQUE (GuildId, ChannelId, DatasetKey, Name)` constraints on items and locations. Startup backs up the database first. Data, uniqueness, foreign keys and indexed name lookups are preserved. Freed pages become reusable; no automatic `VACUUM` runs. See [the storage guide (French)](../docs/tracking-v2-storage.fr.md).

### Intentional plaintext storage

Since SQLite migration `5.0.12`, the `Room`, `Tracker`, and `Patch` fields are stored in plaintext. No key, PEM pair, or recovery procedure is required for normal operation.

Anyone who obtains a copy of `AST.db` or a recent backup can therefore read these identifiers and links. This is intentional: they are considered shareable configuration data, and ease of maintenance takes priority.

This does not mean every secret is stored in plaintext:

- the Discord token remains in `.env` and is not copied to SQLite;
- portal tokens are generated randomly and only their SHA-256 hashes are stored;
- complete URLs and sensitive arguments are not written to the audit log.

### Migrating an older encrypted database

This section applies only to databases at version `5.0.11` or earlier that contain `astenc:v1:` values.

1. Stop the bot.
2. Temporarily keep either the old `AST_DATA_PROTECTION_KEY` variable **or** the `AST.data-protection.key` file next to the executable.
3. From the directory containing `AST.db`, run:

```powershell
.\ArchipelagoSphereTracker.exe --UpdateBDD
```

```bash
./ArchipelagoSphereTracker --UpdateBDD
```

4. AST first creates a backup in `extern/database-backups`, transactionally decrypts the old envelopes, removes the protection metadata, and leaves the fields in plaintext.
5. Start AST normally and check a room, a patch, and a newly generated portal link.
6. After your rollback period, delete the old key file and any PEM files. They are no longer used.

An incorrect key or corrupted envelope rolls back the transaction: the database remains at its previous version and the backup is not modified.

The old `--generate-data-protection-recovery-key`, `--configure-data-protection-recovery`, and `--rotate-data-protection-key` commands have been removed. If a binary ignores one of them and starts the bot, you are using a version that no longer supports these commands.

### Recommended backup procedure

For a consistent backup:

1. stop AST cleanly;
2. copy `AST.db`, `.env`, `extern/` and `data/` to protected storage;
3. keep the Discord token and any archive containing `.env` out of public repositories;
4. periodically test a restore in a separate directory.

A missing `AST.db` on first startup is not an error. AST reports that migration was skipped, initializes a new database, and then starts the bot.

### Deleting a room

A confirmed manual deletion or the cleanup performed after 14 days removes the room's records from every related table, revokes its portals, and deletes its local files. See [[Room tracking|Room-tracking]] for the complete lifecycle.

---

## Français

### Où AST stocke ses données

Le stockage principal est le fichier SQLite `AST.db`, créé à côté de l’exécutable. Il contient notamment :

- les Guild IDs, Channel IDs et paramètres des rooms ;
- les identifiants de room/tracker et les liens de patch ;
- les slots associés aux utilisateurs et leurs filtres ;
- les objets, hints, statuts de jeu, récaps et exclusions ;
- l’état durable du scheduler : pause, échéances, erreurs et intervalle effectif ;
- les responsables AST délégués ;
- les hashes et expirations des tokens de portail ;
- le journal d’audit de sécurité.
- les préparations actives : joueurs, contenu et validation des YAML, instantanés, reçus d’envoi et délais de relance.

Les fichiers complémentaires utilisent `extern/`, et les préparations utilisent aussi `data/` :

```text
AST.db
.env
extern/
├── Archipelago/          # installation locale en mode Archipelago
├── database-backups/     # sauvegardes automatiques avant migration
├── portal/               # ressources Web partagées
├── portal-downloads/     # téléchargements protégés
└── upload-quarantine/    # uploads temporaires en validation
data/
└── room-preparations/    # tâches de préparation et ZIP de secours temporaires
```

Les données autonomes d’une room suivie—spoiler actif, YAML du dossier de génération et téléchargements protégés—sont nettoyées avec cette room. Les custom worlds globaux ne le sont pas. Les préparations ont leur propre cycle de vie, plus court, décrit ci-dessous.

### Stockage et suppression des préparations

Le démarrage crée `RoomPreparations`, `PreparationParticipants`, `PreparationFiles`, `PreparationRuns`, `PreparationArchiveDeliveries` et `PreparationReminders` si elles sont absentes. Cette initialisation additive utilise `AST.db`, sans serveur de base supplémentaire ni remise à zéro de l’historique de suivi ou des dates de checks existantes.

Le contenu des YAML et les instantanés figés sont stockés dans SQLite. Les entrées/sorties des tâches utilisent `data/room-preparations/<préparation>/<tâche>/`. L’annulation supprime les lignes de préparation et leurs dépendances, ainsi que son répertoire de travail. Une génération réussie fait de même après publication effective du ZIP ou de son lien sur Discord, libérant le salon pour une nouvelle préparation.

Si Discord ne peut pas accepter l’archive, une copie distincte sous `data/room-preparations/downloads/` reste téléchargeable 24 heures sans conserver les lignes de préparation. Son lien aléatoire donne accès à toute personne qui le possède. Les copies expirées sont retirées au démarrage, toutes les heures lorsque le portail tourne et lors d’une tentative d’accès. Si la transmission échoue, la préparation reste disponible pour réessayer.

Les accès généraux au portail, audits techniques, sauvegardes existantes et rooms suivies ne sont pas supprimés par ce nettoyage. La suppression de lignes SQLite est logique et ne réécrit pas les sauvegardes existantes. Voir [[Préparation des rooms|Room-preparation]] pour la reprise après redémarrage et les droits des joueurs.

### Migration du suivi natif V2

Le choix des migrations utilise un registre ordonné unique, au lieu d'une branche par version de départ. Seules les étapes suivantes sont exécutées ; une version inconnue ou plus récente est refusée avant migration. La sauvegarde préalable et la mise à jour finale de version sont conservées. Voir [le guide d'ordonnancement](../docs/database-migrations.fr.md).

La migration `5.0.18` importe les dernières données de suivi V1 dans les tables V2, puis supprime `AliasChoicesTable`, `DisplayedItemTable`, `GameStatusTable` et `HintStatusTable` dans la même transaction. Les snapshots V2 actifs gardent la priorité. Le démarrage sauvegarde d'abord la base et ne recrée plus les anciennes tables. Les configurations, abonnements, exclusions et récapitulatifs personnels sont conservés. Voir [le guide de migration](../docs/tracking-v2-native.fr.md).

### Rétention des snapshots V2

La migration `5.0.19` conserve les identifiants des snapshots et l'historique des événements, puis libère les anciens corps JSON par petits lots en arrière-plan. Le dernier snapshot de chaque room et ceux nécessaires aux livraisons non terminées restent complets. SQLite réutilise les pages libérées ; réduire le fichier nécessite un `VACUUM` explicite, service arrêté. Les métriques du GC et des allocations SQLite sont disponibles dans Prometheus. Voir [le guide de déploiement et de compactage](../docs/tracking-v2-storage.fr.md).

### Nettoyage des index de datapackages

La migration `5.0.21` supprime deux index de noms explicites déjà couverts par les contraintes `UNIQUE (GuildId, ChannelId, DatasetKey, Name)` des objets et locations. Le démarrage sauvegarde d'abord la base. Les données, l'unicité, les clés étrangères et les recherches indexées par nom sont conservées. Les pages libérées deviennent réutilisables, sans `VACUUM` automatique. Voir [le guide de stockage](../docs/tracking-v2-storage.fr.md).

### Stockage en clair assumé

Depuis la migration SQLite `5.0.12`, les champs `Room`, `Tracker` et `Patch` sont en clair. Aucune clé, paire PEM ou procédure de récupération n’est nécessaire au fonctionnement courant.

Toute personne qui obtient une copie de `AST.db` ou d’une sauvegarde récente peut donc lire ces identifiants et liens. Le choix est volontaire : il s’agit de données de configuration considérées comme partageables, et la simplicité de maintenance est privilégiée.

Cela ne signifie pas que tous les secrets sont en clair :

- le token Discord reste dans `.env` et n’est pas copié dans SQLite ;
- les tokens de portail sont générés aléatoirement et seul leur hash SHA-256 est stocké ;
- les URLs complètes et arguments sensibles ne sont pas écrits dans l’audit.

### Migration d’une ancienne base chiffrée

Cette section ne concerne que les bases `5.0.11` ou antérieures contenant des valeurs `astenc:v1:`.

1. Arrêtez le bot.
2. Conservez temporairement l’ancienne variable `AST_DATA_PROTECTION_KEY` **ou** le fichier `AST.data-protection.key` à côté de l’exécutable.
3. Lancez depuis le dossier contenant `AST.db` :

```powershell
.\ArchipelagoSphereTracker.exe --UpdateBDD
```

```bash
./ArchipelagoSphereTracker --UpdateBDD
```

4. AST crée d’abord une sauvegarde dans `extern/database-backups`, déchiffre transactionnellement les anciennes enveloppes, retire les métadonnées de protection et laisse les champs en clair.
5. Démarrez normalement et vérifiez une room, un patch et un lien de portail neuf.
6. Après votre période de rollback, supprimez l’ancien fichier de clé et les éventuels PEM. Ils ne servent plus.

Une mauvaise clé ou une enveloppe corrompue annule la transaction : la base reste à son ancienne version et la sauvegarde n’est pas altérée.

Les anciennes commandes `--generate-data-protection-recovery-key`, `--configure-data-protection-recovery` et `--rotate-data-protection-key` ont été retirées. Si un binaire les ignore et démarre le bot, vous utilisez une version qui ne les prend plus en charge.

### Sauvegarde conseillée

Pour une sauvegarde cohérente :

1. arrêtez AST proprement ;
2. copiez `AST.db`, `.env`, `extern/` et `data/` vers un stockage protégé ;
3. conservez le token Discord et les archives contenant `.env` hors d’un dépôt public ;
4. testez périodiquement une restauration dans un dossier séparé.

L’absence de `AST.db` au premier démarrage n’est pas une erreur. AST affiche que la migration est ignorée, initialise une nouvelle base, puis démarre le bot.

### Suppression d’une room

Une suppression manuelle confirmée ou le nettoyage après 14 jours retire les enregistrements de la room dans toutes les tables concernées, révoque ses portails et supprime ses fichiers locaux. Voir [[Suivi des rooms|Room-tracking]] pour le cycle de vie complet.
