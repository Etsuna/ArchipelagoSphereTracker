# AST : préparation de l'examen Discord des privileged intents

> Mise à jour : les changements locaux suppriment maintenant les intents, remplacent l'ajout de masse par un bouton d'inscription et branchent les notifications V2. Voir [le guide de déploiement](tracking-v2-notifications.fr.md). Les constats ci-dessous décrivent l'audit AVANT ces changements ; aucun réglage du portail Discord ni déploiement de production n'a été effectué ici.

Application : AST / Archipelago Sphere Tracker (`1408901673522430047`).
Échéance indiquée dans la notification : 24/01/2027 à 19:28:50 (fuseau non précisé dans le texte fourni).
Audit du code local : 26/10/2026. La version réellement hébergée n'a pas été vérifiée.

## Conclusion de l'audit

La nécessité de renouveler Guild Members et Message Content n'est pas démontrée par le code local. La recommandation est de supprimer leurs dépendances résiduelles, vérifier la version hébergée, puis désactiver les accès devenus inutiles dans le Developer Portal. Ce dossier ne constitue pas une demande déjà envoyée ni une certification de conformité.

Discord demande d'utiliser les alternatives lorsqu'elles couvrent les fonctionnalités :

- [Guide de l'examen](https://docs.discord.com/developers/gateway/getting-started-with-privileged-intent-review)
- [Alternatives aux privileged intents](https://docs.discord.com/developers/gateway/you-might-not-need-a-privileged-intent)

## Détails de l'application : texte à copier

AST (Archipelago Sphere Tracker) est un bot Discord open source destiné aux communautés qui jouent à des parties multiworld Archipelago. Les utilisateurs configurent explicitement une room à suivre. AST récupère sa progression depuis le WebHost Archipelago et publie dans le salon ou le thread choisi des notifications concernant les objets reçus, la progression et la réalisation des objectifs des joueurs.

Les utilisateurs interagissent avec AST par des commandes slash, notamment /ast, puis par des boutons, menus et formulaires. Ils peuvent associer leur compte Discord à leurs slots de jeu, consulter des récapitulatifs et des hints, régler leurs notifications et accéder à un portail Web. Les actions de configuration et d'administration sont soumises à des contrôles de permissions.

Le logiciel comprend également un mode Archipelago facultatif permettant aux utilisateurs de préparer une partie collectivement : inscription, dépôt explicite de fichiers YAML, validation, indication de disponibilité et génération d'une archive multiworld. Ces fonctionnalités dépendent du mode activé sur l'instance ; elles ne doivent être présentées comme disponibles sur le bot public que si ce mode y est réellement activé.

AST conserve les données nécessaires au suivi et à la configuration dans une base SQLite locale, ainsi que les fichiers volontairement fournis pour les fonctions utilisées. La progression des parties provient d'Archipelago. Le code examiné ne comporte pas de fonction de modération des conversations ni de suivi des présences Discord.

Site : https://www.ast-bot.com/
Code source et documentation : https://github.com/Etsuna/ArchipelagoSphereTracker
Wiki : https://github.com/Etsuna/ArchipelagoSphereTracker/wiki

## Politique de confidentialité

Une politique est présente dans le dépôt, dans `wiki/Archipelago-Sphere-Tracker-–-Privacy-Policy.md`.

URL prévue du wiki : https://github.com/Etsuna/ArchipelagoSphereTracker/wiki/Archipelago-Sphere-Tracker-%E2%80%93-Privacy-Policy

Répondre « Oui » à la présence d'une politique publique seulement après avoir vérifié que cette URL est accessible sans connexion et correspond bien à la politique appliquée à l'instance publique. L'accès public de cette page n'a pas pu être confirmé pendant l'audit.

La politique locale décrit notamment les identifiants Discord, les associations aux slots, les préférences, les tokens de portail hachés, les audits et les fichiers téléversés. Elle contient encore une mention de document modèle : son adaptation à l'exploitation réelle reste à vérifier avant toute certification générale.

## Guild Members / Server Members Intent

Constats techniques :

- `src/Program.cs:187` demande Guilds, GuildMessages et MessageContent ; GuildMembers n'est pas inclus.
- Aucun abonnement aux événements d'arrivée, départ ou mise à jour de membres n'a été trouvé.
- `src/Security/AstAuthorizationService.cs:98` et `:175` effectuent des recherches individuelles REST pour les contrôles d'accès. Discord indique que cette opération ne nécessite pas Guild Members.
- `src/Bot/UrlClass.cs:205` possède toutefois une option `autoAddMembers` qui parcourt les utilisateurs du salon pour les ajouter à un thread public. Cette option mérite une vérification sur la version hébergée : sans chargement complet des membres, le cache peut être incomplet. Pour conserver un fonctionnement sans intent, préférer l'inscription explicite ou l'ajout de personnes identifiées.
- L'énumération des membres d'un thread privé pour en contrôler l'accès est distincte de la demande de la liste complète des membres d'un serveur.

Il serait inexact de présenter AST comme un bot de bienvenue ou de synchronisation en temps réel des membres. Aucune justification de renouvellement de ce type n'est préparée.

## Message Content Intent

Constats techniques :

- MessageContent est encore demandé dans `src/Program.cs:190`.
- `src/Program.cs:201` abonne le bot à `BotCommands.MessageReceivedAsync`.
- `src/Bot/BotCommands.cs:105` lit les messages humains commençant par `/` pour les transmettre à l'ancien CommandService. Aucun module de commande textuelle enregistré n'a été trouvé.
- Le parcours actif utilise les commandes slash, les composants et les modales. Les fichiers fournis comme options de commandes slash n'exigent pas de lecture passive des pièces jointes de tous les messages.
- Les lectures de messages repérées dans le module de préparation servent à modifier les propres messages du bot ; Discord prévoit une exception pour le contenu envoyé par l'application elle-même.

Discord recommande de remplacer les commandes à préfixe par les commandes slash. Il n'y a donc pas de justification démontrée pour conserver Message Content uniquement pour cet ancien gestionnaire.

## Réponses concernant les données

- **Conservation de données d'API hors Discord : Oui.** AST conserve notamment des identifiants de serveur, salon et utilisateur, des associations utilisateur-slot, des préférences et des événements d'audit dans SQLite. Les paramètres et durées effectifs dépendent de l'instance.
- **Stockage de contenu de message hors Discord : ne pas affirmer un « Non » global sans préciser le périmètre.** Aucun archivage général des conversations n'a été trouvé. Des fichiers explicitement transmis via les interactions, notamment YAML et spoilers, sont toutefois conservés pour les fonctionnalités demandées. Cela doit être expliqué si le formulaire inclut ces fichiers dans son périmètre.
- **Machine learning / entraînement d'IA : aucun usage de ce type trouvé dans le code examiné.** Une réponse « Non » doit aussi correspondre aux outils d'exploitation et de journalisation de l'instance publique.
- **Possibilité de refuser le suivi Message Content : aucun mécanisme individuel de refus n'a été trouvé dans l'ancien gestionnaire textuel.** Le réglage des notifications n'est pas un refus de lecture des messages. Ne pas le présenter comme tel. Après suppression du gestionnaire et de l'intent, la lecture passive ne serait plus nécessaire.
- **Captures et vidéos : aucune preuve d'une fonctionnalité nécessitant ces intents n'est disponible dans ce dossier.** Une capture de /ast peut documenter le bot, mais ne démontrerait pas le besoin d'un intent privilégié.

## Suite recommandée

1. Vérifier que le bot public utilise bien la version correspondant à cet audit et confirmer son mode d'exécution.
2. Retirer du code la demande MessageContent et le gestionnaire textuel résiduel, puis compiler et vérifier /ast, les commandes de fichiers, les interactions et le portail.
3. Vérifier le comportement de l'option d'ajout automatique de membres aux threads ; adopter un parcours d'inscription explicite si nécessaire.
4. Déployer et vérifier les fonctions utilisées avant de désactiver les accès inutiles dans le Developer Portal. Ne pas désactiver Message Content alors que la version en service le demande encore : cela peut empêcher sa connexion au Gateway.
5. Si aucun privileged intent n'est nécessaire après cette vérification, suivre le parcours sans privileged intents ; la notification fournie indique qu'aucune demande n'est nécessaire dans ce cas.
6. Si une fonctionnalité de production absente du dépôt exige réellement un intent, compléter une demande uniquement pour cet intent avec son fonctionnement exact, les alternatives insuffisantes et une démonstration réelle.

Aucun code du bot, paramètre du portail Discord ou déploiement n'a été modifié par cet audit. Aucune demande n'a été soumise.
