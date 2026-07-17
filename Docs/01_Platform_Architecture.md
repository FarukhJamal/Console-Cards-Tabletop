# Console Cards — Platform Architecture

**Document ID:** 01_Platform_Architecture  
**Version:** 1.0 Draft  
**Status:** Approved with Open Decisions
**Depends on:** `00_Product_Vision.md`, `02_Terminology.md`, `03_Project_Principles.md`

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams in this document are illustrative unless explicitly labelled **Approved Contract**. Codex must not treat illustrative examples as fixed public APIs.

## 1. Architectural Style

Console Cards uses a **modular layered monolith**.

It is one Unity application divided into modules with explicit dependency direction. It borrows useful ideas from Clean Architecture without adopting unnecessary enterprise ceremony.

The architecture combines:

- Plain C# Domain State.
- Application Use Cases and Commands.
- Unity Presentation.
- Infrastructure adapters.
- Data-driven Game Templates.
- Strategy-based Play Areas and Policies.
- Replaceable networking.

## 2. Top-Level Model

```text
Console Cards Platform
│
├── Domain
│   ├── IDs
│   ├── Tabletop Object State
│   ├── Containers
│   ├── Seats
│   ├── Match State
│   └── Technical Invariants
│
├── Application
│   ├── Commands
│   ├── Use Cases
│   ├── Policies
│   ├── Results
│   └── Domain Events
│
├── Platform Modules
│   ├── Tabletop Objects
│   ├── Interaction
│   ├── Play Areas
│   ├── Hands and Consoles
│   ├── Game Templates
│   └── Persistence Contracts
│
├── Presentation
│   ├── Views
│   ├── Camera
│   ├── Input
│   ├── Animation
│   └── UI
│
├── Infrastructure
│   ├── Persistence
│   ├── Networking Adapters
│   ├── Authentication
│   ├── Logging
│   └── Content Loading
│
└── Bootstrap
    └── Composition Root
```

## 3. Dependency Direction

Dependencies point inward toward stable concepts.

```text
Core Domain
    ↑
Application
    ↑
Platform Modules
    ↑
Presentation / Infrastructure
    ↑
Bootstrap
```

Rules:

1. Domain references no Unity scene, UI, or networking package.
2. Application references Domain and abstractions.
3. Presentation references Application contracts and view models.
4. Infrastructure implements abstractions.
5. Bootstrap constructs concrete implementations.
6. Game Templates contain data and references; they do not reverse the dependency direction.

## 4. Source of Truth

### 4.1 Runtime State

Plain C# Runtime State is authoritative.

Core examples:

- `TabletopObjectState`
- `CardInstanceState`
- `ContainerState`
- `SeatState`
- `ConsoleState`
- `PlayAreaState`
- `MatchState`

### 4.2 Views

Unity Views render Runtime State.

Examples:

- `CardView`
- `DeckView`
- `PawnView`
- `PlayAreaView`
- `ConsoleView`

Views may animate and preview. They do not directly decide rules or mutate unrelated state.

### 4.3 Definitions

Static content is stored in Definitions, normally authored through ScriptableObjects or versioned serialized assets.

Examples:

- `CardDefinition`
- `ObjectDefinition`
- `GameTemplateDefinition`
- `PlayAreaDefinition`

Definitions are immutable during a Match.

## 5. State-Change Pipeline

```text
Input
→ Interaction Intent
→ Command
→ Application Use Case
→ Technical Invariant Check
→ Policy Evaluation
→ State Mutation
→ Domain Events
→ View Update
→ Persistence/Network Notification
```

A freeform action may pass Policy evaluation automatically, but it still uses the same pipeline.

## 6. Module Responsibilities

### 6.1 Core Domain

Owns:

- Strong identifiers.
- Coordinates and poses.
- Object Instance State.
- Container membership and ordering.
- Seats and ownership.
- Match revisions.
- Technical Invariants.
- Serializable value types.

Must not own:

- Pointer input.
- Unity animations.
- RPCs.
- Scene loading.
- Game-specific rules.

### 6.2 Application

Owns:

- Use Cases.
- Command processing.
- Transactions.
- Policy orchestration.
- Explicit Results.
- Domain Event emission.
- Snapshot requests.

