# Console Cards — Rules, House Rules and Hints

**Document ID:** 23_Rules_House_Rules_And_Hints
**Version:** 0.8
**Status:** Approved direction (owner, 2026-10-09). R1, R2a, R2b, C1, R3 and R4 implemented (Trap Floor G2a applies the revised turn). Pings wait for multiplayer.

> **Contract note:** type names and field lists here are illustrative unless labelled **Approved Contract**. They describe the direction agreed with the owner, not fixed public APIs.

## 1. Owner intent

- Rules are **never forced** on players. They are shown so players can play the game.
- Every game ships with **default rules** (its game box). They can be edited and updated later.
- When players **agree** to change a rule, the change must **appear visually** to everyone and be **saved as a new rule set** that can be picked from a list.
- **Hints** are visual clues for new players. Other players can also give clues (pings). For hints to explain play, the system must understand the game better than the players do.

This follows doc 09: *freedom by default, enforcement by configuration*. Rules sit at the **Free** or **Assisted** level unless a game explicitly configures more.

## 2. How it works today (verified 2026-10-09)

- **Rules are assistance, not enforcement.** Trap Floor's runtime says so ("It never restricts freeform tabletop actions"), and its game definition says assistance "must not block freeform actions". Players can always move, draw, flip and place anything by hand.
- **Progression is optional.** The round loop (Start → Search → Trigger → Floorfall → End, 10 rounds) and the turn helper record progress and offer assisted actions. They never move pieces on their own.
- **Rule logic lives in code.** Each rule is a Trap Floor runtime service (round orchestration, turn tracking, reveal/search, Floorfall, collapse, objective, abilities, Floormaster lifecycle). Only some values are data:
  - **Mode:** keys needed, starting abilities, collapse schedule.
  - **Game:** search costs, as a configuration string; hand size and payment options.
  - **Floor content:** each card's effects.
- **Hints today** are pieces with no rules model behind them: lit drop targets while dragging, the status card's phase help line, and status messages.

## 3. Model

### 3.1 Rule set (R1, implemented)

A **rule set** is a game's rules as data (`RuleSetDefinition`):

- name, stable ID, the game it belongs to, and **based on** (empty for the default set);
- **sections** (heading, numbered or not) of **rule entries**.

A **rule entry** has:

- a stable ID;
- player text, which may contain `{value}`;
- an optional **typed setting**: kind (number, on/off, choice, text), key (for example `trap-floor.rounds`) and value;
- an optional **mode filter** (for example one goal line per difficulty). An empty filter means every mode.

The **default rule set** is referenced by the game definition (`GameDefinition.DefaultRuleSet`) and is never overwritten.

**Changed lines.** `RuleSetComparison.IsChanged` compares an entry with the same entry in the set it is based on. It counts as changed when it is new, or its text, kind or value differs. The rules card marks changed lines and shows a HOUSE RULES badge for any set that is not a default.

### 3.2 Different games, one rules system

Each game supplies its own entries and setting keys. The platform only knows how to show, compare and (later) edit entries. Rules refer to pieces by **catalog / definition IDs** (doc 22), for example "Key cards" or "Controller Cards A/B/X/Y". So one system serves games with different cards, pawns and tokens.

### 3.3 Two kinds of rules

- **Settings rules** carry a typed setting the game's assistance reads (R2). Changing one changes what the assistance does.
- **Table rules** are text only. They are shown and agreed on, but the assistance cannot follow them, so the card labels them as enforced by the players.

## 4. Stages

