# Installation

[English](#english) · [Français](#français)

## English

### Option A — use the public bot

The public bot runs in Normal mode and requires no installation:

1. [Invite AST to your server](https://discord.com/oauth2/authorize?client_id=1408901673522430047).
2. Grant the requested permissions.
3. Run `/ast` in a server channel.
4. Open **AST Administration → Configure room**.

### Option B — host your own instance

#### 1. Create the Discord application

1. Open the [Discord Developer Portal](https://discord.com/developers/applications) and create a **New Application**.
2. Open **Bot**, create the bot, and copy its token. Never publish this token.
3. Enable **Message Content Intent**, which is used by the current Discord client configuration.
4. Enable **Public Bot** only if other people should be able to invite your instance.
5. Under **OAuth2 → URL Generator**, select the `bot` and `applications.commands` scopes.
6. Use permission integer `395137117248`, or manually grant: View Channels, Send Messages, create/manage public and private threads, send messages in threads, Embed Links, Attach Files, Add Reactions, Manage Messages, and Read Message History.

Direct URL, replacing `{AppId}`:

```text
https://discord.com/oauth2/authorize?client_id={AppId}&scope=bot%20applications.commands&permissions=395137117248
```

#### 2. Download AST

Download the latest official version from [GitHub Releases](https://github.com/Etsuna/ArchipelagoSphereTracker/releases):

- Windows x64: `ast-win-x64-vX.Y.Z.zip`
- Linux x64: `ast-linux-x64-vX.Y.Z.tar.gz`

Extract the archive into a dedicated directory. Releases are self-contained, so the .NET SDK is not required to run them.

#### 3. Create `.env`

Place `.env` next to the executable:

```dotenv
DISCORD_TOKEN=YOUR_DISCORD_TOKEN
LANGUAGE=en
ENABLE_WEB_PORTAL=true
WEB_PORT=5199
WEB_BASE_URL=https://ast.example.com
AST_OWNER_USER_ID=123456789012345678
```

See [[Configuration]] for every option.

#### 4. Start AST

```powershell
# Windows — tracking only
.\ArchipelagoSphereTracker.exe --NormalMode

# Windows — local Archipelago features
.\ArchipelagoSphereTracker.exe --ArchipelagoMode
```

```bash
# Linux — tracking only
./ArchipelagoSphereTracker --NormalMode

# Linux — local Archipelago features
./ArchipelagoSphereTracker --ArchipelagoMode
```

Other startup arguments:

| Argument | Effect |
|---|---|
| `--install` | Installs or updates the local Archipelago environment with backup/restore |
| `--UpdateBDD` | Runs `AST.db` migrations, then exits |
| `--gui` | Opens the desktop configuration and control interface |
| `--BigAsync` | Advanced single-guild mode associated with `ALLOW_DISCORD` |

On the first startup, a missing `AST.db` is normal: migration is skipped, then the database is created and initialized automatically.

#### 5. Recommended systemd service on Linux

```ini
[Unit]
Description=ArchipelagoSphereTracker
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
WorkingDirectory=/opt/ast
ExecStart=/opt/ast/ArchipelagoSphereTracker --NormalMode
Restart=on-failure
RestartSec=5
User=ast

[Install]
WantedBy=multi-user.target
```

After creating `/etc/systemd/system/ast.service`:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now ast
sudo journalctl -u ast -f
```

### Updating AST

1. Stop AST cleanly.
2. Back up `AST.db`, `.env`, and the `extern/` directory.
3. Replace the program files with those from the new release.
4. Restart in the same mode. SQLite migrations run automatically.

---

## Français

### Option A — utiliser le bot public

Le bot public fonctionne en mode Normal et ne demande aucune installation :

1. [Invitez AST sur votre serveur](https://discord.com/oauth2/authorize?client_id=1408901673522430047).
2. Accordez les permissions demandées.
3. Lancez `/ast` dans un salon du serveur.
4. Ouvrez **Administration AST → Configurer une room**.

### Option B — héberger sa propre instance

#### 1. Créer l’application Discord

1. Ouvrez le [Discord Developer Portal](https://discord.com/developers/applications) et créez une **New Application**.
2. Dans **Bot**, créez le bot puis copiez son token. Ne publiez jamais ce token.
3. Activez **Message Content Intent**, utilisé par la configuration actuelle du client Discord.
4. Activez **Public Bot** seulement si d’autres personnes doivent pouvoir inviter votre instance.
5. Dans **OAuth2 → URL Generator**, sélectionnez les scopes `bot` et `applications.commands`.
6. Utilisez les permissions `395137117248`, ou accordez manuellement : voir les salons, envoyer des messages, créer/gérer des threads publics et privés, envoyer dans les threads, intégrer des liens, joindre des fichiers, ajouter des réactions, gérer les messages et lire l’historique.

URL directe, en remplaçant `{AppId}` :

```text
https://discord.com/oauth2/authorize?client_id={AppId}&scope=bot%20applications.commands&permissions=395137117248
```

#### 2. Télécharger AST

Téléchargez la dernière version officielle depuis les [Releases GitHub](https://github.com/Etsuna/ArchipelagoSphereTracker/releases) :

- Windows x64 : `ast-win-x64-vX.Y.Z.zip`
- Linux x64 : `ast-linux-x64-vX.Y.Z.tar.gz`

Décompressez l’archive dans un dossier dédié. Les releases sont autonomes : le SDK .NET n’est pas requis pour les exécuter.

#### 3. Créer `.env`

Placez `.env` à côté de l’exécutable :

```dotenv
DISCORD_TOKEN=VOTRE_TOKEN_DISCORD
LANGUAGE=fr
ENABLE_WEB_PORTAL=true
WEB_PORT=5199
WEB_BASE_URL=https://ast.example.com
AST_OWNER_USER_ID=123456789012345678
```

Voir [[Configuration]] pour toutes les options.

#### 4. Démarrer

```powershell
# Windows — suivi uniquement
.\ArchipelagoSphereTracker.exe --NormalMode

# Windows — fonctions locales Archipelago
.\ArchipelagoSphereTracker.exe --ArchipelagoMode
```

```bash
# Linux — suivi uniquement
./ArchipelagoSphereTracker --NormalMode

# Linux — fonctions locales Archipelago
./ArchipelagoSphereTracker --ArchipelagoMode
```

Autres commandes de démarrage :

| Argument | Effet |
|---|---|
| `--install` | Installe ou met à jour l’environnement Archipelago local, avec sauvegarde/restauration |
| `--UpdateBDD` | Exécute les migrations de `AST.db`, puis quitte |
| `--gui` | Lance l’interface desktop de configuration et de contrôle |
| `--BigAsync` | Mode avancé mono-serveur, associé à `ALLOW_DISCORD` |

Au premier démarrage, l’absence de `AST.db` est normale : la migration est ignorée, puis la base est créée et initialisée automatiquement.

#### 5. Service systemd conseillé sous Linux

```ini
[Unit]
Description=ArchipelagoSphereTracker
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
WorkingDirectory=/opt/ast
ExecStart=/opt/ast/ArchipelagoSphereTracker --NormalMode
Restart=on-failure
RestartSec=5
User=ast

[Install]
WantedBy=multi-user.target
```

Après création du fichier `/etc/systemd/system/ast.service` :

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now ast
sudo journalctl -u ast -f
```

### Mettre à jour

1. Arrêtez AST proprement.
2. Sauvegardez `AST.db`, `.env` et le dossier `extern/`.
3. Remplacez les fichiers du programme par ceux de la nouvelle release.
4. Relancez dans le même mode. Les migrations SQLite sont automatiques.
