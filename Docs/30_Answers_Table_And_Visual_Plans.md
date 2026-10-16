# 30 — Answers, Table Selection Plan and Visual Polish Plan

Version 0.1 · 2026-10-16 · Status: proposal for the owner to approve, change or reorder. Nothing here is built yet
except what is marked as existing.

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

1. **Controller-button pings.** Alt-click the table or a piece. A pixel ring pulses there in your colour, with a
   button glyph and a short 8-bit beep:
   - A = "look here"
   - B = "no / careful"
   - X = "mine / I'll take it"
   - Y = "your turn / go"

   They work at one screen now and are sent to everyone later. The turn ping already follows this style.
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
   - Every UI action goes through it. This is stage MP-0, and it fits the R1 split.
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

**Stages:**

1. MP-0: the command door, local only.
2. MP-1: events to the UI.
3. MP-2: serialisable state, and join by snapshot.
4. MP-3: two players on a local network.
5. MP-4: content sharing.
6. MP-5: Relay and Lobby, rejoining, and the host leaving.

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

---

## Part C — Visual polish plan: away from the prototype look

**Current setup:**

- Unity 6 with **URP 17.5**, so post-processing volumes, colour grading and SSAO are available.
- uGUI with the Final UI kit (cream, ink, orange, mustard, teal; Silkscreen and Chakra Petch fonts).
- Prototype pieces (`PrototypeCard`, `PrototypePawn` and so on).

| Stage | What |
|---|---|
| V0 Art direction | A one-page style sheet for the owner to sign off: "retro console on a game night table". Palette (the UI kit colours plus player colours), materials (felt, card stock, soft plastic, wood), lighting mood, icon style, sound style. A few reference pictures. |
| V1 Lighting and post | One soft key light with soft shadows, a fill light and a reflection probe. A URP Global Volume with: tonemapping (Neutral or ACES); a colour-grading LUT per theme; low bloom (for neon and screens); light vignette; SSAO for contact shadows under cards and pieces; depth of field only when inspecting a card; anti-aliasing (MSAA, or TAA if needed). Light presets: Day, Evening, Arcade. |
| V2 Pieces | Real models and materials for the card (thin rounded box with bevelled edge, card-stock material front and back), pawns and meeples, tokens, dice (bevelled, engraved pips), the controller box, the Console mat (cloth), the board mat. Consistent real-world scale (doc 00 §3.1). The Prototype prefabs are replaced through the catalogs, not edited. |
| V3 Table and room | The generated table (Part B) with good felt and edge materials; a soft, blurred room around it; a floor. |
| V4 UI and UX | Same design language everywhere (doc 21): a spacing scale, an icon set with icon + word buttons, clear hover/press/disabled states, 120–200 ms fades and slides, click and snap sounds, a UI size setting, readable minimum text sizes, controller glyphs (A/B/X/Y) as visual language; a title screen with a console boot, a settings screen and a loading screen; the developer controls removed from player builds. |
| V5 Motion and feel | Held pieces lift with a soft shadow; cards tilt slightly when moving; flip, shuffle and fan animations; dice rattle and roll sounds; quiet ambient room sound; small "snap" feedback when a card lands in a slot or grid space. |
| V6 Colour grading and themes | A LUT and light preset per table theme; colour-blind-safe player colours; a check pass in grey-scale for contrast. |
| V7 Performance | Quality levels (Low/Medium/High); GPU instancing for cards and pieces; fewer UI rebuilds; a 60 fps target on mid-range PCs, profiled. |

**Suggested order overall:**

1. Finish and approve L2-6b.
2. R0 names, if approved.
3. T1–T3: shape and size.
4. V0–V1: look and light.
5. T4–T5: textures and paint.
6. V2–V5.
7. Cleanup R1–R2 alongside MP-0, because they are the same split.

The owner decides the order.
