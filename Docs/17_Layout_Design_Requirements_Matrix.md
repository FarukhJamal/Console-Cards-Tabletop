# Console Cards - Layout Design Requirements Matrix

**Document ID:** 17_Layout_Design_Requirements_Matrix  
**Version:** 1.2
**Status:** Approved with Open Decisions  
**Authoritative source:** `Consolecards_LayoutRef_doc.pdf`, title "Game Design Notes", pages 1-6, supplied 2026-08-07  
**Purpose:** Preserve the PDF's design requirements, distinguish Platform requirements from Game-specific content, and trace each requirement to current implementation and planned delivery.

## 1. Interpretation Rules

- The source PDF is an authoritative design-requirements source.
- Reference screenshots communicate interaction or layout principles. They are not exact layouts that every Game Template must reproduce.
- "Physical" Card movement means smooth, controlled, natural Presentation behavior. It does not require physics-authoritative Runtime State or unrestricted physical simulation.
- `Console` means the universal personal Console system. `Game Board` means the Game-specific central Board and associated Play Areas.
- Trap Door and Super Leroy Sisters are separate Game-specific Board types and content packages. Their brief examples do not supply complete Game Rules.
- Runtime State remains authoritative. A requirement marked `Partial` or `Missing` is not implemented merely because an architectural extension point exists.

### Status values

- **Implemented:** Representative source exists for the stated requirement in the current local prototype or Runtime State model.
- **Partial:** Some required capability exists, but the full requirement is not delivered.
- **Architecture only:** Approved architecture anticipates the requirement, but usable implementation is absent.
- **Missing:** No implementing system was found in the current repository.

## 2. Player Count / Seating

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-001 | Support 1-8 Players and corresponding Seats. Four Players are the default presentation. | Platform-wide | **Partial:** `SeatState` and stable Seat IDs exist, but the prototype constructs one local Seat and no 1-8 Player layout controller exists. | M4 structural model and confirmed presets; network occupancy later | PDF p.1 | OD-014 retains the unconfirmed Player-count mappings. |
| LDR-002 | Provide eight available Seat positions around the table without enlarging the table as Player count increases. | Platform-wide | **Missing:** M1 provides stable tabletop coordinates and camera-local surface coverage, but no eight-position Seat layout exists. | M4 confirmed eight-Player preset | PDF p.1 | OD-014 retains only the unconfirmed Player-count mappings. |
| LDR-003 | Reposition occupied Seats toward the center for smaller Player counts instead of leaving Players on distant edge positions. | Platform-wide | **Missing** | M4 compact four-Player preset; other smaller-count mappings remain deferred | PDF pp.1, 4 | OD-014 |
| LDR-004 | Support a standard 4-Player layout, an 8-Player layout, and a compact/alternate 4-Player layout. | Platform-wide layout capability; Template-selected use | **Missing** | M4 confirmed presets | PDF p.4 | OD-014 retains the future selection rule and unconfirmed mappings. |
| LDR-005 | Show other Players' general presence and position around the table so turn/activity context is legible. | Platform-wide | **Missing:** no multiplayer presence presentation exists. | M4 structural Seat positions; presence delivery later | PDF pp.1, 3 | Exact presence and activity cues are not specified. |
| LDR-006 | By default, frame all important usable and interactable areas without hiding required play off-screen. | Platform-wide; evaluated per Game Template | **Partial:** camera pan, zoom, bookmarks, and visibility evaluation exist; adaptive whole-layout framing does not. | M4 central focus region; M4.1 Template-specific framing | PDF p.1 | Template-specific framing thresholds remain to be authored. |

## 3. Card Interaction

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-007 | Cards follow the pointer smoothly with controlled physical feel rather than harsh instantaneous snapping. | Platform-wide | **Implemented for the current prototype:** Card drag preview and interruptible Presentation transitions exist. This is controlled movement, not physics-authoritative simulation. | Maintain from M2-M3; acceptance review at P1 | PDF p.1 | None. |
| LDR-008 | Cards support natural free-form drag and drop on the tabletop, not only fixed-slot movement. | Platform-wide | **Implemented for single Cards:** tabletop movement and contained-Card transfer to tabletop/Containers exist. | Maintain from M2-M3; expand for groups before P1 closure unless required earlier | PDF p.1 | Group manipulation behavior is OD-016. |

