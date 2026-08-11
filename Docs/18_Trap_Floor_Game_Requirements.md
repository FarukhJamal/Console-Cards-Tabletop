# Console Cards - Trap Floor Game Requirements

**Document ID:** 18_Trap_Floor_Game_Requirements
**Version:** 1.2
**Status:** Approved with Open Decisions
**Authoritative source:** Approved Trap Floor direction supplied 2026-08-10 and confirmed freeform tabletop rule philosophy supplied 2026-08-11
**Purpose:** Define the approved minimum Game-specific setup, Board, written play flow, modes, content boundaries, and manually playable acceptance for Trap Floor without moving Game Rules into the Platform or requiring comprehensive Game-rule automation.

## 1. Authority and Supersession

The approved Game name is **Trap Floor**.

**Trap Door** is obsolete terminology. Earlier Trap Door descriptions of a sequential Level Deck, dungeon/room progression, enemies, keys, exits, or a reveal-next-Level-Card loop are superseded and are not Trap Floor requirements.

This document is the authoritative Trap Floor direction. `Consolecards_LayoutRef_doc.pdf` remains authoritative for shared layout and interaction requirements and for Super Leroy Sisters where not superseded by a later approved decision.

## 2. Platform Boundaries

- The Console is universal.
- The Trap Floor Game Board is Game-specific.
- The Game Template defines starting setup, content, and layout.
- Game-specific rules remain outside the generic Game Template schema.
- Trap Floor Game Rules are primarily interpreted and enforced by Players through Freeform Actions.
- Game-specific automation is optional assistance and must not own or disable generic physical actions.
- Runtime State remains authoritative during a Match.
- Trap Floor is selected through Platform Session Entry and must not be forced automatically at application startup.
- Trap Floor uses the Platform's generic component types; its Game Template does not own or redefine them.
- The wider Console Cards Player Layout model remains structurally capable of one to eight Players; Trap Floor itself supports two to four Players.
- Central gameplay remains the primary visual focus.

## 3. Player Count

Trap Floor supports **two to four Players**.

The exact authored Seat mappings for two and three Players remain unresolved under OD-014. The confirmed four-Player layouts do not authorize inventing those missing mappings.

## 4. Game Board and Floorfall

- The Game Board/Play Area is a fixed `6 x 6` grid made from **36 Floor Cards**.
- Floor Cards are Board tiles, not a drawable sequential Level Deck.
- Each Floor Card occupies a stable X/Y grid coordinate.
- Floorfall rolls `2d6`:
  - die 1 selects the X-axis coordinate;
  - die 2 selects the Y-axis coordinate.
- The resulting coordinate identifies the Floor Card that collapses.
- During round 1, reroll a Floorfall result that identifies a starting corner.
- The two d6 are first-class generic Platform Die Object Instances with authoritative values and Tabletop Poses. Trap Floor's Game-specific Floorfall logic interprets them as X and Y; it does not introduce a Trap Floor-specific Die type.
- The Trap Floor Game Template includes those two generic d6 as starting setup/content; the underlying Die type remains available to Empty/Custom Tables and other Games.

## 5. Floormaster Deck

The Floormaster's Deck contains 36 Cards:

- 14 Trap Cards.
- 14 Coin Cards.
- 8 Item Cards.

Shuffle these Cards as the Floormaster's Deck. Draw from the left and discard to the right. Reshuffle the discard into the Deck when the Deck is exhausted.

The Floormaster's Deck is separate from the 36 Floor Cards that form the Board.

Players must be able to perform this lifecycle manually using generic Deck, Card, Stack/discard, shuffle, and transfer capabilities. Optional Floormaster lifecycle assistance may perform the known official draw/reveal/discard/reshuffle sequence, but it is not the required or exclusive way to play those Cards.

## 6. Player Setup

Each Player has:

- one universal Console;
- an Avatar Card in the Main Console Slot;
- a Rule Card and a Mode Card in the Bottom Console Slots, ordered left to right;
- a Controller Deck beside the Console;
- up to three Item Cards per round in the Top Console Slots;
- one Pawn/meeple representing their Board position.

Each Player begins on any corner Floor Card.

## 7. Shared Coins

- A shared pool contains 50 wooden coin cubes.
- Players acquire coins from this pool.
- Acquired coins are stored on the Player's Console.
- Spent coins return to the shared pool.

## 8. Turn and Round Structure

Trap Floor lasts **10 rounds**. Each round follows this five-step loop:

> **Start -> Search -> Trigger -> Floorfall -> End**

- **Search:** draw one Card from the Floormaster's Deck.
- **Trigger:** immediately resolve the drawn Card's effect.
- **Floorfall:** roll `2d6` and collapse the Floor Card at the resulting coordinate, subject to the round 1 starting-corner reroll.
- **Hard Mode:** perform two Floorfalls instead of one.

This loop is a Player-facing Game Rule. Players may track and perform it using readable instructions and physical Component manipulation. A coded round/phase controller is not required for Trap Floor to be playable.

