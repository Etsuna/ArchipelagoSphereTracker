# Préparer une room Archipelago

Disponible uniquement en **Archipelago Mode**. La préparation rassemble les inscriptions, YAML et disponibilités avant génération.

## Créer et participer

Depuis le salon souhaité, lancer `/organize-room` et choisir un nom. La limite est automatiquement fixée à 200 joueurs et 200 slots, sans champ de capacité à remplir. La création exige les permissions de gestion reconnues par AST. Le bouton correspondant de `/ast` et le formulaire du portail d’administration sont également disponibles.

Choisir un salon textuel existant dans la liste. AST y publie le tableau et ses boutons. Les inscriptions et les YAML se gèrent dans ce salon, et les accès se règlent dans Discord.

Choisir ensuite où installer le suivi après la génération. Il peut utiliser le salon de préparation ou un fil de ce salon. Ce fil reprend la visibilité du salon parent.

1. Cliquer **S’inscrire**.
2. Déposer ses YAML dans ce même salon via `/send-yaml file:…`, `/ast file:…`, `/organize-room fichier:…` (option `file` en anglais), ou le portail. Les commandes et le portail utilisent la même préparation.
3. Attendre la validation Archipelago, puis cliquer **Prêt**.

Un changement ou retrait de fichier remet son propriétaire à **Pas prêt**. Chaque joueur ne peut télécharger, remplacer ou supprimer que ses propres YAML. Le tableau expose les slots, jeux et statuts, sans publier le contenu des YAML ou les diagnostics personnels des autres joueurs.

Pour un envoi groupé, sélectionner plusieurs `.yaml`/`.yml` dans le sélecteur de la page, ou envoyer un ZIP avec `/send-yaml file:…`, `/ast file:…` ou `/organize-room fichier:…`. Dans une préparation, le ZIP importe les fichiers sans lancer de génération. Il peut contenir des sous-dossiers sûrs : les noms de fichiers sont conservés sans le chemin. Les noms en double et les fichiers autres que YAML sont refusés. Limites : 20 fichiers par lot et 16 Mio pour le ZIP et son contenu, toujours 1 Mio par YAML. Le lot est vérifié avant toute écriture : un nom, format ou quota invalide ne modifie aucun fichier existant. Chaque YAML accepté est ensuite validé séparément par Archipelago, avec son propre résultat.

Limites : 1 Mio UTF-8 et 20 documents par fichier, 20 fichiers par joueur, 200 slots et 16 Mio de YAML par préparation. Les noms avec variables ou pondérations sont résolus à la génération.

## Relancer les joueurs

La fermeture des inscriptions relance automatiquement les inscrits non prêts. Dès que tous les inscrits sont prêts, le bot mentionne l’organisateur et les gestionnaires AST délégués ayant accès au salon. Le même état prêt ne déclenche qu’une notification.

L’organisateur et les gestionnaires autorisés par AST disposent du bouton **Relancer les non-prêts**, sur Discord et sur le portail, ainsi que de l’action correspondante de `/organize-room`.

La relance mentionne tous les inscrits qui ne sont pas prêts. Ceux qui n’ont pas encore envoyé de YAML sont invités à déposer leurs fichiers ou à prévenir l’organisateur s’ils ne souhaitent plus participer. Ceux qui ont déjà déposé des YAML reçoivent une demande de confirmation de leurs réglages et de leur disponibilité. Aucun rôle ni `@everyone` n’est mentionné. Un délai de cinq minutes entre deux relances est conservé en DB, y compris après redémarrage. Un échec ou un envoi partiel garde ce délai pour éviter les répétitions.

## Générer et récupérer le ZIP

### Programmer la génération

La création sans WebHost propose un délai facultatif en heures, par exemple **72**.
Avec un WebHost, programmez le timer depuis la préparation pour fournir votre
identifiant de session. Sans délai, la préparation reste manuelle. Le bouton **Timer de génération**, l’action
`/organize-room action:schedule heures:72` (option `hours` en anglais) et le
formulaire du portail permettent de fixer ou remplacer cette échéance. **0**
désactive le timer. Limite : de 1 à 720 heures à partir de l’enregistrement.
L’heure exacte et la règle d’exclusion sont affichées sur le tableau et le portail.

Le bot relance les participants incomplets **24 h, 6 h et 1 h** avant l’échéance,
en mentionnant uniquement les personnes concernées. Les seuils déjà dépassés lors
de la programmation ne sont pas envoyés immédiatement. Après une interruption,
seul le rappel pertinent le plus proche est envoyé, sans rafale de rappels anciens.
Le délai de cinq minutes des relances manuelles est partagé avec les rappels automatiques.

À l’échéance, les modifications des participants sont bloquées. Le bot retient
uniquement les participants **prêts**, avec **au moins un YAML et tous leurs YAML
validés**. Un participant sans YAML, non prêt, ou possédant un fichier invalide
ou encore en validation est exclu de cette génération, avec tous ses fichiers.
Les données exclues restent disponibles pour correction si la génération échoue ;
elles suivent le nettoyage normal après transmission réussie de l’archive.
Le bot annonce le nombre de participants retenus et exclus, reteste l’ensemble,
puis génère et transmet le ZIP selon le parcours habituel.

S’il ne reste aucun participant éligible, aucune génération vide n’est lancée :
la préparation est fermée et un message explique la situation. En cas d’erreur
de génération, les données sont conservées. Il faut corriger puis générer
manuellement ou programmer une nouvelle échéance ; les échecs ne tournent pas en boucle.

Le contrôle des échéances se fait chaque minute quand le bot est connecté. Une
échéance non encore déclenchée survit à un redémarrage et sera traitée au retour
du bot si elle est dépassée. Une génération déjà lancée puis interrompue reste
à reprendre manuellement. Une génération manuelle consomme le timer actif pour
éviter un second lancement automatique.

