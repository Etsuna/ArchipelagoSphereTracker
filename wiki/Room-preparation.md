# Room preparation / Préparation des rooms

[English](#english) · [Français](#français)

## English

Room preparation is available only in **Archipelago mode**. Players register, upload their own YAML files and mark themselves ready before the organizer starts generation. Discord commands and the Web page use the same preparation data.

### Create a preparation

Run `/organize-room` in the desired guild text channel and choose a name. The command posts a persistent message with buttons **in the current channel**; it does not create another thread. An existing thread can also host the preparation. Its existing permissions determine who can see the message and the eventual ZIP.

Creation is available to guild managers recognized by AST, provided they have access to Archipelago tools. The `/ast` home screen and administration portal also provide creation controls. New preparations allow **200 players and 200 total slots**, with no capacity field to fill in. There can be one preparation per channel at a time. The organizer need not register as a player.

### Register and upload YAML files

1. Select **Register** on the message or preparation page.
2. Upload your files in that same channel using any of these interfaces:

   ```text
   /send-yaml file:<player.yaml>
   /ast file:<player.yaml>
   /organize-room file:<player.yaml>
   ```

   The preparation page also provides **Upload or replace my YAML files**, with multiple selection.
3. Wait for Archipelago validation, then select **Ready**.

The receipt identifies the file added to the preparation. Replacing a file with the same name replaces your own copy; another player's file with that name is separate. Replacing or removing a file resets your readiness. You can withdraw before generation starts.

To upload a batch, select several YAML files on the page, or send a ZIP using `/send-yaml file:<players.zip>`, `/ast file:<players.zip>` or `/organize-room file:<players.zip>` in the preparation channel. The ZIP imports files without generating a seed. Safe subfolders are accepted and flattened to filenames; duplicate filenames, links, unsafe paths and non-YAML entries are rejected. A batch contains at most 20 YAML files; the ZIP and its uncompressed content are each limited to 16 MiB, with 1 MiB per YAML. Every imported file belongs to the uploading player.

The whole batch is checked for filenames, YAML structure and quotas before any existing file is replaced. A rejected batch leaves existing files and readiness unchanged. Accepted files are then validated separately by Archipelago, so a game-specific error affects that file's result rather than hiding the other files' results.

The player table shows slots, games, overall YAML validation and readiness. **My YAML files** shows your actual filenames and personal validation errors. Weighted games and names such as `Player{number}` are indicative until Archipelago resolves them during generation.

**Pending** means the file is waiting for its validation turn; **Archipelago validation in progress** means its process has started. A failed file displays **Archipelago validation failed**, the generator diagnostic and correction advice. A missing or broken APWorld is reported as an unavailable game when Archipelago identifies that error. AST closes interactive input and stops on fatal player-file errors rather than waiting for a keyboard confirmation. After fixing the YAML or installing the correct APWorld, upload the file again. Readiness stays blocked until all your files validate successfully.

| Limit | Value |
|---|---|
| Players / total slots | 200 / 200 |
| Files per player | 20 |
| Documents per file | 1–20 |
| File size and encoding | 1 MiB, UTF-8, `.yaml` or `.yml` |
| Total YAML size per preparation | 16 MiB |

Every player must have at least one valid file to mark themselves ready. Players may manage only their own YAML files. Other players can see slot/game metadata and readiness, but cannot download another player's YAML or read their personal diagnostics.

Outside a preparation, `/send-yaml` retains its historical generation-folder behavior. The legacy **YAML → List** and `/list-yamls` manage that older folder, not the preparation's database files. Use the preparation page or `/organize-room` status to inspect preparation files.

### Organizer controls

- **Close registrations** blocks new players but lets existing participants correct files or readiness. **Reopen registrations** allows new registrations again.
- **Remind unready players** mentions only registered players who have uploaded at least one YAML file and are not ready. It asks whether their settings are final. The control is available on Discord, the preparation page, and as an action of `/organize-room`. Only the organizer may use it. A five-minute cooldown persists across restarts; no role or `@everyone` is mentioned.
- **Test YAML files** tests the full set without producing a ZIP or requiring every player to be ready. Changing files invalidates the previous combined test result.
- **Generate** requires at least one registered player, all ready with valid YAML files. AST freezes the inputs, locks changes, tests the full set again, then generates the archive. The page offers an option to skip progression balancing.
- **Cancel preparation** permanently deletes the preparation data and working files, freeing the channel. It is available before generation starts; a running generation remains locked.

Each Archipelago process has a 30-minute timeout. Individual validations and preparation generation run one at a time within this workflow. The older standalone generation commands remain independent. A failed generation retains the preparation for correction and retry.

### ZIP delivery and automatic cleanup

| Outcome | Delivery | Cleanup |
|---|---|---|
| Preparation cancelled | No generated archive | Registrations, YAML files, snapshots, generation history, delivery receipts, reminders and working files are deleted |
| Generation succeeds and Discord accepts the ZIP | Attachment in the same channel | Preparation data and local working files, including the original ZIP, are deleted after successful delivery |
| The ZIP cannot be attached | Download button published in the same channel, valid for **24 hours** | Preparation data is deleted after publication; only a temporary ZIP copy remains |
| Neither attachment nor link can be published | Organizer can use **Retry delivery** | Files and preparation are retained until delivery succeeds |

The temporary link requires an active Web portal and a correctly configured public `WEB_BASE_URL`. Anyone possessing that link can download the ZIP; its authorization is independent of the deleted preparation. Expired archives are removed at startup and hourly while the portal runs. Access to an expired link is refused and also attempts to remove the file.

The preparation message is replaced with a completion/cancellation notice without player names or active buttons. An already open preparation page clears its player/file lists and stops refreshing. The channel can immediately host a new preparation.

Host the ZIP yourself on your Archipelago server or archipelago.gg, then use `/add-url` or the usual `/ast` room-configuration workflow to start tracking. Hosting is not automatic, and the deleted preparation no longer offers room attachment.

### Persistence and restart

AST uses its existing local SQLite database; no extra database server is required. Preparation tables are created additively at startup. They store registrations, current YAML content, validation results, frozen input snapshots, delivery receipts and reminder cooldowns.

Working files are isolated under `data/room-preparations/<preparation>/<job>/`. Temporary ZIP links use `data/room-preparations/downloads/`, independently of the preparation tables. See [[Data and migrations|Data-and-migrations]].

After restart, interrupted generation returns to the closed state. Interrupted individual validations require the affected files to be uploaded again. Generation is not restarted automatically. AST resumes cleanup for cancelled preparations and generations whose delivery receipt was already recorded.

Discord delivery and SQLite are separate operations: a crash after publication but before saving its receipt can cause a repeated delivery. General portal permissions, technical audit records, existing backups and tracked rooms are independent of preparation cleanup.

---

## Français

La préparation est disponible uniquement en **mode Archipelago**. Les joueurs s’inscrivent, déposent leurs YAML et se déclarent prêts avant le lancement de la génération par l’organisateur. Les commandes Discord et la page Web utilisent les mêmes données de préparation.

### Créer une préparation

Lancez `/organize-room` dans le salon texte souhaité et choisissez un nom. La commande publie un message persistant avec des boutons **dans le salon courant**, sans créer de nouveau thread. Un thread existant peut également accueillir la préparation. Ses permissions déterminent qui peut voir le message et le ZIP final.

La création est accessible aux gestionnaires reconnus par AST, s’ils ont accès aux outils Archipelago. L’accueil de `/ast` et le portail d’administration proposent aussi cette création. Les nouvelles préparations acceptent **200 joueurs et 200 slots au total**, sans champ de capacité à renseigner. Une seule préparation peut occuper un salon à la fois. L’organisateur n’a pas besoin de s’inscrire comme joueur.

### S’inscrire et envoyer ses YAML

1. Cliquez sur **S’inscrire** dans le message ou la page de préparation.
2. Déposez vos fichiers dans ce même salon avec l’une de ces interfaces :

   ```text
   /send-yaml file:<joueur.yaml>
   /ast file:<joueur.yaml>
   /organize-room fichier:<joueur.yaml>
   ```

   La page de préparation propose aussi **Déposer ou remplacer mes YAML**, avec sélection multiple. L’option de `/organize-room` s’appelle `file` lorsque le bot est en anglais.
3. Attendez la validation Archipelago, puis cliquez sur **Prêt**.

La confirmation nomme le fichier ajouté à la préparation. Renvoyer un fichier de même nom remplace votre propre copie ; celle d’un autre joueur reste distincte. Modifier ou retirer un fichier vous remet à **Pas prêt**. Vous pouvez vous désinscrire avant le lancement de la génération.

Pour envoyer un lot, sélectionnez plusieurs YAML sur la page, ou envoyez un ZIP avec `/send-yaml file:<joueurs.zip>`, `/ast file:<joueurs.zip>` ou `/organize-room fichier:<joueurs.zip>` dans le salon de préparation. Le ZIP importe les fichiers sans générer de seed. Les sous-dossiers sûrs sont acceptés et leurs fichiers conservés sans chemin ; noms en double, liens, chemins dangereux et entrées non YAML sont refusés. Un lot contient au maximum 20 YAML ; le ZIP et son contenu décompressé sont limités à 16 Mio chacun, avec 1 Mio par YAML. Tous les fichiers importés appartiennent au joueur qui les envoie.

Tout le lot est contrôlé sur les noms, la structure YAML et les quotas avant le remplacement d’un fichier existant. Un lot refusé ne change ni les fichiers ni la disponibilité existants. Les fichiers acceptés sont ensuite validés séparément par Archipelago : une erreur propre à un jeu apparaît sur ce fichier, sans masquer le résultat des autres.

Le tableau des joueurs affiche les slots, jeux, validation globale des YAML et disponibilité. **Mes fichiers YAML** affiche les vrais noms de fichiers et vos diagnostics personnels. Les jeux pondérés et noms tels que `Player{number}` sont indicatifs jusqu’à leur résolution par Archipelago à la génération.

**Validation en attente** signifie que le fichier attend son tour ; **Validation Archipelago en cours** signifie que son processus a démarré. En cas d’échec, le fichier affiche **Erreur de validation Archipelago**, le diagnostic du générateur et les conseils de correction. Un APWorld absent ou défaillant est signalé comme jeu indisponible lorsqu’Archipelago identifie cette erreur. AST ferme l’entrée interactive et arrête les erreurs fatales des fichiers joueurs, au lieu d’attendre une confirmation clavier. Après correction du YAML ou installation du bon APWorld, renvoyez le fichier. Le statut Prêt reste bloqué tant que tous vos fichiers ne sont pas valides.

| Limite | Valeur |
|---|---|
| Joueurs / slots au total | 200 / 200 |
| Fichiers par joueur | 20 |
| Documents par fichier | 1–20 |
| Taille et encodage d’un fichier | 1 Mio, UTF-8, `.yaml` ou `.yml` |
| Taille totale des YAML par préparation | 16 Mio |

Chaque joueur doit avoir au moins un fichier valide pour se déclarer prêt. Chacun gère uniquement ses propres YAML. Les autres joueurs peuvent voir les métadonnées des slots/jeux et la disponibilité, sans télécharger les YAML des autres ni lire leurs diagnostics personnels.

Hors préparation, `/send-yaml` conserve son fonctionnement historique dans le dossier de génération. Le menu **YAML → Lister** et `/list-yamls` gèrent cet ancien dossier, pas les fichiers de préparation stockés en DB. Utilisez la page de préparation ou le statut de `/organize-room` pour consulter ces derniers.

### Commandes de l’organisateur

- **Fermer les inscriptions** bloque les nouveaux joueurs, mais permet aux inscrits de corriger leurs fichiers ou leur disponibilité. **Rouvrir les inscriptions** autorise à nouveau les inscriptions.
- **Relancer les non-prêts** mentionne uniquement les inscrits ayant au moins un YAML et n’étant pas prêts, pour demander si leurs réglages sont définitifs. Disponible sur Discord, sur la page de préparation et comme action de `/organize-room`, ce contrôle est réservé à l’organisateur. Le délai de cinq minutes entre deux relances persiste après redémarrage. Aucun rôle ni `@everyone` n’est mentionné.
- **Tester les YAML** teste l’ensemble sans ZIP et sans exiger que tous soient prêts. Modifier les fichiers invalide le résultat du test global précédent.
- **Générer** exige au moins un inscrit, tous prêts avec des YAML valides. AST fige les fichiers, bloque les modifications, reteste l’ensemble puis génère l’archive. La page permet d’ignorer l’équilibrage de progression.
- **Annuler la préparation** supprime définitivement ses données et fichiers de travail, puis libère le salon. Ce bouton est disponible avant la génération ; une génération en cours reste verrouillée.

Chaque processus Archipelago est limité à 30 minutes. Les validations individuelles et générations de ce parcours sont exécutées une à la fois. Les anciennes commandes de génération restent indépendantes. Un échec de génération conserve la préparation pour permettre sa correction et une nouvelle tentative.

### Transmission du ZIP et nettoyage automatique

| Résultat | Transmission | Nettoyage |
|---|---|---|
| Préparation annulée | Pas d’archive générée | Suppression des inscriptions, YAML, instantanés, historiques, reçus d’envoi, relances et fichiers de travail |
| Génération réussie et ZIP accepté par Discord | Pièce jointe dans le même salon | Suppression des données et fichiers locaux de préparation, ZIP original compris, après transmission réussie |
| Impossible de joindre le ZIP | Bouton de téléchargement publié dans le même salon, valable **24 heures** | Suppression des données après publication ; seule une copie temporaire du ZIP subsiste |
| Ni le ZIP ni le lien ne peuvent être publiés | Bouton **Réessayer la transmission** pour l’organisateur | Conservation des fichiers et de la préparation jusqu’à réussite de la transmission |

Le lien temporaire exige un portail Web actif et un `WEB_BASE_URL` public correctement configuré. Toute personne qui possède le lien peut télécharger le ZIP ; son autorisation est indépendante de la préparation supprimée. Les archives expirées sont retirées au démarrage et toutes les heures lorsque le portail tourne. Un accès à un lien expiré est refusé et tente également de retirer le fichier.

Le message de préparation devient un avis de fin/annulation, sans noms de joueurs ni boutons actifs. Une page déjà ouverte vide ses listes de joueurs/fichiers et arrête ses actualisations. Le salon peut immédiatement accueillir une nouvelle préparation.

Hébergez vous-même le ZIP sur votre serveur Archipelago ou archipelago.gg, puis utilisez `/add-url` ou le parcours habituel de configuration de room dans `/ast` pour lancer le suivi. L’hébergement n’est pas automatique et la préparation supprimée ne propose plus de rattachement.

### Persistance et redémarrage

AST utilise sa base SQLite locale existante : aucun serveur de base de données supplémentaire n’est requis. Les tables de préparation sont créées de manière additive au démarrage. Elles conservent inscriptions, contenu des YAML courants, validations, instantanés des entrées, reçus d’envoi et délais de relance.

Les fichiers de travail sont isolés sous `data/room-preparations/<préparation>/<tâche>/`. Les liens ZIP temporaires utilisent `data/room-preparations/downloads/`, indépendamment des tables de préparation. Voir [[Données et migrations|Data-and-migrations]].

Après redémarrage, une génération interrompue revient à l’état fermé. Les validations individuelles interrompues demandent de renvoyer les fichiers concernés. Aucune génération n’est relancée automatiquement. AST reprend le nettoyage des préparations annulées et des générations dont la transmission a déjà été enregistrée.

La publication Discord et SQLite sont deux opérations distinctes : un arrêt entre la publication et l’enregistrement de son reçu peut entraîner un nouvel envoi. Les permissions générales du portail, audits techniques, sauvegardes existantes et rooms suivies sont indépendants du nettoyage des préparations.
