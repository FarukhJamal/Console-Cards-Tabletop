# Console Cards - Trap Floor Game Requirements

**Document ID:** 18_Trap_Floor_Game_Requirements
**Version:** 1.6
**Status:** Approved with Open Decisions
**Authoritative source:** User-approved Console Cards System & Games direction supplied 2026-09-21, superseding conflicting historical Trap Floor direction
**Purpose:** Define the current approved minimum Game-specific direction for Trap Floor while keeping provisional design values open and preserving manual tabletop play.

> **Revision 1.5 (2026-10-09, owner):** the first playtest changes in §15 are approved and supersede the conflicting parts of §4 (Search), §6 (Trap consequences), §9 (buying Abilities) and §10 (Floor counts). Where §15 and an earlier section disagree, §15 wins.

## 1. Authority and Supersession

The approved Game name is **Trap Floor**.

This revision supersedes the former Trap Floor build based on a shared 50-coin pool, a 36-Card Floormaster's Deck composed of 14 Trap, 14 Coin, and 8 Item Cards, a fixed 10-round loop, and the associated Floormaster draw/discard lifecycle. Those elements may remain in the current prototype or implementation history, but they are outdated design reference and are not authority for future Trap Floor content or rules. It also supersedes the previous prohibition on authored Modes, Controller hand configuration, Ability/Action Card input costs, and Console Side Slot configuration.

The latest Russell/Milanote direction expressly uses Keys and an Exit as Trap Floor concepts. Earlier documentation that excluded all Key/Exit concepts as belonging only to **Trap Door** is superseded. The obsolete **Trap Door** terminology, sequential Level Deck, dungeon/room progression, enemies, and reveal-next-Level-Card loop remain non-authoritative unless separately approved later.

The latest Milanote card-set structure is the current content reference. Exact Card counts and detailed content remain provisional wherever this document does not explicitly confirm them; do not recover old counts from earlier documents or implementation.

This document is the authoritative Trap Floor direction. `Consolecards_LayoutRef_doc.pdf` remains authoritative for shared layout and interaction requirements and for Super Leroy Sisters where not superseded by a later approved decision.

## 2. Platform Boundaries

- The Console is universal.
- The Trap Floor Game Board is Game-specific.
- The Game Template defines starting setup, content, and layout.
- Game-specific rules remain outside the generic Game Template schema.
- Trap Floor Game Rules are primarily interpreted and enforced by Players through Freeform Actions.
- Game-specific automation is optional assistance and must not own or disable generic physical actions.
- Runtime State remains authoritative during a Match.
- Trap Floor is loaded through the Platform's Game Template flow and must not be forced automatically at application startup.
- Trap Floor uses the Platform's generic component types; its Game Template does not own or redefine them.
- The wider Console Cards Player Layout model remains structurally capable of one to eight Players; Trap Floor itself supports two to four Players.
- Central gameplay remains the primary visual focus.

## 3. Player Count

Trap Floor supports **two to four Players**.

The exact authored Seat mappings for two and three Players remain unresolved under OD-014. The confirmed four-Player layouts do not authorize inventing those missing mappings.

## 4. Game Board and Floor Cards

- The current Trap Floor Game Board/Play Area is a `6 x 6` grid made from **36 Floor Cards**. Grid dimensions are Game-authored data; `6 x 6` is not a Platform constant.
- Floor Cards are Board tiles, not a drawable sequential Level Deck.
- Each Floor Card occupies a stable X/Y grid coordinate.
- Players may share the same Floor tile.
- Movement is orthogonal only.
- Normal movement remains flexible and player-judged, generally no more than three tiles. The exact movement value and any Avatar-specific variation remain provisional.
- **Search** means flipping/revealing the Floor Card being interacted with.
- A revealed Floor Card may expose a Key, Trap, or other effect according to the current card set.
- Search does not itself collapse a Floor Card.

## 5. Setup Dice and Starting Position

- Setup uses `2d6` to determine a starting position on the `6 x 6` Floor grid.
- The two d6 are first-class generic Platform Die Object Instances. Trap Floor interprets their accepted values as a grid coordinate; it does not introduce a Trap Floor-specific Die type.
- Exact setup resolution details beyond the `2d6` coordinate remain provisional unless supplied by current readable Game content.

