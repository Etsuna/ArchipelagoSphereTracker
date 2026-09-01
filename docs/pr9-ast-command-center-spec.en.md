# PR 9 — `/ast` command center specification

> Status: implemented on `codex/evolution`; real Discord validation remains required before merge.

## Goal

AST currently registers 47 slash commands: 35 general commands and 12 additional commands in Archipelago mode. PR 9 replaces the entire public command surface with one command, with an optional attachment for imports:

```text
/ast
```

It opens a personal, ephemeral and context-aware command center. Existing capabilities remain available through buttons, select menus, forms, guided workflows and explicit confirmations. No permanent control message is posted in a channel or room thread.

## Product rules

- Discord registers exactly one command, `/ast`.
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
4. `AST administration` for guild managers and the instance owner
5. `Refresh`

In a regular guild channel it displays accessible rooms, room setup, global health, administration, and help according to the actor’s permissions. In an untracked thread it explains that no room is associated and returns to the guild home. Direct messages are rejected in this first version.

## Legacy command mapping

### General commands (35)

| Legacy command | `/ast` destination | Interaction | Access |
|---|---|---|---|
| `get-aliases` | Room → Associations | private paginated/searchable list | Member; full Discord mapping only for managers |
| `add-alias` | My space → Slots → Associate | slot and notification-filter selectors | Member |
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
| `list-yamls` | Administration → YAML → Files | pagination | Guild manager |
| `list-apworld` | Administration → APWorld → Files | pagination | Instance owner |
| `backup-yamls` | Administration → YAML → Backup | private download | Guild manager |
| `backup-apworld` | Administration → APWorld → Backup | private download | Instance owner |
| `download-template` | Administration → YAML → Templates | selector + private download | Guild manager |
| `delete-yaml` | Administration → YAML → Delete | selector + confirmation | Guild manager |
| `clean-yamls` | Administration → YAML → Delete all | strong confirmation | Guild manager |
| `send-yaml` | `/ast file:<players.yaml>` | native Discord attachment | Guild manager |
| `generate-with-zip` | `/ast file:<players.zip>` | native attachment + balancing choice | Guild manager |
| `send-apworld` | `/ast file:<world.apworld>` | native Discord attachment | Instance owner |
| `generate` | Administration → Generation → Run | confirmation + balancing choice | Guild manager |
| `test-generate` | Administration → Generation → Test | confirmation | Guild manager |

Personal exclusions are deliberately reclassified: storage is already user-scoped, so members may manage only their own exclusions. Global operations remain manager-only.

## Private uploads

Discord buttons and modals cannot request attachments, so the single `/ast` command keeps an optional `file` parameter. Its extension routes the upload to YAML, generation ZIP, APWorld or spoiler-log handling. Authorization runs before processing, and existing size, quarantine, extension and content validation remains mandatory. The response is ephemeral and no sensitive file is requested in a public message. Explicit Web-portal buttons remain available as a parallel system, but Discord imports do not depend on the portal.

## Session and security model

Each `/ast` opening creates a 15-minute in-memory session bound to its owner, guild, source channel and selected room. It stores the current screen, page, filters and drafts. Component IDs contain only the router prefix, an opaque session ID and a stable action ID. Stolen, expired or out-of-context interactions are rejected.

Permissions are checked when rendering, clicking and immediately before a write. Destructive actions and configuration changes retain correlation-based security auditing. Outputs neutralize mentions and respect Discord limits. Private portal links are scoped and revocable.

## Implementation shape

1. `SlashCommandDefinitions` registers only `/ast`.
2. `AstCommandCenter` renders context-aware screens.
3. `AstInteractionRouter` handles buttons, menus and modals by stable action ID.
4. `AstUiSessionStore` owns isolation and expiration.
5. Slash-command-dependent methods are moved behind typed requests carrying actor, guild, channel and parameters.
6. Discord and Web adapters call the same services.
7. Slow work defers immediately and completes through the private interaction response.

Bulk command overwrite removes all 47 legacy Discord entries during deployment. Existing room, association, recap, exclusion, YAML, APWorld, portal and audit data is retained.

## Accepted product decisions

1. `/ast file:` is the native Discord upload path for YAML, ZIP, APWorld and spoiler logs; the private portal remains an explicit parallel path.
2. Members may download patches only for their associated slots; room managers may access every room slot.
3. Members see room slots, their own Discord association and public game data. The complete Discord-to-slot mapping is manager-only.
4. Recap cleanup remains under an Advanced section with confirmation.
5. `/ast` is rejected outside guilds in the first version.

## Acceptance criteria

- Exactly `/ast` is registered in both operating modes.
- All 47 legacy commands have a working destination above.
- UI visibility and server-side authorization match the actor’s role.
- Normal navigation never posts into the channel or thread.
- A 3,000-player room remains usable through search and bounded pagination.
- Expired, stolen and replayed interactions are rejected; confirmations are single-use.
- Uploads retain quarantine and validation controls.
- The full build and test suite pass without warnings.
