# Optimisations du stockage V2 (base 5.0.21)

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

### Suppression des index de noms redondants (5.0.21)

Les tables `DatapackageItems` et `DatapackageLocations` possèdent une contrainte
`UNIQUE (GuildId, ChannelId, DatasetKey, Name)`. Son index automatique couvre déjà
les recherches par nom ; les index explicites `IX_DatapackageItems_GCD_Name` et
`IX_DatapackageLocations_GCD_Name` répétaient les mêmes colonnes.

La migration **5.0.21** vérifie les contraintes équivalentes puis supprime seulement
ces deux index supplémentaires dans une transaction. Les données, les index de
clé primaire, les contraintes d'unicité et les clés étrangères sont conservés.
La création d'une nouvelle base et les redémarrages ne recréent plus ces doublons.

Déployer le nouveau binaire et démarrer normalement : le démarrage crée une
sauvegarde préalable dans `extern/database-backups/`, puis applique la migration.
On peut aussi exécuter `--updatebdd`, bot arrêté, avant le démarrage normal.
Conserver la sauvegarde pour un éventuel retour à un ancien binaire.

Le gain est de l'espace réutilisable dans SQLite et deux index de moins à maintenir
lors des écritures de datapackages. Dans les anciens relevés du VPS, ces deux
index représentaient environ 314 Mio ; le gain actuel dépend de leur taille au
moment de la migration. Le fichier `.db` ne rétrécit pas nécessairement : les
pages deviennent réutilisables, sans `VACUUM` automatique. Il ne s'agit pas d'une
économie équivalente garantie en RAM.

### Mises à jour ciblées de l'inventaire

Après une première observation complète, `TrackingItems` est mis à jour par
différence avec le snapshot précédent : insertion des nouveaux objets, mise à
jour des champs affichés modifiés, suppression des identités disparues. Les
objets inchangés ne sont pas réécrits. Les deux inventaires normalisés étant déjà
triés par identité, la comparaison se fait en un passage sans recopier les
inventaires dans des dictionnaires. Les commandes SQL sont réutilisées.

Les changements des noms canoniques des joueurs mettent à jour les lignes
concernées, sans nouvelle notification d'objet. Une section `Items` absente garde
l'inventaire précédent ; une section complète et vide le supprime réellement.
L'initialisation, la bascule vers les notifications et les anciens inventaires
incomplets conservent un remplacement complet pour éliminer les lignes provisoires.

Les écritures restent dans la même transaction que le snapshot, les événements,
les récaps et les livraisons. Il n'y a pas de changement de schéma ni de requête
Archipelago supplémentaire. Les hints et les états des joueurs gardent leur
fonctionnement actuel.

Un test avec 10 000 objets puis un ajout compte les écritures via des triggers
SQLite : une insertion et aucune mise à jour/suppression, au lieu de supprimer
10 000 lignes puis d'en insérer 10 001. Cette mesure concerne la projection
`TrackingItems`, pas les autres écritures du poll ni son temps total.

### Calcul de l'empreinte pendant la persistance

`TrackingV2Store` calcule l'empreinte une seule fois après la consolidation des
sections partielles et l'éventuelle bascule vers la première référence de
notifications. La comparaison avec l'état courant, l'insertion du snapshot et la
mise à jour de la room réutilisent cette même valeur locale. Pour un changement
sur une room déjà initialisée, cela remplace trois sérialisations et calculs
SHA-256 par un seul ; pour une première référence, deux deviennent un. Un état
inchangé nécessitait déjà un seul calcul.

Le format de l'empreinte et les règles de détection restent identiques. Il n'y a
pas de cache global ni de changement de schéma. Ce gain concerne le calcul de
l'empreinte dans la persistance, pas l'ensemble du traitement d'un poll.

