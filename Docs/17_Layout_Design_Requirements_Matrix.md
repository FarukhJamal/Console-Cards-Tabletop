# Console Cards - Layout Design Requirements Matrix

**Document ID:** 17_Layout_Design_Requirements_Matrix  
**Version:** 1.8
**Status:** Approved with Open Decisions  
**Authoritative sources:** `Consolecards_LayoutRef_doc.pdf`, title "Game Design Notes", pages 1-6, supplied 2026-08-07; the latest Russell/Milanote Trap Floor direction supplied 2026-09-08 and recorded in `18_Trap_Floor_Game_Requirements.md`; the confirmed freeform tabletop rule philosophy supplied 2026-08-11; and the user-approved visual/physical framework supplied 2026-09-03, recorded in ADR-026
**Purpose:** Preserve the approved design requirements, distinguish Platform requirements from Game-specific content, and trace each requirement to current implementation and planned delivery.

## 1. Interpretation Rules

- The source PDF is an authoritative design-requirements source except where the later approved Trap Floor direction explicitly supersedes its obsolete Trap Door example.
- Reference screenshots communicate interaction or layout principles. They are not exact layouts that every Game Template must reproduce.
- ADR-025 supersedes the former Presentation-only interpretation of physical loose Card movement: holding is controlled/kinematic and release may use Rigidbody simulation, with settled 3D state accepted through the Application authority boundary. Contained Cards remain layout-controlled. This approval is not implementation evidence.
- `Console` means the universal personal Console system. `Game Board` means the Game-specific central Board and associated Play Areas.
- Trap Floor and Super Leroy Sisters are separate Game-specific Board types and content packages. Trap Floor is governed by `18_Trap_Floor_Game_Requirements.md`; unresolved details remain open rather than inheriting obsolete Trap Door rules.
- Runtime State remains authoritative. A requirement marked `Partial` or `Missing` is not implemented merely because an architectural extension point exists.
- Game Rules are primarily player-enforced. Rows describing a Game flow, cost, consequence, or win condition do not imply that coded execution is required for playable acceptance.
- Optional Game-specific assistance is tracked as assistance, not as ownership of the underlying Freeform Actions.
- ADR-026 records the approved Platform visual/physical framework. For Trap Floor content, the latest Milanote card-set structure is the current reference; exact counts/content remain provisional unless explicitly confirmed in `18_Trap_Floor_Game_Requirements.md`. The former 14 Trap + 14 Coin + 8 Item Floormaster Deck and 50-coin setup are legacy/reference material only.

### Status values

- **Implemented:** Representative source exists for the stated requirement in the current local prototype or Runtime State model.
- **Partial:** Some required capability exists, but the full requirement is not delivered.
- **Architecture only:** Approved architecture anticipates the requirement, but usable implementation is absent.
- **Missing:** No implementing system was found in the current repository.
- **Approved; not assessed:** The requirement is approved, but implementation/asset compliance was not assessed by this documentation update.

## 2. Player Count / Seating

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-001 | Support 1-8 Players and corresponding Seats. Four Players are the default presentation. | Platform-wide | **Partial:** Unity-free Player Layout data supports 1-8 structurally and Match setup supports Seats; authored 1-3 and 5-7 layouts and multiplayer occupancy remain unresolved. | M4 complete for structural model/confirmed presets; remaining mappings OD-014; network occupancy later | PDF p.1 | OD-014 retains the unconfirmed Player-count mappings. |
| LDR-002 | Provide eight available Seat positions around the table without enlarging the table as Player count increases. | Platform-wide | **Implemented for the confirmed preset:** the authored eight-Player definition uses the fixed table and central Play Area. | M4 complete | PDF p.1 | OD-014 retains only the unconfirmed Player-count mappings. |
| LDR-003 | Reposition occupied Seats toward the center for smaller Player counts instead of leaving Players on distant edge positions. | Platform-wide | **Partial:** compact four-Player placement is authored; smaller 1-3 and 5-7 mappings are not. | M4 complete for compact four-Player; remaining mappings deferred | PDF pp.1, 4 | OD-014 |
| LDR-004 | Support a standard 4-Player layout, an 8-Player layout, and a compact/alternate 4-Player layout. | Platform-wide layout capability; Template-selected use | **Implemented:** all three confirmed definitions exist and a Game Template selects a PlayerLayoutId. | M4/M4.1 complete | PDF p.4 | OD-014 retains the future host-selection rule and unconfirmed mappings. |
| LDR-005 | Show other Players' general presence and position around the table so turn/activity context is legible. | Platform-wide | **Partial:** authored Seat/player-zone positions exist; multiplayer presence/activity presentation does not. | M4 structural positions complete; presence delivery later | PDF pp.1, 3 | Exact presence and activity cues are not specified. |
| LDR-006 | By default, frame all important usable and interactable areas without hiding required play off-screen. | Platform-wide; evaluated per Game Template | **Partial:** central focus/bounds and in-memory camera bookmark data exist; adaptive whole-layout framing and Game-specific acceptance remain. | M4/M4.1 foundation complete; validate in G1/G2 | PDF p.1 | Template-specific framing thresholds remain to be authored. |