| Stage | Content | Status |
|---|---|---|
| R1 | Rule set data model; Trap Floor default rules drafted from the code; rules card (left column, foldable, changed-line marks, house-rules badge); Table menu switches for Rules card, Hints and Help; developer wording replaced with player wording | Implemented |
| R2a | Rule settings feed the game data before the template is built (`GameDefinition.ToData(ruleSet)` → `RuleSettingsApplication`): hand size, Keys needed per difficulty, win mode (Team / Survival, separate from difficulty). Default rule set rewritten for the revised Trap Floor (doc 18 §15) | Implemented |
| R2b | Board mix and Avatars as data: floor groups (content sets with role `floor`, a draw count and optional repeats), floor count and placement settings, new card definitions (Spring ×4, Crane, Overcharged, Push), Action/Reaction text on every Ability, each seat's Avatar and its starting ability, a Setup section on the rules card | Implemented |
| R3 | House rules: propose a change from the rules card, every player agrees, the change is saved on this computer as a named set, the card shows the difference, and saved sets can be picked on the card (§8). The start flow's Rules step comes with the game selection menu (doc 27, List 2) | Implemented |
| R4 | Hints for moving (§9): the tiles in reach light while the active player holds their pawn, a longer move is explained with "Undo move" offered, and the turn help names the move count. Pings arrive with multiplayer (doc 10) as a separate social layer | Implemented (pings later) |

## 5. Setting keys (R2a–C1, approved contract)

| Key | Kind | Scope | Feeds |
|---|---|---|---|
| `controller.hand-size` | Number | game | `ControllerConfiguration.MaximumHandSize` (draw limit) |
| `mode.keys-needed` | Number | per mode (mode filter) | `Mode.RequiredKeyCount` and its any-Key objective |
| `game.win-mode` | Choice: Team / Survival | all modes or per mode | `Mode.Behavior` |
| `floor.count.<content set ID>` | Number | all modes or per mode | How many cards the setup draws from that floor group (R2b). Passed through as `GameDefinitionData.RuleSettings` |
| `floor.placement` | Choice: random / pattern | all modes or per mode | Placement method (R2b). Passed through; until G2 every floor is placed at random |
| `move.tiles` | Number | all modes or per mode | How many tiles a player may move on their turn (R4, default 3). Passed through; the hints read it |
| `card.cost.<card stable ID>` | Text, in card cost notation | game | That card's input cost (C1). Replaces the authored cost in the game data, so the purchase popup, payment check and inspect text all use it. Empty means no cost yet |

**Card cost notation (C1).** Parts are joined with `+`. A part lists one or more inputs separated by `/`, and any one of them pays it. `x2` after a part repeats it. Examples: `Up + A/B/X/Y` (an Up card and one of A, B, X or Y), `Left/Right + A/B/X/Y`, `A + B + X + Y`, `Right x2 + A`. Input names are Up, Down, Left, Right, A, B, X and Y, in any letter case. A cost that cannot be read is skipped with a warning and the card keeps its authored cost. Card assets store the same thing: each requirement has an input, optional `alsoAccepts` inputs and a count.

**Pass-through settings** are checked when the rule set is applied and then kept on the game data (`GameDefinitionData.TryGetRuleSetting(key, mode)`) for the game's setup to read. A floor count for a group the game does not have is skipped with a warning. If the floor groups no longer add up to the grid, the setup reports it and does not build; R3's rule editor will check the total before a house rule is saved.

A setting may carry a **display value** shown on the rules card in place of the raw value. Unknown keys and unreadable values are skipped with a console warning, and the authored value is kept.

## 6. Hints (R1 behaviour)

The Hints switch is a viewer setting. It is not table state, so Undo and Reset never change it. In R1 it switches guidance text only:

- the bottom-right controls strip (Help stays in the Table menu);
- the status card's phase help line;
- the Toolbox "Left-click place…" bar.

Lit drop targets and status messages are always shown, because play depends on them.

**Whose turn (UX-1).** The turn strip shows a chip with the pawn icon in the turn player's colour (Player 1 blue, 2 red, 3 green, 4 yellow), and the turn text names the colour, for example "PLAYER 1 (BLUE) TURN". During the floor turn the chip shows the player who rolls. If another player's pawn is dropped on a face-down tile, a status message says whose pawn flips tiles. These are always shown, like status messages.

## 7. Decided questions (owner, 2026-10-15)

- **Agreement:** everyone agrees. Each player presses their own Agree button; one "No" (or closing the panel) keeps the rules.
- **Where saved sets live:** on this computer, one file per set in the game's content folder (`Content/<game>/rules/<id>.json`, next to its art packs, doc 24). Shared tables (doc 10) can send the same file later.
- **Where rules are changed:** from the rules card.
- **What rules cover (owner, 2026-10-15):** how to play, dos and don'ts, card numbers and components used. Not where cards go on the Console or controller; players work that out.

