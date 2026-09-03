# Console Cards — Architecture Decisions

**Document ID:** 04_Architecture_Decisions  
**Version:** 1.6

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
- Player-initiated Commands preserve the requesting actor/Player context needed for later authority and permission validation.
- Continuous drag preview remains local or rate-limited.
- Final placement is a Command.

---

## ADR-005 — Game Templates as Data

**Status:** Accepted

**Decision:** Games initially enter the Platform as Game Templates containing setup data, content, Rulebooks, Play Areas, and default Policies.

**Reason:** The Platform must support official, modified, custom, and empty-table experiences without hardcoded Game logic.

**Consequences:**

- Trap Floor and Super Leroy Sisters do not define Platform code.
- Optional Game-specific assistance modules may be added where they provide demonstrated value; they are not required for a Game Template to be playable.
- Generic physical tabletop actions remain Platform capabilities and do not move into Game modules.
- A Game Template is not a Unity scene.

---

## ADR-006 — Freedom by Default

**Status:** Accepted

**Decision:** Console Cards is primarily a freeform Virtual Tabletop. Game Rules are primarily interpreted and enforced by Players, while the Platform always preserves Technical Invariants.

**Reason:** The intended experience mirrors physical tabletop play.

**Consequences:**

- Invalid-by-rule actions may be allowed.
- Corrupt state is never allowed.
- Optional Assisted Actions must not disable the underlying Freeform Actions.
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

**Status:** Superseded by ADR-024

**Historical decision:** The original decision used an effectively unbounded logical tabletop with camera-local rendered surface coverage.

**Disposition:** This decision no longer authorizes an unbounded placement surface, camera-following Table geometry, or a camera-local Table Surface proxy. ADR-024 replaces it, with the loose-object placement model subsequently superseded by ADR-025. The authored-layout coordinate mapping and the rule that Camera movement must not move Match State remain preserved.

---

## ADR-010 — Play Areas Are Optional

**Status:** Accepted

**Decision:** The base table has no mandatory Grid. Game Templates may create zero, one, or multiple Play Areas.

**Reason:** Different Games require grids, side-scrollers, tracks, tiles, zones, or no structure.

**Consequences:**

- Grid is one layout strategy.
- Play Areas supply suggestions and focus bounds.
- Freeform loose-object creation uses valid physical Table/Board surfaces under ADR-025. Play Areas remain optional guidance, not collision surfaces or a reason to snap back an off-table physical release. Non-physical Container positioning retains ADR-024's authored Table boundary.

---

## ADR-011 — Independent Local Cameras

**Status:** Accepted

**Decision:** Each Player normally controls an independent local Camera.

**Reason:** A shared synchronized camera would make a large tabletop unusable for groups.

**Consequences:**

- Camera state is usually not authoritative Match State.
- Camera movement does not reposition, rotate, or scale the physical Table or its authored usable area.
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
- Local/offline implementations preserve the same request -> validation -> authoritative mutation boundary without requiring a networking package.

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

**Status:** Accepted in part; conflicting interaction/physics restrictions superseded by ADR-025

**Historical decision:** Use controlled tabletop interaction and deterministic placement rather than unrestricted Rigidbody simulation; logically generate Dice outcomes and animate them.

**Superseded:** The prohibition on loose-object Rigidbody interaction, the restriction of physics to visual feedback, and the blanket deferral of physical Dice. ADR-025 approves physical Card, Pawn, Token, and Die interaction and settled-face Dice results.

**Preserved:** The scope is not full-scene simulation. Deck/Stack/Console bodies retain their existing positioning system, contained Cards remain layout-controlled, and accepted Match State remains separate from Unity objects and changes through the Application boundary.

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

---

## ADR-020 - Universal Console, Game-Specific Game Board

**Status:** Accepted

**Decision:** Keep the Console as a universal Platform system and represent the central Game Board, Board layout, and Play Areas as Game-specific Game Template content.

**Reason:** Players should learn the Console once while each Game remains free to present its own central Board type and layout.

**Consequences:**

- Trap Floor and Super Leroy Sisters use separate Game-specific Boards.
- Game Templates may configure Console contents without replacing the Console contract.
- The Platform must not force one reference screenshot, Grid, or Board layout onto every Game.
- Game Board content must not introduce Game-specific dependencies into universal Console modules.

---

## ADR-021 - Stable Table with Configurable Player Layouts

**Status:** Accepted with mappings deferred