## 6. Objective and Loss Conditions

- The objective is to find all required Keys and bring them to the Exit.
- The selected Mode supplies the required-Key count: Easy requires 1; Hard and Impossible require 3.
- Players lose by failing a Trap or by running out of usable Floor.
- Exact Trap failure costs/consequences, falling rules, and the detailed determination of "running out of usable Floor" remain provisional.

Players apply these written rules and consequences manually unless optional assistance has been approved and implemented for a particular operation.

## 7. Floor Collapse

- Floor Collapse is a distinct operation from Search.
- At intervals, Collapse uses `2d6` to identify an X/Y Floor position.
- The identified Floor becomes a permanent hole/unusable space.
- Collapse schedule kind is authored per Mode: Easy and Hard default to round-based Collapse; Impossible defaults to real-time Collapse. Exact interval/frequency, protection/reroll conditions, falling consequences, and interaction with any action economy remain provisional.

The existing prototype's **Floorfall** targeting may be treated as optional assistance for identifying a `2d6` Floor coordinate, but its old round schedule and old lifecycle do not define current Game Rules.

## 8. Console

- Each Player uses the universal configurable Console.
- The **Main Console Slot contains the Avatar**.
- The Trap Floor Console has **eight Side Slots** accepting Ability/Action Cards.
- Side Slots support Card stacking. Physical Slot count is independent of total Card capacity.
- Cards in Console Slots may remain face-up or be flipped through generic tabletop interaction.
- Other HUD/stat/component areas remain configurable authored data.
- Controller Mapping is optional and is not required for Trap Floor to function.

## 9. Controller Hand, Costs, and Modes

The available Controller input vocabulary is **Up, Down, Left, Right, A, B, X, Y**.

- Maximum Controller hand size is 10.
- At the start of a Player turn, draw up to 10; unused Controller Cards carry over. For example, a hand of four draws six.
- Ability/Action Card costs are authored collections of Controller inputs and may repeat an input, such as Right x2 and A x1.
- Costs are all-or-nothing. Partial payment is not permitted.
- Players do not contribute Controller Cards to another Player's purchase.
- A future cartridge may use fixed mappings, Player mappings, Controller Cards, or no mapping.

Trap Floor currently defines:

- **Easy:** Team behavior, 1 required Key, 2 starting Abilities, round-based Collapse default.
- **Hard:** Team behavior, 3 required Keys, 1 starting Ability, round-based Collapse default.
- **Impossible:** Survival behavior, 3 required Keys, 0 starting Abilities, real-time Collapse default.

Changing a Card cost, Grid dimension, Mode value, or Console placement rule is an authored Definition-data change and must not require changing generic Platform code.

## 10. Current Content Reference and Provisional Structure

Use the latest Milanote card-set structure as the current content reference.

The following are explicitly not fixed by this requirements revision:

- exact Card counts;
- exact Card names and text not otherwise confirmed here;
- exact distribution of Keys, Traps, or other Floor effects;
- exact Ability/Action Card cost values not explicitly authored in current content;
- Controller Deck composition and reshuffle details;
- exact Avatar stats and starting/base Ability assignments;
- a round limit;
- detailed HUD/stat/component area layout.

The former 14 Trap + 14 Coin + 8 Item Floormaster Deck and shared 50-coin setup are legacy implementation/reference material only. They must not be used to fill gaps in the current card set.

## 11. Flexible and Provisional Rules

Keep the following explicitly flexible until separately confirmed:

- Avatar stats and abilities;
- exact movement values, while preserving orthogonal movement and the general no-more-than-three guidance;
- Trap costs and consequences;
- exact Collapse interval/frequency within the authored schedule kind;
- falling rules;
- action economy.

Do not hard-lock a round limit or still-provisional detailed content. The approved Controller hand, input-cost, Console Slot, and Mode values above are authored defaults, not Platform restrictions on freeform play.

## 12. Freeform Play and Optional Assistance

Trap Floor remains playable as people would play it at a physical table:

1. Players read the current Cards and instructions.
2. Players decide what the current rule requires.
3. Players manipulate the physical tabletop Components to carry it out.
4. Other Players observe, challenge, and correct mistakes or illegal moves socially.

The distinction is:

- **Freeform Action:** direct physical manipulation, such as revealing a Floor Card, moving a Pawn orthogonally, carrying a Key, rolling Dice, or marking/removing a collapsed Floor.
- **Assisted Action:** optional Game-specific interpretation or convenience, such as identifying the Floor coordinate selected by two accepted d6 values.

Assistance may fail or decline when Players use house rules, substitute Components, or alter the official setup beyond recognition. It must not prevent continued manual play.

### 12.1 Existing Assistance Disposition

Completed implementation history is classified as follows:

- **Floorfall/Floor-coordinate targeting:** potentially reusable optional assistance for interpreting two d6 as an X/Y Floor coordinate. Its old round-one exception and schedule are not current authority unless reconfirmed.
- **Floormaster Search lifecycle:** legacy/prototype assistance tied to the superseded Floormaster Deck and draw/discard Search model. It is not current required gameplay.
- **Round/phase orchestration:** legacy/prototype optional infrastructure tied to the superseded fixed loop. It is not current required gameplay or authority for a round limit.

None of these systems may block the current manual Search, movement, Key, Exit, Trap, or Collapse flow.

## 13. Manually Playable Completion Criteria

Trap Floor may be considered playable when:

1. Its current approved starting Game Template loads correctly.
2. The `6 x 6` Board and required physical Components are present and readable.
3. Two to four Players can establish starting positions with `2d6` in an authored and verified Player Layout.
4. Players can manually move orthogonally, share Floor tiles, reveal searched Floor Cards, carry required Keys to the Exit, apply Traps/effects, and mark permanent collapsed holes.
5. Players can manually manipulate the required Cards, Dice, Pawns, Tokens, Consoles, Slots, and other confirmed Components.
6. Current Game content/instructions are readable enough for Players to perform the confirmed flow and adjudicate provisional values socially.
7. Reset and Template/session replacement behavior is coherent.
8. Optional or legacy assistance does not prevent manual play.

This playable milestone does not require comprehensive Card-effect automation, automatic movement legality, automatic Trap/fall resolution, automatic Key/Exit validation, automatic win/loss evaluation, or comprehensive round/action enforcement. Any Player-count claim must identify the authored and verified Player Layouts actually supported.

After this manually playable state is reached, the next Trap Floor work is a dedicated polishing pass rather than deeper mandatory rules-engine implementation.

## 14. Explicit Exclusions

Do not restore the superseded 50-coin objective/economy, 14 Trap + 14 Coin + 8 Item Floormaster Deck, Floormaster draw/discard Search lifecycle, fixed six-Slot Trap Floor Console allocation, or fixed 10-round sequence as current authority. The Easy/Hard/Impossible Mode definitions in §9 replace conflicting historical Mode direction.

Do not infer the current content counts or provisional rules from either the old Trap Floor prototype or the older Trap Door concept. Keys and the Exit are current Trap Floor concepts; the obsolete sequential Level Deck/dungeon progression and enemies are not.

## 15. Playtest Revision 1.5 (approved 2026-10-09)

Sources: *Trap Floor — Playtest Feedback & Proposed Changes* and *Trap Floor: New Cards* (Oct 1, 2026, @zakwan), plus the owner's answers of 2026-10-09.

### 15.1 Playtest issues being addressed

1. Ability timing was unclear (Dodge and Shield are used after a Trap triggers, yet the turn rule said "move or use an ability").
2. Trap variety felt flat and early elimination felt unfun on a physical board.
3. Too few kinds of Ability.
4. Search and Safe Search slowed the game down.
5. Unused Controller Cards piled up in hand.
6. Buying Abilities from a separate market was slow and hard to track.

### 15.2 Turn and Search

- On your turn you draw Controller Cards up to the hand limit (§9), then either **move** or play an **Action** Ability in place of moving.
- **Search is free.** Moving onto a face-down Floor Card flips it and it takes effect. The paid Search action is retired. *(Supersedes the A/B/X/Y Search cost.)*
- ~~**Safe Search is an Ability**~~ Removed for now (§15.9 follow-up): Check covers making a Trap harmless.

