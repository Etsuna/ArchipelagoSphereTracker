# Suivi et notifications Discord V2

Le suivi V2 est désormais natif et toujours actif. `ENABLE_TRACKING_V2` est obsolète et ignoré. Le choix du scheduler (`USE_LEGACY_TRACKING_SCHEDULER`) est indépendant. Voir [la migration native `5.0.16`](tracking-v2-native.fr.md).

## Bascule

La migration SQLite `5.0.14` ajoute `TrackedRooms.NotificationsV2Initialized`. La migration `5.0.15` ajoute `DiscordNotificationBatches` pour conserver les groupes, les pages et leurs reçus de livraison. Le démarrage habituel sauvegarde la base avant migration. Les données et préférences existantes sont conservées.

Au premier poll V2 de chaque room, son état courant devient une référence silencieuse. Cela vaut aussi pour les rooms déjà enregistrées par l'ancien dual-write. Aucun changement antérieur à cette référence n'est annoncé. Les livraisons expérimentales `DiscordChannel` restent disponibles pour diagnostic et ne sont jamais réclamées par le nouveau worker `DiscordNotificationV2`.

Les polls suivants enregistrent atomiquement le snapshot, les nouveaux événements et les livraisons nécessaires. Un échec de cette transaction fait échouer le poll ; il ne provoque pas de publication de secours V1. Les commandes lisent les projections natives V2, mises à jour dans la même transaction. Les récapitulatifs personnels sont alimentés une seule fois par transfert. L'export des hints au moment de la configuration reste disponible.

## Notifications

- Les transferts d'objets entre slots donnent une notification `ItemReceived`. `ItemSent` reste dans le journal, sans seconde notification du même transfert. Les objets d’une même synchronisation sont regroupés par slot destinataire et par liste de mentions, avec un compteur. Les groupes longs sont découpés sans perdre d’objet.
- Les hints créés ou modifiés sont annoncés avec les mentions des joueurs concernés ; ils restent masqués en mode silencieux.
- Les objectifs reposent sur le statut Archipelago `CLIENT_GOAL`, indépendamment du pourcentage de checks. En mode silencieux, ils sont annoncés uniquement pour les slots ayant un abonnement.
- Les abonnements, exclusions personnelles et filtres de catégories d'objets s'appliquent aux destinataires de chaque objet, y compris lorsqu'un slot est partagé.
- Les checks individuels, statuts et événements d'activité restent dans le journal sans ajouter de messages Discord.

Les préférences sont relues avant la préparation du premier envoi. Le contenu et les mentions des pages sont ensuite figés pour reprendre exactement le même groupe après une panne. Les notifications filtrées sont acquittées avec un reçu `skipped:notification-preferences`. Les noms et messages longs sont bornés pour respecter les limites Discord. Seuls les identifiants explicitement sélectionnés peuvent être mentionnés ; les noms de joueurs ne peuvent pas déclencher `@everyone`.

Les trois destinations sont prises en charge : thread privé, thread public et salon texte existant. Aucun intent privilégié n'est ajouté : seul `Guilds` est utilisé.

### Présentation des nouveaux messages

Les groupes affichent le joueur et le nombre d'objets dans le titre, les mentions sur une ligne dédiée, puis les objets en gras avec leur expéditeur et leur lieu en dessous. Le corps conserve la barre verticale de citation à gauche. Exemple sans abonnement :

```text
📦 **Objets pour Player2** · **(2)**

>>> - **Chewing-gum de Ressort**
  ↳ De **Player1** · *Contrôle municipal 002*
- **Double saut**
  ↳ De **Player3** · *Ancienne tour*
```

Les nouveaux indices (`🔎`), les indices actualisés (`🧭`) et les objectifs (`🏁`) utilisent la même présentation. Les pages indiquent `1/2`, `2/2`, etc. après le titre. Les noms provenant des jeux sont échappés pour préserver la mise en forme ; les filtres et mentions restent inchangés.

Cette présentation s'applique aux nouveaux groupes et aux nouvelles réémissions. Les groupes déjà figés conservent leur texte exact pour permettre la reprise d'un envoi interrompu. Aucun ancien message Discord n'est modifié.

## Reprise et permissions

Le worker démarre et s'arrête avec le suivi, attend une connexion Discord et reprend les livraisons échouées ou dont le lease a expiré. Les livraisons restent ordonnées par destination. Une erreur dans une room ne bloque pas les autres rooms.

Les messages sont du texte simple, sans identifiant technique visible ni footer. Les groupes et les identifiants de messages Discord sont conservés dans SQLite. Chaque page est acquittée séparément : après une panne au milieu d’un groupe, les pages acquittées ne sont pas renvoyées.

Après un résultat d’envoi incertain, la reprise parcourt l’historique depuis le début de la tentative et recherche le texte exact parmi les propres messages du bot. Les reçus déjà attribués à un autre groupe sont exclus. Les anciens footers peuvent encore être reconnus pour acquitter un envoi interrompu avant la mise à jour ; aucun nouveau footer n’est ajouté. Les erreurs de lecture d’historique restent des échecs de livraison. Les réessais automatiques des POST sur timeout/502 sont désactivés dans le SDK ; l’outbox pilote les reprises.

Le bot doit disposer de **Voir le salon**, **Voir les anciens messages**, **Intégrer des liens** et des permissions d'envoi adaptées à la destination. Pour un thread, les permissions héritées sont vérifiées sur son salon parent. Ces permissions faisaient déjà partie de l'invitation recommandée. La récupération compare uniquement les propres messages du bot et ne nécessite pas `Message Content`, conformément à [l’exception Discord pour les propres messages de l’application](https://docs.discord.com/developers/events/gateway#message-content-intent).

Le transport reste « au moins une fois » : la suppression ou la modification du message de récupération peut permettre un doublon après un incident. Un autre envoi simultané du bot ayant exactement le même texte peut aussi rendre la recherche ambiguë. Une panne réseau ambiguë ne permet pas de promettre un « exactement une fois » absolu. Les leases bornent le traitement et les accusés sont conditionnés au numéro de tentative courant.

L'arrêt automatique lorsque tous les checks sont terminés attend les notifications en cours. Un arrêt explicite du suivi ou la suppression de ses données annule les livraisons restantes par les cascades SQLite existantes.

## Activation et rollback

Après déploiement, démarrer AST normalement. Les migrations jusqu'à `5.0.18` s'appliquent avec sauvegarde préalable ; la dernière termine l'import V1 et supprime ses quatre anciennes tables de suivi. Aucun processus de bot ni test sur Discord n’a été lancé pour cette modification.

Le retour à V1 via une variable d’environnement est retiré. Un retour à une ancienne version demande de réinstaller l’ancien exécutable avec sa sauvegarde de base correspondante, car les tables V1 ont été supprimées de la base active.

## Validation

Les tests couvrent la migration répétée, la bascule depuis le dual-write, le rollback, les transactions interrompues, les préférences individuelles, les réponses partielles, le parcours de plusieurs pages d'historique et la panne après un envoi accepté. Le transport Discord est simulé ; les tests n'envoient pas de messages sur un serveur réel.

Références : [messages et historique Discord](https://docs.discord.com/developers/resources/message), [implémentation Discord.Net 3.18](https://github.com/discord-net/Discord.Net/blob/3.18.0/src/Discord.Net.Rest/Entities/Channels/ChannelHelper.cs).
