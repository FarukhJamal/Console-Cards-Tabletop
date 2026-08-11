# Console Cards - Layout Design Requirements Matrix

**Document ID:** 17_Layout_Design_Requirements_Matrix  
**Version:** 1.5
**Status:** Approved with Open Decisions  
**Authoritative sources:** `Consolecards_LayoutRef_doc.pdf`, title "Game Design Notes", pages 1-6, supplied 2026-08-07; the approved Trap Floor correction in `18_Trap_Floor_Game_Requirements.md`; and the confirmed freeform tabletop rule philosophy supplied 2026-08-11
**Purpose:** Preserve the approved design requirements, distinguish Platform requirements from Game-specific content, and trace each requirement to current implementation and planned delivery.

## 1. Interpretation Rules

- The source PDF is an authoritative design-requirements source except where the later approved Trap Floor direction explicitly supersedes its obsolete Trap Door example.
- Reference screenshots communicate interaction or layout principles. They are not exact layouts that every Game Template must reproduce.
- "Physical" Card movement means smooth, controlled, natural Presentation behavior. It does not require physics-authoritative Runtime State or unrestricted physical simulation.
- `Console` means the universal personal Console system. `Game Board` means the Game-specific central Board and associated Play Areas.
- Trap Floor and Super Leroy Sisters are separate Game-specific Board types and content packages. Trap Floor is governed by `18_Trap_Floor_Game_Requirements.md`; unresolved details remain open rather than inheriting obsolete Trap Door rules.
- Runtime State remains authoritative. A requirement marked `Partial` or `Missing` is not implemented merely because an architectural extension point exists.
- Game Rules are primarily player-enforced. Rows describing a Game flow, cost, consequence, or win condition do not imply that coded execution is required for playable acceptance.
- Optional Game-specific assistance is tracked as assistance, not as ownership of the underlying Freeform Actions.

### Status values

- **Implemented:** Representative source exists for the stated requirement in the current local prototype or Runtime State model.
- **Partial:** Some required capability exists, but the full requirement is not delivered.
- **Architecture only:** Approved architecture anticipates the requirement, but usable implementation is absent.
- **Missing:** No implementing system was found in the current repository.

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
| LDR-022 | Table layout varies by Game and Player count; no reference screenshot is a universal fixed layout. | Platform-wide architecture; Game-specific configuration | **Partial:** Player Layout definitions, Play Area state, and Game Template layout references exist; official Game layouts are not yet authored. | M4/M4.1 foundation complete; G1/G2 content | PDF pp.3-4 | None. |
| LDR-023 | Keep the current Game's core action centered and arrange personal tools, resources, and controls around that center. | Platform-wide layout principle; Template-specific realization | **Partial:** authored Seat/Hand/Console poses surround a fixed central Play Area focus; official Game-specific realization remains. | M4/M4.1 foundation complete; validate in G1/G2 | PDF pp.4-5 | The core focus region is authored per Game Template. |
| LDR-024 | Preserve the same table scale/readability while switching among supported Player layouts. | Platform-wide | **Implemented for the three confirmed authored layouts at the data/projection foundation;** Game-specific visual acceptance remains. | M4 complete; validate in G1/G2 | PDF pp.1, 4-5 | OD-014 |

## 9. Universal Console vs Game-Specific Game Board

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-025 | The Console is universal: Players learn one persistent personal interaction/storage system reused across Games. | Platform-wide | **Partial:** Console/Slot Runtime State, Views, transfer, prototype interactions, and Template setup data exist; reuse must be validated with both official Games. | M4.1 foundation complete; validate in G1 and G2 | PDF pp.5-6 | Templates may configure Console contents without replacing the universal Console contract. |
| LDR-026 | The central Game Board is separate from the Console and is defined by the loaded Game. | Platform-wide boundary; Game-specific content | **Partial:** stable Play Area identity/bounds/focus and Template Play Area definitions exist; official Game Board content does not. | M4/M4.1 foundation complete; G1/G2 content | PDF pp.5-6 | None. |
| LDR-027 | Each Game may provide its own Board layout and mechanics; the Platform must not force one grid or Board type. | Platform-wide extension rule | **Architecture only for strategies:** the Game Template/Play Area boundary exists, but the Trap Floor grid and Super Leroy Sisters Side-Scroller are not implemented. | G1, G2 | PDF p.6 | Implement only Board types required by approved Games. |

## 10. Trap Floor

