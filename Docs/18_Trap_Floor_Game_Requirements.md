# Console Cards - Trap Floor Game Requirements

**Document ID:** 18_Trap_Floor_Game_Requirements
**Version:** 1.1
**Status:** Approved with Open Decisions
**Authoritative source:** Approved Trap Floor direction supplied 2026-08-10
**Purpose:** Define the approved minimum Game-specific setup, Board, round flow, modes, and content boundaries for Trap Floor without moving Game Rules into the Platform or generic Game Template schema.

## 1. Authority and Supersession

The approved Game name is **Trap Floor**.

**Trap Door** is obsolete terminology. Earlier Trap Door descriptions of a sequential Level Deck, dungeon/room progression, enemies, keys, exits, or a reveal-next-Level-Card loop are superseded and are not Trap Floor requirements.

This document is the authoritative Trap Floor direction. `Consolecards_LayoutRef_doc.pdf` remains authoritative for shared layout and interaction requirements and for Super Leroy Sisters where not superseded by a later approved decision.

## 2. Platform Boundaries

- The Console is universal.
- The Trap Floor Game Board is Game-specific.
- The Game Template defines starting setup, content, and layout.
- Game-specific rules remain outside the generic Game Template schema.
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

## 11. Intentionally Unresolved Trap Floor Design

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

These decisions remain tracked by OD-018. The two- and three-Player Seat mappings remain tracked by OD-014.

## 12. Explicit Exclusions

Do not add enemies, keys, exits, or a sequential Level Deck to Trap Floor. Those elements belong to the superseded Trap Door concept and are not part of the approved Game.
