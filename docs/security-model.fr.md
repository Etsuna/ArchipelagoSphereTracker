# Modèle de sécurité AST

Ce document décrit les règles appliquées depuis la PR 2 de durcissement. Les décisions sont centralisées dans `AstAuthorizationService` et sont recalculées côté serveur : le navigateur ne choisit ni son identité Discord ni son rôle.

## Niveaux d'autorisation

| Niveau | Autorise | Identités acceptées |
|---|---|---|
| Membre du serveur | consultation, récapitulatif personnel, alias personnel | membre Discord ayant encore accès au salon ou au thread |
| Gestionnaire de room | configuration et suppression d'une room, portail du thread, consultation des patches | propriétaire du thread, permission `Manage Threads`, gestionnaire du serveur |
| Gestionnaire du serveur | création de room, YAML, génération, portail global | propriétaire du serveur, administrateur, permission `Manage Server`, propriétaire de l'instance |
| Propriétaire de l'instance | installation, sauvegarde et chargement des APWorld | utilisateur `AST_OWNER_USER_ID`; à défaut, propriétaire du serveur Discord |

Les commandes Discord et les requêtes Web utilisent la même matrice. Un token de portail est un secret porteur lié à `(serveur, salon, utilisateur)` ; sa présence ne suffit pas : AST vérifie aussi que l'utilisateur appartient toujours au serveur, qu'il voit encore le salon et qu'il possède le niveau demandé.

## Portail Web

- Les anciennes pages d'administration sans token répondent `404`.
- Les API de lecture et d'écriture portent toutes le token utilisateur.
- Les liens de téléchargement générés sont authentifiés et les archives sont conservées hors du dossier statique public pendant une heure.
- Les réponses du portail utilisent `no-store`, `Referrer-Policy: no-referrer`, une CSP restrictive, `X-Frame-Options: DENY` et `nosniff`.
- Les anciens chemins statiques `/portal/.../downloads/...` sont bloqués, y compris si des fichiers d'une version précédente sont encore présents sur disque.

Les URLs de portail doivent être traitées comme des mots de passe : ne pas les publier ni les enregistrer dans des captures ou des journaux. Une rotation/expiration explicite des tokens reste prévue dans une évolution ultérieure.

## Fichiers et code APWorld

Les noms de fichiers sont réduits à un nom simple et leur extension est vérifiée. La limite par défaut est de 64 Mio (`WEB_MAX_UPLOAD_BYTES`). Les ZIP de génération sont limités à 500 entrées et 256 Mio décompressés, et seules des entrées YAML non imbriquées sont acceptées.

Un APWorld contient du code exécuté par l'outillage Archipelago local. Son chargement est donc réservé au propriétaire de l'instance ; la validation d'extension ne transforme pas un APWorld non fiable en fichier sûr.

## Sorties réseau et SSRF

Une URL de room doit être une URL HTTP(S) exacte de forme `/room/{id}`, sans identifiants, requête ni fragment. Les adresses loopback, link-local, privées, de documentation, multicast et réservées sont bloquées à la validation et à chaque nouvelle connexion HTTP.

Une instance Archipelago privée peut être autorisée explicitement avec `ARCHIPELAGO_ALLOWED_HOSTS`, liste de noms d'hôtes séparés par des virgules. Cette dérogation doit rester minimale.

## Observabilité

Les journaux HTTP n'affichent plus les identifiants de room, de tracker ni les URLs de patch. La métrique `ast_channel_info` n'exporte plus `base_url`, `room`, `tracker` et `port` comme labels.
