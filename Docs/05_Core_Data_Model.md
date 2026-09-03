# Console Cards — Core Data Model

**Document ID:** 05_Core_Data_Model  
**Version:** 1.2
**Status:** Approved

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams in this document are illustrative unless explicitly labelled **Approved Contract**. Codex must not treat illustrative examples as fixed public APIs.

## 1. Goals

The model must:

- Represent a freeform tabletop independently of Unity scenes.
- Support Game Templates, Matches, saving, reset, and multiplayer.
- Preserve unique Object Instances and Container membership.
- Support future object categories without one class per Game.
- Remain serializable and versionable.

## 2. Strong Identifiers

Use dedicated value types rather than unstructured strings.

```text
GameTemplateId
MatchId
SessionId
PlayerId
SeatId
TabletopObjectId
ObjectDefinitionId
ContainerId
PlayAreaId
ZoneId
CommandId
SnapshotId
```

Identifiers must:

- Be stable within their intended lifetime.
- Support serialization.
- Implement value equality.
- Never depend on a scene hierarchy path.

## 3. Coordinates

### 3.1 Foundation Coordinate

The first implementation uses a minimal logical two-dimensional coordinate independent of Unity transforms.

```csharp
public readonly record struct TableCoordinate(
    double X,
    double Y);
```

This is illustrative, not an Approved Contract.

The foundation must keep logical coordinates separate from rendered Unity positions. Sectoring, floating-origin rebasing, and chunk coordinates are deferred until the Virtual Table prototype demonstrates that they are required.

### 3.2 Tabletop Pose

A Tabletop Pose conceptually contains:

- Table Coordinate.
- Rotation.
- Layer.
- Local order.

Runtime scaling is excluded until explicitly approved.

`TabletopPose` remains the authored/template/container-layout model. It must not be expanded to carry loose-object height, full 3D orientation, or physical motion.

### 3.3 Loose Physical Pose and State

ADR-025 approves separate authoritative 3D physical pose/state for loose Cards, Pawns, Tokens, and Dice, associated with their existing stable Object IDs in `MatchState`.

It represents full 3D position and rotation, plus the motion/lifecycle state needed for held/kinematic and released/dynamic behavior, including linear/angular motion where needed. These are serializable Runtime values, not Unity Rigidbody/Transform references or a replacement for `TabletopPose`. Physical coordinates correspond to the shared 3D tabletop world, independently of the local Camera.

The current placement owner is explicit: Container membership/layout controls a contained Card with loose physics disabled; accepted extraction restores physical control. Authored/template/layout poses remain available without competing with accepted loose physical state.

Commands or approved Application Use Cases accept simulation outcomes with the existing actor context, Technical Invariants, and revision boundary. Settling commits the final 3D pose; a Die's settled pose and resolved value commit together. Off-table physical state is valid and must not be rejected merely because no Table/Board is beneath it. In future multiplayer, host/server physics is the accepted source of simulation outcomes; clients do not independently decide them.

This section approves the state separation and authority model, not a fixed API or field layout. Snapshot compatibility follows the existing versioning rules; scene objects alone are not a recovery source.

## 4. Definitions

### 4.1 Object Definition

```text
ObjectDefinition
- ObjectDefinitionId
- DisplayName
- Category
- VisualReference
- Dimensions
- DefaultRotation
- Tags
- CapabilitySet
- Metadata
```

### 4.2 Card Definition

Extends object content conceptually with:

- Front visual.
- Back visual.
- Card category.
- Optional text.
- Default face.
- Physical dimensions.

### 4.3 Definition Rules

Definitions are immutable during a Match.

Definitions must not contain:

- Current owner.
- Current pose.
- Current Container.
- Current visibility.
- Current face state.
- Current stack order.

## 5. Runtime Object State

### 5.1 Base Tabletop Object State

The first foundation uses an explicit base state containing only universal data:

- Tabletop Object ID.
- Object Definition ID.
- Tabletop Pose.
- Optional owning Container ID.
- Optional owner Player ID.
- Visibility state.
- Persistent user-lock state.

