# Console Cards — Terminology

**Document ID:** 02_Terminology  
**Version:** 1.0 Draft  
**Status:** Approved
**Depends on:** `00_Product_Vision.md`  
**Purpose:** Establish one canonical vocabulary for product, design, architecture, documentation, and implementation.

---

> **Scope note:** Defining a term does not place that feature in the current milestone. Milestone scope is controlled only by `16_Milestones_And_Roadmap.md` and approved task prompts.

## 1. Usage Rules

This document is the vocabulary source of truth for Console Cards.

All project documents, code, comments, task prompts, implementation reports, and UI specifications must use the terms defined here consistently.

### Required rules

1. Do not invent a new term when an existing term already describes the concept.
2. Do not use similar terms interchangeably unless this document explicitly allows it.
3. When a new concept is introduced, update this document before using the term broadly.
4. Prefer the canonical term in headings, class names, interfaces, fields, tests, and documentation.
5. Product terms and technical implementation terms must remain distinct.
6. A temporary networking identifier must never be treated as a permanent player identity.
7. A Unity GameObject must never be described as the authoritative gameplay state.
8. A Game Template must never be described as a hardcoded game implementation.

---

# 2. Product Terms

## 2.1 Console Cards

**Definition:**  
The complete multiplayer virtual tabletop platform.

Console Cards includes:

- The shared virtual table.
- Player seats.
- Hands and Consoles.
- Generic tabletop objects.
- Play Areas.
- Interaction systems.
- Game Templates.
- Saving and restoration.
- Multiplayer synchronization.
- Configurable policies.

**Do not use as a synonym for:**

- A Match.
- A Game Template.
- A single game.
- The Console UI.

---

## 2.2 Platform

**Definition:**  
The reusable systems that exist independently of any particular game.

The Platform owns universal functionality such as:

- Tabletop coordinates.
- Object state.
- Interaction.
- Containers.
- Hands.
- Consoles.
- Seats.
- Play Areas.
- Policies.
- Persistence.
- Multiplayer synchronization.

**Important distinction:**  
The Platform does not know the rules of Super Leroy Sisters, Trap Floor, or any other specific game unless a future optional game-specific module is explicitly added.

---

## 2.3 Game

**Definition:**  
The rules and intended experience that players choose to play.

A Game may exist as:

- An official design.
- A custom player-created design.
- A modified house-rule version.
- A spoken rule set with no saved template.

**Important distinction:**  
A Game is the conceptual ruleset. A Game Template is the saved starting configuration used to prepare the table.

---

## 2.4 Game Template

**Definition:**  
A reusable, serializable starting configuration used to prepare a Match.

A Game Template may include:

- Metadata.
- Rulebook.
- Player-count recommendations.
- Seats.
- Play Areas.
- Consoles.
- Cards and decks.
- Boards and pieces.
- Starting arrangement.
- Default policies.
- Camera bookmarks.
- Initial snapshot data.

**A Game Template is data and content.**

It is not automatically:

- A rule engine.
- A hardcoded gameplay system.
- A Unity scene.
- A Match.
- A network session.

### Game Template categories

#### Official Game Template

Created and maintained by the Console Cards development team.

#### Modified Game Template

A copy of another Game Template with player or designer changes.

#### Custom Game Template

Created from player-selected or user-created content.

#### Empty Table Template

A minimal Game Template containing only the basic table, seats, hands, Consoles, and required platform objects.

**Preferred term:** `Game Template`  
**Avoid:** preset, game preset, mode preset, level preset.

“Preset” may be used only for small internal configuration objects, not for the product-level game package.

---

## 2.5 Match

**Definition:**  
A running playable instance created from a Game Template or Empty Table Template.

A Match contains mutable runtime state, such as:

- Current object positions.
- Current deck order.
- Current hands.
- Current ownership.
- Current visibility.
- Current Console contents.
- Current policy selections.
- Current seat occupancy.

**Important distinction:**  
The Game Template describes the starting state. The Match contains the changing live state.

---

## 2.6 Multiplayer Session

**Definition:**  
The connected group and network lifecycle surrounding a Match.

A Multiplayer Session may include:

- Player membership.
- Connection state.
- Lobby or room information.
- Host or server authority.
- Reconnection.
- Seat rebinding.
- Match synchronization.

**Important distinction:**  
A Multiplayer Session is not the same thing as a Match. A session may exist before a Match starts or while a Match is recovering.

**Preferred short form:** `Session` when context is unambiguous.

