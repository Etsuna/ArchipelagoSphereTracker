# PR 9 — `/ast` command center specification

> Status: `/ast` command center implemented; compatibility mode restored with the direct commands from `v5.6.7`.

## Goal

AST originally registered 47 slash commands: 35 general commands and 12 additional commands in Archipelago mode. PR 9 initially replaced the entire public command surface with one command, with an optional attachment for imports:

```text
/ast
```

It opens a personal, ephemeral and context-aware command center. The current compatibility mode also publishes every direct command present in `v5.6.7`, so users can choose either interface. No permanent control message is posted in a channel or room thread.

## Product rules

- Discord registers `/ast` and the direct commands from `v5.6.7` at the same time.
- The interface is ephemeral and visible only to its requester.
- Navigation edits the same private response instead of posting messages.
- The home screen adapts to a tracked room thread, a regular guild channel or an invalid context.
- Visibility is filtered by role, while every action is authorized again on the server at click and confirmation time.
- Search and bounded pagination are mandatory for large player, slot, item and room lists.
- Sensitive values and user input never appear in component custom IDs.
- Destructive operations require explicit confirmation.
- Existing business logic is moved behind typed services shared by Discord and the Web portal; fake slash-command objects must not be constructed.

## Navigation

In a tracked room, `/ast` displays room health and progress, then offers:

1. `My space`
2. `The room`
3. `Manage room` for room managers
4. `Archipelago tools` in Archipelago mode, unless the member is explicitly restricted
5. `AST administration` for guild managers and the instance owner
6. `AST instance` for the configured instance owner
7. `Help`

In a regular guild channel it displays accessible rooms, room setup, global health, administration, and help according to the actor’s permissions. In an untracked thread it explains that no room is associated and returns to the guild home. Direct messages are rejected in this first version.

## Legacy command mapping

### General commands (35)

| Legacy command | `/ast` destination | Interaction | Access |
|---|---|---|---|
| `get-aliases` | Room → Associations | private paginated/searchable list | Member; full Discord mapping only for managers |
| `add-alias` | My space → Slots → Associate | notification-filter then slot selectors; slots may be shared by several users | Member |
| `delete-alias` | My space → Slots → Dissociate | own-slot selector + confirmation | Member |
| `update-frequency-check` | Manage → Polling | merged polling workflow | Room manager |
| `add-url` | Guild home → Configure room | setup workflow | Guild manager |
| `ast-setup` | Guild home → Configure room | existing setup workflow embedded in `/ast` | Guild manager |
| `update-silent-option` | Manage → Notifications | normal/silent selector | Room manager |
| `delete-url` | Manage → Delete room | typed confirmation + final confirmation | Room manager |
| `status-games-list` | Room → Progress | searchable pagination | Member |
| `ast-health` | Guild home → AST health | guild summary and room selector | Guild manager |
| `ast-room-health` | Room → Tracking health | live private view | Member |
| `ast-sync-now` | Manage → Sync now | direct action | Room manager |
| `ast-pause` | Manage → Pause | confirmation | Room manager |
| `ast-resume` | Manage → Resume | direct action | Room manager |
| `ast-polling` | Manage → Polling | mode and interval selectors | Room manager |
| `info` | Room → Information | private view | Member |
| `get-patch` | My space → Patch | authorized-slot selector + private delivery | Member |
| `recap-all` | My space → Recap → All my slots | pagination | Member |
| `recap` | My space → Recap → One slot | selector + pagination | Member |
| `recap-and-clean` | My space → Recap → Show and clear | selector + confirmation | Member |
| `clean` | My space → Recap → Clear one | selector + confirmation | Member |
| `clean-all` | My space → Recap → Clear all | strong confirmation | Member |
| `hint-from-finder` | My space → Hints → Found by slot | selector + pagination | Member |
| `hint-for-receiver` | My space → Hints → Received by slot | selector + pagination | Member |
| `list-items` | My space → Items | selector, filters and pagination | Member |
| `analyze-spoiler-log` | Manage → Spoiler → Analyze | guided analysis form | Room manager |
| `send-spoiler-log` | `/ast file:<spoiler.txt>` | native Discord attachment | Room manager |
| `apworlds-info` | Help → APWorlds | private information view | Member |
| `discord` | Help → Community | link button | Member |
| `excluded-item` | My space → Exclusions → Add | own-slot and item selectors | Member, own data only |
| `excluded-item-list` | My space → Exclusions | personal pagination | Member |
| `delete-excluded-item` | My space → Exclusions → Remove | selector + confirmation | Member, own data only |
| `ast-user-portal` | My space → Portal | issue/revoke | Member |
| `ast-room-portal` | Manage → Portal | issue/revoke | Room manager |
| `ast-portal` | Administration → Portal | issue/revoke | Guild manager |

