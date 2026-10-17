# Archipelago mode

[English](#english) · [Français](#français)

## English

Archipelago mode adds the installation and use of a local Archipelago environment to AST. It is intended for a private instance, normally connected to a single Discord guild.

Start it with:

```powershell
.\ArchipelagoSphereTracker.exe --ArchipelagoMode
```

```bash
./ArchipelagoSphereTracker --ArchipelagoMode
```

AST can separately install or update the local environment with `--install`. The runtime targets Windows x64 or Linux x64.

### Additional features

- import, list, back up, download, and delete YAML files;
- download available YAML templates;
- import, list, and back up APWorld files;
- test a generation before running it;
- generate from existing YAML files or from a ZIP archive;
- choose whether to skip progression balancing during generation;
- download generated archives through a private link.

### Recommended workflow

For a group session, use [[Room preparation|Room-preparation]]: `/organize-room` posts the board in the current channel, supports command and Web YAML uploads, and waits for all players to be ready before generation. New preparations use a fixed limit of 200 players and 200 total slots. After cancellation or successful ZIP/link delivery, their data is deleted automatically. Organizers can remind unready players who have uploaded YAML files.

The standalone YAML/generation tools below remain available independently. Their older generation folders are separate from preparation files.

```mermaid
flowchart TD
    A[/"Prepare YAML and APWorld files"/] --> B{"File type"}
    B -->|"YAML"| C["/ast file:players.yaml"]
    B -->|"APWorld"| D["/ast file:world.apworld"]
    B -->|"YAML ZIP"| E["/ast file:players.zip"]
    C --> F["Archipelago tools → YAML"]
    D --> G["Archipelago tools → APWorld"]
    F --> H["List · back up · delete · clean"]
    G --> I["List · back up"]
    A --> T["Archipelago tools → Templates"]
    C --> J["Archipelago tools → Generation"]
    E --> K["Generate directly from ZIP"]
    J --> L["Test"]
    L -->|"OK"| M["Generate"]
    K --> N["Protected archive download"]
    M --> N
```

### Importing files

Discord buttons cannot request an attachment. Files can be uploaded through `/ast` with its optional file argument:

```text
/ast file:<players.yaml>
/ast file:<players.zip>
/ast file:<world.apworld>
```

YAML, APWorld, Generation, and Templates each have their own section under **Archipelago tools** on the `/ast` home screen. A YAML ZIP starts its generation workflow directly after validation.

The restored direct alternatives are `/send-yaml`, `/generate-with-zip`, and `/send-apworld`. The other direct administration commands are listed under [[Slash commands and AST|Legacy-command-migration]].

### File validation and quarantine

Standalone file uploads use `extern/upload-quarantine/` and are not installed before validation succeeds. AST checks the extension, file signature and structure, normalizes archive paths, and rejects traversal or symbolic-link entries. Preparation YAML files instead use their dedicated database and validation directories, with the limits documented under [[Room preparation|Room-preparation]].

A YAML ZIP archive may contain at most 500 entries. An APWorld may contain up to 5,000 entries. Both archive types are limited to 256 MiB of uncompressed data. The normal `WEB_MAX_UPLOAD_BYTES` and Discord attachment limits still apply before these archive checks.

Standalone protected downloads are placed under `extern/portal-downloads/` and require a valid private portal token. Preparation generation first sends the ZIP to Discord; its fallback download link uses a separate temporary archive valid for 24 hours, independently of the deleted preparation.

### APWorld trust warning

An APWorld can contain executable Python code that will later be loaded by Archipelago. Install APWorld files only from authors and sources you trust. File validation can reject malformed or dangerous archive paths, but it cannot prove that the contained game code is safe.

Custom worlds are global assets of the Archipelago installation. Deleting a tracked room does not delete an installed APWorld.

### Permissions

Every guild member may use YAML, APWorld, Generation, and Templates by default. A Discord guild owner, administrator, member with **Manage Server**, or the instance owner can explicitly restrict a member under **AST Administration → Archipelago access restrictions**. The restriction applies to `/ast`, direct slash commands, and Archipelago Web-portal operations. The instance owner always retains access. See [[Administration and security|Administration-and-security]].

---

## Français

Le mode Archipelago ajoute à AST l’installation et l’utilisation d’un environnement Archipelago local. Il est destiné à une instance privée, normalement reliée à un seul serveur Discord.

Démarrez-le avec :

```powershell
.\ArchipelagoSphereTracker.exe --ArchipelagoMode
```

```bash
./ArchipelagoSphereTracker --ArchipelagoMode
```

AST peut installer ou mettre à jour séparément l’environnement local avec `--install`. Le runtime cible Windows x64 ou Linux x64.

### Fonctions supplémentaires

- importer, lister, sauvegarder, télécharger et supprimer les fichiers YAML ;
- télécharger les modèles YAML disponibles ;
- importer, lister et sauvegarder les fichiers APWorld ;
- tester une génération avant de la lancer ;
- générer depuis les YAML existants ou une archive ZIP ;
- choisir d’ignorer ou non le progression balancing pendant la génération ;
- télécharger les archives générées avec un lien privé.

### Workflow conseillé

Pour une session de groupe, utilisez [[Préparation des rooms|Room-preparation]] : `/organize-room` publie le tableau dans le salon courant, accepte les YAML par commande ou page Web, puis attend que tous les joueurs soient prêts pour générer. Les nouvelles préparations sont limitées à 200 joueurs et 200 slots au total. Après annulation ou transmission réussie du ZIP/lien, leurs données sont supprimées automatiquement. L’organisateur peut relancer les inscrits ayant un YAML mais pas prêts.

Les outils YAML/génération autonomes ci-dessous restent disponibles indépendamment. Leurs anciens dossiers de génération sont distincts des fichiers de préparation.

```mermaid
flowchart TD
    A[/"Préparer les YAML et APWorld"/] --> B{"Type de fichier"}
    B -->|"YAML"| C["/ast file:players.yaml"]
    B -->|"APWorld"| D["/ast file:world.apworld"]
    B -->|"ZIP de YAML"| E["/ast file:players.zip"]
    C --> F["Outils Archipelago → YAML"]
    D --> G["Outils Archipelago → APWorld"]
    F --> H["Lister · sauvegarder · supprimer · nettoyer"]
    G --> I["Lister · sauvegarder"]
    A --> T["Outils Archipelago → Modèles"]
    C --> J["Outils Archipelago → Génération"]
    E --> K["Génération directe depuis le ZIP"]
    J --> L["Tester"]
    L -->|"OK"| M["Générer"]
    K --> N["Téléchargement protégé de l’archive"]
    M --> N
```

### Importer les fichiers

Les boutons Discord ne peuvent pas demander de pièce jointe. Les fichiers peuvent être envoyés avec `/ast` et son argument facultatif :

```text
/ast file:<players.yaml>
/ast file:<players.zip>
/ast file:<world.apworld>
```

YAML, APWorld, Génération et Modèles possèdent désormais chacun leur propre rubrique sous **Outils Archipelago**, depuis l’accueil de `/ast`. Un ZIP de YAML lance directement son workflow de génération après validation.

Les alternatives directes restaurées sont `/send-yaml`, `/generate-with-zip` et `/send-apworld`. Les autres commandes directes d’administration sont listées dans [[Commandes slash et AST|Legacy-command-migration]].

### Validation et quarantaine des fichiers

Les uploads autonomes utilisent `extern/upload-quarantine/` et ne sont pas installés avant la réussite de la validation. AST vérifie l’extension, la signature et la structure du fichier, normalise les chemins d’archive et refuse les entrées de traversée ou les liens symboliques. Les YAML de préparation utilisent leur base et leurs répertoires de validation dédiés, selon les limites décrites dans [[Préparation des rooms|Room-preparation]].

Une archive ZIP de YAML peut contenir au maximum 500 entrées. Un APWorld peut en contenir jusqu’à 5 000. Les deux types d’archives sont limités à 256 Mio de données décompressées. Les limites habituelles `WEB_MAX_UPLOAD_BYTES` et de pièce jointe Discord s’appliquent toujours avant ces contrôles d’archive.

Les téléchargements protégés autonomes sont placés sous `extern/portal-downloads/` et exigent un token de portail privé valide. La génération de préparation envoie d’abord le ZIP sur Discord ; son lien de secours utilise une archive temporaire distincte valable 24 heures, indépendamment de la préparation supprimée.

### Avertissement de confiance APWorld

Un APWorld peut contenir du code Python exécutable qui sera ensuite chargé par Archipelago. Installez uniquement des APWorld provenant d’auteurs et de sources de confiance. La validation peut refuser un fichier mal formé ou des chemins d’archive dangereux, mais elle ne peut pas prouver que le code du jeu contenu est sûr.

Les custom worlds sont des assets globaux de l’installation Archipelago. La suppression d’une room suivie ne supprime pas un APWorld installé.

### Permissions

Tous les membres du serveur peuvent utiliser YAML, APWorld, Génération et Modèles par défaut. Le propriétaire Discord, un administrateur, un membre ayant **Gérer le serveur** ou l’Owner de l’instance peut interdire explicitement un membre depuis **Administration AST → Restrictions d’accès Archipelago**. Cette interdiction s’applique à `/ast`, aux slash commands directes et aux opérations Archipelago du portail Web. L’Owner de l’instance conserve toujours son accès. Voir [[Administration et sécurité|Administration-and-security]].
