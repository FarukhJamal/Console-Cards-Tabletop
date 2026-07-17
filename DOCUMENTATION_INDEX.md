# Console Cards Documentation Index

**Pack version:** 1.2 Approved Baseline  
**Implementation status:** M0 unblocked; networking decisions deferred to M6

## Approval Rule

Codex may implement from documents marked `Approved` or `Approved with Open Decisions`. The exact technical baseline is in `Docs/TECHNICAL_BASELINE.md`.

All code and type examples are illustrative unless explicitly labelled `Approved Contract`.

## Reading Order

| Order | File | Purpose | Status |
|---:|---|---|---|
| 1 | `Docs/00_Product_Vision.md` | Product identity and requirement horizons | Approved |
| 2 | `Docs/02_Terminology.md` | Canonical vocabulary | Approved |
| 3 | `Docs/03_Project_Principles.md` | Decision principles | Approved |
| 4 | `Docs/01_Platform_Architecture.md` | Layered architecture and boundaries | Approved with Open Decisions |
| 5 | `Docs/04_Architecture_Decisions.md` | Architecture Decision Records | Approved with Open Decisions |
| 6 | `Docs/05_Core_Data_Model.md` | Runtime state and invariants | Approved |
| 7 | `Docs/06_Tabletop_Interaction_Design.md` | Input and physical interaction | Approved |
| 8 | `Docs/07_Play_Area_Architecture.md` | Table, grids, side-scroller, zones | Approved |
| 9 | `Docs/08_Game_Template_Architecture.md` | Template loading and future custom content | Approved |
| 10 | `Docs/09_Policy_And_Enforcement_Architecture.md` | Free/Assisted/Restricted/Enforced policies | Approved |
| 11 | `Docs/15_Non_Goals.md` | Scope exclusions | Approved |
| 12 | `Docs/12_Unity_Project_Structure.md` | Folders, assemblies, scenes | Approved |
| 13 | `Docs/13_Coding_Standards.md` | C# and Unity rules | Approved |
| 14 | `Docs/14_Testing_Strategy.md` | Automated/manual testing | Approved |
| 15 | `AGENTS.md` | Codex operating rules | Approved |
| 16 | `Docs/10_Multiplayer_Architecture.md` | Authority, Seats, reconnect, privacy | Approved with Open Decisions |
| 17 | `Docs/11_Persistence_And_Snapshots.md` | Save/load/reset/recovery | Approved with Open Decisions |
| 18 | `Docs/16_Milestones_And_Roadmap.md` | Delivery scope, hours, exit gates | Approved |

## Control Documents

| File | Purpose |
|---|---|
| `Docs/OPEN_DECISIONS.md` | Implementation-blocking and deferred decisions |
| `Docs/REQUIREMENTS_TRACEABILITY.md` | Requirement-to-milestone/test mapping |
| `Docs/AUDIT_RESOLUTION_v1.1.md` | v1.0 audit correction record |
| `CHANGELOG.md` | Pack history |
| `README.md` | Repository entry point |

## Current Decision State

M0–M5 baseline decisions are resolved.

Deferred until M6:

- Networking technology.
- Host-migration inclusion.

Deferred beyond Foundation:

- Player-facing Template editor.
- Official Game Template production order.
