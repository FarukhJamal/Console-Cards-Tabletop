# Console Cards — Milestones and Roadmap

**Document ID:** 16_Milestones_And_Roadmap  
**Version:** 1.11

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
- One fixed physical Table that remains independent from Camera movement.
- Authored, inspector-editable Table collision surfaces that follow the Table Transform and scale without using decorative geometry as gameplay authority.
- Physical Table/Board raycast placement for loose objects under ADR-025; mathematical coordinates remain for authored/template/container layout.
- Basic View culling strategy.

Exit:

- Player navigates around a fixed physical Table while authored logical coordinates remain stable; new loose placement requires valid physical Table/Board hits. ADR-025 integration is a separate pending gate below, not implied complete by earlier M1/M2 evidence.

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

The approved delivery direction is:

> **Completed shared foundations -> finish the shared physical tabletop capabilities Trap Floor needs -> G1 Trap Floor manually playable -> Trap Floor polishing pass -> G2 Super Leroy Sisters manually playable -> P1 remaining shared Phase 1 work and closure -> future persistence/multiplayer milestones.**

Completed history is preserved: M4 Player Layout + Central Play Area, M4.1 minimum Game Template support, Trap Floor tabletop/Floorfall targeting, Session Entry, Empty/Custom Table, Component Toolbox, generic Dice, Floormaster Search lifecycle assistance, and prototype round/phase orchestration. The assisted Trap Floor systems are retained as optional/prototype infrastructure; deeper rules-engine work is not a prerequisite for G1.

### ADR-025 Physical-Object Integration Gate

**Status:** Initial ADR-025 implementation is present; compilation, automated execution, and Editor/manual physics verification remain pending. No physical-play completion claim is made. Existing M2 interaction and generic Dice completion evidence describes the previous controlled-plane/RNG model, not this physical replacement.

The initial integration uses separate immutable physical state and per-object physical revisions, a shared local authority/Rigidbody adapter, authored Table/Board colliders registered by `PhysicalTabletopSurface` independently of visual models, and six authored Dice face mappings. New regression tests are added but unexecuted. Existing Floorfall controls launch the two generic physical Dice, wait for settled results, and physically reroll protected corners; assistance no longer overwrites physical Dice with preselected values. Container-body positioning, UI structure, and Game rules remain outside the replacement.

This is scoped shared tabletop work within the existing immediate G1 capability priority:

- Author real Table/Board collision surfaces independent of decorative meshes, editable with and following each surface's Transform/scale.
- Raycast those surfaces for loose Card/Pawn/Token/Die creation, Card batches, and duplicate placement; preview at a valid hit and create slightly above it. No valid hit means no commit.
- Share Rigidbody/collider holding and release: temporary kinematic control with gravity off, clear lifted pointer following, then dynamic gravity/collision with preserved release velocity and torque. Off-table throws fall without snap-back.
- Keep Container membership/order and contained Card layout control, with loose physics disabled while contained and restored on accepted extraction. Keep Deck/Stack/Console bodies on existing positioning.
- Add separate authoritative 3D physical pose/state for loose objects; preserve authored/template/container `TabletopPose`, stable IDs, `MatchState`, Commands, actor context, Technical Invariants, and revisions.
- Resolve physical d4/d6/d8/d10/d12/d20 through explicit authored face/value mappings and commit settled pose/value together for both Roll and manual throw. Trap Floor's two d6 use the same generic system.

Exit evidence must cover valid/no-hit creation (including batch/duplicate), held/released transitions and off-table falling, accepted settlement/revision handling, Container entry/exit, all six authored Dice mappings, and preservation of existing selection/flip/Inspect/Delete/labels/UI/transfers/reset. These are future acceptance requirements, not tests performed by this documentation update.

Excluded: Container-body physics, physics-driven contained Card layouts, Camera redesign, Game-rule/content changes, new UI, and networking implementation. Future host/server physics authority is an architectural requirement, not an early transport deliverable. Persistence continues to version accepted Runtime State through the existing milestone boundaries.

**Planning impact:** Replace the earlier plane-boundary-only loose-placement work with this physical-object scope. Re-estimate integration effort before implementation scheduling; no new hour estimate or completion claim is inferred. Approval is recorded by ADR-025; Game-specific delivery order remains unchanged.

