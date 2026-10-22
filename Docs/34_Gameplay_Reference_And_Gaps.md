# 34 — Trap Floor Gameplay Reference and Gap Check

Version 1.3 · 2026-10-22 · The owner's gameplay reference (2026-10-22), and how the current build compares to it.
The reference is the target for Trap Floor 1.0. It stays a **simulator**: the game shows, prompts and records; it
never forces a move. A player who does something out of turn is told so and asked to Undo.

---

## 1. The gameplay reference (owner, 2026-10-22)

Example: 4 players.

1. **Turn direction.** With more than 2 players, the players choose clockwise or anticlockwise.
2. **Turn start.** Everyone takes Controller Cards up to 10 and draws 3 Abilities from the Ability deck to buy.
3. **Player turn.** The player either moves or uses an Ability card. Landing on a face-down tile reveals it:
   - **Trap:** its effect is shown in a popup in the middle of the screen. The player must comply (no cheating).
     The game cannot check every effect yet, so it notifies the players. Effects are of two kinds, and the
     notification says which:
     - **immediate:** Spring, Crane, Fall;
     - **next round:** Blind, Slow, Sticky.
   - **Key:** a popup shows the Key with one option, **Take Key**.
   - **Ability:** a popup shows the Ability with **Take it** (which leads to buying) or **Reject**.
   - **Friend:** the same popup as an Ability, with no buying on Take.
4. **End of turn.** The player presses End Turn, or Skip Turn.
5. The other 3 players play the same way.
6. **Round end.** A message says the round ended and a tile collapses. Nothing else is shown.
   - Any player rolls the 2d6, and the game waits for the roll.
   - If someone tries to take a turn meanwhile, every player is told, and that player is asked to Undo. Nothing is
     forced.
   - After the roll, the game records the tile and highlights it. It then offers a **Collapse** button, or any player
     removes the tile and sets it aside face up.
7. **Repeat** until the players win or are all eliminated. A win counts even if only one player survives and wins.
8. **Next floor.** A popup says "Starting next phase" with an **OK** button. The new floor has one row and one
   column fewer, and play is the same.
9. **Last floor.** This continues down to 3×3. That floor ends the game, as a win or a loss.

## 1b. Difficulty and win mode (owner, 2026-10-22)

The difficulty and the win mode are two choices made at New table…. The difficulty sets what the exits need. The win
mode is optional (Regular is the default) and adds its own rules on top.

**Difficulty**
- **Easy:** bring 1 Key to the Grand Exit.
- **Hard:** bring all 3 Keys to the Grand Exit, by any means possible. The Keys count for the whole team.
- **Impossible:** all 3 Keys are required. Once a player escapes, the turn order becomes player, floor, player, floor:
  after every player turn a tile collapses. The players roll for it; it is never automatic.

**Win mode**
- **Regular (default):**
  - Grand Exit (the main exit): once the team has the Keys, the floor is won when every player still in stands on it.
  - Secret Exit (a bonus exit): a player holding a Key who lands on it escapes now and wins the floor at once.
- **Team:** only one player of the team needs to escape. The Grand Exit lets one player out with the team's Keys.
  The Secret Exit stays the bonus instant win for one player.
- **Survival:**
  - All team members must escape. Knocked-out players do not count.
  - An escaped pawn leaves the floor, and that player takes no more turns this floor.
  - The Grand Exit opens only with 3 Keys.
  - Once the Grand Exit tile is face up, stepping on any tile triggers it again, and prizes (Abilities, Friends)
    can no longer be taken.

This reverses doc 33 D1 (one win rule) and D2 (hide Impossible). D3 stands: there is no 2d6 Exit roll.

### Build status (2026-10-22, doc 27 row 44)

Stages TRAP-2, PRIZE (as PRIZE-3), TURN-2, ROUND-1, FLOOR-2, DIR-1, MODE-1, MODE-2 and IMP-1 are written and wait
for the owner's check (play-test script: doc 35). NOTIFY-1 is covered for these flows: their instructions always
show. CUT-2 waits for the owner. Not done: tiles set aside face up (G8), next-round effects re-shown as a popup at the
turn start (G3).

