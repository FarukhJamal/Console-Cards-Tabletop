# 24 — Content Packs

Version 0.4 · Status: Approved direction, A1–A4 implemented

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
- Board (floor) card backs: `GameDefinition.boardCardBacks`, one per round in order, repeating, with their keys in
  `GameDefinition.boardCardBackReferences` (same order).

The key is lower case, uses `/`, and starts with the game's folder name. It never changes when the
picture changes, which is what makes art replaceable: a new picture under the same key wins.

## Lookup order

When a card needs a picture, the first match wins:

1. Player upload for that key.
2. The active game pack's picture for that key.
3. The built-in texture on the ScriptableObject.
4. No picture: the placeholder colour and label are shown.

All four steps are implemented: step 1 by A4 (your pictures), step 2 by A3 (packs), steps 3–4 by A1/A2.

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
| A2 | Art lookup service and texture cache keyed by art key | Implemented |
| A3 | JSON pack format: import and export (version 1 carries art) | Implemented |
| A4 | Player upload UI and art overlays, stored per game | Implemented |

## Privacy

Player-uploaded pictures stay on the player's device and in the player's own packs. They are not
shared unless the player shares the pack.

## A2 record (2026-10-14)

- **`ArtLibrary`** (`Presentation/Art/ArtLibrary.cs`): three layers in lookup order, PlayerUpload, GamePack,
  BuiltIn, then the asset's own texture. Keys are normalised (lower case, `/`, no leading slash or spaces).
  `LoadTexture(bytes, key)` makes a texture from PNG/JPG bytes for A3/A4; textures handed over with ownership are
  destroyed when replaced, removed or cleared (only once no layer uses them). A change raises `Changed` first
  (cards re-apply art in place, no table rebuild), then `TextureReleased` (card materials built from that texture
  are destroyed).
- **Every art read** on the table goes through the library: card fronts and backs, avatars, the round's floor
  back, and the landing, reveal, search and purchase pictures. Built-in pictures are registered by key when the
  game definition is first used.
- **Editor:** Console Cards > Content > Fill Missing Art Keys fills empty keys from the texture's place under an
  `Art` folder (`Art/TrapFloor/Fronts/shield.png` gives `trap-floor/fronts/shield.png`), never changes a set key,
  and reports duplicate keys and keys that differ from their file.
- **Test hook:** on the composition, Art Test Key and Art Test Texture, with the component menu items
  Apply Art Test and Clear Art Test (Play mode), put a picture in the upload layer to check live replacement.

## Architecture (A2–A4, one system)

```
Card files (ScriptableObjects) ─┐
Game pack folder  (pack.json) ──┼─► ContentArtService ─► ArtLibrary (3 layers) ─► table, popups, Card Art panel
Your pictures     (uploads/) ───┘        │
                                         └─ ContentPackStore (files on this computer)
```

- **`Presentation/Art/ArtLibrary`** — the lookup and texture cache only (A2). Layers: PlayerUpload, GamePack,
  BuiltIn. Batches group many changes (a whole pack) into one refresh.
- **`Presentation/Content/ContentArtService`** — the single entry point. For the active game it registers the
  built-in pictures, lists every replaceable picture (cards, avatars, floor backs; anything with an art key),
  activates a pack, and adds or removes your pictures. The table asks it for pictures by key and nothing else.
- **`Presentation/Content/ContentPackStore`** — files only. Per game, under
  `<persistent data>/Content/<game id>/`:
  - `packs/<pack>/pack.json` and the pack's pictures (A3),
  - `uploads/uploads.json` and your pictures (A4),
  - `settings.json` — the active pack.
  Paths read from a manifest must stay inside their own folder; pictures over 16 MB are refused.
- **`Presentation/Content/PictureFilePicker`** — Unity's file panel in the Editor, the Windows file dialog in a
  Windows build; elsewhere the panel points to the packs folder.
- **UI:** `Final/UI/CardArtPanel.prefab` and `CardArtRow.prefab` (Modal layer; made by Create Missing Gameplay UI
  Prefabs), opened from Table > Card art… (added by Add Missing Table Menu Rows).

## A3 record — game packs (2026-10-14)

- **Format, version 1:** a folder with `pack.json` (`formatVersion`, `packId`, `gameStableId`, `displayName`,
  `author`, `description`, `art: [{ key, file }]`) and the picture files. A newer format version is refused.
  Version 1 carries art; card text and rules in packs are left for a later format version.
- **Export:** Console Cards > Content > Export Built-in Art Pack… writes the selected (or first) game's built-in
  pictures as a pack (`<game>-built-in/`, files under `art/<key>`). It is a valid starting point: replace files,
  change `packId` and `displayName`.
- **Import:** Console Cards > Content > Import Pack Folder… copies a pack into this computer's store; players can
  also drop a pack folder into the packs folder (Card art panel > Open packs folder) and press Reload.
- **Use:** the Card art panel's < and > switch between Built-in and each pack; the choice is remembered per game.
  Pictures the pack lacks keep their built-in art.

## A4 record — your pictures (2026-10-14)

- The Card art panel lists every replaceable picture with a thumbnail and where it comes from now (Built-in,
  Pack: name, Your picture). **Choose picture…** copies a PNG or JPG into this game's uploads folder and uses it at
  once on every card with that key; **Reset** removes it and the pack or built-in picture returns.
- Your pictures win over packs and built-in art, are kept per game, load when the game starts, and stay on this
  computer (see Privacy).