Les gestionnaires reconnus par les droits Discord/AST existants (dont les rôles
délégués configurés dans AST) peuvent gérer la préparation même s’ils ne l’ont
pas créée : fermeture, réouverture, timer, relance, test, génération, annulation,
invitation et nouvelle tentative de transmission. Ils doivent avoir accès au
salon et aux outils Archipelago. Ils n’obtiennent pas le droit de télécharger,
modifier ou supprimer les YAML personnels des autres joueurs.

Le schéma additif `PreparationSchedules` est créé au démarrage, sans modifier les
préparations existantes ni activer de timer par défaut.

### Génération manuelle

**Fermer les inscriptions** bloque seulement les nouveaux inscrits ; les participants peuvent encore corriger leurs fichiers. **Tester les YAML** teste l’ensemble sans ZIP et sans exiger que tous soient prêts.

**Générer** exige au moins un joueur, tous prêts, et au moins un YAML valide par joueur. AST fige les fichiers, bloque les modifications, reteste l’ensemble puis génère le ZIP. Chaque processus est limité à 30 minutes. Les validations et générations de ce parcours sont exécutées une à la fois. Un échec de génération conserve la préparation pour permettre sa correction.

Après réussite :

- Si Discord accepte la pièce jointe, AST envoie le ZIP dans le salon, puis supprime les données et fichiers de préparation.
- Si la pièce jointe ne peut pas être envoyée, AST publie un bouton de téléchargement valable **24 heures**, puis supprime les données et fichiers de préparation. Seule une copie temporaire du ZIP subsiste, sans inscription, YAML ou historique de préparation en DB. Le lien donne accès à l’archive à toute personne qui le possède, comme la pièce jointe accessible depuis le salon.
- Si le ZIP et le lien ne peuvent pas être publiés, AST conserve les fichiers. Le bouton **Réessayer la transmission** permet à l’organisateur de reprendre l’envoi sans régénérer.

Le portail web doit être actif et son URL publique (`WEB_BASE_URL`) correctement configurée pour que le lien soit utilisable à distance. Les archives temporaires expirées sont supprimées au démarrage et toutes les heures lorsque le portail tourne ; un accès à un lien expiré est refusé et retire aussi le fichier.

Le message de préparation devient un message de fin sans noms de joueurs ni boutons actifs. Le salon est immédiatement disponible pour une nouvelle préparation. Le portail ouvert indique que la préparation est terminée et arrête ses actualisations.

L’URL du WebHost est facultative à la création. Avec un WebHost compatible, AST y envoie le ZIP, crée la room puis installe le suivi dans la destination choisie. Si un fil est demandé, AST le crée à partir du tableau de préparation et le réutilise si l’installation doit être réessayée. Les alias suivent les slots générés et leurs auteurs. Si cette correspondance est incomplète, le bot signale les alias à compléter.

La personne qui lance ou programme la génération saisit son identifiant
utilisateur en privé. AST fournit un lien vers la page `/session` du WebHost
choisi. Copiez l’identifiant unique affiché sur cette page. L’identifiant est
obligatoire pour l’hébergement automatique. AST le conserve chiffré pour le
timer et les éventuels nouveaux essais, puis le supprime dès que la room est
hébergée ou que l’organisateur termine manuellement.
Un WebHost HTTP doit être explicitement autorisé dans `ARCHIPELAGO_ALLOWED_HOSTS`.

Dans une session partagée, la reprise se base sur l’identifiant du seed confirmé
par le WebHost. Si la réponse à l’envoi du ZIP est perdue avant cette confirmation,
le bot conserve le ZIP et demande une vérification manuelle de la session.

En cas d’échec, le ZIP reste disponible. **Réessayer la transmission** reprend l’hébergement ou le suivi à partir du résultat déjà enregistré. **Terminer manuellement** permet de terminer la préparation avec le ZIP et, si elle existe déjà, l’URL de la room hébergée. Sans WebHost, héberger le ZIP puis utiliser `/add-url` dans le salon de préparation. Le bot rappelle la destination de suivi choisie.

## Annuler et nettoyer

**Annuler** supprime définitivement les inscriptions, YAML courants, instantanés, historiques de génération, reçus d’envoi, informations de relance et fichiers de travail. Les validations individuelles déjà lancées se terminent avant la suppression de leurs fichiers. Une génération en cours reste verrouillée et ne peut pas être annulée par ce bouton.

Les suppressions sont limitées au répertoire de la préparation concernée sous `data/room-preparations/`. Les fichiers SQLite sont supprimés logiquement par des suppressions de lignes et leurs cascades ; les sauvegardes existantes ne sont pas réécrites. Les journaux d’audit techniques et les accès généraux au portail sont indépendants de la préparation et restent conservés. Le suivi des rooms existantes n’est pas supprimé.

Au redémarrage, AST reprend le nettoyage des préparations annulées et de celles dont la transmission a été enregistrée. Une génération interrompue revient à l’état fermé ; les validations individuelles interrompues doivent être relancées en redéposant les fichiers concernés. Aucune génération n’est relancée automatiquement.

La publication Discord et SQLite ne forment pas une transaction unique : un arrêt entre la publication du ZIP ou du lien et l’enregistrement de son reçu peut entraîner un nouvel envoi à la reprise.

## Vérification

Les tests couvrent les permissions, inscriptions concurrentes, propriété des YAML, validations, disponibilité, instantanés, suppressions en cascade et sur disque, réutilisation du salon, téléchargement temporaire après suppression, expiration, reprise et relances ciblées avec délai persistant. Le générateur et les envois sont simulés ; un essai réel Discord/Archipelago reste à faire sur l’instance déployée.
