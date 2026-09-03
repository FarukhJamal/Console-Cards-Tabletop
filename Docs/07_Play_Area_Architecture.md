# Console Cards — Play Area Architecture

**Document ID:** 07_Play_Area_Architecture  
**Version:** 1.5

**Status:** Approved

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams in this document are illustrative unless explicitly labelled **Approved Contract**. Codex must not treat illustrative examples as fixed public APIs.

## 1. Purpose

A Play Area is an optional structured region on the freeform Virtual Tabletop.

It organizes play without making the Platform dependent on one Game layout.

## 2. Base Requirements

A Play Area may provide:

- Bounds.
- Visual background.
- Placement strategy.
- Snapping.
- Zones and Slots.
- Camera focus region.
- Layer guidance.
- Optional occupancy information.
- Policy references.

A Match may contain zero, one, or multiple Play Areas.

The default composition keeps the active shared Game Board or core Game action centered. Seat, Hand, Console, personal resource, and supporting UI layouts are arranged around that center and must not force the table to grow with Player count.

## 2.1 Player Layout Foundation

Player Layout is separate from Play Area layout. It arranges Seats and their personal areas around the central Game Board without defining the Game Board itself.

Required reusable Player Layouts are:

- Standard four-Player: one Seat per side.
- Eight-Player: two Seats per side.
- Compact four-Player: Seats pulled toward the central action.

The Platform supports one to eight Players. For smaller Player counts, occupied Seats reposition toward the center rather than remaining at distant unused edge positions. The fixed physical Table, its authored usable area, and the central Game Board do not scale up as more Players join.

Selection of a Player Layout is Game Template and Player-count configuration, not hardcoded Game logic. Exact mappings for one to three and five to seven Players remain open.

## 3. Base Contract

Conceptual interface:

```csharp
public interface IPlacementStrategy
{
    PlacementSuggestion Evaluate(
        TabletopObjectState objectState,
        TabletopPose requestedPose,
        PlacementContext context);
}
```

A `PlacementSuggestion` may contain:

- Suggested pose.
- Target Play Area.
- Target Zone or Slot.
- Confidence/priority.
- Warning information.
- Whether bypass is available.

## 4. Freeform Play Area

Provides:

- Visual region.
- Optional bounds.
- Optional gridless snapping points.
- Camera focus.

It does not require cell coordinates.

## 5. Rectangular Grid

Configuration:

- Rows.
- Columns.
- Cell width and height.
- Origin.
- Rotation.
- Visibility.
- Snap strength.
- Object footprint support.

Grid cells may contain multiple layered objects by default.

Occupancy information is descriptive unless Policy restricts it.

## 6. Hex Grid

Future-compatible layout with:

- Axial or cube coordinates.
- Hex orientation.
- Cell size.
- Neighbour lookup.
- Snapping.

Hex implementation is not required before a real template needs it.

## 7. Side-Scroller Play Area

Represents a continuous horizontal arrangement.

Configuration:

- Direction.
- Section size.
- Section spacing.
- Lane count.
- Starting index.
- Visible window preference.
- Archive behavior.
- Camera focus rules.

The Play Area does not determine whether a section was cleared. Players or future Game automation handle that rule.

## 8. Track Play Area

Represents ordered positions.

Supports:

- Linear track.
- Curved track.
- Circular track.
- Future branching track.

Track positions use stable indices or node IDs.

## 9. Zone-Based Play Area

Provides named regions such as:

- Player field.
- Enemy field.
- Draw area.
- Discard area.
- Equipment area.
- Objectives.

Zones may use internal layout strategies.

## 10. Tile-Built Play Area

Supports boards assembled during a Match.

Responsibilities:

- Edge or Grid snapping.
- Stable Tile poses.
- Expanding focus bounds.
- Layering.
- Optional connectivity metadata.

It does not automatically enforce Tile placement rules.

## 11. Board and Play Area Relationship

A Board is a Tabletop Object.

A Play Area is logical structure.

Possible relationships:

- Board supplies one Play Area.
- Board supplies several Play Areas.
- Play Area exists without a Board.
- Several Boards exist inside one Play Area.

Do not merge these concepts.

### 11.1 Universal Console and Game-Specific Game Board

The universal Console is not a Play Area strategy and is not part of the Game-specific Game Board.

The loaded Game Template supplies the central Game Board and associated Play Areas. Different Games may therefore use different central layouts, including a Card-built dungeon/room structure or a Side-Scroller Play Area, while reusing the same Console contract.

Reference screenshots and mockups demonstrate possible compositions only. They do not define one mandatory Board, Grid, Seat, or Camera layout for every Game.

## 12. Fixed Table and Physical Surfaces

The Virtual Tabletop uses one fixed physical Table. The Table and Game Boards provide real authored collision surfaces for loose Card/Pawn/Token/Die placement and simulation under ADR-025.

An authored usable Table/Board surface:

- Uses explicitly configured physical colliders editable with the Table/Board in Unity.
- Follows the owning Table/Board's position, rotation, and scale.
- Is independent from decorative Table mesh geometry and mesh bounds.
- Supplies valid raycast hits for new loose placement and catches released objects by collision.
- Does not move with the local Camera.

Game Boards sit on the Table and are physical surfaces, while Play Areas remain logical structure. A mat, Zone, Slot, or guide does not create a collision surface merely by defining bounds. Off-table physical releases may fall naturally and must not snap back. Deck/Stack/Console bodies retain their non-physical positioning and applicable authored Table-area validation under ADR-024.

## 13. Coordinate and Projection Strategy

Authored/template/container layout retains direct logical-to-render mapping: logical X maps to Unity world X, logical Y maps to Unity world Z, and `1` table unit maps to `1` Unity world unit. Existing `TableCoordinate` and `TabletopPose` values remain unchanged for those uses. Separate authoritative 3D physical pose/state represents loose physical objects in the shared world; full 3D is not forced into `TabletopPose`.

New loose placement raycasts valid physical Table/Board surfaces instead of the mathematical placement plane. A hit supplies the preview and initial placement slightly above the surface; no hit means no creation commit. This applies to each loose object in Card batches and duplicate placement. Holding is controlled/kinematic, releasing is dynamic, and settling commits final 3D pose through the authoritative Application path. Loss of a surface hit must not cancel release or roll back an off-table object.

Moving/scaling an authored Table/Board moves/scales its colliders, not the logical coordinate system. Camera movement changes neither. Non-physical Container positioning retains its existing coordinate/projection model; this decision does not convert Container bodies or logical Play Area strategies to physics.

## 14. Layering

Canonical layer intent:

1. Table Surface.
2. Board and large Play Area visuals.
3. Tiles and section cards.
4. Cards.
5. Pawns and Miniatures.
6. Tokens and markers.
7. Dragged object preview.
8. Interaction/UI overlays.

Logical `Layer` and `LocalOrder` must not depend solely on transform Y position.

This layering is authored/layout and overlay intent, not a constraint on simulated loose-object height or orientation. Collision determines physical overlap and resting height; Container layouts control contained Cards with loose physics disabled.

## 15. Placement Priority

When multiple guides overlap:

1. Explicit Slot under pointer.
2. Explicit Zone target.
3. Active Play Area strategy.
4. Nearby Stack/Deck target.
5. Freeform loose placement on a valid physical Table/Board surface; non-physical Container placement retains the authored Table-area rule.

The exact priority must be configurable and tested.

## 16. Policy Interaction

The Play Area calculates suggestions.

Policy determines whether:

- Suggestion is ignored.
- Warning is shown.
- Snap is applied.
- Out-of-bounds placement is blocked.
- Bypass is allowed.

This separation prevents layout code from becoming a Rule engine.

A valid physical surface hit is a requirement for new loose placement, not a Play Area suggestion or Game Rule. A logical guide or bypass cannot fabricate a surface. Off-table physical release and falling remain valid under ADR-025; do not apply a two-dimensional Table-boundary check to reject accepted physical settlement. Non-physical Container positioning retains ADR-024's boundary validation. Optional Game-rule restrictions remain separate from these generic mechanics.

## 17. Camera Bookmarks

A Play Area may define:

- Default focus.
- Active-region focus.
- Full-area overview.
- Named bookmarks.

Bookmarks are template data. Each Player’s current Camera remains local.

## 18. Serialization

Play Areas persist:

- Definition ID.
- Runtime pose.
- Layout configuration.
- Dynamic sections or Tiles.
- Zones and Slots.
- Policy references.
- Version.

Visual caches are not persisted.

## 19. Approved Implementation Order

1. M4 Player Layout model with structural one-to-eight-Player support and only the confirmed standard four-Player, compact four-Player, and eight-Player authored arrangements.
2. M4 central Play Area stable identity, bounds, and focus foundation, separate from the universal Console.
3. M4.1 minimum Game Template support and selection among supported authored Player Layout definitions.
4. Trap Floor playable with its approved fixed `6 x 6` Floor Card Game Board requirements from `18_Trap_Floor_Game_Requirements.md`.
5. Super Leroy Sisters playable, including its required Side-Scroller Play Area.
6. Remaining shared Phase 1 Play Area capabilities, including Freeform placement, generic Zone/Slot, Rectangular Grid, placement suggestions, snap bypass, and independent personal Play Area visibility configuration.
7. Tile-built or Track layout only when an approved Game Template requires it.

Mappings for one to three and five to seven Players remain unresolved under OD-014 and must not be invented. OD-015 visibility and OD-016 marquee/group-landing requirements remain required before Phase 1 closure but do not block the M4 foundation unless an approved Game requires a defined subset earlier.

Do not implement every future Play Area before a template requires it.