**Decision:** Support one to eight Players by repositioning Seats around a stable central play space. Do not enlarge the table as Player count increases.

**Reason:** Table growth reduces Card readability and weakens the centered core-gameplay composition.

**Consequences:**

- Standard four-Player, eight-Player, and compact four-Player layouts are required.
- Smaller groups move toward the central action rather than occupying distant unused edge positions.
- Player Layout configuration is separate from Game Board and Play Area configuration.
- Exact mappings for one to three and five to seven Players remain in `OPEN_DECISIONS.md`.

---

## ADR-022 - Explicit Session Entry and Empty Table

**Status:** Accepted

**Decision:** Application startup presents an explicit choice between an Empty/Custom Table and available Game Templates before constructing a Match. No official Game, including Trap Floor, is automatically selected as permanent product behavior.

**Reason:** Console Cards is the Platform; official Games are selectable content, and freeform/custom tabletop use is a first-class product workflow.

**Consequences:**

- An Empty/Custom Table may construct a valid Match without a Game-specific Board, Game-specific rules, or selected official Game Template.
- Trap Floor uses the M4.1 Game Template pipeline only after player selection.
- The current automatic Trap Floor prototype bootstrap is temporary and must be replaced.
- Final Session Entry UI styling remains deferred.

---

## ADR-023 - Authoritative Toolbox Components and Dice

**Status:** Accepted in part; Dice-result and loose-placement restrictions superseded by ADR-025

**Preserved decision:** Generic components added through the in-session component toolbox become first-class authoritative object/container instances. Template-created and toolbox-created pieces share stable identity, Runtime State, actor-aware Commands, and Match revisions.

**Reason:** Empty/custom games and house rules require reusable pieces that remain compatible with later persistence and host/server-authoritative multiplayer.

**Consequences:**

- Initial toolbox categories are Card, Deck, Stack/pile, Pawn/meeple, Token/counter, and Die.
- Toolbox-created and Template-created pieces share stable identity and Runtime State contracts.
- A Die records side count and authoritative current/result value and has a Presentation View. Its authored/layout `TabletopPose` remains; loose physical pose/state is separate under ADR-025.
- Initial common Die options are d4, d6, d8, d10, d12, and d20; no custom-die editor is implied.
- Roll follows actor request -> authoritative validation -> physical roll -> settled-face resolution -> authoritative pose/value commit under ADR-025. RNG-only result selection with Presentation-only settling is no longer the required model.
- Trap Floor's two d6 are generic Platform Dice that its Game-specific Floorfall logic interprets as X and Y.
- Networking implementation, persistence milestones, and speculative component categories remain deferred. The scoped physical-object system is approved by ADR-025, not covered by the former blanket physics deferral.

---

## ADR-024 - Fixed Physical Table and Authored Placement Boundary

**Status:** Accepted in part; loose-object plane/boundary/release rules superseded by ADR-025

**Supersedes:** ADR-009

**Preserved decision:** Console Cards uses one real, fixed physical Table. The local Camera moves independently and never repositions, rotates, or scales the Table or Match objects. Surface authoring is inspector-editable, follows the Table Transform/scale, and is independent of decorative mesh geometry. `TableCoordinate` and `TabletopPose` remain the authored/template/container-layout model. Freeform and house-rule play remain available without Game-rule enforcement.

**Superseded:** Loose Card/Pawn/Token/Die placement no longer projects onto a mathematical plane and then tests a two-dimensional Table boundary. Physical release is no longer rejected or rolled back solely for being outside that boundary. Accepted loose poses are no longer limited to `TabletopPose`. Game Boards are valid physical collision/placement surfaces, not merely inner logical placement guides. ADR-025 defines these replacements.

**Remaining scope:** Deck/Stack/Console bodies remain non-physical in this pass. Their existing positioning model retains mathematical-plane/layout placement and authored Table-area validation: invalid creation does not commit, and invalid supported movement releases retain/return to the previous authoritative pose. This does not authorize adding missing Container interactions or imply boundary validation is already implemented.

---

## ADR-025 - Physical Loose Objects and Authoritative 3D State

**Status:** Accepted

**Supersedes:** The conflicting portions of ADR-017, ADR-023, and ADR-024 identified above. ADR-009 remains superseded; neither the infinite placement plane nor camera-following Table Surface behavior is reinstated.

