# ArchipelagoSphereTracker
<details>
<summary>🇫🇷 Français</summary>

Un bot Discord pour **suivre des rooms Archipelago**, centraliser les infos de progression des joueurs, et automatiser la gestion des salons/threads.

- Site web : https://www.ast-bot.com/
- Dépôt : https://github.com/Etsuna/ArchipelagoSphereTracker
- Companion de bureau : https://github.com/Etsuna/ASTCompanion
- Wiki : https://github.com/Etsuna/ArchipelagoSphereTracker/wiki
- Randomizer supporté : https://github.com/ArchipelagoMW/Archipelago et Apworld
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
- [Préparation des rooms (Archipelago)](#préparation-des-rooms-archipelago)
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
2. **Archipelago Mode** : ajoute la préparation des rooms, la gestion YAML/APWorld, la génération multiworld et les sauvegardes de l’environnement Archipelago local. L’hébergement de la seed générée reste une étape manuelle.

---

## Fonctionnalités

### Communes (Normal + Archipelago)

- Multi-serveurs Discord, multi-salons et multi-threads.
- Centre de commandes éphémère `/ast` : toute l’interface Discord (configuration, rooms, alias, récaps, exclusions, portails et administration) est accessible depuis une seule commande slash, en complément des commandes historiques.
- Assistant interactif intégré à `/ast` pour connecter une room, configurer le thread, les notifications et le polling, puis confirmer depuis un aperçu éphémère.
- Polling automatique par défaut : une minute lorsque la room est active ou que son activité est inconnue. Une inactivité connue et des réponses inchangées permettent un ralentissement progressif à deux, quatre, puis au maximum cinq minutes.
- Mode fixe optionnel (`5m`, `15m`, `30m`, `1h`, `6h`, `12h`, `18h`, `1d`) : la fréquence n’est demandée que dans ce mode.
- Santé et contrôle du suivi depuis `/ast` (également disponibles dans le portail de room).
- Option silencieuse (`silent`) configurable à la création puis via commande.
- Alias joueurs : ajout/suppression/liste.
- Gestion des items affichés, items exclus et hints.
- Fonctions de recap/clean par alias ou globales.
- Portail web intégré avec navigation latérale, tableaux compacts, recherche et pages utilisateur, room et administration.
- Automatisation :
  - message lors de nouveaux objets,
  - message de fin d’objectif joueur,
  - avertissement après sept jours sans nouvel objet, puis arrêt du suivi et suppression des données AST de la room après quatorze jours. Le thread Discord est conservé.

Les délais dépendent aussi du WebHost, des files d’attente et des éventuelles erreurs réseau. Voir [le fonctionnement du suivi et du polling](wiki/Room-tracking.md).

### Spécifiques au mode Archipelago

- Entrée principale `Outils Archipelago` dans `/ast`, avec quatre rubriques séparées : YAML, APWorld, Génération et Modèles.
- Outils Archipelago accessibles par défaut à tous les membres, avec restrictions individuelles possibles. La création des préparations est réservée aux gestionnaires reconnus par AST ; leurs contrôles de gestion sont réservés à l’organisateur.
- Backup/restauration des assets liés à la génération.
- Génération multiworld (`/generate`, `/test-generate`, `/generate-with-zip`).
- Préparation collaborative via `/organize-room` : inscriptions, YAML personnels, validation Archipelago, disponibilité et relance ciblée des joueurs non prêts.
- Envoi groupé de YAML : sélection multiple sur le Web ou ZIP via Discord, dans la même préparation.
- ZIP généré envoyé dans le salon courant, ou lien de téléchargement temporaire ; nettoyage de la préparation après transmission réussie ou annulation.
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
--gui
```

### Description des modes

- `--install` : prépare l’environnement Archipelago (backup, installation, restauration).
- `--NormalMode` : mode de suivi Discord classique.
- `--ArchipelagoMode` : active les fonctionnalités de génération/fichiers Archipelago.
- `--UpdateBDD` : exécute la logique de migration BDD puis quitte.
- `--BigAsync` : mode de suivi avancé conservant un polling fixe d’une heure.
- `--gui` : lance l’interface desktop native d’administration (édition `.env`, test Discord, start/stop bot, logs live).

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
- GUI Desktop (admin) :
  - Windows : `ArchipelagoSphereTracker.exe --gui`
  - Linux : `./ArchipelagoSphereTracker --gui`

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

# Recommandé : utilisateur Discord propriétaire de l'instance AST
AST_OWNER_USER_ID=123456789012345678

# Optionnel (défaut : 67108864, soit 64 Mio)
WEB_MAX_UPLOAD_BYTES=67108864

# Optionnel (défaut : 60 minutes, intervalle accepté : 5–1440)
UPLOAD_QUARANTINE_RETENTION_MINUTES=60

# Optionnel (défaut : 30 jours, intervalle accepté : 1–365)
SPOILER_LOG_RETENTION_DAYS=30

# Optionnel (défaut : 30 jours, intervalle accepté : 1–365)
PORTAL_TOKEN_LIFETIME_DAYS=30

# Optionnel (défaut : 90 jours, intervalle accepté : 1–3650)
AUDIT_RETENTION_DAYS=90

# Modèle V2 expérimental (non branché sur les notifications en production)
ENABLE_TRACKING_V2=false

# Rollback temporaire vers l'ancien scan minute
USE_LEGACY_TRACKING_SCHEDULER=false

# Budgets du scheduler/client WebHost central
TRACKING_GLOBAL_CONCURRENCY=10
TRACKING_ORIGIN_CONCURRENCY=2

# Optionnel : hôtes privés explicitement approuvés pour Archipelago
ARCHIPELAGO_ALLOWED_HOSTS=ap.example.lan

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
- `AST_OWNER_USER_ID` désigne le super-administrateur facultatif de l'instance. Il peut administrer et nettoyer tous les serveurs/salons enregistrés depuis `Instance AST` dans `/ast`. Sans valeur, aucun utilisateur ne reçoit ce pouvoir global ; les administrateurs Discord gardent la gestion complète de leur propre serveur.
- Depuis la migration SQLite `5.0.12`, les identifiants de room, trackers et liens de patch sont stockés directement en clair. Aucune clé de chiffrement n'est requise. Voir [le stockage SQLite des identifiants](docs/data-protection-at-rest.fr.md).
- `WEB_MAX_UPLOAD_BYTES` limite la taille des fichiers Web et Discord acceptés.
- Les préparations imposent aussi leurs propres limites : 1 Mio par YAML, 20 fichiers par joueur, 16 Mio de YAML au total et 16 Mio par ZIP envoyé.
- Pour publier un lien de secours vers le ZIP généré, le portail doit être actif et `WEB_BASE_URL` doit être une URL publique accessible aux joueurs. Ce lien expire après 24 heures.
- `UPLOAD_QUARANTINE_RETENTION_MINUTES` fixe le délai de nettoyage des téléversements interrompus placés en quarantaine.
- `SPOILER_LOG_RETENTION_DAYS` fixe la rétention des fichiers spoiler `.txt` et `.json`.
- `PORTAL_TOKEN_LIFETIME_DAYS` fixe l'expiration des nouveaux liens du portail.
- `AUDIT_RETENTION_DAYS` fixe la rétention du journal des actions sensibles.
- `ENABLE_TRACKING_V2=true` active le dual-write expérimental des snapshots, événements et livraisons V2. Aucun worker Discord V2 n'est démarré dans cette version : les notifications historiques restent seules publiées.
- `USE_LEGACY_TRACKING_SCHEDULER=true` réactive temporairement l'ancien scan minute ; le scheduler central est utilisé par défaut.
- Le mode historique désactive les commandes de pause/reprise/synchronisation forcée et ne tient pas compte des pauses persistées. Revenir au scheduler central restaure leur application.
- `TRACKING_GLOBAL_CONCURRENCY` et `TRACKING_ORIGIN_CONCURRENCY` bornent respectivement les requêtes WebHost simultanées du processus et d'une même origine.
- `ARCHIPELAGO_ALLOWED_HOSTS` est une liste séparée par des virgules. Elle constitue une dérogation explicite au blocage SSRF des adresses privées/locales.
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

AST enregistre `/ast` ainsi que les commandes historiques de la version `v5.6.7`. Les utilisateurs peuvent employer indifféremment l’interface centralisée ou les commandes directes.

- `/ast` ouvre un centre de commandes personnel et éphémère, adapté au salon, à la room et aux permissions de l’utilisateur.
- `/ast file:<fichier>` importe un YAML, un APWorld, un ZIP de génération ou un spoiler selon le mode et les permissions.
- `/organize-room` crée et gère une préparation en mode Archipelago. Dans son salon, `/send-yaml file:<fichier>` et `/ast file:<fichier>` ajoutent un YAML ou un ZIP de YAML à cette même préparation, sans lancer la génération.
- Les mêmes fonctions restent disponibles dans les écrans `Mon espace`, `La room`, `Gérer la room`, `Outils Archipelago` et `Administration AST`.
- Les grandes listes sont paginées ; les exclusions disposent en plus d’une recherche.
- Les actions sensibles sont revérifiées côté serveur et inscrites dans le journal de sécurité.
- Les propriétaires, administrateurs et membres ayant `Gérer le serveur` peuvent déléguer la gestion AST à un membre sans lui donner de rôle administrateur Discord. Le responsable délégué ne peut pas redéléguer ce droit.

Voir [le centre `/ast`](wiki/AST-command-center.md) et [la correspondance des commandes historiques](wiki/Legacy-command-migration.md).

---

## Préparation des rooms (Archipelago)

1. Lancez `/organize-room` et choisissez un nom. AST publie le message de préparation **dans le salon courant**, ou dans le thread existant. Aucun nouveau thread n’est créé. La capacité est de **200 joueurs et 200 slots au total**.
2. Les joueurs s’inscrivent puis déposent leurs YAML sur la page Web ou par commande Discord dans ce salon. Le Web accepte plusieurs fichiers à la fois ; Discord accepte un ZIP via `/send-yaml file:<joueurs.zip>`, `/ast file:<joueurs.zip>` ou `/organize-room fichier:<joueurs.zip>` (`file` lorsque le bot est en anglais).
3. Chaque YAML est validé par Archipelago. La page distingue l’attente, la validation en cours et les erreurs, avec le diagnostic du générateur. Après correction du YAML ou installation d’un APWorld manquant, renvoyez le fichier. Tous les fichiers d’un joueur doivent être valides pour qu’il puisse se déclarer **Prêt**.
4. L’organisateur peut fermer/rouvrir les inscriptions, tester l’ensemble et relancer les inscrits ayant un YAML mais n’étant pas prêts, au maximum une fois toutes les cinq minutes. Il lance **Générer** lorsque tous les inscrits sont prêts.

Chaque joueur gère ses propres fichiers. Les lots sont contrôlés avant tout remplacement ; un lot refusé conserve les fichiers et la disponibilité existants. Les validations Archipelago sont ensuite réalisées fichier par fichier. Les limites sont de **20 fichiers par joueur**, **1 Mio par YAML** et **16 Mio de YAML par préparation** ; le ZIP et son contenu décompressé sont également limités à 16 Mio chacun.

- **Annulation** : suppression des données de préparation et de ses fichiers de travail.
- **Génération réussie** : transmission du ZIP dans le même salon, puis suppression des données de préparation. Si la pièce jointe est impossible, AST publie d’abord un lien valable **24 heures** ; seule une copie temporaire du ZIP subsiste jusqu’à expiration. Toute personne possédant ce lien peut télécharger l’archive.
- **Transmission impossible** : conservation de la préparation et des fichiers, avec une action permettant de réessayer. Un échec de génération permet également de corriger puis relancer.

Hébergez ensuite le ZIP sur votre serveur Archipelago ou archipelago.gg et connectez la room avec `/add-url` ou l’assistant `/ast`. La préparation utilise SQLite et ne demande aucun serveur de base de données supplémentaire.

Voir [le guide complet des préparations](wiki/Room-preparation.md) pour les permissions, limites, validations et règles de nettoyage.

---

## Portail Web intégré

Quand `ENABLE_WEB_PORTAL=true`, AST héberge des pages sous `/portal` et leurs API sous `/api/portal/...`. Une navigation latérale, des tableaux compacts et des outils de recherche facilitent la consultation des grandes rooms.

- **Utilisateur** : progression, objets, hints, alias et récapitulatifs personnels.
- **Room** : tableau des joueurs, progression, état du suivi et dernière synchronisation.
- **Administration** : configuration et contrôles accessibles selon les permissions Discord/AST.
- **Préparation** (mode Archipelago) : inscriptions, envoi multiple de YAML, erreurs de validation, disponibilité et génération.

Les liens personnels du portail sont associés à un utilisateur, un serveur et un salon ; les permissions sont revérifiées côté serveur. Les liens temporaires du ZIP généré utilisent une autorisation distincte et restent valables après suppression de la préparation, jusqu’à leur expiration.

Voir [le guide du portail Web](wiki/Web-portal.md).

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
- `ast_command_invocations_total{surface,command}`
- `ast_command_options_total{surface,command,option,selection}`
- `ast_command_outcomes_total{surface,command,outcome}`
- `ast_command_duration_seconds`
- `ast_tracked_rooms`, `ast_tracked_guilds`, `ast_tracked_slots`
- `ast_room_poll_states{state}`, `ast_event_deliveries{state}`
- `ast_portal_tokens{state}`, `ast_archipelago_access_restrictions`
- `ast_metrics_collection_success`, `ast_metrics_collection_failures_total`
- `http_requests_received_total`, `http_request_duration_seconds`

Le label `surface` distingue `discord_slash`, les boutons/sélecteurs/modales de `/ast` et `web_portal`. Les valeurs libres ne sont jamais exportées : alias, URLs, recherches, noms de salons, IDs utilisateur et noms de fichiers sont réduits à `provided`; seule une petite liste de choix bornés et d’extensions est conservée.

Exemple PromQL pour les commandes les plus utilisées sur 30 jours :

```promql
sum by (surface, command) (increase(ast_command_invocations_total[30d]))
```

```promql
sum by (surface, command, option, selection) (increase(ast_command_options_total[30d]))
```

---

## Compilation depuis les sources

```bash
# 1) Cloner
git clone https://github.com/Etsuna/ArchipelagoSphereTracker.git
cd ArchipelagoSphereTracker

# 2) Créer .env avec la configuration présentée ci-dessus

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
dotnet test ArchipelagoSphereTracker.sln
```

La solution racine couvre le parsing WebHost, les convertisseurs, les services DB, les commandes et les fixtures anonymisées, ainsi que les préparations : API Web, lots YAML/ZIP, validations, relances et nettoyage. La CI exécute cette suite avant de publier les binaires Windows et Linux.

---

## Structure du projet

```text
src/
  Bot/                # commandes Discord, logique principale bot
  SqlCommands/        # accès SQLite + migrations
  Web/                # portail web intégré (pages/API)
  Preparations/       # inscriptions, YAML, validation et génération
  Gui/                # GUI desktop native Avalonia (--gui)
  TrackerLib/         # parsing stream, datapackage, modèles
  Install/            # installation/backup Archipelago
tests/
  ArchipelagoSphereTracker.Tests/
apworld/
  # assets/templates apworld
Install/
  # scripts d'installation distribués
```

---

## Stockage et données

- Une base SQLite locale pour l’instance : `AST.db`. Aucun serveur PostgreSQL n’est nécessaire.
- Le bot maintient des tables de channels, alias, statut de jeu, hints, recap et préparation (inscriptions, contenu YAML, validations, instantanés et reçus de transmission).
- Les migrations BDD sont gérées automatiquement au démarrage si nécessaire.
- Les fichiers de travail des préparations se trouvent dans `data/room-preparations/`. L’annulation et la transmission réussie déclenchent le nettoyage de leurs données ; les copies ZIP pour téléchargement expirent après 24 heures. Les audits techniques, sauvegardes et rooms suivies restent indépendants de ce nettoyage.
- Pour sauvegarder l’instance, arrêtez le bot puis conservez `AST.db`, `.env`, `extern/` et `data/`. Protégez ces copies, qui peuvent contenir des YAML et des identifiants privés.

Voir [les données et migrations](wiki/Data-and-migrations.md).

---

## Dépannage

### Le bot ne démarre pas

- Vérifier `DISCORD_TOKEN` dans `.env`.
- Vérifier l’argument de lancement (`--NormalMode` ou `--ArchipelagoMode`).
- Vérifier que l’OS est Windows ou Linux.

### Le GUI ne démarre pas sur Linux serveur

- En environnement headless (sans session graphique), `--gui` ne peut pas s’ouvrir.
- Dans ce cas, utilisez `--NormalMode` / `--ArchipelagoMode` côté serveur, ou lancez `--gui` depuis une machine desktop.

### Les commandes slash n’apparaissent pas

- Vérifier les permissions OAuth2/bot sur le serveur.
- Vérifier que le bot est bien connecté.
- Après mise à jour, redémarrer le bot pour enregistrer les commandes. `/organize-room` demande le mode Archipelago.
- Attendre quelques instants après ajout du bot (propagation Discord).

### Le portail web est inaccessible

- Vérifier `ENABLE_WEB_PORTAL=true`.
- Vérifier `WEB_PORT` libre et exposé.
- Si reverse proxy : vérifier redirection vers le bon port local.

### Erreurs liées à la génération multiworld

- Vérifier que le mode utilisé est `--ArchipelagoMode`.
- Vérifier la présence des fichiers requis (`yaml`, `apworld`, ROMs selon besoins).
- Rejouer l’installation avec `--install` si l’environnement Archipelago est incomplet.
- Dans une préparation, consulter le diagnostic de chaque YAML sur la page Web. Après correction ou ajout d’un APWorld, renvoyer le fichier pour relancer sa validation.

### Un YAML envoyé n’apparaît pas dans `/list-yamls`

Dans un salon de préparation, `/send-yaml` et `/ast file:` alimentent la préparation commune au Web et à Discord. Consultez sa page ou le statut de `/organize-room` ; `/list-yamls` affiche l’ancien dossier de génération utilisé hors préparation.

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

- Website: https://www.ast-bot.com/
- Repository: https://github.com/Etsuna/ArchipelagoSphereTracker
- Desktop companion: https://github.com/Etsuna/ASTCompanion
- Wiki: https://github.com/Etsuna/ArchipelagoSphereTracker/wiki
- Supported randomizer: https://github.com/ArchipelagoMW/Archipelago and Apworld
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
- [Room preparation (Archipelago)](#room-preparation-archipelago)
- [Built-in web portal](#built-in-web-portal)
- [Prometheus metrics](#prometheus-metrics)
- [Build from source](#build-from-source)
- [Tests](#tests-1)
- [Project structure](#project-structure)
- [Storage and data](#storage-and-data)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq-1)
- [License](#license)

---

## Overview

ArchipelagoSphereTracker (AST) monitors one or more Archipelago rooms and posts key events to Discord: progression updates, received items, player status, recaps, hints, and more.

AST supports **two modes**:

1. **Normal Mode**: tracking/monitoring only (recommended for most Discord servers).
2. **Archipelago Mode**: adds room preparation, YAML/APWorld management, multiworld generation, and backups of the local Archipelago environment. Hosting the generated seed remains a manual step.

---

## Features

### Shared (Normal + Archipelago)

- Multi-server, multi-channel, multi-thread support.
- Ephemeral `/ast` command center: all Discord workflows (setup, rooms, aliases, recaps, exclusions, portals, and administration) are accessible from one slash command, alongside the historical commands.
- Interactive assistant embedded in `/ast` to connect a room, configure its thread, notifications, and polling, then confirm from an ephemeral preview.
- Automatic polling by default: one minute while the room is active or its activity is unknown. Known inactivity and unchanged responses allow progressive slowdown to two, four, then at most five minutes.
- Optional fixed mode (`5m`, `15m`, `30m`, `1h`, `6h`, `12h`, `18h`, `1d`): a frequency is requested only in this mode.
- Tracking health and controls from `/ast` (also available in the room portal).
- Silent option configurable at room creation and later updates.
- Player alias management (add/remove/list).
- Displayed/excluded items and hints management.
- Recap/cleanup tools by alias or globally.
- Built-in web portal with sidebar navigation, compact tables, search, and user, room, and administration pages.
- Automation:
  - automatic messages for newly received items,
  - completion message when a player reaches their goal,
  - a warning after seven days without a new item, then tracking stops and the room’s AST data is deleted after fourteen days. The Discord thread is retained.

Observed delays also depend on the WebHost, queues, and possible network errors. See [room tracking and polling](wiki/Room-tracking.md).

### Archipelago mode only

- Top-level `Archipelago tools` entry in `/ast`, with separate YAML, APWorld, Generation, and Templates sections.
- Archipelago tools are available to every member by default, with individual restrictions possible. Creating preparations requires an AST-recognized manager; their management controls are reserved for the organizer.
- Backup/restore for generation-related assets.
- Multiworld generation (`/generate`, `/test-generate`, `/generate-with-zip`).
- Collaborative preparation through `/organize-room`: registration, personal YAML files, Archipelago validation, readiness, and targeted reminders for unready players.
- Batch YAML uploads: multiple selection on the Web or a ZIP through Discord, in the same preparation.
- Generated ZIP delivery in the current channel, or a temporary download link; preparation cleanup after successful delivery or cancellation.
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
--gui
```

### Mode details

- `--install`: prepare Archipelago environment (backup, install, restore).
- `--NormalMode`: standard Discord tracking mode.
- `--ArchipelagoMode`: enables generation and Archipelago file management.
- `--UpdateBDD`: run DB migration logic and exit.
- `--BigAsync`: advanced tracking mode retaining fixed one-hour polling.
- `--gui`: starts the native desktop admin UI (edit `.env`, test Discord, start/stop bot, live logs).

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
- Desktop GUI (admin):
  - Windows: `ArchipelagoSphereTracker.exe --gui`
  - Linux: `./ArchipelagoSphereTracker --gui`

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

# Recommended: Discord user who owns this AST instance
AST_OWNER_USER_ID=123456789012345678

# Optional (default: 67108864, i.e. 64 MiB)
WEB_MAX_UPLOAD_BYTES=67108864

# Optional (default: 60 minutes, accepted range: 5–1440)
UPLOAD_QUARANTINE_RETENTION_MINUTES=60

# Optional (default: 30 days, accepted range: 1–365)
SPOILER_LOG_RETENTION_DAYS=30

# Optional (default: 30 days, accepted range: 1–365)
PORTAL_TOKEN_LIFETIME_DAYS=30

# Optional (default: 90 days, accepted range: 1–3650)
AUDIT_RETENTION_DAYS=90

# Experimental V2 model (not wired to production notifications)
ENABLE_TRACKING_V2=false

# Temporary rollback to the legacy minute scan
USE_LEGACY_TRACKING_SCHEDULER=false

# Central scheduler/WebHost client budgets
TRACKING_GLOBAL_CONCURRENCY=10
TRACKING_ORIGIN_CONCURRENCY=2

# Optional: explicitly trusted private Archipelago hosts
ARCHIPELAGO_ALLOWED_HOSTS=ap.example.lan

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
- `AST_OWNER_USER_ID` designates the optional instance super-administrator. That user can manage and clean every stored guild/channel from `AST instance` in `/ast`. When unset, nobody receives global authority; Discord administrators retain full control of their own guild.
- Since SQLite migration `5.0.12`, room identifiers, trackers, and patch links are stored directly as plaintext. No encryption key is required. See [SQLite identifier storage](docs/data-protection-at-rest.en.md).
- `WEB_MAX_UPLOAD_BYTES` limits accepted Web and Discord file sizes.
- Preparations also enforce their own limits: 1 MiB per YAML, 20 files per player, 16 MiB of YAML in total, and 16 MiB per uploaded ZIP.
- Publishing a fallback link to the generated ZIP requires an active portal and a public `WEB_BASE_URL` reachable by players. The link expires after 24 hours.
- `UPLOAD_QUARANTINE_RETENTION_MINUTES` controls cleanup of interrupted uploads left in quarantine.
- `SPOILER_LOG_RETENTION_DAYS` controls retention of `.txt` and `.json` spoiler files.
- `PORTAL_TOKEN_LIFETIME_DAYS` controls expiry for newly issued portal links.
- `AUDIT_RETENTION_DAYS` controls retention of sensitive-action audit records.
- `ENABLE_TRACKING_V2=true` enables experimental dual-write of V2 snapshots, events, and deliveries. No V2 Discord worker is started in this release, so only legacy notifications are published.
- `USE_LEGACY_TRACKING_SCHEDULER=true` temporarily restores the old minute scan; the central scheduler is the default.
- Legacy mode disables pause/resume/forced-sync commands and ignores persisted pauses. Switching back to the central scheduler restores their enforcement.
- `TRACKING_GLOBAL_CONCURRENCY` and `TRACKING_ORIGIN_CONCURRENCY` cap concurrent WebHost requests process-wide and per origin.
- `ARCHIPELAGO_ALLOWED_HOSTS` is a comma-separated list and explicitly bypasses private/local-address SSRF blocking for those hosts.
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

AST registers `/ast` together with the historical commands from version `v5.6.7`. Users can choose either the centralized interface or the direct commands.

- `/ast` opens a personal ephemeral command center adapted to the channel, room, and user permissions.
- `/ast file:<file>` imports a YAML, APWorld, generation ZIP, or spoiler according to the current mode and permissions.
- `/organize-room` creates and manages a preparation in Archipelago mode. In its channel, `/send-yaml file:<file>` and `/ast file:<file>` add a YAML or YAML ZIP to that same preparation without starting generation.
- Existing capabilities remain available under `My space`, `The room`, `Manage room`, `Archipelago tools`, and `AST administration`.
- Large lists are paginated; exclusions also provide search.
- Sensitive actions are authorized again server-side and written to the security audit log.
- Guild owners, administrators, and members with `Manage Server` can delegate AST management to a member without granting a Discord administrator role. A delegated manager cannot delegate that permission again.

See [the `/ast` command center](wiki/AST-command-center.md) and [the legacy-command mapping](wiki/Legacy-command-migration.md).

---

## Room preparation (Archipelago)

1. Run `/organize-room` and choose a name. AST posts the preparation message **in the current channel**, or in the existing thread. No new thread is created. Capacity is **200 players and 200 total slots**.
2. Players register, then upload their YAML files on the Web page or through a Discord command in that channel. The Web accepts multiple files at once; Discord accepts a ZIP through `/send-yaml file:<players.zip>`, `/ast file:<players.zip>`, or `/organize-room file:<players.zip>` (`fichier` when the bot is in French).
3. Each YAML is validated by Archipelago. The page distinguishes queued validation, validation in progress, and errors, with the generator diagnostic. After correcting a YAML or installing a missing APWorld, upload the file again. All of a player’s files must validate before they can mark themselves **Ready**.
4. The organizer can close/reopen registrations, test the full set, and remind registered players who have a YAML but are not ready, at most once every five minutes. They select **Generate** when all registered players are ready.

Each player manages their own files. Batches are checked before any replacement; a rejected batch leaves existing files and readiness unchanged. Archipelago validation then runs separately for each file. Limits are **20 files per player**, **1 MiB per YAML**, and **16 MiB of YAML per preparation**; the ZIP and its uncompressed content are also limited to 16 MiB each.

- **Cancellation**: preparation data and working files are deleted.
- **Successful generation**: the ZIP is delivered in the same channel, then preparation data is deleted. If attaching it is impossible, AST first publishes a link valid for **24 hours**; only a temporary ZIP copy remains until expiry. Anyone possessing that link can download the archive.
- **Delivery failure**: preparation and files are retained, with an action to retry delivery. A generation failure also allows correction and another attempt.

Then host the ZIP on your Archipelago server or archipelago.gg and connect the room with `/add-url` or the `/ast` wizard. Preparation uses SQLite and requires no extra database server.

See [the full preparation guide](wiki/Room-preparation.md) for permissions, limits, validation, and cleanup rules.

---

## Built-in web portal

When `ENABLE_WEB_PORTAL=true`, AST serves pages under `/portal` and their APIs under `/api/portal/...`. Sidebar navigation, compact tables, and search tools make large rooms easier to browse.

- **User**: progression, items, hints, aliases, and personal recaps.
- **Room**: player table, progression, tracking health, and last synchronization.
- **Administration**: configuration and controls according to Discord/AST permissions.
- **Preparation** (Archipelago mode): registration, multiple YAML uploads, validation errors, readiness, and generation.

Personal portal links are associated with a user, guild, and channel; permissions are checked again server-side. Temporary generated-ZIP links use separate authorization and remain valid after preparation deletion, until expiry.

See [the Web portal guide](wiki/Web-portal.md).

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
- `ast_command_invocations_total{surface,command}`
- `ast_command_options_total{surface,command,option,selection}`
- `ast_command_outcomes_total{surface,command,outcome}`
- `ast_command_duration_seconds`
- `ast_tracked_rooms`, `ast_tracked_guilds`, `ast_tracked_slots`
- `ast_room_poll_states{state}`, `ast_event_deliveries{state}`
- `ast_portal_tokens{state}`, `ast_archipelago_access_restrictions`
- `ast_metrics_collection_success`, `ast_metrics_collection_failures_total`
- `http_requests_received_total`, `http_request_duration_seconds`

The `surface` label distinguishes `discord_slash`, `/ast` buttons/selects/modals, and `web_portal`. Free-form values are never exported: aliases, URLs, searches, channel names, user IDs, and filenames become `provided`; only a small bounded set of choices and extensions is retained.

Example PromQL for the most-used commands over 30 days:

```promql
sum by (surface, command) (increase(ast_command_invocations_total[30d]))
```

```promql
sum by (surface, command, option, selection) (increase(ast_command_options_total[30d]))
```

---

## Build from source

```bash
# 1) Clone
git clone https://github.com/Etsuna/ArchipelagoSphereTracker.git
cd ArchipelagoSphereTracker

# 2) Create .env using the configuration shown above

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
dotnet test ArchipelagoSphereTracker.sln
```

The root solution covers WebHost parsing, converters, DB services, commands, and anonymized fixtures, as well as preparations: Web APIs, YAML/ZIP batches, validation, reminders, and cleanup. CI runs this suite before publishing Windows and Linux binaries.

---

## Project structure

```text
src/
  Bot/                # Discord commands and core bot logic
  SqlCommands/        # SQLite access + migrations
  Web/                # built-in web portal (pages/API)
  Preparations/       # registration, YAML, validation, and generation
  Gui/                # native Avalonia desktop GUI (--gui)
  TrackerLib/         # stream parser, datapackage, models
  Install/            # Archipelago setup/backup flows
tests/
  ArchipelagoSphereTracker.Tests/
apworld/
  # apworld assets/templates
Install/
  # distributed install scripts
```

---

## Storage and data

- One local SQLite database per instance: `AST.db`. No PostgreSQL server is required.
- The bot stores channels, aliases, game status, hints, recaps, and preparation data (registrations, YAML content, validation, snapshots, and delivery receipts).
- DB migrations are applied automatically at startup when needed.
- Preparation working files live under `data/room-preparations/`. Cancellation and successful delivery trigger cleanup of their data; downloadable ZIP copies expire after 24 hours. Technical audits, backups, and tracked rooms remain independent of this cleanup.
- To back up the instance, stop the bot and save `AST.db`, `.env`, `extern/`, and `data/`. Protect these copies, which may contain YAML files and private identifiers.

See [data and migrations](wiki/Data-and-migrations.md).

---

## Troubleshooting

### Bot does not start

- Check `DISCORD_TOKEN` in `.env`.
- Check startup argument (`--NormalMode` or `--ArchipelagoMode`).
- Confirm runtime OS is Linux or Windows.

### GUI does not start on Linux server

- In headless environments (no desktop/X11/Wayland session), `--gui` cannot open.
- Use `--NormalMode` / `--ArchipelagoMode` on servers, and run `--gui` from a desktop machine.

### Slash commands do not appear

- Check OAuth2/bot permissions on the Discord server.
- Confirm the bot is online and connected.
- After updating, restart the bot to register commands. `/organize-room` requires Archipelago mode.
- Wait a short time after inviting the bot (Discord propagation).

### Web portal is unreachable

- Check `ENABLE_WEB_PORTAL=true`.
- Ensure `WEB_PORT` is free and exposed.
- If using reverse proxy, verify routing to the local port.

### Multiworld generation errors

- Ensure launch mode is `--ArchipelagoMode`.
- Verify required files exist (`yaml`, `apworld`, ROMs when needed).
- Run `--install` again if the Archipelago environment is incomplete.
- In a preparation, inspect each YAML’s diagnostic on the Web page. After correcting it or adding an APWorld, upload the file again to restart validation.

### An uploaded YAML does not appear in `/list-yamls`

In a preparation channel, `/send-yaml` and `/ast file:` add files to the preparation shared by Web and Discord. Check its page or `/organize-room` status; `/list-yamls` lists the older generation folder used outside preparations.

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
