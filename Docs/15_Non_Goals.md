# Console Cards — Non-Goals

**Document ID:** 15_Non_Goals  
**Version:** 1.4

**Status:** Approved

## 1. Purpose

This document prevents foundation work from expanding into unrelated product systems.

A Non-Goal may become a future milestone only through explicit approval and roadmap revision.

## 2. Foundation Non-Goals

Phase 1 includes minimum manually playable Trap Floor and Super Leroy Sisters Game Templates after the shared M4 foundations. This does not authorize unstated Game Rules, full automation, or production-complete content. Playable means Players can use readable instructions and generic physical tabletop capabilities; it does not mean the Platform comprehensively understands the Game.

The initial foundation is not building:

- Trap Floor content or rules beyond the approved minimum playable flow.
- Super Leroy Sisters content or rules beyond the approved minimum playable flow.
- Full Game-rule automation for Trap Floor or Super Leroy Sisters.
- Automated Game-rule validation.
- Automatic victory detection.
- Mandatory coded Trap/Coin/Item effect execution.
- Automatic Pawn movement legality.
- Coded Trap Floor coin-cost/reward enforcement.
- Automatic elimination or survival evaluation.
- Mandatory round/phase orchestration for official Game play.
- Universal turn enforcement.
- Anti-cheat suitable for ranked play.
- Public matchmaking.
- Ranked competition.
- Dedicated-server infrastructure.
- AI opponents.
- Voice chat.
- Text moderation.
- Economy.
- Cosmetic store.
- Battle pass.
- Achievements.
- Production analytics dashboard.
- Mobile release.
- VR support.
- Console platform certification.
- Steam Workshop.
- Template marketplace.
- Community moderation.
- Arbitrary runtime code plugins.
- User-authored C#.
- General visual scripting for rules.
- Full mod SDK.
- Cloud content distribution.
- Custom 3D model upload.
- Unrestricted shader upload.
- Full-scene physical simulation beyond the scoped loose Card/Pawn/Token/Die system approved by ADR-025.
- Physics-based Container bodies or contained Card layouts, including Decks/Stacks/Hands/Consoles.
- Independent client-authoritative physics or Dice results.
- Measuring rulers.
- Freehand drawing.
- Fog of war.
- Spectator mode.
- Replay viewer.
- Complete undo history.
- Seamless host migration unless included in an approved networking milestone.
- Host secrecy from the player acting as authority.
- Persistent online Matches running without any Player.
- Cross-platform account system.
- Monetization.

## 3. Architecture Non-Goals

The foundation is not intended to become:

- A generic engine for every tabletop game before real requirements exist.
- A comprehensive generic rules engine that must understand every legal or illegal Game action.
- A microservice architecture.
- A custom ECS.
- A reflection-heavy dependency framework.
- A universal event bus.
- A service locator.
- A deep inheritance hierarchy.
- A full Clean Architecture template copied mechanically.
- A package abstraction around every Unity API.
- An interface for every class.
- A configurable system for every constant.

## 4. Content Non-Goals

The first content library does not need every tabletop object ever created.

Implement only primitives required by current milestones.

The object model should remain extensible, but future components are not delivered merely because the architecture can support them.

## 5. Editor Non-Goals

The first foundation may load authored Game Templates but does not require a polished player-facing template editor.

A developer/editor workflow may be sufficient initially.

## 6. Multiplayer Non-Goals Before Technology Selection

Before the networking Architecture Decision, do not implement:

- Photon-specific Runtime State.
- NGO-specific Domain models.
- Mirror-specific Commands.
- Vendor-specific identity inside Seat State.
- Vendor-specific serialization in Core assemblies.

## 7. Quality Clarification

“Non-Goal” does not excuse:

- Corrupt state.
- Unclear input.
- Broken reset.
- Missing tests for Technical Invariants.
- Unhandled failures.
- Architecture drift.

The foundation must be small, not careless.

Player-enforced rules do not weaken authoritative state. Freeform Actions still preserve actor context, Match revisions, Container membership, visibility/ownership requirements, and other Technical Invariants.

ADR-025 explicitly removes loose Card/Pawn/Token/Die Rigidbody physics and settled-face Dice results from the Non-Goals. Approved scope includes real Table/Board collision surfaces, surface-raycast placement, controlled/kinematic holding, gravity/collision/velocity/torque on release, natural off-table falling without snap-back, and separate authoritative 3D physical pose/state alongside unchanged authored/layout `TabletopPose`. Standard d4/d6/d8/d10/d12/d20 use explicit authored face/value mappings. Contained Cards remain layout-controlled with loose physics disabled; Deck/Stack/Console bodies are not converted.

Runtime State, IDs, actor-aware Commands/Application Use Cases, and Match revisions remain authoritative. Future host/server physics determines accepted outcomes; clients do not independently decide results. This approval does not authorize networking packages, full-scene simulation, or new Game rules/content. Roadmap scope and acceptance are recorded under the ADR-025 physical-object integration gate.

## 8. Change Process

To remove an item from Non-Goals:

1. State the user value.
2. Identify affected documents.
3. Add or update an Architecture Decision.
4. Define milestone scope.
5. Define completion criteria.
6. Approve before implementation.
