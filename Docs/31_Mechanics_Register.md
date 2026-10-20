# 31 — Mechanics Register

Version 1.25 · 2026-10-20 · Status: living document. Updated after every playtest document (see "How playtest
updates work").

## What this is

One place that lists **every mechanic built so far**, each under its own fixed heading with a short ID (for
example `TF-MOVE`), so that playtest changes can be matched to the right mechanic without guessing.

- **TF-** headings are Trap Floor game mechanics.
- **P-** headings are platform mechanics, shared by every game.
- **PL-** headings are planned mechanics that are not built yet.

Each heading keeps the same parts, in the same order:

- **Status:** Built · Built, in playtest · Planned · Open (being decided) · Removed
- **Rule now:** what the mechanic does today, in plain words
- **Settings:** rule-set keys a house rule can change (doc 23 §5), if any
- **Source:** the document sections with the full detail
- **Open:** questions still being decided
- **Changes:** dated history, newest first

## How playtest updates work

1. **The playtest document uses the same headings** as this register, with the ID first, for example
   `### TF-TRAPS · Traps`. Under a heading, write what to **change**, **keep** or **remove**, in plain words.
   A heading that is not mentioned stays as it is.
2. **New things** that have no heading yet go under `## New`, one line each. They may name an ID for a new
   mechanic (for example `TF-SWAP · Swap places`).
3. **Checklist.** Anything that needs work goes under `## To do` as a checklist (`- [ ] …`). A checked item
   (`- [x] …`) means it is done.
4. **When a playtest document arrives, the assistant:**
   - finds each heading by its ID and updates that mechanic's **Rule now**, **Settings**, **Status** and
     **Open**;
   - adds a dated line to its **Changes** saying what changed and that it came from that playtest;
   - adds new mechanics under the right part (TF, P or PL), with the same parts as the others;
   - copies every to-do into **Playtest checklists** at the end of this register, under the playtest's date, and
     ticks off items that are now done;
   - lists anything it could not match or found unclear, instead of guessing;
   - then updates the game and work documents (doc 18 for Trap Floor rules, doc 27 for the work lists) where the
     change needs code or content work.
5. **IDs never change.** A removed mechanic keeps its heading with Status: Removed, so old playtest notes still
   match.

**Playtest document template** (copy for each playtest):

```
# Playtest — <date> — <game>
Testers: <names> · Players: <n> · Difficulty and win mode: <e.g. Hard · Team>

### TF-MOVE · Moving
Change: <what to change>
Keep: <what worked>
Remove: <what to drop>

### TF-TRAPS · Traps
Change: …

## New
- TF-<NEW-ID> · <name>: <what it should do>

## To do
- [ ] <task>
- [ ] <task>
```

---

## Part 1 — Trap Floor mechanics

### TF-PLAYERS · Players and seats
- **Status:** Built, in playtest
- **Rule now:** 2 to 4 players. Each screen plays one seat (as in Tabletop Simulator). Player colours: 1 blue,
  2 red, 3 green, 4 yellow. The two- and three-player seat layouts are still open.
- **Settings:** —
- **Source:** doc 18 §3, §15.11
- **Open:** exact two- and three-player seat layouts (OD-014).
- **Changes:** 2026-10-14 seats per screen (H1b).

### TF-SETUP · Setup and starting positions
- **Status:** Built, in playtest
- **Rule now:** the game is loaded from New table… (never at start-up). The floor is laid out face down, 6 × 6.
  Each pawn starts on a corner on its own side of the board: of the two corners on its seat's side, the one to
  the player's left (4 players: Blue bottom-left, Red top-left, Green top-right, Yellow bottom-right). The
  original 2d6 starting-position rule is replaced by these corners.
- **Settings:** `floor.placement` (random / pattern)
- **Source:** doc 18 §5, §15.11 (F3)
- **Open:** —
- **Changes:** 2026-10-14 starting corners worked out from seat positions (F3).

### TF-AVATARS · Avatars
- **Status:** Built, in playtest
- **Rule now:** each player chooses an Avatar card, which sits in the Console's Main slot and brings its own
  starting Ability: Clairvoyant → Dodge, Strategist → Check, Athlete → Rush, Engineer → Disarm. The Athlete also
  moves 3 tiles. Pairings are data, editable without code. Avatar cards are the large card size.
- **Settings:** `move.tiles.avatar.<Avatar ID>`
- **Source:** doc 18 §8, §15.9; doc 24 (card sizes)
- **Open:** Avatar stats beyond the move count.
- **Changes:** 2026-10-09 Avatar brings the starting Ability (replaces the per-mode count).

### TF-TURN · Turn order
- **Status:** Built, in playtest
- **Rule now:** on your turn, draw Controller Cards up to the hand limit (10, unused cards carry over), then
  either **move** or play an **Action** Ability instead of moving. Buying from the Shop does not replace moving.
  End Turn passes to the next player; the floor turn (collapse) comes between rounds. On Impossible, after the
  first player exits, the order becomes player → floor → player → floor.
- **Settings:** `controller.hand-size`
- **Source:** doc 18 §9, §15.2, §15.8
- **Open:** action economy beyond move-or-Action.
- **Changes:** 2026-10-09 move or Action (playtest 1.5).

### TF-MOVE · Moving
- **Status:** Built, in playtest
- **Rule now:** up to **2 tiles in one straight line** (up, down, left or right; never diagonal, never turning),
  counted from where the pawn stood when the turn started. The Athlete moves up to 3. Overcharged adds 1, Slow
  takes 1 away, Stuck allows no move, Blind allows only the rolled direction. Moves never cross a hole. Nothing is
  blocked: hints light the tiles in reach, and a move the rules do not allow does not flip the tile; the player
  can Undo it or choose **Keep move** if the table agrees.
- **Settings:** `move.tiles` (2), `move.tiles.avatar.<Avatar ID>` (Athlete 3)
- **Source:** doc 18 §15.2; doc 23 §9
- **Open:** —
- **Changes:** 2026-10-15 owner: 2 tiles in a straight line, Athlete 3 (was "about 3, flexible").

### TF-LANDING · Landing on a tile (free Search)
- **Status:** Built, in playtest
- **Rule now:** moving onto a face-down tile flips it for free and it takes effect at once for the player who
  landed (the "YOU LANDED ON" popup). The physical card stays face down until no pawn is on it, then turns over
  (or automatically when the turn ends). Only the active player's own pawn triggers it. The paid Search, Careful
  Search and Safe Search are removed.
- **Settings:** —
- **Source:** doc 18 §15.2, §15.4 (G2a)
- **Open:** —
- **Changes:** 2026-10-15 trigger now, turn later; 2026-10-15 Search code removed (G2d); 2026-10-09 Search is free.

### TF-FLOOR · Floor composition
- **Status:** Built, in playtest
- **Rule now:** 36 Floor Cards on a 6 × 6 grid: 18 Traps, 4 Friends (from the four Friend cards plus
  Overcharged), 6 Keys (Bronze, Silver and Golden, two of each), 1 Grand Exit, 1 Secret Exit and 6 Abilities
  (from Check, Rush, Dodge, Disarm, Shield, Push; repeats allowed). Each group is drawn at random. The floor card
  back changes each round (for example red, then blue, then yellow).
- **Settings:** `floor.count.<content set ID>` (the groups must still add up to 36)
- **Source:** doc 18 §4, §15.6, §15.9
- **Open:** the final Trap-type mix, after playtest.
- **Changes:** 2026-10-09 new composition (playtest 1.5).

### TF-PLACEMENT · Floor placement and difficulty patterns
- **Status:** Built (random); patterns proposed
- **Rule now:** chosen at setup: **Random**, the **difficulty pattern**, or another saved layout.
  - **Easy (guided):** no Fall, Spring or Crane next to a starting corner; an Ability within two tiles of each
    start; Keys in different quarters; the Grand Exit away from the corners; no edge Spring pointing off the grid.
  - **Hard:** random, but no Fall and no Key next to a starting corner.
  - **Impossible:** fully random.
- **Settings:** `floor.placement`
- **Source:** doc 18 §15.7, §15.9
- **Open:** the patterns are a proposal; every floor is placed at random today.
- **Changes:** 2026-10-09 proposal.

### TF-TRAPS · Traps
- **Status:** Built, in playtest
- **Rule now:** a Trap affects only the player who flips it, on that turn, once. A flipped Trap **waits** on the
  player, so a Reaction (Check, Dodge, Shield) can answer it. **Resolve Trap** applies it, and End Turn applies
  any still waiting. Chains: a Trap that moves a player onto a face-down card flips that card too (each card flips
  once).

  | Trap | Effect | Proposed count |
  |---|---|---|
  | Fall | eliminated for this Floor | 3 |
  | Slow | −1 movement next turn | 3 |
  | Sticky | Stuck: no move next turn | 3 |
  | Blind | next move only in a rolled direction | 3 |
  | Spring (▲ ▼ ◀ ▶) | launched one tile that way; off the grid or onto a hole = eliminated | 4 |
  | Crane | roll 2d6 (column, row) and go there; onto a hole = eliminated | 2 |
- **Settings:** `floor.count.<Traps set>`
- **Source:** doc 18 §15.5, §15.6 (G2b, G2c)
- **Open:** the final Trap mix.
- **Changes:** 2026-10-13 Spring, Crane, chains, waiting Traps (G2b–G2c); 2026-10-09 new Trap types.

### TF-SPECIAL · Friends and Overcharged
- **Status:** Built, in playtest
- **Rule now:** Friends are prizes that can be taken for free (TF-PRIZES). Overcharged gives the player who flips
  it +1 movement on their next turn (shown as OVERCHARGED); it cannot be taken.
- **Settings:** `floor.count.<Friends set>`
- **Source:** doc 18 §15.5, §15.10
- **Open:** what each Friend card does beyond being a prize.
- **Changes:** 2026-10-09 Overcharged joins the Friend pool.

### TF-ABILITIES · Abilities (Action and Reaction)
- **Status:** Built, in playtest
- **Rule now:** every Ability card is an **Action** (played on your turn instead of moving: Rush, Disarm, Push) or
  a **Reaction** (played on a waiting Trap: Check, Dodge, Shield). The card text says what it does.
  - **Check:** the Trap does not hurt anyone.
  - **Rush:** move over up to 3 spaces quickly, even over a hole.
  - **Dodge, Shield, Disarm:** exact text still open.
  - **Push:** push a player who shares your tile 2 tiles horizontally.

  Costs (any one of the inputs after "/"): Check `Up + A/B/X/Y`, Dodge `Down + A/B/X/Y`, Shield `A + B + X + Y`,
  Rush `Left/Right + A/B/X/Y`, Disarm and Push not set.
- **Settings:** `card.cost.<card ID>`
- **Source:** doc 18 §15.3, §15.9, §15.10
- **Open:** costs of Disarm and Push (and confirming Check and Rush); exact text of Dodge, Shield and Disarm.
- **Changes:** 2026-10-09 Check replaces Jump, Rush replaces Run; Safe Search removed.

### TF-PAYMENT · Controller hand and paying costs
- **Status:** Built, in playtest
- **Rule now:** the Controller inputs are Up, Down, Left, Right, A, B, X and Y (deck of 48: 6 of each). The hand
  holds up to 10. A cost is all or nothing, cannot be shared, and may repeat inputs or accept any one of several.
  To pay: choose the cards from your hand, place them in your Console, then **Confirm Payment** in the turn menu
  (Cancel Payment keeps the cards where they are).
- **Settings:** `controller.hand-size`, `card.cost.<card ID>`
- **Source:** doc 18 §9, §15.4; doc 22 §6
- **Open:** whether any game uses Start and Select (left out of the deck for now).
- **Changes:** 2026-10-09 "/" alternatives in costs (C1).

### TF-PRIZES · Taking prizes from the floor
- **Status:** Built, in playtest
- **Rule now:** on a face-up Ability or Friend under your pawn, the turn menu offers **Take …**. An Ability is
  paid by its cost (TF-PAYMENT) and played later by paying the same cost again; a Friend, or an Ability with no
  cost yet, is free. You may decline, and the next player who lands there may take it. The card goes to your hand
  (or beside your Console when the hand is full), and the tile becomes a hole. A prize never eliminates.
- **Settings:** `card.cost.<card ID>`
- **Source:** doc 18 §15.4 (G2a)
- **Open:** —
- **Changes:** 2026-10-12 implemented (G2a).

### TF-SHOP · The Shop
- **Status:** Built, approved
- **Rule now:** besides taking from the floor, players can buy any Ability of the game from the Shop (Actions
  first, then Reactions), on their own turn, at any time; buying does not replace moving. Off-turn the Shop can be
  opened to look. Payment is the same as for floor prizes. An Ability with no price is listed as "Price not set
  yet". The Shop entry is on the session bar, between Redo and Table.
- **Settings:** `shop.enabled` (applies at once)
- **Source:** doc 18 §15.4
- **Open:** —
- **Changes:** 2026-10-15 the market returns as the Shop.

### TF-KEYS-EXITS · Keys and Exits
- **Status:** Built, in playtest
- **Rule now:** collect the required Keys (Easy 1, Hard and Impossible 3) and bring them to the Grand Exit. When
  all required Keys are collected, roll 2d6 to find the Exit's position. **Grand Exit:** lets all players exit.
  **Secret Exit:** an instant win for one player for one round, once that player meets the Key requirement. On
  Hard, players may pass Keys to each other.
- **Settings:** `mode.keys-needed`
- **Source:** doc 18 §6, §15.8, §15.9
- **Open:** —
- **Changes:** 2026-10-09 Grand and Secret Exits.

### TF-ENDGAME · After the Exit is revealed
- **Status:** Built, in playtest
- **Rule now:** stepping on any Trap tile triggers it again, including Traps already flipped. Prizes can still be
  taken; a taken prize tile then acts as a hole.
- **Settings:** —
- **Source:** doc 18 §15.8, §15.9
- **Open:** —
- **Changes:** 2026-10-09 owner decisions.

### TF-COLLAPSE · Floor collapse
- **Status:** Built, in playtest
- **Rule now:** at intervals, 2d6 pick a tile (column, row) that becomes a permanent hole. Easy and Hard collapse
  each round (the floor turn). On Impossible there is no collapse until the first player exits; then the floor
  collapses at each player's turn. The hint shows the tile the dice point at.
- **Settings:** —
- **Source:** doc 18 §7, §15.8, §15.9; doc 23 §10
- **Open:** exact interval, rerolls and protection; what happens to a player standing on a collapsing tile.
- **Changes:** 2026-10-09 Impossible collapses only after the first exit.

### TF-ELIMINATION · Elimination
- **Status:** Built, in playtest
- **Rule now:** a player is eliminated by a Fall, or by a Spring or Crane sending them off the grid or onto a hole.
  Elimination lasts for the current Floor only: the player sits out the rest of that round and returns next round
  (in Survival too). Players lose together by running out of usable floor.
- **Settings:** —
- **Source:** doc 18 §6, §15.5, §15.9
- **Open:** the exact rule for "running out of usable floor".
- **Changes:** 2026-10-09 elimination lasts one Floor.

### TF-DIFFICULTY · Difficulty
- **Status:** Built, in playtest
- **Rule now:** **Easy:** bring 1 Key to the Grand Exit. **Hard:** all 3 Keys, by any means possible.
  **Impossible:** all 3 Keys; once a player exits, the floor collapses at each player's turn. Starting Abilities
  come from the Avatar (Hard adds none).
- **Settings:** `mode.keys-needed`
- **Source:** doc 18 §9, §15.8, §15.9
- **Open:** —
- **Changes:** 2026-10-09 split from win mode.

### TF-WINMODE · Win mode
- **Status:** Built, in playtest
- **Rule now:** chosen separately from difficulty (for example Hard · Team). **Team:** only one player needs to
  escape. **Survival:** every player must escape.
- **Settings:** `game.win-mode`
- **Source:** doc 18 §15.8
- **Open:** —
- **Changes:** 2026-10-09 separate choice.

### TF-ENDTURN · End Turn and waiting Traps
- **Status:** Built, in playtest
- **Rule now:** End Turn and Skip Turn first apply every Trap still waiting on the player, oldest first (only a
  Reaction prevents it). The button reads "(applies N)" and lists what it will apply, with "Apply and End Turn" or
  "Not yet". A Spring or Crane moves the pawn there (with a short hop), flips the target and applies any Trap on
  it. Everything End Turn applies is one Undo step. The turn strip shows effect chips: BLIND, SLOW, OVERCHARGED,
  STUCK, N TRAPS WAITING.
- **Settings:** —
- **Source:** doc 18 §15.5 (G2c), §15.11 (F3); doc 25 F3, F4
- **Open:** —
- **Changes:** 2026-10-14 warning and chips (F3).

### TF-HINTS · Trap Floor hints
- **Status:** Built, approved
- **Rule now:** while you hold your own pawn, glowing markers show the tiles you can reach. Over a tile the rules
  do not allow, a RULE reminder says why. Also: prize hints, waiting-Trap hints and the collapse target on the
  board. Game tips are made from the rules in play. All follow the Hints switch, except the rule check on
  landing.
- **Settings:** follows `move.tiles` and the other rules in play
- **Source:** doc 23 §9, §10
- **Open:** —
- **Changes:** 2026-10-15 H1–H3.

---

## Part 2 — Platform mechanics

### P-FREEFORM · Free play with physical pieces
- **Status:** Built
- **Rule now:** the game is played as at a real table: players move pieces, flip cards, roll dice and apply rules
  themselves. Game helpers are optional and never block a physical action. Select a piece by clicking; drag to
  move; **F** flips; the mouse wheel turns the selected piece 15°; right-click opens a piece's menu. Dragging a
  card onto a deck, stack, discard pile or Console slot puts it there; dragging out of a pile takes one. Fallen
  pieces are recovered beside the table.
- **Source:** doc 18 §2, §12; doc 22
- **Changes:** —

### P-COMPONENTS · Components and the Toolbox
- **Status:** Built
- **Rule now:** every piece comes from one component library (Base Box, Controller Box, one box per game). The
  Toolbox places them: cards (1 = a loose card, 2+ = a deck), decks, stacks, discard piles, pawns, tokens, dice
  (size chips), Consoles, board mats, and box tiles that place real game cards (the Controller Deck, a game's
  card sets, Avatars). Placement **keeps placing** until Esc or right-click. **Duplicate** is on every piece's
  right-click menu: cards, dice, pawns, tokens, Consoles, board mats, and now decks, stacks and discard piles
  (copied with all their cards). A duplicated card is an exact copy (the same card and face), a game's own cards
  included; a pawn, token or die from a game becomes a generic copy ("Duplicate as Generic …").
- **Source:** doc 22 §2–§13; doc 29 (L2-6b)
- **Changes:** 2026-10-18 a duplicated card copies the real card (owner); 2026-10-18 Duplicate on decks, stacks and
  discard piles (owner); 2026-10-16 keep placing until Esc or right-click.

### P-PILES · Decks, stacks, discard piles and hands
- **Status:** Built
- **Rule now:** decks shuffle and draw; a discard pile turns arriving cards face down; piles move and, when
  Toolbox-made and empty, delete. **Draw to Hand** with a count from a deck's menu. The Empty Table starts with a
  hand (switch in the Toolbox footer). **Shuffle** shows a quick riffle (two halves slide apart and drop back
  interleaved); **split, Split into, merge and draw** move the cards one after another along a small arc (SMOOTH-1).
  As in Tabletop Simulator (owner, 2026-10-18):
  - **Card on card:** while a card is carried over a loose card, a green stack outline shows under that card; let
    go and they become a stack at once where the bottom card lies (the cards glide together, a soft double tap, a
    green flash). Each card keeps its face. One Undo step. Trap Floor's floor tiles never stack.
  - **Landing:** a card landing on a stack or deck gives a light tap and a short green flash on that pile.
  - **One card left:** a stack left with one card turns back into a loose card; a stack left empty goes away. Game
    piles are never removed; a stack placed empty from the Toolbox stays until it has been used. A split that
    leaves a single card leaves it as a card. Undo goes straight back to the stack before.
  - **Stack menu:** right-clicking a stack or any card in it opens the STACK menu, the same pile actions as a deck:
    Inspect (the card clicked), Draw to Hand, Shuffle, Split Stack, Split into…, Duplicate, Move, Delete. A stack
    made by dropping cards on each other is an ordinary stack.
  - **Carry a whole stack** (owner, 2026-10-18; replaces the menu's Merge Into…): press and hold a card in a stack
    without moving (about half a second) and the whole stack lifts and follows the pointer (a quick drag still takes
    only the top card). Over another stack, that stack's green outline lights; let go there and the carried cards
    are merged onto it. Let go anywhere else and the stack is put down there. Escape puts it back. One Undo step.
  - **Undo and the table:** a card let go onto the table and its landing are one Undo step; Undo never puts a card
    back in mid-air.
  - **Split into…** (decks and stacks): choose 2 to 6; the cards are dealt into that many piles in a row beside it,
    the top part first, and the pile keeps the bottom part. One Undo step. A stack also keeps **Split Stack** (in
    half). Not on a game's assisted Controller Deck.
  - **Duplicate** (decks, stacks, discard piles): a copy with the same cards, order and faces, placed like any
    duplicate.
- **Source:** doc 22 §8, §11
- **Changes:** 2026-10-18 card on card, Split into…, Duplicate with cards (owner). 2026-10-18 fix: after a
  card-on-card stack, cards taken out of it stayed unclickable; fixed and checked live. 2026-10-18 stacking feel:
  outline while carrying, instant stack, taps and flashes, a one-card stack turns back into a card (owner).
  2026-10-18 fixes: stack forms where the bottom card lies; stack menu on its cards with deck actions (Draw to Hand,
  Shuffle); drop and landing one Undo step, no card in mid-air after Undo (owner). 2026-10-18: Merge Into… removed
  from the menu; press and hold a stack to carry it whole, let go on another stack to merge (owner); approved
  (owner, 2026-10-19).

### P-CONSOLE · The Console mat
- **Status:** Built, approved
- **Rule now:** a double-sided mat. The front has 12 symbol slots plus Main (Square ×3 top, Diamond ×3 bottom,
  Star left, Cross right, Circle, Triangle, Plus and Minus corners); the back has the controller mapping (A, B, X,
  Y take cards; the D-pad arrows take dice set to a number). Cards with a symbol snap magnetically to a slot with
  the same symbol. **Flip** (right-click or F over the mat) turns it over with the cards; **Duplicate** places a
  second Console with the other side up. In Trap Floor every slot but Main is an item slot. PAD-1: an arrow holds
  up to 3 dice (the first on it, the next ones beside it), for example 2d6; the dice stay there until the player
  changes them. A controller card put into a Console slot says what it means: "X = Heavy Kick" (the card on that
  button of the same Console's back, else of the nearest Console showing its controller side) or "Right = 2d6
  (4 + 3)" (the dice on that arrow of the Console showing its controller side). It never blocks play.
- **Source:** doc 22 §14; doc 32 (controller pad)
- **Changes:** 2026-10-15 C3d; 2026-10-20 PAD-1 (to be checked).

### P-BOARD · Game board mats
- **Status:** Built, approved
- **Rule now:** board mats are orange-framed grids of card spaces (10 × 5 by default) that join edge to edge into a
  bigger board. Right-click: Move, Columns ±, Rows ±, card or small spaces, Duplicate, Delete. A loose card that
  comes to rest on a space snaps to its centre, squared to the board. Trap Floor's board is a mat over its 6 × 6
  floor.
- **Source:** doc 22 §15
- **Open:** marked areas (deck, hand, discard) on mats; the client's modular mat system and game-specific mats
  (doc 30 §C0).
- **Changes:** 2026-10-15 C3e.

### P-SEATS · Seats, hands and hot-seat
- **Status:** Built
- **Rule now:** each screen plays one seat; its hand is the screen hand, and other hands lie face down at their
  owners' table edges with a card count. Table menu > **Seat** switches the seat on this screen (for testing).
  **Hot-seat** (off by default) covers the screen with "PASS THE SCREEN" and a summary when the turn belongs to
  another player. **Hands on table** puts your hand face up on your table edge instead of the screen tray.
- **Source:** doc 18 §15.11; doc 25 (H1b/H2, F3)
- **Changes:** 2026-10-14 H1b, H2, F3.

### P-UNDO · Undo and Redo
- **Status:** Built
- **Rule now:** Ctrl+Z and Ctrl+Y (and the session bar buttons) undo and redo table changes, with a tooltip naming
  the action and who did it. Turn steps that belong together are one Undo step. Seat switches, hints and viewer
  settings are not undone.
- **Source:** doc 18 §15.4, §15.5
- **Changes:** —

### P-RULES · Rules card and house rules
- **Status:** Built, approved
- **Rule now:** the rules card shows the game's rules (how to play, dos and don'ts, card numbers and components).
  Each line has **Change**: a proposed change is tried off the table, then every player presses their own
  **Agree**. It is saved on this computer as a named rule set ("House rules N") and marked on the card. Saved sets
  are picked on the card. Text changes show at once; settings apply at restart (the Shop switch at once).
- **Source:** doc 23 §3, §5, §7, §8
- **Changes:** 2026-10-15 R3.

### P-HINTS · Platform tips and the Hints switch
- **Status:** Built, approved
- **Rule now:** a hint strip explains platform pieces and actions for new players; Tips and Hints switches in the
  Table menu turn guidance off. The turn strip always shows whose turn it is, in their colour.
- **Source:** doc 23 §6, §10
- **Changes:** 2026-10-15 H1.

### P-FEEDBACK · Feedback
- **Status:** Built
- **Rule now:** banners for big moments (turn start with a ping, Traps, Keys, collapse, eliminations, wins), a
  message stack, board markers (active pawn ring, waiting-Trap warning, move targets and arrows, flip flash,
  collapse burst), a short camera kick or glance, pawn hops for automatic moves, and the "What happened" log
  (L). **Full feedback** switch: off keeps only the turn and round banners.
- **Source:** doc 25
- **Changes:** 2026-10-14 F1–F4.

### P-CAMERA · Camera
- **Status:** Built
- **Rule now:** the mouse wheel zooms (when nothing is selected); WASD, the arrow keys or a middle-button drag pan;
  views 1 2 3 4 are close, mid, board and top-down. A seat switch moves the camera to that seat.
- **Source:** in-game help card
- **Changes:** —

### P-ART · Card art and content packs
- **Status:** Built
- **Rule now:** card pictures come from the game, game packs, or your own pictures, looked up in that order. Card
  art is edited from the game selection menu (Make it yours).
- **Source:** doc 24 (A1–A4)
- **Open:** final art and correct PNG sizes (the art looks squished until then, doc 30 §C0).
- **Changes:** 2026-10-14 A2–A4.

### P-CARD-EDITOR · Card editor
- **Status:** Built
- **Rule now:** make or change cards for a game (front and back pictures, words, kind, cost), with a live
  preview; a card can be in several decks.
- **Source:** doc 24 (L2-1, L2-2)
- **Changes:** 2026-10-16 L2-2.

### P-DECK-EDITOR · Deck editor
- **Status:** Built
- **Rule now:** make or change decks (which cards and how many copies) for a game, including your own decks.
- **Source:** doc 24 (L2-3)
- **Changes:** 2026-10-16 L2-3.

### P-GAME-SELECT · New table (game selection)
- **Status:** Built
- **Rule now:** New table… lists every game with its picture, players, mode, rules and deck; choose the deck to
  play with (yours or the game's); Playing as; Make it yours (card art, card editor, deck editor); Start. Your own
  games are listed too.
- **Source:** doc 24 (L2-4, L2-5); doc 29
- **Changes:** 2026-10-16 L2-4, L2-5.

### P-PLAYER-GAMES · Save table as a game
- **Status:** Built, approved
- **Rule now:** set up an Empty Table, then Save table as a game in three steps: **1 Deck** (choose or make one,
  put it on the table), **2 Cards** (create cards in it or pick its cards), **3 Save** (name, players,
  description, written rules). Start lays the table out again with the game's deck shuffled where it lay. Save
  over and Delete are offered.
- **Source:** doc 29
- **Changes:** 2026-10-16 L2-6b steps.

### P-PROFILES · Player profiles and saving
- **Status:** Built
- **Rule now:** everything you make is saved in your profile on this computer. The profile panel (Table menu)
  plays as another profile, makes a new one or renames this one; switching starts a fresh table.
- **Source:** doc 28
- **Changes:** 2026-10-16 P2.

### P-TABLE · Table shape, size and edge
- **Status:** Built, approved
- **Rule now:** the Table menu's **Table** row cycles Classic → Rectangle → Square → Round → Oval → Star → other
  table models; **Edge** cycles None, Lip, Rounded rail, Wooden rail, Spikes, Neon. **Table tools…** opens Table
  mode: shape buttons, sizes (− / +, typed values), corners, star points and inner size, edge size, size handles
  dragged on the table (Alt for free sizes), Undo and Redo, Cancel (Esc) or Done. A new shape grows out of the old
  one. When the table shrinks, every piece at the edge moves in: cards, dice, pawns, tokens, decks, discard piles,
  stacks, Consoles (with their cards) and board mats (with the cards on them). Only another player's pile stops it.
  During a game the table never gets smaller than the classic area. Legs come from the base table model and stay attached. The
  choice is saved in the profile. The Table menu scrolls with the mouse wheel (or Up / Down) when it is taller than
  the screen (2026-10-20; the Table rows were off the bottom).
- **Source:** doc 30 §B6–§B10
- **Open:** picture cards for the shapes (V4). Seats along the edge: PL-CARTRIDGE (CART-1c, approved).
- **Changes:** 2026-10-18 every Toolbox piece moves in, game mats included (owner); 2026-10-17 T1–T3 approved.

### P-TABLE-MODELS · Table models
- **Status:** Built, approved
- **Rule now:** any table model, a client's included, can be used if it is made of three parts: playing area,
  sides and lower frame (legs as separate pieces). Console Cards > Table: **Split Table Model…**, **Check Table
  Model**, **Use Table Model As Scene Table**.
- **Source:** doc 30 §B7, §B8
- **Changes:** 2026-10-17 T1b.

### P-LOOK · Background and look
- **Status:** Quick wins in progress
- **Rule now:** a dark teal circuit-board skybox with faint bright-teal data lines, with bright-teal pulses running
  along them like electrical current (Console Cards > Look > Use Circuit Skybox). The full look (bright-teal lines, orange kept, retro but readable) comes with V0–V5.
- **Source:** doc 30 Part C, §C0
- **Open:** V0 style sheet with Russell.
- **Changes:** 2026-10-18 moving currents (owner); 2026-10-17 circuit skybox (Q2).

### P-PING · Table ping
- **Status:** Built, to be checked (UX-1)
- **Rule now:** Alt + left click, or a quick tap of the middle mouse button, anywhere on the table: a ring pulses at
  that spot in your colour for two seconds with a short beep, then fades. It only marks a spot: it never selects,
  moves or points at a piece, it is not saved, not in Undo and not in the activity log, and no game system reads it.
  Up to six rings at once. Not in Table mode. With multiplayer it becomes a small message to the others. With the
  **Ping tool** (tool bar) a plain click pings. The ring is a notched ring with a soft glow and a ripple when it lands;
  the cursor is a pin.
- **Source:** doc 30 §A2, Part H (TOOLS-1)
- **Open:** the optional A/B/X/Y picture on the ring (left out: keys 1–4 already change the camera view).
- **Changes:** 2026-10-19 built (UX-1); 2026-10-19 Ping tool, new ring and pin cursor (TOOLS-1).

### P-GESTURES · Cursor, flick and dice shake
- **Status:** Built, to be checked (UX-1)
- **Rule now:**
  - **Cursor:** an open hand over a piece; a closed hand while carrying; a stack mark while a press on a stack waits
    to pick the whole stack up; a red "no" over a pile that will not take the card; a target while Alt is held
    (ping); a cross-hair while Shift is held over a piece or a flick is aimed. Over menus, the normal arrow. 8-bit
    pictures made for the game.
  - **Flick:** hold Shift, press on a loose card, coin, token, pawn or die, pull back and let go. An arrow shows the
    direction and strength (teal soft, orange hard); the piece slides across the table, a die tumbles. A tiny pull
    does nothing; Escape cancels. One Undo step once it rests. Floor tiles and locked pieces never flick.
  - **Dice shake:** while carrying a die, hold Shift: it rattles in the hand; let go and it leaves tumbling (a real
    roll).
- **Source:** doc 30 §A1, §A2
- **Changes:** 2026-10-19 built (UX-1); 2026-10-19 the Flick tool flicks without Shift (TOOLS-1).

### P-FEEL · Carry, drop, hover, keys and sound (POL-1 to POL-8)
- **Status:** Built, to be checked (POL-1 to POL-8)
- **Rule now:**
  - **Carry:** the carried piece leans into the movement; the wheel raises or lowers it; an outline below shows
    where it will land (on the board space it snaps to).
  - **Turn:** Q / E turn the carried piece, the selection, the piece under the pointer or the selected piece by 15°
    (45° with Shift).
  - **Settle:** a card set on a board space glides there with a small spring; a pawn or token at rest on a board
    space or a loose card glides onto its centre unless another piece stands there.
  - **Hover:** an outline in your colour; after a moment a tooltip (name as it lies, a pile's count, a die's value,
    a pawn's owner); Alt held over a card shows it large.
  - **Piles:** 1–9 over a deck or stack draw that many (elsewhere 1–4 change the view); R shuffles; drops slide
    in; a carried stack follows smoothly and is pulled toward its merge target; Spread lays a pile out in rows.
  - **Dice:** R over a die rolls it (or the selected dice); shaking the mouse while carrying a die shakes it; the
    result floats up when it rests.
  - **Flick:** power curve, a charge sound that rises with the pull, a snap on release.
  - **Hand:** the tray hand is a curved fan or a straight row (FAN / ROW on the tool bar).
  - **Placing:** a placed piece makes the sound of its material; Shuffle plays a riffle sound.
  - **Messages:** ordinary action messages do not pop up (warnings and game news do).
  - **Selection:** drag one selected piece and the others come along; F flips, Q / E turn, Delete removes the
    Toolbox ones; grabbing an unselected piece drops the selection.
  - **Sound:** impacts by material and speed with random pitch; quieter when many play at once.
- **Source:** doc 30 Part H (POL-1 to POL-8)
- **Changes:** 2026-10-19 built (owner: all polishes in one go).

### P-TOOLS · Tool bar, keys strip and selecting cards
- **Status:** Tool bar accepted (owner, 2026-10-19); selecting every component to be checked (TOOLS-1b)
- **Rule now:**
  - **Tool bar** (bottom right): Grab (normal), Select, Ping, Flick, Draw (greyed until T5), Keys (show or hide the
    keys strip) and ? (help).
  - **Keys strip:** the keys for what you are doing right now; hidden with Keys or with Hints off.
  - **Box select with Grab:** a drag on the empty table draws the selection box (new box = new selection, Ctrl adds).
  - **Select** (or Ctrl held), as in Tabletop Simulator: every component except game boards and Consoles. Click a
    loose card, pawn, token or die to add or remove it; click a card in a deck, stack or discard pile to pick the whole
    pile; click the table to clear; drag a box to select everything inside (bags too). Never selected: boards and floor
    tiles, Consoles and what sits on them, hands, locked pieces.
  - **Group into deck** (button or G, two or more cards or piles): the selected cards and the cards of the selected
    piles go into the first selected deck, or a new deck where the first selected card or pile lay; emptied piles go
    away (not the game's own); pawns, tokens, dice and bags stay. One Undo step. The selection itself is not saved
    and not in Undo.
  - **Esc** with nothing in progress clears the selection, then goes back to Grab.
- **Source:** doc 30 Part H (TOOLS-1); client checklist item 6 (doc 32)
- **Open:** moving the whole selection together (POL-7).
- **Changes:** 2026-10-19 built (TOOLS-1); 2026-10-19 Select picks every component but boards and Consoles (owner, TOOLS-1b).

---

## Part 3 — Planned mechanics (not built yet)

### PL-SHUFFLE · Shuffle animations and styles
- **Status:** Built (SHUF-1, 2026-10-19), to be checked
- **Rule now:** a deck's or stack's menu has **Shuffle** (in your style), **Shuffle as...** (Riffle, Overhand, Hindu,
  Pile, Wash or Cut once, or **Set my default...**, kept per profile) and **Flourish...** (Fan, Spring, Ribbon
  spread, Cascade, Swivel cut: show only, the order stays). Each style is its own animation and sound; the new order
  always comes from the table's random source; **Cut** cuts once at a random point (at least one card each side), top
  part under. One Undo step. Quick Shuffle (R) stays the short riffle. Speed: Feel Settings › Shuffle Style Speed.
- **Source:** doc 30 Part H

### PL-DEAL · Dealing by the game's rules
- **Status:** Built (DEAL-1, 2026-10-19), to be checked
- **Rule now:** a deck's or stack's menu has **Deal (the rule)** and **Deal another way...**, or **Deal...** when the
  game has no deal rule (an Empty Table never has one); **T** over a pile deals too. The rule says how many each
  (or all, evenly), how many at a time, to their hands or the table, face up or down, and whether the game deals when
  it starts. Without a rule a small form asks (players, each, at a time, hands or table, face; a click on a line
  changes it). Players are dealt in turn order from the left of the dealer (this screen's seat). Cards on the table go
  into one stack per player (a third of the way from the seat toward the pile, or in a row in front of the pile on a
  table without seats); a table without players' hands lays each player's cards there. The cards fly out one after
  another, with a deal sound. One Undo step. A game's rules carry it as typed settings (`deal.count`, `deal.packet`,
  `deal.face`, `deal.to`, `deal.when`, doc 23 §5), so the rules card shows it and a house rule changes it; a game a
  player made sets it in the **Deal** step of Save table as a game (doc 29) and deals when it starts (asking how many
  play when it allows several).
- **Not covered:** dealing each round by itself (a game's turn helper would ask for that).
- **Source:** doc 30 Part H

### PL-ICONS · Icons on card sides and corners
- **Status:** Built (ICON-1, 2026-10-19), to be checked
- **Rule now:** a card can carry up to 8 icons (Square, Circle, Star, Cross, Diamond, Triangle, Plus, Minus), one at
  each side (top, right, bottom, left) and each corner, set in the card editor and drawn small on the card's front.
  A card fits every Console slot whose symbol is one of its icons (or its older single Console symbol) and is pulled
  into the nearest one; a card without icons fits any slot. In the slot the card turns so that the matching icon
  points outward on the mat: across the mat edge for an edge slot (half turns in a long slot, any quarter turn in a
  square one), out through the corner for a corner slot. A card matched only by its Console symbol keeps the slot's
  orientation. Icons are saved with your cards (new cards and changes to the game's cards, doc 28).
- **Not covered:** cards connecting to other cards (the owner chose Console slots only).
- **Source:** client feature checklist item 1 (doc 32); owner's choices 2026-10-19

### PL-FRAMES · Card frames per game
- **Status:** Built (FRAME-1, 2026-10-19, stand-in pictures), to be checked
- **Rule now:** the platform draws every card's border from its game's Card Frame Style (frame picture with a clear
  middle, colours, tick marks, side and corner tab shapes, how far tabs stick out). A game has a default frame and
  named variants; each card picks one in the card editor. A tab is drawn only where the card has an icon, sticks out
  past the card edge in the frame's colour and holds the shared symbol (PL-ICONS). Card art is the picture only.
  Picking up and stacking still use the plain card rectangle. A variant can turn the frame off (the icons then
  sit inside the card as small badges). A style can also carry its own symbol picture; empty uses the shared one.
  Frame variants are saved with your cards and with changes to the game's cards (doc 28).
- **Source:** owner, 2026-10-19 (Console card frames 13, 14, 36-39)

### PL-CARTRIDGE · Cartridge contract
- **Status:** Built, approved (CART-1a–1c, owner 2026-10-20)
- **Rule now:** what a game ("cartridge") must declare to plug into the platform, including where its seats may go
  on any table shape. CART-1a: the table talks to the cartridge in play for its table setups, its per-frame step, its
  status line and card, its Shop entry, its hint helpers, its own card menus and its reactions to cards played into
  Console slots. Trap Floor is the first code cartridge; an Empty Table, a game a player made and a game box without
  code are data cartridges (the status line names a player's game). CART-1b: the table's own code no longer names a
  game; a cartridge also brings its templates, its rules state and commands, its part of each Undo step, its seats
  and turns, its board tiles (look, menu, inspect text) and its feedback. CART-1c: a cartridge says where its seats
  may go: fixed, or along the edge. Trap Floor's seats sit at the table's edge on any shape (out along the line
  from the centre through their classic places, keeping order and facing, never nearer than the classic place);
  each seat's Console, piles, hand zone and the loose cards beside it move together, when a game starts or is reset
  and when the table changes during a game. On the classic table the seats are at home.
- **Source:** doc 30 §A4

### PL-MULTIPLAYER · Online multiplayer
- **Status:** Planned (to do only when the owner asks)
- **Source:** doc 30 §A6

### PL-SLS · Super Leroy Sisters
- **Status:** Planned (waiting for the game from Russell)
- **Rule now:** a second game, added here and in Tabletop Simulator, to measure the work per new game.
- **Source:** doc 30 §C0

### PL-TABLE-TEXTURES · Table textures and paint
- **Status:** Planned (T4–T5)
- **Rule now:** your own picture on the playing area (repeat or fit), tint, tile size and rotation, and a paint
  layer with brush, eraser and fill.
- **Source:** doc 30 §B3, §B5

---

## Playtest checklists

Each playtest document's to-do list is copied here under its date. Ticked items are done.

*(None yet.)*
