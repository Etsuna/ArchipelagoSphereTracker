# PR 8 — Discord `/ast-setup` assistant

## Problem

Creating a room required users to know all six `/add-url` parameters. Channel, thread-type, or frequency mistakes were only visible after submitting the command. The historical success response could also echo the private room URL.

## Architecture

`/ast-setup` opens an ephemeral Discord assistant. An in-memory session, scoped to the user, guild, and interaction channel, keeps the draft for 15 minutes of inactivity. Components contain only a random session identifier and an action; the room URL is never placed in a `custom_id`, preview, or log.

The flow lets an organizer:

1. select a guild text channel through a native channel selector;
2. enter the WebHost URL and thread name in a modal;
3. choose a private thread, public thread, or public thread with member addition;
4. choose normal or silent notifications;
5. set the minimum polling frequency;
6. review the preview, then confirm or cancel.

The preview displays only the validated host name. On confirmation, AST consumes the session to prevent double submission, revalidates the `GuildManager` role, guild, and target channel, then invokes the same service as `/add-url`. SSRF/URL validation, public WebHost API reads, thread creation, encrypted persistence, and tracking startup therefore remain centralized. AST never connects to the Archipelago protocol.

Discord-to-slot associations and notification detail levels are not faked in this PR: they require the persistent preference model planned in their dedicated PRs. The assistant provides the integration point once those records exist.

## Permissions and audit

- Starting and confirming require the application `GuildManager` role.
- Authorization is enforced server-side; button visibility is not treated as permission.
- The selected target must be a text channel in the same guild and cannot be a thread.
- Denied attempts and confirmations are audited as `RoomAdd`, with a correlation ID and no sensitive arguments.
- The legacy `/add-url` success response no longer repeats the complete private URL.

## Files changed

- `src/Bot/AstSetupWizard.cs`: sessions, components, modal, preview, and confirmation.
- `src/Bot/BotCommands.cs`: Discord interaction handler registration.
- `src/Bot/SlashCommandDefinitions.cs`: `/ast-setup` definition.
- `src/Bot/UrlClass.cs`: reusable structured result and secret-free response.
- `src/Security/SecurityAuditLog.cs`: audit classification.
- `tests/ArchipelagoSphereTracker.Tests/AstSetupWizardTests.cs` plus command and authorization tests.
- `README.md` and this bilingual documentation.

## Migration

No database migration is required. Drafts intentionally do not survive a restart; confirmed rooms use the existing persistence model.

## Validation

```powershell
dotnet build ArchipelagoSphereTracker.sln --configuration Release
dotnet test tests\ArchipelagoSphereTracker.Tests\ArchipelagoSphereTracker.Tests.csproj --configuration Release
```

Tests cover user/guild/channel isolation, expiry, replacement of an older session, atomic consumption on confirmation, private room identifier masking, component definitions, and audit classification.

## Risks and rollback

Sessions are local to one process, so a restart requires the organizer to start again. Discord or WebHost creation may still fail after confirmation; the user then receives a generic failure or the existing service's controlled response. Rollback removes the command and its three interaction handlers, then restores the internal `UrlClass` return type. No data migration or deletion is required.
