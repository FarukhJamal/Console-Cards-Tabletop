# 33 — Audit and Polish Plan (Console Cards / Trap Floor)

Version 1.1 · 2026-10-21 · Static audit of the code and docs (nothing was compiled or run).
Snapshot: branch `System-Implementations` (9e9258f) plus every stage committed after it (PLACE-1/2, D4-1, DICE-1, AVATAR-1/2).

Five audits were run side by side: (1) Trap Floor rules vs code, (2) player experience and UI, (3) the general game
system, (4) code health, (5) the physical table. This doc merges them into one list and one plan.
Severity: **Blocker** = stops play or gets stuck; **Major** = wrong or confusing behaviour a player will meet;
**Minor** = rough edge or cleanup.

---

## 1. The short version

You are right on both counts.

1. **Trap Floor feels unfinished because its rules were rewritten about six times in two weeks** (Search → Shop →
   Ability deck → hand-drawn deck; dice rolled by the game → by the player; elimination per round → per floor; one
   floor → four shrinking floors). Each rewrite added code; almost nothing old was removed, and the rules card and card
   texts were not brought into line. So players meet hidden steps, rules the game ignores, states that get stuck, and
   cards that do nothing.
2. **The UI hides the game.** Instruction messages are switched off by default, the turn panel has no main button,
   the Table menu has 20 rows, and many messages describe code ("SourceMustBeLoose", "run Console Cards > UI > …").
3. **There is no general game system.** The table platform (pieces, piles, Consoles, Undo, rules card, hints, packs,
   profiles) is good. But turns, prompts ("roll the dice now"), scores and winning exist only inside Trap Floor. A
   second game, or a player-made game, gets none of them.
4. **The code is in better shape than it feels.** No compile errors were found. Most problems are small, local fixes.
   About 2,700 lines are dead and can be deleted.

Nothing here needs a rewrite. The plan is: **unstick → cut → make the UI tell the player what to do → finish the
rules you decide on → polish text → then grow the general system one block at a time.**

---

## 2. Do these first (before any play-test)

Some features "do nothing" only because the Editor builders have not been run on the project:

| Menu | Adds |
|---|---|
| Console Cards > UI > Create Missing Gameplay UI Prefabs | the PLAYERS row in New table…, the Avatar button in Profile |
| Console Cards > Feedback > Create Missing Persona Prefabs (Final World) | `PersonaSet`, the seat avatar stand |

Then commit the generated assets and `.meta` files together with the code.

---

## 3. Top findings by area

### 3.1 Stuck states and blockers (fix first; no decisions needed)

| # | Sev | What the player meets | Source |
|---|---|---|---|
| S1 | Blocker | A payment popup left open blocks every other player. | A1 B2 |
| S2 | Blocker | Claim Key is only in the right-click menu of a tile that still looks face down, and it credits the on-screen seat, not the turn player. | A1 B1 |
| S3 | Major | A pending collapse leaks into the next player's turn; End Floor Turn can be pressed before the floor has collapsed. | A1 M1 |
| S4 | Major | After Undo during a collapse or Blind roll, rolling the dice does nothing until the turn menu is opened. | A4 R2c |
| S5 | Major | Picking a die up and putting it down gently counts as "rolled"; a bumped, leaning die that was not asked for makes the collapse wait silently. | A4 R2a/b |
| S6 | Major | Undo can switch itself off, and can bring back a piece floating in mid-air (drop A, grab B before A rests, undo). | A4 R4, A5 M1 |
| S7 | Major | Changing the player count or table can destroy card content the live table still uses (if the new table fails to start). | A4 R3 |
| S8 | Major | Crane uses dice values from an earlier throw. | A1 M2 |
| S9 | Major | Starting Abilities are dealt as loose cards, so Reactions are never offered for them. | A1 M3 |
| S10 | Minor | A Draw popup left open across a turn change can throw an exception. | A1 m7 |

### 3.2 Rules the game shows but does not do (need your decisions, §4)

