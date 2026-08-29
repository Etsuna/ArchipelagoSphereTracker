# AST security model

Authorization decisions are centralized in `AstAuthorizationService` and recomputed server-side. Browsers never choose their Discord identity or role.

## Authorization levels

| Level | Scope | Accepted identities |
|---|---|---|
| Guild member | read operations, personal recap and alias | Discord member who can still access the channel or thread |
| Room manager | room configuration/deletion, thread portal, patch listing | thread owner, `Manage Threads`, or guild manager |
| Guild manager | room creation, YAML, generation, global portal | guild owner, administrator, `Manage Server`, or instance owner |
| Instance owner | APWorld install, backup, and loading | `AST_OWNER_USER_ID`; guild owner fallback when unset |

Discord commands and Web requests use the same matrix. Portal tokens are bearer secrets bound to a guild, channel, and user; AST additionally verifies current Discord membership, channel access, and the required level.

## Web, files, and network boundaries

- Legacy tokenless administration URLs return `404`; all scoped APIs carry a user token.
- Generated downloads are authenticated, stored outside the public static tree, and retained for one hour.
- Portal responses use no-store caching, no-referrer, CSP, frame denial, and nosniff headers.
- Upload names and extensions are validated. The default limit is 64 MiB (`WEB_MAX_UPLOAD_BYTES`). Generation ZIPs accept at most 500 flat YAML entries and 256 MiB uncompressed.
- APWorld files contain executable code and are restricted to the instance owner.
- Room URLs must be exact HTTP(S) `/room/{id}` URLs. Private, local, link-local, reserved, and multicast addresses are blocked during validation and on each HTTP connection. Explicit private hosts require `ARCHIPELAGO_ALLOWED_HOSTS`.
- Logs and `ast_channel_info` no longer expose room, tracker, patch URL, or server port secrets.

Portal URLs must be treated like passwords. Explicit token rotation and expiry remain a later hardening item.
