# Console Cards — Architecture Decisions

**Document ID:** 04_Architecture_Decisions  
**Version:** 1.0 Draft  
**Status:** Approved with Open Decisions

This file records accepted or proposed Architecture Decision Records. A decision must not be silently reversed during implementation.

## ADR-001 — Modular Layered Monolith

**Status:** Accepted

**Decision:** Use one Unity application split into assemblies and modules with explicit dependency direction.

**Reason:** A microservice-style or package-heavy architecture is unnecessary for the MVP. A scene-driven MonoBehaviour architecture is too fragile for persistence, multiplayer, and multiple Game Templates.

**Consequences:**

- Clear module boundaries.
- One deployment unit.
- Moderate architectural discipline.
- No network or Unity package types in Domain State.

---

## ADR-002 — Runtime State Separate from Views

**Status:** Accepted

**Decision:** Plain C# Runtime State is authoritative. Unity GameObjects are Views.

**Reason:** Scene objects are difficult to serialize, restore, test, migrate, and synchronize reliably.

**Consequences:**

- State-to-view binding is required.
- Views may use local previews.
- Completed actions must be committed through the Application layer.

---

## ADR-003 — Definition and Instance Separation

**Status:** Accepted

**Decision:** Static Definitions are separate from mutable Object Instances.

**Reason:** Multiple cards may share one definition while having unique owner, location, visibility, and face state.

**Consequences:**

- ScriptableObjects store authored Definitions.
- Runtime Match State stores Instances.
- Template and save formats reference stable Definition IDs.

---

## ADR-004 — Command-Based State Changes

**Status:** Accepted

**Decision:** Meaningful state changes are represented as serializable Commands.

**Reason:** Commands support testing, multiplayer, logging, revisions, duplicate protection, snapshots, and future undo.

**Consequences:**

- Direct state mutation from Views is forbidden.
- Continuous drag preview remains local or rate-limited.
- Final placement is a Command.

---

## ADR-005 — Game Templates as Data

**Status:** Accepted

**Decision:** Games initially enter the Platform as Game Templates containing setup data, content, Rulebooks, Play Areas, and default Policies.

**Reason:** The Platform must support official, modified, custom, and empty-table experiences without hardcoded Game logic.

**Consequences:**

- Super Leroy Sisters and Trap Floor do not define Platform code.
- Optional automated rule modules may be added later.
- A Game Template is not a Unity scene.

---

## ADR-006 — Freedom by Default

**Status:** Accepted

**Decision:** First builds use Free enforcement for Game Rules while always preserving Technical Invariants.

**Reason:** The intended experience mirrors physical tabletop play.

**Consequences:**

- Invalid-by-rule actions may be allowed.
- Corrupt state is never allowed.
- Future restrictions are Policies, not foundational rewrites.

---

## ADR-007 — Policy-Based Enforcement

**Status:** Accepted

**Decision:** Interaction, Placement, Visibility, Ownership, and future Rule Enforcement use configurable Policies.

**Reason:** The designer requires unrestricted early builds but wants future restrictions to remain possible.

**Consequences:**

- Policies return explicit decisions.
- Free mode is the first implementation.
- No generic scripting language is introduced.

---

## ADR-008 — Capability Composition

**Status:** Accepted

**Decision:** Tabletop Objects gain reusable capabilities rather than deep class inheritance.

**Reason:** Cards, dice, boards, pieces, and future objects share overlapping behavior.

**Consequences:**

- Capability configuration must remain understandable.
- Avoid an unrestricted entity-component framework.
- Common state remains centralized.

---

## ADR-009 — Effectively Unbounded Tabletop

**Status:** Accepted

**Decision:** Use logical Tabletop Space with a seamless rendered Table Surface around the local Camera. The logical Virtual Tabletop is effectively unbounded and does not move during Camera panning. Camera movement must not move Match State or tabletop objects. Begin with minimal logical two-dimensional coordinates; adopt sectoring or floating-origin behavior only after M1 measurements justify it.

**Reason:** Players must continue placing objects without visible table duplication or a practical edge.

**Consequences:**

- Logical coordinates are independent from visible surface sections.
- Visible surface geometry may be camera-local and repositioned as a Presentation rendering proxy.
- Surface-proxy transforms are Presentation state only, not Match State, object placement authority, Play Area state, or coordinate source.
- Surface visual patterns, markings, and textures must remain anchored to Tabletop Space or world-space X/Z coordinates so proxy repositioning is visually undetectable.
- Large-coordinate precision is measured in M1.
- Sectoring and floating origin remain deferred pending measurement.
- Far objects may be culled visually while remaining in Runtime State.

