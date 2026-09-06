# AST security model

Authorization decisions are centralized in `AstAuthorizationService` and recomputed server-side. Browsers never choose their Discord identity or role.

## Authorization levels

| Level | Scope | Accepted identities |
|---|---|---|
| Guild member | read operations, personal recap and alias, upload and analysis of the room's shared spoiler | Discord member who can still access the channel or thread |
| Room manager | room configuration/deletion, thread portal, patch listing | thread owner, `Manage Threads`, or guild manager |
| Guild manager | room creation and management, YAML, generation, global portal | guild owner, administrator, `Manage Server`, delegated AST manager, or instance owner |
| Instance owner | inspect, control, and clean every guild and channel stored by the instance | the exact user configured in `AST_OWNER_USER_ID` |

A guild owner, administrator, or member with `Manage Server` can use `/ast` → `AST administration` → `AST managers` to grant or revoke the application-level `GuildManager` role. It does not change Discord roles and delegated managers cannot delegate again. Bindings are persisted per guild since SQLite migration `5.0.13`, and changes are audited.

`AST_OWNER_USER_ID` no longer falls back to the Discord guild owner. When configured, that user gets an `AST instance` console in `/ast` to browse stored guilds and rooms, inspect health, force synchronization, pause/resume tracking, and clean data after confirmation. This global authority also covers orphaned entries whose Discord guild or channel is gone.

Discord commands and Web requests use the same matrix. Portal tokens are bearer secrets bound to a guild, channel, and user; AST additionally verifies current Discord membership, channel access, and the required level.

## Web, files, and network boundaries

- Legacy tokenless administration URLs return `404`; all scoped APIs carry a user token.
- SQLite stores only a SHA-256 token digest. Issuing a link replaces the previous one and it expires after `PORTAL_TOKEN_LIFETIME_DAYS` days.
- Passing `revoke:true` to a portal command invalidates the active link without creating another.
- Personal pages are rendered dynamically, so legacy HTML files cannot bypass expiry or revocation.
- Generated downloads are authenticated and stored outside the public static tree with the room data. They are deleted when its thread or URL is removed.
- Portal responses use no-store caching, no-referrer, CSP, frame denial, and nosniff headers.
- Upload names and extensions are validated. The default limit is 64 MiB (`WEB_MAX_UPLOAD_BYTES`). Each upload is first written under an opaque name in quarantine outside active folders, validated, then atomically promoted. A rejected file therefore never replaces the active copy. Temporary quarantine residue is cleaned according to `UPLOAD_QUARANTINE_RETENTION_MINUTES`. A room's active spoiler and YAML files do not expire: they are deleted with the URL or thread.
- Generation ZIPs accept at most 500 flat YAML entries and 256 MiB uncompressed. APWorld archives must be readable, stay within the same limits, and contain no absolute or traversal path. YAML and text spoilers must be non-empty UTF-8 text without NUL bytes; a `.json` spoiler must contain a valid JSON object or array. See [upload quarantine and validation](upload-quarantine-security.en.md).
- APWorld files contain executable code. In Normal mode they remain restricted to the instance owner; in single-guild Archipelago mode they are also available to that guild's managers.
- The global `extern/Archipelago/custom_worlds` directory is excluded from every room and guild cleanup. Custom worlds are removed only by a dedicated manual action.
- Room URLs must be exact HTTP(S) `/room/{id}` URLs. Private, local, link-local, reserved, and multicast addresses are blocked during validation and on each HTTP connection. Explicit private hosts require `ARCHIPELAGO_ALLOWED_HOSTS`.
- Logs and `ast_channel_info` no longer expose room, tracker, patch URL, or server port secrets.

Portal URLs must be treated like passwords. Requesting a new link rotates the token and immediately invalidates the previous URL.

Sensitive actions are stored in `SecurityAuditLogTable` with UTC time, correlation ID, source, Discord actor, guild, channel, action, and outcome. Command arguments, tokens, URLs, aliases, and filenames are never recorded. Retention is controlled by `AUDIT_RETENTION_DAYS`; `/api/portal/{guild}/{channel}/{token}/audit` is restricted to guild managers.

## SQLite storage

Since SQLite migration `5.0.12`, room identifiers, tracker identifiers, and patch links are stored as plaintext in SQLite. They are treated as shareable configuration data and AST no longer requires an encryption key. Portal tokens remain hashed and the Discord token remains supplied through the environment. See [identifier storage](data-protection-at-rest.en.md).
