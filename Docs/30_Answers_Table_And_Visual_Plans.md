# 30 — Answers, Table Selection Plan and Visual Polish Plan

Version 0.5 · 2026-10-17 · Status: approved by the owner with changes (2026-10-16; teal instead of green, C1): the table ping is free
(A2), multiplayer is a to-do done only when the owner asks while the code is kept ready for it (A6), Part B has a
performance budget (B5), Part C covers the screen layout and the circuit-board theme (C1–C3). The order of
implementation is approved (end of the doc). Nothing here is built yet except what is marked as existing.

The owner asked seven questions (2026-10-16) and for two plans: Table Selection (shape, size, playing area,
edges, with easy tools) and visual polish (visuals, UI, UX, colour grading). The answers come from reading the
code and docs on 2026-10-16.

---

## Part A — Answers to the owner's questions

### A1. Game cursor and gestures

**Custom cursor: not implemented.** Nothing in the code sets a cursor, so the normal system arrow is used
everywhere.

**Gestures that exist:**

| Input | Does |
|---|---|
| Left-click | select; click a Toolbox tile, then click the table to place |
| Left-drag | move a piece; drag a card out of a deck, hand or slot |
| Right-click | the piece's menu; ends a placement |
| Right-drag | turn the camera around the table |
| Middle-drag, WASD, arrow keys | move the camera |
| Mouse wheel | zoom; with a piece selected or while placing, rotate it by 15° |
| F | flip the selected card |
| Esc | cancel or roll back |
| Hand | hover lifts and spreads the cards; drag to reorder |
| Releasing a piece while it moves | it keeps its speed (see A2) |

**Not there yet:**

- Selecting several pieces (box-drag or Shift-click) and moving them as a group. Doc 06 §11 requires this.
- Double-click.
- Hold to peek or zoom on a card.
- Touch or gamepad.
- Keyboard shortcuts beyond F, WASD and Esc.

**Proposal (stage UX-1):**

- **Cursor set.** One cursor asset with these states:
  - point
  - can grab (over a piece)
  - grabbing
  - place (during placement)
  - rotate
  - not allowed here
  - ping
  - measure

  One small service picks the cursor from what the pointer is over and what is happening. The cursor changes only
  when the state changes, so nothing is allocated every frame. The look is drawn in the console theme: pixel-edged,
  cream fill, ink outline, with the player's colour as an accent.
- **Gestures:**

  | Gesture | Does |
  |---|---|
  | Double-click a card | inspect it |
  | Double-click a deck | draw one card to your hand |
  | Shift-click, or box-drag on empty table | select several pieces |
  | Hold Alt | enlarge the card under the cursor (peek) |
  | Q / E | rotate |
  | R | roll the die, or shuffle the deck, under the cursor |
  | 1–9 | draw that many cards |
  | Hold Space | zoom to the cursor |

### A2. Pings, flicking and other fun

| Feature | Status |
|---|---|
| Turn ping (banner, sound and pulse in the player's colour, doc 25) | Exists |
| Pings players give each other (point at something) | Not built: doc 23 says "pings wait for multiplayer" |
| Throwing | Partly: a released card, pawn, token or die keeps the speed the mouse gave it, so it can be tossed across the table (the physics release in `PhysicalLooseObject`). There is no deliberate flick with aim or strength. |
| Dice roll from the menu | Exists, with a fixed arc |

**What we can add in the console/retro theme:**

1. **Table ping (owner, 2026-10-16: a ping anywhere on the table, not linked to any piece or system).**
   - Alt-click (or the middle mouse button tapped) **anywhere** on the table. A ring pulses at that spot for about
     two seconds in your colour, with a short beep, then fades.
   - The ping only marks a spot. It ignores whatever is under it (card, board, empty felt): it does not select,
     move or point at a piece, and no game system reads it.
   - It is not table state: not saved, not in Undo, not in the activity log.
   - Optional look: a button glyph (A, B, X or Y) chosen with keys 1–4 while holding Alt, only as a picture
     ("look here", "careful", "mine", "go"). It means nothing to the game.
   - One screen now. With multiplayer, it is sent as a small message to the other players, not as a game
     command (A6).
   - Built from a small pool of ring objects that are reused, so it allocates nothing per frame.
   - The turn ping (doc 25) stays as it is.
2. **Flick.** Hold on a card, coin or token, pull back and release. A small arrow shows the strength, and the
   piece slides across the table. It is good for "pass this card", shuffleboard-style mini-games and flicking
   coins.
3. **Dice shake and throw.** Hold the die (or a dice cup piece) to shake it with a rattle sound, then release to
   throw. Add a dice tray piece to catch them.
4. **Pixel emotes.** Small 8-bit speech bubbles above your seat: GG, ?!, :), Oops, Nice, Hurry.
5. **Cartridge boot.** Starting a game slots a cartridge into the console mat, the board "screen" boots with a
   short CRT flicker, then "PLAYER 1 START".
6. **Win and lose moments.** Pixel confetti for "YOU WIN", a "GAME OVER" screen flicker, and a short camera shake
   on a Trap. Some already come through the feedback system (doc 25), so this is adding the art and sound.
7. **Riffle shuffle and card fan.** Animated shuffle and fan-out on a deck's menu.
8. **Table toys** that can be turned off: a spinner, a coin to flip, a sand timer, a score dial.

### A3. Useful mechanics for usability and ease

In rough order of value:

1. **Select several and move together** (A1; doc 06 requires it).
2. **Lock a piece** so it cannot be moved by accident (doc 06 §15; not built). Useful for boards and Consoles in
   your own games.
3. **Deal.** From a deck's menu: deal N cards to every seat, or to chosen seats.
4. **Gather.** Collect every card of a deck back into it and shuffle; also flip all cards and sort a hand.
5. **Search a deck.** Look through a deck privately, take one card, then shuffle.
6. **Camera views.** Save views on 1–4 and jump back with one key. `TabletopCameraBookmark` already exists, so
   this mostly needs keys and UI.
7. **Notes and counters.**
   - A sticky-note piece for the table.
   - A counter token: tap to add 1, right-click to take 1 away.
   - A score track and a turn timer.
