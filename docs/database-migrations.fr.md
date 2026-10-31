# Ordonnancement des migrations SQLite

`Program.CheckBdd` lit la version de la base puis demande le plan à
`src/SqlCommands/DatabaseMigrations.cs`. Chaque migration est enregistrée une seule
fois, avec sa version et sa méthode. Les versions sont comparées numériquement
avec `System.Version` : `5.0.10` vient après `5.0.9`.

| Version de départ | Traitement |
| --- | --- |
| Version absente (`-1`), non renseignée (`0`) ou antérieure à V2 | Conversion V1 vers `5.0.0`, puis migrations V2 nécessaires et nettoyage des anciennes tables |
| Version V2 connue, plus ancienne | Seulement les migrations suivantes, dans l'ordre |
| Version cible déjà installée | Aucun changement ni nouvelle sauvegarde |
| Version inconnue, plus récente ou cible non enregistrée | Arrêt avant toute migration ou sauvegarde ; aucune conversion V1 de secours |

Par exemple, de `5.0.18` vers `5.0.21`, le plan contient uniquement `5.0.19`,
`5.0.20` et `5.0.21`.

## Sauvegarde et erreurs

La sauvegarde préalable, les méthodes de migration et le délai de la chaîne
restent ceux du fonctionnement précédent. L'exécution est séquentielle : une
erreur empêche de lancer les étapes suivantes. Le numéro de version cible n'est
écrit qu'après la réussite de toute la chaîne, comme auparavant.

Chaque migration conserve ses propres transactions. La chaîne complète ne forme
pas une transaction unique : si une étape échoue, les étapes précédentes peuvent
déjà avoir été validées. Le registre n'ajoute ni retour arrière global ni garantie
de relance automatique des anciennes étapes non idempotentes. La sauvegarde reste
disponible pour la récupération.

## Ajouter une migration

1. Ajouter sa méthode dans `DBMigration_5` et son message console.
2. Ajouter une entrée dans `DatabaseMigrations.Steps`, dans l'ordre des versions.
3. Mettre à jour `Declare.BddVersion`.
4. Étendre les tests de sélection pour la nouvelle version et tester la migration
   sur le schéma précédent.

Il n'est plus nécessaire de modifier une branche par version de départ dans
`Program.cs`. Les tests vérifient les plans de toutes les versions V2 actuellement
connues, la conversion des anciens points d'entrée, le refus des versions non
prises en charge et les erreurs de sauvegarde/migration. Les migrations récentes
sont également exercées avec SQLite et une sauvegarde réelle ; les tests de plan
n'exécutent pas les anciens appels réseau de conversion V1.
