# Console Cards — Milestones and Roadmap

**Document ID:** 16_Milestones_And_Roadmap  
**Version:** 1.2

**Status:** Approved
**Planning basis:** One developer, approximately 30–35 focused hours per week.

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams are illustrative unless explicitly labelled **Approved Contract**.

## 1. Planning Rules

Each milestone must produce:

- A demonstrable build or executable test result.
- Explicit scope and exclusions.
- Required automated and manual tests.
- An Implementation Report.
- No hidden future-milestone work.

Hour ranges are planning estimates. The safe schedule includes approximately 15% contingency for integration and defects.

Documentation approval time is tracked separately from implementation time.

## 2. D0 — Documentation Approval

**Status:** Complete for M0 baseline  
**Included in implementation timeline:** No

Deliver:

- Approved core documents.
- Resolved implementation-blocking Open Decisions.
- Requirements Traceability.
- Clean repository baseline.
- Approved Codex instructions.

Exit:

- Relevant documents are marked `Approved` or `Approved with Open Decisions`.
- OD-001 through OD-008 are resolved.
- M0 is unblocked.

## 3. M0 — Project Skeleton and Core Domain

**Expected:** 25 hours  
**Safe:** 35 hours

Approved baseline:

- Fresh Unity project.
- Unity `6000.5.4f1`.
- URP.
- Windows desktop.
- Orthographic top-down 3D presentation.
- New Input System with mouse and keyboard.
- Git/GitHub Desktop.

Deliver:

- Baseline verified in the created repository.
- Folder and assembly structure.
- Strong IDs.
- Minimal logical Table Coordinate and Tabletop Pose.
- Base Tabletop Object State.
- Card, Pawn, and Token State.
- Container, Seat, Console, and Match State.
- Command, Result, and Domain Event foundations.
- Edit Mode Technical Invariant tests.

Exclude:

- Runtime serialization/storage.
- Card dragging.
- Networking.
- Official Game content.

Exit:

- Plain C# state supports object creation and atomic Container transfer.
- State is serialization-compatible but no persistence system is implemented.

## 4. M1 — Virtual Table and Camera

**Expected:** 32 hours  
**Safe:** 42 hours

Deliver:

- Top-down local Camera.
- Pan, zoom, and focus bookmarks.
- Logical-to-render coordinate conversion.
- Camera-local Table Surface proxy providing seamless visual coverage without moving the logical tabletop.
- Large-area precision measurements.
- Decision on whether sectoring/floating origin is actually required.
- Basic View culling strategy.

Exit:

- Player navigates a visually seamless large tabletop while logical object coordinates remain stable.

## 5. M2 — Generic Object and Card Interaction

**Status:** Complete

**Expected:** 55 hours  
**Safe:** 70 hours

Deliver:

- Explicit Card, Pawn, and Token object Views with local hit resolution.
- Selection and drag preview.
- Final Move Command.
- Rotation and flipping.
- Cancellation/rollback.
- Temporary Interaction Lock abstraction.
- Card, Pawn, and basic Token Views.
- Play Mode interaction tests.
- No global View registry was required for M2. A registry remains deferred unless later product requirements prove one is necessary.

Exit:

- One Player naturally manipulates Cards, Pawns, and Tokens through accepted Runtime State.

Completion evidence:

- Generic object interaction is implemented in `TabletopPrototype`.
- Edit Mode: 828 passed.
- Play Mode: 793 passed.
- Integrated real-scene smoke coverage completed.

## 6. M3 - Decks, Stacks, Hands, and Consoles

**Status:** Current local prototype feature set implemented; closure evidence must be updated separately when verification is run.

**Original expected:** 55 hours

**Original safe:** 70 hours

Delivered in current source:

- Deck and Stack state/operations.
- Draw one and selected count.
- Shuffle.
- Merge and split behavior.
- Discard Pile.
- Private Hand and natural Hand reorder.
- Console and Slots.
- Universal Button Card definitions.
- Atomic Card transfers.
- Player-facing prototype context controls.

Exit:

- Local session supports the core Console Cards tabletop flow without Game-rule enforcement.

## 7. Immediate Phase 1 Delivery Sequence

The approved next-work order is:

> **M4 Play Area/player-layout foundation -> M4.1 minimum Game Template support -> G1 Trap Door playable -> G2 Super Leroy Sisters playable -> P1 Phase 1 closure with both Games playable.**

Do not combine these gates into one broad implementation task. Each gate requires its own tests, manual checks, implementation report, and rollback point.

## 8. M4 - Play Area and Player-Layout Foundation

Previous estimates are obsolete because the authoritative layout requirements expand M4. Re-estimate before implementation.

Deliver:

- One-to-eight-Player layout model around a stable central play space.
- Standard four-Player, eight-Player, and compact four-Player layouts.
- Seat repositioning toward the center for smaller Player counts.
- Core Game Board focus kept central without enlarging the table.
- Freeform Play Area.
- Generic Zone and Slot.
- Rectangular Grid.
- Side-Scroller Play Area.
- Placement suggestions and snap bypass.
- Marquee Card selection with clear selected-collection feedback.
- Live landing indicators for one Card and a selected Card group.
- Separate Hand, personal Play Area, and individual Card visibility model.
- Default framing that keeps required interactive areas visible.

Exclude:

- Game Template loading.
- Trap Door or Super Leroy Sisters content/rules.
- Networking enforcement of visibility.
- Player-facing custom Template editor.

Exit:

- The Platform can present the required Player Layouts and Game Board layout primitives without Game-specific conditions.
- Single and grouped Card placement communicates the accepted landing arrangement before release.

## 9. M4.1 - Minimum Game Template Support

Deliver only the Template support required for Empty Table, Trap Door, and Super Leroy Sisters:

- Local Game Template schema.
- Empty Table Template.
- Stable content references and minimum local content resolution.
- Player Layout selection for one to eight Players.
- Universal Console configuration separate from Game-specific Game Board/Play Area content.
- Template validation and atomic Match creation.
- Initial in-memory Match baseline and reset behavior.
- Default Camera focus and required-area framing.
- Large, central, readable Card-choice UI with hide/reopen and explicit selection/confirmation feedback.

Exclude:

- Player-facing custom Template editor.
- Workshop/content sharing.
- Persistence beyond the minimum Initial Snapshot/reset contract.
- Generic Game-rule scripting.

Exit:

- Empty Table and representative Trap Door/Super Leroy Sisters setup data can load without modifying Platform code.
- The same universal Console loads with distinct Game-specific central Boards.

## 10. G1 - Trap Door Playable

Prerequisite: resolve OD-018. Do not infer missing Game Rules from the layout reference.

Deliver the approved minimum playable Trap Door Game Template using:

- A Card-based level built from Level Cards.
- A Trap Door-specific dungeon/room Game Board.
- A meeple representing Player position.
- 2d6 randomization.
- Button Cards and Move Cards.
- The stated flow: reveal Level Card, view obstacle, play Button Cards/Move Card, resolve Card, move Player, reveal next Card.
- Only approved obstacle/content definitions.
- A human-readable Rulebook for all decisions not automated.

Exit:

- A Player can complete the approved minimum Trap Door playthrough end-to-end using authoritative Runtime State and existing Platform use cases.

## 11. G2 - Super Leroy Sisters Playable

Prerequisite: resolve OD-019. Do not infer missing Game Rules from the layout reference.

Deliver the approved minimum playable Super Leroy Sisters Game Template using:

- A Side-Scroller Play Area made from Level Cards generated by a Level Deck.
- A meeple moving Card by Card.
- Progressive reveal of new level sections.
- Button Cards and Move Cards for obstacle resolution.
- The stated flow: draw Level Card, place it into the level, move Player, encounter obstacle, play Button Cards/Move Card, resolve obstacle, continue to the next Card.
- A human-readable Rulebook for all decisions not automated.

Exit:

- A Player can complete the approved minimum Super Leroy Sisters playthrough end-to-end without adding Game-specific conditions to Platform modules.

## 12. P1 - Phase 1 Closure

Prerequisite: resolve OD-020.

Deliver:

- Regression and interaction pass across Empty Table, Trap Door, and Super Leroy Sisters.
- Verification of one-to-eight Seat layout capability, including standard four-Player, eight-Player, and compact four-Player layouts.
- Verification that the table does not grow and core gameplay remains centered.
- Verification of marquee selection, landing indicators, independent visibility configuration, and high-stakes Card-choice UI.
- Documentation reconciliation and requirements traceability closure.
- Known-issues register.
- Stable Phase 1 build containing both playable Games.

Exit:

- Trap Door and Super Leroy Sisters both meet their approved minimum playable criteria.
- Missing or deferred requirements remain explicitly marked and are not represented as implemented.

## 13. Post-Phase 1 Platform Milestones

These remain planned Platform work but are not on the immediate Phase 1 critical path above.

### M5 - Persistence Foundation

- Versioned Snapshot DTO.
- Local save/load.
- Atomic load/reset.
- Content resolution.
- Snapshot validation and round-trip tests.

### M6 - Multiplayer Technology Decision

- Current-version technology scorecard and approved networking ADR.
- Selected adapter plan.
- Explicit host-migration inclusion or deferral.

### M7 - Multiplayer Foundation

- Private Session create/join.
- Stable Player ID and Seat binding for one to eight Players.
- Shared Commands and interaction locks.
- Authoritative draw/shuffle.
- Independent Hand, personal Play Area, and Card-identity filtering.
- Snapshot join, reconnect, reset, and controlled host-loss fallback.

### M8 - Post-Phase 1 Platform Stabilization

- Persistence/multiplayer regression and stress pass.
- Object-count performance baseline.
- Architecture audit and documentation reconciliation.

## 14. Planning and Scope Clarifications

- Previous M4-M8 aggregate hour estimates are retired; the new requirements and Phase 1 content gates require re-estimation.
- Custom player-authored Templates remain a future product direction, not an M4.1 editor deliverable.
- Basic Tokens are included in M2.
- Runtime serialization is not included in M0.
- M4 supplies Player Layout and Play Area capability; Game Templates decide which supported layout a Game uses.
- Trap Door and Super Leroy Sisters are separate Game-specific Board types and Game Templates.
- Phase 1 requires minimum playable versions of both Games, not invented rules, full automation, or production-complete content.
- Reconnection and Seat restoration remain M7 requirements.
- Host migration remains conditional.

## 15. Change Control

Any milestone change must document:

- Added and removed scope.
- Expected and safe hour impact.
- Dependency impact.
- Test impact.
- Documentation impact.
- Approval.
