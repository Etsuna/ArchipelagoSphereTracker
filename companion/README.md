# AST Companion — Aster

AST Companion is the personal desktop companion for ArchipelagoSphereTracker. Its mascot, **Aster**, watches the authenticated AST user portal and reacts to the player's received items and hints without requiring Discord to stay open.

## Aster experience

- Aster uses artwork derived directly from the approved concept sheets rather than a simplified vector substitute
- compact transparent borderless desktop-pet window
- draggable and position is remembered locally
- no normal taskbar entry during play
- native system-tray menu for show/hide, settings/history, always-on-top, reconnect and quit
- closing the pet or settings hides it instead of terminating the background companion
- explicit **Quit AST Companion** command stops the application cleanly
- optional always-on-top mode
- gentle idle motion plus state accents
- distinct reactions for progression, useful items, normal deliveries, traps and hints
- sleeps visually when AST is unavailable and reacts when the connection returns
- queues reactions so bursts of items are shown one by one
- local history of recent items and hints in a separate settings window

## Connection model

The user pastes their existing authenticated AST portal URL:

```text
https://ast-bot.com/portal/{guildId}/{channelId}/{token}/
```

The companion extracts the base URL, guild, channel and token, then reads:

```text
/api/portal/{guildId}/{channelId}/{token}/summary
```

Only the portal URL and UI preferences are persisted on the player's machine. AST Companion does **not** connect to Discord and does **not** connect directly to the Archipelago room.

## Tray controls

The tray icon is the permanent control surface for the Companion:

- **Afficher / Masquer Aster**
- **Paramètres et historique**
- **Toujours au-dessus : basculer**
- **Reconnecter AST**
- **Quitter AST Companion**

Double-clicking Aster also opens the settings window.

## States

| AST event | Aster state |
| --- | --- |
| Connected / idle | Floating idle |
| Normal item | Delivering item |
| Progression / required | Progression celebration |
| Useful item | Useful item reaction |
| Trap | Trap reaction |
| New hint | Reading hint |
| AST unreachable | Sleeping / offline |
| Connection restored | Wake / reconnect |

## Run from source

```bash
dotnet run --project companion/AST.Companion/AST.Companion.csproj
```

## Publish

Windows x64:

```bash
dotnet publish companion/AST.Companion/AST.Companion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Linux x64:

```bash
dotnet publish companion/AST.Companion/AST.Companion.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

The Companion GitHub Actions workflow builds and publishes downloadable CI artifacts for Windows and Linux.

## Design language

Aster is an original fantasy forest messenger designed for AST:

- moss-green hood: `#6B7F4A`
- sage accents: `#8BA26B`
- cream fabric: `#F3EAD2`
- leather satchel: `#8B5E34`
- turquoise magic: `#4FD6C6`
- soft gold: `#E7C36A`

See `companion/ASTER_DESIGN.md` for the canonical character rules.

## Later improvements

The current portal URL is deliberately reused to keep the server-side migration small. Natural follow-ups are short pairing codes/revocable companion tokens, push events (SSE/WebSocket), optional sounds, autostart and signed release installers.
