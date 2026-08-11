# Console Cards — Requirements Traceability

**Version:** 1.5

**Status:** Approved

The source, scope, repository status, milestone, and unresolved-decision trace for `Consolecards_LayoutRef_doc.pdf` and the later approved Trap Floor correction in `18_Trap_Floor_Game_Requirements.md` is maintained in `17_Layout_Design_Requirements_Matrix.md`.

| ID | Requirement | Architecture Owner | Planned Milestone | Evidence |
|---|---|---|---|---|
| PR-001 | Top-down shared Virtual Tabletop | Platform + Play Area Architecture | M1 | Camera/Table Surface manual test |
| PR-002 | Effectively unbounded normal-use table | Play Area Architecture | M1 | Large-area precision and seam test |
| PR-003 | One-to-eight configurable Seats and Player Layouts | Core Data + Play Areas + Multiplayer | M0, M4, M7 | Seat state evidence; Player Layout tests; multiplayer join test |
| PR-004 | Private Hands | Core Data + Multiplayer | M3, M7 | Visibility tests; multiplayer filtering |
| PR-005 | Personal Consoles separate from Hands | Core Data + Interaction | M3 | Console transfer and UI tests |
| PR-006 | Universal Button Cards | Product Vision + Game Templates | M3 | Definition and deck tests |
| PR-007 | Freeform object movement | Interaction Design | M2 | Object Views, pointer projection, hit resolution, selection, drag preview, accepted movement, and cancel/rollback: `Assets/ConsoleCards/Presentation/Views/`, `Assets/ConsoleCards/Presentation/Interaction/`, `Assets/ConsoleCards/Tests/PlayMode/Presentation/TabletopPrototypeInteractionSmokeTests.cs` |
| PR-008 | Cards can flip, rotate, stack, and transfer | Interaction + Core Data | M2, M3 | Rotation, flip, stack, and transfer source exists under `Assets/ConsoleCards/Runtime/Application/` and `Assets/ConsoleCards/Presentation/`; current M3 verification evidence is not recorded by this documentation pass |
| PR-009 | Deck draw, move, shuffle, split/merge | Interaction + Core Data | M3 | Commands, use cases, prototype Views, context controls, and lifecycle source exist; current M3 verification evidence remains to be updated |
| PR-010 | Cards, Pawns, and basic Tokens | Core Data + Tabletop Objects | M0, M2 | Explicit state and View coverage, prototype prefabs/materials/layer, and scene integration: `Assets/ConsoleCards/Runtime/Core/Domain/`, `Assets/ConsoleCards/Presentation/Views/`, `Assets/ConsoleCards/Content/Prefabs/Prototype/`, `Assets/ConsoleCards/Presentation/Scenes/TabletopPrototype.unity` |
| PR-011 | Optional Play Areas | Play Area Architecture | M4 foundation; remaining capabilities by P1 closure | Stable central identity/bounds/focus tests; later placement tests |
| PR-012 | Rectangular Grid | Play Area Architecture | Remaining shared requirements before P1 closure unless required earlier | Grid suggestion/snap tests |
| PR-013 | Side-scroller Play Area | Play Area Architecture | G2 | Layout continuation test |
| PR-014 | Game Templates are data, not hardcoded Games | Game Template Architecture | M4.1 | Template loading without Platform changes |
| PR-015 | Empty/Custom Table is a first-class Match/session path without mandatory Game-specific Template, Board, or rules | Session Entry + Game Template Architecture | Immediate shared prerequisite inside G1 | Explicit entry and empty-Match construction/reset tests |
| PR-016 | Official Game Templates: Trap Floor then Super Leroy Sisters | Game Template Architecture | G1, G2 | End-to-end approved minimum playable acceptance for each separate Game Template |
| PR-017 | Custom Template direction not blocked | Game Template Architecture | Architectural only | Review/ADR; no editor in Foundation |
| PR-018 | Freedom by default | Policy Architecture | M2-M4 | M2 preserves free local manipulation while enforcing technical invariants, local interaction locks, and deterministic routing; no Game rules added: `Assets/ConsoleCards/Presentation/Interaction/`, `Assets/ConsoleCards/Presentation/Input/` |
| PR-019 | Future restrictions through Policies | Policy Architecture | Foundation contracts; later implementation | Policy composition tests |
| PR-020 | Technical Invariants always enforced | Core/Application | M0 onward | Edit Mode invariant tests |
| PR-021 | Runtime State separate from Views | Platform Architecture | M0-M2 | M2 View/state boundary verified: Runtime State remains authoritative; Transform, highlight roots, Card face roots, and TableSurfaceProxy are Presentation only; `TabletopPrototypeComposition` is prototype-only and not a permanent Bootstrap |
| PR-022 | Save/load and reset | Persistence Architecture | M4.1 baseline/reset, M5 persistence | Snapshot round-trip/reset tests |
| PR-023 | Networking remains vendor-neutral before decision | Multiplayer Architecture | M0–M6 | Assembly audit |
| PR-024 | Stable identity and Seat restoration | Multiplayer Architecture | M7 | Reconnect test |
| PR-025 | Controlled host-loss handling | Multiplayer Architecture | M7 | Host-loss manual/automated test |
| PR-026 | Host migration only if approved | Multiplayer Architecture | Conditional M7 | Technology-specific migration tests |
| PR-027 | Codex does not invent missing requirements | AGENTS.md | All | Prompt/report audit |
| PR-028 | Table size remains stable while Seats reposition for Player count | Play Area Architecture | M4 confirmed presets; unresolved mappings remain OD-014 | Structural one-to-eight capacity plus standard four-Player, eight-Player, and compact four-Player layout tests; no invented intermediate-count layouts |
| PR-029 | Core gameplay remains centered and required interactive areas are visible by default | Play Area + Game Templates + Camera | M4, M4.1 | Layout/framing acceptance across supported Player Layouts and both official Games |
| PR-030 | Marquee multi-Card selection with clear highlights | Interaction Design | Remaining shared requirements before P1 closure unless required earlier | Selection-boundary, highlight, cancel, and collection-state tests; LDR-010-LDR-011 |
| PR-031 | Live landing indicators for one Card and selected Card groups | Interaction + Play Areas | Remaining shared requirements before P1 closure unless required earlier | Tabletop, Zone, Slot, valid/invalid, cancel, and group-placement tests; LDR-012-LDR-013 |
| PR-032 | Large hideable/reopenable high-stakes Card-choice UI | Presentation + Game Templates | Remaining shared requirements before P1 closure unless a concrete Game requires it earlier | Legibility, hide/reopen, input isolation, candidate selection, and confirmation tests; LDR-014-LDR-016; OD-017 |
| PR-033 | Hand, personal Play Area, and individual Card visibility are independent | Policy + Play Areas + Multiplayer | Remaining shared requirements before P1 closure; secure delivery M7 | Policy composition and unauthorized-delivery tests; LDR-017-LDR-021 |
| PR-034 | Console is universal and Game Board is Game-specific | Hands/Consoles + Play Areas + Game Templates | M4, M4.1, G1, G2 | Load distinct Game Boards with unchanged universal Console contract; LDR-025-LDR-027 |
| PR-035 | Trap Floor minimum playable Game Template and rule/content layer | Game-specific content | G1 | Tabletop/Board setup and Floorfall targeting foundations implemented; complete playable acceptance still requires the approved 2-4 Player flow and resolution of OD-014/OD-018 dependencies; LDR-028-LDR-030 and LDR-034-LDR-039 |
| PR-036 | Super Leroy Sisters minimum playable Game Template | Game-specific content | G2 | Approved end-to-end flow after OD-019 resolution; LDR-031-LDR-033 |
| PR-037 | Phase 1 closes after both official Games are playable and remaining shared requirements are complete | Roadmap + Acceptance | P1 | Approved acceptance after OD-015, OD-016, OD-017, and OD-020 resolution |
| PR-038 | Session Entry explicitly chooses Empty/Custom Table or an available Game Template; startup does not force Trap Floor | Bootstrap + Presentation + Game Templates | Immediate shared prerequisite inside G1 | Input-isolated choice UI, no-auto-load, valid selection, and atomic construction tests |
| PR-039 | In-session component toolbox adds generic authoritative Card, Deck, Stack/pile, Pawn/meeple, Token/counter, and Die instances | Core + Application + Presentation | Immediate shared prerequisite inside G1 | Stable-ID creation, state/view binding, container/pose integration, removal, reset, and house-rule manipulation tests |
| PR-040 | Dice are first-class physical Tabletop Objects; Roll uses authoritative RNG/state and Presentation-only tumble/settle | Core + Application + Tabletop Objects + Presentation | Immediate shared prerequisite inside G1 | d4/d6/d8/d10/d12/d20 creation, actor-aware Roll, deterministic RNG, state/view reconciliation, and Trap Floor generic-2d6 integration tests |
| PR-041 | New player-initiated actions preserve actor context and authoritative request boundaries without networking packages | Application + Multiplayer boundary | Immediate shared prerequisite onward; transport remains M6/M7 | No implicit Seat-0/local-user assumptions; request validation and assembly/dependency audit |

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