---

## 2. Already as the reference (keep)

| Step | Current build |
|---|---|
| 3 Move, flip on landing, "YOU LANDED ON" popup | Built (doc 31 TF-LANDING) |
| 3 Use an Ability instead of moving | Built (Action Abilities, played into the Console: "USE …?") |
| 3 Key popup with only Take Key | **Fixed today (KEYS-2)**. KEYS-1 claimed the Key at once, which closed the reveal popup |
| 4 End Turn and Skip Turn | Built (they do the same thing; Skip only logs "skipped") |
| 6 Wait for the 2d6, no button first | Built yesterday (COLLAPSE-3) |
| 6 Any player lifts the collapsed tile off | Built (COLLAPSE-2) |
| 7 Win with one survivor | Built (KEYS-1: Grand Exit checked after every End Turn; Secret Exit for one player) |
| 7 Tile under a pawn collapses: eliminated | Built (FALL-1) |
| 8–9 Shrinking floors to 3×3, the 3×3 ends the game | Built (FLOORS-1) |
| Advisory, never forced | Built for moves (Move not allowed + Undo / Keep move) |

---

## 3. Different from the reference (to change)

| # | Reference | Current build | Change |
|---|---|---|---|
| G1 | Players choose clockwise or anticlockwise (more than 2 players) | Fixed seat order | Add a Turn direction choice to New table… (and the rules card), with 3+ players |
| G2 | Turn start: Controller Cards up to **10**, draw 3 Abilities | Draw is hidden in the deck's right-click menu and fills to **20**. The "Draw 3 Abilities" button was removed (D4-1); the rules card asks players to do it by hand. Nothing prompts it | A turn-start prompt: "Draw up to 10 · Draw 3 Abilities", with buttons that do it (Blind skips the draw). Fix the limit to 10 |
| G3 | Trap popup: comply. It says whether the effect is **immediate** or **next round** | The popup offers Resolve Trap **or Return to Tabletop** (the trap then waits until End Turn). The text says "Assisted consequence: …" and does not say when the effect happens | Effect popup: the effect in plain words, a tag **NOW** (Fall, Spring, Crane) or **NEXT ROUND** (Blind, Slow, Sticky), and one **Apply** button. A next-round effect shows again at that player's next turn start |
| G4 | Ability popup: **Take it** (buying) or **Reject** | Popup shows only "Return to Tabletop"; Take is in the turn panel | Ability popup with Take it (opens the payment) and Reject |
| G5 | Friend popup: Take (no buying) or Reject | As G4; Friends also have no effect yet | Friend popup with Take and Reject |
| G6 | Round end: one message, "Round ended: collapse a tile" | A FLOOR TURN with a named **floor operator** who changes each round. The menu shows Collapse Floor and End Floor Turn | "ROUND n ENDED — roll the 2 floor dice to collapse a tile." **Any** player may roll. Drop the operator from the player-facing text |
| G7 | Someone takes a turn during the collapse: warn everyone, ask to Undo | A pawn moved during the floor turn is ignored silently | Warning popup: "The round is over — roll the floor dice first. Press Undo to take that move back." |
| G8 | After the roll: **highlight** the tile, then a **Collapse** button, or a player removes it face up | The roll collapses the tile **at once** (no highlight, no button). End Floor Turn sets it aside **as it lies** (face down if unrevealed) | After the roll: highlight + "Collapse (x,y)" button. A player lifting the tile off also collapses it. Set aside **face up** |
| G9 | Next floor: "Starting next phase" + OK | Popup "FLOOR n WON/LOST" with a "Deal Next Floor (5×5)" button | Rename to "Starting next phase" / OK (keep the won or lost line) |
| G10 | One clear notification per event | Many instruction messages are hidden by default (Feel › Show Action Messages off); others show as small toasts | A notification system with three kinds: **centre popup** (trap effect, Key/Ability/Friend, round end, next phase), **banner** (turn start), **toast** (warnings). Instructions never hidden |