8. **Private zone.** An area in front of each seat where your cards are hidden from the others.
9. **Hover info.** A short tooltip with the name and owner; hold Alt for the big card.
10. **Arrange.** Put selected cards in a neat row, grid or fan.
11. **Settings.**
    - UI size.
    - Colour-blind safe player colours.
    - Key remapping.
    - Mouse sensitivity.
    - Turning sounds and animations down.
12. **Radial menu.** Hold right-click for a quick wheel: flip, rotate, lock, ping, duplicate, delete.

### A4. Cartridge system status

"Cartridge" is the product idea that a game plugs into the Console platform (doc 00 §3, doc 18 §9). There is no
code named "cartridge". These are the parts it is made of today:

| Part | Status |
|---|---|
| Game box per game in the component library (doc 22): Trap Floor filled; Super Leroy Sisters box present, empty | Exists |
| Game definition as data: cards, modes, default rules, art, players (`GameDefinition`) | Exists |
| Rules as data, with house rules and hints (doc 23) | Exists |
| Cards, decks and pictures in packs and in the player profile; DLC packs read-only (docs 24, 28) | Exists |
| The game selection menu lists every game box; player-made games as data (doc 29) | Exists |
| Game setup code (`TrapFloorTemplateFactory`) and game helpers (rounds, Shop, Floorfall, hints) | Exists, but only for Trap Floor |
| **One "cartridge" contract**, so a new game plugs in without changing the table code | **Missing.** Trap Floor is wired into the table code by name, for example `trapFloorGameDefinition`, the TrapFloor partial files and `RegisterTrapFloorTemplates`. A second game with its own helpers would need edits there. |
| Loading a DLC game that has its own code | **Missing.** Packs carry data only. |
| A physical cartridge piece and the insert moment | **Missing** (A2 idea 5) |

**Estimate:** the data side is about 70% there; the code side (game-specific help) about 30%.

**Proposal (stage CART-1):**

- A small cartridge contract, referenced by each game box. It holds:
  - the game definition;
  - how to set up a table;
  - optional helpers (round flow, hints, HUD, Shop);
  - the controller mapping (optional).
- Trap Floor moves behind it first. The table code then talks to "the current cartridge", never to Trap Floor
  by name.
- **Data cartridges:** player-made games (doc 29) and simple DLC games need no code at all.
- **Code cartridges:** official games with helpers ship as their own code package (one assembly per game; the
  Trap Floor assembly already exists) plus content.

### A5. Cleaning up the code (names, architecture, patterns, SOLID)

**What is good already:**

- Runtime (Core, Application, Game templates, Definitions, Trap Floor) does not depend on Unity, with separate
  assemblies.
- Every change goes through commands and use cases with results and stable IDs.
- Catalogs for pieces.
- Final UI prefabs with views.
- One save system.

**What needs work:**

| Problem | Size |
|---|---|
| `TabletopPrototypeComposition.cs` does almost everything: camera and input wiring, Toolbox, table start/reset/undo, Trap Floor's flow, the Shop, hints, editors, saving, menus | 16,900 lines (741 KB) + 7 partial files ≈ 19,000 lines |
| "Prototype" in names that are now the real thing, for example `PrototypeRuntimeUiController`, `PrototypeCardVisualReferences`, `PrototypeCard.prefab`, `PrototypeHand.prefab`, `Content/Prefabs/Prototype/` | about 40 files and prefabs |
| Developer controls drawn with the old on-screen GUI inside the composition | small |
| UI refreshes are called by hand after changes, instead of the UI reacting to accepted changes | everywhere |

**Plan.** Every step keeps the game working, is one stage, and changes no behaviour.

