# Console Cards - Console Layout Slots, Cube Cells, and Mat Surface

**Document ID:** 19_Console_Layout_Slots_And_Mat_Surface
**Version:** 0.2
**Status:** Stages (a) and (a2) implemented (2026-10-06/07; records in §4.4 and §11). Stages (a2b) onward each need their own approved plan; the current order is §9.1. Binding platform rules: `21_Platform_Principles.md`.
**Source:** Stage 3c-1 read-only report (cube cells and the mat as a surface) and the user's decisions on it.
**Purpose:** Define the console as one generic component whose mat exposes every card slot and every cube position as data, so any Game can choose what to use.

## 1. Scope and boundaries

- The Console is one generic, game-agnostic component (ADR-020, ADR-026). Trap Floor and Super Leroy Sisters are data templates; play is multiplayer and user-authored.
- ADR-026: Cube Slots and Dice Slots are part of the Console framework as "configurable layout roles, not new Runtime types". This proposal therefore adds no new `ContainerKind`; every slot is a `ContainerState` with `ContainerKind.ConsoleSlot`.
- `05_Core_Data_Model` already sketches `ConsoleSlotState { Accepted Capability/Tag Filters, Capacity, Ordered Object IDs }`; this proposal implements the accepted-kinds filter and keeps `Capacity` (0 = unbounded) as it is today.
- Unchanged: authority and Undo contracts (Commands with the right record mode), held bodies dynamic, no per-frame allocations, no piece-transform writes outside the command/presentation-transition path, frozen pieces solid and never merge targets, Prototype* prefabs untouched, scene edits only after the exact YAML lines are approved.
- Separate from this document: the pending Stage 3c-0 surface-height write.

## 2. Recorded decisions (2026-10-03)

1. **Edge plates:** option 2. Top and bottom card anchors move out to z ±1.7811 and rails to x ±2.9753 so a 1.4 × 1.4 footprint clears the track cells. Data only.
2. **Cube cells are containers.** Accepted object kinds are enforced in Core. A slot with no stored accepted-kinds value accepts all kinds, so existing snapshots load unchanged.
3. **Capacities:** track cell = 1; big square = 0 (unbounded), with contents laid out in layers inside the footprint. Capacity is per-slot component data. *(Amended 2026-10-05: templates do not override it; see 2.1.)*
4. **Toolbox console** exposes all 9 card slots and all 63 cube cells. One component definition, no reduced variant.
5. **Cube piece:** size 0.126 world (about 8 mm, ADR-026), kept as data.
6. **IDs:** stable slot keys plus a new ID range with no 15-side-slot cap (section 6).
7. **Stage order:** (a) definition + variant, (c) mat support surface, (b) card slot targeting and settle, (d) cube cells. The `ContainedCardDragCoordinator.Release` exclusion bug is fixed in (b). The token drop path is dead code, so (d) uses the live move path. *(Order superseded 2026-10-07 by §9.1.)*
8. **Measurement rules:** measured cell centres (no fixed pitch), cyan cells stored as a `marker` flag, half-pixel correction applied to the card anchors.

### 2.1 Addendum principles (2026-10-05)

- **Games never add, remove or restrict slots.** Every console slot is always usable, as on a real tabletop. A template may only give slots starting content, and rule hooks may refer to slots by key. Slot keys in a template exist for starting content and rule hooks only; there is no notion of "active slots".
- **Component structure stays a component property:** accepted kinds and capacity come from the layout asset, not from a template. Loose pieces can always rest anywhere on the mat.
- **Turns and phases are advisory** (doc 20 §8): no slot or console check depends on turn or phase.

## 3. Texture measurements (`Gamemat - 1.png`, 2550 × 1325 px)

World frame: W = 6.0, origin at the mat centre, +x right, +z toward the top of the texture, z scaled by 0.987. Centres use pixel-edge coordinates, `(x0 + x1 + 1) / 2` against the image centre (1275, 662.5). This removes the half-pixel bias (up to 0.0012) in the earlier card anchors.

