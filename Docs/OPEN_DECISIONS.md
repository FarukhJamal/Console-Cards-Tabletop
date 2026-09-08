# Console Cards — Open Decisions

**Version:** 1.8

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

**Decision:** Trap Floor is produced first, followed by Super Leroy Sisters. Completed milestone history is preserved: M4 Player Layout + Central Play Area, M4.1 minimum Game Template support, Trap Floor tabletop/Floor-coordinate foundations, Session Entry + Component Toolbox, generic Dice, legacy Floormaster lifecycle assistance, and legacy prototype round/phase orchestration. The next priority is aligning Trap Floor with the current manually playable direction, then a dedicated Trap Floor polishing pass, then Super Leroy Sisters playable work and remaining shared Phase 1 closure. Deeper Trap Floor rules-engine automation is not a prerequisite.

### OD-013 - Official Game Name

**Status:** Resolved

**Decision:** Use **Trap Floor**. **Trap Door** remains obsolete terminology, and its sequential Level Deck, dungeon/room progression, enemy, and reveal-next-Level-Card concepts are not Trap Floor authority. The latest Russell/Milanote Trap Floor direction independently adopts Keys and an Exit as current Trap Floor concepts. See `18_Trap_Floor_Game_Requirements.md`.

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

**Status:** Deferred for unresolved mappings; confirmed M4 presets are unblocked

**Authorized M4 scope:** Implement the Player Layout model with structural support for one to eight occupied Seats and authored definitions for standard four-Player, compact four-Player, and eight-Player layouts only. The table remains fixed in size, occupied Seats use authored placement around the centered core gameplay area, and the universal Console remains separate from the Game-specific Game Board.

**Remaining question:** How are Seats assigned and oriented for one to three and five to seven Players, and when does a four-Player Game Template select the standard versus compact four-Player layout?

**Rule:** The unresolved mappings do not block implementation of the three confirmed presets. Do not invent or imply layouts for the unresolved Player counts. Trap Floor may reach manually playable acceptance using an authored and verified layout, but it cannot claim two- or three-Player layout support until those mappings are resolved.

### OD-015 - Visibility Audience and Limited Awareness

**Status:** Milestone Blocking for P1 closure; network delivery deferred to M7

**Question:** For Hand, personal Play Area, and individual face-down Card visibility, which audiences receive full identity, public back, count-only, silhouette, or no information? What exactly is the permitted limited awareness of other Players' tools/resources?

**Rule:** These three visibility subjects remain independently configurable, and Presentation hiding alone is not a security boundary. This decision does not block the M4 Player Layout + Central Play Area foundation. Complete the required local model/configuration before Phase 1 closure unless an approved Game requires a defined subset earlier.

### OD-016 - Marquee Selection and Group Landing Contract

**Status:** Milestone Blocking for P1 closure

**Question:** Does marquee inclusion use Card center, any overlap, or a threshold; how are stacked/contained Cards treated; and how does a selected group preserve spacing when a destination cannot accept the whole group?

**Known requirements:** Click-hold-drag creates a marquee; every included Card highlights; dragging shows a live group landing indicator; accepted mutation remains atomic.

**Rule:** This decision does not block the M4 Player Layout + Central Play Area foundation. Complete marquee selection and single/group landing indicators before Phase 1 closure unless an approved Game requires a defined subset earlier.

### OD-017 - High-Stakes Card-Choice Contract

**Status:** Milestone Blocking for P1 closure unless a concrete Game requires it earlier

**Question:** What minimum candidate data, confirmation/cancellation behavior, reopen affordance, and Game-facing request/result contract are required?

**Known requirements:** The UI is large and readable, hideable/reopenable, preserves the pending choice, and gives hover, selection, confirmation, and registered-choice feedback.

**Rule:** OD-017 does not block M4.1 minimum Game Template support. Complete the high-stakes Card-choice UI before Phase 1 closure unless a concrete Trap Floor or Super Leroy Sisters implementation proves that the contract is required for that Game's approved playable flow, in which case resolve it before that dependency is implemented.

### OD-018 - Trap Floor Remaining Game Rules and Content

**Status:** Deferred where details remain unresolved; blocks only missing readable content or physical setup required for a claimed G1 play configuration, not comprehensive automation

