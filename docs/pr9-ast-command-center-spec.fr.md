# PR 9 — Spécification du centre de commandes `/ast`

> Statut : implémenté sur `codex/evolution` ; validation Discord en conditions réelles à effectuer avant fusion.

## 1. Objectif

AST expose actuellement 47 commandes slash : 35 commandes générales et 12 commandes supplémentaires en mode Archipelago. Cette surface est difficile à découvrir, encombre le sélecteur Discord et oblige les utilisateurs à connaître les noms et paramètres techniques.

La PR 9 remplace toutes les commandes slash publiques par une seule commande, avec une pièce jointe facultative pour les imports :

```text
/ast
```

`/ast` ouvre un centre de commandes éphémère, personnel, contextuel et filtré selon les permissions. Les anciennes fonctions restent disponibles via boutons, sélecteurs, formulaires, assistants et confirmations. Aucun panneau permanent n’est publié dans les salons ou threads.

## 2. Principes validés

- Une seule commande Discord enregistrée : `/ast`.
- Toutes les interfaces ouvertes par `/ast` sont éphémères et visibles uniquement par leur utilisateur.
- Le même message est réutilisé pendant la navigation ; les actions ne créent pas de bruit dans le thread.
- L’accueil dépend du contexte : room suivie, salon du serveur ou contexte invalide.
- Une action n’est affichée que si elle est pertinente pour l’utilisateur, mais les permissions sont toujours revérifiées côté serveur au clic et à la confirmation.
- Les listes utilisent recherche, filtrage et pagination ; aucune interface ne tente d’afficher simultanément des milliers de joueurs, slots ou objets.
- Les anciennes classes métier sont conservées puis découplées progressivement de `SocketSlashCommand`. L’interface `/ast` appelle des services typés, pas de fausses interactions slash reconstruites en mémoire.
- Les paramètres et secrets ne sont jamais placés dans les `custom_id` Discord.
- Toute action destructive exige une confirmation explicite.

## 3. Accueil contextuel

### 3.1 Dans le thread d’une room suivie

L’accueil affiche un résumé compact : nom de la room, état du suivi, dernière synchronisation, progression, nombre de joueurs et éventuelle alerte. Il propose :

1. `👤 Mon espace`
2. `🌐 La room`
3. `⚙️ Gérer la room` — uniquement pour `RoomManager` ou niveau supérieur
4. `🛠️ Administration AST` — uniquement pour `GuildManager` ou `InstanceOwner`
5. `🔄 Actualiser`

### 3.2 Dans un salon normal du serveur

L’accueil propose :

1. `🌐 Mes rooms` — sélecteur paginé des rooms accessibles
2. `➕ Configurer une room` — `GuildManager`
3. `📊 Santé AST` — `GuildManager`
4. `🛠️ Administration AST` — selon les droits
5. `❓ Aide et liens`

Sélectionner une room ouvre son accueil personnel sans publier de message dans son thread.

### 3.3 Dans un thread non suivi

AST indique que ce thread n’est associé à aucune room et propose de revenir à l’accueil serveur. La création d’une room reste disponible uniquement depuis un salon texte compatible.

### 3.4 En message privé

Comportement proposé pour la PR 9 : refuser proprement la commande, car les permissions et le contexte de room ne peuvent pas être déterminés sans sélectionner d’abord un serveur. Un accueil multi-serveurs en message privé pourra être ajouté ultérieurement.

## 4. Arborescence fonctionnelle

### 4.1 Mon espace — `GuildMember`

- `Mes slots` : voir ses associations, associer un slot libre, modifier les notifications, dissocier un de ses slots.
- `Mes objets` : objets reçus, filtres par slot et type, pagination.
- `Mes hints` : vue « reçus » ou « trouvés », filtrée par slot.
- `Mon récap` : un slot ou tous les slots ; nettoyage séparé et confirmé.
- `Mon patch` : téléchargement pour un slot autorisé.
- `Mes exclusions` : voir, ajouter ou retirer une exclusion personnelle.
- `Mon portail` : créer ou révoquer un lien privé.

### 4.2 La room — `GuildMember`

