# ArchipelagoSphereTracker

<details>
<summary>🇫🇷 Français</summary>

ArchipelagoSphereTracker est un bot Discord qui se connecte à une room Archipelago (exemple : https://archipelago.gg/room/trackerID) pour suivre l'avancée des joueurs et automatiser la gestion des ressources.

Un bot prêt à l'emploi en mode Normal peut être ajouté à votre serveur Discord : https://discord.com/oauth2/authorize?client_id=1408901673522430047.

Si vous préférez héberger votre propre bot, téléchargez la dernière release ou compilez le projet.

## Fonctionnalités actuelles
### Mode Normal et Mode Archipelago
* Multi-Discord et multi-channel
* Ajouter une URL (droits d'admin requis)
* Supprimer une URL (droits d'admin requis)
* Récupérer tous les noms depuis le tracker
* Définir un alias (remplace le nom par celui sur Discord)
* Supprimer son propre alias (propriétaire ou admin requis)
* Récapituler la table de loot depuis le dernier nettoyage (uniquement si un alias a été créé)
* Récapituler et nettoyer la table de loot (uniquement si un alias a été créé)
* Envoyer automatiquement des messages concernant les nouveaux objets lootés sur Discord (avec tag, uniquement si un alias a été créé)
* Envoyer automatiquement un message quand un joueur complète son objectif
* Lister les items reçus par nom de joueur (affichage en retour à la ligne ou séparé par des virgules)
* Lister les hints par receivers ou par finders
* Lister les liens de patch
* Récupérer le port de connexion
* Suppression automatique du fil après 1 semaine d'inactivité

### Mode Archipelago uniquement
* Fonctionne uniquement sur architecture x64
* Envoyer des `.yaml` au serveur (filtré par channel)
* Envoyer des `.apworld` au serveur
* Backup des `.yaml` envoyés par channel
* Backup des `.apworlds` envoyés par channel
* Générer un Multiworld depuis le dossier du serveur
* Générer un fichier ZIP contenant tous les `.yaml` présents sur le serveur
* Lister les `Yamls` filtrés par channel
* Lister les `Apworlds` présents sur le serveur
* Gestion automatique de la compatibilité Windows et Linux pour la génération des Multiworlds

Plus d'informations sont disponibles sur le [Wiki](https://github.com/Etsuna/ArchipelagoSphereTracker/wiki).

## Jeux pris en charge
Tous les jeux pris en charge par le Randomizer MultiWorld [Archipelago](https://github.com/ArchipelagoMW/Archipelago) sont compatibles et peuvent être utilisés en MultiWorld complet entre eux.

## Prérequis
```
Aucun prérequis n’est nécessaire pour utiliser la version précompilée.
Dotnet 8 est requis uniquement si vous souhaitez compiler le projet vous-même.
```

## Configuration
Un fichier `.env` est nécessaire dans le répertoire principal du dépôt.

### Exemple de configuration
```
DISCORD_TOKEN=YOUR_DISCORD_BOT_TOKEN
LANGUAGE=fr (langues supportées : en et fr) — si non défini, l’anglais sera utilisé par défaut.
```

Si vous souhaitez créer votre propre bot Discord en utilisant le code de ce dépôt, votre bot aura besoin des permissions définies par l'entier `395137117248`.

Les permissions suivantes seront accordées à ArchipelagoSphereTracker :
* Voir les salons
* Envoyer des messages
* Créer des fils publics
* Créer des fils privés
* Envoyer des messages dans les threads
* Gérer les messages
* Gérer les fils
* Intégrer des liens
* Joindre des fichiers
* Ajouter des réactions
* Lire l’historique des messages
* Utiliser des commandes slash

## Exécution avec l'intégration d'Archipelago (génération de Multiworld, envoi de `.yaml`/`.apworld`, etc.)
```
Téléchargez la version Windows "ast-win-x64-vX.X.X.zip" ou Linux "ast-linux-x64-vX.X.X.tar.gz" depuis la page des releases.
Décompressez dans un dossier.
Ajoutez dans le même répertoire le fichier .env correctement configuré.
Ajoutez dans le dossier ./extern/Archipelago/ les ROMs nécessaires si besoin.
Windows : exécutez le programme ArchipelagoSphereTracker.exe.
Linux : exécutez le programme ./ArchipelagoSphereTracker.
```

## Installation avec Dotnet 8
```
# Clonez le dépôt
git clone https://github.com/Etsuna/ArchipelagoSphereTracker.git

# Entrez dans le répertoire
cd ArchipelagoSphereTracker

# Configurez votre fichier .env
vim .env

# Restaurez le projet
dotnet restore

# Compilez le projet
dotnet build --configuration Release

# Publiez le projet
Windows x64 : dotnet publish ArchipelagoSphereTracker.csproj -c Release -r win-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true
Linux x64 : dotnet publish ArchipelagoSphereTracker.csproj -c Release -r linux-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true

# Lancez le bot
Allez dans le dossier .\\bin\\Release\\net8.0\\linux-x64\\publish\\ ou .\\bin\\Release\\net8.0\\win-x64\\publish\\ selon votre OS.
Copiez le fichier .env dans ce dossier.
Windows : exécutez ArchipelagoSphereTracker.exe (--install ou --NormalMode ou --ArchipelagoMode).
Linux : exécutez ./ArchipelagoSphereTracker.
```

## Télémétrie
Une fonctionnalité de télémétrie a été ajoutée pour collecter des statistiques d’usage anonymes du programme.
Elle peut être désactivée en ajoutant dans le `.env` le paramètre `TELEMETRY=false`. Si non défini, la télémétrie est activée par défaut.

### Que collecte la télémétrie ?
* Le nombre total de serveurs Discord (guilds) où le programme est actif
* Le nombre total de fils utilisés
* Le nombre d’instances distinctes du programme en fonctionnement (identifiées par un identifiant unique local, anonymisé)

### Ce qui n’est pas collecté
* Aucune donnée personnelle ou sensible (pas de noms, IDs Discord, IP, messages, etc.)
* Aucun détail sur les fils ou contenus des serveurs

### Pourquoi cette télémétrie ?
Elle permet de mieux comprendre l’adoption du programme, d’évaluer son utilisation et d’améliorer son développement, tout en respectant la vie privée des utilisateurs.

### Fonctionnement technique
* La télémétrie est envoyée automatiquement une fois par jour ou à chaque fois qu'une URL de room est ajoutée ou supprimée depuis chaque instance.
* Les données sont transmises de façon sécurisée via HTTPS vers un serveur dédié.
* Chaque instance génère localement un identifiant unique non personnel utilisé pour compter les programmes distincts.
* Un mécanisme évite les envois multiples par jour depuis une même instance.
* Aucune donnée personnelle n’est envoyée.

</details>

<details open>
<summary>🇬🇧 English</summary>

ArchipelagoSphereTracker is a Discord bot that connects to an Archipelago room (example: https://archipelago.gg/room/trackerID) to follow player progress and automate resource handling.

A ready-to-use bot in Normal Mode can be added to your Discord server: https://discord.com/oauth2/authorize?client_id=1408901673522430047.

If you prefer to host your own bot, download the latest release or compile the project yourself.

## Current Features
### Normal Mode and Archipelago Mode
* Multi-Discord and multi-channel support
* Add a tracker URL (admin rights required)
* Remove a tracker URL (admin rights required)
* Fetch all player names from the tracker
* Set an alias (replaces the name with your Discord username)
* Delete your alias (owner or admin required)
* Summarize the loot table since the last cleanup (only if an alias is set)
* Summarize and clean the loot table (only if an alias is set)
* Automatically post messages when new items are received (with Discord tag, only if an alias is set)
* Automatically announce when a player completes their goal
* List items received by player name (option to display items line-by-line or comma-separated)
* List hints by receivers or by finders
* List patch links
* Retrieve the tracker connection port
* Auto-delete threads after 1 week of inactivity

### Archipelago Mode Only
* Runs only on x64 architecture
* Upload `.yaml` files to the server (filtered by channel)
* Upload `.apworld` files to the server
* Backup uploaded `.yaml` files by channel
* Backup uploaded `.apworlds` files by channel
* Generate a Multiworld from the server’s folder
* Generate a ZIP file containing all `.yaml` files from the server
* List `Yamls` files filtered by channel
* List `Apworlds` files on the server
* Automatic handling of Windows/Linux compatibility for Multiworld generation

More info is available on the [Wiki](https://github.com/Etsuna/ArchipelagoSphereTracker/wiki).

## Supported Games
All games supported by the [Archipelago MultiWorld Randomizer](https://github.com/ArchipelagoMW/Archipelago) are fully compatible and can be mixed freely in MultiWorld.

## Requirements
```
No requirements for using the precompiled version.
Dotnet 8 is only needed if you want to compile the project yourself.
```

## Configuration
A `.env` file is required in the repository root.

### Example
```
DISCORD_TOKEN=YOUR_DISCORD_BOT_TOKEN
LANGUAGE=en (supported languages: en and fr) — if not set, English will be used by default.
```

If you want to create your own bot using this code, your bot must have the permissions defined by the integer `395137117248`.

The following permissions will be used by ArchipelagoSphereTracker:
* View channels
* Send messages
* Create public threads
* Create private threads
* Send messages in threads
* Manage messages
* Manage threads
* Embed links
* Attach files
* Add reactions
* Read message history
* Use Slash Commands

## Running with Archipelago Integration (Multiworld generation, `.yaml`/`.apworld` uploads, etc.)
```
Download the Windows version "ast-win-x64-vX.X.X.zip" or Linux version "ast-linux-x64-vX.X.X.tar.gz" from the release page.
Unzip into a folder.
Add a properly configured .env file to the same folder.
Add the necessary ROMs in the ./extern/Archipelago/ folder if needed.
Windows: run ArchipelagoSphereTracker.exe.
Linux: run ./ArchipelagoSphereTracker.
```

## Installation with Dotnet 8
```
# Clone the repo
git clone https://github.com/Etsuna/ArchipelagoSphereTracker.git

# Enter the folder
cd ArchipelagoSphereTracker

# Set up the .env file
vim .env

# Restore project dependencies
dotnet restore

# Build the project
dotnet build --configuration Release

# Publish the project
Windows: dotnet publish ArchipelagoSphereTracker.csproj -c Release -r win-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true
Linux: dotnet publish ArchipelagoSphereTracker.csproj -c Release -r linux-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true

# Run the bot
Go to the folder .\\bin\\Release\\net8.0\\linux-x64\\publish\\ or .\\bin\\Release\\net8.0\\win-x64\\publish\\ depending on your OS.
Copy the .env file to this folder.
Windows: run ArchipelagoSphereTracker.exe (--install or --NormalMode or --ArchipelagoMode).
Linux: run ./ArchipelagoSphereTracker.
```

## Telemetry
A telemetry feature collects anonymous usage statistics.
You can disable it by setting `TELEMETRY=false` in the `.env` file. If not set, telemetry is enabled by default.

### What telemetry collects
* Total number of Discord servers (guilds) where the bot is active
* Total number of threads used
* Number of distinct bot instances (identified by a local, anonymized unique ID)

### What is **not** collected
* No personal or sensitive data (no names, Discord IDs, IPs, messages, etc.)
* No details about threads or server content

### Why telemetry?
It helps understand adoption, usage, and improve development while respecting user privacy.

### Technical details
* Telemetry is sent once per day or when a tracker URL is added or removed.
* Data is transmitted securely over HTTPS to a dedicated server.
* Each bot instance generates a unique local ID used only to count instances.
* Duplicate submissions from the same instance are prevented.
* No personal data is sent.

</details>
