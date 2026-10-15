# 27 — Work Lists

Version 1.7 · 2026-10-15 · Owner's order: finish List 1, then List 2 (and the owner's further lists).

## List 1 — current (in progress)

| Item | What | Status |
|---|---|---|
| C3c | Game board from the catalog; Controller and Trap Floor box tiles place real cards (doc 22 §13) | Done, confirmed by the owner |
| C3d | Double-sided Console mat: 12 symbol slots + Main on the front (Square ×3 top, Diamond ×3 bottom, Star left, Cross right, Circle/Triangle/Plus/Minus corners), controller mapping on the back (A/B/X/Y take cards, D-pad takes dice set to a number), magnetic snapping by symbol, flip with the card-reveal animation (cards too), Duplicate (doc 22 §14); fix 2026-10-15: corner size for the seat check, all slots are Trap Floor item slots, landed tiles turn once no pawn is on them (doc 18 §15.2), Scene view layout editor | Done, approved by the owner (Console Workbench added) |
| C3e | Game board v2 (owner, 2026-10-15), see below; owner choices: snap points, boards snap edges and move singly, grid changed from the right-click menu, Trap Floor's board becomes a mat (doc 22 §15) | Done, approved by the owner |
| Rules content | The rules card covers how to play, dos and don'ts, card numbers and components used, not where cards go on the Console or controller (owner, 2026-10-15) | Recorded in doc 23 §7; applies to rules content and R4 hints |
| R3 | House rules in game: propose a change from the rules card, everyone agrees, it is saved on this computer as a named rule set and shown on the card; saved sets are picked on the card; changed settings start at restart (doc 23 §8) | Done, approved by the owner |
| R4 | Hints: reach while holding your pawn, a too-far move is explained with Undo move offered, the move count is a rule setting (doc 23 §9); landing triggers at once and the tile turns once the pawn is off (doc 18 §15.2) | Landing approved; reach reworked in H1 |
| H1 | Hint system (doc 23 §10): hint data, service, hint strip, Tips switch, platform tips; reach as markers; movement in one straight line, 2 tiles, the Athlete 3 (doc 18 §15.2) | Done, approved by the owner |
| H2 | Hint tags on rule lines; game tips made from the rules in play; Trap Floor rules tagged (doc 23 §10) | Done, approved by the owner |
| H3 | Trap Floor hint helpers behind the rule tags: reach and why not (RULE reminders), prize, waiting Traps, collapse target as a board marker (doc 23 §10); a landing the rules do not allow does not flip, the warning points at the Undo button, Keep move in the turn menu | Done, approved by the owner |
| Shop | The market returns as the Shop (owner, 2026-10-15): every Ability, bought on your turn as well as taking from the floor; Shop entry (icon and word) on the session bar; on/off rule with everyone's agreement, applied at once (doc 18 §15.4, doc 23 §5) | Done, approved by the owner |
| G2d | Remove the retired Search and Careful Search code (doc 18 §15.4); the market stays as the Shop. Part 1: the table's Search code and texts. Part 2: search data in the reveal rules, the Undo record, the search cost settings, the old Buy service | Done, approved by the owner (parts 1 and 2) |
| Pink material | Pink material when a card is placed on the Main slot | Parked by the owner (2026-10-15) |
| Tests cleanup | The owner deleted `Assets/ConsoleCards/Tests`; the two `InternalsVisibleTo` lines (TabletopCameraController.cs, TabletopPointerProjector.cs) are removed | Done, approved by the owner |

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
  and so on) and to change the current default cards, **in the game** (the owner dropped the Unity Editor version
  later the same day: everything is done in the game).
- **Game selection menu.** The planned menu where a game is chosen and set up (rules, number of decks, …) is
  where this lives. Card art, packs and player content belong there, **not inside a running Trap Floor table**.

What already exists and is reused:

- A2–A4 (doc 24): art keys, the art lookup with player/pack/built-in layers, the per-game content store on the
  player's computer, the file picker and the Card Art panel. Conceptually right, wrongly placed: the Card art
  entry is in the Trap Floor table's Table menu and the pack tools are Editor menus. List 2 moves the entry to the
  game selection menu and adds deck and game-template editing in the game.

### The owner's direction for List 2 (2026-10-15)

- The platform is what players buy. It comes with Trap Floor and our components and tools (dice, tokens, template
  cards, game box, base box and so on). More games come as DLC or updates, each with its own cards, decks,
  numbers and rules.
- Players can change a game's cards and numbers, add new cards, build decks, write rules and make their own games.
  Their cards always sit on our components and use our tools.
- **Everything is done in the game**, on visual screens, not in the Unity Editor.
- **Everything is saved in the player profile**, next to the game (doc 28): several named profiles, pick one.
- Card IDs are made for the player and shown, with an optional short code. A new card has a front and a back
  picture, an ID, a name, a description, a cost, a number of copies, and the game card it behaves like. Changing a
  game card sets the values you want for those same fields, with Reset.

### List 2 stages (owner's order, 2026-10-15)

| Stage | What | Status |
|---|---|---|
| L2-1 | Card data: your new cards, changes to default cards and decks, applied at table start before house rules; pack format 2 carries cards and decks (doc 24) | Reworked: saved in the profile; Editor menus removed. Written, to be checked (not compiled yet) |
| P1 | Save system: profiles next to the game, one save path, every save in the profile (doc 28) | Written, to be checked (not compiled yet) |
| P2 | Profile screen in the game: pick, create, rename a profile | Next |
| L2-2 | Card editor in the game, with a visual card preview (new cards and changes to default cards) | Planned |
| L2-3 | Deck editor in the game | Planned |
| L2-4 | Game selection menu (replaces New table…); Card art moves there | Planned |
| L2-5 | Decks used by games (Trap Floor or the Empty Table) | Planned |
| L2-6 | Rules and player-made games from our components | Planned |

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