- progression et statuts des jeux ;
- joueurs et slots avec recherche/pagination ;
- associations Discord ↔ slots visibles dans la room ;
- informations de connexion non sensibles et configuration publique ;
- santé du suivi et fraîcheur des données ;
- aide et liens du projet.

### 4.3 Gérer la room — `RoomManager`

- synchroniser maintenant ;
- suspendre ou reprendre le suivi ;
- configurer le polling dans un seul assistant cohérent ;
- configurer le mode silencieux et les notifications ;
- ouvrir ou révoquer le portail de gestion ;
- importer et analyser un spoiler log ;
- gérer les validations de sphères ;
- supprimer la room avec confirmation forte.

Le réglage historique de fréquence et le polling adaptatif deviennent une seule interface. Le service interne conserve les modes automatique et fixe, les fréquences minimales/maximales et les validations existantes.

### 4.4 Administration AST — `GuildManager` et `InstanceOwner`

- configurer une nouvelle room avec l’assistant existant ;
- santé globale des rooms du serveur ;
- portail d’administration ;
- gestion YAML ;
- génération ;
- gestion APWorld ;
- sauvegardes et diagnostic.

Les fonctions YAML, génération et APWorld ne sont visibles que lorsque `Declare.IsArchipelagoMode` est actif. Les actions APWorld sensibles restent réservées à `InstanceOwner` conformément à la matrice actuelle.

## 5. Matrice de remplacement des 47 commandes

### 5.1 Commandes générales (35)

| Ancienne commande | Destination dans `/ast` | Interaction proposée | Permission |
|---|---|---|---|
| `get-aliases` | La room → Associations | liste paginée/recherche | GuildMember |
| `add-alias` | Mon espace → Mes slots → Associer | sélecteur de slot puis sélecteur de filtres de mention | GuildMember |
| `delete-alias` | Mon espace → Mes slots → Dissocier | sélecteur limité aux slots de l’utilisateur + confirmation | GuildMember ; gestion globale séparée pour administrateur |
| `update-frequency-check` | Gérer la room → Polling | fusionné dans l’assistant de polling | RoomManager |
| `add-url` | Accueil serveur → Configurer une room | remplacé par l’assistant de configuration | GuildManager |
| `ast-setup` | Accueil serveur → Configurer une room | assistant éphémère existant intégré au routeur `/ast` | GuildManager |
| `update-silent-option` | Gérer la room → Notifications | sélecteur normal/silencieux | RoomManager |
| `delete-url` | Gérer la room → Supprimer | résumé, saisie de confirmation et confirmation finale | RoomManager |
| `status-games-list` | La room → Progression | vue paginée et filtrable | GuildMember |
| `ast-health` | Accueil serveur → Santé AST | résumé puis sélection d’une room | GuildManager |
| `ast-room-health` | La room → Santé du suivi | vue directe + actualisation | GuildMember |
| `ast-sync-now` | Gérer la room → Synchroniser | bouton avec retour d’état | RoomManager |
| `ast-pause` | Gérer la room → Suspendre | confirmation | RoomManager |
| `ast-resume` | Gérer la room → Reprendre | bouton direct | RoomManager |
| `ast-polling` | Gérer la room → Polling | sélecteurs mode et fréquence | RoomManager |
| `info` | La room → Informations | vue directe | GuildMember |
| `get-patch` | Mon espace → Mon patch | sélecteur de slot puis envoi privé | GuildMember |
| `recap-all` | Mon espace → Mon récap → Tous mes slots | vue paginée | GuildMember |
| `recap` | Mon espace → Mon récap → Un slot | sélecteur puis vue | GuildMember |
| `recap-and-clean` | Mon espace → Mon récap → Afficher puis vider | sélecteur + confirmation | GuildMember |
| `clean` | Mon espace → Mon récap → Vider un slot | sélecteur + confirmation | GuildMember |
| `clean-all` | Mon espace → Mon récap → Tout vider | confirmation forte | GuildMember |
| `hint-from-finder` | Mon espace → Mes hints → Trouvés par mon slot | sélecteur + pagination | GuildMember |
| `hint-for-receiver` | Mon espace → Mes hints → Reçus par mon slot | sélecteur + pagination | GuildMember |
| `list-items` | Mon espace → Mes objets | sélecteur + filtres + pagination | GuildMember |
| `analyze-spoiler-log` | Portail utilisateur → Spoiler partagé → Analyser | assistant : slot, sphère, mode, masquage et validation | GuildMember |
| `send-spoiler-log` | Portail utilisateur ou `/ast file:<spoiler.txt>` | spoiler commun à la room, pièce jointe privée contrôlée | GuildMember |
| `apworlds-info` | Aide et liens → APWorlds | vue directe | GuildMember |
| `discord` | Aide et liens → Communauté Discord | lien direct | GuildMember |
| `excluded-item` | Mon espace → Mes exclusions → Ajouter | sélecteur slot puis objet | GuildMember sur ses propres données |
| `excluded-item-list` | Mon espace → Mes exclusions | liste personnelle paginée | GuildMember |
| `delete-excluded-item` | Mon espace → Mes exclusions → Retirer | sélecteur + confirmation | GuildMember sur ses propres données |
| `ast-user-portal` | Mon espace → Mon portail | créer/révoquer | GuildMember |
| `ast-room-portal` | Gérer la room → Portail | créer/révoquer | RoomManager |
| `ast-portal` | Administration AST → Portail | créer/révoquer | GuildManager |