1. **R0 — Names.**
   - Rename the Prototype classes and prefabs to final names, for example:
     - `TabletopPrototypeComposition` → `TableSession`;
     - `PrototypeRuntimeUiController` → `TableUiController`;
     - `PrototypeCard` → `Card`.
   - The rename is done inside Unity, so every reference (the files' IDs) stays intact. This needs the owner's
     go-ahead, because the Prototype prefabs are currently not to be edited.
2. **R1 — Split the composition by job**, into plain C# classes it creates and wires (a composition root):
   - `TableSessionService`: start, replace, reset, undo and redo.
   - `PieceSpawner`: catalog spawns and their views.
   - `PlacementService`: Toolbox placement, duplicates, keep placing.
   - `ViewRegistry`: finds the view of a piece.
   - `ContextMenuPresenter`.
   - One presenter per panel: game menu, card editor, deck editor, save game, profiles, rules card.
   - `TrapFloorCartridge`: all Trap Floor code (becomes the first cartridge, A4).
3. **R2 — Events instead of hand refreshes.**
   - Accepted actions are published as events (`IDomainEvent` exists).
   - Views and presenters listen to them.
   - This is also what multiplayer needs (A6).
4. **R3 — Assemblies.**
   - Keep one assembly per layer.
   - Add one per cartridge and one for the UI presenters, so wrong-way dependencies cannot compile.

**Patterns to use:**

- **Composition root with constructor injection.** Plain C#, no framework needed.
- **Model–View–Presenter for every panel.** The view is the Final UI prefab, the presenter is a plain class, and
  the model is the profile stores.
- **Command.** Exists.
- **Strategy / plug-in for cartridges.**
- **Observer / event stream for accepted changes.**
- **State machine for interaction.** Exists.
- **Repository for saves.** The stores exist.

**SOLID in practice:**

- **Single responsibility:** the R1 split.
- **Open/closed:** new games plug in as cartridges instead of editing the table code.
- **Liskov:** views behind small interfaces; `IContainerView` already works this way.
- **Interface segregation:** small interfaces such as "place a piece" and "read the table".
- **Dependency inversion:** presenters depend on interfaces of the stores and services, not on the composition.

### A6. Getting the architecture ready for multiplayer

**Owner's decision (2026-10-16):** the architecture below is approved. Multiplayer is **not** on the
implementation list. It sits in the to-do list (doc 27) and is built only when the owner says "implement
multiplayer". Until then, the code is kept ready for it, so that work goes quickly. The rules for that are in
"Keep the code multiplayer-ready" at the end of A6.

**Already aligned with doc 10:**

- Every command carries who asked (player ID), a command ID and the expected revision. Accepted actions get a
  revision number.
- Physics has one local authority that commits settled poses and dice results.
- Interaction locks have an owner.
- Hands are private (hidden hand views).
- Random numbers come from one injected source.

**Gaps, and how to close them:**

1. **One door for every change.** Today the table code calls the use cases directly (about 30 places) and reads
   the state directly (about 180 places).
   - Add `ITableAuthority.Submit(command)`. The local version calls the use cases as now; the network version
     sends the command to the host.
   - Every UI action goes through it. It is built, local only, as part of the R1 cleanup.
2. **UI that reacts to changes.** Remote players' changes arrive as accepted events, not as local calls (R2).
3. **Undo per player.**
   - Today Undo restores a whole-table snapshot.
   - Online, Undo becomes "reverse my last action", done by the host.
   - Undoing another player's action needs their agreement, like house rules.
4. **Content sharing.** Your cards, pictures, decks and games live in your profile.
   - When you host, the game's content goes to the others with a content check (hash).
   - Joiners download what they are missing.
   - Player-made games are already plain data files (doc 29).
5. **Physics.** The host simulates and the others smooth between the poses they receive. Throws and flicks send
   their release speed to the host.
6. **Hot-seat versus networked seats.** Turn "the local player" into a per-client identity from the profile (name
   and colour).
7. **Table state as data.** The state must be fully serialisable for joining late and reconnecting (doc 11). The
   table save file (doc 29) is a first step.
8. **Networking product.** Choose it later, behind `IMultiplayerSession` (doc 10 §14). Candidates:
   - Unity Netcode for GameObjects with Relay and Lobby;
   - Fish-Net;
   - Mirror;
   - Photon Fusion.

**Multiplayer stages — to-do, only when the owner asks:**

1. MP-1: serialisable table state, and joining by snapshot.
2. MP-2: two players on a local network (networking product chosen then).
3. MP-3: content sharing (cards, pictures, decks, player games, table spec).
4. MP-4: Undo per player, online.
5. MP-5: Relay and Lobby, rejoining, and the host leaving.

**Keep the code multiplayer-ready (applies now, to every stage):**

- **One door for changes.** The command door (`ITableAuthority.Submit`, local only) is built as part of the R1
  cleanup. New code changes the table only through commands and use cases with the actor's player ID, never by
  editing state or views directly.
- **UI reacts to accepted changes.** New panels and views update from accepted actions and events (R2), not from
  hand-called refreshes.
- **Everything saved is plain data.** Table spec, player games, decks, cards and settings are plain data files
  (already true for doc 28/29). The table shape is sent as its spec, never as a mesh.
- **Randomness from the injected source.** Never `UnityEngine.Random` for anything that affects the table.
- **No "the" local player.** Code asks for the local player's ID; it does not assume seat 0 or one player.
- **Visual-only things stay out of table state.** The ping, hover, cursor and camera are local. A ping is sent
  later as a small message, not a command.
- **Physics release values (speed, spin) are passed as data**, so a host can simulate them later.

### A7. Gamification that keeps play free — better than Tabletop Simulator

**Where Tabletop Simulator is weak:**

- A cold, empty start.
- No guidance.
- Making games needs scripting (Lua).
- No progress across sessions.
- A generic look.

**Ideas that keep freeform play first:**

1. **Console identity.**
   - Each game is a cartridge you insert, with a boot screen.
   - Your profile is your "save card".
   - Every table is a little console setup.
2. **Cosmetic progress only.** Playing, making cards, making games and sharing them earn XP that unlocks looks:
   - card backs;
   - console mat colours;
   - dice skins;
   - table edges and felts;
   - cursor trails;
   - ping sounds.

   **Nothing that changes play is ever locked.**
3. **Trophies** per game and for the platform. Examples: "Made your first game", "Escaped Trap Floor on
   Impossible", "Rolled three 6s".
4. **Assistance level per table, chosen by the players:** Free, Hints, Assisted or Strict (doc 09 policy
   levels).
5. **Creator loop.**
   - The step-by-step game maker (L2-6b).
   - "Remix" any game: copy it, change it, keep a credit line.
   - Later: likes, sharing and featured games.
6. **Session recap.** An end-of-game card with the winner, turns, best moment and a shareable picture.
7. **Daily seed.** One shared Trap Floor floor layout per day, with a best-time board for solo or co-op.
8. **Learn by playing.** The tutorial is a tiny cartridge ("Level 1"), using the hint system.
9. **Social.** Pings and emotes (A2), spectators, a "next game" vote.
10. **Seasonal table themes**, as free cosmetics.

---

## Part B — Table Selection plan (owner, 2026-10-16)

**Owner's request:**

- Choose the table's shape: round, oval, square, star, rectangular.
- Resize it in every direction the shape allows.
- Choose the playing area's texture.
- Choose the edge: spikes, no edge, and so on.
- Easy tools: a resize tool, paint and texturing.
- Change the table's shape without breaking or degrading anything.

### B1. How the table works today (verified)

- The table is a model in the scene. Its top is an authored **surface collider** (`PhysicalTabletopSurface`).
  Game boards are surfaces too.
- **Loose pieces** (cards, pawns, tokens, dice) are placed by a ray onto the registered surface colliders.
  **Decks, Consoles and boards** take their height from a ray at their spot. Both work with a collider of any
  shape.
- A piece released past the edge falls off naturally (no snap-back).
- **Rectangle assumptions:** a game's layout (Trap Floor's seats and Consoles) is placed in the table's own
  coordinates through one table frame, and the camera limits and play areas are rectangles (`TabletopBounds`).

### B2. The key decision: build the table, don't bend a model

To change shape safely, the table is **generated from a small description**, never by stretching or deforming
an imported mesh:

```
TableSpec
  shape:      Rectangle | Square | Round | Oval | Star | (later: Hexagon, Octagon, Stadium)
  size:       width, depth (or radius / radii; star: outer radius, inner radius, points)
  corner:     corner radius (rectangle, square), tip roundness (star)
  felt:       playing-area material key, tint, tile size, rotation, paint layer (optional)
  edge:       None | Lip | Rounded rail | Wooden rail | Spikes | Neon; edge material key, height, width
  base:       legs / pedestal / none, material key
```

From the spec, a **table builder** makes the meshes and colliders:

1. **Outline.** The shape becomes a closed 2D line of points spaced by distance, not by count. A bigger table
   gets more points instead of stretched ones, so curves stay smooth at any size. Each shape is one function:
   - a rectangle with rounded corners;
   - a circle or ellipse;
   - a star: points alternate between the outer and inner radius, tips rounded.
2. **Top (playing area).** The outline is filled with triangles.
   - Ear-clipping handles concave shapes like the star; convex shapes use a simple fan.
   - **The texture is mapped in table units**, so it repeats (felt tiles) instead of stretching when the table is
     resized.
3. **Edge.** A small cross-section (lip, rounded rail, wooden rail with a bevel) is swept along the outline, with
   clean mitred corners.
   - Spikes and other ornaments are separate small models placed along the outline at even spacing, drawn as one
     batch (GPU instancing).
   - "None" builds nothing.
4. **Colliders.**
   - The top gets a collider made from the top mesh, as the surface that pieces land on.
   - Lip and rail edges get their own collider, so pieces can bump them.
   - Spikes are decoration (no collider) unless the owner wants them to stop pieces.
5. **Rebuilding.**
   - The table is rebuilt only when the spec changes, never every frame. It reuses the same mesh and lists, so
     no garbage is made.
   - While a size handle is dragged, a light version is built. The full version is built on release.
6. **Safety.**
   - Every shape has minimum and maximum sizes.
   - The outline is checked before building: no crossing lines, no zero area.
   - If anything fails, the last good table stays and a message says why.
   - The current scene table stays as the "Classic" table until the new one is approved.
7. **Saved as data.**
   - The spec is saved with the table, with your games (doc 29) and as table presets in the profile.
   - In multiplayer only the spec is sent, and every player builds the same table.

**Making the rest of the game shape-aware:**

- **Contains / closest point.** A table shape answers "is this point on the table?" and "where is the nearest
  point on it?". This replaces the rectangle checks used for camera limits and play areas.
- **Seats around any shape.** Seats are spread evenly along the outline (by distance), each facing the centre.
  This works for round and star tables too. The current seat presets become "seat at these positions along the
  edge".
- **Game layouts.**
  - A game (Trap Floor) says the play rectangle it needs.
  - The builder finds the largest rectangle that fits inside the chosen shape and centres the game's layout in it.
  - When a table is too small for the game, the size tool shows the minimum and will not shrink further while
    that game is on it.
- **Pieces when shrinking.** Pieces that would end up off the table are moved back inside along the shortest way
  (one Undo step), or the shrink stops at them. The owner chooses which.

### B3. The Table tools (UX)

The aim is "obvious in five seconds", no menus inside menus.

- **Table mode.**
  - Opened from New table… (a "Table" step before Start) and from the Table menu > Table….
  - The camera goes to a top view. A tool rail appears on the left: **Shape · Size · Playing area · Edge ·
    Base**.
  - Done and Undo are at the bottom right. Esc leaves Table mode.
- **Shape.** Big picture cards (Round, Oval, Square, Rectangle, Star). Clicking one morphs the table smoothly
  into the new shape.
- **Size.** Handles on the table itself:

  | Shape | Handles |
  |---|---|
  | Rectangle | four side handles |
  | Square | one corner handle |
  | Round | one radius handle |
  | Oval | width and depth handles |
  | Star | outer radius and inner radius handles, and a − / + for the number of points |

  - Sizes snap to 5 cm (hold Alt for free sizes).
  - The size shows in a small label while dragging, and number boxes are there for exact sizes.
- **Playing area.**
  - A texture shelf: felts in several colours, wood, leather, cloth, a pixel grid and a "console board" pattern.
  - **Upload your own picture.** This uses the same picture picker and profile uploads as the card editor.
  - Tint colour, tile size and rotation.
- **Paint** (inside Playing area).
  - A brush with colour, size and opacity, an eraser and fill.
  - It paints on a separate paint layer over the texture, saved as a picture in the profile.
  - Stamps (logos, symbols) come later.
- **Edge.** Cards: None, Lip, Rounded rail, Wooden rail, Spikes, Neon, with a colour or material and a height.
- **Base.** Legs, pedestal or none (mostly seen at low camera angles).
- **Presets.** "Save as table preset" stores the spec in the profile under `tables/`. Presets appear as cards in
  the Shape tool.

### B4. Table stages

| Stage | What |
|---|---|
| T1 | `TableSpec`, outline functions (rectangle, square, round, oval), table builder for the top and a simple lip, surface collider, shape-aware contains/closest point, seats along the outline, camera limits from the shape. Classic table kept behind a switch. |
| T2 | Star shape (concave filling); edge profiles (none, lip, rounded rail, wooden rail); spikes and neon as instanced ornaments; edge colliders. |
| T3 | Table mode UI: shape cards, size handles, number boxes, snapping, the morph, Undo; pieces kept on the table when shrinking; game minimum size. |
| T4 | Playing area: texture shelf, upload, tint, tile size, rotation; saved in the profile and with your games; table presets. |
| T5 | Paint layer and brush tools (paint, erase, fill); stamps later. |
| T6 | Base and room around the table (ties into V3). |

Each stage is built, checked and approved before the next, as in List 2.

### B5. Performance budget: no heavy work, no texturing limits (owner, 2026-10-16)

The owner asked that nothing in the table system becomes heavy to compute or limits texturing. These are fixed
rules for T1–T6.

**Meshes:**

| Part | Budget | How |
|---|---|---|
| Outline | at most 512 points, whatever the size | Points spaced by distance with a cap. A huge table gets slightly wider spacing instead of more points. |
| Top | at most about 2,000 triangles | One fill of the outline. |
| Edge | at most about 12,000 vertices | One profile swept along the outline; the profile has at most 12 points. |
| Spikes and ornaments | one draw call | GPU instancing: one small mesh drawn many times. A cap of 256 per table. |
| Base and legs | prefabs with level-of-detail | Only seen at low camera angles. |

**Building:**

- Rebuilt only when the spec changes, never every frame.
- While a size handle is dragged, the light version is built at most once per frame, and only the parts that
  change (the top and edge, not the base).
- The full version, and the collider, are built once on release.
- The mesh objects and lists are made once and reused, so a rebuild makes no garbage.
- The collider uses a simplified outline (at most 128 points), baked once on release. Unity can bake it off the
  main thread so the game does not stutter.
