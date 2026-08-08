# Aster — Canonical Design Guide

Aster is the official mascot of **AST Companion**.

## Character concept

Aster is a small original forest-adventurer spirit who acts as an inter-world messenger. Aster is not a robot, virtual assistant, or generic software mascot. The character should always read first as a fantasy video-game companion.

Core traits:

- oversized moss-green hood with a tiny leaf sprout
- cream face and simple light tunic
- small brown leather messenger satchel
- floating turquoise magical orb
- soft gold clasp / progression accents
- warm, expressive, slightly mischievous personality
- compact silhouette that stays readable at desktop-pet scale

## Canonical palette

| Name | Hex |
| --- | --- |
| Moss Green | `#6B7F4A` |
| Sage Green | `#8BA26B` |
| Cream Beige | `#F3EAD2` |
| Leather Brown | `#8B5E34` |
| Turquoise Magic | `#4FD6C6` |
| Soft Gold | `#E7C36A` |

## Runtime states

### Idle
Aster floats softly while the orb pulses nearby. Expression is calm and curious.

### Delivering item
Aster presents a small parcel / item and looks happy to deliver it.

### Progression
Gold accents and sparkles. Aster celebrates visibly and proudly.

### Useful
Positive, warm reaction with the useful object motif.

### Trap
Comedic panic. Aster shakes, reacts to spikes, and looks surprised rather than distressed.

### Hint
Aster becomes focused and reads a scroll / clue.

### Offline
Aster curls up under the hood and sleeps. The orb remains nearby but subdued.

### Reconnect
Aster wakes up with a short celebratory magical reaction.

## UI rules

- Aster is the persistent character; notification cards are temporary speech / delivery surfaces.
- The normal play state should remain visually light and non-blocking.
- Never show a large permanent dashboard unless the player explicitly opens history/settings.
- Trap reactions should be funny, not alarming.
- The orb is Aster's signature magical element and should remain turquoise in normal states.
- Progression may introduce soft gold; traps may introduce muted red accents.

## Animation rules

Keep animation small and readable:

- idle: gentle vertical float
- orb: slow pulse
- progression: sparkles and a stronger bounce
- trap: short horizontal shake
- offline: no floating; sleeping pose
- reconnect: bright wake-up accent

Avoid constant high-motion animation because the companion sits next to active gameplay.

## Brand boundaries

Aster should evoke a cozy fantasy action-adventure atmosphere while remaining an original AST character. Do not copy recognizable costumes, symbols, weapons, fairies, logos, UI frames, or silhouettes from existing game franchises.
