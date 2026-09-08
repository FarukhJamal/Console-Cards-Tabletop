# Console Cards - Trap Floor Game Requirements

**Document ID:** 18_Trap_Floor_Game_Requirements
**Version:** 1.3
**Status:** Approved with Open Decisions
**Authoritative source:** Latest Russell/Milanote Trap Floor direction supplied 2026-09-08, with the freeform tabletop rule philosophy supplied 2026-08-11
**Purpose:** Define the current approved minimum Game-specific direction for Trap Floor while keeping provisional design values open and preserving manual tabletop play.

## 1. Authority and Supersession

The approved Game name is **Trap Floor**.

This revision supersedes the former Trap Floor build based on a shared 50-coin pool, a 36-Card Floormaster's Deck composed of 14 Trap, 14 Coin, and 8 Item Cards, Controller Deck requirements, fixed Console Slot usage beyond the Main Slot, a fixed 10-round loop, and the associated Floormaster draw/discard lifecycle. Those elements may remain in the current prototype or implementation history, but they are outdated design reference and are not authority for future Trap Floor content or rules.

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

- The Game Board/Play Area is a fixed `6 x 6` grid made from **36 Floor Cards**.
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
- The number of required Keys is provisional.
- Players lose by failing a Trap or by running out of usable Floor.
- Exact Trap failure costs/consequences, falling rules, and the detailed determination of "running out of usable Floor" remain provisional.

Players apply these written rules and consequences manually unless optional assistance has been approved and implemented for a particular operation.

## 7. Floor Collapse

- Floor Collapse is a distinct operation from Search.
- At intervals, Collapse uses `2d6` to identify an X/Y Floor position.
- The identified Floor becomes a permanent hole/unusable space.
- Collapse timing, frequency, protection/reroll conditions, falling consequences, and interaction with any action economy remain provisional.

The existing prototype's **Floorfall** targeting may be treated as optional assistance for identifying a `2d6` Floor coordinate, but its old round schedule and old lifecycle do not define current Game Rules.

## 8. Console

- Each Player uses the universal configurable Console.
- The **Main Console Slot contains the Avatar**.
- Avatar stats and abilities remain provisional.
- Other Console Slot usage is not currently hard-locked. Do not infer Rule, Mode, Item, Side, Cube, Dice, or other Slot assignments from the former prototype setup.
- A Game Template should configure only the Slots required by the confirmed current content.

## 9. Current Content Reference and Provisional Structure

Use the latest Milanote card-set structure as the current content reference.

The following are explicitly not fixed by this requirements revision:

- exact Card counts;
- exact Card names and text not otherwise confirmed here;
- exact distribution of Keys, Traps, or other Floor effects;
- Controller Deck existence, composition, or costs;
- Skill/Ability Card architecture;
- Hand, draw, discard, or reshuffle rules;
- mode structure;
- a round limit;
- Console Slot usage beyond Avatar in the Main Slot.

The former 14 Trap + 14 Coin + 8 Item Floormaster Deck and shared 50-coin setup are legacy implementation/reference material only. They must not be used to fill gaps in the current card set.

## 10. Flexible and Provisional Rules

Keep the following explicitly flexible until separately confirmed:

- Avatar stats and abilities;
- exact movement values, while preserving orthogonal movement and the general no-more-than-three guidance;
- Trap costs and consequences;
- number of required Keys;
- Collapse timing and frequency;
- falling rules;
- action economy.

Do not hard-lock:

- a Controller Deck;
- Skill/Ability architecture;
- other Console Slot usage;
- Hand/draw/discard rules;
- modes;
- a round limit.

## 11. Freeform Play and Optional Assistance

Trap Floor remains playable as people would play it at a physical table:

1. Players read the current Cards and instructions.
2. Players decide what the current rule requires.
3. Players manipulate the physical tabletop Components to carry it out.
4. Other Players observe, challenge, and correct mistakes or illegal moves socially.

The distinction is:

- **Freeform Action:** direct physical manipulation, such as revealing a Floor Card, moving a Pawn orthogonally, carrying a Key, rolling Dice, or marking/removing a collapsed Floor.
- **Assisted Action:** optional Game-specific interpretation or convenience, such as identifying the Floor coordinate selected by two accepted d6 values.

Assistance may fail or decline when Players use house rules, substitute Components, or alter the official setup beyond recognition. It must not prevent continued manual play.

### 11.1 Existing Assistance Disposition

Completed implementation history is classified as follows:

- **Floorfall/Floor-coordinate targeting:** potentially reusable optional assistance for interpreting two d6 as an X/Y Floor coordinate. Its old round-one exception and schedule are not current authority unless reconfirmed.
- **Floormaster Search lifecycle:** legacy/prototype assistance tied to the superseded Floormaster Deck and draw/discard Search model. It is not current required gameplay.
- **Round/phase orchestration:** legacy/prototype optional infrastructure tied to the superseded fixed loop. It is not current required gameplay or authority for a round limit.

None of these systems may block the current manual Search, movement, Key, Exit, Trap, or Collapse flow.

## 12. Manually Playable Completion Criteria

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

## 13. Explicit Exclusions

Do not restore the superseded 50-coin objective/economy, 14 Trap + 14 Coin + 8 Item Floormaster Deck, Floormaster draw/discard Search lifecycle, mandatory Controller Deck, fixed six-Slot Trap Floor Console allocation, Easy/Hard mode rules, or fixed 10-round sequence as current authority.

Do not infer the current content counts or provisional rules from either the old Trap Floor prototype or the older Trap Door concept. Keys and the Exit are current Trap Floor concepts; the obsolete sequential Level Deck/dungeon progression and enemies are not.