| # | What | Source |
|---|---|---|
| R1 | Survival does nothing; the rules card mixes "Team" and individual wins. | A1 M4 |
| R2 | Impossible plays exactly like Hard. | A1 M5 |
| R3 | The 2d6 Exit roll and traps re-triggering after the Exit are on the rules card but not built. | A1 M6 |
| R4 | Drawing is hidden (right-click the deck), fills to 20 not 10, and Blind's "no draw" is not enforced. | A1 M7 |
| R5 | Ability buying is not enforced, and the assisted "draw 3 and buy" popup can no longer be reached. | A1 M8 |
| R6 | Friend cards do nothing; Disarm and Push are broken or have no cost. | A1 M9/M10 |
| R7 | Losing floors 1–3 costs nothing; a pawn on a collapsing tile stays put; running out of floor is not a loss. | A1 m2/m3 |
| R8 | The card backs never change (they were tied to the dead round system). | A1 M11 |

### 3.3 Player experience and UI

| # | Sev | What | Source |
|---|---|---|---|
| U1 | Major (near Blocker) | Instruction messages are hidden by default (`showActionMessages = false`), so "roll the two floor dice", "the die is leaning", "click to place" never show, and reminder buttons look dead. | A2 B-1 |
| U2 | Major | The turn panel has no main button: up to six equal buttons titled "ASSISTED ACTIONS", Skip and End side by side, Hide Hand mixed in. | A2 B-5 |
| U3 | Major | Turning Hints off also removes the "what to do now" line. | A2 B-7 |
| U4 | Major | Opening any right-click menu closes every other panel, including the payment popup and the editors. | A2 B-3 |
| U5 | Major | One screen for several players is off by default: the screen stays on Player 1 while a banner tells Player 2 "YOUR TURN". | A2 B-4 |
| U6 | Major | The Table menu has 20 rows mixing play, table building, editors, profile and help; it scrolls off screen. | A2 B-8 |
| U7 | Major | Messages show code: enum names, object IDs, "Match revision cannot advance", builder instructions. | A2 B-9 |
| U8 | Major | One thing, many names: Floorfall / Collapse Floor / Collapse / "rolls the floor"; Skip vs End; Hints / Tips / Help. | A2 B-6 |
| U9 | Major | "Reset to start" runs without asking; other destructive actions each ask differently. | A2 B-10 |
| U10 | Major | Key game actions (draw, claim Key) exist only by right-click. | A2 B-11 |
| U11 | Minor | Sandbox and developer tools (Seat switch, Toolbox, Arrange table) sit inside an official game. | A2 B-12 |
| U12 | Minor | The activity log and handover summary are flooded with UI noise. | A2 B-13 |

### 3.4 The physical table

| # | Sev | What | Source |
|---|---|---|---|
| T1 | Major | After clicking any piece, the mouse wheel turns that piece (one Undo step per notch) instead of zooming, until you click empty table. Most likely "the camera is broken" report. | A5 M2 |
| T2 | Major | Two different "selections": F and the wheel act on the last-clicked piece; Q/E/R/Delete act on the hovered piece. Delete over a hovered piece removes it without asking. | A5 M3 |
| T3 | Major | Shift means four things (flick, die shake, 45° turn, straight line). Holding Shift to turn a die makes it rattle. | A5 M4 |
| T4 | Major | A card over a Console slot teleports in and out every frame, shoving neighbours and losing the throw (breaks CLAUDE.md rule 2). | A5 M5 |
| T5 | Major | Moving 5 selected pieces takes 5–8 Undo presses; a die on the D-pad makes 3 steps. | A5 M6 |
| T6 | Major | A frozen (recovered) card can become the bottom of a new stack (breaks rule 10). | A5 M7 |
| T7 | Major | A card can apparently be dragged out of another seat's hidden hand (check in Play mode). | A5 M8 |
| T8 | Major | Typing "f", "h" or "l" in a text box flips a Console, folds the hand tray or opens the log. | A5 M9 |
| T9 | Minor | Stacks move by press-and-hold, decks only by right-click > Move. | A5 m1 |
| T10 | Minor | Right-drag on a piece opens its menu; placement mode turns off zoom; Table mode still lets you grab pieces and Undo game steps. | A5 m6–m8 |

### 3.5 Code health

