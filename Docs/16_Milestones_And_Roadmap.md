# Console Cards — Milestones and Roadmap

**Document ID:** 16_Milestones_And_Roadmap  
**Version:** 1.1 Approval Candidate  
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
- Seamless Table Surface prototype.
- Large-area precision measurements.
- Decision on whether sectoring/floating origin is actually required.
- Basic View culling strategy.

Exit:

- Player navigates a visually seamless large tabletop while logical object coordinates remain stable.

## 5. M2 — Generic Object and Card Interaction

**Expected:** 55 hours  
**Safe:** 70 hours

Deliver:

- Object Views and registry.
- Selection and drag preview.
- Final Move Command.
- Rotation and flipping.
- Cancellation/rollback.
- Temporary Interaction Lock abstraction.
- Card, Pawn, and basic Token Views.
- Play Mode interaction tests.

Exit:

- One Player naturally manipulates Cards, Pawns, and Tokens through accepted Runtime State.

## 6. M3 — Decks, Stacks, Hands, and Consoles

**Expected:** 55 hours  
**Safe:** 70 hours

Deliver:

- Deck and Stack state/operations.
- Draw one and selected count.
- Shuffle.
- Merge and first approved split behavior.
- Discard Pile.
- Private Hand.
- Console and Slots.
- Universal Button Card definitions.
- Draw-card versus move-Deck interaction.
- Atomic transfers and tests.

Exit:

- Local session supports the core Console Cards tabletop flow without Game-rule enforcement.

## 7. M4 — Play Areas and Game Template Loading

**Expected:** 55 hours  
**Safe:** 70 hours

Deliver:

- Freeform Play Area.
- Generic Zone and Slot.
- Rectangular Grid.
- Side-scroller Play Area.
- Placement suggestions and snap bypass.
- Local Game Template schema.
- Empty Table Template.
- Template validation.
- Initial in-memory Match baseline and reset behavior.

Exclude:

- Player-facing custom Template editor.
- Workshop/content sharing.
- Full official Games.

Exit:

- Multiple arrangements load from data without modifying Platform code.

## 8. M5 — Persistence Foundation

**Expected:** 28 hours  
**Safe:** 38 hours

Deliver:

- Versioned Snapshot DTO.
- Local save/load.
- Atomic load/reset.
- Content resolution.
- Snapshot validation.
- Round-trip tests.

Exit:

- Match saves and restores accepted state without Unity scene references in persisted data.

## 9. M6 — Multiplayer Technology Decision

**Expected:** 12 hours  
**Safe:** 20 hours

Deliver:

- Current-version Fusion/NGO/Mirror scorecard.
- Small prototype only if needed to resolve uncertainty.
- Cost and continuity comparison.
- Approved networking ADR.
- Selected adapter plan.
- Explicit host-migration inclusion or deferral.

Exit:

- One networking technology is approved.

## 10. M7 — Multiplayer Foundation

**Expected:** 75 hours  
**Safe:** 105 hours

Deliver:

- Private Session create/join.
- Stable Player ID and Seat binding.
- Two-to-six Seats.
- Shared Commands.
- Interaction locks.
- Private Hand filtering.
- Authoritative draw/shuffle.
- Full Snapshot join and non-host reconnect.
- Session reset.
- Multiplayer tests.
- Controlled host-loss fallback.

Conditional:

- Host migration only if approved in M6.

Exit:

- Multiplayer remains state-consistent during normal play, contention, and non-host reconnect tests.

## 11. M8 — Foundation Stabilization

**Expected:** 32 hours  
**Safe:** 45 hours

Deliver:

- Regression pass.
- Interaction tuning.
- Snapshot stress testing.
- Object-count performance baseline.
- Architecture audit.
- Documentation reconciliation.
- Known-issues register.
- Stable Foundation build.

Exit:

- Foundation is ready for official Game Template production.

## 12. Timeline Summary

### Local Foundation — M0 to M4

- Expected effort: approximately 222 hours.
- Safe effort: approximately 287 hours.
- Expected schedule at 30–35 hours/week: approximately 6.5–7.5 weeks.
- Safe schedule with integration contingency: approximately 8–10 weeks.

### Local Foundation with Persistence — M0 to M5

- Expected effort: approximately 250 hours.
- Safe effort: approximately 325 hours.
- Safe planning range: approximately 8–12 weeks.

### Multiplayer-Ready Foundation — M0 to M8

- Expected effort: approximately 369 hours.
- Safe effort: approximately 495 hours.
- Safe planning range: approximately 12–18 weeks.

These ranges exclude completed documentation work and assume scope does not expand.

## 13. Scope Clarifications

- Custom player-authored Templates are a future product direction, not an M4 editor deliverable.
- Basic Tokens are included in M2.
- Runtime serialization is not included in M0.
- Reconnection and Seat restoration are required in M7.
- Host migration is conditional.
- Super Leroy Sisters and Trap Floor production work begins only after the Foundation is stable.

## 14. Change Control

Any milestone change must document:

- Added and removed scope.
- Expected and safe hour impact.
- Dependency impact.
- Test impact.
- Documentation impact.
- Approval.
