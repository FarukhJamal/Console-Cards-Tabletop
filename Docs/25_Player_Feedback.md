# 25 — Player Feedback

Version 0.2 · Status: Approved plan (2026-10-13); F1 implemented, F2–F4 planned

## Purpose

Players must see what the game did and what it is about to do. Today most results only reach the one-line
message strip. This document sets one feedback system for every game: events from the game's own records,
shown on screen and on the board with authored prefabs (Final UI, doc 26). Nothing is built at runtime.

## Gaps it closes

| Moment | Before | With feedback |
|---|---|---|
| Turn changes | Turn strip text | Banner and ping in the player's colour; ring on their pawn |
| A Trap waits on your tile | Nothing on the board | Pulsing warning on the tile until resolved |
| End Turn will apply a Trap | Nothing until pressed | End Turn button warns and lists what it applies |
| Spring, Crane, Dodge, Rush target | Tile tint | Player-coloured target and an arrow from the pawn |
| End Turn moves a pawn | Pawn jumps | Short visible hop along the path |
| Eliminated, Key claimed, collapse, round start | Message strip | Banner, camera kick, log entry |
| Blind, Slow, Sticky, Overcharged | Status card text | Effect chips on the player in the turn strip |
| Missed something | Message replaced | "What happened" log |

## Design

1. **Feedback events.** A platform event: kind, title, subtitle, player colour, objects involved, importance.
   Trap Floor adds an adapter from its activity feed (flips, Traps, Abilities, eliminations, Keys, collapse,
   turn and round changes), so no new rules code is needed.
2. **Screen (Final UI prefabs).** Announcement banner (one at a time from a queue, about 1.5 s, click to skip,
   never blocks); message stack replacing the single strip (info, warning, loss; fades); effect chips;
   End Turn warning; foldable "What happened" log of the last 20 events.
3. **Turn ping.** No pass-the-device pause. The new player gets a banner in their colour, a pulse on their
   pawn and a sound cue (an audio slot per event kind; sounds added later).
4. **Board (world prefabs, pooled).** Active pawn ring and bobbing arrow; waiting-Trap outline and "!" marker
   (green tick once neutralised); move target outline with an arrow; flip flash; collapse effect; pawn hop
   for automatic moves (presentation only; the authority state changes at once).
5. **Camera.** Short effects that always return: a small shake for a Trap or elimination, or a quick
   focus-and-back on an event elsewhere on the board. The camera never stays moved.
6. **Rules.** Prefabs only; no per-frame allocations (pooled markers and banners); one design language
   across games; a Table-menu switch Feedback: Full / Minimal.

## Stages

| Stage | Content | Status |
|---|---|---|
| UI-F | Final UI prefabs, no layout from code (doc 26) | Implemented |
| F1 | Feedback event model, Trap Floor adapter, banner, message stack, turn ping | Implemented |
| F2 | Board markers: active pawn, waiting Trap, move target and arrow, flip and collapse effects, camera kick | Planned |
| F3 | End Turn warning, effect chips | Planned |
| F4 | "What happened" log, pawn hop | Planned |

## F1 record (2026-10-13)

- **Prefabs** (Final UI, created by Console Cards > UI > Create Missing Gameplay UI Prefabs): `FeedbackBanner`
  (top centre, Notification layer) and `FeedbackMessages` (bottom centre, four authored rows). Timings, colours,
  queue size and sounds are Inspector fields on them.
- **Sounds**: `Content/Audio/Feedback/` — TurnPing, Trap, Eliminated, Reward, Collapse. Replace a file, or the
  clip slot on the banner prefab, to change a sound.
- **Banners**: turn start ("PLAYER 2 (RED) — YOUR TURN", with the ping), floor turn, Trap flipped, Key /
  Exit found, Key claimed, floor collapsed, player eliminated, everyone out, escaped, win.
- **Messages**: every game message now goes to the stack (info, warning, loss, gain); the bottom strip keeps
  only the standing status. Abilities used, Blind/Slow/Sticky applied, prizes found and skips also appear.
- **Source**: the Trap Floor activity feed and the turn state; a rebuilt session (Undo, Reset) is not
  re-announced. A new game greets the first player.
- **Switch**: `Player Feedback` on the composition (Inspector) turns it off; the Table-menu switch comes with F3.
  Without the prefabs the game falls back to the old status strip and logs one warning.
