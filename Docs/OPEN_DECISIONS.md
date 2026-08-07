# Console Cards — Open Decisions

**Version:** 1.2

**Status:** Approved with Open Decisions  
**Purpose:** Record resolved and deferred decisions so Codex does not infer them.

## Status Values

- `Resolved`: accepted and transferred to an authoritative document.
- `Milestone Blocking`: blocks the named milestone.
- `Deferred`: intentionally unresolved until a later milestone.

## Resolved Implementation Baseline

### OD-001 — Project Starting Point

**Status:** Resolved

**Decision:** Fresh Unity project.

### OD-002 — Exact Unity Version

**Status:** Resolved

**Decision:** Unity `6000.5.4f1`.

### OD-003 — Render Pipeline

**Status:** Resolved  
**Decision:** Universal Render Pipeline (URP).

### OD-004 — Presentation Model

**Status:** Resolved  
**Decision:** 3D tabletop scene with an orthographic top-down Camera. Cards and pieces use controlled movement.

### OD-005 — Initial Target Platform

**Status:** Resolved  
**Decision:** Windows desktop.

### OD-006 — Input

**Status:** Resolved  
**Decision:** Unity New Input System; mouse and keyboard first.

### OD-007 — Source Control

**Status:** Resolved  
**Decision:** Git using GitHub Desktop as the primary client.

### OD-008 — Card and World Dimensions

**Status:** Resolved  
**Decision:**

- Width: `1.0` Unity unit.
- Height: `1.4` Unity units.
- Thickness: approximately `0.02` Unity units.
- Spacing range: `0.08–0.12` Unity units.
- Prototype default spacing: `0.10` Unity units, configurable.

See `TECHNICAL_BASELINE.md`.

### OD-012 - Official Game Template Production Order

**Status:** Resolved

**Decision:** Trap Door is produced first, followed by Super Leroy Sisters. Both follow the M4 Play Area/player-layout foundation and M4.1 minimum Game Template support.

### OD-013 - Official Game Name

**Status:** Resolved

**Decision:** Use **Trap Door**. Earlier documentation references to **Trap Floor** were stale and are superseded by the authoritative layout requirements.

## Deferred and Milestone-Blocking Decisions

### OD-009 — Networking Technology

**Status:** Deferred to M6  
**Options:** Photon Fusion, NGO, Mirror, or another approved solution.  
**Rule:** No vendor types may enter Core before resolution.

### OD-010 — Host Migration

**Status:** Deferred to M6  
**Question:** Include recoverable host migration in M7 or use controlled host-loss fallback?  
**Dependencies:** Networking choice, implementation cost, and schedule.

### OD-011 — Player-Facing Game Template Editor

**Status:** Deferred beyond the immediate Phase 1 content path

**Current position:** Architecture must not block it; M4.1 does not deliver it.

### OD-014 - Player Layout Mapping

**Status:** Milestone Blocking for M4

**Question:** How are Seats assigned and oriented for one to three and five to seven Players, and when does a four-Player Game Template select the standard versus compact four-Player layout?

**Known requirements:** Support one to eight Players; do not enlarge the table; reposition smaller groups toward the center; provide standard four-Player, eight-Player, and compact four-Player layouts.

### OD-015 - Visibility Audience and Limited Awareness

**Status:** Milestone Blocking for M4 visibility model; network delivery deferred to M7

**Question:** For Hand, personal Play Area, and individual face-down Card visibility, which audiences receive full identity, public back, count-only, silhouette, or no information? What exactly is the permitted limited awareness of other Players' tools/resources?

**Rule:** These three visibility subjects remain independently configurable, and Presentation hiding alone is not a security boundary.

### OD-016 - Marquee Selection and Group Landing Contract

**Status:** Milestone Blocking for M4 interaction completion

**Question:** Does marquee inclusion use Card center, any overlap, or a threshold; how are stacked/contained Cards treated; and how does a selected group preserve spacing when a destination cannot accept the whole group?

**Known requirements:** Click-hold-drag creates a marquee; every included Card highlights; dragging shows a live group landing indicator; accepted mutation remains atomic.

### OD-017 - High-Stakes Card-Choice Contract

**Status:** Milestone Blocking for M4.1

**Question:** What minimum candidate data, confirmation/cancellation behavior, reopen affordance, and Game-facing request/result contract are required?

**Known requirements:** The UI is large and readable, hideable/reopenable, preserves the pending choice, and gives hover, selection, confirmation, and registered-choice feedback.

### OD-018 - Trap Door Minimum Playable Rules

**Status:** Milestone Blocking for G1

**Question:** Define supported Player count, starting setup, Board dimensions/layout, Level Deck contents, 2d6 resolution, Button Card/Move Card effects, obstacle outcomes, progression, completion, and failure conditions.

**Known requirements only:** Card-built level, meeple position, 2d6, stated obstacle examples, and the reveal-to-resolve flow in `17_Layout_Design_Requirements_Matrix.md`.

### OD-019 - Super Leroy Sisters Minimum Playable Rules

**Status:** Milestone Blocking for G2

**Question:** Define supported Player count, starting setup, Level Deck contents/order, Side-Scroller section/window behavior, Button Card/Move Card effects, obstacle outcomes, progression, completion, and failure conditions.

**Known requirements only:** Level Deck builds a Card-based Side-Scroller, meeple moves Card by Card, sections reveal during progress, and the stated draw-to-resolve flow in `17_Layout_Design_Requirements_Matrix.md`.

### OD-020 - Phase 1 Playable Acceptance

**Status:** Milestone Blocking for P1

**Question:** What content quantity, session length, supported Player configurations, completion/failure demonstration, and known-issue threshold define "playable" for each official Game?

**Rule:** P1 cannot close merely because both Templates load; each must complete its approved end-to-end minimum flow.

## Current Gate

- **M0:** Unblocked.
- **M1:** Unblocked, with large-coordinate strategy intentionally evaluated during M1.
- **M2:** Unblocked using the approved prototype card dimensions.
- **M3:** No open design decision blocks the current prototype feature set; closure verification is tracked separately.
- **M4:** Blocked by OD-014, OD-015, and OD-016 at implementation start.
- **M4.1:** Blocked by OD-017 at implementation start.
- **G1:** Blocked by OD-018.
- **G2:** Blocked by OD-019.
- **P1:** Blocked by OD-020.
- **M6/M7:** Blocked only by OD-009 and OD-010 at their planned decision point.