- Mat: 6.000 × 3.077 world (half extents 3.000 × 1.5386).
- **Track cells (54):** top row 20, bottom row 20, left column 7, right column 7. Each cell is 87–88 px square (about 0.207 × 0.204 world). Pitch varies from 96 to 99 px (mean 97.05 px, 0.228 world), so anchors use each cell's measured centre. Gaps between cells are 0.02–0.03 world.
- **Cyan marker cells (10):** cells 5, 10, 15 and 20 of the top and bottom rows (every 5th) and cell 4 (the middle) of each side column. Same size as the others; stored as `marker = true`.
- The first and last cells of the top and bottom rows sit directly above and below the side columns. The top row is 5 px further from the centre than the bottom row (art asymmetry).
- **Big squares (9):** 3 × 3 grid of 175 × 175 px tiles (0.412 × 0.406 world).
- **Totals:** 9 card slots + 54 track cells + 9 big squares = **72 slots**.

## 4. Slot table (layout asset content)

Card footprints are the 1.4 × 1.4 worst case (either orientation, off-axis yaw accepted), except Main, which uses its measured screen rect. Cube footprints are the measured cell rects. Orientation is the slot's default card orientation for placement; any card can still be placed in either orientation. Capacity is the component's current default (0 = unbounded); it is a component property that templates do not change, and it should become a component-declared configurable parameter (§5). Ordinal is the stable `idOrdinal` (section 6). Card ordinals 5–9 were renumbered once in Stage (a), before any session used them; from now on ordinals are append-only and must never be reused or renumbered.