Do not combine these gates into one broad implementation task. Each gate requires its own tests, manual checks, implementation report, and rollback point.

## 8. M4 - Player Layout + Central Play Area Foundation

**Status:** Complete

Previous estimates are obsolete because the authoritative layout requirements expand M4. Re-estimate before implementation.

Deliver:

- A Unity-free Player Layout model structurally capable of representing one to eight occupied Seats around a fixed-size table.
- Authored Player Layout definitions for the currently confirmed presets only: standard four-Player, compact four-Player, and eight-Player.
- Per-Seat authored placement for the Seat/player zone, Hand anchor, universal Console anchor, and facing toward the central Game Board.
- Adaptive seating through authored layouts without enlarging the table or moving core gameplay away from the center.
- A Game-independent central Play Area foundation with stable identity, bounds, and a focus region.
- An explicit boundary between the universal Console and the Game-specific central Game Board/Play Areas.
- Narrow Presentation projection of a selected supported Player Layout around the existing fixed tabletop.

The model may validate and represent one to eight occupied Seats, but M4 must not invent authored layouts for one to three or five to seven Players. Selection between standard and compact four-Player layouts remains configuration owned by a future Game Template or host setup.

Exclude:

- Authored mappings for one to three and five to seven Players while OD-014 remains unresolved.
- Game Template loading.
- Play Area strategies beyond the central identity/bounds/focus foundation, including Freeform, generic Zone/Slot, Rectangular Grid, and Side-Scroller behavior unless required by a later approved Game milestone.
- Placement suggestions and snap bypass.
- Marquee selection, group selection, and single/group landing indicators; these remain Phase 1 requirements governed by OD-016.
- Independent Hand, personal Play Area, and individual Card visibility configuration; this remains a Phase 1 requirement governed by OD-015.
- Trap Floor or Super Leroy Sisters content/rules.
- Networking enforcement of visibility.
- Player-facing custom Template editor.

Exit:

- The Player Layout model supports one to eight occupied Seats structurally and presents the three confirmed authored presets without Game-specific conditions.
- The table remains fixed in size, core gameplay remains centered, and the central Play Area is distinct from every universal Console.
- No unresolved Player-count mapping is represented as implemented.

## 9. M4.1 - Minimum Game Template Support

**Status:** Complete

Deliver only the minimum local Template pipeline needed to prepare later selectable Trap Floor and Super Leroy Sisters content and an Empty/Custom Table session path:

- A Unity-free Game Template schema for starting setup, content, and layout.
- Template validation.
- Minimum local content resolution.
- Atomic `MatchState` construction from a valid Template, with clear failure and no partial Match creation.
- An initial in-memory reset baseline created from the valid Template setup.
- Minimal prototype bootstrap integration without broadly replacing the existing prototype composition.

The schema preserves the approved boundaries: the Console is universal, the Game Board and Play Areas are Game-specific Template content, and a Game Template defines starting setup/content/layout rather than gameplay rules.

Exclude:

- Player-facing custom Template editor.
- Workshop/content sharing.
- Persistence beyond the minimum Initial Snapshot/reset contract.
- Generic Game-rule scripting.
- Trap Floor or Super Leroy Sisters content and gameplay rules.
- High-stakes Card-choice UI; OD-017 governs that remaining shared Phase 1 requirement unless a concrete Game implementation proves it is required earlier.

Exit:

- A valid local Template constructs one complete authoritative `MatchState`; invalid input fails without exposing a partial Match.
- Reset restores the initial in-memory baseline produced from the Template setup.
- The prototype can exercise the minimum Template bootstrap while retaining the universal Console/Game-specific Game Board boundary.

## 10. G1 - Trap Floor Playable

**Authority:** `18_Trap_Floor_Game_Requirements.md`
**Prerequisite:** M4 and M4.1 are complete. OD-014 still governs which Player-count layouts may be claimed as supported. OD-018 must resolve any missing readable content or physical Component definition needed for Players to know what to do, but it does not require coded execution of those rules. Do not infer missing rules.