## 4. Selection and Multi-Selection

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-009 | A hovered, selected, or held Card has clear visual feedback. | Platform-wide | **Partial:** one hovered and one selected View are tracked, and authored selection highlights exist; a complete distinct hover/held visual contract is not documented or generalized. | Remaining shared requirements before P1 closure | PDF p.2 | Exact hover versus selected visual treatment remains Presentation design. |
| LDR-010 | Click-hold-drag on empty space creates a marquee selection box that selects Cards inside it. | Platform-wide | **Missing:** current selection state stores one primary selected View. | Remaining shared requirements before P1 closure; earlier only if a Game requires it | PDF p.2 | OD-016 |
| LDR-011 | Every Card in a multi-selection has an obvious selected highlight. | Platform-wide | **Missing:** single-selection visuals exist; collection selection does not. | Remaining shared requirements before P1 closure; earlier only if a Game requires it | PDF p.2 | OD-016 |

## 5. Drop Indicators

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-012 | While dragging, show a live landing indicator that previews where the Card will land before release. | Platform-wide | **Partial:** the dragged Card previews pointer position and Containers show valid/source/invalid feedback; there is no explicit generalized landing indicator for exact tabletop or Play Area placement. | Remaining shared requirements before P1 closure; earlier only if a Game requires it | PDF p.2 | OD-016 |
| LDR-013 | A dragged group receives a live landing indicator for the group, not only the primary Card. | Platform-wide | **Missing** | Remaining shared requirements before P1 closure; earlier only if a Game requires it | PDF p.2 | OD-016 |

## 6. Card-Choice UI

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-014 | High-stakes choices between Cards use a large, central, readable UI rather than a minor popup. | Platform-wide Presentation capability; invoked by a Game | **Missing:** current OnGUI prototype menus are action controls, not a high-stakes Card-choice flow. | Remaining shared requirements before P1 closure; earlier only if a concrete Game implementation proves it is required | PDF p.2 | OD-017 |
| LDR-015 | The choice UI can be hidden temporarily to inspect the Board and reopened without losing the pending choice. | Platform-wide Presentation capability | **Missing** | Remaining shared requirements before P1 closure; earlier only if a concrete Game implementation proves it is required | PDF pp.2-3 | OD-017 |
| LDR-016 | Hover, candidate selection, confirmation, and registered-choice states provide clear feedback. | Platform-wide Presentation capability | **Missing** | Remaining shared requirements before P1 closure; earlier only if a concrete Game implementation proves it is required | PDF p.3 | OD-017 |

## 7. Visibility

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-017 | Hand visibility is controlled separately from personal Play Area visibility and individual Card visibility. | Platform-wide security and Presentation | **Partial:** `ObjectVisibility` and Owner-only Hand Containers exist; personal Play Area visibility and complete audience filtering do not. | Remaining shared requirements before P1 closure; multiplayer enforcement M7 | PDF pp.3-4 | OD-015 |
| LDR-018 | A Player can hide their Hand from other Players without necessarily hiding their personal Play Area. | Platform-wide | **Partial:** the prototype marks the local Hand Owner-only, but there is no multiplayer data filtering or user-facing independent control. | Remaining shared requirements before P1 closure; multiplayer enforcement M7 | PDF p.3 | OD-015 |
| LDR-019 | A Player can hide their personal Play Area independently from their Hand. | Platform-wide | **Missing:** Play Area Runtime State and visibility controls are not implemented. | Remaining shared requirements before P1 closure; multiplayer enforcement M7 | PDF p.3 | OD-015 |
| LDR-020 | An individual Card in a shared Play Area can be turned face-down to conceal its identity independently of Hand and Play Area visibility. | Platform-wide | **Partial:** Card face state and Flip use case exist, but shared-area visibility delivery and policy semantics are incomplete. | Remaining shared requirements before P1 closure; multiplayer enforcement M7 | PDF p.4 | OD-015 |
| LDR-021 | A Player sees their own tools/resources and receives only a limited awareness view of other Players' tools, while central shared play remains prominent. | Platform-wide with Game Template policy/configuration | **Missing** | Remaining shared requirements before P1 closure; multiplayer enforcement M7 | PDF p.5 | OD-015 defines what limited awareness may reveal. |

