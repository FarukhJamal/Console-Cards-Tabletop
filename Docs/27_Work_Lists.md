# 27 — Work Lists

Version 1.65 · 2026-10-21 · Owner's order: finish List 1, then List 2 (and the owner's further lists).

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
| L2-4 | Game selection menu (New table…): every game with picture, players, mode, rules and deck; Playing as; Make it yours (card art, card editor, deck editor, moved off the Table menu); Start (doc 24, L2-4 record). Owner 2026-10-18: Card editor… and Deck editor… are back in the Table menu too, to check cards and decks without starting a new table | Done, approved by the owner |
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
| 5 | R1–R2 | Code cleanup: split the composition, local command door, UI from events (multiplayer-ready, doc 30 A6); stages R1a–R1f, then R2 (doc 30 Part E) | R1a done, approved (owner, 2026-10-18). R1b part 1 (the command door; the table code's 38 direct use-case calls go through it) approved (owner, 2026-10-18: "rest seems fine", with the pile tools found while checking it); R1b part 2 and R1c–R1f written together on the owner's request (2026-10-18, "Do all R1's in one go"; doc 30 Part E), approved (owner, 2026-10-18). R2a–R2d written on the owner's request (2026-10-18, "implement whole R2"; doc 30 Part F), approved (owner, 2026-10-19). R1–R2 done |
| 6 | UX-1 | Cursor, gestures and the free table ping (doc 30 A2) | Written 2026-10-19 on the owner's "go": table ping (Alt-click or middle tap), cursor states, Shift-flick, dice shake (doc 30 Part G). Accepted with the polish plan (owner, 2026-10-19); the keys become editable in POL-0 |
| 6b | POL-0–POL-9, SHUF-1, DEAL-1 | Polish the mechanics to Tabletop Simulator's feel (doc 30 Part H): editable keys, icons and feel numbers first, then pick-up and carry, drop and settle, hover and info, piles (spread), dice and flick, hand, select several and group into deck, sound, a hideable controls toolbar; shuffle animations and styles; dealing written in the game's rules | Accepted (owner, 2026-10-19). Order set from the owner's worst points (doc 30 Part H): POL-0 (written 2026-10-19, to be checked), TOOLS-1 tool bar with Ping tool and keys strip (accepted 2026-10-19; TOOLS-1b: Select picks every component but boards and Consoles, to be checked), SMOOTH-1 smooth shuffle and split and no missed clicks (written 2026-10-19, to be checked), POL-1 to POL-8 all at once (owner, written 2026-10-19, approved with five points, done the same day; selection outlines fixed, to be checked), ICON-1 and FRAME-1 (approved, owner 2026-10-19), SHUF-1 shuffle styles and flourishes (approved, owner 2026-10-19), DEAL-1 dealing by the game's rules (approved with two fixes, owner 2026-10-20). Next: CART-1 |
| 6c | ICON-1 | Cards with icons on any side or corner connect to matching Console slot icons at any rotation (client checklist item 1, doc 32) | Written 2026-10-19, to be checked (owner's choices: icons set in the card editor, 4 sides and 4 corners drawn small on the card; Console slots only; the nearest matching slot wins). Record in doc 30 Part H |
| 6d | FRAME-1 | Card frames per game: the platform draws each card's border, tick marks and icon tabs from the game's Card Frame Style; the symbols stay the same in every game (doc 30 Part H, FRAME-1) | Agreed (owner, 2026-10-19): the platform draws the border, a game has a default frame plus named variants each card picks, tabs stick out past the card edge. Written 2026-10-19 on the owner's "Start Frame1" with stand-in frame pictures until the real ones exist (frames and symbols replaceable without code). Approved (owner, 2026-10-19) |
| 7 | CART-1 | Cartridge contract (what a game hands the platform, doc 30 A4), including seats along the table outline | In stages (owner, 2026-10-20): **1a** the contract, Trap Floor plugged in through it, data cartridges for an Empty Table and player-made games (approved); **1b** the rest of Trap Floor's state behind it, the table's own files no longer name Trap Floor, and **1c** seats along the table's edge on any shape (approved, owner 2026-10-20; doc 30 §A4 records; the Table menu now scrolls). The cartridge piece and boot moment come later with the visuals (owner) |
| 8 | PAD-1 | Controller pad (doc 32): up to 3 real dice per arrow, the meaning of a controller card shown when it goes into a Console slot ("X = Heavy Kick", "Right = 2d6 (4 + 3)") | Owner's choices 2026-10-20; written and approved 2026-10-20 (cards without a front picture show the plain yellow plate; art comes later) |
| 9 | HAND-1 | Smaller hand drop area: a card from the table goes into the hand only over the lower part of the hand strip (Feel › Hand Drop Height, 55 %) | Owner's choice 2026-10-20; written 2026-10-20, to be checked |
| 10 | DRAW-1 | Drawing tool (client checklist item 5, moved up from T5 by the owner 2026-10-20): the Draw tool's panel with Pen, Highlighter (MARKER), Label, Eraser (Pen and Marker freehand; Shift held: a straight line; STYLE cycles Plain, Brush, Swirl, Wavy, Dashed, Dotted; owner 2026-10-20), 8 colours (the first is your seat's colour) and Clear all; lines on the table, mats, boards and Trap Floor's floor; the eraser takes a whole line or label; each line, label, erase or Clear is one Undo step; saved with "Save table as a game"; a new or reset table starts clean (doc 31 P-DRAW) | Owner's choices 2026-10-20 (all four recommended); written 2026-10-20; approved with the straight lines and STYLE (owner, 2026-10-20) |
| 11 | CAM-1 | Camera fixes (owner, 2026-10-20: "some of keys 1–4 are not working; the Table tool zooms in too much; after a pan the tool starts from the wrong point"): views 1–4 work on the Empty Table (whole table, or your near half for 1); the camera rests on the measured table top; Table mode frames the whole table from above wherever the camera was, follows a size change, and Done goes back to the Board view; a size handle stays under the pointer at any angle; typing in a text box no longer moves the camera. Also fixed: seats along the edge followed the table one change behind | Written 2026-10-20, approved with DRAW-1 (owner, 2026-10-20; doc 31 P-CAMERA) |
| 12 | ART-0 | Stand-in card fronts (owner's choices 2026-10-20): every built-in card with no front art (today 21 Trap Floor cards: 19 traps, Push, Overcharged) gets a PNG front drawn by Console Cards > Content > Create Stand-in Card Fronts: the name at the top, a type band (TRAP, ABILITY, FRIEND…), a type symbol (warning sign, bolt, star) and the rules text, on the type's colour with rays and rounded corners. Real art always wins (doc 31 P-ART) | Written 2026-10-20, approved (owner, 2026-10-20) |
| 13 | BOX-FIX | Toolbox fix (owner, 2026-10-20): Trap Floor's box showed the six abilities twice ("Floor Abilities" and "Trap Floor Abilities", the game's floor set and shop set hold the same cards). Fill Controller and Game Boxes now makes one tile per distinct set of cards and removes a deck tile that places the same cards as an earlier one ("Floor Abilities" stays) | Written 2026-10-20, approved (owner, 2026-10-20) |
| 14 | BUG-1 | Known issues (CLAUDE.md), owner's pick 2026-10-20: (a) no errors when Play mode stops (panels and pile/slot feedback are left alone while the scene is torn down); (b) d6 and d8 use the pack's painted "Rough Scratched" materials (blue d6, red d8; one line in each prefab), so their numbers are readable; (c) a cocked die shows "?" above it, a message says to roll it again (R), and its menu says "cocked (roll again)"; (d) a piece carried in from off the table eases down onto it instead of dropping. The camera height issue was fixed in CAM-1 | Written 2026-10-20 (one compile fix: UnityEngine.Application), approved (owner, 2026-10-20) |
| 15 | MARK-1 | Move-target lights (last known issue, owner's pick 2026-10-20). Checked: the Rush and Dodge target tiles already show as outlines with an arrow (board feedback). What showed nothing was a trap made harmless (Disarm, Check): its tint went on a hidden plate. It now gets a calm green outline on its tile while the floor lasts | Written 2026-10-20, to be checked |
| 16 | ABIL-1 | Abilities are the player's choice, with help (owner, 2026-10-21: an Ability in the Console fired at once or did nothing, and a trap later hit anyway). Putting an Ability into the Console asks first: what it would do now (or why not and when), its cost, Use / Not now; the card stays in the slot. A waiting trap with a Reaction that can stop it is offered on the reveal ("Use a Reaction…" / "Let it happen"), in the turn menu and before End Turn. Using is free (owner, 2026-10-21, as in the playtest: buying is the payment); a card in the hand goes into a free Console slot (doc 31 TF-ABILITIES) | Owner's choices 2026-10-21; written 2026-10-21, to be checked |
| 17 | COLLAPSE-2 | The collapsing tile (owner, 2026-10-21, as in the playtest): the 2d6 make it a hole at once and unlock it; any player lifts it off and sets it beside the board; End Floor Turn sets any still in its cell beside the board (a column to the right). Pawn tiles are found by grid cell, so a lifted tile still counts as the hole there. Rules card line tf-floor-roll says so | Written 2026-10-21, to be checked |
| 18 | FLOORS-1 | Floors that shrink (owner, 2026-10-21): the first exit (win) or everyone eliminated (lose) ends the floor; a new floor is dealt fresh, one row and one column smaller (6×6, 5×5, 4×4, 3×3; 3×3 is the last floor); tiles reshuffled face down, pawns back to the corners, holes reset; a die beyond the grid is rolled again | Owner's choices 2026-10-21; next (needs a plan for which tiles a smaller floor uses) |
| 19 | FLOW-1 | Turn flow fixes (owner, 2026-10-21): (a) a knocked-out player is out for the rest of the floor and gets no more turns (only a failed floor, everyone out, brings everyone back); End Turn after a waiting trap knocks you out no longer says "rejected"; (b) a tile the floor dice collapse is no longer drawn as a black HOLE: it keeps its own face and back and is an ordinary card (move, flip, stack, menu), while its space stays a hole; a taken prize's tile stays the hole marker; (c) a prize you leave when your turn ends is not offered to you again while you stay on it ("walk off next turn"); the next player to land there may take it. Rules card: tf-trap-out and tf-ability-cost reworded; Fall Trap text says "out for the rest of this floor" (doc 31 TF-ELIMINATION, TF-PRIZES, TF-COLLAPSE) | Written 2026-10-21, to be checked |


**Pile tools (owner, 2026-10-18, found while checking R1b):** Duplicate was missing on decks, stacks and discard
piles; there was no Split on decks; a card dropped on a card did not stack. Owner's choices: Duplicate copies all
the cards; Split into 2–6 piles; card on card works as in Tabletop Simulator. Written 2026-10-18
(`TabletopComposition.PileTools.cs`, `DuplicatePile` in the creation use case, decks allowed in Split Stack), to be
checked (not compiled yet). Register: P-PILES, P-COMPONENTS.

**Fix (owner, 2026-10-18: "cards moved from a stack become not interactable"):** when a card-on-card stack formed
while the pointer rested over a card, rebuilding the table's interaction code stopped half way with an error, so
nothing on the table could be picked up afterwards. The rebuild now drops the hover first, and the stack waits if the
player is already holding something else. Checked live in Unity 2026-10-18: stack made, card dragged out, both cards
pick up again, no errors.

**Stacking feel (owner, 2026-10-18: "empty stack should disappear when one card is left and stacking should feel
satisfying"):** carrying a card over a loose card lights a green stack outline under it; letting go stacks at once
(the cards glide together, a soft double tap, the new pile flashes green). A card landing on a stack or deck gives a
light tap and a short green flash. A stack left with one card turns back into a loose card, and an emptied stack goes
away (game piles never; a Toolbox stack only once it has been used); Undo never shows a stack of one. Two short
sounds made for the game (`Content/Resources/CardSounds`). Code: `TabletopComposition.Stacking.cs` (new). Checked live
in Unity 2026-10-18: outline, instant stack, one-card tidy-up and Undo work, no new errors. The flash and sound are
for the owner to judge.

**Stacking fixes (owner, 2026-10-18: feel approved; "stack always forms at a certain point", "no Split on a stack
made from free cards, the mechanic should be the same", "Undo on a stack makes cards fly"):** the stack now forms
where the bottom card really lies (its physics position, not where it was first placed). Right-clicking any card in a
stack opens the STACK menu, as a card in a deck opens the DECK menu: Inspect, Draw to Hand, Shuffle, Split Stack,
Split into…, Merge Into…, Duplicate, Move, Delete (the old card-in-stack menu with Move Up/Down is gone; stacks now
draw and shuffle like decks). Undo: a card let go onto the table is one step together with its landing (it used to be
two steps, and undoing the second put the card back in mid-air with its throw speed); the one-card tidy-up joins that
step and no Undo snapshot is taken of a card in mid-air. Written 2026-10-18, not compiled here; for the owner to
check. Pile tools, stacking feel and these fixes approved (owner, 2026-10-18). **Working rule (owner, 2026-10-18):** when fixing, change the code only; do not drive Unity on the owner's
computer, the owner checks.

Not yet placed in the order: SLS (Super Leroy Sisters, waiting for the game from Russell; the owner will say when).

**Carry a whole stack (owner, 2026-10-18):** Merge Into… is gone from the stack menu. Press and hold a card in a
stack without moving and the whole stack lifts and follows the pointer; let go on another stack (its outline lights
green) to merge, anywhere else to put it down; Escape puts it back. A quick drag still takes the top card. Written
2026-10-18 (`TabletopComposition.StackCarry.cs`, the router's new carry route), approved (owner, 2026-10-19). Register: P-PILES.

**Duplicate a single card (owner, 2026-10-18):** a duplicated card is now an exact copy (same card and face), also for
a game's own cards; pawns, tokens and dice from a game still become generic copies. Written 2026-10-18, to be checked.

### Waiting for visual approval (owner, 2026-10-18)

Started only after the V0 look is approved (the V0 design board: versions A, B and C of every screen, and the
console-style table). Owner's own preference so far: version B.

| Stage | What | Status |
|---|---|---|
| Q1 | Fix the panel corner glitches (client meeting 2026-10-15) | Waiting |
| V0–V1 | Art direction "inside the console" (circuit board) with layout mock-ups; lighting and post (doc 30 C) | V0 board made 2026-10-17 (16 screens in A, B and C, with references); waiting for approval |
| T4–T5 | Table textures (any picture, repeat or fit) and paint layer, within the B5 budget; includes the console-style table textures (plastic body and mat) if approved | Waiting |
| V2–V5 | Pieces; table and room; menus and layout (V4a), readability (V4b), circuit-board skin (V4c); motion and feel | Waiting |
| MENU-1 | Organise all menus (owner, 2026-10-18: "right now everything is on the Table menu; sections should be easy to view and navigate"). Designed as a senior artist and designer would, in the approved V0 look, and shown for approval before any code. Goes with V4a (menus and layout) | Waiting; plan below |
| V6–V7 | Later visual stages (doc 30 C) | Waiting |

**MENU-1 plan (2026-10-18).** Today one Table menu holds about twenty rows of different kinds: starting and clearing
tables, play settings, the table's shape, your content, your profile and help. The plan:

- **Sort by what the player is doing**, not by when the feature was added. A first grouping to design from:
  - **Play** (this table): seat, hot seat, hands on table, rules card, hints, full feedback.
  - **Table:** New table…, Reset, Clear, shape, edge, Table tools…, Save table as a game….
  - **Workshop** (your content): cards, decks, card art, your games.
  - **You:** profile, tips, help.
- **One place, clear sections.** For example a menu panel with a section rail (icon and name) and the section's rows
  beside it, instead of one long list. The bar stays small: game chip, Undo/Redo, Shop, Menu.
- **The same row everywhere:** icon, name, current value or switch, a short hint; "…" only on rows that open a panel.
- **Design first:** the layout is drawn as a design board (on version B unless the owner picks otherwise), checked
  with the owner, and only then built into the Final UI prefabs (no UI built from code).

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
