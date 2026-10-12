# 24 — Content Packs

Version 0.2 · Status: Approved direction, A1 implemented

## Purpose

A game's content is its cards, its rules and its art. This document sets how that content is
authored, packaged and replaced, so that one game template can ship with built-in content and
still let players bring their own. It is one system for every game; Trap Floor is the first user.

## Sources of content

There are three sources. All three feed the same runtime Data model (`GameDefinitionData`,
`CardDefinitionData`, `AvatarDefinitionData`), so gameplay code never knows where content came from.

| Source | What it holds | Who makes it |
|---|---|---|
| Built-in (ScriptableObjects) | The game's default cards, rules and art, inside the project | The game's author, in Unity |
| Game pack (JSON + PNGs) | A full or partial set of cards, rules and art for one game | Authors or advanced players |
| Player art overlay (PNGs) | Replacement pictures for existing cards only | Any player, from the game's menu |

ScriptableObjects remain the authoring source for built-in content. A JSON pack is the same content
in a portable form; exporting the built-in content gives a valid pack.

## Art keys

Every picture is named by a key string, not by a Unity asset:

- Card fronts: `CardDefinition.frontArtworkReference`, for example `trap-floor/fronts/shield.png`.
- Card backs: `CardDefinition.backArtworkReference`.
- Avatars: `AvatarDefinition.artworkReference`.
- Board (floor) card backs: `GameDefinition.boardCardBacks`, one per round in order, repeating.

The key is lower case, uses `/`, and starts with the game's folder name. It never changes when the
picture changes, which is what makes art replaceable: a new picture under the same key wins.

## Lookup order

When a card needs a picture, the first match wins:

1. Player upload for that key.
2. The active game pack's picture for that key.
3. The built-in texture on the ScriptableObject.
4. No picture: the placeholder colour and label are shown.

A1 implements step 3 only. Steps 1 and 2 arrive with A2–A4.

## Presentation rules

- When a card shows art, its tint is white and its placeholder labels are hidden.
- A collapsed floor tile shows no art and keeps its `HOLE (x,y)` label on both sides.
- Floor card backs follow the round: round 1 uses the first back, round 2 the second, and so on.

## Card sizes

- Every card has a size class: Standard or Large. `CardDefinition.sizeClass` defaults to Standard;
  `AvatarDefinition.cardSize` defaults to Large (avatar/hero cards are bigger cards).
- Presentation draws a Large card at `largeCardVisualScale` (1.486 by default) times a Standard card,
  including its click box. It is a drawing scale only; the card's position is unchanged.
- Avatar/hero cards never go into a hand.
- Card height in a Console slot comes from the slot itself (authored on the Console prefab), never from code.
- Art is drawn on the supplied card model's two material slots: `face` takes the front art, `back` the back art.

## Built-in Trap Floor art (A1)

Location: `Assets/ConsoleCards/Content/Art/TrapFloor/` with `Fronts`, `Backs`, `Avatars` and
`Controller` folders. Textures import as Default with mipmaps, max size 2048.

Stand-in art, to be replaced later under the same keys:

- Rush uses the Run picture; Check uses the Jump picture.
- The Clairvoyant picture says "+1 Run ability"; the data gives Clairvoyant Dodge.
- Spring, Crane, Overcharged and Push have no art yet.

## Stages

| Stage | Scope | Status |
|---|---|---|
| A1 | Built-in Trap Floor art on cards and avatars; floor backs per round; this document | Implemented |
| A2 | Art lookup service and texture cache keyed by art key | Planned |
| A3 | JSON pack format: import and export of cards, rules and art | Planned |
| A4 | Player upload UI and art overlays, stored per game | Planned |

## Privacy

Player-uploaded pictures stay on the player's device and in the player's own packs. They are not
shared unless the player shares the pack.