Examples:

- Move object.
- Draw cards.
- Flip card.
- Shuffle deck.
- Transfer object.
- Reset Match.

### 6.3 Tabletop Objects

Owns reusable object behavior and configuration for:

- Cards.
- Decks.
- Stacks.
- Hands.
- Boards.
- Pawns.
- Tokens.
- Dice.
- Tiles.
- Notes.
- Future object categories.

Objects are implemented through common state plus capabilities.

### 6.4 Interaction

Owns:

- Selection.
- Input Intent resolution.
- Interaction State Machine.
- Drag previews.
- Rotation and flip input.
- Multi-selection.
- Interaction cancellation.
- Snap bypass.
- Object-control requests.

### 6.5 Play Areas

Owns:

- Freeform layouts.
- Grid layouts.
- Side-scroller layouts.
- Tracks.
- Zones.
- Placement suggestions.
- Camera focus regions.
- Optional occupancy information.

Play Areas do not inherently enforce Game Rules.

### 6.6 Hands and Consoles

Owns:

- Private Hand layout and visibility.
- Console slots and layout.
- Personal focus targets.
- Transfer between Hand, Tabletop, and Console.
- Universal Button Card presentation.

### 6.7 Game Templates

Owns:

- Template metadata.
- Rulebook references.
- Initial objects and Containers.
- Seats.
- Play Areas.
- Starting poses.
- Default Policies.
- Initial Snapshot generation.

A Game Template is content, not a running Match.

### 6.8 Presentation

Owns:

- GameObject and prefab Views.
- Camera controls.
- UI.
- Animation.
- Audio and visual feedback.
- Local hidden-information presentation.
- View pooling when later justified.

### 6.9 Infrastructure

Owns technical details behind interfaces:

- File persistence.
- Networking transport.
- Authentication.
- Logging.
- Content import.
- Analytics.
- Cloud services.

### 6.10 Bootstrap

Owns dependency composition and startup.

Bootstrap must not become a universal `GameManager`.

## 7. Core Extension Points

Approved extension points include:

- `IPlacementStrategy`
- `IPolicy`
- `ITabletopAuthority`
- `IMatchSnapshotStore`
- `IPlayerIdentityProvider`
- `IRandomSource`
- `IContentResolver`
- `IViewFactory`

New extension points require demonstrated need.

## 8. Transactions

Multi-step operations are atomic.

Example Move Card purchase:

1. Validate cost cards exist.
2. Validate Move Card exists.
3. Validate target Console destination.
4. Prepare all mutations.
5. Commit as one transaction.
6. Emit events.
7. Increment revision.

Failure before commit leaves state unchanged.

## 9. Event Model

Use typed Domain Events.

Examples:

- `ObjectMovedEvent`
- `CardFlippedEvent`
- `DeckShuffledEvent`
- `ObjectTransferredEvent`
- `SeatAssignedEvent`
- `MatchResetEvent`

Avoid unrestricted string event buses.

## 10. Game-Specific Automation

The first Platform does not require Game-specific code modules.

Future automation may use optional modules that depend on Platform contracts.

The Platform must not depend on those modules.

## 11. Networking Boundary

The Domain and Application layers operate without Photon, NGO, or Mirror types.

Networking adapters translate:

- Player requests into Commands.
- Accepted Commands or Snapshots into synchronized messages.
- Connection identity into stable Player ID bindings.

The final networking vendor is selected later through an Architecture Decision.

## 12. Architecture Guardrails

Forbidden:

- Giant `GameManager`.
- Runtime state stored in ScriptableObjects.
- Views sending arbitrary RPCs.
- Public mutable state collections.
- `FindObjectOfType` as dependency injection.
- Game-specific conditionals inside universal modules.
- Scene hierarchy names used as persistent IDs.
- Direct package types inside Domain models.
- Silent partial transactions.

## 13. Change-Resistance Targets

The architecture is acceptable when:

- Replacing card visuals does not change deck logic.
- Adding a new Play Area does not change cards.
- Adding a new Game Template does not change Platform code.
- Replacing networking does not change Runtime State.
- Increasing Console slots changes configuration and presentation, not card architecture.
- Adding controller input changes the input adapter, not Commands.