## 3. Card Interaction

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-007 | Cards follow the pointer smoothly while held, then use the approved physical release model. | Platform-wide | **Partial for ADR-025:** prior Card drag preview and interruptible Presentation transitions exist; shared kinematic holding, physical release, and authoritative 3D settlement remain pending. | ADR-025 integration gate; acceptance review at P1 | PDF p.1; ADR-025 | No new UI design implied. |
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
| LDR-012 | While dragging, preview the intended surface placement or Container layout; a physical throw's final resting pose is not guaranteed by the indicator. | Platform-wide | **Partial:** the dragged Card previews pointer position and Containers show valid/source/invalid feedback; a generalized surface/layout indicator remains pending. | Remaining shared requirements before P1 closure; earlier only if a Game requires it | PDF p.2; ADR-025 | OD-016 |
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
| LDR-022 | Table layout varies by Game and Player count; no reference screenshot is a universal fixed layout. | Platform-wide architecture; Game-specific configuration | **Partial:** Player Layout definitions, Play Area state, and Game Template layout references exist; official Game layouts are not yet authored. | M4/M4.1 foundation complete; G1/G2 content | PDF pp.3-4 | None. |
| LDR-023 | Keep the current Game's core action centered and arrange personal tools, resources, and controls around that center. | Platform-wide layout principle; Template-specific realization | **Partial:** authored Seat/Hand/Console poses surround a fixed central Play Area focus; official Game-specific realization remains. | M4/M4.1 foundation complete; validate in G1/G2 | PDF pp.4-5 | The core focus region is authored per Game Template. |
| LDR-024 | Preserve the same table scale/readability while switching among supported Player layouts. | Platform-wide | **Implemented for the three confirmed authored layouts at the data/projection foundation;** Game-specific visual acceptance remains. | M4 complete; validate in G1/G2 | PDF pp.1, 4-5 | OD-014 |

## 9. Universal Console vs Game-Specific Game Board

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-025 | The Console is universal: Players learn one persistent personal interaction/storage system reused across Games. | Platform-wide | **Partial:** Console/Slot Runtime State, Views, transfer, prototype interactions, and Template setup data exist; reuse must be validated with both official Games. | M4.1 foundation complete; validate in G1 and G2 | PDF pp.5-6 | Templates may configure Console contents without replacing the universal Console contract. |
| LDR-026 | The central Game Board is separate from the Console and is defined by the loaded Game. | Platform-wide boundary; Game-specific content | **Partial:** stable Play Area identity/bounds/focus and Template Play Area definitions exist; official Game Board content does not. | M4/M4.1 foundation complete; G1/G2 content | PDF pp.5-6 | None. |
| LDR-027 | Each Game may provide its own Board layout and mechanics; the Platform must not force one grid or Board type. | Platform-wide extension rule | **Architecture only for strategies:** the Game Template/Play Area boundary exists, but the Trap Floor grid and Super Leroy Sisters Side-Scroller are not implemented. | G1, G2 | PDF p.6 | Implement only Board types required by approved Games. |

### 9.1 Shared Visual and Physical Framework

The following additions record Platform direction only. Trap Floor's current Game-specific composition/setup is governed separately by `18_Trap_Floor_Game_Requirements.md`.

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-040 | System Cards use poker-card proportions. | Platform visual framework | **Approved; not assessed** | No scheduling change in this approval | User approval 2026-09-03; ADR-026 | Exact artwork dimensions are not prescribed. |
| LDR-041 | Console is configurable, not permanently six Slots; support a Main Slot, optional Side Slots, Cube Slots, and Dice Slots. A Game Template may use only the Slots it needs. | Platform capability; Template configuration | **Approved; not assessed** | No scheduling change in this approval | User approval 2026-09-03; ADR-026 | No universal Slot count or per-Game allocation is inferred. |
| LDR-042 | Console may be horizontal or vertical while retaining the universal Console contract. | Platform visual/layout framework | **Approved; not assessed** | No scheduling change in this approval | User approval 2026-09-03; ADR-026 | No default orientation is imposed on existing Templates. |
| LDR-043 | Preserve Slot-symbol language, including newer Milanote teal fill, Plus / Diamond / Minus bottom-slot symbols, and nested-shape combinations. | Platform visual framework | **Approved; not assessed** | No scheduling change in this approval | Newer Milanote revision summary approved 2026-09-03; ADR-026 | Exact teal value and artwork geometry are not specified by the summary. |
| LDR-044 | Physical reference sizing: approximately 16 mm for Dice/chits/meeples and 8 mm for cubes. | Platform physical authoring reference | **Approved; not assessed** | No scheduling change in this approval | User approval 2026-09-03; ADR-026 | Not an exact collider size, manufacturing specification, or Unity-unit conversion. |