**Resolved direction:** `18_Trap_Floor_Game_Requirements.md` approves the two-to-four-Player range; fixed 36-Card `6 x 6` Floor Card Board; `2d6` setup positioning; orthogonal movement; shared Floor-tile occupancy; flexible movement generally no more than three tiles; Search as flipping/revealing the interacted Floor Card; Keys, Traps, and other reveal effects; collecting all required Keys at the Exit as the objective; loss by Trap failure or exhaustion of usable Floor; separate interval-based `2d6` Floor Collapse creating permanent holes; and Avatar in the Main Console Slot. The latest Milanote card-set structure is the current content reference.

**Remaining questions:**

- Exact Floor Card visual design and unconfirmed latest-Milanote Card counts/content.
- Avatar stats and abilities.
- Exact movement values and any Avatar-specific variation.
- Trap costs and consequences.
- Number of required Keys.
- Collapse timing/frequency and protection/reroll conditions.
- Falling rules and the detailed usable-Floor exhaustion condition.
- Action economy.

**Rule:** Do not restore the former 50-coin objective/economy, 14 Trap + 14 Coin + 8 Item Floormaster Deck, Floormaster draw/discard Search lifecycle, mandatory Controller Deck, fixed Trap Floor Console allocation beyond Avatar in Main, Easy/Hard modes, or fixed 10-round loop as current authority. Do not hard-lock Controller Deck, Skill/Ability architecture, other Console Slot usage, Hand/draw/discard rules, modes, or a round limit. Resolve provisional dependencies before authoring affected content or assistance; do not infer them from either the old Trap Floor prototype or obsolete Trap Door material. Comprehensive coded Game-rule enforcement is not required for manually playable G1 acceptance.

### OD-019 - Super Leroy Sisters Minimum Playable Rules

**Status:** Milestone Blocking for G2

**Question:** Define supported Player count, starting setup, Level Deck contents/order, Side-Scroller section/window behavior, Button Card/Move Card effects, obstacle outcomes, progression, completion, and failure conditions.

**Known requirements only:** Level Deck builds a Card-based Side-Scroller, meeple moves Card by Card, sections reveal during progress, and the stated draw-to-resolve flow in `17_Layout_Design_Requirements_Matrix.md`.

### OD-020 - Phase 1 Playable Acceptance

**Status:** Trap Floor criteria resolved; Super Leroy Sisters criteria remain Milestone Blocking for P1

**Resolved Trap Floor criteria:** Trap Floor is playable when its approved Template loads; its `6 x 6` Board and required physical Components are present/readable; Players can use `2d6` setup, move orthogonally and share Floor tiles, reveal searched Floor Cards, carry all required Keys to the Exit, resolve Traps/effects, and mark permanent `2d6` Collapse holes manually; enough current Game content/instructions are readable to adjudicate provisional values socially; reset/session behavior is coherent; and optional or legacy assistance does not prevent manual play.

Trap Floor playable acceptance does not require automated Card effects, movement legality, Trap/fall consequences, Key/Exit validation, win/loss, or comprehensive round/action enforcement. Any Player-count claim must be limited to authored and verified layouts.

**Remaining question:** Define equivalent content quantity, supported Player configurations, completion/failure demonstration, and known-issue threshold for Super Leroy Sisters.

**Rule:** P1 cannot close merely because both Templates load; each must support its approved end-to-end minimum flow through readable instructions and physical tabletop actions. Comprehensive Game-rule automation is not required.

## Current Gate

- **M0:** Unblocked.
- **M1:** Unblocked, with large-coordinate strategy intentionally evaluated during M1.
- **M2:** Unblocked using the approved prototype card dimensions.
- **M3:** No open design decision blocks the current prototype feature set; closure verification is tracked separately.
- **M4:** Complete for the confirmed standard four-Player, compact four-Player, and eight-Player presets plus the central Play Area foundation. OD-014 retains unresolved mappings.
- **M4.1:** Complete for minimum Game Template schema, validation, minimum content resolution, atomic MatchState construction, initial in-memory reset baseline, and minimal prototype bootstrap integration.
- **G1:** M4/M4.1, Trap Floor tabletop/Floor-coordinate targeting, Session Entry + Component Toolbox, generic Dice, legacy Floormaster lifecycle assistance, and legacy prototype round/phase orchestration are implemented foundations/history. Align content and setup with the current manually playable criteria, then perform the dedicated Trap Floor polishing pass. OD-014 limits Player-count claims; OD-018 blocks only missing readable content/physical setup for the claimed configuration, not absent rules automation.
- **G2:** Blocked by OD-019.
- **P1:** Blocked by OD-015, OD-016, OD-017, and the unresolved Super Leroy Sisters portion of OD-020.
- **M6/M7:** Blocked only by OD-009 and OD-010 at their planned decision point.