**Decision:** Use one reusable physical tabletop interaction model initially for loose Cards, Pawns, Tokens, and Dice. The real fixed Table and Game Boards provide explicitly authored, valid physical collision surfaces. Their colliders are editable with the corresponding Table/Board and follow its Transform and scale; decorative mesh details are not gameplay authority.

**Approved surface contract:** A `PhysicalTabletopSurface` component opts in an enabled, non-trigger, fixed Collider on the same GameObject. The single local Collider is resolved automatically; if multiple colliders exist, the authored top Collider is selected explicitly. Enabled surface components register/unregister through their lifecycle; the shared query reads live registrations in the Camera's physics scene, not a list of model references, names, or hierarchy paths. This is a Presentation-only collider membership registry, not globally stored Match State or a service locator. Table/Board models can be replaced independently; placing the authored collider on a child makes its area follow the model Transform/scale. Missing or invalid surface setup is diagnosed in the Inspector and runtime logs. Disabling a surface excludes it from placement; disabling its Collider/GameObject also removes physical collision. Existing session-owned Board visibility remains separate from surface discovery.

**Physical interaction:**

- Toolbox placement raycasts valid Table/Board physical surfaces, not the mathematical placement plane or `TabletopSurfaceProxy`. A valid hit supplies the preview and initial 3D placement; no valid hit means invalid/hidden preview and no creation commit.
- Create slightly above the hit surface so the loose object can settle through collision and gravity. Card batches and duplicate placement use the same physical-surface rule for every created loose object.
- Loose objects may use Rigidbody and collider physics. While held, they are temporarily controlled/kinematic with gravity disabled, lifted clear of surfaces/objects, and follow the pointer.
- Release restores dynamic physics and gravity and preserves release velocity/throw momentum. Collision, velocity, and torque determine subsequent motion. Releasing or throwing beyond the Table is allowed to fall naturally; loss of a surface hit is not a movement rejection and must not snap the object back.
- Deck, Stack, Hand, Console/Slot, and other Container layouts control contained Cards with loose physics disabled. Accepted extraction returns a Card to loose physical behavior; transfers remain authoritative and atomic.
- Deck/Stack/Console bodies keep their existing positioning system. Selection, flip, Inspect, Delete, Duplicate, labels, UI, Container ordering/transfers, Camera controls, and Game rules/content are not redesigned by this decision.

**State and authority:**

- Keep existing `TabletopPose` for authored/template/container layout. Add a separate authoritative, serializable 3D physical pose/state for loose physical objects, including full position and rotation and the motion/lifecycle state needed by the physical interaction. Do not force full 3D into `TabletopPose` or store live Match State only on a Rigidbody.
- Associate physical state with the existing stable Object ID in `MatchState`. Contained layout and loose physical state must not compete to position the same object.
- Player requests and accepted simulation outcomes pass through Commands or approved Application Use Cases with actor context, Technical Invariants, duplicate/stale-request handling, and Match revisions. When physics settles, commit the final 3D pose and any Die result through this boundary; Views must not directly mutate Runtime State.
- The approved local/offline implementation runs physics under the local authority. In future multiplayer, one host/server physics simulation determines accepted motion and Dice results. Clients may display/interpolate feedback but do not independently decide authoritative outcomes. This does not select or authorize a networking package.
- Surface-hit validation gates new loose placement. It is not an invariant that every accepted physical pose remains above the Table: off-table falling/settled state is valid.

**Dice:**

- d4, d6, d8, d10, d12, and d20 each have explicit authored physical face/value mappings, including their result-reading convention. Do not infer values from mesh triangle order or object names.
- A Roll action physically lifts/throws the Die with randomized impulse and torque; the settled physical orientation determines the result through that mapping. Manual grab/throw uses the same settle/value-resolution path.
- Commit the settled 3D pose and resolved value to authoritative Die State together. RNG may drive the throw, but no preselected RNG value overrides the settled face.
- Trap Floor's two d6 remain generic Platform Dice using this same system. Optional Floorfall assistance interprets accepted values without replacing generic Dice physics or changing Game rules.

**Reason:** Real surfaces, collisions, and throws supply physical tabletop behavior while the existing identity, Command, revision, Container, and Match authority boundaries preserve consistent accepted state. Layout authoring remains separate from free 3D simulation.

**Approval boundary:** This is approved direction, not evidence of implementation or verification. Full-scene physics, physical Container bodies/stacks, networking delivery, new Game rules, and unrelated interaction redesign remain outside this pass.
