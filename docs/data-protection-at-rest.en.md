# Data protection at rest

This PR7 unit protects private identifiers required for tracking without changing the Archipelago protocol or responses shown to authorized users.

## Architecture

AST encrypts these columns before writing them to SQLite:

- `ChannelsAndUrlsTable.Room`;
- `ChannelsAndUrlsTable.Tracker`;
- `UrlAndChannelPatchTable.Patch`.

Each value uses AES-256-GCM with a random 96-bit nonce, a 128-bit tag, and authenticated context bound to its column. The `astenc:v1:` format distinguishes envelopes from legacy plaintext. Two writes of the same value produce different envelopes.

`BaseUrl` contains only the HTTP(S) origin, without a room identifier, and remains plaintext for network-limit grouping. Portal tokens remain hashed and the Discord token remains supplied by the environment; AST does not copy either into SQLite.

## Key management

The preferred source is:

```dotenv
AST_DATA_PROTECTION_KEY=BASE64_ENCODED_32_BYTES
```

Generation example:

```bash
openssl rand -base64 32
```

Without the variable, AST generates `AST.data-protection.key` next to `AST.db`. On Unix, it requests mode `0600`. This provides a configuration-free upgrade, but a key injected by the deployment secret manager is preferred for containers and multiple instances.

Back up the key separately, keep it out of Git, and never change it while the database contains envelopes. `DataProtectionMetadata.KeyCheck` verifies the key on every startup. A missing, corrupt, or different key fails closed before the bot starts.

## Migration 5.0.11

The transactional migration:

1. creates and verifies the encrypted key check;
2. encrypts every existing room and tracker identifier;
3. encrypts every non-empty patch link;
4. accepts already migrated envelopes, making it idempotent.

The migration also enables `secure_delete` while rewriting rows and truncates the WAL after commit so former plaintext pages do not remain in active SQLite files.

Readers continue to accept legacy plaintext during transition. New writes are always encrypted. Room lookup decrypts candidates from the same guild and origin instead of comparing randomized ciphertext.

AST creates its usual pre-migration SQLite backup before changing data. That backup still contains the former plaintext values: restrict its permissions and delete it after the rollback window. To roll back, stop AST, restore that backup with the previous binary, then retain the key in case the encrypted version is used again.

## Limits and risks

- Encryption protects an isolated copy of `AST.db`, not against an attacker who can read both the database and local key or process memory.
- Automatic key rotation is not included in this unit.
- Losing the key makes protected values unrecoverable.
- SQLite backups created before migration must be treated as sensitive.

Migration and tracking logs no longer print room identifiers, patch links, or complete HTTP exceptions that could include a URL.

## Verification

```bash
dotnet test tests/ArchipelagoSphereTracker.Tests/ArchipelagoSphereTracker.Tests.csproj -c Release
dotnet build ArchipelagoSphereTracker.sln -c Release
```
