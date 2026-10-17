# Préparer une room Archipelago

Disponible uniquement en **Archipelago Mode**. La préparation rassemble les inscriptions, YAML et disponibilités avant génération.

## Créer et participer

Depuis le salon souhaité, lancer `/organize-room` et choisir un nom. La limite est automatiquement fixée à 200 joueurs et 200 slots, sans champ de capacité à remplir. La création exige les permissions de gestion reconnues par AST. Le bouton correspondant de `/ast` et le formulaire du portail d’administration sont également disponibles.

AST publie le tableau et ses boutons directement dans le salon courant. Aucun nouveau fil n’est créé. La visibilité suit les permissions du salon. L’organisateur peut organiser sans s’inscrire comme joueur. Dans un fil privé existant, il peut inviter un membre, qui devra ensuite s’inscrire.

1. Cliquer **S’inscrire**.
2. Déposer ses YAML dans ce même salon via `/send-yaml file:…`, `/ast file:…`, `/organize-room fichier:…` (option `file` en anglais), ou le portail. Les commandes et le portail utilisent la même préparation.
3. Attendre la validation Archipelago, puis cliquer **Prêt**.

Un changement ou retrait de fichier remet son propriétaire à **Pas prêt**. Chaque joueur ne peut télécharger, remplacer ou supprimer que ses propres YAML. Le tableau expose les slots, jeux et statuts, sans publier le contenu des YAML ou les diagnostics personnels des autres joueurs.

Limites : 1 Mio UTF-8 et 20 documents par fichier, 20 fichiers par joueur, 200 slots et 16 Mio de YAML par préparation. Les noms avec variables ou pondérations sont résolus à la génération.

## Relancer les joueurs

L’organisateur dispose du bouton **Relancer les non-prêts**, sur Discord et sur le portail, ainsi que de l’action correspondante de `/organize-room`.

La relance mentionne uniquement les inscrits qui ont au moins un fichier YAML et ne sont pas prêts. Elle leur demande si leurs réglages sont définitifs ou s’ils préparent encore leurs fichiers. Aucun rôle ni `@everyone` n’est mentionné. Un délai de cinq minutes entre deux relances est conservé en DB, y compris après redémarrage. Un échec ou un envoi partiel garde ce délai pour éviter les répétitions.

## Générer et récupérer le ZIP

**Fermer les inscriptions** bloque seulement les nouveaux inscrits ; les participants peuvent encore corriger leurs fichiers. **Tester les YAML** teste l’ensemble sans ZIP et sans exiger que tous soient prêts.

**Générer** exige au moins un joueur, tous prêts, et au moins un YAML valide par joueur. AST fige les fichiers, bloque les modifications, reteste l’ensemble puis génère le ZIP. Chaque processus est limité à 30 minutes. Les validations et générations de ce parcours sont exécutées une à la fois. Un échec de génération conserve la préparation pour permettre sa correction.

Après réussite :

- Si Discord accepte la pièce jointe, AST envoie le ZIP dans le salon, puis supprime les données et fichiers de préparation.
- Si la pièce jointe ne peut pas être envoyée, AST publie un bouton de téléchargement valable **24 heures**, puis supprime les données et fichiers de préparation. Seule une copie temporaire du ZIP subsiste, sans inscription, YAML ou historique de préparation en DB. Le lien donne accès à l’archive à toute personne qui le possède, comme la pièce jointe accessible depuis le salon.
- Si le ZIP et le lien ne peuvent pas être publiés, AST conserve les fichiers. Le bouton **Réessayer la transmission** permet à l’organisateur de reprendre l’envoi sans régénérer.

Le portail web doit être actif et son URL publique (`WEB_BASE_URL`) correctement configurée pour que le lien soit utilisable à distance. Les archives temporaires expirées sont supprimées au démarrage et toutes les heures lorsque le portail tourne ; un accès à un lien expiré est refusé et retire aussi le fichier.

Le message de préparation devient un message de fin sans noms de joueurs ni boutons actifs. Le salon est immédiatement disponible pour une nouvelle préparation. Le portail ouvert indique que la préparation est terminée et arrête ses actualisations.

L’hébergement reste manuel : héberger le ZIP, puis utiliser `/add-url` ou le parcours habituel de `/ast` pour créer le suivi. Le rattachement depuis l’ancienne préparation n’est plus proposé, puisque celle-ci a été supprimée.

## Annuler et nettoyer

**Annuler** supprime définitivement les inscriptions, YAML courants, instantanés, historiques de génération, reçus d’envoi, informations de relance et fichiers de travail. Les validations individuelles déjà lancées se terminent avant la suppression de leurs fichiers. Une génération en cours reste verrouillée et ne peut pas être annulée par ce bouton.

Les suppressions sont limitées au répertoire de la préparation concernée sous `data/room-preparations/`. Les fichiers SQLite sont supprimés logiquement par des suppressions de lignes et leurs cascades ; les sauvegardes existantes ne sont pas réécrites. Les journaux d’audit techniques et les accès généraux au portail sont indépendants de la préparation et restent conservés. Le suivi des rooms existantes n’est pas supprimé.

Au redémarrage, AST reprend le nettoyage des préparations annulées et de celles dont la transmission a été enregistrée. Une génération interrompue revient à l’état fermé ; les validations individuelles interrompues doivent être relancées en redéposant les fichiers concernés. Aucune génération n’est relancée automatiquement.

La publication Discord et SQLite ne forment pas une transaction unique : un arrêt entre la publication du ZIP ou du lien et l’enregistrement de son reçu peut entraîner un nouvel envoi à la reprise.

## Vérification

Les tests couvrent les permissions, inscriptions concurrentes, propriété des YAML, validations, disponibilité, instantanés, suppressions en cascade et sur disque, réutilisation du salon, téléchargement temporaire après suppression, expiration, reprise et relances ciblées avec délai persistant. Le générateur et les envois sont simulés ; un essai réel Discord/Archipelago reste à faire sur l’instance déployée.
