# 27 — Work Lists

Version 0.8 · 2026-10-15 · Owner's order: finish List 1, then List 2 (and the owner's further lists).

## List 1 — current (in progress)

| Item | What | Status |
|---|---|---|
| C3c | Game board from the catalog; Controller and Trap Floor box tiles place real cards (doc 22 §13) | Done, confirmed by the owner |
| C3d | Double-sided Console mat: 12 symbol slots + Main on the front (Square ×3 top, Diamond ×3 bottom, Star left, Cross right, Circle/Triangle/Plus/Minus corners), controller mapping on the back (A/B/X/Y take cards, D-pad takes dice set to a number), magnetic snapping by symbol, flip with the card-reveal animation (cards too), Duplicate (doc 22 §14); fix 2026-10-15: corner size for the seat check, all slots are Trap Floor item slots, landed tiles turn once no pawn is on them (doc 18 §15.2), Scene view layout editor | Done, approved by the owner (Console Workbench added) |
| C3e | Game board v2 (owner, 2026-10-15), see below; owner choices: snap points, boards snap edges and move singly, grid changed from the right-click menu, Trap Floor's board becomes a mat (doc 22 §15) | Done, approved by the owner |
| Rules content | The rules card covers how to play, dos and don'ts, card numbers and components used, not where cards go on the Console or controller (owner, 2026-10-15) | Recorded in doc 23 §7; applies to rules content and R4 hints |
| R3 | House rules in game: propose a change from the rules card, everyone agrees, it is saved on this computer as a named rule set and shown on the card; saved sets are picked on the card; changed settings start at restart (doc 23 §8) | Written, to be checked (run Create Missing Gameplay UI Prefabs first) |
| R4 | Hints: highlight legal moves and explain why a move is or is not allowed (doc 23) | To do |
| G2d | Remove the retired Search, Careful Search and Buy Ability code (doc 18) | To do |
| Pink material | Pink material when a card is placed on the Main slot | Waiting for a screenshot |
| Tests cleanup | After the owner deletes `Assets/ConsoleCards/Tests`, remove the two `InternalsVisibleTo` lines (TabletopCameraController.cs:8, TabletopPointerProjector.cs:8) | Waiting for the owner |

## List 2 — player-made content (after List 1)

The owner's intent (2026-10-15), recorded as said:

- **Blank base card.** The card model is a white base card with a front and a back material. A game's cards are
  that base card plus pictures.
- **Cards belong to a game.** Each game (Trap Floor first) has its own cards and decks, preset as a template for
  now: for example "Trap Floor has a 48-card deck: these decks, these cards".
- **In the game, not the Editor.** Players can, inside the game:
  - upload pictures to make their own cards,
  - build their own decks and use them in Trap Floor or any other game,
  - make a new game from the platform's components and save it as a template that can then be played.
- **Card editor (owner, 2026-10-15).** A proper menu to add new cards (name, id, description, picture, which deck,
  and so on) and to change the current default cards. Wanted both **in the game** and **in the Unity Editor**
  (the Editor version for testing).
- **Game selection menu.** The planned menu where a game is chosen and set up (rules, number of decks, …) is
  where this lives. Card art, packs and player content belong there, **not inside a running Trap Floor table**.

What already exists and is reused:

- A2–A4 (doc 24): art keys, the art lookup with player/pack/built-in layers, the per-game content store on the
  player's computer, the file picker and the Card Art panel. Conceptually right, wrongly placed: the Card art
  entry is in the Trap Floor table's Table menu and the pack tools are Editor menus. List 2 moves the entry to the
  game selection menu and adds deck and game-template editing in the game.

### C3e — Game board v2 (owner, 2026-10-15)

- **The board is the "screen".** The table reads like a console game: the game board (screen) at the top, each
  player's Console mat and controller below it.
- **Look:** an orange-framed mat with a grid of card spaces (a 10 × 5 grid in the reference picture).
- **Extendable:** several board mats join edge to edge on the table to make a bigger board (the 2 × 2 example).
- **Resizable grid (virtual tabletop only):** the number and size of the card spaces inside a board can be changed
  in the game; the physical product stays the same.
- **Magnetic grid:** cards snap into the board's grid spaces, like Console slots.
- **Areas (not decided yet):** a board variant with marked areas for deck, hand, discard pile and so on (the
  green + / red − panels in the reference).
