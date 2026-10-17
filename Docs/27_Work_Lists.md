# 27 — Work Lists

Version 1.27 · 2026-10-18 · Owner's order: finish List 1, then List 2 (and the owner's further lists).

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
| P2 | Profile screen in the game: pick, create, rename a profile; Table menu "Profile: name…"; asked at start when there are several profiles; switching starts a fresh table (doc 28) | Done, approved by the owner |
| L2-2 | Card editor in the game (Table > Card editor…): pick the game; the game's deck (every card with copies, and the total); your own cards; live preview; new cards that work like a card of the game; changes to game cards with Reset; "In decks" ticks with copies in each (the game's deck and yours); pictures copied into the profile (doc 24, L2-2 record). First build looked good to the owner; game picker, game deck and In decks added 2026-10-16 | Done, approved by the owner |
| L2-3 | Deck editor in the game (Table > Deck editor…): pick the game; the game's deck and your decks; new, copy, rename, delete; every card's copies in the selected deck; Save / Undo changes (doc 24, L2-3 record) | Done, approved by the owner |
| L2-4 | Game selection menu (New table…): every game with picture, players, mode, rules and deck; Playing as; Make it yours (card art, card editor, deck editor, moved off the Table menu); Start (doc 24, L2-4 record) | Done, approved by the owner |
| L2-5 | Decks used by games: choose the deck to play with in the game selection menu; Trap Floor plays with your deck (setup tried first); an Empty Table starts with your deck to place (doc 24, L2-5 record) | Done, approved by the owner |
| L2-6 | Player-made games: set up an Empty Table, Save table as a game (name, players, description, written rules); the table save file; your games in New table… (Start lays the table out, rules on the rules card, Save over, Delete) (doc 29) | Written; owner asked for polish (L2-6b) |
| L2-6b | Polish (owner, 2026-10-16): Save table as a game in steps — 1 Deck (choose yours or a game's, or make a new one; put it on the table), 2 Cards (create a card in it or pick its cards), 3 Save; the deck is the game's own deck, placed shuffled where it lay at Start (owner's choice). Toolbox placement keeps placing until Esc or right-click (owner's choice) (doc 29 §L2-6b) | Done, approved by the owner |

### Next — approved order (owner, 2026-10-16; doc 30). Visual work moved down (owner, 2026-10-18)

The owner is waiting for visual approval (V0 board with Tony and Russell), so all visual work waits at the end of
the list. The order that stays active:

| Order | Stage | What | Status |
|---|---|---|---|
| 1 | L2-6b | Save table as a game in steps; keep placing | Done, approved |
| 2 | R0 | Names: Prototype classes and prefabs get final names, renamed so references stay intact (doc 30 A5, record in Part D) | Done, approved ("Works now"; the one-off R0 tool may be deleted) |
| 3 | T1–T3 | Table shape and size: table spec and builder, star and edges, Table mode with handles (doc 30 B) | T1 done, working after the collider fix (owner, 2026-10-17; doc 30 §B6); T1b model tables done, approved (owner, 2026-10-17; doc 30 §B8); T2 star, edges and edge colliders done, approved (owner, 2026-10-17; doc 30 §B9); T3 Table mode approved; T3 completed with the owner's choice (pieces move in), size handles and the shape change; owner 2026-10-18: every Toolbox piece must move in (the game mat did not), done, approved (owner, 2026-10-18; doc 30 §B10). Seats along the outline moved to CART-1 |
| 4 | Q2 | Circuit-board skybox (doc 30 §C0): Console Cards > Look > Use Circuit Skybox | Done, approved with the moving currents (owner, 2026-10-18) |
| 5 | R1–R2 | Code cleanup: split the composition, local command door, UI from events (multiplayer-ready, doc 30 A6); stages R1a–R1f, then R2 (doc 30 Part E) | R1a (split into partial files by job, no code changed) written 2026-10-18, to be checked (not compiled yet); R1b next |

Not yet placed in the order (owner to choose): UX-1 (cursor, gestures, the free table ping), CART-1 (cartridge
contract; also seats along the table outline), SLS (Super Leroy Sisters, waiting for the game from Russell).

### Waiting for visual approval (owner, 2026-10-18)

Started only after the V0 look is approved (the V0 design board: versions A, B and C of every screen, and the
console-style table). Owner's own preference so far: version B.

| Stage | What | Status |
|---|---|---|
| Q1 | Fix the panel corner glitches (client meeting 2026-10-15) | Waiting |
| V0–V1 | Art direction "inside the console" (circuit board) with layout mock-ups; lighting and post (doc 30 C) | V0 board made 2026-10-17 (16 screens in A, B and C, with references); waiting for approval |
| T4–T5 | Table textures (any picture, repeat or fit) and paint layer, within the B5 budget; includes the console-style table textures (plastic body and mat) if approved | Waiting |
| V2–V5 | Pieces; table and room; menus and layout (V4a), readability (V4b), circuit-board skin (V4c); motion and feel | Waiting |
| V6–V7 | Later visual stages (doc 30 C) | Waiting |

Client meeting 2026-10-15 (doc 30 §C0): quick wins before V0 — **Q1** fix the panel corner glitches (now waiting
for visual approval), **Q2** circuit-board skybox (written 2026-10-17, to be checked); V0 is made with Russell;
**SLS** add Super Leroy Sisters once Russell sends it.

### To-do — only when the owner asks

- **Multiplayer (MP-1 to MP-5, doc 30 A6).** The architecture is approved; it is built only when the owner says
  "implement multiplayer". Until then every stage keeps the code multiplayer-ready (doc 30 A6, "Keep the code
  multiplayer-ready").

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
