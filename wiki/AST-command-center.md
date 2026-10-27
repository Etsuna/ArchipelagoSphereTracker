# `/ast` command center

[English](#english) · [Français](#français)

## English

AST provides `/ast` as its centralized interface while also registering the direct slash commands from `v5.6.7`:

```text
/ast
```

The interface is personal and ephemeral: other members cannot see your navigation or private results. A session expires after 15 minutes of inactivity and remains bound to the user, guild, and channel in which it was opened.

The direct commands are optional shortcuts to the same features. See [[Slash commands and AST|Legacy-command-migration]] for their names, required context, and permissions.

```mermaid
flowchart TD
    A["/ast"] --> C{"Context and permissions"}
    C --> P["👤 My space"]
    C --> R["🌐 The room"]
    C --> M["⚙️ Manage room"]
    C --> AR["🧰 Archipelago tools"]
    C --> G["🛠️ AST Administration"]
    C --> I["🧭 AST Instance"]
    C --> H["❓ Help"]
    P --> PS["Slots · items · hints · recap · patch · exclusions · spoiler · portal"]
    R --> RS["Progress · information · associations · health"]
    M --> MS["Sync · pause · resume · polling · notifications · deletion"]
    G --> GS["New room · guild health · AST managers · restrictions · portal"]
    AR -. "Archipelago mode" .-> GA["YAML · APWorld · generation · templates"]
    I --> IS["Every guild and room stored by this instance"]
```

### Context-sensitive home screen

- **Inside a tracked thread or a directly tracked text channel**: direct access to My space, The room, Archipelago tools when enabled and allowed, and—depending on your permissions—Manage room or AST Administration.
- **Inside an untracked server channel**: a paginated selector for accessible rooms, Archipelago tools when enabled and allowed, AST Administration, and help.
- **Inside an untracked thread**: no room is selected; return to a text channel to configure one.
- **In a preparation channel/thread**: the preparation portal is available independently of tracking. Guild managers can create its message through **Organize a room**; no additional thread is created.
- **In a direct message**: `/ast` is refused because the guild and permissions cannot be determined.

### My space

Available to every member who can view the room:

- **My slots**: mention filter, slot association, association/dissociation by name, and removal of the user's own associations.
- **My items**: items received by the user's slots.
- **My hints**: received or found hints.
- **My recap**: viewing and confirmed cleanup.
- **My patch**: download limited to an associated slot.
- **My exclusions**: add or remove items that should not notify this user.
- **Analyze spoiler**: analyze the room's shared spoiler log.
- **My portal**: issue or rotate a personal Web link.

A slot is not exclusive: several users may associate it. Only the exact duplicate “same user + same slot + same room” is rejected.

### The room

Displays tracking health, game progress, public information, and available associations. Large lists are paginated and can be filtered.

### Manage room

Restricted to guild managers and delegated AST managers, plus the thread owner or members with **Manage Threads** in thread mode, or members with **Manage Channels** on the tracked channel in direct-channel mode:

- synchronize immediately;
- pause or resume tracking;
- choose automatic or fixed polling;
- switch between normal and silent notifications;
- issue or revoke the room portal;
- remove tracking and local data after confirmation.

### AST Administration

Restricted to administrators, the Discord guild owner, members with **Manage Server**, delegated AST managers, and the instance owner:

- configure a new room;
- inspect the health of every room in the guild;
- issue or revoke the administration portal;
- grant or revoke AST managers when the user has a real Discord management permission;
- restrict or restore a member's access to Archipelago tools when the user has a real Discord management permission.

### Archipelago tools

Visible in Archipelago mode to every member unless that member has been explicitly restricted:

- **YAML**: list, back up, delete, or clean player files;
- **APWorld**: list and back up installed APWorlds;
- **Generation**: test or run a generation and configure progression balancing;
- **Templates**: browse and download YAML templates independently from YAML management.

The instance owner cannot be restricted. A Discord guild owner, administrator, member with **Manage Server**, or the instance owner can manage the deny list under **AST Administration → Archipelago access restrictions**.

### AST Instance

Visible only to the user configured through `AST_OWNER_USER_ID`. This user can browse every stored guild and room, inspect health, force a synchronization, pause/resume, and clean orphaned entries.

### Importing a file

Discord cannot ask for an attachment from a button. Use the command option instead:

```text
/ast file:<file>
```

| Type | Mode | Where to run it | Minimum permission |
|---|---|---|---|
| `.txt`, `.json` | Normal and Archipelago | A tracked room thread | Room member |
| `.yaml` | Archipelago | Target generation channel | Member unless restricted |
| `.zip` | Archipelago | Target generation channel | Member unless restricted |
| `.apworld` | Archipelago | Target channel | Member unless restricted |

In Archipelago mode, `/ast` also provides the `skip-prog-balancing` option for ZIP generation.

When the current channel has a preparation, `/ast file:<player.yaml>` and `/send-yaml file:<player.yaml>` add the file to that player's preparation, using the same validation and readiness rules as the Web page. Registration is required. Preparation uploads also accept `.yml` and ZIPs containing several YAML files; the page supports multiple file selection. In that context `/ast file:<players.zip>` imports the batch without starting generation. Outside a preparation, ZIP uploads keep the standalone generation behavior. Legacy generation-folder YAML uploads use `.yaml`. Use the preparation controls to test/generate these registered players. See [[Room preparation|Room-preparation]].

See [[Slash commands and AST|Legacy-command-migration]] for the complete direct-command catalog.

---

## Français

AST propose `/ast` comme interface centralisée tout en enregistrant également les commandes slash directes de `v5.6.7` :

```text
/ast
```

L’interface est personnelle et éphémère : les autres membres ne voient ni votre navigation ni les résultats privés. Une session expire après 15 minutes d’inactivité et reste liée à l’utilisateur, au serveur et au salon où elle a été ouverte.

Les commandes directes sont des raccourcis facultatifs vers les mêmes fonctions. Voir [[Commandes slash et AST|Legacy-command-migration]] pour leurs noms, leur contexte et leurs droits.

```mermaid
flowchart TD
    A["/ast"] --> C{"Contexte et permissions"}
    C --> P["👤 Mon espace"]
    C --> R["🌐 La room"]
    C --> M["⚙️ Gérer la room"]
    C --> AR["🧰 Outils Archipelago"]
    C --> G["🛠️ Administration AST"]
    C --> I["🧭 Instance AST"]
    C --> H["❓ Aide"]
    P --> PS["Slots · objets · hints · récap · patch · exclusions · spoiler · portail"]
    R --> RS["Progression · informations · associations · santé"]
    M --> MS["Sync · pause · reprise · polling · notifications · suppression"]
    G --> GS["Nouvelle room · santé · responsables AST · restrictions · portail"]
    AR -. "mode Archipelago" .-> GA["YAML · APWorld · génération · modèles"]
    I --> IS["Toutes les guildes et rooms de l’instance"]
```

### Accueil contextuel

- **Dans un thread suivi ou un salon texte suivi directement** : accès direct à Mon espace, La room, aux Outils Archipelago lorsqu’ils sont activés et autorisés et, selon vos droits, à Gérer la room ou Administration AST.
- **Dans un salon non suivi** : sélection paginée des rooms accessibles, Outils Archipelago lorsqu’ils sont activés et autorisés, Administration AST et aide.
- **Dans un thread non suivi** : aucune room n’est sélectionnée ; revenez dans un salon texte pour en configurer une.
- **Dans un salon/thread de préparation** : le portail de préparation est disponible indépendamment du suivi. Les gestionnaires peuvent créer son message depuis **Organiser une room**, sans nouveau thread.
- **En message privé** : `/ast` est refusé, car le serveur et les permissions ne peuvent pas être déterminés.

### Mon espace

Pour tout membre qui peut voir la room :

- **Mes slots** : filtre de mentions, association d’un slot, association/dissociation par nom et suppression de ses propres associations.
- **Mes objets** : objets reçus pour ses slots.
- **Mes hints** : hints reçus ou trouvés.
- **Mon récap** : consultation et nettoyage confirmé.
- **Mon patch** : téléchargement limité à un slot associé.
- **Mes exclusions** : ajout ou retrait d’objets à ne pas notifier pour soi.
- **Analyser le spoiler** : analyse du spoiler partagé de la room.
- **Mon portail** : création ou rotation d’un lien Web personnel.

Un slot n’est pas exclusif : plusieurs utilisateurs peuvent l’associer. Seul le doublon exact « même utilisateur + même slot + même room » est refusé.

### La room

Affiche la santé du suivi, la progression des jeux, les informations publiques et les associations disponibles. Les listes importantes sont paginées et peuvent être filtrées.

### Gérer la room

Réservé aux gestionnaires du serveur et aux responsables AST délégués, ainsi qu’au propriétaire du thread ou aux membres ayant **Gérer les fils** en mode thread, ou **Gérer les salons** sur le salon suivi en mode salon direct :

- synchroniser immédiatement ;
- suspendre ou reprendre le suivi ;
- choisir le polling automatique ou fixe ;
- basculer les notifications normales/silencieuses ;
- créer/révoquer le portail de room ;
- supprimer le suivi et les données locales après confirmation.

### Administration AST

Réservé aux administrateurs, au propriétaire du serveur, aux membres ayant **Gérer le serveur**, aux responsables AST délégués et au propriétaire d’instance :

- configurer une nouvelle room ;
- consulter la santé de toutes les rooms du serveur ;
- créer/révoquer le portail d’administration ;
- accorder ou révoquer des responsables AST, si l’utilisateur possède un vrai droit Discord de gestion ;
- interdire ou rétablir l’accès d’un membre aux outils Archipelago, si l’utilisateur possède un vrai droit Discord de gestion.

### Outils Archipelago

Visible en mode Archipelago pour tous les membres, sauf ceux qui ont été explicitement interdits :

- **YAML** : lister, sauvegarder, supprimer ou nettoyer les fichiers joueurs ;
- **APWorld** : lister et sauvegarder les APWorlds installés ;
- **Génération** : tester ou lancer une génération et régler le progression balancing ;
- **Modèles** : parcourir et télécharger les modèles YAML indépendamment de la gestion YAML.

L’Owner de l’instance ne peut pas être interdit. Le propriétaire Discord, un administrateur, un membre ayant **Gérer le serveur** ou l’Owner de l’instance peut gérer la liste depuis **Administration AST → Restrictions d’accès Archipelago**.

### Instance AST

Visible uniquement par l’utilisateur défini dans `AST_OWNER_USER_ID`. Il peut parcourir toutes les guildes et rooms stockées, inspecter leur santé, forcer une synchronisation, suspendre/reprendre et nettoyer des entrées orphelines.

### Importer un fichier

Discord ne permet pas à un bouton de demander une pièce jointe. Utilisez donc l’option de la commande :

```text
/ast file:<fichier>
```

| Type | Mode | Où lancer la commande | Droit minimal |
|---|---|---|---|
| `.txt`, `.json` | Normal et Archipelago | Thread d’une room suivie | Membre de la room |
| `.yaml` | Archipelago | Salon cible de génération | Membre, sauf interdiction |
| `.zip` | Archipelago | Salon cible de génération | Membre, sauf interdiction |
| `.apworld` | Archipelago | Salon cible | Membre, sauf interdiction |

En mode Archipelago, `/ast` ajoute aussi l’option `skip-prog-balancing` pour les générations ZIP.

Lorsqu’une préparation existe dans le salon courant, `/ast file:<joueur.yaml>` et `/send-yaml file:<joueur.yaml>` y ajoutent le fichier du joueur, avec les mêmes règles de validation et de disponibilité que la page Web. L’inscription est obligatoire. Les préparations acceptent aussi `.yml` et les ZIP contenant plusieurs YAML ; la page permet la sélection multiple. Dans ce contexte, `/ast file:<joueurs.zip>` importe le lot sans lancer de génération. Hors préparation, un ZIP conserve son parcours de génération autonome. Les uploads historiques du dossier de génération utilisent `.yaml`. Utilisez les contrôles de préparation pour tester/générer ses inscrits. Voir [[Préparation des rooms|Room-preparation]].

Voir [[Commandes slash et AST|Legacy-command-migration]] pour le catalogue complet des commandes directes.