---

## 2.7 Rulebook

**Definition:**  
Human-readable instructions describing how a Game is intended to be played.

A Rulebook may contain:

- Overview.
- Setup.
- Turn sequence.
- Object meanings.
- Allowed actions.
- Scoring.
- Victory conditions.
- House rules.
- Examples.

A Rulebook does not automatically execute or enforce its text.

---

## 2.8 House Rule

**Definition:**  
A player-defined change or addition to a Game’s intended rules.

House Rules may be:

- Spoken during a Match.
- Written into a modified Rulebook.
- Saved into a Modified Game Template.

---

# 3. Player and Seating Terms

## 3.1 Player

**Definition:**  
A human participant in a Multiplayer Session.

A Player has a stable platform identity that is separate from their temporary network connection.

---

## 3.2 Player ID

**Definition:**  
The stable identity used to associate a Player with saved or runtime state.

A Player ID may be backed by authentication, account data, or another stable identifier.

**Must not be confused with:**

- Network client ID.
- Photon `PlayerRef`.
- NGO `ClientId`.
- Mirror connection ID.
- Seat ID.

---

## 3.3 Seat

**Definition:**  
A stable position and ownership context around the virtual table.

A Seat may define:

- Table orientation.
- Player area.
- Hand ownership.
- Console ownership.
- Camera focus.
- Visibility relationship.
- Personal zones.

Seats may exist before Players occupy them.

---

## 3.4 Seat ID

**Definition:**  
The stable identifier of a Seat within a Match or Game Template.

A Seat ID must remain stable when a Player disconnects and reconnects.

---

## 3.5 Seat Assignment

**Definition:**  
The association between a Player ID and a Seat ID.

Seat Assignment may be:

- Automatic.
- Host-controlled.
- Restored after reconnection.
- Released after a timeout or explicit removal.

---

## 3.6 Network Connection ID

**Definition:**  
A temporary identifier assigned by the selected networking technology.

Examples include:

- NGO `ClientId`.
- Photon `PlayerRef`.
- Mirror connection ID.

A Network Connection ID may change after reconnection and must not own persistent seat or hand state.

---

# 4. Tabletop Space Terms

## 4.1 Virtual Tabletop

**Definition:**  
The complete logical and visual shared surface on which players place and manipulate objects.

The Virtual Tabletop is effectively unbounded for normal use.

**Preferred term:** `Virtual Tabletop` or `Tabletop`  
**Avoid:** infinite board, endless board, replicated tables, parallax table.

---

## 4.2 Tabletop Space

**Definition:**  
The logical coordinate space used to position Tabletop Objects.

Tabletop Space exists independently of:

- Camera position.
- Visible table mesh sections.
- GameObject hierarchy.
- Play Area coordinates.

---

## 4.3 Table Coordinate

**Definition:**  
A logical two-dimensional location in Tabletop Space.

A Table Coordinate may use:

- Global coordinates.
- Sector or chunk coordinates.
- Local position inside a sector.

The exact implementation is defined in the Core Data Model document.

---

## 4.4 Tabletop Pose

**Definition:**  
The complete logical placement of a Tabletop Object.

A Tabletop Pose may include:

- Table Coordinate.
- Rotation.
- Layer.
- Local order.
- Optional scale where supported.

---

## 4.5 Table Surface

**Definition:**  
The visual representation of the tabletop beneath objects.

The Table Surface may be streamed, repositioned, tiled, or generated around the camera, but it must appear seamless.

The Table Surface is not the authoritative source of object positions.

---

## 4.6 Camera

**Definition:**  
A local player-controlled view into the shared Tabletop Space.

Each Player normally controls their own Camera independently.

Camera movement does not modify Match state unless an explicit shared-camera feature is introduced.

---

# 5. Play Area Terms

## 5.1 Play Area

**Definition:**  
An optional structured region placed within the Virtual Tabletop to organize a particular part of play.

A Play Area may provide:

- Visual boundaries.
- Placement suggestions.
- Snapping.
- Cells.
- Slots.
- Tracks.
- Zones.
- Camera focus bounds.
- Layering guidance.

A Play Area does not automatically enforce Game rules.

A Game Template may contain zero, one, or multiple Play Areas.

---

## 5.2 Play Area Layout

**Definition:**  
The strategy that calculates organization and suggested placement inside a Play Area.

Examples:

- Freeform layout.
- Rectangular grid layout.
- Hex grid layout.
- Side-scroller layout.
- Track layout.
- Zone layout.
- Tile-built layout.

---

## 5.3 Grid

**Definition:**  
A Play Area layout divided into addressable cells.

A Grid may define:

- Rows.
- Columns.
- Cell dimensions.
- Origin.
- Orientation.
- Snapping.
- Cell coordinates.

A Grid is not automatically:

- A board.
- A rule validator.
- A Game.
- A fixed platform-wide requirement.

---

## 5.4 Grid Cell

**Definition:**  
One addressable location inside a Grid.

A Grid Cell may contain multiple layered objects unless a Policy later restricts occupancy.

---

## 5.5 Side-Scroller Play Area

**Definition:**  
A horizontally oriented Play Area representing a continuous sequence of sections or slots.

It may support:

- Expanding horizontal content.
- Lanes.
- Character positions.
- Active-region camera focus.
- Archived or previously completed sections.

It is not merely assumed to be a one-row Grid, although a grid-like implementation may be used internally.

---

## 5.6 Track

**Definition:**  
An ordered path of positions used for progress, movement, racing, scoring, or sequence-based layouts.

A Track may be:

- Linear.
- Curved.
- Circular.
- Branching.

---

## 5.7 Zone

**Definition:**  
A named organizational region that may accept or display Tabletop Objects.

Examples:

- Draw zone.
- Discard zone.
- Character area.
- Available Move Cards area.
- Equipment area.
- Player area.

A Zone may provide snapping and layout without being a Container.

---

## 5.8 Slot

**Definition:**  
A specific placement target intended for one or more objects according to configuration.

Examples:

- Console slot.
- Card slot.
- Pawn starting slot.

A Slot is more specific than a Zone.

---

## 5.9 Placement Guide

**Definition:**  
A non-authoritative visual or logical aid used to suggest where objects should be placed.

Grids, tracks, slots, and zone highlights may act as Placement Guides.

---

## 5.10 Snapping

**Definition:**  
Adjusting an object toward a suggested placement supplied by a Play Area, Zone, Slot, or other Placement Guide.

Snapping may be:

- Off.
- Optional.
- Strong.
- Locked by a future Policy.

---

# 6. Tabletop Object Terms

## 6.1 Tabletop Object

**Definition:**  
Any interactable or placeable runtime object that exists in Tabletop Space.

Examples:

- Card.
- Deck.
- Pawn.
- Token.
- Dice.
- Board.
- Tile.
- Miniature.
- Note.
- Bag.

A Tabletop Object has a stable instance identity and runtime state.

---

## 6.2 Object Definition

**Definition:**  
Static authored data describing what a type of Tabletop Object is.

An Object Definition may include:

- Definition ID.
- Name.
- Visual assets.
- Dimensions.
- Category.
- Tags.
- Default capabilities.
- Default orientation.
- Optional metadata.

Object Definitions may be stored using ScriptableObjects or serialized content assets.

Object Definitions must not contain mutable Match state.

---

## 6.3 Object Instance

**Definition:**  
One runtime copy of an Object Definition inside a Match.

An Object Instance has its own:

- Instance ID.
- Current pose.
- Current container.
- Current owner.
- Current visibility.
- Current state.

Multiple Object Instances may reference the same Object Definition.

---

## 6.4 Tabletop Object ID

**Definition:**  
The stable unique identifier of one Object Instance within a Match.

**Preferred implementation name:** `TabletopObjectId`

---

## 6.5 Card Definition

**Definition:**  
Static data describing a card type.

Examples:

- Button A.
- Move Card.
- Level Card.
- Rule Reference Card.

---

## 6.6 Card Instance

**Definition:**  
One physical runtime copy of a Card Definition in a Match.

Ten Button A cards are ten Card Instances referencing one Card Definition.

---

## 6.7 Button Card

**Definition:**  
A universal Console Cards card representing one console input.

Canonical Button Cards:

- Up
- Down
- Left
- Right
- A
- B
- X
- Y

The meaning of a Button Card is determined by the Game or Rulebook.

---

## 6.8 Move Card

**Definition:**  
A card representing a stored or purchasable action that may be placed on a Player’s Console.

Move Card behavior depends on the Game and is not universally enforced by the Platform.

---

## 6.9 Board

**Definition:**  
A large Tabletop Object that visually represents a structured playing surface.

A Board may provide or reference Play Areas, Zones, Tracks, or Grids.

A Board and a Play Area are related but not identical:

- Board = physical/visual tabletop object.
- Play Area = logical organizational region.

---

## 6.10 Tile

**Definition:**  
A modular Tabletop Object used to construct maps, paths, rooms, terrain, or sections.

Tiles may snap to each other or to a Play Area.

---

## 6.11 Pawn or Meeple

**Definition:**  
A generic movable game piece representing a player, character, position, or objective.

Use `Pawn` as the general technical term.  
Use `Meeple` only in player-facing content where the shape or game language specifically requires it.

---

## 6.12 Miniature

**Definition:**  
A three-dimensional Tabletop Object representing a character, creature, vehicle, building, or decorative piece.

---

## 6.13 Token

**Definition:**  
A small physical marker representing a state, resource, objective, effect, or ownership.

---

## 6.14 Counter

**Definition:**  
A value-tracking object or UI representation.

A Counter may be:

- Numeric.
- Dial-based.
- Token-backed.

A Counter is not always a physical Token.

---

## 6.15 Dice

**Definition:**  
A randomizable tabletop object with defined faces and outcomes.

Use `Dice` for the object category.  
Use `Die` only when referring to exactly one object in grammatical prose.

---

## 6.16 Note

**Definition:**  
A text-bearing tabletop object used for rules, reminders, labels, or player-authored information.

---

# 7. Container and Collection Terms

## 7.1 Container

**Definition:**  
A logical structure that owns or organizes Object Instances.

Examples:

- Deck.
- Hand.
- Discard pile.
- Stack.
- Bag.
- Console slot collection.

An Object Instance may belong to at most one owning Container at a time.

A Zone is not automatically a Container.

---

## 7.2 Deck

**Definition:**  
An ordered card Container intended for drawing, shuffling, splitting, merging, or dealing.

A Deck is not implemented as a simple `Stack<T>` because physical tabletop operations require more than top-only access.

---

## 7.3 Stack

**Definition:**  
An ordered collection of cards or stackable objects created or modified during play.

A Stack may be:

- A temporary pile.
- A manually created group.
- A discard pile representation.

A configured Deck and a player-created Stack are related but not identical concepts.

---

## 7.4 Hand

**Definition:**  
A private card Container owned by a Seat or Player.

A Hand may provide:

- Private visibility.
- Card ordering.
- Fan or row layout.
- Card play and receipt.

---

## 7.5 Discard Pile

**Definition:**  
A Container used for objects that have been discarded or resolved.

A Discard Pile may be returned to a Deck according to the Game’s rules or player choice.

---

## 7.6 Console

**Definition:**  
A persistent personal interaction and storage area associated with a Seat.

A Console may contain:

- Slots.
- Stored Move Cards.
- Game-specific cards.
- Status information.
- Button-related controls.
- Personal organizational areas.

The Console is separate from the Hand.

---

# 8. Interaction Terms

## 8.1 Interaction

**Definition:**  
A player action involving selection, manipulation, inspection, or transfer of a Tabletop Object.

---

## 8.2 Interaction Intent

**Definition:**  
The action the system infers from input context before committing a state change.

Examples:

- Select object.
- Move card.
- Draw top card.
- Move entire Deck.
- Rotate.
- Flip.
- Split Stack.

---

## 8.3 Interaction State

**Definition:**  
The current state of the local interaction state machine.

Examples:

- Idle.
- Hovering.
- Pressed.
- DraggingObject.
- DraggingDeck.
- Rotating.
- SelectingMultiple.
- Cancelling.

---

## 8.4 Interaction Lock

**Definition:**  
A temporary authority claim preventing conflicting simultaneous manipulation of the same object or collection.

An Interaction Lock is not permanent ownership.

---

## 8.5 Selection

**Definition:**  
The local indication that one or more objects are targeted for interaction.

Selection state is usually temporary and may not be part of the authoritative Match snapshot.

---

## 8.6 Manipulation

**Definition:**  
Direct player control of an object’s pose or state, such as moving, rotating, or flipping.

---

## 8.7 Command

**Definition:**  
A serializable request describing one meaningful attempted state change.

Examples:

- MoveObjectCommand.
- DrawCardCommand.
- FlipCardCommand.
- ShuffleDeckCommand.
- PlaceInConsoleCommand.

A Command contains data and identifiers, not Unity GameObject references.

---

## 8.8 Command Result

**Definition:**  
The explicit outcome of processing a Command.

A Command Result may represent:

- Success.
- Failure.
- Rejection.
- Conflict.
- Stale revision.
- Missing object.
- Locked object.

Expected invalid actions should use Command Results rather than exceptions.

---

## 8.9 Domain Event

**Definition:**  
A typed notification stating that an authoritative state change has occurred.

Examples:

- ObjectMoved.
- CardFlipped.
- DeckShuffled.
- SeatAssigned.

A Domain Event describes something that happened. A Command requests that something happen.

---

# 9. Policy and Enforcement Terms

## 9.1 Policy

**Definition:**  
A configurable rule that determines whether an action is allowed, suggested, warned about, or blocked.

Policies allow future restriction without hardcoding all Game rules into the initial Platform.

---

## 9.2 Enforcement Level

**Definition:**  
The general strength with which a Policy affects player actions.

Canonical levels:

### Free

No Game-rule blocking. Technical integrity still applies.

### Assisted

The system provides guidance or warnings but normally allows the action.

### Restricted

Selected configured actions are blocked.

### Enforced

A complete implemented ruleset controls relevant actions and progression.

---

## 9.3 Technical Invariant

**Definition:**  
A structural rule that is always enforced to prevent corrupt or impossible state.

Examples:

- One Object Instance cannot belong to two Containers.
- A missing object cannot be moved.
- One Command ID cannot be applied twice.
- Unauthorized private information cannot be sent to another Player.
- Two Players cannot commit control of the same locked object simultaneously.

Technical Invariants are not optional Game rules.

---

## 9.4 Game Rule

**Definition:**  
A rule describing how a particular Game is intended to be played.

Examples:

- Draw six cards.
- Move only three cells.
- Take turns clockwise.
- Discard a Move Card after use.

Game Rules are initially followed socially unless a Policy or future rule module enforces them.

---

## 9.5 Interaction Policy

**Definition:**  
A Policy controlling which physical object interactions are permitted.

---

## 9.6 Placement Policy

**Definition:**  
A Policy controlling snapping, bounds, occupancy, or placement restrictions.

---

## 9.7 Visibility Policy

**Definition:**  
A Policy controlling which Players may receive or display specific object information.

---

## 9.8 Ownership Policy

**Definition:**  
A Policy controlling who may manipulate or access particular objects or Containers.

---

## 9.9 Rule Enforcement Policy

**Definition:**  
A Policy controlling whether implemented Game Rules are free, assisted, restricted, or enforced.

---

# 10. State and Persistence Terms

## 10.1 Runtime State

**Definition:**  
Mutable data representing the current Match.

Runtime State must remain separate from static Object Definitions and Unity views.

---

## 10.2 Authoritative State

**Definition:**  
The official accepted Match state from which all clients render and recover.

Depending on the networking architecture, authority may belong to:

- Player host.
- Dedicated server.
- Backend service.

The selected technology does not change the meaning of Authoritative State.

---

## 10.3 View

**Definition:**  
A Unity-facing visual representation of Runtime State.

Examples:

- CardView.
- DeckView.
- PawnView.
- ConsoleView.

A View is not authoritative and must not independently decide Game rules.

---

## 10.4 Snapshot

**Definition:**  
A serializable representation of authoritative Match state at a specific revision.

Snapshots may support:

- Reset.
- Save and load.
- Reconnection.
- Host migration.
- Debugging.
- Recovery.

---

## 10.5 Initial Snapshot

**Definition:**  
The starting state generated from a Game Template when a Match begins.

Resetting the Match restores the Initial Snapshot unless another reset policy is selected.

---

## 10.6 Match Snapshot

**Definition:**  
A Snapshot of the current live Match state.

---

## 10.7 Revision

**Definition:**  
A monotonically increasing state version used to order authoritative changes and detect stale Commands or Snapshots.

---

## 10.8 Definition Data

**Definition:**  
Static authored content describing object types and template content.

Definition Data may be stored in ScriptableObjects or other serialized assets.

---

## 10.9 Instance Data

**Definition:**  
Mutable per-Match data belonging to Object Instances, Seats, Containers, and other Runtime State.

---

# 11. Architecture Terms

## 11.1 Domain

**Definition:**  
Plain C# models, rules, invariants, identifiers, and state representing the tabletop problem space.

The Domain must not depend on Unity scenes, UI, or a specific networking package.

---

## 11.2 Application Layer

**Definition:**  
The layer that coordinates use cases, Commands, Policies, and Domain state changes.

---

## 11.3 Presentation Layer

