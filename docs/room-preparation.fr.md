# Préparer une room Archipelago

Cette fonctionnalité est disponible uniquement en **Archipelago Mode**. Elle organise les inscriptions et les YAML avant la génération, puis permet de rattacher la room hébergée au suivi AST.

## Démarrer

Depuis un salon texte du serveur, lancer `/organize-room` pour ouvrir l’assistant : nom et nombre maximum de joueurs (20 par défaut). La création exige les permissions de gestion du serveur reconnues par AST. Un bouton équivalent est disponible dans `/ast`, ainsi qu’un formulaire dans le portail d’administration.

AST publie un message avec un tableau persistant et des boutons directement dans le salon courant, sans créer de fil. Le ZIP sera envoyé dans ce même salon. La visibilité suit les permissions existantes du salon. Une préparation est associée à chaque salon ; les anciennes préparations déjà créées dans des fils restent accessibles. L’organisateur n’est pas inscrit comme joueur automatiquement : il peut organiser sans jouer. Si la commande est lancée dans un fil privé existant, l’action **Inviter un joueur** ajoute un membre au fil ; ce membre doit ensuite s’inscrire lui-même.

## Parcours des joueurs

1. Cliquer **S’inscrire** sur le message ou dans son portail de préparation.
2. Déposer ses YAML via `/send-yaml file:…`, `/organize-room fichier:…` (option `file` en anglais), `/ast file:…` dans ce salon, ou le portail. `/send-yaml` utilise la préparation du salon lorsqu’elle existe ; ailleurs, son fonctionnement historique est conservé. Une préparation dans un fil n’exige pas d’URL de suivi pour recevoir les YAML.
3. Attendre la validation réelle par le générateur Archipelago installé.
4. Cliquer **Prêt**. Tout remplacement ou retrait d’un fichier remet son propriétaire à **Pas prêt**.

Un joueur peut apporter plusieurs fichiers et plusieurs slots. Les noms avec variables et les jeux pondérés restent indicatifs avant génération. Chaque joueur peut télécharger, remplacer ou supprimer ses propres YAML. Le tableau expose les slots, jeux et statuts, mais pas le contenu des fichiers ni les diagnostics personnels des autres joueurs.

Limites : 1 Mio par fichier UTF-8, 20 documents par fichier, 20 fichiers par joueur, 200 slots et 16 Mio de YAML par préparation. Le nombre de joueurs est réglable de 1 à 200. Les restrictions d’accès aux outils Archipelago d’AST continuent de s’appliquer.

## Organisateur

- **Fermer les inscriptions** empêche les nouveaux joueurs de rejoindre. Les inscrits peuvent encore corriger leurs fichiers et leur disponibilité. **Rouvrir** réactive les inscriptions.
- **Tester les YAML** teste l’ensemble sans produire de ZIP et sans exiger que tous soient prêts. Un changement de fichiers invalide ce résultat.
- **Générer** exige au moins un joueur, tous prêts, avec au moins un YAML valide chacun. AST fige les fichiers, verrouille les modifications, reteste l’ensemble puis lance la génération. Le portail permet de désactiver l’équilibrage de progression.
- **Annuler** termine la préparation avant génération. Les données restent conservées ; il faut créer une autre préparation pour recommencer.

Le ZIP est envoyé automatiquement dans le salon de préparation. Si sa taille dépasse la limite Discord, un message propose le bouton du portail, où l’organisateur et les inscrits peuvent le télécharger. Le portail reste disponible en complément de la pièce jointe. La pièce jointe Discord est visible par les personnes ayant accès au salon ; utiliser un salon privé pour limiter cet accès.

L’organisateur l’héberge ensuite sur son serveur Archipelago ou sur archipelago.gg, puis renseigne l’URL dans le portail ou via l’action **Rattacher la room hébergée** de `/organize-room`. AST crée le fil de suivi avec une fréquence automatique et y ajoute les participants lorsque Discord le permet. L’association aux alias suit le parcours habituel du tracker.

L’identifiant du message d’envoi est conservé dans `PreparationArchiveDeliveries` pour éviter de renvoyer l’archive à chaque actualisation ou redémarrage. Un échec d’envoi ne remet pas en cause la génération ; l’envoi est retenté lors d’une prochaine actualisation du tableau. Un arrêt entre l’envoi Discord et l’enregistrement de son identifiant peut entraîner un doublon.

L’hébergement n’est pas automatique. La génération utilise l’installation Archipelago et les apworlds du serveur AST. Chaque processus est limité à 30 minutes ; les validations et générations de ce nouveau parcours sont exécutées une à la fois. Les anciennes commandes de génération restent indépendantes.

## Persistance et exploitation

La migration additive crée `RoomPreparations`, `PreparationParticipants`, `PreparationFiles` et `PreparationRuns` dans SQLite. Les anciens checks et leurs dates ne sont pas modifiés. Les YAML courants, leur empreinte, leur validation et la disponibilité des joueurs sont persistés. Chaque exécution conserve un instantané des fichiers dans `PreparationRuns`.

Les entrées de génération et archives sont isolées sous `data/room-preparations/<préparation>/<exécution>/`, à partir du répertoire de base d’AST. Les fichiers temporaires des validations individuelles sont retirés. Les instantanés, résultats et préparations annulées n’ont pas de purge automatique : prévoir leur conservation dans les sauvegardes et la gestion de l’espace disque. Se désinscrire retire les fichiers courants du joueur, mais pas les instantanés historiques déjà créés.

Après un redémarrage, une génération interrompue est signalée et la préparation revient à l’état fermé. Les validations individuelles interrompues doivent être relancées en redéposant les fichiers concernés. Aucune génération n’est relancée automatiquement. Les tableaux Discord sont restaurés lorsque les fils existent encore et que le bot y a accès.

Le rattachement évite les lancements simultanés. Une interruption entre la création du fil de suivi Discord et sa sauvegarde SQLite nécessite toutefois de vérifier les fils existants avant de recommencer.

## Vérification

Les tests couvrent les inscriptions concurrentes, les permissions, la propriété des YAML, la disponibilité, les instantanés, les erreurs et redémarrages, le rattachement et les routes HTTP avec identité liée au jeton. Le générateur est simulé dans les tests. Un essai réel Discord et Archipelago reste nécessaire sur l’instance déployée.