- The edge collider uses simple boxes along the outline where possible, not a detailed mesh.

**Textures (no limits for the player):**

- **Any picture is accepted:** PNG or JPG, any size, any shape. Large pictures are scaled down on load to the
  quality setting (2048 px by default, 4096 on High), compressed in memory (GPU format) and given mipmaps, so
  they stay sharp at a distance and light on memory. The original file is kept in the profile untouched.
- **Repeat or fit.** A picture can repeat (felt, wood) or be stretched once across the whole playing area (a
  painted mat, a game map). Both work on every shape, because the texture coordinates come from table units and
  the table's bounding box.
- **Tint, tile size, rotation and offset** are material settings, not new pictures. Tints are set per table
  through a property block, so no extra material copies are made.
- **Built-in textures** are shipped compressed with mipmaps.
- **Changing texture** releases the old one right away, so memory does not build up.

**Paint layer:**

- One fixed-size paint texture (2048 × 2048 by default; 1024 on Low), painted on the graphics card with a brush
  shader. A stroke costs a few small draws, not a loop over pixels.
- It is saved as a PNG only when you press Done, never per stroke.
- Undo inside Table mode keeps at most 8 steps, as small copies on the graphics card.
- Nothing is painted? Then no paint texture exists at all.

**Draw calls for the whole table:** the top, the edge, the ornaments and the base are about 4–6 draw calls in
total, whatever the shape or size.


### B6. T1 record: table shape (written 2026-10-16, to be checked)

**What the player sees.** The Table menu has a new row, **Table: Classic**. Each press changes the table to the
next shape — Classic → Rectangle → Square → Round → Oval → Classic — and the row shows the new name. The menu
stays open, so you can step through the shapes. The choice is saved in your profile (`table.json`) and comes
back at the next start.

**How it fits the game without moving anything.**

- Classic is the scene's own table (TCGTable2), unchanged.
- The other shapes are built where the classic playing area is, at the same height, with the same frame.
  Game Templates, the Empty Table and saved positions all land in the same places.
- Each shape covers the whole classic playing area (W × D), so every game layout still fits:

  | Shape | Default size |
  |---|---|
  | Rectangle | W × D |
  | Square | the longer side |
  | Round | the diagonal as the diameter |
  | Oval | the ellipse through the area's corners (W·√2 × D·√2) |

- Only one table is the placement surface at a time; the other is turned off first.
- The playing area uses the classic felt, mapped so the felt does not stretch: a bigger table repeats it.
- The sides, underside and lip use the classic table's material.
- If a shape cannot be built, the last good table stays and a message says why.

**Code (new, `Presentation/TableSurface/`):**

| File | What |
|---|---|
| `TableSpec.cs` | The table as data: shape, width, depth, corner rounding, edge (None or Lip), edge height and width, thickness; safe ranges; the default sizes above. This is what is saved and, later, sent in multiplayer. |
| `TableOutline.cs` | The outline: points spaced by distance, at most 512 (128 for the collider). It answers "is this point on the table?" and "where is the nearest point on it?". |
| `TableMeshBuilder.cs` | The top as a slab (felt on top; sides and underside below) and the lip swept around the outline. The lists are made once and reused. |
| `GeneratedTable.cs` | The built table: Top (mesh, collider, `PhysicalTabletopSurface` as the Template Layout Origin) and Edge. It is rebuilt only when the spec changes. |

**Changed:**

- `PhysicalTabletopSurface`: `ConfigureTemplateOrigin`, plus read access to the frame and units.
- `TabletopComposition.Table.cs` (new partial):
  - finds the classic table once;
  - applies the saved shape at start, before the first table;
  - provides the menu row.
- `TabletopComposition.cs` calls it in `Start()` and in the table-menu refresh.
- `SessionBarView`: `BindTableShape`.
- `TableUiController`: `ShowTableShapeEntry`.
- `SessionUiBuilder`: adds the row through Add Missing Table Menu Rows.

**Budget (B5):**

- Top: at most about 2,000 triangles (playing area, underside and sides). Lip: at most about 4,100 vertices.
- 3 draw calls in all: felt, sides and lip.
- Nothing is built per frame.

**Not in T1 (moved, with the reason):**

- **Seats along the outline and camera limits from the shape: moved to T3.** Every shape still covers the classic
  area, so today's seats and camera limits stay correct. They only need to change once tables can be resized,
  which is T3.
- **Legs and base: T6.** The classic table's legs are part of its single model, so a built shape shows no legs
  for now.
- **The lip has no collider yet.** Edge colliders are part of T2.
- **The shape is not yet saved with your games** (T4 saves the table with games), and changing shape is not an
  Undo step (Undo comes with Table mode in T3).


**First check (owner, 2026-10-17) and fixes:**

| Seen | Why | Fix |
|---|---|---|
| "The surface MeshCollider has no mesh", then no placement at all | Code: the collider got its mesh while the new table was still switched off. | The table now switches on first, then the collider takes its mesh, and only then does the surface turn on. If the collider still has no mesh, the Classic table comes back with a message. |
| Scrambled texture on the sides | The classic model's picture is laid out for that one model. It cannot wrap any other shape. | For now the sides, underside and lip are plain theme black (#1B1B1D). Proper materials follow the kit rules in B7. |
| The table base (legs) disappears | Legs, rim and top are one mesh in TCGTable2, so hiding its top hides its legs too. | Needs the kit in B7: legs and base as their own pieces. |

### B7. How the table model should be made (a kit, not one model)

The rule: **the code builds everything whose shape changes, and the model supplies the pieces that do not.**
One finished table model breaks as soon as the table changes shape, because its top, rim, legs and texture are
welded to one size. A kit never breaks.

**1. Built by the code; not modelled:**

- the playing area (top);
- the sides and underside;
- the edge, from a cross-section.

**2. Modelled, as separate pieces (one FBX or prefab each):**

| Piece | What | Pivot | Budget |
|---|---|---|---|
| Leg | One leg. The code places 3–8 along the outline, set in from the edge. | Top centre, where it meets the underside | ≤ 1,500 triangles |
| Pedestal | A centre column with a foot, for round and oval tables. | Top centre | ≤ 3,000 triangles |
| Apron or skirt (optional) | Not a mesh: a cross-section like the edge, swept by the code. | — | ≤ 12 points |
| Ornament | A spike, stud, bolt or neon cap, placed along the edge and drawn instanced. | Base centre | ≤ 200 triangles |
| Edge profile | Rounded rail, wooden rail and so on, given as cross-sections. The artist can draw one as a flat 2D outline; the code sweeps it. | — | ≤ 12 points |