| ID | Requirement | Scope | Current implementation status | Planned milestone | Source/page | Unresolved decision |
|---|---|---|---|---|---|---|
| LDR-028 | Trap Floor supports 2-4 Players while the wider Platform retains independent structural support for 1-8. | Game-specific: Trap Floor | **Partial:** required Player count is representable by Game Templates and four-Player layouts exist; authored two- and three-Player mappings do not. | G1 | Trap Floor requirements §§2-3 | OD-014 for two- and three-Player Seat mappings. |
| LDR-029 | The Trap Floor Game Board is 36 Floor Cards in a fixed `6 x 6` X/Y coordinate grid. Floor Cards are not a drawable sequential Level Deck. | Game-specific: Trap Floor | **Foundation implemented:** Template-driven 36-Card coordinate Board and visible projection exist. Readable final content/polish and physical manual collapse handling remain G1 work; automatic collapse consequences are not required. | G1 manual playable completion, then polish | Trap Floor requirements §4 | Exact Floor Card visual design remains OD-018. |
| LDR-030 | Floorfall uses `2d6`: die 1 selects X, die 2 selects Y, and the resulting Floor Card collapses; in round 1, reroll a result that hits a starting corner. | Game-specific: Trap Floor | **Optional targeting assistance implemented:** the two official d6 are first-class generic physical Dice; authoritative X/Y resolution, round-1 corner protection, current target state, and prototype highlighting exist. Players may also roll and apply Floorfall manually. | Assistance foundation complete; manual usability/polish in G1 | Trap Floor requirements §4 | Exact collapse handling may remain player-executed; unresolved content remains OD-018. |
| LDR-034 | The Floormaster's Deck contains 36 Cards: 14 Trap, 14 Coin, and 8 Item; draw left, discard right, and reshuffle when exhausted. | Game-specific: Trap Floor | **Optional lifecycle assistance implemented:** official stable identity, 14/14/8 content categories, draw/pending/discard transfer, and exhaustion reshuffle exist. Generic manual draw, discard, and shuffle remain valid and required. | Assistance foundation complete; manual readability/usability in G1 | Trap Floor requirements §5 | Detailed Card text/content remains OD-018; coded effects are not required. |
| LDR-035 | Each Player uses the universal Console with Avatar in Main, Rule then Mode in Bottom Slots, up to three Items in Top Slots, a Controller Deck beside the Console, and a Pawn/meeple beginning on a corner Floor Card. | Game-specific setup using Platform Console | **Partial playable setup:** the Trap Floor Template uses generic Console Slots, Decks, Cards, Pawns, Seats, and starting poses; remaining readable content and unresolved Player layouts require completion or an explicit support claim. | G1 manual playable completion | Trap Floor requirements §6 | Avatar/content details and two-/three-Player layouts remain open. |
| LDR-036 | A shared pool contains 50 wooden coin cubes; acquired coins are stored on Player Consoles and spent coins return to the pool. | Game-specific: Trap Floor | **Partial:** generic authoritative Tokens and freeform movement exist; G1 must ensure the shared supply and Console storage are present/readable and manually usable. Automatic economy validation is not required. | G1 manual playable completion | Trap Floor requirements §7 | Coin Card content remains OD-018. |
| LDR-037 | Trap Floor lasts 10 rounds using `Start -> Search -> Trigger -> Floorfall -> End`; Search draws one Floormaster Card, Trigger resolves it immediately, and Hard Mode performs two Floorfalls. | Game-specific: Trap Floor | **Prototype optional orchestration implemented:** authoritative round/phase, per-Player Search/Trigger progress, Floorfall count, and schedule completion assistance exist. It is not mandatory play; G1 requires readable instructions and manual physical support. | Optional prototype retained; manual flow acceptance in G1 | Trap Floor requirements §§8, 11-12 | Detailed Card effects may remain player-resolved under OD-018. |
| LDR-038 | Easy uses one Floorfall and all-for-one elimination; Hard uses two Floorfalls and one-for-all elimination. The documented win condition for each is exactly 50 group/survivor coins within 10 rounds. | Game-specific: Trap Floor | **Player-facing rules:** automatic consequences and win/loss evaluation are intentionally not required. G1 must provide readable instructions and the physical Tokens/Pawns/actions needed for Players to apply them. | G1 manual playable completion | Trap Floor requirements §§9, 11-12 | Missing readable content remains relevant; coded enforcement does not. |
| LDR-039 | Controller Cards, Skill Cards, and universal A/B/X/Y Button inputs are distinct concepts. Search uses A/B/X/Y; Careful Search uses A+B+X+Y; documented Dodge costs X+Y+B and permits escape to one of eight adjacent tiles. | Game-specific content using universal inputs | **Partial:** universal Button Card definitions and generic manual Card manipulation exist; unresolved Controller/Skill content remains. Players may apply costs and effects manually; automatic validation is not required. | G1 readable content/manual play; optional assistance later only if justified | Trap Floor requirements §§10-12 | Exact Controller/Skill distinction, deck composition, costs, and Skill content remain OD-018. |

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
- Trap Floor Template/tabletop content, physical d6, Floorfall targeting assistance, Floormaster lifecycle assistance, and prototype round/phase orchestration exist. Their presence does not make assistance mandatory.
- No marquee collection selection, high-stakes Card-choice UI, or Super Leroy Sisters package was found in this documentation assessment.

