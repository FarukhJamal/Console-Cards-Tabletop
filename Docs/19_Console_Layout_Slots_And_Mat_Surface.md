# Console Cards - Console Layout Slots, Cube Cells, and Mat Surface

**Document ID:** 19_Console_Layout_Slots_And_Mat_Surface
**Version:** 0.2
**Status:** Stages (a), (a2) and (a2b) implemented (2026-10-06/07/08; records in §4.4, §11 and §12); (a2b) 3a pile bays written 2026-10-08 (§12.1); C1 component catalogs and pile prefabs written 2026-10-08 (§12.2, doc 22); C2a, C2b and C3a written 2026-10-08/09 (doc 22); 3b pending. Later stages each need their own approved plan; the current order is §9.1. Binding platform rules: `21_Platform_Principles.md`.
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
- **Trap Floor seat containers versus the larger console (status 2026-10-08).** Placed by `ConsoleAdjacentPlacement` from the real console shape (§12):
  - Hand: the radius-4.15 hand pose is removed (a2); the hand is a camera tray, and its zone default is console-local (+5.575, 0.00), 1.0 × 1.4.
  - Controller Deck at console-local (+4.375, 0), Action stack at (−4.375, 0), starting-ability staging (Easy) at (−5.575, −0.9) and (−6.775, −0.9). Minimum clearance 0.200, table margin ≥ 0.844 (Easy) / 1.744 (Hard, Impossible).

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

Done: (a) definition + variant (2026-10-06); (a2) hand tray (2026-10-07, §11); (a2b) deck, stack and staging placement (2026-10-08, §12).

1. ~~(a2b) Deck, stack and staging placement~~ — done (§12). Follow-up (§12.1, §12.2): 3a plate-less piles with bays (done); catalog stages C1 and C1b (done), C2a (written: authored Toolbox prefab from the library, Real UI prefab catalog, several cards become one Deck), C2b (written: Discard Pile in Runtime and the Toolbox, face-down arrival, Undo rebuilds Toolbox pieces), C3a (written: every spawn from the catalogs, old prefab fields removed), C3b (scene-owned pieces become catalog spawns) and C3c (game board and mapping board prefabs, Controller and Trap Floor boxes filled), doc 22; then 3b new-player hints (Hints switch, legal targets light during a drag). Stage 4 (split stacks) is absorbed by C1.
2. (a2c) Hand optional (declared per template, in its rules), before the first template without a hand. Includes: removing Trap Floor's Action area; one routing rule for granted cards (starting cards, purchases): into the owner's hand when the template uses one, otherwise loose on the table at the next free staging spot (placement-checked); the purchase service accepts the seat's own Hand as destination.
3. (c) Mat support surface.
4. (b) Card slot targeting and settle.
5. Game setup pipeline: the second-template pipeline that removes Trap Floor-only wiring (doc 20 §12).
6. Interaction queue: camera framing (stale camera pivot and framing; `tabletopHeight` 14.3 stays untouched), Flip, stacking and decks, Quick Inspect, Ping.
7. (d) Cube cells.
8. Architecture audit.

## 10. Open items

- ~~Trap Floor seat container stopgaps~~ — resolved by (a2b) (§12).
- **Split stacks (resolved by C1, §12.2):** a split stack is the catalog's Stack pile in its bay, like every other Stack. The rest of the stacking/decks work stays in the interaction queue (§9.1).
- Seat 0 worst-case overhang of 0.037 past the near table edge.
- Mapping of the current Trap Floor Main/Side slots to layout keys, to be fixed in the Stage (a) plan from the current prefab's anchor order.
- **Multiplayer (open, for the multiplayer stage):** `TransferCardUseCase` has no owner rule for Hand destinations, so any player can transfer a card into another player's hand. The H2 hand comfort cap is a local setting that only guards the owner's own draws and drops, so it does not close this. Do not fix before the multiplayer stage.
- **Card-slot capacity (resolved for now, 2026-10-06):** Main 1; plates and rails 0 (unbounded, stackable). These are the current defaults only; capacity is to become a component-declared configurable parameter (§5).
- **Sharing permissions (open, for the multiplayer stage):** a console, hand or deck is private to its owner by default. The owner can grant a teammate or friend access and revoke it. One rule covers console slot placement, hand transfers and deck access. The grants live in state (authoritative, undoable, synced), not in a local setting. Today nothing enforces this: see the 3c-0 report (2026-10-05) for current behaviour on another seat's console.
- **Per-player save and load (open):** players can save and load sessions, built on top of the existing in-memory snapshots (`GameTemplateInitialSnapshot`, active-session undo history). These are explicitly not a persistence format today, so this needs a versioned save format and a load path that respects the sharing permissions above.
- **Hand zone on the table (dropped from a2):** the hand container keeps a zone placement and Application can move it (owner only), but there is no zone view or Move Hand Zone UI. Add them back only if multiplayer needs a visible, movable zone (§11).