| Ordinal | Key | Kind | Group | x | z | Footprint w × d | Orientation | Default capacity | Marker | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `Main` | Card | Main | -0.8141 | +0.0070 | 2.0941 × 1.4097 | Landscape | 1 |  | screen px 484–1373 × 356–962 |
| 2 | `TopL` | Card | TopPlates | -1.7624 | +1.7811 | 1.400 × 1.400 | Portrait | 0 |  | plate px 176–875 × 0–69; z pushed out |
| 3 | `TopC` | Card | TopPlates | +0.0024 | +1.7811 | 1.400 × 1.400 | Portrait | 0 |  | plate px 926–1625 × 0–69; z pushed out |
| 4 | `TopR` | Card | TopPlates | +1.7600 | +1.7811 | 1.400 × 1.400 | Portrait | 0 |  | plate px 1673–2372 × 0–69; z pushed out |
| 5 | `RailL` | Card | Rails | -2.9753 | +0.0000 | 1.400 × 1.400 | Portrait | 0 |  | rail px 0–69; x pushed out |
| 6 | `RailR` | Card | Rails | +2.9753 | +0.0000 | 1.400 × 1.400 | Portrait | 0 |  | rail px 2480–2549; x pushed out |
| 7 | `BotL` | Card | BottomPlates | -1.7624 | -1.7811 | 1.400 × 1.400 | Portrait | 0 |  | plate px 176–875 × 1255–1324; z pushed out |
| 8 | `BotC` | Card | BottomPlates | +0.0024 | -1.7811 | 1.400 × 1.400 | Portrait | 0 |  | plate px 926–1625 × 1255–1324; z pushed out |
| 9 | `BotR` | Card | BottomPlates | +1.7600 | -1.7811 | 1.400 × 1.400 | Portrait | 0 |  | plate px 1673–2372 × 1255–1324; z pushed out |
| 10 | `T01` | Cube | TopTrack | -2.1682 | +0.9789 | 0.205 × 0.204 | — | 1 |  | px 310–396 × 197–284 |
| 11 | `T02` | Cube | TopTrack | -1.9424 | +0.9789 | 0.205 × 0.204 | — | 1 |  | px 406–492 × 197–284 |
| 12 | `T03` | Cube | TopTrack | -1.7082 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 505–592 × 197–284 |
| 13 | `T04` | Cube | TopTrack | -1.4824 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 601–688 × 197–284 |
| 14 | `T05` | Cube | TopTrack | -1.2541 | +0.9789 | 0.207 × 0.204 | — | 1 | yes | px 698–785 × 197–284 |
| 15 | `T06` | Cube | TopTrack | -1.0282 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 794–881 × 197–284 |
| 16 | `T07` | Cube | TopTrack | -0.7953 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 893–980 × 197–284 |
| 17 | `T08` | Cube | TopTrack | -0.5694 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 989–1076 × 197–284 |
| 18 | `T09` | Cube | TopTrack | -0.3412 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 1086–1173 × 197–284 |
| 19 | `T10` | Cube | TopTrack | -0.1153 | +0.9789 | 0.207 × 0.204 | — | 1 | yes | px 1182–1269 × 197–284 |
| 20 | `T11` | Cube | TopTrack | +0.1176 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 1281–1368 × 197–284 |
| 21 | `T12` | Cube | TopTrack | +0.3435 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 1377–1464 × 197–284 |
| 22 | `T13` | Cube | TopTrack | +0.5718 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 1474–1561 × 197–284 |
| 23 | `T14` | Cube | TopTrack | +0.8047 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 1573–1660 × 197–284 |
| 24 | `T15` | Cube | TopTrack | +1.0306 | +0.9789 | 0.207 × 0.204 | — | 1 | yes | px 1669–1756 × 197–284 |
| 25 | `T16` | Cube | TopTrack | +1.2588 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 1766–1853 × 197–284 |
| 26 | `T17` | Cube | TopTrack | +1.4847 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 1862–1949 × 197–284 |
| 27 | `T18` | Cube | TopTrack | +1.7106 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 1958–2045 × 197–284 |
| 28 | `T19` | Cube | TopTrack | +1.9459 | +0.9789 | 0.207 × 0.204 | — | 1 |  | px 2058–2145 × 197–284 |
| 29 | `T20` | Cube | TopTrack | +2.1718 | +0.9789 | 0.207 × 0.204 | — | 1 | yes | px 2154–2241 × 197–284 |
| 30 | `R1` | Cube | RightTrack | +2.1718 | +0.6746 | 0.207 × 0.204 | — | 1 |  | px 2154–2241 × 328–415 |
| 31 | `R2` | Cube | RightTrack | +2.1718 | +0.4494 | 0.207 × 0.204 | — | 1 |  | px 2154–2241 × 425–512 |
| 32 | `R3` | Cube | RightTrack | +2.1718 | +0.2195 | 0.207 × 0.204 | — | 1 |  | px 2154–2241 × 524–611 |
| 33 | `R4` | Cube | RightTrack | +2.1718 | -0.0035 | 0.207 × 0.204 | — | 1 | yes | px 2154–2241 × 620–707 |
| 34 | `R5` | Cube | RightTrack | +2.1718 | -0.2288 | 0.207 × 0.204 | — | 1 |  | px 2154–2241 × 717–804 |
| 35 | `R6` | Cube | RightTrack | +2.1718 | -0.4517 | 0.207 × 0.204 | — | 1 |  | px 2154–2241 × 813–900 |
| 36 | `R7` | Cube | RightTrack | +2.1718 | -0.6746 | 0.207 × 0.204 | — | 1 |  | px 2154–2241 × 909–996 |
| 37 | `B01` | Cube | BottomTrack | -2.1682 | -0.9673 | 0.205 × 0.204 | — | 1 |  | px 310–396 × 1035–1122 |
| 38 | `B02` | Cube | BottomTrack | -1.9424 | -0.9673 | 0.205 × 0.204 | — | 1 |  | px 406–492 × 1035–1122 |
| 39 | `B03` | Cube | BottomTrack | -1.7082 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 505–592 × 1035–1122 |
| 40 | `B04` | Cube | BottomTrack | -1.4824 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 601–688 × 1035–1122 |
| 41 | `B05` | Cube | BottomTrack | -1.2541 | -0.9673 | 0.207 × 0.204 | — | 1 | yes | px 698–785 × 1035–1122 |
| 42 | `B06` | Cube | BottomTrack | -1.0282 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 794–881 × 1035–1122 |
| 43 | `B07` | Cube | BottomTrack | -0.7953 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 893–980 × 1035–1122 |
| 44 | `B08` | Cube | BottomTrack | -0.5694 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 989–1076 × 1035–1122 |
| 45 | `B09` | Cube | BottomTrack | -0.3412 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 1086–1173 × 1035–1122 |
| 46 | `B10` | Cube | BottomTrack | -0.1153 | -0.9673 | 0.207 × 0.204 | — | 1 | yes | px 1182–1269 × 1035–1122 |
| 47 | `B11` | Cube | BottomTrack | +0.1176 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 1281–1368 × 1035–1122 |
| 48 | `B12` | Cube | BottomTrack | +0.3435 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 1377–1464 × 1035–1122 |
| 49 | `B13` | Cube | BottomTrack | +0.5718 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 1474–1561 × 1035–1122 |
| 50 | `B14` | Cube | BottomTrack | +0.8047 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 1573–1660 × 1035–1122 |
| 51 | `B15` | Cube | BottomTrack | +1.0306 | -0.9673 | 0.207 × 0.204 | — | 1 | yes | px 1669–1756 × 1035–1122 |
| 52 | `B16` | Cube | BottomTrack | +1.2588 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 1766–1853 × 1035–1122 |
| 53 | `B17` | Cube | BottomTrack | +1.4847 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 1862–1949 × 1035–1122 |
| 54 | `B18` | Cube | BottomTrack | +1.7106 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 1958–2045 × 1035–1122 |
| 55 | `B19` | Cube | BottomTrack | +1.9459 | -0.9673 | 0.207 × 0.204 | — | 1 |  | px 2058–2145 × 1035–1122 |
| 56 | `B20` | Cube | BottomTrack | +2.1718 | -0.9673 | 0.207 × 0.204 | — | 1 | yes | px 2154–2241 × 1035–1122 |
| 57 | `L1` | Cube | LeftTrack | -2.1671 | +0.6746 | 0.207 × 0.204 | — | 1 |  | px 310–397 × 328–415 |
| 58 | `L2` | Cube | LeftTrack | -2.1671 | +0.4494 | 0.207 × 0.204 | — | 1 |  | px 310–397 × 425–512 |
| 59 | `L3` | Cube | LeftTrack | -2.1671 | +0.2195 | 0.207 × 0.204 | — | 1 |  | px 310–397 × 524–611 |
| 60 | `L4` | Cube | LeftTrack | -2.1671 | -0.0035 | 0.207 × 0.204 | — | 1 | yes | px 310–397 × 620–707 |
| 61 | `L5` | Cube | LeftTrack | -2.1671 | -0.2288 | 0.207 × 0.204 | — | 1 |  | px 310–397 × 717–804 |
| 62 | `L6` | Cube | LeftTrack | -2.1671 | -0.4517 | 0.207 × 0.204 | — | 1 |  | px 310–397 × 813–900 |
| 63 | `L7` | Cube | LeftTrack | -2.1671 | -0.6746 | 0.207 × 0.204 | — | 1 |  | px 310–397 × 909–996 |
| 64 | `G11` | Cube | Grid | +0.6788 | +0.5086 | 0.412 × 0.406 | — | 0 |  | px 1476–1650 × 356–530 |
| 65 | `G12` | Cube | Grid | +1.2224 | +0.5086 | 0.412 × 0.406 | — | 0 |  | px 1707–1881 × 356–530 |
| 66 | `G13` | Cube | Grid | +1.7635 | +0.5086 | 0.412 × 0.406 | — | 0 |  | px 1937–2111 × 356–530 |
| 67 | `G21` | Cube | Grid | +0.6788 | +0.0070 | 0.412 × 0.406 | — | 0 |  | px 1476–1650 × 572–746 |
| 68 | `G22` | Cube | Grid | +1.2224 | +0.0070 | 0.412 × 0.406 | — | 0 |  | px 1707–1881 × 572–746 |
| 69 | `G23` | Cube | Grid | +1.7635 | +0.0070 | 0.412 × 0.406 | — | 0 |  | px 1937–2111 × 572–746 |
| 70 | `G31` | Cube | Grid | +0.6788 | -0.4947 | 0.412 × 0.406 | — | 0 |  | px 1476–1650 × 788–962 |
| 71 | `G32` | Cube | Grid | +1.2224 | -0.4947 | 0.412 × 0.406 | — | 0 |  | px 1707–1881 × 788–962 |
| 72 | `G33` | Cube | Grid | +1.7635 | -0.4947 | 0.412 × 0.406 | — | 0 |  | px 1937–2111 × 788–962 |

