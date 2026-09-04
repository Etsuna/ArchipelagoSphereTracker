# Stockage SQLite des identifiants

Depuis la version de base `5.0.12`, AST stocke directement en clair :

- `ChannelsAndUrlsTable.Room` ;
- `ChannelsAndUrlsTable.Tracker` ;
- `UrlAndChannelPatchTable.Patch`.

Aucune clé, paire PEM ni procédure de récupération n'est nécessaire. `AST_DATA_PROTECTION_KEY` et les anciennes commandes de génération, configuration et rotation ont été retirées.

Les tokens du portail restent hachés et le token Discord reste fourni par l'environnement : ces secrets ne sont pas recopiés en clair dans SQLite.

## Migration depuis 5.0.11

La migration `5.0.12` détecte les anciennes enveloppes `astenc:v1:`, les déchiffre transactionnellement avec l'ancienne variable `AST_DATA_PROTECTION_KEY` ou l'ancien fichier `AST.data-protection.key`, puis supprime `DataProtectionMetadata` et `DataProtectionRecoveryMetadata`.

La clé historique n'est requise que pendant cette migration. AST crée sa sauvegarde SQLite pré-migration habituelle avant la conversion. Après vérification du bon fonctionnement de la base `5.0.12` et expiration de la période de rollback, l'ancien fichier de clé et les fichiers PEM peuvent être supprimés.

Pour convertir sans démarrer le bot, exécuter depuis le dossier qui contient `AST.db` :

```powershell
ArchipelagoSphereTracker.exe --UpdateBDD
```

Une erreur de clé ou une enveloppe corrompue annule toute la transaction. La base reste alors en `5.0.11` et la sauvegarde n'est pas modifiée.

## Conséquence assumée

Toute personne obtenant une copie de `AST.db` ou d'une sauvegarde `5.0.12` peut lire ces identifiants et liens. Ce comportement est volontaire : ils sont considérés comme partageables et la simplicité d'exploitation est privilégiée.