**3. Export rules for every piece:**

- **Axes and scale:**
  - Unity axes: Y up, Z forward.
  - Transforms applied (rotation 0, scale 1), at the scale of today's table. Its top is about 25 × 17 units, and
    a leg is about 11 units tall from the floor to the top.
- **Colliders and lights:** none inside the model. The code adds colliders (the top; simple boxes for edges and
  legs).
- **Parts:** separate objects, not one merged mesh. Nothing hidden inside another part.

**4. Materials and textures:**

- **Tileable (seamless) textures, never an atlas laid out for one model.** Felt, wood, metal and the circuit-board
  pattern all repeat.
- **Repeat size.** Each material says how many units one repeat covers (for example, wood every 2 units). The
  code maps by distance:
  - along the edge: length along the outline;
  - up the side: height.
  
  So a texture never stretches, whatever the shape or size.
- **Four separate slots:**
  1. Playing area.
  2. Body (sides and underside).
  3. Edge.
  4. Base (legs or pedestal).
  
  Each can be changed on its own, which is what the T4 texture shelf needs.
- **Shader and maps:** URP Lit, with base map, normal map and optional mask map. 1024 or 2048 px, with mipmaps.

**5. The classic look on every shape.** The look of TCGTable2 can be kept by splitting it in Blender:

- one leg as the Leg piece;
- the rim's cross-section as an edge profile;
- its wood as a tileable Body/Edge texture;
- its felt as the playing-area texture.

The Classic shape then becomes "Rectangle with the classic kit", and the single model is no longer needed.

---

## Part C — Visual polish plan: away from the prototype look

**Owner's direction (2026-10-16):**

- Visual polish also means **how the menus are organised**: what goes in which menu, how they are spread over
  the screen, how much of the screen they cover, where they sit and in what order things are listed. It is not
  only colour.
- **The pixel art is not readable enough.**
- **The client's theme: the inside of a console's hardware.** A circuit board with copper traces, chips,
  solder points and white printed labels.

### C1. The theme: inside the console

**Board colour: teal, not green (owner, 2026-10-16).** The board is a deep teal so it sets off the orange, black
and red of the components and game systems, instead of competing with them.

