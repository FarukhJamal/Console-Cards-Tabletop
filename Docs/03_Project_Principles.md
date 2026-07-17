# Console Cards — Project Principles

**Document ID:** 03_Project_Principles  
**Version:** 1.0 Draft  
**Status:** Approved
**Depends on:** `00_Product_Vision.md`, `02_Terminology.md`

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams in this document are illustrative unless explicitly labelled **Approved Contract**. Codex must not treat illustrative examples as fixed public APIs.

## 1. Purpose

These principles guide product, design, architecture, implementation, testing, and review decisions. When two possible solutions are technically valid, choose the one that better follows these principles.

## 2. Core Principles

### 2.1 Freedom by default, enforcement by configuration

The first builds must feel like a physical tabletop. Players may move objects, use house rules, and resolve legality socially.

Future restrictions must be added through explicit Policies. They must not require the foundation to be rewritten.

### 2.2 Platform before individual Games

The Platform must remain independent of any specific Game Template.

Super Leroy Sisters, Trap Floor, and future Games are content and validation cases. They must not introduce conditional logic into universal systems.

Forbidden pattern:

```csharp
if (currentGame == GameId.SuperLeroySisters)
{
    // special universal behavior
}
```

### 2.3 State is authoritative; views represent it

Runtime State is the source of truth. Unity GameObjects, transforms, animations, and UI display that state.

A View may preview an action locally, but completed state changes must pass through the Application layer.

### 2.4 Definitions and instances are separate

Static authored data belongs in Definitions. Mutable Match data belongs in Instance State.

ScriptableObjects may store definitions and editor configuration. They must not store current owner, current location, current deck order, current face state, or other live Match data.

### 2.5 Meaningful changes use Commands

Drawing, moving, flipping, shuffling, stacking, transferring, placing, and resetting are represented as Commands.

Commands create a consistent path for:

- Local play.
- Multiplayer.
- Logging.
- Testing.
- Snapshots.
- Reconnection.
- Future undo or replay.

Continuous drag previews are not permanent Commands. The final accepted placement is.

### 2.6 Composition over inheritance

Tabletop Objects share capabilities rather than deep inheritance trees.

Prefer:

```text
Object Definition
+ Movable capability
+ Rotatable capability
+ Flippable capability
+ Stackable capability
```

Avoid:

```text
TabletopObject
  Card
    ButtonCard
      DirectionButtonCard
```

Inheritance is allowed only when substitutability is clear and stable.

### 2.7 Data-driven, not data-everything

Content and configuration should be data-driven where designers need variation.

Do not turn every behavior into a configurable abstraction. A stable technical rule may remain code when configuration adds no real value.

### 2.8 Interfaces at boundaries, not everywhere

Use interfaces when:

- A technical implementation may change.
- A module crosses an architectural boundary.
- Testing needs a substitute implementation.
- Multiple valid strategies exist.

Do not create interfaces mechanically for every class.

### 2.9 Optional structure must not destroy freeform play

Play Areas, Grids, Zones, Tracks, and Slots organize the table. Their default role is guidance.

Players must retain a controlled bypass path unless an approved Policy explicitly restricts placement.

### 2.10 The Platform enforces technical integrity

Free play does not mean corrupt state.

The Platform always protects Technical Invariants, including:

- One Object Instance cannot exist in two Containers.
- One Command cannot be committed twice.
- A missing object cannot be manipulated.
- Private data must not be sent to unauthorized Players.
- Conflicting object control must be resolved.
- Snapshots must have valid versions.

### 2.11 Networking remains replaceable

The Domain, Application layer, Play Areas, Game Templates, and interaction logic must not depend directly on Photon, NGO, Mirror, Relay, or another vendor.

The selected networking technology lives behind adapters and authority contracts.

### 2.12 Persistence is designed from the start

State models must be serializable and versionable.

Do not postpone all save, reset, reconnect, and migration concerns until after Runtime State is tied to scene objects.

### 2.13 Make invalid states difficult to represent

Use strong IDs, explicit Container membership, controlled mutation, and clear Result types.

Do not expose public mutable collections or permit unrelated systems to edit state directly.

### 2.14 Prefer deterministic outcomes

Deck order, shuffle results, dice results, and state transitions should be reproducible where required by authority, testing, or recovery.

Visual animation may vary. Accepted outcomes must not.

### 2.15 Build the smallest complete vertical foundation

Each milestone must produce a demonstrable, coherent result.

Avoid broad scaffolding for future systems that are not yet used.

### 2.16 No hidden scope expansion

A task must not silently add:

- New packages.
- New major modules.
- New scenes.
- New public APIs.
- Future milestone features.
- New Game-specific automation.

Unrequested expansion must be reported as a proposal, not implemented.

### 2.17 Readability over cleverness

Prefer direct, testable code over compressed abstractions, reflection-heavy systems, or generalized frameworks.

A future developer should understand the intent from names, types, and tests.

### 2.18 Fail explicitly

Expected failures use typed Results or errors.

Do not silently ignore invalid Commands. Do not claim success when only part of a transaction completed.

### 2.19 Atomic operations remain atomic

Operations such as purchasing a Move Card must either complete fully or leave state unchanged.

Partial mutation is unacceptable.

### 2.20 Unity best practices remain subordinate to project architecture

Use Unity features where appropriate, but do not allow convenience APIs to collapse architectural boundaries.

Avoid scene searches, static global state, mutable ScriptableObject Match state, and uncontrolled MonoBehaviour-to-MonoBehaviour dependencies.

## 3. Decision Priority

When principles conflict, use this order:

1. Correct state and data integrity.
2. Product vision and player freedom.
3. Clear module boundaries.
4. Testability and recoverability.
5. Simplicity.
6. Performance.
7. Future extensibility.

Performance may move higher only when profiling demonstrates a real problem.

## 4. Approval Rule

After approval, implementation changes that violate these principles require an explicit Architecture Decision and documented rationale.
