# Console Cards — Rules, House Rules and Hints

**Document ID:** 23_Rules_House_Rules_And_Hints
**Version:** 0.2
**Status:** Approved direction (owner, 2026-10-09). R1 and R2a implemented; R2b–R4 planned.

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
| R2b | Board mix and Avatars as data: floor groups with counts and random pools, placement method setting, new card definitions (Spring, Crane, Overcharged, Push, Check, Rush), each Avatar's starting ability | Planned |
| R3 | House rules: propose a change in game, every player agrees, the change is saved as a named set based on the current one, the card shows the difference, and saved sets can be picked in the start flow's Rules step | Planned |
| R4 | Hints engine built on the assistance layer: legal-move highlights and "why / why not" explanations for the active phase and rule set. Pings arrive with multiplayer (doc 10) as a separate social layer | Planned |

## 5. Setting keys (R2a, approved contract)

| Key | Kind | Scope | Feeds |
|---|---|---|---|
| `controller.hand-size` | Number | game | `ControllerConfiguration.MaximumHandSize` (draw limit) |
| `mode.keys-needed` | Number | per mode (mode filter) | `Mode.RequiredKeyCount` and its any-Key objective |
| `game.win-mode` | Choice: Team / Survival | all modes or per mode | `Mode.Behavior` |

A setting may carry a **display value** shown on the rules card in place of the raw value. Unknown keys and unreadable values are skipped with a console warning, and the authored value is kept.

## 6. Hints (R1 behaviour)

The Hints switch is a viewer setting. It is not table state, so Undo and Reset never change it. In R1 it switches guidance text only:

- the bottom-right controls strip (Help stays in the Table menu);
- the status card's phase help line;
- the Toolbox "Left-click place…" bar.

Lit drop targets and status messages are always shown, because play depends on them.

## 7. Open questions

- R3 agreement: unanimous, majority, or the host decides? (Owner to choose before R3.)
- Where saved rule sets live: per player profile, or shared with the table? (Depends on doc 11 persistence.)