**Current status:** The Template-driven tabletop, `6 x 6` Board, authoritative physical `2d6`, Floorfall targeting assistance, direct Empty Table simulator startup, in-simulator Game Template loading, Component Toolbox, Floormaster Search lifecycle assistance, and prototype round/phase orchestration are implemented foundations. Search lifecycle and round orchestration are optional/prototype assistance rather than required core play.

### Completed Shared Foundation

The completed Template Loading + Component Toolbox Foundation provides:

- direct Empty/Custom Table startup and in-simulator loading of available Game Templates;
- authoritative toolbox-created Card, Deck, Stack/pile, Pawn/meeple, Token/counter, and Die instances;
- first-class generic Dice and actor-aware authoritative Roll;
- Trap Floor's two d6 through the same generic Die capability; and
- actor/revision boundaries suitable for later multiplayer without assuming Seat 0 is the permanent actor.

This history remains complete. It does not make assisted Trap Floor automation mandatory or establish physical-roll completion; the existing Dice foundation used authoritative RNG results and Presentation settling. ADR-025's physical replacement remains pending at the integration gate above.

### G1 Delivery Focus

Complete Trap Floor as a manually playable Game Template by prioritizing the shared physical capabilities and readable Game content needed for Players to carry out the rules themselves:

- Load the approved starting Template through the in-simulator Games / Templates panel.
- Present the fixed `6 x 6` Floor Card Board and required physical Components readably.
- Preserve the separate 36-Card Floormaster's Deck composition of 14 Trap, 14 Coin, and 8 Item Cards, with draw-left, discard-right, and exhaustion reshuffle as Player-facing rules.
- Present the universal Console/Slot setup, Controller Decks, Pawns, shared 50-coin supply, two d6, Rule Cards, Avatar Cards, Mode Cards, Item Slots, and starting poses required by the approved setup.
- Ensure Players can manually draw, shuffle, flip, reorder, stack, transfer, discard, move Pawns/Tokens/coins, roll/reposition Dice, and move Cards among Hands, table, Consoles, Slots, Decks, Stacks, and other Containers as Trap Floor requires.
- Provide enough readable Game content and instructions for Players to perform the 10-round `Start -> Search -> Trigger -> Floorfall -> End` loop and apply the approved Easy/Hard, cost, effect, elimination, and win/loss rules socially.
- Keep Reset, Clear Table, and in-simulator Template replacement behavior coherent.
- Ensure optional assistance never prevents manual play, including after house-rule modification or Component substitution.

The detailed Game Rules remain the intended Trap Floor design, but automatic execution is not G1 scope. Floorfall targeting may remain optional assistance. The Floormaster lifecycle may remain optional/prototype assistance. Prototype round/phase orchestration may remain experimental optional infrastructure.

G1 does **not** require:

- full automated Trap, Coin, or Item effects;
- coded Controller Card or coin economy validation;
- automatic movement legality;
- automatic elimination or survival calculation;
- automatic win/loss evaluation;
- comprehensive round/phase enforcement; or
- extending the existing prototypes into a complete Trap Floor rules engine.

Do not add a sequential Level Deck, dungeon/room reveal progression, enemies, keys, or exits. Those belong to the superseded Trap Door concept.

Exit:

1. The approved starting Trap Floor Template loads correctly.
2. The `6 x 6` Board and required physical Components are present and readable.
3. Players can manipulate the required Cards, Decks, Dice, Pawns, Tokens, Consoles, Slots, and discard areas manually.
4. Players can read enough Game content and instructions to know what actions to perform.
5. Generic physical actions required by Trap Floor are functional.
6. Reset and session behavior are coherent.
7. Optional assistance does not prevent manual play.

Any playable claim must name the Player layouts actually verified. OD-014 prevents claiming unresolved two- and three-Player layout support, but does not turn comprehensive Game-rule automation into a prerequisite.

### Trap Floor Polishing Pass

After G1 reaches the manually playable exit criteria, perform a dedicated Trap Floor polishing pass before moving deeper into subsequent Game content. Focus on readability, layout, interaction clarity, status/reference presentation, reset/session coherence, and defects found during manual play. Do not use polishing as a reason to require a comprehensive rules engine.

## 11. G2 - Super Leroy Sisters Playable

Prerequisite: resolve OD-019. Do not infer missing Game Rules from the layout reference.