| # | Sev | What | Source |
|---|---|---|---|
| C1 | Major | The seat avatars allocate memory every frame (breaks rule 5). | A4 R1 |
| C2 | Major | Trap Floor's turn, round and ability changes skip the command door; Undo for them is patched by hand. | A3 R1 |
| C3 | Major | Trap Floor's state (≈37 fields) lives on the generic table object, not in the table state or in the cartridge. | A3 R2, A4 §1.2 |
| C4 | Minor | ≈2,000 lines of the old round / Floormaster / Floorfall mode are never built but still ticked; 9 empty stub files; an old Toolbox view; the R0 rename tool; 19 unused PERSONA-1 images. | A4 D1–D6 |
| C5 | Minor | The Floorfall / ability-destination / neutralized tints go to card plates that are switched off; the board markers already show the same thing. | A4 D3 |
| C6 | Minor | `?.` on Unity objects in shutdown paths (cause of the known MissingReference errors on scene reload); debug logs on every Shop refresh; broad catch blocks that hide real errors. | A4 R5, R6, R10 |
| C7 | Minor | `PersonaAvatarView.tag` hides `Component.tag` (compiler warning). | A4 C1 |
| C8 | Minor | CLAUDE.md "Known issues" is out of date: 5 of 8 are fixed in code. | A4 §5 |

### 3.6 The general game system

| # | Sev | What | Source |
|---|---|---|---|
| G1 | Major | No shared turn, phase, prompt, score or win system. Hot-seat, the turn strip, avatar glow and turn hints work only in Trap Floor. | A3 R4 |
| G2 | Major | A game box with data but no code cannot be started ("cannot be played yet"). | A3 R5 |
| G3 | Major | The cartridge contract is Trap Floor-shaped (board tiles, Controller deck, Shop, one pawn per seat). | A3 R6 |
| G4 | Major | Player-made games are a separate model with no typed rules, house rules, hints, turns or win condition. | A3 R7 |
| G5 | Minor | The Core action enum, rule keys and mode fields use Trap Floor words (Keys, collapse, Team/Survival). | A3 R9, R10 |

**What is good and stays:** the physics carry and throw, card-on-card stacking, the container/Undo path, recovery
from falling, the Unity-free runtime, the command door, rules as data with house rules, packs and profiles, the
landing flip and popup, waiting traps, Spring, Reactions, prizes and payment, the shrinking floors, reach lights,
DICE-1, PLACE-1/2 and the avatars.

---

## 4. Decisions only you can make

These decide what gets built and what gets cut in Phase 3. My recommendation is first.

| # | Decision | Recommendation |
|---|---|---|
| D1 | **Win rule.** One rule for 1.0, or keep the Team/Survival switch? | One rule (Exits as written, individual escape); remove the switch. |
| D2 | **Impossible mode.** Define it now, or hide it? | Hide it for 1.0. |
| D3 | **Endgame.** Build the 2d6 Exit roll and trap re-trigger, or delete them from the rules? | Delete; keep pre-dealt Exits. |
| D4 | **Keys.** Claimed automatically on landing, or by a button? Carried by a player, or a team count? | Auto-claim on landing, carried by that player. |
| D5 | **Ability buying.** An assisted "draw 3, buy one" step, or the honour system? | Honour system, with a short HUD reminder; delete the unreachable popup. |
| D6 | **Drawing.** Add a "Draw to 10" row in the turn panel (Blind skips it)? | Yes. |
| D7 | **Friends, Disarm, Push.** Give them real effects and costs, or remove them for 1.0? | Remove Friends; fix Disarm and Push with costs. |
| D8 | **Floor rules.** Pawn on a collapsing tile; out of floor; cost of losing a floor. | Pawn is knocked out; out of floor = floor lost; a lost floor ends the game. |
| D9 | **Setup choices.** Avatar choice and placement patterns in 1.0? | Later; hide the placement row. |
| D10 | **Card backs.** Change per floor, per round, or never? | Per floor. |
| D11 | **Wheel.** Wheel always zooms (TTS), turning only with Q/E? | Yes. |
| D12 | **Cut the legacy round / Floormaster code** (≈2,000 lines)? | Yes. |
| D13 | **Hot-seat.** On by default when more than one player? | Yes. |