## 9. Modes

### Easy

- One Floorfall per round.
- **All for one:** if one Player is eliminated, the whole group loses that round's coins.
- The currently approved win condition is collecting exactly 50 coins within 10 rounds.

### Hard

- Two Floorfalls per round.
- **One for all:** an eliminated Player loses only their own coins and is removed for the rest of the Game.
- The currently approved win condition is the group/surviving Players collecting exactly 50 coins within 10 rounds.

## 10. Cards and Inputs

The following are distinct concepts and must not be merged in documentation or implementation without a later approved decision:

### Controller Cards

- Each Player has a Controller Deck.
- Controller Cards are spent for Trap and Coin interactions.
- The complete Controller Deck composition and exact costs are unresolved.

### Button Inputs

- A, B, X, and Y are universal inputs.
- Search uses A/B/X/Y.
- Careful Search uses A+B+X+Y.

### Skill Cards

- Skill Cards may require specific Button input combinations.
- The documented example is **Dodge**, which costs X+Y+B and allows escape to one of the eight adjacent tiles.
- The exact structural relationship between Controller Cards and Skill Cards remains unresolved.

Costs, rewards, movement, Card effects, elimination, and win/loss may be carried out and judged by Players through physical tabletop actions. Their presence as Game Rules does not inherently require automatic Platform validation or execution.

## 11. Freeform Play and Optional Assistance

Trap Floor is intended to remain playable as people would play it at a physical table:

1. Players read Rule Cards, Mode Cards, Card text, and other instructions.
2. Players decide what the current rule requires.
3. Players manipulate the physical tabletop Components to carry it out.
4. Other Players observe, challenge, and correct mistakes or illegal moves socially.

Examples include manually discarding two Controller Cards when instructed, moving a Pawn, paying or taking coin Tokens, storing an Item Card, removing an eliminated Pawn, and declaring the Game result according to the written rules. The Platform need not calculate whether those Game-specific choices are legal.

The distinction is:

- **Freeform Action:** direct physical manipulation, such as drawing a Card, moving it to discard, rolling a Die, dragging a Pawn, flipping a Card, or moving coins.
- **Assisted Action:** optional Game-specific interpretation or convenience, such as using the official two d6 to highlight a Floor Card.

Assistance may fail or decline when Players use house rules, substitute Components, or alter the official setup beyond recognition. It must not prevent continued manual play.

### 11.1 Existing Assistance Disposition

Completed implementation history remains valid and is classified as follows:

- **Floorfall targeting:** useful optional assistance that interprets the two official d6 and identifies/highlights the target Floor Card.
- **Floormaster Search lifecycle:** optional/prototype assistance for the official draw, pending reveal, discard, and exhaustion reshuffle lifecycle. Manual Floormaster Card play remains valid.
- **Round/phase orchestration:** experimental/prototype optional assisted-flow infrastructure. It is not required core Trap Floor gameplay and future completion does not depend on extending it into a comprehensive rules engine.

None of these systems authorizes blocking unrelated Freeform Actions or requiring Players to use the assisted path.

## 12. Manually Playable Completion Criteria

Trap Floor may be considered playable when:

1. Its approved starting Game Template loads correctly.
2. The `6 x 6` Board and required physical Components are present and readable.
3. Players can manually manipulate the required Cards, Decks, Dice, Pawns, Tokens, Consoles, Slots, and discard areas.
4. Players can read enough Game content and instructions to know what actions to perform.
5. The generic physical actions required by Trap Floor are functional.
6. Reset and Session Entry/exit behavior is coherent.
7. Optional assistance does not prevent manual play.

This playable milestone does not require full automated Card effects, a coded coin economy, automatic movement legality, automatic elimination, automatic win/loss evaluation, or comprehensive round-rule enforcement. Any Player-count claim must still identify the authored and verified Player Layouts actually supported; unresolved two- and three-Player layout mappings must not be claimed as complete.

After this manually playable state is reached, the next Trap Floor work is a dedicated polishing pass rather than deeper mandatory rules-engine implementation.

## 13. Intentionally Unresolved Trap Floor Design

Do not infer or invent:

- exact Floor Card visual design;
- exact collapsed-tile behavior beyond the currently documented consequences;
- complete Controller Deck list/count;
- exact Controller Card costs;
- exact distinction between Controller Cards and Skill Cards;
- Skill Card count/content;
- whether movement is orthogonal-only or allows diagonals;
- what happens when multiple meeples occupy the same Floor Card;
- detailed Avatar abilities or move speeds where not already specified;
- detailed Trap, Coin, or Item Card contents beyond the approved categories and fields.

These decisions remain tracked by OD-018. They may affect readable Game content, optional assistance, or later refinement, but absent automation alone does not block the manually playable criteria in Section 12. The two- and three-Player Seat mappings remain tracked by OD-014.

## 14. Explicit Exclusions

Do not add enemies, keys, exits, or a sequential Level Deck to Trap Floor. Those elements belong to the superseded Trap Door concept and are not part of the approved Game.
