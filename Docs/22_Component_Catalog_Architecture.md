# Console Cards - Component Catalog Architecture

**Document ID:** 22_Component_Catalog_Architecture
**Version:** 0.1
**Status:** Stages C1 and C1b written 2026-10-08 (catalog types, component library, Base Box catalog, pile prefabs). C2 and C3 pending; each needs its own approved plan. Binding rule: doc 21 principle 18.
**Source:** Owner decisions 2026-10-08 and the product material (box set, controller box and game box images).
**Purpose:** Define how every component and its definitions are registered, so the table, the Toolbox and the UI resolve the same component from one place.

## 1. Product structure

- **Main (Base) box: the Console system.** The console, console cards, dice sets, game board, tokens and minis. It contains two Controller boxes and one free game.
- **Controller box: one per player.** It holds the controller deck and misc components (meeples, dice, tokens). Its lid doubles as a card organizer with holder ridges.
- **Controller deck (product material):** 6 each of Up, Down, Left, Right, A, B, X and Y, plus 2 Start and 2 Select: 52 cards.
- **Game box: one per game (e.g. Trap Floor).** The game's supplement cards (Location, Item, Avatar) and any extra components. Other games are sold separately.

## 2. Catalogs

- **One catalog asset per box or shelf** (`ComponentCatalog`, Presentation): `BaseBox.asset`, `ControllerBox.asset`, one Game box per game, and `Environment.asset` later. Category: BaseBox, ControllerBox, GameBox, Environment.
- **Component library (C1b):** one `ComponentLibrary` asset gathers the shelf, and the scene references only it:

  ```
  Component Library
  ├── Base Box
  ├── Controller Box
  ├── Game Boxes
  │   ├── Trap Floor           (game: Trap Floor's GameDefinition)
  │   └── Super Leroy Sisters  (empty; names its game once it has a GameDefinition)
  └── Environment              (later)
  ```

  Assets: `Content/Catalogs/ComponentLibrary.asset`, `BaseBox.asset`, `ControllerBox.asset`, and the Game boxes in `Content/Catalogs/GameBoxes/`. Adding a game means adding its Game box to the library's list; no scene or code change.
- **A Game box names its game** (`ComponentCatalog.Game`, a `GameDefinition`); only Game boxes may, and a Game box with components must. The library rejects two boxes for one game, a catalog in the wrong slot, and a catalog listed twice. `TryGetGameBox(game)` finds the current game's box.
- **Entry:** a stable GUID ID (the ID Runtime uses), display name, kind, the 3D prefab, its UI face (icon), Toolbox visibility and order, and named links to definitions.
- **Linked definitions (relationships):** Console → `Layout` (`StandardConsoleLayout`); Die → `Shape` (`PhysicalD4`–`D20`); Deck, Stack and Discard Pile → `BayStyle` (`PileBayStyle`). A Game box names its game at catalog level (above).
- **Validation (once at initialisation):** GUID IDs unique across the whole library, prefab assets present, links with a role and a definition, and per kind: piles have their view, a fixed-container visual with a bay and a `BayStyle` link equal to the bay's style; a Console links a `Layout` equal to its prefab's `ConsoleLayoutBinding`; a Die links at least one `Shape`.
- **Runtime stays Unity-free:** Core and Application keep working with IDs; the catalog maps an ID or kind to the prefab and definitions in Presentation. Authority and Undo are unchanged.
- **Three faces of one component:** the stable ID (state), the 3D prefab on the table, and the UI face (Toolbox, Inspect, the in-game rules card). The same entry serves a template and the Toolbox.

## 3. Base Box catalog (C1)

| Entry | Kind | Prefab | Links |
|---|---|---|---|
| Card | Card | `PrototypeCard` | — |
| Deck | Deck | `Real/Deck` | BayStyle |
| Stack | Stack | `Real/Stack` | BayStyle |
| Discard Pile | DiscardPile | `Real/DiscardPile` | BayStyle |
| Pawn | Pawn | `PrototypePawn` | — |
| Token | Token | `PrototypeToken` | — |
| Die | Die | `PrototypeDie` | Shape × 6 |
| Console | Console | `Real/ConsoleMat` | Layout |
| Hand | Hand | `PrototypeHand` (not in the Toolbox) | — |

- Card, Pawn, Token and Die keep the GUIDs of `ToolboxComponentDefinitions`.
- Not yet in a catalog: the game board and the controller mapping board are scene objects today and get prefabs in C3.
- `ControllerBox.asset` and `TrapFloor.asset` are empty in C1 and are filled in C3.

## 4. Pile prefabs

- `Content/Prefabs/Real/Deck.prefab`, `Stack.prefab`, `DiscardPile.prefab`: root with the view, drop target, trigger box (the bay footprint, 1.16 × 1.56), label and fixed-container visual; a `Model` placeholder for the future model; a `Bay` child (`PileBayView`) reading `PileBayStyle.asset`.
- The bay's look lives only in `PileBayStyle.asset` (one design language, doc 21 principle 16). Its outer size is the placement footprint (doc 19 §12.2).
- The controller box model is expected to go into the Deck's `Model` slot, with its lid as the bay.

## 5. Stages

- **C1 (written):** catalog types and validation, `BaseBox`/`ControllerBox`/`TrapFloor` catalogs, `PileBayStyle`, the three pile prefabs, one behaviour per pile kind, bay footprint in placement; the composition spawns Decks and Stacks from the catalogs.
- **C1b (written):** `ComponentLibrary` (Base, Controller, Game boxes, Environment); Game boxes name their game; Super Leroy Sisters box (empty); the scene references the library through one field (`componentLibrary`).
- **C2:** the Toolbox becomes a UI prefab built from the library (tabs Base, Controller, Game Boxes with the current game, Environment; one button per entry); a new `RealUiPrefabCatalog` (UI prefab catalog) holds it plus the untouched Prototype UI prefabs; Discard Pile in Runtime (create, move, delete) and in the Toolbox; the quick-spawn grid uses catalog footprints.
- **C3:** every other spawn (card, pawn, token, die, console, hand) from the catalogs; game board and mapping board prefabs; Controller and Trap Floor boxes filled; the old prefab fields and their scene lines removed.

## 6. Open items

- **Controller deck size:** Trap Floor's controller deck is 48 cards today (no Start or Select). Owner to decide whether Trap Floor uses Start and Select.
- **UI faces:** icons and card art per entry are empty until the art is supplied.