## 11. Stage (a2) record: hand (2026-10-07)

- **Tray:** the local hand is a camera-anchored tray at the bottom centre of the screen (`HandTrayRig`, `HandView` tray mode). It keeps a constant on-screen size and faces the owner at any pitch or yaw. Hover, select, reorder, and drag out to the table and back all ease smoothly. H or the HUD button collapses and expands it. Tray cards cast no shadows and their colliders are triggers while in the tray. The tray band is the hand's drop target, enabled only during a card drag.
- **Zone data:** the hand container has a placement with pose and extent (`ContainerPlacementState`); the initial snapshot captures it, so Undo and reset restore it. Trap Floor default: console-local (+5.575, 0.00), 1.0 × 1.4 (one card pile; set in (a2b), §12).
- **Other seats:** each other seat's hand is a face-down pile with a card count at its zone pose (`HiddenHandView`). Faces are hidden and Inspect is not offered; the cards cannot be dragged out.
- **Dropped:** the table zone view (trigger plus outline) and Move Hand Zone were built and then removed. The tray band replaces the zone as the drop target; the zone pose now only places other seats' piles. Application still supports moving a hand zone (owner only); a UI for it is tracked in §10.
- **Owner-only moves:** `MoveContainerUseCase` rejects moving a seat-owned Deck, Stack or Hand zone by anyone except that seat's occupant (`NotContainerOwner`). Unowned Toolbox decks and stacks stay movable by anyone. `BeginContainerMove` runs the same check (`MoveContainerUseCase.IsMoveBlockedByOwner`) before any preview, hiding or placement starts.
- **Removed:** `PlayerHandRadius` (4.15) and `GetHandPose`. The table hand plate is hidden at runtime and no longer part of camera framing.
- **Optional hand:** Core still requires a hand per seat. Making it optional per game, with no fixed size, is Stage (a2c).

## 12. Stage (a2b) record: deck, stack and staging placement (2026-10-08)

- **Helper:** `ConsoleAdjacentPlacement` (GameTemplates, pure data) places pieces beside a console from its real shape: the mat plus every Card slot footprint of the layout. A piece goes outward from the console, or from an inner piece on the same side, by the gap, within the z band it occupies. `ConsoleAdjacentPlacementSettings.Standard`: gap 0.2, pile 1.0 × 1.4, minimum clearance 0.15, minimum table margin 0.1. These are helper parameters, not game constants.
- **Trap Floor placement choices** (console-local along-z): right side Controller Deck then the Hand zone (both z 0); left side Action stack (z 0) then the Easy staging row (z −0.9). Resulting centres: deck (+4.375, 0), hand zone (+5.575, 0), stack (−4.375, 0), staging (−5.575, −0.9) and (−6.775, −0.9). This also fixes the earlier staging cards overlapping each other along z.
- **Table bounds as data:** `PlayerLayoutDefinition.TableBounds` (optional). Standard 4-player: x −12.578…12.178, y −8.544…8.644 (TCGTable2). Compact and 8-player leave it unset, which skips the margin check.
- **Build check:** the factory checks every seat's console shape and pieces, the board and the table, and throws when clearance < 0.15 or margin < 0.1. Result: minimum clearance 0.200 (console to deck/stack), table margin 0.844 (Easy) and 1.744 (Hard, Impossible). The shipped Trap Floor path always passes the console layout, so the check always runs there; factory overloads without a layout (tests) use a worst-case box and skip the check. The Floorfall dice are physical and are not part of the check (their start is 0.178 from seat 3's console).
- **Pile style (declared parameter):** `GameTemplateContainerDefinition.PileStyle` (`GameTemplatePileStyle`: plate-less on/off, maximum pile height, default 0.25), allowed only on Deck and Stack (`ContainerPileStyleInvalid` otherwise). The Trap Floor factory sets it for each seat's Controller Deck and Action stack; the composition reads it when it builds the pile views. Presentation data only: no Core change, nothing in Match State or snapshots.
- **Plate-less piles:** the pile is the visual. The root rests at the placement surface; each card rests on the table at surface + `PivotToBottom` + `RestClearance` + i × step, with step = min(0.02, maximum pile height / card count), so the 48-card controller deck is about 0.25 high. Piles are neat (no per-card drift). The plate never shows at rest; labels are kept. The root drop box becomes a trigger, so the pile no longer blocks loose pieces while drops and right-clicks still resolve through it. Slot stacks and Toolbox decks/stacks keep their plates and the 0.02 step.
- **Empty-pile affordance:** while a card is dragged, an empty plate-less pile within 1.5 of the pointer shows its plate faintly (base material); over it, the existing valid/invalid feedback tints it. Visual only: the trigger drop target is always enabled.
- **Owner-only moves** of the seat deck and stack are unchanged (Stage (a2) rule).
- **Split stacks** are unchanged for now (§10).

