# Console Cards — Play Area Architecture

**Document ID:** 07_Play_Area_Architecture  
**Version:** 1.0 Draft  
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

## 12. Effectively Unbounded Tabletop

The Tabletop is not expanded by stretching one mesh.

Recommended model:

- Logical sector-based coordinates.
- Camera-centered visible surface sections.
- Seamless repeatable material.
- Visual culling for distant objects.
- Stable Runtime State independent of visible surface.

This is not parallax.

## 13. Large-Coordinate Strategy

The first implementation uses logical two-dimensional coordinates and keeps them independent from rendered Unity positions.

Sectoring, chunk coordinates, or floating-origin rebasing are not foundation contracts yet. During the Virtual Table milestone, prototype large-area navigation and measure precision. Adopt one of those strategies only if the prototype demonstrates a real need.

Any later render-origin adjustment must not alter accepted logical Match State.

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

## 15. Placement Priority

When multiple guides overlap:

1. Explicit Slot under pointer.
2. Explicit Zone target.
3. Active Play Area strategy.
4. Nearby Stack/Deck target.
5. Freeform table placement.

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

## 19. Initial Implementation Order

1. Freeform table placement.
2. Generic Zone and Slot.
3. Rectangular Grid.
4. Side-scroller layout.
5. Camera focus bounds.
6. Tile-built or Track layout when required.

Do not implement every future Play Area before a template requires it.