**Definition:**  
Unity-specific views, input adapters, camera controls, UI, animation, and feedback.

---

## 11.4 Infrastructure

**Definition:**  
Technical integrations such as networking, persistence, logging, authentication, and analytics.

---

## 11.5 Adapter

**Definition:**  
An implementation that connects a Platform abstraction to a specific technical system.

Examples:

- Photon networking adapter.
- NGO networking adapter.
- File persistence adapter.

---

## 11.6 Composition Root

**Definition:**  
The single startup location where dependencies and implementations are constructed and connected.

The Composition Root must not become a general-purpose global service locator.

---

## 11.7 Capability

**Definition:**  
A reusable behavior or feature composed onto a Tabletop Object type.

Examples:

- Movable.
- Rotatable.
- Flippable.
- Stackable.
- Randomizable.
- Snappable.
- Lockable.

---

# 12. Canonical Distinctions

These pairs must not be treated as interchangeable.

| Term A | Term B | Distinction |
|---|---|---|
| Platform | Game Template | Reusable systems vs. starting configuration |
| Game | Game Template | Ruleset/experience vs. saved setup |
| Game Template | Match | Starting data vs. live runtime instance |
| Match | Multiplayer Session | Play state vs. connection lifecycle |
| Player ID | Network Connection ID | Stable identity vs. temporary connection |
| Seat | Player | Stable table position vs. human participant |
| Object Definition | Object Instance | Static type data vs. runtime copy |
| Card Definition | Card Instance | Card type vs. one physical copy |
| Board | Play Area | Visual object vs. logical organization region |
| Zone | Container | Placement region vs. ownership structure |
| Deck | Stack | Configured ordered draw collection vs. general pile |
| Hand | Console | Private card collection vs. personal storage/interface area |
| Command | Domain Event | Requested action vs. completed state change |
| View | Runtime State | Visual representation vs. authoritative data |
| Game Rule | Technical Invariant | Social/configurable rule vs. mandatory state integrity |
| Snapping | Placement Enforcement | Suggested alignment vs. blocking invalid placement |
| Authority | Hosting | Who owns truth vs. where process runs |
| Server-authoritative | Dedicated server | State-control model vs. hosting topology |

---

# 13. Deprecated or Discouraged Terms

Avoid these terms unless a specific document defines a narrow technical use.

| Avoid | Use instead |
|---|---|
| Preset | Game Template |
| Game mode | Game Template or Match configuration |
| Infinite table | Effectively unbounded Virtual Tabletop |
| Parallax table | Seamless Table Surface |
| Player slot | Seat |
| Client ID as player identity | Player ID |
| Card data | Card Definition or Card Instance State |
| GameObject state | View state or Runtime State |
| GameManager | Name the actual responsibility |
| Rule engine | Policy system or future game-specific rule module |
| Grid system for all games | Optional Play Area layout |
| Game scene | Tabletop scene, content scene, or Game Template content |
| Full freedom forever | Freedom by default, enforcement by configuration |
| Restriction system | Policy and Enforcement system |
| Sync everything | Synchronize authoritative state and required previews |
| Multiplayer object owns logic | Network adapter or authoritative Domain state |

---

# 14. Naming Guidance for Code

Use these suffixes consistently where applicable:

| Suffix | Meaning |
|---|---|
| `Definition` | Static authored data |
| `State` | Mutable runtime domain data |
| `Id` | Strong identifier |
| `Command` | Requested state change |
| `Result` | Explicit operation outcome |
| `Event` | Completed domain occurrence |
| `Policy` | Configurable decision rule |
| `Service` | Cohesive application or infrastructure operation |
| `View` | Unity visual representation |
| `Adapter` | Integration implementation |
| `Factory` | Controlled object construction |
| `Repository` | Persistence abstraction, only when actually needed |
| `Snapshot` | Serializable state capture |
| `Config` | Technical configuration, not a Game Template |
| `Controller` | Input or orchestration role with a clear scope |

Avoid vague names such as:

- Manager.
- Handler.
- Helper.
- Utility.
- System.

These names may be used only when the responsibility remains specific and obvious, such as `InteractionStateMachine` or `HostMigrationCoordinator`.

---

# 15. Approval Rule

After this document is approved:

1. New architecture and implementation documents must use these terms.
2. Existing documents must be corrected when terminology conflicts.
3. Codex must not introduce alternative vocabulary without explicit approval.
4. Any genuinely new core term must be added here before it becomes part of public architecture or APIs.