La matrice corrige une incohérence actuelle : les exclusions sont stockées avec le `UserId`, mais l’ajout et la suppression sont aujourd’hui classés `RoomManager`. Dans `/ast`, un joueur doit pouvoir gérer uniquement ses propres exclusions ; toute opération globale reste une action distincte réservée aux gestionnaires.

### 5.2 Commandes du mode Archipelago (12)

| Ancienne commande | Destination dans `/ast` | Interaction proposée | Permission actuelle conservée |
|---|---|---|---|
| `list-yamls` | Administration → YAML → Fichiers | liste paginée | GuildManager |
| `list-apworld` | Administration → APWorld → Fichiers | liste paginée | InstanceOwner |
| `backup-yamls` | Administration → YAML → Sauvegarder | génération puis téléchargement privé | GuildManager |
| `backup-apworld` | Administration → APWorld → Sauvegarder | génération puis téléchargement privé | InstanceOwner |
| `download-template` | Administration → YAML → Modèles | sélecteur paginé puis téléchargement privé | GuildManager |
| `delete-yaml` | Administration → YAML → Supprimer | sélecteur + confirmation | GuildManager |
| `clean-yamls` | Administration → YAML → Tout supprimer | confirmation forte | GuildManager |
| `send-yaml` | `/ast file:<players.yaml>` | pièce jointe Discord native | GuildManager |
| `generate-with-zip` | `/ast file:<players.zip>` | pièce jointe native + option de balancing | GuildManager |
| `send-apworld` | `/ast file:<world.apworld>` | pièce jointe Discord native | InstanceOwner |
| `generate` | Administration → Génération → Lancer | confirmation + choix de balancing | GuildManager |
| `test-generate` | Administration → Génération → Tester | confirmation | GuildManager |

## 6. Import de fichiers

Discord ne permet pas à un bouton ou à une modale de demander une pièce jointe. L’unique commande `/ast` conserve donc une option facultative `file`. En mode Normal, seuls les spoilers `.txt` et `.json` sont acceptés. En mode Archipelago, le type de fichier peut aussi router vers un YAML, un ZIP de génération ou un APWorld, et l’option `skip-prog-balancing` devient disponible. Les permissions sont contrôlées avant traitement, puis les limites de taille, la quarantaine, l’extension et le contenu sont validés comme auparavant. Le résultat est une réponse éphémère ; aucun fichier sensible n’est demandé dans un message public. Le portail reste disponible séparément via ses boutons explicites, mais il n’est pas requis pour les imports Discord.

## 7. Navigation et état de session

Chaque ouverture crée une session courte en mémoire :