### 15.3 Action and Reaction Abilities

Every Ability Card is one of two kinds, printed on the card:

- **Action:** played on your turn, in place of your movement. Examples: Rush, Disarm, Push.
- **Reaction:** played in response to a Trap, on the Trap card the player is on. Examples: Shield, Dodge, Check (the former "Jump").

The card's own text states exactly what it does (for example, *"Push a player who shares your tile 2 tiles horizontally"*). Edge cases (grid edges, collapsed tiles) are handled by the card text and by players at the table.

### 15.4 Abilities on the Floor (market retired)

- Ability Cards are part of the Floor Cards. The separate purchase market (Buy Ability) is retired for now.
- A player who flips an Ability Card may **take it into their hand by paying its cost**, and **plays it later by paying the same cost again**.
- A player may decline. The card stays face up, and the next player who lands on it may take it on the same terms.
- Costs remain all-or-nothing and are not shared (§9).

### 15.5 Traps

- **General:** Trap and special Floor Cards are one-time effects. They only affect the player on the turn the card is flipped.
- **Chaining:** if a Trap moves a player onto another face-down card, that card flips and resolves too. Each card flips once, so chains always end.
- Players can always share a tile.
- **Elimination lasts for the current Floor only.** An eliminated player sits out the rest of that round and returns next round. This matches the current runtime.
- **New Trap types:**
  - **Spring Trap** (4 variants: ▲ ▼ ◀ ▶). It launches the player one tile in the direction shown. Off the grid or onto a collapsed tile, the player is eliminated. Onto a face-down card, that card flips and resolves.
  - **Crane Trap.** The player rolls 2d6 and is placed on that tile (first die = column, second die = row). Onto a collapsed tile, the player is eliminated. Onto a face-down card, that card flips and resolves.
- **New special Floor Card:**
  - **Overcharged.** The player who flips it gets +1 movement on their next turn.

### 15.6 Floor composition (36 Floor Cards)

| Content | Count |
|---|---|
| Trap Cards | 18 |
| Friend Cards | 4 |
| Keys | 6 |
| Secret Exit | 1 |
| Grand Exit | 1 |
| Ability Cards | 6 |
| **Total** | **36** |

Placement is either random or follows a difficulty pattern (§15.7). The exact mix of Trap types within the 18 is still being set. A starting proposal for playtest:

| Trap type | Proposed count |
|---|---|
| Spring (1 per direction) | 4 |
| Crane | 2 |
| Fall (eliminate this Floor) | 3 |
| Slow | 3 |
| Sticky | 3 |
| Blind | 3 |

Overcharged is a special Floor Card in the Friend pool (§15.10).

**Implementation (R2b, 2026-10-09):** the floor is built from five **floor groups** in the game definition (content sets with the role `floor`). Each draws its count at random:

| Group | Draws | From |
|---|---|---|
| Traps | 18 | the Trap mix above (each card counted by its quantity) |
| Friends | 4 | the four Friend cards plus Overcharged |
| Keys | 6 | Bronze, Silver and Golden, two of each |
| Exits | 2 | Grand Exit and Secret Exit |
| Abilities | 6 | Check, Rush, Dodge, Disarm, Shield and Push, repeats allowed |

The Trap, Friend and Ability counts and the placement method are rule settings (doc 23 §5), so a house rule can change them. The groups must still add up to the 36 tiles. The earlier provisional Traps (Chain Snare, Crushing Walls and the rest) are no longer on the floor.

### 15.7 Difficulty placement patterns (proposal)

Placement is authored per Mode. It is data, not code, so it can be tuned between playtests.

- **Easy (guided):**
  - The tiles next to each starting corner hold no Fall, Spring or Crane.
  - At least one Ability lies within two tiles of each start.
  - Keys are spread across different quadrants.
  - The Grand Exit is away from the starting corners.
  - No Spring sits on an edge tile pointing off the grid.
- **Hard (constrained random):** random, except that no Fall lies next to a starting corner and no Key lies next to one.
- **Impossible (random):** fully random. Edge Springs pointing off the grid are allowed.