### 4.1 Option 2 geometry

- Track outer edges: top row z +1.0811, bottom row z −1.0694, left column x −2.2706, right column x +2.2753.
- Plate anchors: z ±1.7811 = top-row edge + 0.7. The bottom row needs only −1.7694; the symmetric value leaves it 0.0117 spare.
- Rail anchors: x ±2.9753 = right-column edge + 0.7 (+0.0577 from 2.9176). The left column needs only −2.9706; the symmetric value leaves it 0.0047 spare.
- Cube-to-card clearance at a track cell: (0.204 − 0.126) / 2 = 0.039.

### 4.2 Overhang and footprint (mat + cards)

| Case | Half extents x × z | Full footprint | Overhang past mat edge |
|---|---|---|---|
| Worst case, 1.4 square in every edge slot | 3.6753 × 2.4811 | **7.351 × 4.962** | x 0.675 per side, z 0.943 per side |
| Best case, 1.0 square (card's narrow side) | 3.4753 × 2.2811 | 6.951 × 4.562 | x 0.475, z 0.743 |
| Mat only | 3.000 × 1.5386 | 6.000 × 3.077 | — |

### 4.3 Table fit, Trap Floor standard 4-player layout

Inputs:
- Console poses at radius 6.1 (`PlayerConsoleRadius`).
- Live table top (TCGTable2 `UsableTableSurface`): x [−12.578, +12.178], z [−8.544, +8.644]. 1 table unit = 1 world unit.
- Board: 6 × 6 grid of 0.72 × 1.0 cells, so x ±2.16, z ±3.0.
- The footprint is symmetric, so the result does not depend on which way the mat faces.

| Seat | Console pose | Footprint bbox (worst case) | Margin to table edge | Gap to board |
|---|---|---|---|---|
| 0 | (0, −6.1), 0° | x [−3.675, +3.675], z [−8.581, −3.619] | **−0.037 (over the near edge)** | 0.619 |
| 1 | (−6.1, 0), 90° | x [−8.581, −3.619], z [−3.675, +3.675] | 3.997 | 1.459 |
| 2 | (0, +6.1), 180° | x [−3.675, +3.675], z [+3.619, +8.581] | 0.063 | 0.619 |
| 3 | (+6.1, 0), 270° | x [+3.619, +8.581], z [−3.675, +3.675] | 3.596 | 1.459 |

- **Seats do not overlap.** The nearest pair (neighbouring seats) is 1.159 apart.
- **Seat 0 near edge:** a 1.4 card in a bottom plate hangs 0.037 past the table edge. A landscape card (0.5 half-depth) stays inside.
- **Trap Floor seat containers versus the larger console (status 2026-10-07).** These are code constants in `TrapFloorTemplateFactory`, not console data:
  - Hand: the radius-4.15 hand pose is removed (a2); the hand is a camera tray, and its zone default is console-local (+6.40, 0.00), 2.0 × 1.4.
  - Controller Deck at console-local x +4.45, Purchased Ability stack at −4.45, and starting-ability staging at −7.6 are stopgaps (clearance ≥ 0.175, table margin ≥ 0.444) until (a2b) replaces them with `ConsoleAdjacentPlacement`.

### 4.4 Stage (a) record (2026-10-06)

- **Layout asset:** `Content/Definitions/Console/StandardConsoleLayout.asset`, 72 entries as in §4; each slot carries kind, footprint, default capacity, marker and default orientation.
- **Mat console:** `Content/Prefabs/Real/ConsoleMat.prefab` (not a Prototype* prefab). The mat is a nested `ConsoleCard` prefab instance with local scale (279.94424, 102.52346, 5.9562984); the slot feedback plates match the slot footprints (Main 2.0941 × 1.4097).
- **Mat rotation fix:** the mat instance is at local rotation (90, 0, 0) (quaternion x = w = 0.7071068) and local position (−0.000595, 0, −0.000156). It was previously authored at (90, 0, 180), which maps console (x, z) to (−x, −z) and showed the art rotated 180°. Layout data and anchors are unchanged.
- **Main slot:** footprint measured from the on-mat screen (2.0941 × 1.4097, landscape). Trap Floor turns the avatar to Main's layout default (+90° for Landscape).
- **Consoles:** every console (Trap Floor and Toolbox) exposes all 9 card slots, with capacities from the layout asset; no slot selection or hiding. Trap Floor keeps its category-41 IDs; its Side n slot maps to ordinal n + 1. Cube entries are layout data only; they become containers in (d).
- **Scene:** repointed to `ConsoleMat.prefab` (no reference to the old prefab GUID remains); `PrototypeConsole.prefab` restored from git.

## 5. Slot-kind model

- **Core (Stage d):** each slot is a `ContainerState(ContainerKind.ConsoleSlot)` with `Capacity` (existing) and a new optional `AcceptedObjectKinds` (flags over `TabletopObjectKind`). Missing means accept all. `TransferCardUseCase` and `TransferTokenUseCase` reject a destination that does not accept the object's kind, as a structural check alongside `IsFull`.
- **Slot kinds (authored):** `Card → {Card}`, `Cube → {Token}`, later `Dice → {Die}`. The kind lives in the layout data; Core stores only the accepted kinds.
- **Console state (Stage d):** each slot records its layout key. If no key is stored (existing consoles), slots map positionally to the layout's card slots in ordinal order.
- **Templates** never add, remove or restrict slots: every console (Trap Floor, Toolbox, any game) always exposes all 72 slots. A template may only name slots by key to give them starting content or to refer to them in rule hooks.
- **Cube piece:** `TabletopObjectKind.Token` with a new definition ID and its own prefab (size 0.126 as data). It uses the Token physics profile initially. The existing token (0.72) and Trap Floor coin (about 0.245) do not fit a 0.207 cell.
- **Big squares (capacity 0):** contents are laid out in layers inside the 0.412 × 0.406 footprint.
- **Capacity:** the layout asset's default capacity (today Main 1, other card slots 0, track cells 1, grid squares 0) is the current default only. Capacity should become a component-declared configurable parameter; games never override it outside that declaration (doc 21).

## 6. ID scheme

*Applies from Stage (d). Until then Trap Floor keeps its category-41 IDs, Toolbox card slots use GUIDs, and no category-43 IDs are allocated (§4.4).*

- **Toolbox consoles:** all 72 slot IDs come from the existing identity source (random v4 GUIDs) through `TryAllocateContainerIds`, which already checks for existing IDs. No range is needed.
- **Trap Floor, existing slots:** keep their current category-41 IDs (`idBase = seatIndex × 20`; Hand +1, Main +2, Side +3…, Ability area +18, Controller Deck +19). Existing sessions, undo history and initial snapshots map 1:1.
- **Trap Floor, every other layout slot** (cube cells and any card slot beyond the legacy set): new category **43 (0x2B)** through the existing `CreateGuid`, with `index = (playerNumber << 16) | idOrdinal`.
  - Example: seat 1, ordinal 10 → `5446002b-4f4f-4000-8000-002b0001000a`.
  - This supports up to 65535 slots per seat, so the 15-side-slot cap disappears.

**Why it cannot collide:**
1. Category 43 is unused everywhere scanned:
   - Code categories in use: 1, 2, 20, 30, 40, 41, 42, 45, 46, 47, 60.
   - Authored `stableId` prefixes in all 50 Trap Floor definition assets, the scene and the code: `54460001`, `54460014`, `54460015`, `54460016` (categories 1, 20, 21, 22).
2. The category is encoded twice (in the first 32 bits and in bytes 9–10), so a category-43 GUID cannot equal a GUID of any other category.
3. Within category 43, (seat, ordinal) is unique by construction. The layout asset validates that ordinals are unique, at least 1, and at most 65535.
4. A random v4 GUID matching one of these has probability about 2⁻¹²², and Toolbox allocation still checks for existing IDs.
5. **Snapshots:** nothing is saved to disk today (no file or PlayerPrefs persistence in Runtime or Presentation). The only snapshots are in-memory (initial template snapshot, active-session undo history). They contain only existing IDs, and missing accepted-kinds or slot-key values take the defaults above.

**Rule:** ordinals are append-only. A removed slot retires its ordinal.

## 7. Placement rule (Stages b and d)

**Today:**
- Cards resolve by cursor ray (`CardDropTargetResolver`); ties fall to ray distance, then hash code.
- The preview hard-snaps the held body each frame (`SnapHeldPreview`).
- Settle is a 0.12 s smoothstep transition.
- `ContainedCardDragCoordinator.Release` (line 316) omits the source exclusion that the preview uses.
- Token targets are never created (`CreateTokenContainerInstance` has no callers).

**Proposed `ConsoleSlotTargetResolver.Resolve(pieceCentre, objectKind, excludedContainer, out target)`:**
- Projects the held piece's centre straight down onto console-local x/z and tests it against the footprints of all 72 slots of every console. This is pure data with no allocations.
- Filters by accepted kind and capacity.
- The nearest anchor wins; an exact tie goes to the lower ordinal.
- The same call drives the preview (highlight only, free drag) and the release (commit, then the existing transition).
- With no slot hit, the current resolvers handle decks, stacks, the hand and the table.

## 8. Mat as a physical surface (Stage c)

- **Collider:** the variant gets a solid box collider (not a trigger, no Rigidbody) whose top sits at the mat top and which runs down to the console base. It carries a `PhysicalTabletopSurface` with `templateLayoutOrigin = 0`.
- **Slot colliders** in the variant are triggers or absent.
- **Support raycasts, `TryPointer`, `TryAtLayout`:** they hit the mat, so held pieces ride at mat height and Toolbox placement lands on the mat.
- **Required fix:** console moves and placement must ignore the moving console's own surfaces in `TryRay`. Otherwise a console moved onto its current spot climbs onto its own mat.
- **Out-of-bounds recovery:** unchanged (the lowest surface is still the table).
- **Camera:** unaffected (pan is unclamped, zoom and pitch limits are serialized).
- **3c-0 helper:** `ResolveEmptyTableSurfaceHeight` only reads the layout-origin surface, so it is unaffected.
- **Dice and cubes:** the mat gets the Table material automatically. The cocked-die check uses world up and still works. Dice (ContinuousDynamic) and cubes (ContinuousSpeculative) are both safe against a solid box. Pieces fall the mat's thickness at the edge.

## 9. Stages

| Stage | Contents |
|---|---|
| (a) Definition + variant — **done 2026-10-06** | Layout asset (72 entries), `ConsoleMat.prefab` variant, every console exposes all 9 card slots with layout capacities, Main landscape footprint, mat rotation fix, scene repoint (§4.4). Accepted kinds, slot keys, category-43 IDs and cube containers moved to (d). |
| (c) Mat support surface | Collider + `PhysicalTabletopSurface` on the variant, own-surface exclusion for console moves and placement. |
| (b) Card slot targeting/settle | Shared resolver, free drag with highlight, nearest-anchor rule, `ContainedCardDragCoordinator.Release` exclusion fix. |
| (d) Cube cells | Core accepted kinds (accept-all default), slot keys in console state, category-43 IDs, cube cell containers, cube definition + prefab (0.126), cube cell view with layered layout for capacity 0, token preview and settle through the live move path (`TabletopMoveInteractionCoordinator`), `TransferTokenCommand` (Transaction) into the slot container. The dormant token-container code is not used. |

### 9.1 Stage order (2026-10-07)

Done: (a) definition + variant (2026-10-06); (a2) hand tray (2026-10-07, §11).

1. (a2b) Deck, stack and staging placement: `ConsoleAdjacentPlacement` helper and table bounds as data, plate-less controller deck and action stack, staging and hand-zone defaults; replaces the three stopgap constants.
2. (a2c) Hand optional in Core, before the first template without a hand.
3. (c) Mat support surface.
4. (b) Card slot targeting and settle.
5. Game setup pipeline: the second-template pipeline that removes Trap Floor-only wiring (doc 20 §12).
6. Interaction queue: camera framing (stale camera pivot and framing; `tabletopHeight` 14.3 stays untouched), Flip, stacking and decks, Quick Inspect, Ping.
7. (d) Cube cells.
8. Architecture audit.

## 10. Open items

- Trap Floor seat container stopgaps (Controller Deck +4.45, Ability stack −4.45, staging −7.6) until (a2b) (§4.3). The hand radius is gone (a2).
- Seat 0 worst-case overhang of 0.037 past the near table edge.
- Mapping of the current Trap Floor Main/Side slots to layout keys, to be fixed in the Stage (a) plan from the current prefab's anchor order.
- **Multiplayer (open, for the multiplayer stage):** `TransferCardUseCase` has no owner rule for Hand destinations, so any player can transfer a card into another player's hand. The H2 hand comfort cap is a local setting that only guards the owner's own draws and drops, so it does not close this. Do not fix before the multiplayer stage.
- **Card-slot capacity (resolved for now, 2026-10-06):** Main 1; plates and rails 0 (unbounded, stackable). These are the current defaults only; capacity is to become a component-declared configurable parameter (§5).
- **Sharing permissions (open, for the multiplayer stage):** a console, hand or deck is private to its owner by default. The owner can grant a teammate or friend access and revoke it. One rule covers console slot placement, hand transfers and deck access. The grants live in state (authoritative, undoable, synced), not in a local setting. Today nothing enforces this: see the 3c-0 report (2026-10-05) for current behaviour on another seat's console.
- **Per-player save and load (open):** players can save and load sessions, built on top of the existing in-memory snapshots (`GameTemplateInitialSnapshot`, active-session undo history). These are explicitly not a persistence format today, so this needs a versioned save format and a load path that respects the sharing permissions above.
- **Hand zone on the table (dropped from a2):** the hand container keeps a zone placement and Application can move it (owner only), but there is no zone view or Move Hand Zone UI. Add them back only if multiplayer needs a visible, movable zone (§11).

## 11. Stage (a2) record: hand (2026-10-07)

- **Tray:** the local hand is a camera-anchored tray at the bottom centre of the screen (`HandTrayRig`, `HandView` tray mode). It keeps a constant on-screen size and faces the owner at any pitch or yaw. Hover, select, reorder, and drag out to the table and back all ease smoothly. H or the HUD button collapses and expands it. Tray cards cast no shadows and their colliders are triggers while in the tray. The tray band is the hand's drop target, enabled only during a card drag.
- **Zone data:** the hand container has a placement with pose and extent (`ContainerPlacementState`); the initial snapshot captures it, so Undo and reset restore it. Trap Floor default: console-local (+6.40, 0.00), 2.0 × 1.4, temporary until (a2b).
- **Other seats:** each other seat's hand is a face-down pile with a card count at its zone pose (`HiddenHandView`). Faces are hidden and Inspect is not offered; the cards cannot be dragged out.
- **Dropped:** the table zone view (trigger plus outline) and Move Hand Zone were built and then removed. The tray band replaces the zone as the drop target; the zone pose now only places other seats' piles. Application still supports moving a hand zone (owner only); a UI for it is tracked in §10.
- **Owner-only moves:** `MoveContainerUseCase` rejects moving a seat-owned Deck, Stack or Hand zone by anyone except that seat's occupant (`NotContainerOwner`). Unowned Toolbox decks and stacks stay movable by anyone. `BeginContainerMove` runs the same check (`MoveContainerUseCase.IsMoveBlockedByOwner`) before any preview, hiding or placement starts.
- **Removed:** `PlayerHandRadius` (4.15) and `GetHandPose`. The table hand plate is hidden at runtime and no longer part of camera framing.
- **Optional hand:** Core still requires a hand per seat. Making it optional per game, with no fixed size, is Stage (a2c).
