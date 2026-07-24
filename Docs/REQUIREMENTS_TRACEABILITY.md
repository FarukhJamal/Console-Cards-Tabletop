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
| PR-007 | Freeform object movement | Interaction Design | M2 | Object Views, pointer projection, hit resolution, selection, drag preview, accepted movement, and cancel/rollback: `Assets/ConsoleCards/Presentation/Views/`, `Assets/ConsoleCards/Presentation/Interaction/`, `Assets/ConsoleCards/Tests/PlayMode/Presentation/TabletopPrototypeInteractionSmokeTests.cs` |
| PR-008 | Cards can flip, rotate, stack, and transfer | Interaction + Core Data | M2, M3 | M2 covers rotation and Card flipping through Commands/Use Cases and integrated Play Mode tests; stack and transfer remain M3: `Assets/ConsoleCards/Runtime/Application/Commands/`, `Assets/ConsoleCards/Runtime/Application/UseCases/`, `Assets/ConsoleCards/Presentation/Input/`, `Assets/ConsoleCards/Tests/PlayMode/Presentation/` |
| PR-009 | Deck draw, move, shuffle, split/merge | Interaction + Core Data | M3 | Unit and Play Mode tests |
| PR-010 | Cards, Pawns, and basic Tokens | Core Data + Tabletop Objects | M0, M2 | Explicit state and View coverage, prototype prefabs/materials/layer, and scene integration: `Assets/ConsoleCards/Runtime/Core/Domain/`, `Assets/ConsoleCards/Presentation/Views/`, `Assets/ConsoleCards/Content/Prefabs/Prototype/`, `Assets/ConsoleCards/Presentation/Scenes/TabletopPrototype.unity` |
| PR-011 | Optional Play Areas | Play Area Architecture | M4 | Template load and placement tests |
| PR-012 | Rectangular Grid | Play Area Architecture | M4 | Grid suggestion/snap tests |
| PR-013 | Side-scroller Play Area | Play Area Architecture | M4 | Layout continuation test |
| PR-014 | Game Templates are data, not hardcoded Games | Game Template Architecture | M4 | Template loading without Platform changes |
| PR-015 | Empty Table workflow | Game Template Architecture | M4 | Empty Table load test |
| PR-016 | Official Game Templates later | Game Template Architecture | Post-M8 | Future content acceptance |
| PR-017 | Custom Template direction not blocked | Game Template Architecture | Architectural only | Review/ADR; no editor in Foundation |
| PR-018 | Freedom by default | Policy Architecture | M2-M4 | M2 preserves free local manipulation while enforcing technical invariants, local interaction locks, and deterministic routing; no Game rules added: `Assets/ConsoleCards/Presentation/Interaction/`, `Assets/ConsoleCards/Presentation/Input/` |
| PR-019 | Future restrictions through Policies | Policy Architecture | Foundation contracts; later implementation | Policy composition tests |
| PR-020 | Technical Invariants always enforced | Core/Application | M0 onward | Edit Mode invariant tests |
| PR-021 | Runtime State separate from Views | Platform Architecture | M0-M2 | M2 View/state boundary verified: Runtime State remains authoritative; Transform, highlight roots, Card face roots, and TableSurfaceProxy are Presentation only; `TabletopPrototypeComposition` is prototype-only and not a permanent Bootstrap |
| PR-022 | Save/load and reset | Persistence Architecture | M4, M5 | Snapshot round-trip/reset tests |
| PR-023 | Networking remains vendor-neutral before decision | Multiplayer Architecture | M0–M6 | Assembly audit |
| PR-024 | Stable identity and Seat restoration | Multiplayer Architecture | M7 | Reconnect test |
| PR-025 | Controlled host-loss handling | Multiplayer Architecture | M7 | Host-loss manual/automated test |
| PR-026 | Host migration only if approved | Multiplayer Architecture | Conditional M7 | Technology-specific migration tests |
| PR-027 | Codex does not invent missing requirements | AGENTS.md | All | Prompt/report audit |

## M2 Implementation Evidence

M2 Generic Object and Card Interaction is complete.

- Object Views: `Assets/ConsoleCards/Presentation/Views/`
- Pointer projection, object hit resolution, selection, drag preview, accepted movement coordination, cancel/rollback, rotation coordination, Card flip coordination, local interaction locks: `Assets/ConsoleCards/Presentation/Interaction/`
- Deterministic input routing, Camera/object scroll ownership, immutable input frame, shared frame coordinator: `Assets/ConsoleCards/Presentation/Input/`
- Prototype-only composition root: `Assets/ConsoleCards/Presentation/Prototype/`
- CardFace presentation and selection presentation remain local Presentation projections, not authoritative Runtime State.
- Prototype prefab/layer foundation: `Assets/ConsoleCards/Content/Prefabs/Prototype/`, `Assets/ConsoleCards/Content/Materials/Prototype/`, `ProjectSettings/TagManager.asset`
- Scene integration: `Assets/ConsoleCards/Presentation/Scenes/TabletopPrototype.unity`
- Integrated real-scene smoke tests: `Assets/ConsoleCards/Tests/PlayMode/Presentation/TabletopPrototypeInteractionSmokeTests.cs`

Final M2 verification evidence:

- Edit Mode: 828
- Play Mode: 793
- Failed: 0
- Skipped: 0
- Unity compilation errors: 0

M2 boundaries preserved:

- No networking or persistence implementation.
- No Game Templates, `PlayAreaState`, grid placement, or snapping.
- No Game-rule enforcement.
- No hidden-information security claim from renderer or root visibility.
- No global View registry.
- No production Bootstrap promotion for `TabletopPrototypeComposition`.
