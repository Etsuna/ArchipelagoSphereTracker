# Upload quarantine and validation

This PR7 unit hardens YAML, APWorld, generation ZIP, and spoiler inputs without changing commands or active file paths.

## Applied flow

1. AST checks the simple filename, extension, and advertised size.
2. The stream is copied with a strict limit to `extern/upload-quarantine`, under an opaque GUID name ending in `.quarantine`.
3. The closed file is validated for its type.
4. Only an accepted file is atomically moved to its destination. On rejection, limit overflow, cancellation, or error, the quarantined file is deleted and the previous active file remains intact.

Startup and every new upload make a best-effort cleanup of expired residue. User-provided names are never used inside quarantine.

## Checks by type

- YAML: non-empty UTF-8 text without NUL bytes.
- APWorld: readable ZIP archive, at most 500 entries and 256 MiB uncompressed, without absolute paths or `..` components.
- Generation ZIP: the same limits, with YAML files only at archive root.
- Spoiler: `.txt` or `.json` name and non-empty UTF-8 text; JSON must have an object or array at its root. A new version deletes the old one only after validation.

These checks limit malformed files, path traversal, and ZIP bombs. They are neither antivirus scanning nor proof that code inside an APWorld is trustworthy; installation remains restricted to the instance owner.

## Configuration and operations

```dotenv
WEB_MAX_UPLOAD_BYTES=67108864
UPLOAD_QUARANTINE_RETENTION_MINUTES=60
SPOILER_LOG_RETENTION_DAYS=30
```

Accepted ranges are 5–1440 minutes and 1–365 days respectively. No SQLite schema change is required.

To roll back, restore direct-copy calls in the four file handlers and remove startup cleanup. The two new variables may remain defined with no effect on an older release.

## Verification

```bash
dotnet test tests/ArchipelagoSphereTracker.Tests/ArchipelagoSphereTracker.Tests.csproj -c Release
dotnet build ArchipelagoSphereTracker.sln -c Release
```
