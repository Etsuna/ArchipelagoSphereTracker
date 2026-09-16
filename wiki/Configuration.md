# Configuration

[English](#english) · [Français](#français)

## English

AST loads the `.env` file located next to the executable. Restart the process after every change.

### Recognized variables

| Variable | Default | Description |
|---|---:|---|
| `DISCORD_TOKEN` | — | **Required.** Secret Discord bot token |
| `LANGUAGE` | `en` | Interface language: `fr` or `en` |
| `ENABLE_WEB_PORTAL` | `true` | Enables the built-in Web server |
| `WEB_PORT` | `5199` | HTTP port listened to by AST |
| `WEB_BASE_URL` | empty | Public URL used in generated links, for example `https://ast.example.com` |
| `WEB_MAX_UPLOAD_BYTES` | `67108864` | Maximum upload size, 64 MiB by default |
| `PORTAL_TOKEN_LIFETIME_DAYS` | `30` | Lifetime of newly issued private links, from 1 to 365 days |
| `UPLOAD_QUARANTINE_RETENTION_MINUTES` | `60` | Cleanup delay for interrupted uploads, from 5 to 1440 minutes |
| `AUDIT_RETENTION_DAYS` | `90` | Security audit retention, from 1 to 3650 days |
| `AST_OWNER_USER_ID` | empty | Discord ID of the optional global instance owner |
| `ARCHIPELAGO_ALLOWED_HOSTS` | empty | Explicitly allowed private hosts, separated by commas |
| `TRACKING_GLOBAL_CONCURRENCY` | `10` | Maximum concurrent WebHost reads for the process, from 1 to 100 |
| `TRACKING_ORIGIN_CONCURRENCY` | `2` | Maximum concurrent reads per origin, from 1 to 20 |
| `USE_LEGACY_TRACKING_SCHEDULER` | `false` | Temporary rollback to the former scheduler |
| `ENABLE_TRACKING_V2` | `false` | Experimental V2 tracking dual-write; leave disabled in production unless performing a targeted test |
| `EXPORT_METRICS` | `false` | Enables Prometheus export |
| `METRICS_PORT` | empty | Prometheus port when export is enabled |
| `ALLOW_DISCORD` | empty | Guild ID allowed in the advanced `--BigAsync` mode |
| `USER_ID_FOR_BIG_ASYNC` | empty | User ID used by the advanced `--BigAsync` mode |

The former `AST_DATA_PROTECTION_KEY` and recovery variables are no longer used during normal operation. See [[Data and migrations|Data-and-migrations]].

### Example — Normal mode with the portal

```dotenv
DISCORD_TOKEN=replace_me
LANGUAGE=en
ENABLE_WEB_PORTAL=true
WEB_PORT=5199
WEB_BASE_URL=https://ast.example.com
AST_OWNER_USER_ID=123456789012345678
WEB_MAX_UPLOAD_BYTES=67108864
PORTAL_TOKEN_LIFETIME_DAYS=30
AUDIT_RETENTION_DAYS=90
```

### Accepting files up to 200 MiB

On the AST side:

```dotenv
WEB_MAX_UPLOAD_BYTES=209715200
```

When AST is behind Nginx, the proxy limit must also be increased in the `server` block that actually receives the portal domain. A small margin accounts for the multipart envelope:

```nginx
server {
    server_name ast.example.com;
    client_max_body_size 210m;

    location / {
        proxy_pass http://127.0.0.1:5199;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 86400;
        proxy_send_timeout 86400;
    }
}
```

Then validate and reload Nginx:

```bash
sudo nginx -t
sudo systemctl reload nginx
sudo nginx -T | grep -n -B 3 -A 8 'server_name ast.example.com'
```

An HTML `413 Request Entity Too Large` page branded with `nginx` means Nginx rejected the request before it reached AST. If AST itself returns the error, verify that the variable is defined in the environment of the **service listening on `WEB_PORT`**, then restart that service.

### Private or local WebHost

AST blocks loopback, private, link-local, multicast, and reserved addresses by default. To explicitly trust a controlled private Archipelago instance:

```dotenv
ARCHIPELAGO_ALLOWED_HOSTS=ap.example.lan,archipelago.internal.example
```

Add only the required hosts. The value is a comma-separated list of host names, not complete URLs.

### Disabling the portal

```dotenv
ENABLE_WEB_PORTAL=false
```

The bot continues tracking rooms and `/ast` remains available, but no portal link can be issued.

### Prometheus telemetry

Set `EXPORT_METRICS=true` and choose `METRICS_PORT` to expose the endpoint. In addition to room and scheduler health, AST exports:

- command/action usage by bounded `surface` and `command` labels;
- option usage, retaining only booleans, fixed choices, and allowlisted file extensions;
- slash-command outcomes and processing duration;
- tracked guild, room, and slot totals;
- polling, event-delivery, portal-token, delegated-manager, and Archipelago-restriction counts;
- Discord connectivity and metrics-collector health;
- Web-portal HTTP request volume, status, and duration through route-template labels.

Free-form values are never used as labels. AST does not export aliases, URLs, searches, channel names, user IDs, or filenames through the new command metrics.

```promql
sum by (surface, command) (increase(ast_command_invocations_total[30d]))
```

```promql
sum by (surface, command, option, selection) (increase(ast_command_options_total[30d]))
```

A Dashboard V2 specification is provided in [Grafana-Dashboard.json](Grafana-Dashboard.json). Create or open a dynamic dashboard in Grafana, choose **Edit as code**, select **JSON**, and replace the editor contents with this file. The V2 queries identify Prometheus by datasource type, so Grafana resolves the available Prometheus datasource without embedding an instance-specific UID.

The tracking part of the dashboard consolidates the useful views from the former dashboards into four rows. The cascading **Discord server**, **Tracked room**, **Game**, and **Player / slot** filters can target one selection or **All**.

- **Archipelago overview** provides checks, locations, completion, player/game/room totals, checked-versus-remaining locations, completion and activity distributions, and bounded game/room rankings.
- **Progress and activity** restores the historical checks, locations, guild and room timelines, adds rolling progression and activity percentiles, and highlights stale or lagging rooms.
- **Player drill-down** provides bounded Top/Bottom 25 rankings, checked-versus-remaining worlds, progress made during the selected period, stale players, and a Top 20 progression timeline. Selecting one player turns these panels into a focused player view.
- **Raw tracking details** is collapsed by default and contains the only three tables: room configuration, alias choices, and per-player completion details. Its row-local **Alias** filter restores the focused alias lookup without cluttering the global controls.

Rankings and multi-series charts are explicitly limited to 20 or 25 results, so choosing **All** remains readable for Big Async instances with several thousand players. The former `tracker` filter is intentionally omitted because `ast_channel_info` no longer exports sensitive tracker connection data.

---

## Français

AST charge le fichier `.env` placé à côté de l’exécutable. Redémarrez le processus après chaque modification.

### Variables reconnues

| Variable | Défaut | Description |
|---|---:|---|
| `DISCORD_TOKEN` | — | **Obligatoire.** Token secret du bot Discord |
| `LANGUAGE` | `en` | Langue de l’interface : `fr` ou `en` |
| `ENABLE_WEB_PORTAL` | `true` | Active le serveur Web intégré |
| `WEB_PORT` | `5199` | Port HTTP écouté par AST |
| `WEB_BASE_URL` | vide | URL publique utilisée dans les liens, par exemple `https://ast.example.com` |
| `WEB_MAX_UPLOAD_BYTES` | `67108864` | Taille maximale d’un upload, 64 Mio par défaut |
| `PORTAL_TOKEN_LIFETIME_DAYS` | `30` | Durée des nouveaux liens privés, de 1 à 365 jours |
| `UPLOAD_QUARANTINE_RETENTION_MINUTES` | `60` | Nettoyage des uploads interrompus, de 5 à 1440 minutes |
| `AUDIT_RETENTION_DAYS` | `90` | Rétention du journal de sécurité, de 1 à 3650 jours |
| `AST_OWNER_USER_ID` | vide | ID Discord du propriétaire global facultatif de l’instance |
| `ARCHIPELAGO_ALLOWED_HOSTS` | vide | Hôtes privés explicitement autorisés, séparés par des virgules |
| `TRACKING_GLOBAL_CONCURRENCY` | `10` | Nombre maximal de lectures WebHost simultanées dans le processus, de 1 à 100 |
| `TRACKING_ORIGIN_CONCURRENCY` | `2` | Nombre maximal de lectures simultanées par origine, de 1 à 20 |
| `USE_LEGACY_TRACKING_SCHEDULER` | `false` | Rollback temporaire vers l’ancien scheduler |
| `ENABLE_TRACKING_V2` | `false` | Dual-write expérimental des données de suivi V2 ; laisser désactivé en production sauf test ciblé |
| `EXPORT_METRICS` | `false` | Active l’export Prometheus |
| `METRICS_PORT` | vide | Port Prometheus lorsque l’export est actif |
| `ALLOW_DISCORD` | vide | Guild ID autorisé en mode avancé `--BigAsync` |
| `USER_ID_FOR_BIG_ASYNC` | vide | User ID utilisé par le mode avancé `--BigAsync` |

Les anciennes variables `AST_DATA_PROTECTION_KEY` et de récupération ne sont plus utilisées en fonctionnement normal. Voir [[Données et migrations|Data-and-migrations]].

### Exemple — mode Normal avec portail

```dotenv
DISCORD_TOKEN=remplacez_moi
LANGUAGE=fr
ENABLE_WEB_PORTAL=true
WEB_PORT=5199
WEB_BASE_URL=https://ast.example.com
AST_OWNER_USER_ID=123456789012345678
WEB_MAX_UPLOAD_BYTES=67108864
PORTAL_TOKEN_LIFETIME_DAYS=30
AUDIT_RETENTION_DAYS=90
```

### Accepter des fichiers jusqu’à 200 Mio

Côté AST :

```dotenv
WEB_MAX_UPLOAD_BYTES=209715200
```

Si AST est derrière Nginx, la limite du proxy doit aussi être augmentée dans le bloc `server` qui reçoit réellement le domaine du portail. Une petite marge couvre l’enveloppe multipart :

```nginx
server {
    server_name ast.example.com;
    client_max_body_size 210m;

    location / {
        proxy_pass http://127.0.0.1:5199;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 86400;
        proxy_send_timeout 86400;
    }
}
```

Puis vérifiez et rechargez Nginx :

```bash
sudo nginx -t
sudo systemctl reload nginx
sudo nginx -T | grep -n -B 3 -A 8 'server_name ast.example.com'
```

Une page HTML `413 Request Entity Too Large` signée `nginx` signifie que la requête a été refusée par Nginx avant d’atteindre AST. Si l’erreur vient d’AST, vérifiez que la variable est définie dans l’environnement du **service qui écoute `WEB_PORT`**, puis redémarrez ce service.

### WebHost privé ou local

AST bloque par défaut les adresses loopback, privées, link-local, multicast et réservées. Pour une instance Archipelago privée explicitement maîtrisée :

```dotenv
ARCHIPELAGO_ALLOWED_HOSTS=ap.example.lan,archipelago.internal.example
```

N’ajoutez que les hôtes nécessaires. La valeur est une liste de noms d’hôtes, pas d’URLs complètes.

### Portail désactivé

```dotenv
ENABLE_WEB_PORTAL=false
```

Le bot continue de suivre les rooms et `/ast` reste disponible, mais aucun lien de portail ne peut être créé.

### Télémétrie Prometheus

Définissez `EXPORT_METRICS=true` et choisissez `METRICS_PORT` pour exposer l’endpoint. En plus de la santé des rooms et du scheduler, AST exporte :

- l’utilisation des commandes/actions avec les labels bornés `surface` et `command` ;
- l’utilisation des options, en conservant uniquement les booléens, choix fixes et extensions autorisées ;
- les résultats et la durée de traitement des slash commands ;
- le nombre de serveurs, rooms et slots suivis ;
- les états du polling, des livraisons d’événements, des tokens de portail, des responsables délégués et des restrictions Archipelago ;
- la connexion Discord et la santé du collecteur de métriques ;
- le volume, le statut et la durée des requêtes HTTP du portail, avec des labels fondés sur les modèles de routes.

Les valeurs libres ne servent jamais de labels. Les nouvelles métriques de commandes n’exportent ni alias, ni URL, ni recherche, ni nom de salon, ni ID utilisateur, ni nom de fichier.

```promql
sum by (surface, command) (increase(ast_command_invocations_total[30d]))
```

```promql
sum by (surface, command, option, selection) (increase(ast_command_options_total[30d]))
```

Une spécification Dashboard V2 est fournie dans [Grafana-Dashboard.json](Grafana-Dashboard.json). Créez ou ouvrez un dashboard dynamique dans Grafana, choisissez **Edit as code**, sélectionnez **JSON**, puis remplacez le contenu de l’éditeur par ce fichier. Les requêtes V2 identifient Prometheus par son type de datasource : aucun UID propre à une instance n’est intégré au fichier.

La partie suivi du dashboard regroupe les vues utiles des anciens dashboards dans quatre lignes. Les filtres en cascade **Discord server**, **Tracked room**, **Game** et **Player / slot** permettent de cibler une valeur ou **All**.

- **Archipelago overview** affiche les checks, les locations, la progression, les nombres de joueurs/jeux/rooms, les locations faites et restantes, les répartitions de progression et d’activité, ainsi que des classements bornés par jeu et par room.
- **Progress and activity** restaure les historiques des checks, locations, serveurs et rooms, puis ajoute la progression glissante, les percentiles d’activité et les rooms inactives ou en retard.
- **Player drill-down** fournit des classements Top/Bottom 25, les mondes faits/restants, les gains sur la période sélectionnée, les joueurs inactifs et une timeline limitée aux 20 premières séries. La sélection d’un joueur transforme ces panneaux en vue individuelle.
- **Raw tracking details** est repliée par défaut et contient les trois seuls tableaux : configuration des rooms, choix d’alias et détail de progression par joueur. Son filtre local **Alias** restaure la recherche ciblée par alias sans encombrer les contrôles globaux.

Les classements et graphiques multiséries sont explicitement limités à 20 ou 25 résultats : la sélection **All** reste donc lisible avec plusieurs milliers de joueurs dans un Big Async. L’ancien filtre `tracker` est volontairement absent, car `ast_channel_info` n’exporte plus les données sensibles de connexion au tracker.
