# Console Cards — Non-Goals

**Document ID:** 15_Non_Goals  
**Version:** 1.0 Draft  
**Status:** Approved

## 1. Purpose

This document prevents foundation work from expanding into unrelated product systems.

A Non-Goal may become a future milestone only through explicit approval and roadmap revision.

## 2. Foundation Non-Goals

The initial foundation is not building:

- A complete Super Leroy Sisters Game.
- A complete Trap Floor Game.
- Automated Game-rule validation.
- Automatic victory detection.
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
- Full physical simulation.
- Physics-based card stacks.
- Physics-authoritative dice.
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

## 8. Change Process

To remove an item from Non-Goals:

1. State the user value.
2. Identify affected documents.
3. Add or update an Architecture Decision.
4. Define milestone scope.
5. Define completion criteria.
6. Approve before implementation.