### 12.1 Plate-less everywhere, with bays (2026-10-08)

- **Decision (owner):** one rule for every pile; plate-less everywhere, with visual hints for new players. Recorded as doc 21 principles 16 and 17. This replaces the plate-less on/off switch and the 1.5 empty-pile hint above.
- **Term:** a *bay* is a marked pile spot on the table, part of the console visual family (doc 00 §3.1). "Cartridge" stays the docs' word for a game, so bays have a cartridge-slot look but are not called cartridges.
- **Bay (`PileBayView`):** a thin dim frame flat on the table around the card footprint (1.0 × 1.4 plus a 0.08 margin), with a slot mouth on the edge facing the owner seat's Console (the near edge when the pile has no owner) and an optional mark. Built from collider-less bars with no shadows; a child of the pile root, so moving a pile and Undo carry it.
- **Marks (system set):** `GameTemplateBayMark` None, Draw, Discard. Games choose; they add no symbols or text. There is no play mark: playing a card means putting it into a Console slot, and the slots are the play bays.
- **Pile style:** `GameTemplatePileStyle(bayMark, maximumPileHeight)`; the plate-less flag is removed. Validation: only a Deck or Stack declares one, with a mark from the set (`ContainerPileStyleInvalid`). Trap Floor: Controller Deck marked Draw.
- **Which piles (3a):** fixed piles declared by the template (Trap Floor Controller Decks; the Action area until (a2c) removes it) always show their bay. Toolbox decks and stacks are free piles: plate-less, bay only while empty. The Toolbox spawn and move ghosts show a bay outline instead of the plate. Console slot stacks are unchanged (the slot is the bay). Split stacks: Stage 4. The scene deck, stacks and discard pile are never shown in either session, so they are untouched.
- **Feedback:** valid, source and invalid drop feedback tint the bay frame (thicker; invalid is a darker red), not a plate.
- **Kept for the scene value:** `PrototypeVisualHide.ContainerPlates` still exists so the saved scene loads; a pile with a bay ignores it.

### 12.2 C1: one pile behaviour, catalog pile prefabs, bays never overlap (2026-10-08)

- **One behaviour per pile kind (owner):** a Deck, Stack or Discard Pile behaves the same whether a template or the Toolbox places it (and a Split stack is a Stack). Every pile always shows its bay; the fixed/free split of §12.1 is removed. The mark comes from the kind (`GameTemplatePileStyle.DefaultFor`: Deck → Draw, Discard Pile → Discard, Stack → none) unless a template declares a style. Validation now also allows a pile style on a Discard Pile.
- **Pile prefabs:** `Content/Prefabs/Real/Deck.prefab`, `Stack.prefab` and `DiscardPile.prefab`, each with a `Model` placeholder (for the future model, e.g. the controller box whose lid is the card organizer) and a `Bay` child (`PileBayView`) reading the shared `PileBayStyle.asset` (material, colours, bar sizes). The composition spawns every runtime Deck and Stack, and the spawn and move ghosts, from the Base Box catalog (doc 22). The Prototype pile prefabs stay only for the hidden scene piles.
- **Bays never overlap (owner):** placement uses the bay footprint for piles in a bay (`ConsoleAdjacentPlacementSettings.BayMargin` 0.08 → `BayWidth` × `BayDepth` 1.16 × 1.56; `PlaceBeside(..., inBay: true)`), and the card footprint for loose cards and hand zones. The bay is drawn inside that footprint. Trap Floor: deck (+4.455, 0), hand zone (+5.735, 0), Action area (−4.455, 0), Easy staging along-z moved from −0.9 to −1.1 so it clears the next seat's hand zone: (−5.735, −1.1) and (−6.935, −1.1). Build check: minimum clearance 0.200; table margin 0.644 (Easy), 1.664 (Hard, Impossible). Player drops stay free.
- **Open:** the Toolbox quick-spawn grid (0.62 × 0.72 steps, used when a component is added without the ghost) is smaller than a card for every kind; it moves to catalog footprints in C2.
