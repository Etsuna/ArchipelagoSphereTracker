# Migration sécurité 5.0.6

## Problème traité

Les tokens du portail étaient permanents et conservés en clair. Les actions sensibles ne disposaient pas d'un journal persistant corrélé.

## Architecture retenue

- Un seul token actif par `(serveur, salon, utilisateur)`, généré avec 256 bits aléatoires.
- SHA-256 uniquement dans SQLite, expiration fixe et révocation explicite.
- Rendu dynamique des pages personnelles afin que les fichiers hérités ne contournent pas le contrôle du token.
- Journal append-only applicatif avec événements `Started`, `Succeeded`, `Failed` ou `Denied` et rétention automatique.

## Migration

Avant toute migration, AST crée une sauvegarde SQLite cohérente dans `extern/database-backups`. La migration `5.0.6` reconstruit ensuite `PortalAccessTable`, hache les tokens existants et crée `SecurityAuditLogTable` avec ses index. Les liens existants restent valides jusqu'à leur expiration, rotation ou révocation. La migration est transactionnelle et idempotente. Les anciens dossiers de pages personnelles, qui pouvaient contenir le token dans leur nom, sont supprimés au démarrage après activation du rendu dynamique.

La chaîne de mise à niveau a également été corrigée : une base `5.0.0` à `5.0.4` exécute désormais toutes les migrations intermédiaires avant d'être marquée à jour.

## Validation

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
dotnet test ArchipelagoSphereTracker.sln --configuration Release
```

Les tests couvrent la migration d'une table historique, son rejeu, l'absence de token en clair, rotation, révocation, expiration et rétention de l'audit.

## Risques et rollback

- Demander un nouveau lien invalide immédiatement le précédent, y compris dans AST Companion.
- Tous les types de portail d'un même utilisateur et salon partagent le même token actif.
- Pour revenir à la version précédente, restaurer d'abord la sauvegarde `AST.db` créée avant mise à jour : le code `5.0.5` ne sait pas lire la colonne `TokenHash`.
