# Troubleshooting

[English](#english) · [Français](#français)

## English

### The direct slash commands do not appear

AST registers `/ast` and the direct commands from `v5.6.7`. Restart the updated bot so its guild-command bulk registration runs, allow a few seconds for Discord propagation, then close and reopen Discord if its command list is cached. Archipelago commands appear only with `--ArchipelagoMode`. See [[AST command center|AST-command-center]] and [[Slash commands and AST|Legacy-command-migration]].

### `/ast` does not appear

- Check that the OAuth2 URL included both the `bot` and `applications.commands` scopes.
- Check that the bot is connected and has the **Use Application Commands** permission.
- Look for `Registering commands for guild` followed by `Registering command handlers` in the console.
- Restart AST and allow a few seconds for Discord propagation.
- If an old command list remains cached, close and reopen Discord or try another channel.

### `Invalid portal link or insufficient permissions`

The token has expired, was revoked, was replaced by a newer link, was truncated, or the user no longer has access to the guild/thread. Run `/ast` in the correct context and create the portal again. If the new link uses the wrong origin, correct `WEB_BASE_URL` and restart the bot.

### `413 Request Entity Too Large`

Identify the source:

- an HTML page ending in `nginx/...` comes from Nginx;
- an ASP.NET/AST response comes from the `WEB_MAX_UPLOAD_BYTES` limit;
- a Discord rejection can come from the guild/account attachment limit, which is independent of AST.

To accept a 200 MiB file:

```dotenv
WEB_MAX_UPLOAD_BYTES=209715200
```

```nginx
client_max_body_size 210m;
```

Restart AST after changing `.env`, then run `sudo nginx -t` and `sudo systemctl reload nginx`. Use `sudo nginx -T` to confirm that the directive is in the **virtual host actually used by the portal domain**, not in another proxy or port.

### The portal uses `localhost` or the wrong domain

Set this on the AST instance:

```dotenv
WEB_BASE_URL=https://your-domain.example
```

Then restart AST and request a new link. Existing links are not rewritten automatically.

### The portal does not respond

Check:

```bash
curl -I http://127.0.0.1:5199/portal/
ss -ltnp | grep 5199
sudo nginx -t
```

Make sure that `ENABLE_WEB_PORTAL=true`, that `WEB_PORT` matches `proxy_pass`, and that the service was restarted after the change. Never publish command output that exposes your Discord token.

### The room URL is rejected

Use the room URL, not the tracker URL:

```text
https://archipelago.example/room/<id>
```

It must not contain HTTP credentials, a query string, or a fragment. Private and local hosts are rejected unless explicitly allowed through `ARCHIPELAGO_ALLOWED_HOSTS`.

### Tracking reports an error or no longer synchronizes

Run `/ast` in the room, then open **The room** and **Manage room**:

- check whether the room is paused;
- inspect the last success, next due time, and error type;
- use **Synchronize now** once; a 30-second cooldown applies;
- an HTML error often means a proxy page was returned instead of the JSON API;
- repeated errors can temporarily open the WebHost protection circuit.

The console writes a line after every poll with `no new items`, change counts, or a classified error.

### Pause, resume, or sync is unavailable

These controls are intentionally unavailable when `USE_LEGACY_TRACKING_SCHEDULER=true`. Set it back to `false` and restart. Immediately after startup, also allow the central scheduler to finish initializing.

### “The database file could not be found. Migration was skipped.”

On first startup, this is informational: AST creates `AST.db` afterward. If the process stops, inspect the error that follows this message; the initially missing database is not the cause.

### An old recovery command simply starts the bot

Application-level database encryption and its recovery commands have been removed. Use `--UpdateBDD` only to migrate an old database, with the old key available during conversion. See [[Data and migrations|Data-and-migrations]].

### No player is mentioned for an item

- Associate the slot under **My space → My slots**.
- Check the mention filter selected by each user.
- Check their personal exclusions.
- Multiple users can share the same slot; each user must associate it with their own account.

### YAML, APWorld, or generation features are missing

These features exist only in `--ArchipelagoMode`. If **Archipelago tools** is missing while that mode is active, a server administrator may have restricted your account under **AST Administration → Archipelago access restrictions**. In Normal mode, `/ast file:` accepts only `.txt`/`.json` spoilers in a tracked room.

### Getting help

Open a [GitHub issue](https://github.com/Etsuna/ArchipelagoSphereTracker/issues) or join the [AST Discord](https://discord.gg/PJfWRKVyEW). Include the AST version, operating system, mode, time, and relevant error lines. Always redact tokens, portal URLs, and private room identifiers.

---

## Français

### Les commandes slash directes n’apparaissent pas

AST enregistre `/ast` et les commandes directes de `v5.6.7`. Redémarrez le bot mis à jour afin de lancer l’enregistrement global des commandes du serveur, attendez quelques secondes pour la propagation Discord, puis fermez et rouvrez Discord si sa liste est en cache. Les commandes Archipelago apparaissent uniquement avec `--ArchipelagoMode`. Voir [[Centre de commandes AST|AST-command-center]] et [[Commandes slash et AST|Legacy-command-migration]].

### `/ast` n’apparaît pas

- Vérifiez que le lien OAuth2 contenait les scopes `bot` et `applications.commands`.
- Vérifiez que le bot est connecté et qu’il possède **Utiliser les commandes de l’application**.
- Cherchez dans la console `Registering commands for guild` puis `Registering command handlers`.
- Redémarrez AST et attendez quelques secondes pour la propagation Discord.
- Si une ancienne liste reste en cache, fermez/réouvrez Discord ou testez dans un autre salon.

### `Invalid portal link or insufficient permissions`

Le token est expiré, révoqué, remplacé par un lien plus récent, tronqué, ou l’utilisateur n’a plus accès au serveur/thread. Lancez `/ast` dans le bon contexte et recréez le portail. Si l’origine du nouveau lien est incorrecte, corrigez `WEB_BASE_URL` puis redémarrez le bot.

### `413 Request Entity Too Large`

Identifiez la source :

- une page HTML terminée par `nginx/...` vient de Nginx ;
- une réponse ASP.NET/AST vient de la limite `WEB_MAX_UPLOAD_BYTES` ;
- un refus Discord peut venir de la limite de pièce jointe du serveur/compte, indépendante d’AST.

Pour accepter un fichier de 200 Mio :

```dotenv
WEB_MAX_UPLOAD_BYTES=209715200
```

```nginx
client_max_body_size 210m;
```

Redémarrez AST après modification du `.env`, puis `sudo nginx -t` et `sudo systemctl reload nginx`. Vérifiez avec `sudo nginx -T` que la directive se trouve dans le **vhost réellement utilisé par le domaine du portail**, pas dans un autre proxy ou port.

### Le portail utilise `localhost` ou un mauvais domaine

Définissez sur l’instance AST :

```dotenv
WEB_BASE_URL=https://votre-domaine.example
```

Puis redémarrez AST et demandez un nouveau lien. Un ancien lien n’est pas réécrit automatiquement.

### Le portail ne répond pas

Vérifiez :

```bash
curl -I http://127.0.0.1:5199/portal/
ss -ltnp | grep 5199
sudo nginx -t
```

Assurez-vous que `ENABLE_WEB_PORTAL=true`, que `WEB_PORT` correspond au `proxy_pass`, et que le service a été redémarré après modification. Ne publiez jamais la sortie d’une commande qui affiche votre token Discord.

### L’URL de room est refusée

Utilisez l’URL de la room, pas celle du tracker :

```text
https://archipelago.example/room/<id>
```

Elle ne doit contenir ni identifiants HTTP, ni query string, ni fragment. Les hôtes privés et locaux sont refusés sauf dérogation explicite avec `ARCHIPELAGO_ALLOWED_HOSTS`.

### Le suivi affiche une erreur ou ne synchronise plus

Ouvrez `/ast` dans la room puis consultez **La room** et **Gérer la room** :

- vérifiez si la room est en pause ;
- regardez la dernière réussite, la prochaine échéance et le type d’erreur ;
- utilisez **Synchroniser maintenant** une fois ; un cooldown de 30 secondes s’applique ;
- une erreur HTML indique souvent une page de proxy à la place de l’API JSON ;
- des erreurs répétées peuvent ouvrir temporairement le circuit de protection du WebHost.

La console produit une ligne après chaque poll avec `aucun nouvel objet`, les compteurs de changements ou une erreur classifiée.

### Pause, reprise ou sync indisponible

Si `USE_LEGACY_TRACKING_SCHEDULER=true`, ces contrôles sont volontairement indisponibles. Remettez `false` et redémarrez. Juste après le démarrage, attendez aussi l’initialisation du scheduler central.

### « Le fichier de base de données est introuvable. La migration a été ignorée. »

Au premier lancement, c’est informatif : AST crée ensuite `AST.db`. Si le processus s’arrête, cherchez l’erreur qui suit ce message ; l’absence initiale de base n’en est pas la cause.

### Une ancienne commande de récupération démarre simplement le bot

Le chiffrement applicatif de la base et ses commandes de récupération ont été retirés. Utilisez `--UpdateBDD` uniquement pour migrer une ancienne base, avec l’ancienne clé disponible pendant la conversion. Voir [[Données et migrations|Data-and-migrations]].

### Aucun joueur n’est mentionné pour un objet

- Associez le slot dans **Mon espace → Mes slots**.
- Vérifiez le filtre de mentions choisi pour chaque utilisateur.
- Vérifiez ses exclusions personnelles.
- Plusieurs utilisateurs peuvent partager le même slot ; chacun doit l’associer avec son propre compte.

### YAML, APWorld ou génération invisibles

Ces fonctions n’existent qu’en `--ArchipelagoMode`. Si **Outils Archipelago** n’apparaît pas alors que ce mode est actif, un administrateur du serveur a peut-être interdit votre compte depuis **Administration AST → Restrictions d’accès Archipelago**. En mode Normal, `/ast file:` accepte uniquement les spoilers `.txt`/`.json` dans une room suivie.

### Demander de l’aide

Ouvrez une [issue GitHub](https://github.com/Etsuna/ArchipelagoSphereTracker/issues) ou rejoignez le [Discord AST](https://discord.gg/PJfWRKVyEW). Fournissez la version AST, le système, le mode, l’heure et les lignes d’erreur utiles. Masquez toujours les tokens, URLs de portail et identifiants de room privés.
