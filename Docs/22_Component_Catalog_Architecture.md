# Console Cards - Component Catalog Architecture

**Document ID:** 22_Component_Catalog_Architecture
**Version:** 0.3
**Status:** Stages C1, C1b and C2a written 2026-10-08 (catalog types, component library, Base Box catalog, pile prefabs, authored Toolbox); C2b written 2026-10-09 (Discard Pile as a full component, §8); C3a written 2026-10-09 (every spawn from the catalogs, §9); C3b-1 written 2026-10-09 (local Console, Hand, Avatar card and pawn from the catalogs, §10); H-E written 2026-10-09 (Empty Table hand and its switch, §11); C3b-2 written 2026-10-09 (scene-owned pieces removed, §12). C3c pending; each needs its own approved plan. Binding rule: doc 21 principle 18.
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
- Not yet in a catalog: the game board is a scene object today and the controller mapping board does not exist yet; both get prefabs in C3c.
- `ControllerBox.asset` and `TrapFloor.asset` are empty in C1 and are filled in C3c.

## 4. Pile prefabs

- `Content/Prefabs/Real/Deck.prefab`, `Stack.prefab`, `DiscardPile.prefab`: root with the view, drop target, trigger box (the bay footprint, 1.16 × 1.56), label and fixed-container visual; a `Model` placeholder for the future model; a `Bay` child (`PileBayView`) reading `PileBayStyle.asset`.
- The bay's look lives only in `PileBayStyle.asset` (one design language, doc 21 principle 16). Its outer size is the placement footprint (doc 19 §12.2).
- The controller box model is expected to go into the Deck's `Model` slot, with its lid as the bay.

## 5. Stages

- **C1 (written):** catalog types and validation, `BaseBox`/`ControllerBox`/`TrapFloor` catalogs, `PileBayStyle`, the three pile prefabs, one behaviour per pile kind, bay footprint in placement; the composition spawns Decks and Stacks from the catalogs.
- **C1b (written):** `ComponentLibrary` (Base, Controller, Game boxes, Environment); Game boxes name their game; Super Leroy Sisters box (empty); the scene references the library through one field (`componentLibrary`).
- **C2a (written):** the authored Toolbox (§7); several cards placed at once become one Deck; the Real UI prefab catalog; the scene's `RuntimeUiManager` uses it.
- **C2b (written):** Discard Pile in Runtime (create, move, delete) and in the Toolbox (cards arrive face down, declared on the pile style); Undo and Redo rebuild every Toolbox-placed piece; the quick-spawn grid uses catalog footprints (§8).
- **C3a (written):** Card, Pawn, Token, Die and Console spawns from the catalogs; the seven old prefab fields and their scene lines removed (§9).
- **C3b-1 (written):** the local Console and Hand, the local Avatar card and pawn become catalog spawns (§10).
- **H-E (written):** an Empty Table hand, on by default, with a Toolbox footer switch (§11).
- **C3b-2 (written, §12):** the unused scene pieces (scene Console, Hand, loose card, pawn and token, the first-prototype deck, stacks A and B and discard pile), their fields and dead code removed; the tests for the removed pieces removed and the rest updated (owner choice A).
- **C3c:** game board and mapping board prefabs; Controller and Trap Floor boxes filled, and their tiles place real game pieces (§6).

## 6. Open items