Une mesure locale en Release sur un snapshot synthétique de 5 000 objets,
30 itérations et la médiane de trois passages compare les trois calculs à leur
réutilisation : environ 301 ms et 147,6 Mio alloués cumulativement avant, contre
95 ms et 49,2 Mio après. Ces allocations cumulées ne représentent pas la RAM
résidente. La mesure isole le hachage et exclut SQLite, le diff et les appels
réseau ; elle ne prédit pas le gain global sur le VPS.

### Chargement des snapshots pour les notifications

Le worker `DiscordNotificationV2` prend en charge une livraison avec les métadonnées
du snapshot, sans lire son gros corps JSON. Il cherche d'abord le lot de messages
déjà enregistré dans `DiscordNotificationBatches`. Un lot existant suffit pour
reprendre ses pages et ses reçus ; seul un lot absent charge et désérialise le
snapshot historique pour préparer les messages.

Les identités de l'événement, du snapshot et de la destination restent vérifiées.
Le chargement différé vérifie aussi le numéro de tentative courant. La livraison
non terminée protège toujours son snapshot contre la purge. Les lots restent en
SQLite, sans cache permanent en RAM ; les réémissions conservent leurs clés propres.
Le polling Archipelago, la détection des changements et le contenu des messages
restent inchangés. Les autres types de publishers gardent leur snapshot préchargé.

Ce complément ne modifie pas le schéma : déployer le nouveau binaire et redémarrer
normalement suffit, sans `--updatebdd` ni `VACUUM` supplémentaire.

Les tests utilisent un snapshot de plus de 1 Mio et observent les accès SQL à sa
colonne `PayloadJson` : 20 ou 100 livraisons partageant le même lot n'entraînent
qu'une lecture du corps, même avec un nouveau worker pour chaque livraison.
Ils couvrent aussi la reprise d'un groupe partiellement envoyé après perte de
réponse, les contrôles de portée et la protection contre la purge. Ce résultat
mesure le nombre de lectures évitées, pas un gain garanti de RAM résidente ou de
débit Discord sur le VPS.

### Complément : accélérer une file de notifications déjà en attente

La recherche de la première livraison non terminée par destination ne disposait
pas d'un index adapté. Elle pouvait reparcourir l'historique des livraisons pour
chaque candidat, tout en chargeant les corps des snapshots avant le tri final.

Le correctif crée un index partiel sur les livraisons non terminées et sélectionne
d'abord seulement leurs métadonnées. Il charge ensuite le corps du seul snapshot
retenu, dans la même transaction. L'ordre par destination, les délais de reprise,
les leases et la déduplication ne changent pas.

L'index est créé automatiquement au démarrage normal, y compris sur une base déjà
en 5.0.19 ; ce complément ne nécessite pas une nouvelle version de schéma.
Pour améliorer immédiatement la sélection dans le binaire déjà lancé, depuis le
dossier du bot, exécuter :

```bash
sqlite3 -bail -cmd '.timeout 60000' ./AST.db "
CREATE INDEX IF NOT EXISTS idx_eventdeliveries_unfinished_destination
ON EventDeliveries(DestinationType, DestinationId, Id)
WHERE Status <> 'Delivered';
"
```

Cette opération peut brièvement attendre le verrou d'écriture. Elle ne change
aucun événement ni statut de livraison et ne nécessite pas d'arrêter le bot.
Sur un jeu synthétique local de 14 583 livraisons (9 433 livrées et 5 150 en attente,
82 snapshots de 256 Kio), la sélection initiale prenait environ 3,4 secondes ;
l'index seul l'a ramenée à environ 4 ms, et la sélection des seules métadonnées à
environ 3 ms. Ce résultat n'est pas une mesure du débit Discord sur le VPS.

Comparer `COUNT(*)` par statut à quelques minutes d'intervalle pour mesurer le
rattrapage réel. Ne pas passer manuellement les livraisons à `Delivered` pour
débloquer la purge : cela ferait abandonner leurs notifications.

### Migration et démarrage

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