### Archipelago-mode commands (12)

| Legacy command | `/ast` destination | Interaction | Access |
|---|---|---|---|
| `list-yamls` | Archipelago tools → YAML → Files | pagination | Member unless explicitly restricted |
| `list-apworld` | Archipelago tools → APWorld → Files | pagination | Member unless explicitly restricted |
| `backup-yamls` | Archipelago tools → YAML → Backup | private download | Member unless explicitly restricted |
| `backup-apworld` | Archipelago tools → APWorld → Backup | private download | Member unless explicitly restricted |
| `download-template` | Archipelago tools → Templates | selector + private download | Member unless explicitly restricted |
| `delete-yaml` | Archipelago tools → YAML → Delete | selector + confirmation | Member unless explicitly restricted |
| `clean-yamls` | Archipelago tools → YAML → Delete all | strong confirmation | Member unless explicitly restricted |
| `send-yaml` | `/ast file:<players.yaml>` | native Discord attachment | Member unless explicitly restricted |
| `generate-with-zip` | `/ast file:<players.zip>` | native attachment + balancing choice | Member unless explicitly restricted |
| `send-apworld` | `/ast file:<world.apworld>` | native Discord attachment | Member unless explicitly restricted |
| `generate` | Archipelago tools → Generation → Run | confirmation + balancing choice | Member unless explicitly restricted |
| `test-generate` | Archipelago tools → Generation → Test | confirmation | Member unless explicitly restricted |

Personal exclusions are deliberately reclassified: storage is already user-scoped, so members may manage only their own exclusions. Global operations remain manager-only.

## Private uploads

Discord buttons and modals cannot request attachments, so `/ast` keeps an optional `file` parameter. Normal mode accepts only `.txt` and `.json` spoiler logs. Archipelago mode additionally routes YAML, generation ZIP and APWorld files, and exposes the `skip-prog-balancing` option. The historical direct upload commands also remain available. Authorization runs before processing, and existing size, quarantine, extension and content validation remains mandatory. The `/ast` response is ephemeral and no sensitive file is requested in a public message. Explicit Web-portal buttons remain available as a parallel system, but Discord imports do not depend on the portal.

## Session and security model

Each `/ast` opening creates a 15-minute in-memory session bound to its owner, guild, source channel and selected room. It stores the current screen, page, filters and drafts. Component IDs contain only the router prefix, an opaque session ID and a stable action ID. Stolen, expired or out-of-context interactions are rejected.

Permissions are checked when rendering, clicking and immediately before a write. Destructive actions and configuration changes retain correlation-based security auditing. Outputs neutralize mentions and respect Discord limits. Private portal links are scoped and revocable.

In Archipelago mode, every guild member can use YAML, APWorld, generation and template tools by default. Native Discord administrators (`guild owner`, `Administrator`, or `Manage Server`) and the configured instance owner can maintain a guild-scoped deny list under `AST administration → Archipelago access restrictions`. A denied member loses the `/ast` section and is rejected by the equivalent direct slash commands and Web operations. The instance owner cannot be denied, which preserves a recovery path. Delegated AST managers cannot edit this deny list.

## Implementation shape

1. `SlashCommandDefinitions` registers `/ast` and the `v5.6.7` command surface.
2. `AstCommandCenter` renders context-aware screens.
3. `AstInteractionRouter` handles buttons, menus and modals by stable action ID.
4. `AstUiSessionStore` owns isolation and expiration.
5. Slash-command-dependent methods are moved behind typed requests carrying actor, guild, channel and parameters.
6. Discord and Web adapters call the same services.
7. Slow work defers immediately and completes through the private interaction response.

Bulk command overwrite publishes `/ast` together with the exact direct-command set from `v5.6.7`. Direct commands pass through the current authorization matrix and audit layer before invoking their historical handlers. Existing room, association, recap, exclusion, YAML, APWorld, portal and audit data is retained.

## Accepted product decisions

1. `/ast file:` is the native Discord upload path for YAML, ZIP, APWorld and spoiler logs; the private portal remains an explicit parallel path.
2. Members may download patches only for their associated slots; room managers may access every room slot.
3. Members see room slots, their own Discord association and public game data. The complete Discord-to-slot mapping is manager-only.
4. Recap cleanup remains under an Advanced section with confirmation.
5. `/ast` is rejected outside guilds in the first version.

## Acceptance criteria

- Exactly `/ast` and the `v5.6.7` commands applicable to the current operating mode are registered.
- All 47 legacy commands have a working destination above.
- UI visibility and server-side authorization match the actor’s role.
- Normal navigation never posts into the channel or thread.
- A 3,000-player room remains usable through search and bounded pagination.
- Expired, stolen and replayed interactions are rejected; confirmations are single-use.
- Uploads retain quarantine and validation controls.
- The full build and test suite pass without warnings.