Deliver the approved minimum manually playable Super Leroy Sisters Game Template using:

- A Side-Scroller Play Area made from Level Cards generated by a Level Deck.
- A meeple moving Card by Card.
- Progressive reveal of new level sections.
- Button Cards and Move Cards for obstacle resolution.
- The stated flow: draw Level Card, place it into the level, move Player, encounter obstacle, play Button Cards/Move Card, resolve obstacle, continue to the next Card.
- A human-readable Rulebook for all decisions not automated.

As with Trap Floor, Game-specific automation is optional assistance. G2 must prioritize readable content and the shared physical tabletop capabilities needed for Players to perform the approved flow; it does not require comprehensive coded rule enforcement.

Exit:

- A Player can complete the approved minimum Super Leroy Sisters playthrough end-to-end through readable instructions and physical tabletop manipulation without adding Game-specific conditions to Platform modules.

## 12. P1 - Phase 1 Closure

Prerequisite: resolve OD-015, OD-016, OD-017, and OD-020.

Deliver:

- Remaining shared Phase 1 capabilities not required earlier by an approved Game, including Freeform Play Area support, generic Zone/Slot support, Rectangular Grid support, placement suggestions, and snap bypass.
- Marquee Card selection with clear selected-collection feedback.
- Live landing indicators for one Card and a selected Card group.
- Separate Hand, personal Play Area, and individual Card visibility configuration; secure network delivery remains M7.
- Large, central, readable high-stakes Card-choice UI with hide/reopen and explicit hover, selection, confirmation, and registered-choice feedback.
- Regression and interaction pass across Empty Table, Trap Floor, and Super Leroy Sisters.
- Verification that startup enters an Empty/Custom Table without auto-loading a Game, in-simulator replacement preserves authoritative construction, and toolbox-created pieces remain authoritative.
- Verification of structural one-to-eight Seat capability and the standard four-Player, eight-Player, and compact four-Player authored layouts. Unresolved Player-count mappings must not be claimed as implemented.
- Verification that the table does not grow and core gameplay remains centered.
- Verification of marquee selection, landing indicators, independent visibility configuration, and high-stakes Card-choice UI.
- Documentation reconciliation and requirements traceability closure.
- Known-issues register.
- Stable Phase 1 build containing both playable Games.

Exit:

- Trap Floor and Super Leroy Sisters both meet their approved minimum playable criteria.
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
- M4 is authorized to implement only the confirmed standard four-Player, compact four-Player, and eight-Player authored layouts. OD-014 retains the missing one-to-three and five-to-seven mappings without blocking that confirmed work.
- OD-015 visibility work and OD-016 marquee/group-landing work remain required before Phase 1 closure but do not block the M4 foundation. An approved Game may pull a necessary subset earlier.
- Trap Floor and Super Leroy Sisters are separate Game-specific Board types and Game Templates.
- The superseded Session Entry implementation remains completed history; direct Empty Table startup, in-simulator Template loading, Component Toolbox, generic Dice, Floormaster lifecycle assistance, and prototype Trap Floor round/phase orchestration define the current direction. The latter two remain optional/prototype assistance.
- Empty/Custom Table is a first-class product path, not a debug mode and not dependent on Game-specific Board or rule content.
- Template-created and toolbox-created components share authoritative Runtime State; a Game Template owns setup/content/layout, not generic component types.
- New loose Cards/Pawns/Tokens/Dice, including Card batches and duplicates, require valid Table/Board surface hits. Released physical objects may fall off the Table without snap-back; Deck/Stack/Console bodies retain existing non-physical positioning and applicable ADR-024 authored-area rules. Freeform and house-rule play remain available without Game-rule enforcement.
- New player-initiated component actions preserve actor context for later authority validation without adding networking before M6/M7.
- Phase 1 requires minimum manually playable versions of both Games, not invented rules, full automation, or production-complete content.
- Generic physical tabletop capabilities take priority over deeper Game-specific rules-engine work.
- Trap Floor proceeds from manual playable completion into a dedicated polishing pass, then Super Leroy Sisters playable work, then remaining shared Phase 1 closure work.
- Future multiplayer synchronizes authoritative physical tabletop state and actor actions; it does not require a comprehensive Game-specific rules engine.
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