- identifiant aléatoire ;
- propriétaire, serveur, canal et room liés à la session ;
- durée d’inactivité proposée : 15 minutes ;
- écran courant, page, filtres et brouillons conservés côté serveur ;
- remplacement de l’ancienne session du même utilisateur dans le même contexte ;
- expiration claire avec bouton invitant à relancer `/ast`.

Les `custom_id` contiennent uniquement le préfixe du routeur, l’identifiant opaque de session et l’action. Les URL WebHost, tokens, noms de fichiers, aliases et autres données utilisateur restent côté serveur.

## 8. Sécurité et audit

- Une permission est contrôlée à l’affichage, au clic et juste avant l’écriture.
- Une session ne peut pas être utilisée par un autre utilisateur, dans un autre serveur ou dans un autre canal.
- Les actions destructives et les changements de configuration conservent l’audit existant avec identifiant de corrélation.
- Les nouveaux identifiants d’action stables remplacent la dépendance de l’audit aux noms des anciennes commandes.
- Les sorties neutralisent les mentions et respectent les limites Discord.
- Les téléchargements et portails utilisent des liens privés révocables et de courte durée.
- Les sélecteurs de slots proposés à un joueur sont limités aux opérations qu’il peut réellement effectuer.

## 9. Architecture d’implémentation proposée

1. `SlashCommandDefinitions` n’enregistre plus que `/ast`.
2. `AstCommandCenter` gère l’ouverture et le rendu des écrans.
3. `AstInteractionRouter` route boutons, sélecteurs et modales par identifiant d’action stable.
4. `AstUiSessionStore` isole les sessions et leur expiration.
5. Les fonctions qui acceptent actuellement `SocketSlashCommand` reçoivent progressivement des requêtes typées (`actor`, `guildId`, `channelId`, paramètres).
6. Les adaptateurs Discord et Web appellent les mêmes services métier.
7. Les longs traitements répondent immédiatement par un différé éphémère, puis modifient ou complètent la réponse privée.

L’ancienne logique de dispatch par grand `switch` sur `CommandName` est supprimée lorsque toutes les actions ont leur service typé.

## 10. Migration et compatibilité

- Le déploiement utilise l’écrasement global des commandes du serveur déjà présent : Discord retire automatiquement les 47 anciennes entrées et conserve `/ast`.
- Aucun alias slash temporaire n’est prévu dans cette proposition, conformément à l’objectif de suppression immédiate.
- Les données existantes de rooms, associations, récaps, exclusions, YAML, APWorld, portails et audit sont conservées.
- Les liens de portail déjà émis restent révocables.
- L’assistant `/ast-setup` devient un écran interne ; sa logique et ses tests de sécurité sont conservés.

## 11. Critères d’acceptation

- La liste des commandes enregistrées contient exactement `/ast` dans les deux modes d’exécution.
- Les 47 anciennes commandes ont une destination fonctionnelle dans la matrice ci-dessus.
- Un joueur ne voit et ne déclenche que ses actions autorisées.
- Un gestionnaire retrouve toutes les fonctions de room sans connaître une commande historique.
- Un propriétaire d’instance retrouve toutes les fonctions APWorld.
- Aucune navigation normale ne publie de message dans le salon ou le thread.
- Une liste de 3 000 joueurs reste navigable par recherche et pagination bornée.
- Une session volée, expirée ou utilisée hors contexte est refusée.
- Les confirmations empêchent les doubles exécutions.
- Les imports passent par la quarantaine et les validations existantes.
- Les builds et toute la suite de tests réussissent sans avertissement.

## 12. Décisions produit validées

1. **Imports** : `/ast file:` est la méthode Discord native pour YAML, ZIP, APWorld et spoiler log ; le portail privé reste une méthode parallèle explicite.
2. **Patches** : un joueur accède uniquement aux patches de ses propres slots ; les gestionnaires peuvent accéder à tous les slots de la room.
3. **Associations visibles** : les membres voient les slots de la room, leur propre association Discord et les données de jeu publiques. La correspondance Discord ↔ slot complète est réservée aux gestionnaires.
4. **Nettoyage des récaps** : les actions sont conservées dans une section avancée et protégées par confirmation.
5. **Message privé** : `/ast` est refusé hors serveur dans cette première version.
