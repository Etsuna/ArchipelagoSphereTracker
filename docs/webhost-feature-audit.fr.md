# Audit fonctionnel du WebHost

Cet audit compare la matrice des 47 anciennes commandes de `docs/pr9-ast-command-center-spec.fr.md` avec les trois portails Web. Le WebHost ne reproduit pas chaque commande sous la forme d'un bouton distinct lorsque la même fonction est déjà couverte par une vue interactive (par exemple `recap`, `recap-all` et `list-items`).

## Portail utilisateur — `GuildMember`

| Domaine | Couverture Web |
|---|---|
| Associations | liste des slots de la room, ajout avec filtre de mention, suppression limitée aux slots de l'utilisateur |
| Progression | état de tous les jeux de la room |
| Patch | patch limité aux slots associés à l'utilisateur |
| Récaps | tous les récaps, vidage individuel, affichage puis vidage, vidage global |
| Objets et hints | objets reçus et hints Finder/Receiver regroupés par slot |
| Exclusions | liste personnelle, ajout depuis le datapackage du slot, suppression personnelle |
| Informations et aide | informations de room, liens APWorlds et Discord |
| Portail | ouverture dans AST Companion, copie du lien et révocation du lien actif |

## Portail de room — `RoomManager`

| Domaine | Couverture Web |
|---|---|
| Suivi | santé de la room, synchronisation immédiate, pause, reprise et politique de polling |
| Configuration | fréquence fixe, mode silencieux et suppression de la room |
| Progression | état des jeux de la room |
| Patchs | consultation des patchs de la room |
| Spoiler | upload `.txt`/`.json`, analyse par slot, limite de sphère, mode complet/premier blocage, masquage, validation et remise à zéro |
| Portail | révocation du lien actif |

La santé globale AST a été retirée de cette page : elle relève de `GuildManager`, pas de `RoomManager`.

## Portail d'administration — `GuildManager` / `InstanceOwner`

| Domaine | Couverture Web |
|---|---|
| Rooms | création via URL Archipelago et accès aux portails de room |
| Santé | santé globale AST |
| YAML | liste, backup, téléchargement protégé, upload, suppression, nettoyage et modèles protégés |
| APWorld | liste, backup et upload avec contrôle `InstanceOwner` côté API |
| Génération | test, génération depuis les YAML, génération depuis ZIP, balancing normal ou désactivé |
| Sécurité | journal d'audit et révocation du lien actif |
| Aide | informations APWorlds et Discord |

## Garanties transversales

- Toutes les nouvelles routes restent liées au token, au serveur, au salon et à l'utilisateur Discord résolu côté serveur.
- Les exclusions et patchs personnels sont filtrés côté serveur ; un identifiant utilisateur fourni par le navigateur n'est jamais accepté.
- Les uploads spoiler utilisent la quarantaine, la limite de taille et la validation de contenu existantes.
- Les téléchargements de modèles YAML sont désormais protégés par le token du portail.
- Les actions destructrices demandent une confirmation dans l'interface et sont inscrites dans le journal d'audit lorsqu'elles modifient des données.
- Les libellés ajoutés existent en anglais et en français, et un test vérifie que toute clé dynamique du WebHost existe dans les ressources.
