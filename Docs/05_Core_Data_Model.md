# Console Cards — Core Data Model

**Document ID:** 05_Core_Data_Model  
**Version:** 1.0 Draft  
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

Later object types such as Dice, Bags, Miniatures, or Counters add typed state through an approved milestone and Architecture Decision where necessary.

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

## 7. Pawn and Token State

### 7.1 Pawn State

A Pawn uses Base Tabletop Object State plus optional Seat or Player association. No automated movement rules are included.

### 7.2 Token State

A basic Token uses Base Tabletop Object State and optional stack/count metadata only when the milestone requires it.

Tokens validate that reusable object handling is not limited to Cards.

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

Random operations use an injected random source.

A Match may store:

- Seed.
- Sequence position.
- Last accepted result.

The authoritative implementation determines the official shuffle or dice result.

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

M0 implements PlayAreaId only. PlayAreaState, Play Area layout state, Zones, Slots, Grids, and other Play Area runtime models are deferred to M4 — Play Areas and Game Template Loading.

Defer detailed Miniature, Bag, Spinner, and advanced Dice state until a milestone requires them.