---

## ADR-010 — Play Areas Are Optional

**Status:** Accepted

**Decision:** The base table has no mandatory Grid. Game Templates may create zero, one, or multiple Play Areas.

**Reason:** Different Games require grids, side-scrollers, tracks, tiles, zones, or no structure.

**Consequences:**

- Grid is one layout strategy.
- Play Areas supply suggestions and focus bounds.
- Freeform placement remains available unless a Policy restricts it.

---

## ADR-011 — Independent Local Cameras

**Status:** Accepted

**Decision:** Each Player normally controls an independent local Camera.

**Reason:** A shared synchronized camera would make a large tabletop unusable for groups.

**Consequences:**

- Camera state is usually not authoritative Match State.
- Focus suggestions may be shared.
- Console and Play Area bookmarks are required.

---

## ADR-012 — Atomic Application Transactions

**Status:** Accepted

**Decision:** Multi-step operations commit entirely or not at all.

**Reason:** Partial card transfers, purchases, or setup operations corrupt state.

**Consequences:**

- Use Cases prepare mutations before commit.
- Failures return explicit Results.
- Tests cover rollback behavior.

---

## ADR-013 — Networking Technology Behind Adapters

**Status:** Accepted

**Decision:** Keep the final choice between Fusion, NGO, or another technology outside Domain and Application code.

**Reason:** The networking decision depends on later cost and continuity requirements.

**Consequences:**

- Package-specific types remain in Infrastructure.
- Authority and Session contracts are defined first.
- Replacing networking is difficult but localized.

---

## ADR-014 — Host or Server Authority

**Status:** Accepted with topology deferred

**Decision:** Shared Match State has one accepted authority at a time.

**Reason:** Deck order, private hands, object uniqueness, Commands, and Snapshots require one official state.

**Consequences:**

- The MVP may use a player host.
- Dedicated or backend authority remains possible later.
- Player-host limitations must be documented.

---

## ADR-015 — Snapshot-Based Recovery

**Status:** Accepted

**Decision:** Reset, reconnect, save/load, and host migration use versioned Match Snapshots.

**Reason:** Reconstructing Runtime State from arbitrary scene objects or incomplete event history is fragile.

**Consequences:**

- Runtime State must be serializable.
- Snapshot schemas require versions.
- Temporary drag and hover state are not persisted.

---

## ADR-016 — Manual Dependency Injection

**Status:** Accepted

**Decision:** Use a Composition Root and constructor injection for plain C# services. Avoid a third-party DI container in the foundation.

**Reason:** A DI framework adds learning and debugging cost without clear MVP benefit.

**Consequences:**

- Bootstrap constructs implementations explicitly.
- MonoBehaviours receive dependencies through controlled bind/init methods where necessary.
- No global service locator.

---

## ADR-017 — No Full Physics Foundation

**Status:** Accepted

**Decision:** Use controlled tabletop interaction and deterministic placement rather than unrestricted rigidbody simulation.

**Reason:** Full physics creates unstable stacks, input ambiguity, and network cost without improving the core card experience.

**Consequences:**

- Dice outcomes may be generated logically and animated.
- Objects can appear physical without physics owning authoritative state.
- Advanced physics is deferred.

---

## ADR-018 — Tests at State Boundaries

**Status:** Accepted

**Decision:** Domain and Application invariants receive Edit Mode tests; Unity interaction receives Play Mode tests; synchronization receives multiplayer tests.

**Reason:** Visual-only testing cannot prove state integrity.

**Consequences:**

- Every milestone includes relevant tests.
- “Compiles” is not sufficient evidence.
- Codex must report exactly which tests were run.


---

## ADR-019 — Explicit Typed Object State

**Status:** Accepted

**Decision:** Begin with explicit Base Object, Card, Pawn, Token, and Container State. Do not create a generic arbitrary Object payload, reflection-driven component store, or custom ECS.

**Reason:** The Foundation needs extensibility without speculative infrastructure.

**Consequences:**

- New object-specific state is added only when a milestone requires it.
- Capability composition begins as controlled configuration and interfaces.
- Generic state frameworks require a later ADR.