### 15.8 Difficulty and win mode (owner cards, 2026-10-09)

Difficulty and win mode are **two separate choices**. A table picks one of each, for example *Hard · Team*.

**Difficulty (Settings card):**

| Difficulty | Rule |
|---|---|
| Easy | Bring only 1 Key to the Grand Exit. |
| Hard | Bring all 3 Keys to the Grand Exit, by any means possible. |
| Impossible | All 3 Keys are required to escape. Once a player exits, the floor collapses at each player's turn (a race against time). Turn order changes to player → floor → player → floor. |

**Win mode (How to win card):**

| Win mode | Rule |
|---|---|
| Team | Only one player from the team needs to escape. |
| Survival | All team members must escape. |

**Exit and endgame (How to win card):**

- Players must collect 3 Keys to unlock the Grand Exit and escape. On Easy, 1 Key.
- When all required Keys are collected, roll 2d6 to determine the Exit's position.
- Once the Exit is revealed, stepping on tiles automatically triggers Traps, and prizes are disabled.

**Change from the current build:** each authored Mode today fixes one behaviour (Easy and Hard are Team; Impossible is Survival). It also sets real-time collapse for Impossible, which differs from the card. Runtime and content must split Difficulty from win mode, and the start flow's Difficulty step gains a Team/Survival choice.

### 15.9 Owner decisions (2026-10-09, second round)

**Abilities (from the owner's card notes):**

- Every Ability Card shows its own cost on the card.
- **Check** (replaces the earlier "Jump" card): if the tile is a Trap, the Trap does not hurt anyone.
- **Rush** (replaces the earlier "Run" card; Run and Jump combined): move over up to 3 spaces quickly, even over a missing (collapsed) space.

**Avatars and starting abilities:**

- At the start, every player chooses an Avatar card. Each Avatar brings its own starting Ability:

  | Avatar | Starting Ability |
  |---|---|
  | Clairvoyant | Dodge |
  | Strategist | Check |
  | Athlete | Rush |
  | Engineer | Disarm |

- Avatar-to-Ability pairings must be **data-driven** (editable without code).
- Mode starting-Ability counts are replaced by the Avatar's Ability. Hard starts with **0** extra Abilities.

**Floor placement method:** chosen when the game is set up (start flow Layout step). The choices are **Random**, the **difficulty pattern** (§15.7), or **any other saved layout**. The §15.6 Trap mix and §15.7 patterns are kept as the default options.

**Exits:**

- **Grand Exit:** once the Keys are collected, it lets all players exit.
- **Secret Exit:** an instant win for one player for one round, once that player meets the Key requirement.

**Endgame:**

- Once the Exit is revealed, stepping on any Trap tile triggers it again, including Traps already flipped.
- Prizes (Friends, floor Abilities) can still be taken. A taken prize tile then acts as a collapsed tile.

**Other answers:**

- Impossible: no collapse before the first player exits.
- Survival: an eliminated player returns and tries again next round.
- Hard, "by any means possible": players may pass Keys to each other.

### 15.10 Open decisions from this revision

- Resolved: Overcharged joins the Friend pool. The 4 Friend slots are filled at random from the Friend cards plus Overcharged.
- Resolved: the 6 Ability Cards on the Floor are chosen at random from the Ability roster (Check, Rush, Dodge, Disarm, Shield, Push). **Safe Search is removed for now**; Check covers making a Trap harmless.
- Resolved in §15.9: starting Abilities come from the Avatar; Hard starts with 0.
- Resolved in §15.9: the Grand Exit, the Secret Exit, Traps triggering again after the Exit is revealed, prizes after the Exit, Impossible collapse, Survival retries and passing Keys.
- **Open:** the final Trap-type mix and the §15.7 patterns, after playtest.
- **Open:** the costs of Check, Rush, Disarm and Push. Their cards have no cost yet (marked `cost-pending`); Shield and Dodge keep their provisional costs.
- **Open:** the exact card text for Dodge, Shield and Disarm. Their cards now say whether they are Action or Reaction, and are otherwise resolved by the table.