Do not introduce a generic `ObjectStatePayload`, arbitrary dictionary, reflection-driven component store, or custom ECS in the foundation.

### 5.2 Typed Object State

Object-specific state is introduced as explicit typed models only when required by a milestone.

Initial typed models:

- `CardInstanceState`
- `PawnState`
- `TokenState`
- `ContainerState`

The Session Entry + Component Toolbox Foundation adds explicit `DieState`. ADR-025 adds the separate physical state described in §3.3 for the existing loose Card/Pawn/Token/Die identities; it does not introduce arbitrary payloads. Later object types such as Bags, Miniatures, or specialized Counters add typed state only through an approved milestone and Architecture Decision where necessary.

## 6. Card State

```text
CardInstanceState
- TabletopObjectId
- ObjectDefinitionId
- FaceState
- ContainerId?
- OwnerPlayerId?
- VisibilityState
- OrderIndex
- TabletopPose
```

Canonical face states:

- FaceUp
- FaceDown

Future multi-face cards require an Architecture Decision.

## 7. Pawn, Token, and Die State

### 7.1 Pawn State

A Pawn uses Base Tabletop Object State plus optional Seat or Player association. No automated movement rules are included.

### 7.2 Token State

A basic Token uses Base Tabletop Object State and optional stack/count metadata only when the milestone requires it.

Tokens validate that reusable object handling is not limited to Cards.

### 7.3 Die State

A Die uses Base Tabletop Object State plus:

- side count;
- authoritative current/result value; and
- any minimal roll revision/status needed to project an accepted result safely.

Its stable Tabletop Object ID and authored/layout Tabletop Pose come from Base Tabletop Object State. A Die created by a Game Template and one created by the component toolbox use the same typed Runtime State and separate loose physical state. Its settled physical orientation determines the accepted value through an explicit authored face/value mapping for its d4/d6/d8/d10/d12/d20 variant. The mapping includes the result-reading convention and belongs to immutable content/configuration, not mutable Match data; values must not be inferred from mesh triangle order or names.

## 8. Containers

```text
ContainerState
- ContainerId
- ContainerType
- OwnerSeatId?
- VisibilityMode
- Ordered Object IDs
- Capacity?
- Configuration
```

Container types include:

- Deck.
- Stack.
- Hand.
- DiscardPile.
- Bag.
- ConsoleCollection.
- GenericCollection.

### 7.1 Invariants

- One Object Instance belongs to zero or one owning Container.
- Ordered Containers contain unique Object IDs.
- Order indices are derived from Container ordering where possible.
- Moving an object between Containers is atomic.
- Contained Cards are positioned by Deck/Stack/Hand/Console/Slot or other Container layouts with loose physics disabled. Accepted extraction restores loose physical behavior without changing identity or duplicating membership.
- Deleting a Container requires an explicit policy for its contents.

## 9. Deck State

A Deck is an ordered card Container with additional semantic operations:

- Draw top.
- Draw count.
- Insert top.
- Insert bottom.
- Split.
- Merge.
- Shuffle.
- Reveal count.

Do not expose internal mutable lists.

## 10. Stack State

A Stack is an ordered collection created or modified during play.

A Stack may retain a shared tabletop pose plus per-card visual offsets.

Logical ordering must not depend on transform sibling order.

## 11. Hand State

```text
HandState
- ContainerId
- OwnerSeatId
- Ordered Card IDs
- LayoutPreference
- VisibilityMode
```

Hand order may be visible only to the owner.

Private clients must receive only authorized data.

## 12. Console State

```text
ConsoleState
- SeatId
- ConsoleDefinitionId
- Slot States
- Optional Generic Zones
```

```text
ConsoleSlotState
- SlotId
- Accepted Capability/Tag Filters
- Capacity
- Ordered Object IDs
```

The first build should keep Game-rule validation free. Slot structural validity may still apply.

## 13. Seat State

```text
SeatState
- SeatId
- OccupantPlayerId?
- ConnectionBinding?
- SeatStatus
- Table Orientation
- HandContainerId
- ConsoleState
- CameraBookmarks
```