## 8. House rules (R3, implemented)

**On the rules card.**

- Each rule line has a **Change** button.
- When saved sets exist, a **Rule set ‹ ›** row picks another set. Switching is proposed to the table like a change.
- When the rules in play have different settings from the ones the game was set up with, a mustard row says so and offers **Restart**. Restart loads the game again with these rules, after the usual "Replace current table?" question.

**Proposing a change (the Change a rule panel).**

1. The panel shows the rule now and the proposed rule as it will read.
2. Numbers step with − and + (never below 0). On/off settings flip, keeping their words (true/false, on/off, yes/no). Choice settings step through their listed choices (`RuleEntry.choices`). Other settings, and text-only rules, are typed in the text box.
3. **Propose to the table.** Nothing changed: nothing is proposed. An empty text rule is refused. A changed setting is first tried by setting the game up once, off the table. If the setup fails (for example floor counts that no longer fill the grid), the reason is shown and nothing is proposed.
4. Every player at the table presses their own **Agree** button. Once all have agreed:
   - A new set "House rules N" is saved. It is based on the game's default rules and carries every change already in play plus this one. Changing a rule back to its default drops that change, and with no changes left the default set is back in play.
   - The new set is in play at once. Text shows on the card immediately, with changed-line marks and the HOUSE RULES badge.
   - Settings (hand size, Keys, floor counts, costs, and so on) take effect when the game restarts.
5. If the file cannot be written, the set still lasts until the game is closed, and a message says so.

**Saved file.** `HouseRuleSetRecord`, format 1: stable ID, name, game, the set it is based on, creation time, and the changed entries (entry ID, plus new text and/or value). Files are read once at start. A file that cannot be read, or one made from other rules, is skipped with a console warning.

**Pieces.**

- `RuleSetDefinition.CreateHouseRules` (a runtime copy that is never an asset) and `RuleEntry.WithChange`.
- `HouseRulesStore`.
- `RulesCardModel.WithActions` with `RulesCardActions`.
- `RuleProposalPanelView` (Final UI prefab `RuleProposalPanel`, catalog ID `platform.rule-proposal-panel`, modal layer).
- The Change button and the rule-set and restart rows. Create Missing Gameplay UI Prefabs adds these to the existing rules prefabs and changes nothing else.

The rule set in play is a table setting kept by the game, not table state: Undo and Reset do not change it.

## 9. Hints for moving (R4, implemented)

Owner choices (2026-10-15): reach lights only while dragging your own pawn; a longer move is explained and Undo is offered; the move count is a rule setting.

**How far.** The Trap Floor rule "Move up to {value} tiles" now carries the setting `move.tiles` (default 3), so a house rule can change it. For the active player this turn:

- Overcharged adds 1, Slow takes 1 away (never below 0);
- Stuck allows no move;
- Blind allows only the straight line in the rolled direction (before the roll, nothing lights and the status card asks for the roll).

Steps are counted orthogonally from where the pawn stood when the turn started, never across a hole, so moving 2 and then 2 more counts as 4.

**Reach lights.** While the active player holds their own pawn, the tiles they can still reach this turn light green. Ability and Trap destinations (cyan) take priority, and no reach shows while one is shown. The lights go when the pawn is put down.

**Why not.** When the active pawn lands further than allowed, across a hole, off a Blind line, or while Stuck, a warning says why. For example: "That is 4 tiles from where the turn started; the rules allow 2 (3, SLOW -1)." The turn menu offers **Undo move (too far)** while that move is still the latest step. The move itself is kept: nothing is blocked. Moves made while an Ability or Trap shows where to go (Rush, Dodge, Spring, Crane) are not counted as regular moves for the rest of that turn.

**Turn help.** The status card's help line says "Move up to N tiles" with the count for this turn.

All of these hints follow the Hints switch in the Table menu.