## 8. Interchangeable Layouts

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-022 | Table layout varies by Game and Player count; no reference screenshot is a universal fixed layout. | Platform-wide architecture; Game-specific configuration | **Architecture only:** Game Templates and Play Areas are documented but not implemented; the prototype layout is hardcoded composition. | M4, M4.1 | PDF pp.3-4 | None. |
| LDR-023 | Keep the current Game's core action centered and arrange personal tools, resources, and controls around that center. | Platform-wide layout principle; Template-specific realization | **Missing as a configurable layout system** | M4, M4.1 | PDF pp.4-5 | The core focus region is authored per Game Template. |
| LDR-024 | Preserve the same table scale/readability while switching among supported Player layouts. | Platform-wide | **Missing:** camera controls exist; Player-layout-aware framing and validation do not. | M4 | PDF pp.1, 4-5 | OD-014 |

## 9. Universal Console vs Game-Specific Game Board

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-025 | The Console is universal: Players learn one persistent personal interaction/storage system reused across Games. | Platform-wide | **Partial:** Console/Slot Runtime State, Views, transfer, and prototype interactions exist; reuse across loaded Game Templates is not implemented. | M4.1; validate in G1 and G2 | PDF pp.5-6 | Templates may configure Console contents without replacing the universal Console contract. |
| LDR-026 | The central Game Board is separate from the Console and is defined by the loaded Game. | Platform-wide boundary; Game-specific content | **Architecture only:** Board/Play Area distinction is documented; no Game Board loading exists. | M4, M4.1 | PDF pp.5-6 | None. |
| LDR-027 | Each Game may provide its own Board layout and mechanics; the Platform must not force one grid or Board type. | Platform-wide extension rule | **Architecture only:** optional Play Area strategies are documented; runtime implementation is missing. | M4, M4.1 | PDF p.6 | Implement only Board types required by approved Games. |

## 10. Trap Door

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-028 | Trap Door uses a Card-based level built from Level Cards, a meeple for Player position, 2d6 randomization, and Button Cards/Move Cards to resolve obstacles such as traps, enemies, keys, exits, and rewards. | Game-specific: Trap Door | **Missing:** generic Cards, Button Card definitions, Pawn/meeple state, Containers, and interactions are reusable, but no Trap Door content or rules exist. | G1 | PDF p.5 | OD-018 |
| LDR-029 | Trap Door's stated flow is: reveal Level Card, view obstacle, play Button Cards/Move Card, resolve Card, move Player, reveal next Card. | Game-specific: Trap Door | **Missing** | G1 | PDF p.5 | OD-018; the PDF does not define resolution formulas, setup, completion, or failure rules. |
| LDR-030 | Trap Door uses its own dungeon/room-style central Game Board, distinct from the universal Console. | Game-specific: Trap Door | **Missing** | M4 foundation; G1 content | PDF p.6 | OD-018 |

## 11. Super Leroy Sisters

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-031 | Super Leroy Sisters uses a side-scrolling level made from Cards generated by a Level Deck, with a meeple moving Card by Card and new sections revealed as progress is made. | Game-specific: Super Leroy Sisters | **Missing:** generic Cards, Deck, Pawn/meeple, and interactions are reusable; the Side-Scroller Play Area and Game content are absent. | G2 | PDF p.6 | OD-019 |
| LDR-032 | Super Leroy Sisters resolves obstacles with Button Cards and Move Cards. | Game-specific: Super Leroy Sisters | **Missing** | G2 | PDF p.6 | OD-019 |
| LDR-033 | Super Leroy Sisters' stated flow is: draw Level Card, place it into the level, move Player, encounter obstacle, play Button Cards/Move Card, resolve obstacle, continue to the next Card. | Game-specific: Super Leroy Sisters | **Missing** | G2 | PDF p.6 | OD-019; the PDF does not define setup, card-generation rules, obstacle formulas, completion, or failure rules. |

## 12. Repository Assessment Basis

The status column was assessed from the current repository without running Unity or tests. Representative existing systems include:

- M0 Core Runtime State, IDs, Seats, Containers, Cards, Pawns, Tokens, Consoles, and visibility enum under `Assets/ConsoleCards/Runtime/Core/`.
- M1 Camera, tabletop coordinate conversion, surface proxy, and visibility evaluation under `Assets/ConsoleCards/Presentation/Camera/`, `Coordinates/`, and `TableSurface/`.
- M2-M3 selection, drag preview, movement, Card transfer, drop-target feedback, natural Hand reorder, Deck/Stack/Discard/Hand/Console Views, and existing Application use cases under `Assets/ConsoleCards/Presentation/` and `Assets/ConsoleCards/Runtime/Application/`.
- No `PlayAreaState` implementation, Game Template loader, marquee collection selection, high-stakes Card-choice UI, Trap Door package, or Super Leroy Sisters package was found.