- **Controller deck size (decided 2026-10-09):** the controller deck stays 48 cards (6 each of Up, Down, Left, Right, A, B, X, Y); Start and Select are left out. Whether a game uses them is part of the rules stage.
- **Box tiles (decided 2026-10-09):** Controller and Game box tiles place real game pieces (for example a full labelled controller deck, or a game's real Item cards), not generic ones.
- **Controller mapping board (decided 2026-10-09):** one spot per input where a player lays a card; the mapping rules come later.
- **Avatar (Hero) cards (owner, 2026-10-09):** bigger than other cards, sized to fit the Console's Main slot. Done in C3c with the Trap Floor box's Avatar entry (its own card prefab sized from the Main slot footprint); until then an Avatar is a standard card.
- **UI faces:** icons and card art per entry are empty until the art is supplied.

## 7. Toolbox (C2a, owner decisions 2026-10-08)

- **No UI is built at runtime (owner).** `Content/Prefabs/Real/UI/ComponentToolbox.prefab` is an authored prefab (`ComponentToolboxView`); each tile is a `ToolboxEntryTile` that names one catalog entry. At runtime the Toolbox only binds clicks, checks that every Toolbox entry of the library has exactly one tile and every tile has an entry, and switches tabs, chips and the Placing card.
- **Edit-time builder:** `Console Cards > Toolbox > Build Toolbox Prefab` makes the whole prefab (it also runs once by itself when the prefab is missing); `Sync Tiles from Library` regenerates only the tabs, game chips and tiles by cloning the templates inside the prefab (`Templates`: tab, chip, tile, empty state), so restyling a template and syncing restyles every copy. Both point the Real UI prefab catalog's Toolbox entry at the prefab and give icon-less entries their kind's icon.
- **Look (approved mockup, canvas "Toolbox" page):** cream panel with a thick dark outline and offset shadow, orange header, pixel-font titles (Silkscreen) and Chakra Petch text (both OFL, `Content/UI/Fonts/`), tabs Base Box / Controller Box / Games (one chip per game box; the current game's chip is selected) / Environment when the library has one; two-column tiles with icon, name and the entry's description; an empty-box message for shelves with no tiles; controls in the footer. While placing, the panel folds into a Placing card (icon, "PLACING", subject and rotation) with the controls at the bottom centre.
- **Cards:** the Card tile has a − / + counter (1–99). 1 places one loose card; 2 or more place one Deck holding that many face-down cards, in one command (`CreateTabletopComponentRequest.DeckCardCount`), so one Undo removes both. The ghost shows the deck in its bay with the cards stacked.
- **Dice:** the Die tile's size chips come from the entry's linked Shape definitions; a chip places that size at once.
- **Discard Pile** is in the Toolbox from C2b (§8).
- **Real UI prefab catalog:** `Content/Prefabs/Real/UI/RealUiPrefabCatalog.asset` holds the new Toolbox plus the ten Prototype UI prefabs not yet remade; the scene's `RuntimeUiManager.prefabCatalog` points at it. The Prototype Toolbox prefab and view stay untouched and unused.

## 8. Discard Pile (C2b, 2026-10-09)

- **Runtime:** `TabletopComponentKind.DiscardPile` (added last). Creating one makes an empty placed Discard Pile container in one command; it moves like a Deck or Stack (a seat-owned pile only by that seat's player) and a Toolbox pile can be deleted when empty. Template piles stay protected.
- **Arrival face (authority):** `ContainerArrivalFace { Unchanged, FaceDown, FaceUp }` on `ContainerState`, carried by the Undo snapshot. A single-card transfer into a container with an arrival face sets the card's face in the same command; rollback restores it. Batch transfers (draw, merge) do not apply it.
- **Declared on the pile style:** `GameTemplatePileStyle.ArrivalFace`. `DefaultFor(DiscardPile)` is the Discard mark and `FaceDown`; Deck and Stack are `Unchanged`. A template container copies its declared style (or the kind's default) onto the Container when the Match is built; a Toolbox pile passes the default through `CreateTabletopComponentRequest.ContainerArrivalFace`. Validation requires a defined arrival face.
- **Presentation:** every Discard Pile, from the Toolbox or declared by a template, is the catalog's `DiscardPile.prefab` resting plate-less on the table in its bay with the Discard mark (cards squared in the bay, height capped like a Deck). Right-click gives "DISCARD PILE": Move, and Delete for Toolbox piles. A card's look flips when it lands.
- **Undo and Redo rebuild:** after the session rebuild, every placed Deck, Stack and Console made by the Toolbox or a split, and every Discard Pile, is recreated from the catalog (before C2b, such pieces disappeared from the table after a later Undo).
- **Quick-spawn grid:** cells are a pile's bay plus the placement clearance (1.36 × 1.76); rows step toward the table centre.
- **Not yet:** the old scene-owned discard pile stays until C3.

## 9. Spawns from the catalogs (C3a, 2026-10-09)

- The composition resolves its Card, Pawn, Token, Die and Console prefabs from the library: the first entry of each kind in shelf order (the same rule as the piles). They are the same prefabs as before (`PrototypeCard`, `PrototypePawn`, `PrototypeToken`, `PrototypeDie`, `Real/ConsoleMat`).
- The Console layout comes from the catalog Console prefab's `ConsoleLayoutBinding`; catalog validation already requires its `Layout` link to match.
- Removed: `prototypeCardPrefab`, `prototypePawnPrefab`, `prototypeTokenPrefab`, `prototypeDiePrefab`, `prototypeDeckPrefab`, `prototypeStackPrefab`, `prototypeConsolePrefab` and their seven scene lines. Their prefab checks run in the catalog validation.

## 10. Local seat pieces from the catalogs (C3b-1, 2026-10-09)

- **Console:** every seat's Console, the local one included, is built from the catalog Console entry and posed from its seat; the scene Console is no longer used.
- **Hand:** the local Hand is built from the catalog Hand entry (`PrototypeHand`) at the scene Hand's old pose and drives the camera tray as before. It uses the prefab's Tray Handoff Duration (0.28 s); the scene Hand's 5 s override is dropped (owner choice).
- **Avatar card and pawn:** created like every other card and pawn; the scene loose card, pawn and token are no longer used.
- The unused scene pieces were removed in C3b-2 (§12).

## 11. Empty Table hand (H-E, 2026-10-09)

- **Runtime:** an Empty Table starts with one unowned Hand container (arrival face up), part of the baseline. `DrawCardsUseCase` now applies the destination's arrival face to the drawn cards. `CollectHandIntoDeckUseCase` moves every card of a Hand, in order and face down, into a new placed Deck in one command.
- **Hand switch (owner choice):** in the Toolbox footer, ON / OFF with a one-line hint, shown only on an Empty Table. Off with cards in the hand: they go to the table as one face-down Deck at the first free quick-spawn cell (one Undo step) and the table is rebuilt without the Hand. On: the table is rebuilt with it.
- **A table setting, not a move:** Undo and Redo keep it, except that an Undo which puts cards back into a Hand that is off turns it back on. Reset and loading a table turn it on.
- **Draw to Hand (owner choice):** dragging, plus "Draw to Hand" with a count on a Deck's right-click menu whenever the local player has a Hand that is on (not on Trap Floor's controller decks, which keep their own Draw). The hand comfort cap applies.
- **Prefab:** `Build Toolbox Prefab` adds the footer `HandSwitchRow`; the view hides it and lowers the content edge when no switch is shown.

## 12. Scene-owned pieces removed (C3b-2, 2026-10-09)

- **Scene:** the nine first-prototype prefab instances are gone (loose card, pawn and token; deck, stacks A and B, discard pile; Hand; Console), with their composition fields (`cardView`, `pawnView`, `tokenView`, their selection visuals and highlight roots, `sceneDeckVisual`, `sceneStackAVisual`, `sceneStackBVisual`, `sceneDiscardPileVisual`, `sceneHandVisual`, `sceneConsoleView`, `sceneConsoleSlotViews`, `sceneConsoleSlotVisuals`). The empty `TabletopObjects` and `Containers` roots stay.
- **Composition:** the first-prototype deck, discard and stack A/B paths are removed (their IDs were always empty in Trap Floor and Empty Table), with the old developer-panel Shuffle / Draw 1 / Draw 3 / Merge buttons and the public `ShuffleDeck()`, `DrawOne`, `DrawThree`, `DrawCards(int)`, `MergeStackAOntoStackB/BOntoA`, `DeckView`, `DiscardPileView`, `DeckContainerId`, `DiscardContainerId`, `StackAContainerId`, `StackBContainerId`. Trap Floor's Floormaster deck and discard refresh through `ApplyLayout` of their template containers. The coin area takes its plate material from the catalog Hand prefab. The Trap Floor status line shows the hand count in place of the old deck/discard counts.
- **Tests (owner choice A):** `TabletopPrototypeCompositionTests` and `TabletopM3PrototypeSceneTests` are removed: every case built or loaded the first-prototype composition. `TabletopPrototypeSceneStructureTests` now expects no scene-owned pieces and drops its prefab-instance case. The other tests are unchanged.
