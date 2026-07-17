# Console Cards — Requirements Traceability

**Version:** 1.0  
**Status:** Approved

| ID | Requirement | Architecture Owner | Planned Milestone | Evidence |
|---|---|---|---|---|
| PR-001 | Top-down shared Virtual Tabletop | Platform + Play Area Architecture | M1 | Camera/Table Surface manual test |
| PR-002 | Effectively unbounded normal-use table | Play Area Architecture | M1 | Large-area precision and seam test |
| PR-003 | Two-to-six configurable Seats | Core Data + Multiplayer | M0, M7 | Seat unit tests; multiplayer join test |
| PR-004 | Private Hands | Core Data + Multiplayer | M3, M7 | Visibility tests; multiplayer filtering |
| PR-005 | Personal Consoles separate from Hands | Core Data + Interaction | M3 | Console transfer and UI tests |
| PR-006 | Universal Button Cards | Product Vision + Game Templates | M3 | Definition and deck tests |
| PR-007 | Freeform object movement | Interaction Design | M2 | Play Mode interaction tests |
| PR-008 | Cards can flip, rotate, stack, and transfer | Interaction + Core Data | M2, M3 | Command and Play Mode tests |
| PR-009 | Deck draw, move, shuffle, split/merge | Interaction + Core Data | M3 | Unit and Play Mode tests |
| PR-010 | Cards, Pawns, and basic Tokens | Core Data + Tabletop Objects | M0, M2 | State and View tests |
| PR-011 | Optional Play Areas | Play Area Architecture | M4 | Template load and placement tests |
| PR-012 | Rectangular Grid | Play Area Architecture | M4 | Grid suggestion/snap tests |
| PR-013 | Side-scroller Play Area | Play Area Architecture | M4 | Layout continuation test |
| PR-014 | Game Templates are data, not hardcoded Games | Game Template Architecture | M4 | Template loading without Platform changes |
| PR-015 | Empty Table workflow | Game Template Architecture | M4 | Empty Table load test |
| PR-016 | Official Game Templates later | Game Template Architecture | Post-M8 | Future content acceptance |
| PR-017 | Custom Template direction not blocked | Game Template Architecture | Architectural only | Review/ADR; no editor in Foundation |
| PR-018 | Freedom by default | Policy Architecture | M2–M4 | Free Policy tests |
| PR-019 | Future restrictions through Policies | Policy Architecture | Foundation contracts; later implementation | Policy composition tests |
| PR-020 | Technical Invariants always enforced | Core/Application | M0 onward | Edit Mode invariant tests |
| PR-021 | Runtime State separate from Views | Platform Architecture | M0–M2 | Assembly/dependency audit |
| PR-022 | Save/load and reset | Persistence Architecture | M4, M5 | Snapshot round-trip/reset tests |
| PR-023 | Networking remains vendor-neutral before decision | Multiplayer Architecture | M0–M6 | Assembly audit |
| PR-024 | Stable identity and Seat restoration | Multiplayer Architecture | M7 | Reconnect test |
| PR-025 | Controlled host-loss handling | Multiplayer Architecture | M7 | Host-loss manual/automated test |
| PR-026 | Host migration only if approved | Multiplayer Architecture | Conditional M7 | Technology-specific migration tests |
| PR-027 | Codex does not invent missing requirements | AGENTS.md | All | Prompt/report audit |
