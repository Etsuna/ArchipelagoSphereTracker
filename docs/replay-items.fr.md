# Réémettre les objets d'un joueur

La migration `5.0.17` ajoute `TrackingReplayRequests`. Elle permet de tester les notifications des objets reçus avant V2, y compris ceux repris dans la référence silencieuse, sans effacer d'objets ni fabriquer de nouvelles réceptions dans le journal métier.

Démarrer la nouvelle compilation pour appliquer la migration, puis exécuter dans sa base `AST.db` :

```sql
INSERT INTO TrackingReplayRequests (GuildId, ChannelId, ReceiverSlot)
SELECT GuildId, ChannelId, Slot
FROM TrackingSlots
WHERE GuildId = 'GUILD_ID'
  AND ChannelId = 'CHANNEL_ID'
  AND Alias = 'Player1';
```

Remplacer les IDs et le nom canonique exact du joueur. Pour un suivi dans un thread, utiliser l'ID du thread. Cette insertion peut être faite avec le bot démarré ; valider l'écriture dans le client SQLite. **Chaque insertion demande une nouvelle réémission volontaire.** Une sélection ne correspondant à aucun slot n'insère aucune demande.

Le bot connecté prépare une livraison à partir du dernier snapshot V2 complet pour les objets. Si la room vient de V1 et n'a pas encore de snapshot natif utilisable, la demande reste en attente. La synchronisation habituelle ou l'action « Synchroniser maintenant » dans `/ast` peut fournir cette référence. La demande est ensuite reprise automatiquement.

Le destinataire est choisi par son identifiant de slot, dans la guilde et le salon indiqués. Le contenu est figé sur le snapshot sélectionné. Les autres joueurs et rooms restent exclus. Les transferts entre joueurs sont groupés et découpés comme les notifications normales. Le mode silencieux, les abonnements, les exclusions et les filtres de catégories s'appliquent également à la réémission.

La demande crée un événement distinct `ItemsReplayRequested` et une livraison indépendante. Elle ne modifie ni le snapshot courant, ni l'inventaire, ni les checks, ni les objectifs, ni les récapitulatifs personnels. Les reçus et les pages envoyées sont conservés pour reprendre après un échec.

## Consulter le résultat

```sql
SELECT r.Id, r.ReceiverSlot, r.Status AS RequestStatus,
       r.ItemCount, r.ReasonCode,
       d.Status AS DeliveryStatus, d.LastErrorCode, d.ExternalReceiptId
FROM TrackingReplayRequests r
LEFT JOIN EventDeliveries d ON d.EventId = r.EventId
WHERE r.GuildId = 'GUILD_ID'
  AND r.ChannelId = 'CHANNEL_ID'
ORDER BY r.Id DESC
LIMIT 10;
```

- `Pending` : la demande attend sa préparation ; `WAITING_FOR_ITEM_SNAPSHOT` indique qu'une synchronisation native complète est nécessaire.
- `Queued` : la demande est préparée. Lire `DeliveryStatus` pour savoir si elle est `Pending`, `Delivering`, `Failed` ou `Delivered`.
- `Empty` : aucun transfert éligible n'est présent dans le snapshot pour ce joueur.
- `Rejected` : le salon suivi ou le slot n'existe plus.

`ItemCount` compte les transferts avant application des préférences de notification. Un reçu `skipped:notification-preferences` signifie que ces préférences ont supprimé tous les messages ; il ne signifie pas qu'un message a été envoyé.

La suppression des données de la room supprime ses demandes. L'arrêt automatique après tous les checks terminés attend les demandes encore à préparer et les livraisons en cours.
