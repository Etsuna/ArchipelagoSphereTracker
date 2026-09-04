# SQLite identifier storage

Since database version `5.0.12`, AST stores these values directly as plaintext:

- `ChannelsAndUrlsTable.Room`;
- `ChannelsAndUrlsTable.Tracker`;
- `UrlAndChannelPatchTable.Patch`.

No key, PEM pair, or recovery procedure is required. `AST_DATA_PROTECTION_KEY` and the former generation, configuration, and rotation commands have been removed.

Portal tokens remain hashed and the Discord token remains supplied through the environment; those secrets are not copied as plaintext into SQLite.

## Migration from 5.0.11

Migration `5.0.12` detects former `astenc:v1:` envelopes, decrypts them transactionally with the old `AST_DATA_PROTECTION_KEY` variable or `AST.data-protection.key` file, then removes `DataProtectionMetadata` and `DataProtectionRecoveryMetadata`.

The historical key is required only during this migration. AST creates its usual pre-migration SQLite backup before conversion. After confirming that database version `5.0.12` works and the rollback window has expired, the old key file and PEM files can be deleted.

To convert without starting the bot, run this from the directory containing `AST.db`:

```powershell
ArchipelagoSphereTracker.exe --UpdateBDD
```

A wrong key or corrupt envelope rolls the entire transaction back. The database then remains at `5.0.11` and the backup is unchanged.

## Accepted consequence

Anyone who obtains a copy of `AST.db` or a `5.0.12` backup can read these identifiers and links. This is intentional: they are treated as shareable data, and operational simplicity is preferred.