---

## 4. In the build but not in the reference (keep or cut?)

| # | Mechanic | Where | Suggested |
|---|---|---|---|
| X1 | **Reactions** (Check, Dodge, Shield): stop a trap when it hits | Ability deck, popup "Use a Reaction…" | Owner decision. The reference says "comply", with no Reactions |
| X2 | **Shop** (buy any Ability by its price) | Session bar, off by default | Cut for 1.0 (the Ability deck replaces it) |
| X3 | **Starting Ability** from each Avatar (a loose card beside the Console) | Setup | Owner decision |
| X4 | **Overcharged** tile (+1 move next turn) | Floor mix | Owner decision |
| X5 | **Two exits** (Grand Exit: all still in with the team's Keys; Secret Exit: one player with a Key) | Floor | Owner decision. The reference says only "win" |
| X6 | **Keys needed per mode** (Easy 1, Hard 3); Keys count for the team | Modes | Owner decision. The reference says Keys are taken by the player |
| X7 | **Floor operator** (who must roll) | Turn tracking | Cut from the player-facing text (G6) |
| X8 | **Hand comfort cap 20** | Controller deck | Cut; 10 (G2) |
| X9 | **Team/Survival, Impossible, 2d6 Exit roll, trap re-trigger** | Rules card | Already cut or hidden by decision (doc 33 D1–D3) |
| X10 | **Legacy round / Floormaster / Floorfall code** (~2,000 lines, never built) | Code | Already decided: delete (doc 33 D12) |
| X11 | **Collapse Floor / End Floor Turn buttons** | Turn panel | Keep End Floor Turn ("Next round"); Collapse becomes the post-roll button (G8) |
| X12 | Taken prize "not offered again while you stay on it" | Turn menu | Fold into G4/G5 (Reject) |
| X13 | **Card backs per round** | Floor | Keep; harmless |
| X14 | **Draw 3 Abilities** assisted popup code (unreachable since D4-1) | Code | Revive for G2/G4, or delete |

### Owner answers (2026-10-22)

| # | Answer |
|---|---|
| X1 | **Keep Reactions.** The trap popup also offers Use a Reaction when the player holds one. |
| X7 | **Cut the floor operator.** Any player rolls the collapse. |
| X2, X3, X4 | **Keep for now:** Shop (stays off by default), Avatar starting Ability, Overcharged. |
| X5, X6 | **Keep both exits as now:** Grand Exit and Secret Exit; a lone survivor wins. |
| §5 | Build in the order of §5, starting with NOTIFY-1. |

---

## 5. Proposed stages (each checked by the owner in Play mode)

1. **NOTIFY-1:** the notification system (G10): centre popup, banner and toast, with instructions always shown.
   Every later stage uses it.
2. **TRAP-2:** the trap effect popup with NOW / NEXT ROUND and one Apply button; next-round effects shown again at the
   turn start (G3).
3. **PRIZE-2:** Ability and Friend popups, Take it / Reject (G4, G5).
4. **TURN-2:** the turn-start prompt (draw up to 10, draw 3 Abilities) and the limit set to 10 (G2, X8).
5. **ROUND-1:** round end and collapse: one message, any player rolls, out-of-turn warning, highlight then Collapse,
   set aside face up (G6, G7, G8, X7, X11).
6. **FLOOR-2:** "Starting next phase" + OK (G9).
7. **DIR-1:** turn direction choice (G1).
8. **MODE-1:** a WIN MODE row in New table… (Regular, Team, Survival). Team and Survival escapes: one escape wins,
   or every player still in escapes, with escaped pawns leaving the floor.
9. **MODE-2:** Survival extras: Grand Exit needs 3 Keys; once it is face up, tiles trigger again and prizes are off.
10. **IMP-1:** Impossible: after the first escape, a floor turn after every player turn; Impossible shown again.
11. **CUT-2:** the cuts the owner approves from §4.
