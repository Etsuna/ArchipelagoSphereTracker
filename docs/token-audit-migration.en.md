# Security migration 5.0.6

## Problem and design

Portal tokens were permanent and stored in plaintext, while sensitive operations had no persistent correlated audit trail.

Version `5.0.6` introduces one 256-bit active token per guild/channel/user, stores only its SHA-256 digest, applies fixed expiry and explicit revocation, and renders personal pages dynamically. Sensitive actions produce `Started`, `Succeeded`, `Failed`, or `Denied` audit records with no command arguments, tokens, URLs, aliases, or filenames.

## Migration

Before any migration, AST creates a consistent SQLite backup in `extern/database-backups`. The transactional and idempotent migration then rebuilds `PortalAccessTable`, hashes existing tokens, and creates indexed `SecurityAuditLogTable`. Existing links remain valid until expiry, rotation, or revocation. Legacy personal-page directories, whose names could contain plaintext tokens, are removed at startup after dynamic rendering is enabled. Upgrade chaining was fixed so databases from `5.0.0` through `5.0.4` run every intermediate migration.

## Validation and rollback

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
dotnet test ArchipelagoSphereTracker.sln --configuration Release
```

Tests cover legacy migration and replay, plaintext absence, rotation, revocation, expiry, and audit retention. Requesting a new link invalidates the previous one, including AST Companion links. Rollback requires restoring the pre-upgrade `AST.db` backup because version `5.0.5` does not understand `TokenHash`.
