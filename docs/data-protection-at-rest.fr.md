# Protection des secrets au repos

Ce lot PR7 protège les identifiants privés nécessaires au suivi sans modifier le protocole Archipelago ni les réponses visibles par les utilisateurs autorisés.

## Architecture

AST chiffre les colonnes suivantes avant leur écriture dans SQLite :

- `ChannelsAndUrlsTable.Room` ;
- `ChannelsAndUrlsTable.Tracker` ;
- `UrlAndChannelPatchTable.Patch`.

Chaque valeur utilise AES-256-GCM avec un nonce aléatoire de 96 bits, un tag de 128 bits et un contexte authentifié propre à la colonne. Le format `astenc:v1:` permet de distinguer les enveloppes du plaintext historique. Deux écritures de la même valeur produisent des enveloppes différentes.

`BaseUrl` conserve uniquement l'origine HTTP(S), sans identifiant de room, et reste en clair pour le regroupement des limites réseau. Les tokens du portail restent hachés et le token Discord reste fourni depuis l'environnement : AST ne les recopie pas dans SQLite.

## Gestion de la clé

La source prioritaire est :

```dotenv
AST_DATA_PROTECTION_KEY=BASE64_DE_32_OCTETS
```

Exemple de génération :

```bash
openssl rand -base64 32
```

Sans variable, AST génère `AST.data-protection.key` à côté de `AST.db`. Sous Unix, le mode demandé est `0600`. Cette solution assure une mise à niveau sans configuration, mais une clé injectée par le gestionnaire de secrets du déploiement est préférable pour les conteneurs et les instances multiples.

La clé doit être sauvegardée séparément, rester hors de Git et ne jamais être modifiée tant que la base contient des enveloppes. Le marqueur `DataProtectionMetadata.KeyCheck` vérifie la clé à chaque démarrage. Une clé absente, corrompue ou différente provoque un échec fermé avant le lancement du bot.

## Migration 5.0.11

La migration transactionnelle :

1. crée et vérifie le marqueur de clé ;
2. chiffre toutes les rooms et tous les trackers existants ;
3. chiffre tous les liens de patch non vides ;
4. accepte les enveloppes déjà migrées afin d'être idempotente.

La migration active aussi `secure_delete` pendant la réécriture et tronque le journal WAL après le commit afin que les anciennes pages plaintext ne restent pas dans les fichiers actifs de SQLite.

Les lecteurs acceptent encore le plaintext historique pendant la transition. Les nouvelles écritures sont toujours chiffrées. La recherche d'une room déchiffre les candidates d'une même guilde et origine au lieu de comparer un ciphertext aléatoire.

AST crée sa sauvegarde SQLite pré-migration habituelle avant le changement. Cette sauvegarde contient encore les anciennes valeurs en clair : limitez strictement ses permissions et supprimez-la après la période de rollback. Pour revenir en arrière, arrêtez AST, restaurez cette sauvegarde avec l'ancien binaire, puis conservez la clé pour revenir ultérieurement à la version chiffrée.

## Limites et risques

- Le chiffrement protège une copie isolée de `AST.db`, pas un attaquant capable de lire simultanément la base et la clé locale ou la mémoire du processus.
- La rotation automatique de clé n'est pas incluse dans ce lot.
- La perte de clé rend les valeurs protégées irrécupérables.
- Les sauvegardes SQLite antérieures à la migration doivent être traitées comme sensibles.

Les journaux de migration et de suivi n'affichent plus les identifiants de room, les liens de patch ni les exceptions HTTP complètes susceptibles de contenir une URL.

## Vérification

```bash
dotnet test tests/ArchipelagoSphereTracker.Tests/ArchipelagoSphereTracker.Tests.csproj -c Release
dotnet build ArchipelagoSphereTracker.sln -c Release
```
