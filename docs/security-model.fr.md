# Modèle de sécurité AST

Ce document décrit les règles appliquées depuis la PR 2 de durcissement. Les décisions sont centralisées dans `AstAuthorizationService` et sont recalculées côté serveur : le navigateur ne choisit ni son identité Discord ni son rôle.

## Niveaux d'autorisation

| Niveau | Autorise | Identités acceptées |
|---|---|---|
| Membre du serveur | consultation, récapitulatif personnel, alias personnel, upload et analyse du spoiler partagé de la room | membre Discord ayant encore accès au salon ou au thread |
| Gestionnaire de room | configuration et suppression d'une room, portail du thread, consultation des patches | propriétaire du thread, permission `Manage Threads`, gestionnaire du serveur |
| Gestionnaire du serveur | création de room, YAML, génération, portail global | propriétaire du serveur, administrateur, permission `Manage Server`, propriétaire de l'instance |
| Propriétaire de l'instance | installation, sauvegarde et chargement des APWorld | utilisateur `AST_OWNER_USER_ID`; à défaut, propriétaire du serveur Discord |

Les commandes Discord et les requêtes Web utilisent la même matrice. Un token de portail est un secret porteur lié à `(serveur, salon, utilisateur)` ; sa présence ne suffit pas : AST vérifie aussi que l'utilisateur appartient toujours au serveur, qu'il voit encore le salon et qu'il possède le niveau demandé.

## Portail Web

- Les anciennes pages d'administration sans token répondent `404`.
- Les API de lecture et d'écriture portent toutes le token utilisateur.
- Les liens de téléchargement générés sont authentifiés et les archives sont conservées hors du dossier statique public avec les données de la room. Elles sont supprimées lors de la suppression du thread ou de son URL.
- Les réponses du portail utilisent `no-store`, `Referrer-Policy: no-referrer`, une CSP restrictive, `X-Frame-Options: DENY` et `nosniff`.
- Les anciens chemins statiques `/portal/.../downloads/...` sont bloqués, y compris si des fichiers d'une version précédente sont encore présents sur disque.
- SQLite ne conserve que le SHA-256 du token. Un nouveau lien remplace le précédent et expire après `PORTAL_TOKEN_LIFETIME_DAYS` jours.
- `revoke:true` sur les commandes de portail invalide le lien actif sans en créer un nouveau.
- Les pages personnelles sont rendues dynamiquement : un ancien fichier HTML présent sur disque ne contourne ni expiration ni révocation.

Les URLs de portail doivent être traitées comme des mots de passe : ne pas les publier ni les enregistrer dans des captures ou des journaux. Demander un nouveau lien effectue une rotation et invalide immédiatement le précédent.

## Fichiers et code APWorld

Les noms de fichiers sont réduits à un nom simple et leur extension est vérifiée. La limite par défaut est de 64 Mio (`WEB_MAX_UPLOAD_BYTES`). Chaque téléversement est d'abord écrit sous un nom opaque dans une quarantaine hors des dossiers actifs, contrôlé, puis déplacé atomiquement vers sa destination. Un fichier refusé ne remplace donc jamais la version active. Les résidus temporaires de quarantaine sont nettoyés selon `UPLOAD_QUARANTINE_RETENTION_MINUTES`. Le spoiler actif et les YAML d'une room n'expirent pas : ils sont supprimés avec l'URL ou le thread.

Les ZIP de génération sont limités à 500 entrées et 256 Mio décompressés, et seules des entrées YAML non imbriquées sont acceptées. Les archives APWorld doivent être lisibles, respecter les mêmes limites et ne contenir aucun chemin absolu ou traversée de répertoire. Les YAML et spoilers texte doivent être du texte UTF-8 non vide sans octet nul; un spoiler `.json` doit contenir un objet ou un tableau JSON valide. Voir [quarantaine et validation des téléversements](upload-quarantine-security.fr.md).

Un APWorld contient du code exécuté par l'outillage Archipelago local. Son chargement est donc réservé au propriétaire de l'instance ; la validation d'extension ne transforme pas un APWorld non fiable en fichier sûr.

Le dossier global `extern/Archipelago/custom_worlds` est exclu de tous les nettoyages de room et de guilde. Les custom worlds ne sont supprimés que par une action manuelle dédiée.

## Sorties réseau et SSRF

Une URL de room doit être une URL HTTP(S) exacte de forme `/room/{id}`, sans identifiants, requête ni fragment. Les adresses loopback, link-local, privées, de documentation, multicast et réservées sont bloquées à la validation et à chaque nouvelle connexion HTTP.

Une instance Archipelago privée peut être autorisée explicitement avec `ARCHIPELAGO_ALLOWED_HOSTS`, liste de noms d'hôtes séparés par des virgules. Cette dérogation doit rester minimale.

## Observabilité

Les journaux HTTP n'affichent plus les identifiants de room, de tracker ni les URLs de patch. La métrique `ast_channel_info` n'exporte plus `base_url`, `room`, `tracker` et `port` comme labels.

Les actions sensibles sont inscrites dans `SecurityAuditLogTable` avec date UTC, corrélation, source, auteur Discord, serveur, salon, action et résultat. Aucun argument de commande, token, URL, alias ou nom de fichier n'est enregistré. La rétention est contrôlée par `AUDIT_RETENTION_DAYS`; l'API `/api/portal/{guild}/{channel}/{token}/audit` est réservée aux gestionnaires du serveur.

## Stockage SQLite

Depuis la migration SQLite `5.0.12`, les identifiants de room, de tracker et les liens de patch sont stockés en clair dans SQLite. Ils sont considérés comme des données de configuration partageables et AST ne requiert plus de clé de chiffrement. Les tokens du portail restent hachés et le token Discord reste fourni par l'environnement. Voir [stockage des identifiants](data-protection-at-rest.fr.md).
