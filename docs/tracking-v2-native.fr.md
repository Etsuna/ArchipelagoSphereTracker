# Migration vers le suivi natif V2

Depuis la base `5.0.16`, le suivi, les commandes Discord, le portail, les catalogues d'exclusion, l'analyse spoiler et les métriques lisent les données V2. L'ancien pipeline de comparaison/publication V1 et le double enregistrement du suivi sont retirés.

## Source de vérité et lectures

`WebHostSnapshotParser` lit les identifiants et les tuples Archipelago directement, sans convertir les modèles V1. `ProcessingContext` sert de cache de définitions des slots et des datapackages ; il lit `TrackingSlots` et `TrackingPlayerStates`.

`TrackingV2Store.ApplySnapshotAsync` consolide les sections connues, puis enregistre dans une transaction :

1. le snapshot normalisé ;
2. les projections `TrackingSlots`, `TrackingItems`, `TrackingHints` et `TrackingPlayerStates` ;
3. les nouveaux événements et leurs livraisons ;
4. les nouvelles entrées des récapitulatifs personnels, une seule fois par transfert ;
5. la date du dernier nouvel objet.

Une exception annule ces modifications ensemble. Les sections d'objets et de hints inchangées ne sont pas réinsérées à chaque changement d'activité. Les objectifs utilisent `CLIENT_GOAL` ; les checks évoluent indépendamment du statut client. Une section absente conserve la dernière valeur connue, alors qu'une section complète et vide représente réellement un état vide.

Les abonnements, exclusions et récapitulatifs personnels restent des données utilisateur distinctes. Les associations utilisent les noms canoniques des slots ; les alias d'affichage peuvent changer sans modifier les identifiants de transfert. Vider un récapitulatif personnel ne supprime pas l'historique de la room et ne le réimporte pas au poll suivant.

## Import d'une installation existante

Le démarrage crée sa sauvegarde habituelle avant migration. La migration `5.0.16` est transactionnelle et répétable :

- Les données d'affichage V1 sont copiées dans les projections pour rester consultables avant le premier poll natif.
- Les anciens objets et hints dépourvus d'identifiants bruts gardent des IDs SQL `NULL` et une identité provisoire `legacy:Id`. Aucun identifiant Archipelago n'est inventé.
- Une room V2 déjà active recharge ses projections depuis son dernier snapshot, qui fait autorité sur les anciennes lignes V1.
- Une ancienne room V1 ou shadow établit une référence silencieuse au premier poll natif. Ses sections provisoires restent consultables tant que l'API ne fournit pas leur état complet.
- Les configurations, préférences, récapitulatifs, snapshots, événements et livraisons déjà présents sont conservés. L'import ne crée aucune notification historique.
- Un marqueur de migration empêche un second passage de réintroduire l'ancien historique après son remplacement par un état natif.

La migration `5.0.18` termine cet import et supprime `AliasChoicesTable`, `DisplayedItemTable`, `GameStatusTable` et `HintStatusTable`. Elle relit les dernières données V1, y compris sur une base déjà passée en `5.0.16` ou `5.0.17`. Les snapshots des rooms V2 actives gardent la priorité. L'import final et la suppression des tables partagent une transaction : un échec conserve les tables sources. Une référence étrangère provenant d'une table personnalisée bloque la suppression pour préserver ses données.

Les anciennes tables sont supprimées même lorsqu'elles sont vides et ne sont plus recréées au démarrage. Les configurations, abonnements, exclusions et récapitulatifs personnels restent dans leurs tables dédiées. Aucun `DROP TABLE` manuel n'est nécessaire.

## Configuration et exploitation

`ENABLE_TRACKING_V2` est retiré du choix de moteur. Une ancienne valeur, même `false`, est ignorée avec un message au démarrage : seul le suivi natif V2 fonctionne. `USE_LEGACY_TRACKING_SCHEDULER` reste indépendant et change uniquement la planification des polls.

Le regroupement des notifications par joueur et par destinataires, le découpage en pages et les reçus de reprise restent inchangés. Les nouveaux messages ne contiennent aucun footer technique. Les trois destinations et l'absence d'intents privilégiés sont conservées.

Pour revenir à un ancien exécutable, utiliser sa sauvegarde de base correspondante : les tables V1 ont été supprimées de la base active. La sauvegarde préalable reste conservée sur disque avec les anciennes données ; elle n'est pas restaurée automatiquement.

Cette modification a été vérifiée sur des bases temporaires. Le bot n'a pas été lancé et la base de l'instance en fonctionnement n'a pas été migrée par les tests.

## Validation

Depuis `5.0.17`, une [file de réémission explicite](replay-items.fr.md) permet de tester les objets repris de V1 et de la référence silencieuse, sans modifier les réceptions métier ni les récapitulatifs.

Les tests couvrent l'import sans perte des préférences, la répétition et le rollback transactionnel de la migration, la suppression des quatre tables sans recréation, les références personnalisées, la sauvegarde, la priorité des snapshots V2 actifs et de leurs notifications en attente, les réponses partielles au premier poll, les récapitulatifs personnels et leur remise à zéro, les modifications de checks sans statut client et la suppression en cascade.