### Owner answers (2026-10-21)

| # | Answer |
|---|---|
| D1, D2 | One win rule (Exits as written, individual escape); the Team/Survival switch is removed; Impossible is hidden for 1.0. |
| D4 | Keys are claimed automatically when the turn player lands on one, and that player carries it. |
| D5, D6 | Honour system for Ability buying (HUD reminder; the unreachable popup is deleted); a Draw to 10 button in the turn panel, skipped under Blind. |
| D3 | Cut: the 2d6 Exit roll and the trap re-trigger come off the rules card; pre-dealt Exits stay. |
| D12 | Cut: the legacy round / Floormaster / Floorfall code. |
| D7 | Friend cards are **not** cut: their effects must be defined and built (Phase 3). Disarm and Push still need effects and costs. |
| D11 | Wheel change **not** chosen: the wheel keeps turning a clicked piece for now (T1 stays open). |
| D8, D9, D10, D13 | Still open: floor rules, setup choices, card backs, hot-seat default. |

---

## 5. The plan

Each phase is several small stages, each with your Play-mode check, as now.

### Phase 0 — Run the builders and unstick (no decisions needed)
1. Run the two builders (§2) and commit the assets.
2. S1 cancel an open payment at End Turn. S3 guard the pending collapse; End Floor Turn only after the collapse.
3. S4/S5 DICE-1: count a roll only after a real throw; a leaning un-asked die asks again; re-arm the wait after Undo.
4. S6 Undo: count in-flight pieces instead of one flag; never restore a piece in its held state; cancel only your own transaction.
5. S7 build new table content aside and swap it in only after success.
6. S2 part 1: Claim Key credits the turn player. S8 Crane waits for a fresh throw. S9 starting Abilities go to the hand. S10 guard.
7. C1 avatar allocations; C7 the `tag` warning.

### Phase 1 — Cut (after D1–D3, D12)
8. Delete the legacy round / Floormaster / Floorfall code, 9 stub files, the old Toolbox view, unused PERSONA-1 art (C4).
9. Remove Team/Survival, hide Impossible, delete the Exit roll and re-trigger texts, hide the placement row, merge Skip into End Turn.
10. Archive the 14 unused STAGE-03 trap assets (through the Editor so `.meta` files move).

### Phase 2 — Make the game tell the player what to do
11. U1 messages get an explicit kind: **Instruction** (always shown), **Event**, **Warning**, **Debug** (never shown).
12. U2/U3 turn panel: one NOW line (always on, not tied to Hints) and one main button chosen by state
    (Confirm payment → Claim key → Resolve trap → Roll floor dice → End floor turn → End turn); other actions under "…".
13. U4 a right-click menu no longer closes other panels.
14. U5/U9 hot-seat on by default; one confirmation pattern for Reset, New table and Clear.
15. T1 wheel zooms; T2 one target rule for every key (carried → selected → hovered); T8 typing guards.
16. U6 MENU-1: design board first (Play / Table / You / Advanced; editors move to a Workshop), then build through the builders.

### Phase 3 — Finish the game content (after D4–D10)
17. Keys as decided; Draw to 10 in the turn panel; Ability buying as decided.
18. Disarm, Push and costs; Friends removed or built.
19. Floor rules: pawn on collapsing tile, out of floor, lost floor.
20. Card backs per floor.

### Phase 4 — Text and polish
21. One text pass: rules card, the 6 Ability and 9 trap/special cards, all messages; one glossary
    (Floor turn, Collapse, End turn, Hints).
22. No code words on screen (error map, no IDs).
23. One-step floor turn; "revealed" badge on a held tile; log shows game events only.
24. Table polish: T3 Shift, T4 slot magnet without teleport, T5 one Undo step per gesture, T6 frozen pieces, T7 hidden hands, T9/T10.
25. Close or revert the six unconfirmed stages (MARK-1, ABIL-1, COLLAPSE-2, ABIL-DECK, HAND-1, AVATAR-2).
26. Full doc 18 §13 playable checklist at 4 players, then 2 and 3. Update CLAUDE.md known issues.