| Role | Colour (starting values, tuned in V0) |
|---|---|
| Board (panel background) | deep teal `#0F3A3D`, a lighter teal `#16504F` for raised parts |
| Board accent, selected and "powered" | teal `#2FA58A` (already the UI kit's Teal) |
| Traces and edges | copper `#B87333` / gold `#C9A24A`, thin |
| Printed labels and body text | silkscreen white `#F2F0E6` |
| Chips and buttons | black `#1B1B1D` with a fine metal rim |
| Components and game systems (unchanged) | orange `#E0531F`, black, red; mustard `#F2A81D` for warnings |


| Element | Look |
|---|---|
| Panels | Deep teal circuit board with a faint trace pattern; thin copper or gold edges; small solder dots at the corners |
| Section titles | White "printed on the board" labels in a clean, condensed sans-serif, like real board markings (for example `U3 · DECK EDITOR`) |
| Buttons | Chip-like blocks: black with a fine metal rim. Primary actions have a small LED that lights teal; dangerous actions an amber or red LED |
| Lists and rows | Rows separated by thin copper lines; the selected row lights with a soft teal glow, like a powered trace |
| Links between parts | Thin traces may run from a panel to the piece it is about, sparingly |
| Player colours | LED colours (blue, red, amber, violet, white and so on, kept apart from the board's teal and the components' orange), checked to be colour-blind safe |
| Sounds | Soft relay clicks and short electronic blips instead of 8-bit beeps (8-bit stays for the table ping and fun moments) |

**Readability first (the pixel font goes):**

- **Body text, numbers and labels:** a clean sans-serif that is easy to read at small sizes. Chakra Petch (already
  used) or a similar technical sans.
- **Titles:** a condensed technical display font in the "board marking" style instead of the pixel font
  Silkscreen. The pixel font is kept only for tiny decorative markings, if at all.
- **Minimum sizes at 1920 × 1080:** body 18 px, small labels 15 px, titles 24 px or more.
- **Contrast:** text against the teal board at least 4.5 : 1 (WCAG AA). White or very light text on the dark
  board, never teal on teal.
- **UI size setting:** 80–130 %.

**The table stays a table.** The circuit board is the frame and the menus; the playing area keeps calm felt or
textures (Part B), so the pieces stay easy to see. A "circuit board" felt is one of the table textures to choose.

### C2. Screen layout: menus, coverage, placement and order

**Screen zones during play** (the table keeps at least 65 % of the screen when no panel is open):

```
┌──────────────────────────────────────────────────────────────┐
│ [Game ▾] [↶][↷]        TURN STRIP (centre)      [Shop] [≡ Menu]│  top bar, thin
├────────┬───────────────────────────────────────────┬─────────┤
│ RULES  │                                           │ STATUS  │
│ card   │               THE TABLE                   │ card +  │  side rails: narrow,
│ (fold) │                                           │ log     │  fold to a tab
│        │                                           │ (fold)  │
├────────┴───────────────────────────────────────────┴─────────┤
│ [+ Toolbox]          HAND TRAY (centre)            [? Help]   │  bottom bar
└──────────────────────────────────────────────────────────────┘
```

- **Top bar:** the game button with undo/redo on the left, the turn strip in the centre, the Shop and the menu on
  the right.
- **Left rail:** the rules card, which folds to a tab.
- **Right rail:** the game status and the activity log, which fold to a tab.
- **Bottom:** the Toolbox on the left, the hand in the centre, help on the right.
- **Messages and banners:** top centre, under the turn strip, never over the hand.

**How much screen panels take:**

| Kind | Size | Examples |
|---|---|---|
| Rail panels | at most 22 % of the width each, fold away | rules card, status, log |
| Pop-ups at the cursor | small, next to what was clicked | right-click menu, quantity |
| Work screens | at most 70 % width × 80 % height, centred, the table dimmed behind | card editor, deck editor, save game, table mode tools |
| Full screens | the whole screen, only outside play | title screen, game selection, settings |

**Where menus go, and in what order:**

- **Title screen (new)**, shown after a console boot:
  1. **Play** (game selection: Empty Table, games, your games)
  2. **Make** (card editor, deck editor, your games, table presets)
  3. **Profile**
  4. **Settings**
  5. **Quit**
- **In-play menu (≡)**, grouped and kept short:
  1. **This table:** Rules card, Hints, Reset, Save table as a game…
  2. **Make:** Table mode…, Card editor…, Deck editor…
  3. **View:** feedback, hands on table, camera views
  4. **Profile and settings**
  5. **Leave table** (back to the title screen)
- **Game selection:** the games on the left (Empty Table, our games, your games, sorted by last played), the
  chosen game on the right (picture, players, mode, rules, deck), Start at the bottom right.
- **Lists everywhere:** sorted by name with a small filter box. Last used at the top where that helps (games,
  decks).
- **Buttons:** the main action bottom right; Cancel, Back and Close bottom left or top right, always in the same
  places.

### C3. Visual polish stages (approved list, updated)

**Current setup:**

- Unity 6 with **URP 17.5**, so post-processing volumes, colour grading and SSAO are available.
- uGUI with the Final UI kit (cream, ink, orange, mustard, teal; Silkscreen and Chakra Petch fonts).
- Prototype pieces (`PrototypeCard`, `PrototypePawn` and so on).

| Stage | What |
|---|---|
| V0 Art direction | A style sheet for the owner to sign off: "inside the console" (C1). Teal circuit-board palette, the two fonts, button and panel looks, icon style, sound style, and screen mock-ups of the play layout, a work screen and the title screen (C2). |
| V1 Lighting and post | One soft key light with soft shadows, a fill light and a reflection probe. A URP Global Volume with: tonemapping (Neutral or ACES); a colour-grading LUT per theme; low bloom (for neon and screens); light vignette; SSAO for contact shadows under cards and pieces; depth of field only when inspecting a card; anti-aliasing (MSAA, or TAA if needed). Light presets: Day, Evening, Arcade. |
| V2 Pieces | Real models and materials for the card (thin rounded box with bevelled edge, card-stock material front and back), pawns and meeples, tokens, dice (bevelled, engraved pips), the controller box, the Console mat (cloth), the board mat. Consistent real-world scale (doc 00 §3.1). The Prototype prefabs are replaced through the catalogs, not edited. |
| V3 Table and room | The generated table (Part B) with good felt and edge materials; a soft, blurred room around it; a floor. |
| V4a Menus and layout | The screen zones, panel sizes, menu grouping and ordering of C2: the in-play menu regrouped, rails that fold, work screens at most 70 % × 80 %, the title screen with Play / Make / Profile / Settings / Quit, the settings screen, list sorting and filters, the developer controls removed from player builds. |
| V4b Readability | The fonts, minimum sizes, contrast and the UI size setting of C1. |
| V4c Circuit-board skin | Panels, buttons, rows, LEDs, traces and sounds of C1 for every Final UI prefab (one design language, doc 21), clear hover/press/disabled states, 120–200 ms fades and slides, the console boot. The prefabs stay yours to edit; the builders add the new look only to missing parts. |
| V5 Motion and feel | Held pieces lift with a soft shadow; cards tilt slightly when moving; flip, shuffle and fan animations; dice rattle and roll sounds; quiet ambient room sound; small "snap" feedback when a card lands in a slot or grid space. |
| V6 Colour grading and themes | A LUT and light preset per table theme; colour-blind-safe player colours; a check pass in grey-scale for contrast. |
| V7 Performance | Quality levels (Low/Medium/High); GPU instancing for cards and pieces; fewer UI rebuilds; a 60 fps target on mid-range PCs, profiled. |

**Order of implementation (approved by the owner, 2026-10-16):**

1. L2-6b — done and approved.
2. R0: names.
3. T1–T3: table shape and size.
4. V0–V1: art direction (with the layout mock-ups) and light.
5. T4–T5: table textures and paint.
6. V2–V5: pieces, table and room, menus and layout, readability, circuit-board skin, motion.
7. R1–R2: code cleanup, including the local command door and events (the multiplayer-ready alignment, A6).

Not yet placed in the order: UX-1 (cursor, gestures and the table ping), CART-1 (cartridge contract), V6–V7.

**To-do, only when the owner asks:** multiplayer, MP-1 to MP-5 (A6).


---

## Part D — R0 record: final names (2026-10-16)

Status: written, to be checked (not compiled yet). Owner's go-ahead: "Start R0" (2026-10-16), which also allows
touching the former Prototype prefabs.

**How it was done, so no reference breaks:**

1. **Code** (45 files) now uses the final names: classes, a few members, the namespace
   `ConsoleCards.Presentation.Prototype` → `ConsoleCards.Presentation.Composition`. The files themselves were
   left where they were.
2. **The scene**: its objects get their new names, and the composition's hide setting is renamed. A
   `FormerlySerializedAs` attribute keeps the old setting too. The world-text shader is shown as
   "ConsoleCards/World Text".
3. **A one-time Unity tool** (`Editor/Maintenance/R0RenameTool.cs`) then renames the files, prefabs, materials,
   the scene and three folders *inside Unity*.
   - Every file keeps its asset ID, so the scene, the catalogs and the prefabs still find everything.
   - It is offered in a dialog as soon as the code compiles, and is also in Console Cards > Maintenance > Finish
     R0 Renames. It skips what is already done.
   - It updates the build scene list.
   - It lists in the Console the old prefabs that nothing in the project uses (candidates to delete).
   - Delete the tool once R0 is approved.
4. Between the compile and the renames, Unity shows the renamed scripts as "cannot be loaded". Nothing is lost,
   but do not press Play or save the scene until the renames are done.

**Types:**

| Old | New |
|---|---|
| `TabletopPrototypeCompositionEditor` | `TabletopCompositionEditor` |
| `TabletopPrototypeComposition` | `TabletopComposition` |
| `PrototypeVisualHide` | `TableVisualHide` |
| `PrototypeContextMenuMode` | `ContextMenuMode` |
| `PrototypeTemplateContext` | `TemplateContext` |
| `PrototypeFixedContainerVisual` | `FixedContainerVisual` |
| `PrototypeConsoleSlotVisual` | `ConsoleSlotVisual` |
| `PrototypeCardVisualReferences` | `CardVisualReferences` |
| `PrototypeSessionUndoSnapshot` | `SessionUndoSnapshot` |
| `PrototypeWorldTextDepth` | `WorldTextDepth` |
| `PrototypeUiPrefabIds` | `UiPrefabIds` |
| `PrototypePopupActionOption` | `PopupActionOption` |
| `PrototypePopupActionRowView` | `PopupActionRowView` |
| `PrototypeCardInspectView` | `CardInspectView` |
| `PrototypeCardInspectModel` | `CardInspectModel` |
| `PrototypeCardInspectSideModel` | `CardInspectSideModel` |
| `PrototypeActionAbilityPurchaseOption` | `ActionAbilityPurchaseOption` |
| `PrototypeActionAbilityPurchasePopupView` | `ActionAbilityPurchasePopupView` |
| `PrototypeFocusedCardOptionModel` | `FocusedCardOptionModel` |
| `PrototypeFocusedCardSelectionModel` | `FocusedCardSelectionModel` |
| `PrototypeFocusedCardRevealModel` | `FocusedCardRevealModel` |
| `PrototypeFocusedCardSelectionView` | `FocusedCardSelectionView` |
| `PrototypeFocusedCardOptionView` | `FocusedCardOptionView` |
| `PrototypeTrapFloorStatusModel` | `TrapFloorStatusModel` |
| `PrototypeFloorfallStatusModel` | `FloorfallStatusModel` |
| `PrototypeTrapFloorHudView` | `TrapFloorHudView` |
| `PrototypeGameTemplateOption` | `GameTemplateOption` |
| `PrototypeGameTemplatesPanelView` | `GameTemplatesPanelView` |
| `PrototypeTabletopPopupView` | `TabletopPopupView` |
| `PrototypeQuantityPopupView` | `QuantityPopupView` |
| `PrototypeInteractionGuide` | `InteractionGuideView` |
| `PrototypeRuntimeUiRoot` | `TableUiRoot` |
| `PrototypeRuntimeUiController` | `TableUiController` |
| `PrototypeStatusMessageView` | `StatusMessageView` |
| `PrototypeSessionTemplateButtonView` | `SessionTemplateButtonView` |
| `PrototypeComponentToolboxView` | `LegacyComponentToolboxView` |
| `PrototypeComponentToolboxBindings` | `LegacyComponentToolboxBindings` |

**Members:**

| Old | New |
|---|---|
| `prototypeTemplateContext` | `templateContext` |
| `HidesPrototypeVisual` | `HidesTableVisual` |
| `ConfigurePrototypeLabel` | `ConfigureWorldLabel` |
| `PrototypePlayerCount` | `StandardPlayerCount` |
| `prototypeUiInputConfiguredByComposition` | `uiInputConfiguredByComposition` |
| `ConfigurePrototypeUiInput` | `ConfigureUiInput` |
| `prototypeBodyMesh` | `bodyMesh` |
| `PlayerPrototypeColor` | `PlayerSeatColor` |
| `CreateTrapFloorPrototypeContext` | `CreateTrapFloorTemplateContext` |
| `CompletePendingFloormasterTriggerPrototype` | `CompletePendingFloormasterTrigger` |
| `prototypeVisualHide` | `tableVisualHide` |
| `RestorePrototypeTemplateContext` | `RestoreTemplateContext` |
| `ResetPrototype` | `ResetTable` |
| `PrototypeControlsPanelRect` | `DeveloperControlsPanelRect` |
| `PrototypeCameraOrthographicSize` | `DefaultCameraOrthographicSize` |
| `CreatePrototypeActivePlayers` | `CreateDefaultActivePlayers` |
| `CompleteFloorfallPhasePrototype` | `CompleteFloorfallPhase` |
| `CompleteEndPrototype` | `CompleteEndPhase` |
| `ClearPrototypeUiInput` | `ClearUiInput` |

**Folders and assets:**

| Old | New |
|---|---|
| `Presentation/Prototype/` | `Presentation/Composition/` (files without "Prototype": `TabletopComposition.cs` and its parts) |
| `Content/Prefabs/Prototype/Prototype<Name>.prefab` | `Content/Prefabs/Base/<Name>.prefab` (`PrototypeRuntimeUiRoot` → `TableUiRoot`; the six die shapes move with the folder) |
| `Content/Prefabs/Prototype/PrototypeUiPrefabCatalog.asset` | `Content/Prefabs/Base/BaseUiPrefabCatalog.asset` |
| `Content/Materials/Prototype/<Name>Prototype.mat` | `Content/Materials/Base/<Name>.mat` |
| `Content/Materials/TableSurfacePrototype.mat` | `Content/Materials/TableSurface.mat` |
| `Content/Resources/PrototypeWorldText.mat` / `.shader` | `WorldText.mat` / `WorldText.shader` (loaded by the name "WorldText") |
| `Presentation/Scenes/TabletopPrototype.unity` | `Presentation/Scenes/Tabletop.unity` |

**Notes:**

- `Content/Prefabs/Base/` holds the pieces in use (Card, Pawn, Token, Die, Hand) and several older prefabs that
  newer ones replaced (for example Deck, Stack, DiscardPile, Console, and old UI prefabs). The tool's Console
  list shows which ones nothing uses.
- `Real/` keeps its own `Deck`, `Stack`, `DiscardPile` and `ConsoleMat` prefabs (the ones in use).
- The rule "don't edit Prototype prefabs" now applies to the same prefabs under their new names, unless the owner
  says otherwise.
- Some error messages and comments still say "prototype" in passing; they change with the R1 split.
- **Fix after the renames (2026-10-16):** six renamed scripts had a small data class above their view class. While the
  file and class names did not match, Unity linked those scripts to the data class, and it kept that link after the
  rename because the files themselves had not changed. The Shop panel then failed to start the table ("requires a
  prefab asset reference"). Moving each view class to the top of its file changes the files, so Unity reads them again
  and links them correctly. Rule from now on: the Unity component class comes first in its file.
- Older docs keep the old names where they describe history. CLAUDE.md is updated.