## 10. Trap Floor

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-028 | Trap Floor supports 2-4 Players while the wider Platform retains independent structural support for 1-8. | Game-specific: Trap Floor | **Partial:** required Player count is representable by Game Templates and four-Player layouts exist; authored two- and three-Player mappings do not. | G1 | Trap Floor requirements §§2-3 | OD-014 for two- and three-Player Seat mappings. |
| LDR-029 | The Trap Floor Game Board is 36 Floor Cards in a fixed `6 x 6` X/Y coordinate grid. Floor Cards are not a drawable sequential Level Deck. | Game-specific: Trap Floor | **Foundation implemented:** Template-driven 36-Card coordinate Board and visible projection exist. Readable final content/polish and physical manual collapse handling remain G1 work; automatic collapse consequences are not required. | G1 manual playable completion, then polish | Trap Floor requirements §4 | Exact Floor Card visual design remains OD-018. |
| LDR-030 | Setup uses `2d6` to identify a starting Floor position. At provisional intervals, the separate Floor Collapse operation also uses `2d6` to identify a Floor that becomes a permanent hole/unusable space. Search does not perform Collapse. | Game-specific: Trap Floor | **Partial/legacy assistance:** the two official d6 and X/Y targeting infrastructure exist, but the implemented round-one exception and old Floorfall schedule are not current authority. Generic physical Dice remain reusable. | Align current readable setup and optional assistance in G1 | Trap Floor requirements §§5, 7, 11 | Setup details, Collapse timing, protection/reroll conditions, and falling rules remain OD-018. |
| LDR-034 | Use the latest Milanote card-set structure as the current Trap Floor content reference. Floor Cards may reveal Keys, Traps, or other effects; exact counts/content are provisional unless explicitly confirmed. | Game-specific: Trap Floor | **Legacy mismatch:** the current Floormaster implementation uses the superseded 14 Trap + 14 Coin + 8 Item build and does not establish current content authority. Generic Card/Deck operations remain reusable. | Current readable content alignment in G1 | Trap Floor requirements §§1, 4, 9 | Exact counts, names, text, and effect distribution remain OD-018. |
| LDR-035 | Each Player uses the universal Console with the Avatar in the Main Slot. Other Console Slot usage, Controller Decks, Skill/Ability architecture, and Hand/draw/discard structure are not hard-locked. | Game-specific setup using Platform Console | **Partial/legacy mismatch:** the Template has Consoles and Avatars but also encodes superseded Slot and Controller Deck assumptions. | Current setup alignment in G1 | Trap Floor requirements §§8-10 | Avatar details, other Slot usage, and two-/three-Player layouts remain open. |
| LDR-036 | The objective is to find all required Keys and bring them to the Exit. The number of Keys remains provisional. | Game-specific: Trap Floor | **Not assessed against current direction:** earlier 50-coin setup is legacy and generic Tokens alone do not implement the current objective. | G1 current content/manual play | Trap Floor requirements §§6, 9-12 | Key count and exact Key/Exit content remain OD-018. |
| LDR-037 | Movement is orthogonal only; Players may share a Floor tile; normal movement remains flexible and is generally no more than three tiles. Search flips/reveals the interacted Floor Card, which may reveal a Key, Trap, or other effect. | Game-specific: Trap Floor | **Partial:** generic Pawn movement and Card flip exist; old Floormaster Search and round orchestration are legacy assistance rather than current Game Rules. | G1 readable content/manual play | Trap Floor requirements §§4, 10-12 | Exact movement values, Avatar variation, Trap costs, and action economy remain OD-018. |
| LDR-038 | Players lose by failing a Trap or running out of usable Floor. Floor Collapse is separate from Search and creates permanent holes. | Game-specific: Trap Floor | **Not assessed against current direction:** automatic consequences are not required, but readable current rules and physical support are required. | G1 manual playable completion | Trap Floor requirements §§6-7, 12 | Trap consequences, usable-Floor exhaustion, Collapse timing, and falling rules remain OD-018. |
| LDR-039 | Do not hard-lock Controller Decks, Skill/Ability architecture, other Console Slot usage, Hand/draw/discard rules, modes, or a round limit. Avatar stats/abilities, exact movement values, Trap costs, Key count, Collapse timing, falling rules, and action economy remain provisional. | Game-specific content boundary | **Legacy mismatch:** existing prototype systems may embody superseded choices but do not make them current authority. | Preserve flexibility during G1 alignment | Trap Floor requirements §§8-10, 13 | OD-018 tracks the provisional details. |

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
- M4 Player Layout definitions and central Play Area state exist under `Assets/ConsoleCards/Runtime/Core/`.
- M4.1 Game Template schema, validation, local content resolution, atomic Match construction, and in-memory reset baseline exist under `Assets/ConsoleCards/Runtime/GameTemplates/`.
- Trap Floor Template/tabletop content, physical d6, Floor-coordinate targeting assistance, legacy Floormaster lifecycle assistance, and legacy prototype round/phase orchestration exist. The latter systems reflect the superseded build; none of this implementation history makes assistance mandatory or establishes current Game-content authority.
- No marquee collection selection, high-stakes Card-choice UI, or Super Leroy Sisters package was found in this documentation assessment.