**Trap Floor 1.0 = Phases 0–4.**

### Phase 5 — The general game system (after 1.0, or in parallel once Phase 0 is done)
Seven building blocks, each pulled out of Trap Floor while it keeps working:

| Step | What you get |
|---|---|
| M1 Data-only games start | A game box with no code builds a table (decks, Consoles, rules card). Super Leroy Sisters and DLC unblocked. |
| M2 Turn engine (read-only) | Round, active seat and phase in the table state; turn hints, hot-seat, turn strip and avatar glow work in every game, including player games ("End turn" button). |
| M3 Trap Floor turns through the command door | Fixes C2; Undo and future multiplayer for turns. |
| M4 Prompts and actions | "Roll N dice", "Pick a tile", buttons as data. Trap Floor's three dice-wait flags become one prompt. |
| M5 One game HUD | Every game fills the same turn panel (Phase 2 layout). |
| M6 Counters and win/lose | Score per player; first to N, last standing, round limit, or manual. Keys become a counter. |
| M7 Seat and setup plans | Tables built from data; player games and official games share one setup format. |
| M8 Narrow module contract | Trap Floor state moves off the table object into its own module (fixes C3, G3). |
| M9 Player games join the one model | Typed rules, hints, turns and a win condition for player-made games. |

Not now: a card-effect scripting language, rule enforcement (stays advisory), a generic Shop, teams, networking, DLC code.

---

## 6. Play-test script (Editor, Play mode)

Run after Phase 0 and again before 1.0. Each line: what to do → what you should see. Note anything else.

**Setup**
1. Run both builders. New table… → Trap Floor, Easy, PLAYERS 4 → Start. → 4 Consoles, 4 avatars at the edge, a 6×6 floor, 2 d6 and a d4.
2. Change PLAYERS to 2 → Start, then back to 4. → Cards keep their art each time; no errors in the Console.

**A player turn**
3. Look at the turn panel. → It says whose turn it is and what to do now.
4. Draw your hand. → Note how you found it (today: right-click the Controller deck).
5. Pick up your pawn. → Reachable tiles light. Drop it on one. → The tile flips; the "YOU LANDED ON" popup opens.
6. On a trap: Resolve Trap, or End Turn with it waiting. → The End Turn warning, then the trap applies.
7. On a priced Ability: Take…, place cards, then right-click any piece. → Today the payment popup closes (U4). End Turn. → Today the next player is stuck (S1).
8. On a Key: find how to claim it. → Today only the right-click menu, and the wrong seat may get it (S2).

**The floor turn**
9. After the last player: Collapse Floor. → A message asks for the two dice. (If nothing shows, that is U1.)
10. Pick a die up and put it down gently. → It should still wait (S5).
11. Throw both dice. → A tile collapses. End Floor Turn → the next round starts.
12. Collapse Floor, then Ctrl+Z, then throw the dice. → It should still resolve (S4).

**Table and input**
13. Click a die, then scroll. → The camera should zoom (today it turns the die, T1).
14. Drop card A, grab card B before A rests, release, then Ctrl+Z twice. → Nothing floats (S6).
15. Drag a card across a Console slot. → It should glide in, not jump; neighbours stay put (T4).
16. Type "fhl" into a deck name box. → Nothing happens on the table (T8).
17. Throw a card off the table; carry another card over the recovered one. → No green stack outline (T6).
18. With 2 players and hot-seat on, drag from the other player's hidden hand. → It must refuse (T7).

**End of game**
19. Win or lose a floor. → Deal Next Floor; the board shrinks. Win the 3×3. → "YOU WIN THE GAME", Play Again works.
20. Stop Play mode. → No new errors (the two known MissingReference errors excepted).

---

## 7. Sources

Full audit reports (scratch, this session): `audit_1_trapfloor.md`, `audit_2_ux.md`, `audit_3_platform.md`,
`audit_4_code.md`, `audit_5_table.md`. Related docs: 18 (Trap Floor rules), 21 (principles), 23 (rules and hints),
26 (Final UI), 27 (work lists, MENU-1), 29 (player games), 30 (architecture), 31 (mechanics register).
