# ArchipelagoSphereTracker
<details>
<summary>🇫🇷 Français</summary>
Un bot Discord pour **suivre des rooms Archipelago**, centraliser les infos de progression des joueurs, et automatiser la gestion des salons/threads.

- Dépôt : https://github.com/Etsuna/ArchipelagoSphereTracker
- Wiki : https://github.com/Etsuna/ArchipelagoSphereTracker/wiki
- Randomizer supporté : https://github.com/ArchipelagoMW/Archipelago
- Bot public (mode Normal) : https://discord.com/oauth2/authorize?client_id=1408901673522430047

---

## Sommaire

- [Vue d’ensemble](#vue-densemble)
- [Fonctionnalités](#fonctionnalités)
  - [Communes (Normal + Archipelago)](#communes-normal--archipelago)
  - [Spécifiques au mode Archipelago](#spécifiques-au-mode-archipelago)
- [Modes d’exécution](#modes-dexécution)
- [Prérequis](#prérequis)
- [Installation rapide (release)](#installation-rapide-release)
- [Configuration `.env` complète](#configuration-env-complète)
- [Permissions Discord requises](#permissions-discord-requises)
- [Commandes Slash](#commandes-slash)
- [Portail Web intégré](#portail-web-intégré)
- [Métriques Prometheus](#métriques-prometheus)
- [Compilation depuis les sources](#compilation-depuis-les-sources)
- [Tests](#tests)
- [Structure du projet](#structure-du-projet)
- [Stockage et données](#stockage-et-données)
- [Dépannage](#dépannage)
- [FAQ](#faq)
- [Licence](#licence)

---

## Vue d’ensemble

ArchipelagoSphereTracker (AST) surveille une ou plusieurs rooms Archipelago et publie automatiquement les événements importants dans Discord : progression, objets reçus, statut des joueurs, récapitulatifs, hints, etc.

Le bot existe en **deux modes** :

1. **Normal Mode** : suivi/monitoring uniquement (idéal pour la plupart des serveurs Discord).
2. **Archipelago Mode** : ajoute les fonctions d’hébergement local liées à Archipelago (gestion YAML/APWorld, génération multiworld, backup, etc.).

---

## Fonctionnalités

### Communes (Normal + Archipelago)

- Multi-serveurs Discord, multi-salons et multi-threads.
- Gestion de rooms avec `/add-url` et `/delete-url`.
- Paramétrage de fréquence de polling (`5m`, `15m`, `30m`, `1h`, `6h`, `12h`, `18h`, `1d`).
- Option silencieuse (`silent`) configurable à la création puis via commande.
- Alias joueurs : ajout/suppression/liste.
- Gestion des items affichés, items exclus et hints.
- Fonctions de recap/clean par alias ou globales.
- Portail web intégré avec pages utilisateur et commandes de thread.
- Automatisation :
  - message lors de nouveaux objets,
  - message de fin d’objectif joueur,
  - suppression auto des threads inactifs (2 semaines).

### Spécifiques au mode Archipelago

- Gestion des fichiers de génération : YAML / APWorld / templates.
- Backup/restauration des assets liés à la génération.
- Génération multiworld (`/generate`, `/test-generate`, `/generate-with-zip`).
- Gestion de compatibilité Linux/Windows autour de l’installation Archipelago.
- Installation/mise à jour des dépendances Archipelago via le binaire AST.

> ℹ️ Le mode Archipelago cible une exécution **x64**.

---

## Modes d’exécution

Au lancement, AST attend un argument :

```bash
--install
--NormalMode
--ArchipelagoMode
--UpdateBDD
--BigAsync
```

### Description des modes

- `--install` : prépare l’environnement Archipelago (backup, installation, restauration).
- `--NormalMode` : mode de suivi Discord classique.
- `--ArchipelagoMode` : active les fonctionnalités de génération/fichiers Archipelago.
- `--UpdateBDD` : exécute la logique de migration BDD puis quitte.
- `--BigAsync` : active un mode asynchrone renforcé (usage avancé).

---

## Prérequis

### Utilisation via release précompilée

- Aucun SDK nécessaire.
- Ajouter uniquement un fichier `.env` valide.

### Compilation locale

- .NET 8 SDK.
- OS supportés pour exécution : Linux et Windows.

---

## Installation rapide (release)

### 1) Télécharger

Depuis les releases :

- Windows x64 : `ast-win-x64-vX.X.X.zip`
- Linux x64 : `ast-linux-x64-vX.X.X.tar.gz`

### 2) Décompresser

Décompressez dans un dossier dédié.

### 3) Configurer

Ajoutez un fichier `.env` dans le même dossier que l’exécutable.

### 4) Démarrer

- Windows : `ArchipelagoSphereTracker.exe --NormalMode` (ou `--ArchipelagoMode`)
- Linux : `./ArchipelagoSphereTracker --NormalMode` (ou `--ArchipelagoMode`)

### 5) (Archipelago Mode uniquement)

- Placer les ROMs nécessaires dans `./extern/Archipelago/` si requis par vos mondes.

---

## Configuration `.env` complète

Variables reconnues :

```dotenv
# Obligatoire
DISCORD_TOKEN=YOUR_DISCORD_BOT_TOKEN

# Optionnel (défaut: en)
LANGUAGE=fr

# Optionnel (défaut: true)
ENABLE_WEB_PORTAL=true

# Optionnel (défaut: 5199)
WEB_PORT=5199

# Optionnel (URL publique pour liens de portail, reverse proxy conseillé)
WEB_BASE_URL=https://your-domain.example

# Optionnel (défaut: false)
EXPORT_METRICS=false

# Optionnel (port metrics si export activé)
METRICS_PORT=9090

# Optionnel (usage interne / mode BigAsync)
USER_ID_FOR_BIG_ASYNC=123456789012345678
```

### Détails importants

- `DISCORD_TOKEN` est indispensable pour connecter le bot.
- `LANGUAGE` supporte `fr` et `en`.
- `ENABLE_WEB_PORTAL=false` désactive totalement le serveur web interne.
- `WEB_PORT` est le port d’écoute HTTP du portail (`0.0.0.0:<port>`).
- `WEB_BASE_URL` est utile si AST est exposé derrière un domaine/proxy.
- `EXPORT_METRICS=true` active les exports Prometheus.

---

## Permissions Discord requises

L’entier de permissions recommandé :

```text
395137117248
```

Permissions associées :

- Voir les salons
- Envoyer des messages
- Créer des fils publics
- Créer des fils privés
- Envoyer des messages dans les threads
- Gérer les messages
- Gérer les fils
- Intégrer des liens
- Joindre des fichiers
- Ajouter des réactions
- Lire l’historique des messages
- Utiliser les commandes Slash

---

## Commandes Slash

## Communes

- Room tracking
  - `/add-url`
  - `/delete-url`
  - `/update-frequency-check`
  - `/update-silent-option`
- Alias / joueurs
  - `/get-aliases`
  - `/add-alias`
  - `/delete-alias`
- Items / patch / hints
  - `/get-patch`
  - `/list-items`
  - `/excluded-item`
  - `/excluded-item-list`
  - `/delete-excluded-item`
  - `/hint-from-finder`
  - `/hint-for-receiver`
- Recap & nettoyage
  - `/recap`
  - `/recap-all`
  - `/clean`
  - `/clean-all`
  - `/recap-and-clean`
- Informations
  - `/status-games-list`
  - `/info`
  - `/discord`
  - `/apworlds-info`
- Portail
  - `/ast-user-portal`
  - `/ast-room-portal`
  - `/ast-portal`

### Archipelago Mode uniquement

- `/list-yamls`
- `/list-apworld`
- `/download-template`
- `/send-yaml`
- `/send-apworld`
- `/delete-yaml`
- `/clean-yamls`
- `/backup-yamls`
- `/backup-apworld`
- `/test-generate`
- `/generate`
- `/generate-with-zip`

---

## Portail Web intégré

Quand `ENABLE_WEB_PORTAL=true`, AST héberge une interface web :

- fichiers statiques sous `/portal`
- endpoints API sous `/api/portal/...`

Fonctions exposées :

- vue synthétique par utilisateur (recap/items/hints),
- ajout/suppression d’alias,
- suppression d’éléments de recap,
- pages HTML de commandes de room/thread.

### Sécurité recommandée

- Exposer via un reverse proxy HTTPS (Nginx/Caddy/Traefik).
- Filtrer l’accès par IP ou auth externe si nécessaire.
- Ne pas exposer le serveur sans protection en environnement public.

---

## Métriques Prometheus

Si `EXPORT_METRICS=true`, AST publie des métriques exploitables par Prometheus.

Exemples de familles :

- `ast_channel_info`
- `ast_channel_last_check_seconds`
- `ast_game_status_checks`
- `ast_game_status_total`
- `ast_game_status_last_activity_seconds`
- `ast_alias_choice`
- `ast_last_items_checked_timestamp`

Utilité : supervision de la fraîcheur des données, activité des rooms, volumétrie de suivi.

---

## Compilation depuis les sources

```bash
# 1) Cloner
git clone https://github.com/Etsuna/ArchipelagoSphereTracker.git
cd ArchipelagoSphereTracker

# 2) Configurer .env
cp .env.example .env 2>/dev/null || true
# puis éditer .env

# 3) Restaurer / compiler
dotnet restore
dotnet build --configuration Release

# 4) Publier Windows x64
dotnet publish ArchipelagoSphereTracker.csproj -c Release -r win-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true /p:Version=X.X.X

# 5) Publier Linux x64
dotnet publish ArchipelagoSphereTracker.csproj -c Release -r linux-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true /p:Version=X.X.X
```

Binaire final attendu :

- Windows : `bin/Release/net8.0/win-x64/publish/ArchipelagoSphereTracker.exe`
- Linux : `bin/Release/net8.0/linux-x64/publish/ArchipelagoSphereTracker`

---

## Tests

Depuis la racine :

```bash
dotnet test
```

Le projet inclut des tests unitaires sur parsing/convertisseurs/services DB et commandes.

---

## Structure du projet

```text
src/
  Bot/                # commandes Discord, logique principale bot
  SqlCommands/        # accès SQLite + migrations
  Web/                # portail web intégré (pages/API)
  TrackerLib/         # parsing stream, datapackage, modèles
  Install/            # installation/backup Archipelago
tests/
  ArchipelagoSphereTracker.Tests/
Gui/
  # projet GUI annexe
apworld/
  # assets/templates apworld
Install/
  # scripts d'installation distribués
```

---

## Stockage et données

- Base SQLite locale : `AST.db`
- Le bot maintient des tables de channels, alias, statut de jeu, hints, recap, etc.
- Les migrations BDD sont gérées automatiquement au démarrage si nécessaire.

---

## Dépannage

### Le bot ne démarre pas

- Vérifier `DISCORD_TOKEN` dans `.env`.
- Vérifier l’argument de lancement (`--NormalMode` ou `--ArchipelagoMode`).
- Vérifier que l’OS est Windows ou Linux.

### Les commandes slash n’apparaissent pas

- Vérifier les permissions OAuth2/bot sur le serveur.
- Vérifier que le bot est bien connecté.
- Attendre quelques instants après ajout du bot (propagation Discord).

### Le portail web est inaccessible

- Vérifier `ENABLE_WEB_PORTAL=true`.
- Vérifier `WEB_PORT` libre et exposé.
- Si reverse proxy : vérifier redirection vers le bon port local.

### Erreurs liées à la génération multiworld

- Vérifier que le mode utilisé est `--ArchipelagoMode`.
- Vérifier la présence des fichiers requis (`yaml`, `apworld`, ROMs selon besoins).
- Rejouer l’installation avec `--install` si l’environnement Archipelago est incomplet.

---

## FAQ

### Quels jeux sont supportés ?

Tous les jeux supportés par Archipelago MultiWorld sont potentiellement utilisables en multiworld.

### Puis-je héberger AST sur un VPS ?

Oui, en Linux x64 c’est un cas d’usage courant. Prévoir un service systemd + reverse proxy si portail activé.

### Le mode Normal suffit-il pour suivre une room ?

Oui. Le mode Archipelago est surtout utile pour la génération/gestion de fichiers côté serveur.

---

## Licence

Ce projet est distribué sous licence MIT. Voir le fichier [LICENSE](LICENSE).

</details>

<details open>
<summary>🇬🇧 English</summary>
  
ArchipelagoSphereTracker is a Discord bot to **track Archipelago rooms**, centralize player progression data, and automate thread/channel operations.

- Repository: https://github.com/Etsuna/ArchipelagoSphereTracker
- Wiki: https://github.com/Etsuna/ArchipelagoSphereTracker/wiki
- Supported randomizer: https://github.com/ArchipelagoMW/Archipelago
- Public bot (Normal mode): https://discord.com/oauth2/authorize?client_id=1408901673522430047

---

## Table of contents

- [Overview](#overview)
- [Features](#features)
  - [Shared (Normal + Archipelago)](#shared-normal--archipelago)
  - [Archipelago mode only](#archipelago-mode-only)
- [Run modes](#run-modes)
- [Requirements](#requirements)
- [Quick install (release)](#quick-install-release)
- [Full `.env` configuration](#full-env-configuration)
- [Required Discord permissions](#required-discord-permissions)
- [Slash commands](#slash-commands)
- [Built-in web portal](#built-in-web-portal)
- [Prometheus metrics](#prometheus-metrics)
- [Build from source](#build-from-source)
- [Tests](#tests-1)
- [Project structure](#project-structure)
- [Storage and data](#storage-and-data)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq-1)
- [License](#license-1)

---

## Overview

ArchipelagoSphereTracker (AST) monitors one or more Archipelago rooms and posts key events to Discord: progression updates, received items, player status, recaps, hints, and more.

AST supports **two modes**:

1. **Normal Mode**: tracking/monitoring only (recommended for most Discord servers).
2. **Archipelago Mode**: adds local hosting capabilities for Archipelago assets (YAML/APWorld management, multiworld generation, backups, etc.).

---

## Features

### Shared (Normal + Archipelago)

- Multi-server, multi-channel, multi-thread support.
- Room lifecycle management through `/add-url` and `/delete-url`.
- Configurable polling frequency (`5m`, `15m`, `30m`, `1h`, `6h`, `12h`, `18h`, `1d`).
- Silent option configurable at room creation and later updates.
- Player alias management (add/remove/list).
- Displayed/excluded items and hints management.
- Recap/cleanup tools by alias or globally.
- Built-in web portal with user and thread command pages.
- Automation:
  - automatic messages for newly received items,
  - completion message when a player reaches their goal,
  - automatic deletion of inactive threads (2 weeks).

### Archipelago mode only

- Generation file management: YAML / APWorld / templates.
- Backup/restore for generation-related assets.
- Multiworld generation (`/generate`, `/test-generate`, `/generate-with-zip`).
- Linux/Windows compatibility handling for Archipelago setup.
- Archipelago install/update orchestration through AST.

> ℹ️ Archipelago mode targets **x64** runtime.

---

## Run modes

AST expects one startup argument:

```bash
--install
--NormalMode
--ArchipelagoMode
--UpdateBDD
--BigAsync
```

### Mode details

- `--install`: prepare Archipelago environment (backup, install, restore).
- `--NormalMode`: standard Discord tracking mode.
- `--ArchipelagoMode`: enables generation and Archipelago file management.
- `--UpdateBDD`: run DB migration logic and exit.
- `--BigAsync`: enables advanced async behavior.

---

## Requirements

### Using prebuilt releases

- No SDK required.
- Only a valid `.env` file is needed.

### Building locally

- .NET 8 SDK.
- Supported runtime OS: Linux and Windows.

---

## Quick install (release)

### 1) Download

From releases:

- Windows x64: `ast-win-x64-vX.X.X.zip`
- Linux x64: `ast-linux-x64-vX.X.X.tar.gz`

### 2) Extract

Extract to a dedicated folder.

### 3) Configure

Add a `.env` file in the same folder as the executable.

### 4) Start

- Windows: `ArchipelagoSphereTracker.exe --NormalMode` (or `--ArchipelagoMode`)
- Linux: `./ArchipelagoSphereTracker --NormalMode` (or `--ArchipelagoMode`)

### 5) (Archipelago mode only)

- Place required ROMs under `./extern/Archipelago/` if needed for your worlds.

---

## Full `.env` configuration

Recognized variables:

```dotenv
# Required
DISCORD_TOKEN=YOUR_DISCORD_BOT_TOKEN

# Optional (default: en)
LANGUAGE=en

# Optional (default: true)
ENABLE_WEB_PORTAL=true

# Optional (default: 5199)
WEB_PORT=5199

# Optional (public URL for portal links, reverse proxy recommended)
WEB_BASE_URL=https://your-domain.example

# Optional (default: false)
EXPORT_METRICS=false

# Optional (metrics port when export is enabled)
METRICS_PORT=9090

# Optional (internal usage / BigAsync mode)
USER_ID_FOR_BIG_ASYNC=123456789012345678
```

### Important notes

- `DISCORD_TOKEN` is required for bot login.
- `LANGUAGE` supports `fr` and `en`.
- `ENABLE_WEB_PORTAL=false` disables the web server entirely.
- `WEB_PORT` defines the portal HTTP bind port (`0.0.0.0:<port>`).
- `WEB_BASE_URL` is useful behind a domain/reverse proxy.
- `EXPORT_METRICS=true` enables Prometheus exports.

---

## Required Discord permissions

Recommended permission integer:

```text
395137117248
```

Permissions included:

- View channels
- Send messages
- Create public threads
- Create private threads
- Send messages in threads
- Manage messages
- Manage threads
- Embed links
- Attach files
- Add reactions
- Read message history
- Use Slash Commands

---

## Slash commands

### Shared

- Room tracking
  - `/add-url`
  - `/delete-url`
  - `/update-frequency-check`
  - `/update-silent-option`
- Alias / players
  - `/get-aliases`
  - `/add-alias`
  - `/delete-alias`
- Items / patch / hints
  - `/get-patch`
  - `/list-items`
  - `/excluded-item`
  - `/excluded-item-list`
  - `/delete-excluded-item`
  - `/hint-from-finder`
  - `/hint-for-receiver`
- Recap & cleanup
  - `/recap`
  - `/recap-all`
  - `/clean`
  - `/clean-all`
  - `/recap-and-clean`
- Information
  - `/status-games-list`
  - `/info`
  - `/discord`
  - `/apworlds-info`
- Portal
  - `/ast-user-portal`
  - `/ast-room-portal`
  - `/ast-portal`

### Archipelago mode only

- `/list-yamls`
- `/list-apworld`
- `/download-template`
- `/send-yaml`
- `/send-apworld`
- `/delete-yaml`
- `/clean-yamls`
- `/backup-yamls`
- `/backup-apworld`
- `/test-generate`
- `/generate`
- `/generate-with-zip`

---

## Built-in web portal

When `ENABLE_WEB_PORTAL=true`, AST serves a web interface:

- static pages under `/portal`
- API endpoints under `/api/portal/...`

Exposed capabilities include:

- user summary view (recap/items/hints),
- alias add/remove,
- recap item removal,
- room/thread command pages.

### Recommended security

- Expose through an HTTPS reverse proxy (Nginx/Caddy/Traefik).
- Restrict access (IP filtering and/or upstream auth) when needed.
- Avoid exposing raw service publicly without protection.

---

## Prometheus metrics

If `EXPORT_METRICS=true`, AST exposes Prometheus-consumable metrics.

Examples:

- `ast_channel_info`
- `ast_channel_last_check_seconds`
- `ast_game_status_checks`
- `ast_game_status_total`
- `ast_game_status_last_activity_seconds`
- `ast_alias_choice`
- `ast_last_items_checked_timestamp`

Use cases: data freshness monitoring, room activity tracking, and operational observability.

---

## Build from source

```bash
# 1) Clone
git clone https://github.com/Etsuna/ArchipelagoSphereTracker.git
cd ArchipelagoSphereTracker

# 2) Configure .env
cp .env.example .env 2>/dev/null || true
# then edit .env

# 3) Restore / build
dotnet restore
dotnet build --configuration Release

# 4) Publish Windows x64
dotnet publish ArchipelagoSphereTracker.csproj -c Release -r win-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true /p:Version=X.X.X

# 5) Publish Linux x64
dotnet publish ArchipelagoSphereTracker.csproj -c Release -r linux-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeAllContentForSelfExtract=true /p:Version=X.X.X
```

Expected output binaries:

- Windows: `bin/Release/net8.0/win-x64/publish/ArchipelagoSphereTracker.exe`
- Linux: `bin/Release/net8.0/linux-x64/publish/ArchipelagoSphereTracker`

---

## Tests

From repository root:

```bash
dotnet test
```

The repository includes unit tests for parsers, converters, DB services, and command definitions.

---

## Project structure

```text
src/
  Bot/                # Discord commands and core bot logic
  SqlCommands/        # SQLite access + migrations
  Web/                # built-in web portal (pages/API)
  TrackerLib/         # stream parser, datapackage, models
  Install/            # Archipelago setup/backup flows
tests/
  ArchipelagoSphereTracker.Tests/
Gui/
  # auxiliary GUI project
apworld/
  # apworld assets/templates
Install/
  # distributed install scripts
```

---

## Storage and data

- Local SQLite database: `AST.db`
- The bot stores channels, aliases, game status, hints, recap data, etc.
- DB migrations are applied automatically at startup when needed.

---

## Troubleshooting

### Bot does not start

- Check `DISCORD_TOKEN` in `.env`.
- Check startup argument (`--NormalMode` or `--ArchipelagoMode`).
- Confirm runtime OS is Linux or Windows.

### Slash commands do not appear

- Check OAuth2/bot permissions on the Discord server.
- Confirm the bot is online and connected.
- Wait a short time after inviting the bot (Discord propagation).

### Web portal is unreachable

- Check `ENABLE_WEB_PORTAL=true`.
- Ensure `WEB_PORT` is free and exposed.
- If using reverse proxy, verify routing to the local port.

### Multiworld generation errors

- Ensure launch mode is `--ArchipelagoMode`.
- Verify required files exist (`yaml`, `apworld`, ROMs when needed).
- Run `--install` again if the Archipelago environment is incomplete.

---

## FAQ

### Which games are supported?

All games supported by Archipelago MultiWorld are potentially usable together in multiworld.

### Can I host AST on a VPS?

Yes. Linux x64 is a common deployment target. Prefer systemd + reverse proxy when portal is enabled.

### Is Normal mode enough for room tracking?

Yes. Archipelago mode is mostly needed for server-side generation/file management.

---

## License

This project is distributed under the MIT License. See [LICENSE](LICENSE).

</details>
