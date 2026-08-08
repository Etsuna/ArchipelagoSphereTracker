# AST Companion

AST Companion is a small desktop mascot for ArchipelagoSphereTracker users who want personal item notifications without keeping Discord open.

## MVP behaviour

- connects with the existing AST user portal URL (`/portal/{guildId}/{channelId}/{token}/`)
- calls the existing authenticated summary endpoint
- watches received items every few seconds
- announces newly detected items
- reacts differently to traps
- keeps a small local history
- can stay always on top
- stores only the portal URL and local UI preferences on the player's machine

The companion does **not** connect to Discord and does **not** connect directly to Archipelago.

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

## Pairing

For this first version the user pastes their existing AST portal URL, for example:

```text
https://ast.example/portal/123456789/987654321/random-token/
```

The client extracts the AST base URL, guild ID, channel ID and token, then reads:

```text
/api/portal/{guildId}/{channelId}/{token}/summary
```

A later version can replace the long portal URL with a short pairing code while keeping the same client model.

## Next steps

- short-lived pairing codes and revocable companion tokens
- push events with Server-Sent Events or WebSocket instead of polling
- proper mascot sprite/animation assets
- native desktop notifications and optional sounds
- hints and check-count views
- autostart and tray controls
- signed installers/releases
