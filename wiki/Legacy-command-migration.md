# Slash commands and `/ast`

[English](#english) · [Français](#français)

## English

AST publishes two interfaces at the same time:

- `/ast`, the private, contextual command center;
- every direct slash command that was available in `v5.6.7`.

Users may choose either interface. Existing rooms and data are unchanged.

### Visibility and authorization

Discord shows the registered direct commands to every member who has **Use Application Commands**. AST then checks the user's current permission when the command is executed. A visible command is therefore not necessarily usable by that member.

`/ast` behaves differently: it is visible to everyone, but its private buttons and sections are filtered according to the user's role. Permissions are checked again server-side for every action.

Direct room commands must be run inside a tracked room thread. Server and Archipelago administration commands must be run from a regular guild text channel.

### General direct commands

| Direct command | Context | Direct-command permission | Equivalent in `/ast` |
|---|---|---|---|
| `/get-aliases` | Tracked room | Member | The room → Associations |
| `/add-alias` | Tracked room | Member | My space → My slots → Associate |
| `/delete-alias` | Tracked room | Member, own association | My space → My slots → Dissociate |
| `/status-games-list` | Tracked room | Member | The room → Progress |
| `/info` | Tracked room | Member | The room → Information |
| `/get-patch` | Tracked room | Member, associated slots | My space → My patch |
| `/recap-all` | Tracked room | Member | My space → My recap → All slots |
| `/recap` | Tracked room | Member | My space → My recap → One slot |
| `/recap-and-clean` | Tracked room | Member | My space → Advanced |
| `/clean` | Tracked room | Member | My space → Advanced |
| `/clean-all` | Tracked room | Member | My space → Advanced |
| `/hint-from-finder` | Tracked room | Member | My space → My hints |
| `/hint-for-receiver` | Tracked room | Member | My space → My hints |
| `/list-items` | Tracked room | Member | My space → My items |
| `/analyze-spoiler-log` | Tracked room | Member | My space → Analyze spoiler |
| `/send-spoiler-log` | Tracked room | Member | `/ast file:<spoiler.txt|json>` |
| `/excluded-item-list` | Tracked room | Member | My space → My exclusions |
| `/ast-user-portal` | Tracked room | Member | My space → My portal |
| `/update-frequency-check` | Tracked room | Room manager | Manage room → Polling |
| `/update-silent-option` | Tracked room | Room manager | Manage room → More → Notifications |
| `/delete-url` | Tracked room | Room manager | Manage room → More → Delete room |
| `/excluded-item` | Tracked room | Room manager | My space → My exclusions → Add |
| `/delete-excluded-item` | Tracked room | Room manager | My space → My exclusions → Remove |
| `/ast-room-portal` | Tracked room | Room manager | Manage room → More → Room portal |
| `/discord` | Guild text channel | Member | Help |
| `/apworlds-info` | Guild text channel | Member | Help |
| `/add-url` | Guild text channel | Guild manager | AST Administration → Configure room |
| `/ast-portal` | Guild text channel | Guild manager | AST Administration → Portal |

### Archipelago-mode direct commands

These twelve commands are registered only when AST starts with `--ArchipelagoMode`.

| Direct command | Context | Direct-command permission | Equivalent in `/ast` |
|---|---|---|---|
| `/list-yamls` | Guild text channel | Member unless restricted | Archipelago tools → YAML → List |
| `/backup-yamls` | Guild text channel | Member unless restricted | Archipelago tools → YAML → Backup |
| `/download-template` | Guild text channel | Member unless restricted | Archipelago tools → Templates |
| `/delete-yaml` | Guild text channel | Member unless restricted | Archipelago tools → YAML → Delete |
| `/clean-yamls` | Guild text channel | Member unless restricted | Archipelago tools → YAML → Clean all |
| `/send-yaml` | Guild text channel | Member unless restricted | `/ast file:<players.yaml>` |
| `/generate-with-zip` | Guild text channel | Member unless restricted | `/ast file:<players.zip>` |
| `/generate` | Guild text channel | Member unless restricted | Archipelago tools → Generation → Generate |
| `/test-generate` | Guild text channel | Member unless restricted | Archipelago tools → Generation → Test |
| `/list-apworld` | Guild text channel | Member unless restricted | Archipelago tools → APWorld → List |
| `/backup-apworld` | Guild text channel | Member unless restricted | Archipelago tools → APWorld → Backup |
| `/send-apworld` | Guild text channel | Member unless restricted | `/ast file:<world.apworld>` |

### Role summary

| Role | What it can do |
|---|---|
| **Player / member** | Personal and read-only room commands; `/discord`, `/apworlds-info`, and all Archipelago tools unless explicitly restricted |
| **Room manager** | Everything above, plus polling, notifications, exclusions, room portal, and room deletion for that room |
| **Guild manager / delegated AST manager** | Every room in the guild, room creation, and guild portal |
| **Instance owner** | Every level, unrestricted Archipelago tools, and global instance administration |

A delegated AST manager cannot grant or revoke AST-manager access or edit Archipelago restrictions. Only the Discord guild owner, a Discord administrator, a member with **Manage Server**, or the instance owner may do so.

### Current interface differences

- `/get-aliases` is executable by a member as a direct command, while `/ast` restricts the complete Discord-to-slot mapping to room managers.
- Direct `/excluded-item` and `/delete-excluded-item` require a room manager. In `/ast`, a member may manage only their own exclusions.
- All twelve direct Archipelago commands and the `/ast` Archipelago tools are allowed by default. A Discord administrator can explicitly restrict a member; the instance owner always keeps access.
- `/ast` navigation and results are ephemeral. Direct commands run in a room are also ephemeral; guild-channel command results may be posted in the channel.

---

## Français

AST publie simultanément deux interfaces :

- `/ast`, le centre de commandes privé et contextuel ;
- toutes les commandes slash directes qui existaient dans la version `v5.6.7`.

Les utilisateurs peuvent choisir l’une ou l’autre interface. Les rooms et données existantes ne sont pas modifiées.

### Visibilité et autorisation

Discord affiche les commandes directes enregistrées à tous les membres possédant **Utiliser les commandes de l’application**. AST contrôle ensuite les droits actuels de l’utilisateur au moment de l’exécution. Une commande visible n’est donc pas forcément utilisable par ce membre.

`/ast` fonctionne différemment : la commande est visible par tous, mais ses sections et boutons privés sont filtrés selon le rôle. Les droits sont de nouveau contrôlés côté serveur pour chaque action.

Les commandes de room doivent être lancées dans le thread d’une room suivie. Les commandes d’administration du serveur et du mode Archipelago doivent être lancées dans un salon texte normal du serveur.

### Commandes directes générales

| Commande directe | Contexte | Droit de la commande directe | Équivalent dans `/ast` |
|---|---|---|---|
| `/get-aliases` | Room suivie | Joueur | La room → Associations |
| `/add-alias` | Room suivie | Joueur | Mon espace → Mes slots → Associer |
| `/delete-alias` | Room suivie | Joueur, pour sa propre association | Mon espace → Mes slots → Dissocier |
| `/status-games-list` | Room suivie | Joueur | La room → Progression |
| `/info` | Room suivie | Joueur | La room → Informations |
| `/get-patch` | Room suivie | Joueur, pour ses slots associés | Mon espace → Mon patch |
| `/recap-all` | Room suivie | Joueur | Mon espace → Mon récap → Tous les slots |
| `/recap` | Room suivie | Joueur | Mon espace → Mon récap → Un slot |
| `/recap-and-clean` | Room suivie | Joueur | Mon espace → Avancé |
| `/clean` | Room suivie | Joueur | Mon espace → Avancé |
| `/clean-all` | Room suivie | Joueur | Mon espace → Avancé |
| `/hint-from-finder` | Room suivie | Joueur | Mon espace → Mes hints |
| `/hint-for-receiver` | Room suivie | Joueur | Mon espace → Mes hints |
| `/list-items` | Room suivie | Joueur | Mon espace → Mes objets |
| `/analyze-spoiler-log` | Room suivie | Joueur | Mon espace → Analyser le spoiler |
| `/send-spoiler-log` | Room suivie | Joueur | `/ast file:<spoiler.txt|json>` |
| `/excluded-item-list` | Room suivie | Joueur | Mon espace → Mes exclusions |
| `/ast-user-portal` | Room suivie | Joueur | Mon espace → Mon portail |
| `/update-frequency-check` | Room suivie | Responsable de room | Gérer la room → Polling |
| `/update-silent-option` | Room suivie | Responsable de room | Gérer la room → Plus → Notifications |
| `/delete-url` | Room suivie | Responsable de room | Gérer la room → Plus → Supprimer la room |
| `/excluded-item` | Room suivie | Responsable de room | Mon espace → Mes exclusions → Ajouter |
| `/delete-excluded-item` | Room suivie | Responsable de room | Mon espace → Mes exclusions → Retirer |
| `/ast-room-portal` | Room suivie | Responsable de room | Gérer la room → Plus → Portail de room |
| `/discord` | Salon texte du serveur | Joueur | Aide |
| `/apworlds-info` | Salon texte du serveur | Joueur | Aide |
| `/add-url` | Salon texte du serveur | Admin AST du serveur | Administration AST → Configurer une room |
| `/ast-portal` | Salon texte du serveur | Admin AST du serveur | Administration AST → Portail |

### Commandes directes du mode Archipelago

Ces douze commandes ne sont enregistrées que si AST démarre avec `--ArchipelagoMode`.

| Commande directe | Contexte | Droit de la commande directe | Équivalent dans `/ast` |
|---|---|---|---|
| `/list-yamls` | Salon texte du serveur | Joueur, sauf interdiction | Outils Archipelago → YAML → Lister |
| `/backup-yamls` | Salon texte du serveur | Joueur, sauf interdiction | Outils Archipelago → YAML → Sauvegarder |
| `/download-template` | Salon texte du serveur | Joueur, sauf interdiction | Outils Archipelago → Modèles |
| `/delete-yaml` | Salon texte du serveur | Joueur, sauf interdiction | Outils Archipelago → YAML → Supprimer |
| `/clean-yamls` | Salon texte du serveur | Joueur, sauf interdiction | Outils Archipelago → YAML → Tout nettoyer |
| `/send-yaml` | Salon texte du serveur | Joueur, sauf interdiction | `/ast file:<players.yaml>` |
| `/generate-with-zip` | Salon texte du serveur | Joueur, sauf interdiction | `/ast file:<players.zip>` |
| `/generate` | Salon texte du serveur | Joueur, sauf interdiction | Outils Archipelago → Génération → Générer |
| `/test-generate` | Salon texte du serveur | Joueur, sauf interdiction | Outils Archipelago → Génération → Tester |
| `/list-apworld` | Salon texte du serveur | Joueur, sauf interdiction | Outils Archipelago → APWorld → Lister |
| `/backup-apworld` | Salon texte du serveur | Joueur, sauf interdiction | Outils Archipelago → APWorld → Sauvegarder |
| `/send-apworld` | Salon texte du serveur | Joueur, sauf interdiction | `/ast file:<world.apworld>` |

### Résumé des rôles

| Rôle | Possibilités |
|---|---|
| **Joueur** | Commandes personnelles et consultation de la room ; `/discord`, `/apworlds-info` et tous les outils Archipelago sauf interdiction explicite |
| **Responsable de room** | Tout ce qui précède, plus polling, notifications, exclusions, portail et suppression de cette room |
| **Admin AST du serveur / responsable délégué** | Toutes les rooms du serveur, création de room et portail serveur |
| **Owner de l’instance** | Tous les niveaux, accès Archipelago non révocable et administration globale de l’instance |

Un responsable AST délégué ne peut pas accorder ou retirer ce droit, ni modifier les restrictions Archipelago. Seuls le propriétaire Discord, un administrateur Discord, un membre ayant **Gérer le serveur** ou l’Owner de l’instance peuvent effectuer ces changements.

### Différences actuelles entre les interfaces

- `/get-aliases` est exécutable par un joueur en commande directe, tandis que `/ast` réserve la correspondance Discord-vers-slot complète aux responsables de room.
- `/excluded-item` et `/delete-excluded-item` exigent un responsable de room en commande directe. Dans `/ast`, un joueur peut gérer uniquement ses propres exclusions.
- Les douze commandes directes Archipelago et les outils Archipelago de `/ast` sont autorisés par défaut. Un administrateur Discord peut interdire explicitement un membre ; l’Owner de l’instance conserve toujours son accès.
- La navigation et les résultats de `/ast` sont éphémères. Les commandes directes lancées dans une room le sont également ; les résultats des commandes de salon peuvent être publiés dans le salon.
