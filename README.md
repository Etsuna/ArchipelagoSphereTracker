# ArchipelagoSphereTracker 
<details>
<summary>🇫🇷 Français</summary>

Un bot Discord conçu pour être lié a la room pour Archipelago (Exemple : https://archipelago.gg/room/trackerID).

## Fonctionnalités Actuelles
### Mode Normal et Mode Archipelago
* Multi-Discord et Multi-Channel
* Ajouter une URL (Droits d'admin requis)
* Supprimer une URL (Droits d'admin requis)
* Récupérer tous les noms depuis le tracker
* Définir un alias (remplace le nom par celui sur Discord)
* Supprimer son propre alias (Propriétaire et Admin requis)
* Récapituler la table de loot des objets depuis le dernier récapitulatif et nettoyage (uniquement si un alias a été créé).
* Récapituler et nettoyer la table de loot des objets (uniquement si un alias a été créé).
* Envoyer automatiquement des messages concernant les nouveaux objets lootés sur Discord (avec le tag Discord, uniquement si un alias a été créé).
* Envoyer automatiquement un message quand un joueur complète son objectif.
* Lister les items reçus par le nom du joueur (avec l'option d'affichage en retour à la ligne pour chaque item ou séparés par une virgule).
* Lister les hints par receivers ou par finders.
* Lister le lien des Patchs.
* Récupérer le port de connexion.
* Suppression automatique du fil après 1 semaine d'inactivité.

### Mode Archipelago Seulement
* ARM64 n'est pas supporté avec ce mode.
* Envoyer des `.yamls` au server filtré par le channel.
* Envoyer des `.apworld` au server.
* Backup des `.yamls` envoyés au channel
* Backup des `.apwrolds` envoyés au channel
* Générer à partir du dossier du server un Multiworld.
* Générer à partir du server une fichier Zip contenant tous les `.yamls` compris dans le Zip.
* Lister les `Yamls` filtré par le channel.
* Lister les `Apworlds` présent dans le server.
* Gestion automatique de la compatibilité Windows et Linux pour la generation des Multiworld.


Pour plus d'info, voir le [Wiki](https://github.com/Etsuna/ArchipelagoSphereTracker/wiki)

## Jeux Pris en Charge
Tous les jeux pris en charge par le Randomizer MultiWorld [Archipelago](https://github.com/ArchipelagoMW/Archipelago) sont compatibles et ont une compatibilité MultiWorld complète entre eux.

## Prérequis
```
Aucun prérequis n’est nécessaire pour utiliser la version précompilée.
Dotnet 8 est requis uniquement si vous souhaitez compiler le projet vous-même.
```

## Configuration
Un fichier `.env` est nécessaire dans le répertoire principal du dépôt.

### Exemple de Configuration :
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
* Utiliser des commands slash

## Execution avec l'intégration d'Archipelago (Génération de multiworld, envoi de yamls/apworlds, etc)
```
Téléchargez la version Windows "ast-win-x64-vX.X.X.zip" ou Linux "ast-linux-x64-vX.X.X.tar.gz" depuis la page des releases.
Décompressez dans un dossier
Ajoutez dans la même répertoire le fichier .env correctement configuré
Ajoutez dans le dossier ./extern/Archipelago/ les roms necessaires si besoin
Windows: Executez le programme ArchipelagoSphereTracker.exe
Linux: Executez le programme ./ArchipelagoSphereTracker
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

# Publishez le projet
Windows x64 : dotnet publish ArchipelagoSphereTracker.csproj -c Release -r win-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true
linux x64: dotnet publish ArchipelagoSphereTracker.csproj -c Release -r linux-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true
Windows arm64 : dotnet publish ArchipelagoSphereTracker.csproj -c Release -r win-arm64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true
linux arm64: dotnet publish ArchipelagoSphereTracker.csproj -c Release -r linux-arm64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true

# Lancez le bot
Allez dans le dossier .\bin\Release\net8.0\linux-x64\publish\ ou .\bin\Release\net8.0\win-x64\publish\ selon votre OS
Copiez le fichier .env dans ce dossier
Windows: exécutez ArchipelagoSphereTracker.exe (--install ou --NormalMode ou --ArchipelagoMode)
Linux: exécutez ./ArchipelagoSphereTracker
```

## Télémétrie
Une fonctionnalité de télémétrie a été ajoutée pour collecter des statistiques d’usage anonymes du programme.
Elle peut être désactivée en ajoutant dans le `.env` le paramètre `TELEMETRY=false`. Si non défini, la télémétrie est activée par défaut.

Que collecte la télémétrie ?
* Le nombre total de serveurs Discord (guilds) où le programme est actif
* Le nombre total de fils utilisés
* Le nombre d’instances distinctes du programme en fonctionnement (identifiées par un identifiant unique local, anonymisé)

Ce qui n’est pas collecté :
* Aucune donnée personnelle ou sensible (pas de noms, IDs Discord, IP, messages, etc.)
* Aucun détail sur les fils ou contenus des serveurs

## Pourquoi cette télémétrie ?

Elle permet de mieux comprendre l’adoption du programme, d’évaluer son utilisation, et d’améliorer son développement, tout en respectant la vie privée des utilisateurs.

## Fonctionnement technique

* La télémétrie est envoyée automatiquement une fois par jour ou à chaque fois qu'une URL d'un Room est ajoutée ou supprimée depuis chaque instance.
* Les données sont transmises de façon sécurisée via HTTPS vers un serveur dédié.
* Chaque instance génère localement un identifiant unique non personnel utilisé pour compter les programmes distincts.
* Un mécanisme évite les envois multiples par jour depuis une même instance.
* Aucune donnée personnelle n’est envoyée.


</details>

<details open>
<summary>🇬🇧 English</summary>
A Discord bot designed to be linked to an Archipelago room (example: https://archipelago.gg/room/trackerID).

## Current Features
### Normal Mode and Archipelago Mode
* Multi-Discord and Multi-Channel support  
* Add a tracker URL (admin rights required)  
* Remove a tracker URL (admin rights required)  
* Fetch all player names from the tracker  
* Set an alias (replaces the name with your Discord username)  
* Delete your alias (owner or admin required)  
* Summarize the loot table since last cleanup (only if alias is set)  
* Summarize and clean the loot table (only if alias is set)  
* Automatically post messages when new items are received (with Discord tag, only if alias is set)  
* Automatically announce when a player completes their goal  
* List items received by a player (option to display items line-by-line or comma-separated)  
* List hints by receivers or by finders  
* List patch links  
* Retrieve the tracker connection port  
* Auto-delete threads after 1 week of inactivity  

### Archipelago Mode Only
* X64 Only, ARM64 is not supported for this Mode
* Upload `.yaml` files to the server (filtered by channel)  
* Upload `.apworld` files to the server  
* Backup uploaded `.yaml` files by channel  
* Backup uploaded `.apworld` files by channel  
* Generate a Multiworld from the server’s folder  
* Generate a ZIP file containing all `.yaml` files from the server  
* List `Yamls` files filtered by channel  
* List `Apworlds` files on the server  
* Automatic handling of Windows/Linux compatibility for Multiworld generation


More info available on the [Wiki](https://github.com/Etsuna/ArchipelagoSphereTracker/wiki)

## Supported Games
All games supported by the [Archipelago MultiWorld Randomizer](https://github.com/ArchipelagoMW/Archipelago) are fully compatible with this tool.

## Requirements
```
No requirements for using the precompiled version.
Dotnet 8 is only needed if you want to compile the project yourself.
```
## Configuration
```
A `.env` file is required in the root folder.
```

### Example:
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
Unzip into a folder
Add a properly configured .env file to the same folder
Add the necessary ROMs in the ./extern/Archipelago/ folder if needed.
Windows: Run ArchipelagoSphereTracker.exe
Linux: Run ./ArchipelagoSphereTracker
```

## Installation with Dotnet 8
```
Clone the repo

git clone https://github.com/Etsuna/ArchipelagoSphereTracker.git
Enter the folder

cd ArchipelagoSphereTracker
Set up the .env file

vim .env
Restore project dependencies

dotnet restore
Build the project

dotnet build --configuration Release
Publish the project

Windows: dotnet publish ArchipelagoSphereTracker.csproj -c Release -r win-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true
Linux: dotnet publish ArchipelagoSphereTracker.csproj -c Release -r linux-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true
Run the bot

Go to the folder .\bin\Release\net8.0\linux-x64\publish\ or .\bin\Release\net8.0\win-x64\publish\ depending on your OS
Copy the .env file to this folder
Windows: run ArchipelagoSphereTracker.exe (--install or --NormalMode or --ArchipelagoMode)
Linux: run ./ArchipelagoSphereTracker
```

## Telemetry
A telemetry feature has been added to collect anonymous usage statistics.  
It can be disabled by setting `TELEMETRY=false` in the `.env` file. If not set, telemetry is enabled by default.

### What telemetry collects:
* Total number of Discord servers (guilds) where the bot is active  
* Total number of threads used  
* Number of distinct bot instances (identified by a local, anonymized unique ID)  

### What is **not** collected:
* No personal or sensitive data (no names, Discord IDs, IPs, messages, etc.)  
* No details about threads or server content  

### Why telemetry?
It helps understand adoption, usage, and improve development, while fully respecting user privacy.

### Technical details:
* Telemetry is sent once per day or when a tracker URL is added or removed.  
* Data is transmitted securely over HTTPS to a dedicated server.  
* Each bot instance generates a unique local ID used only to count instances.  
* Duplicate submissions from the same instance are prevented.  
* No personal data is sent.
</details>