Seat status:

- Vacant.
- Reserved.
- Occupied.
- TemporarilyDisconnected.

Temporary connection IDs are infrastructure data and must not replace Player ID.

## 14. Play Area State

```text
PlayAreaState
- PlayAreaId
- Definition Reference
- TabletopPose
- Bounds
- LayoutType
- LayoutState
- Zone IDs
- Policy References
```

Layout-specific state may include:

- Grid dimensions.
- Track positions.
- Side-scroller section indices.
- Tile occupancy.

## 15. Zone State

A Zone is a placement region and may reference a Container, but does not always own objects.

```text
ZoneState
- ZoneId
- PlayAreaId?
- Shape
- TabletopPose
- LayoutPreference
- ContainerId?
- Visibility
```

## 16. Match State

```text
MatchState
- MatchId
- GameTemplateId?
- SchemaVersion
- Revision
- Players
- Seats
- Object Instances
- Containers
- Play Areas
- Policies
- Match Metadata
- Random State
```

Match State must provide indexed lookup by stable ID.

## 17. Command Envelope

```text
CommandEnvelope
- CommandId
- MatchId
- RequestedByPlayerId
- ExpectedRevision?
- CommandType
- Payload
```

Processed Command IDs are retained for duplicate protection within a defined window or snapshot history.

## 18. Result Model

```text
CommandResult
- Status
- ErrorCode?
- Message?
- AcceptedRevision?
- Domain Events
```

Canonical statuses:

- Accepted.
- Rejected.
- Conflict.
- Invalid.
- Unauthorized.
- Stale.

## 19. Visibility

Visibility is data authorization, not only renderer state.

Suggested modes:

- Public.
- OwnerOnly.
- SeatOnly.
- AuthorityOnly.
- HiddenIdentityWithPublicBack.
- CustomPolicy.

Private Definition data must not be distributed to unauthorized clients.

## 20. Random State

Logical random operations such as shuffle use an injected random source. Physical Dice may use randomized impulses and torque to initiate a throw; randomness does not preselect the accepted face value.

A Match may store:

- Seed.
- Sequence position.
- Last accepted result.

The authoritative implementation determines the official shuffle result. For physical Dice, actor-aware Roll validates through the Application boundary, the authority runs the physical throw, and the settled-face mapping supplies the result. Manual grab/throw follows the same settlement path. The final 3D pose and accepted value commit together to Runtime State; clients do not independently resolve authoritative faces. Accepted outcomes are stored for synchronization/recovery rather than reconstructed from deterministic physics replay.

## 21. Mutation Rules

State mutation occurs through Domain methods or Application transactions.

Forbidden:

- Public settable collections.
- View-owned state changes.
- Direct mutation from networking callbacks.
- Definition asset mutation during a Match.

## 22. Serialization Compatibility Rules

Before the persistence milestone, state types must be serialization-compatible:

- No direct Unity object references.
- No networking-vendor types.
- Stable identifiers for content references.
- Controlled collections and explicit values.

Actual Snapshot DTOs, file formats, checksums, storage, and schema migration belong to the persistence milestone.

## 23. Initial Implementation Boundary

Implement first:

- IDs.
- Table Coordinate.
- Tabletop Pose.
- Object Definition references.
- Tabletop Object State.
- Card State.
- Pawn State.
- Token State.
- Container State.
- Deck, Stack, Hand.
- Seat and Console State.
- Match State.
- Commands and Results.

M0 implements `PlayAreaId` only. `PlayAreaState`, Play Area layout state, Player Layout state, Zones, Slots, Grids, and other Play Area runtime models are deferred to M4 - Play Area and Player-Layout Foundation. Minimum Game Template loading follows in M4.1.

Add the minimum first-class Die State in the Session Entry + Component Toolbox Foundation. ADR-025 additionally approves separate loose physical state and explicit authored face/value mappings for the six standard Die variants; this is not a custom-die editor. Defer user-customized Dice, detailed Miniature, Bag, Spinner, and speculative randomizer state until an approved milestone requires them.
