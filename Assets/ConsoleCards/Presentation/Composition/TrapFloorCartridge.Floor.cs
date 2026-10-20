using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Views;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The collapsing tile (owner, 2026-10-21, as in the playtest): after the floor dice pick it, it is a hole at once,
    /// and any player lifts the tile off the board and sets it aside. A tile still lying in its cell when the floor
    /// turn ends is set beside the board by the game (in a column to the right of the board). Event-time only.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        // Sets every collapsed tile still lying in its cell beside the board. Returns how many were moved.
        private int SetCollapsedTilesAside()
        {
            if (host.trapFloorCollapseState == null || host.trapFloorTemplate == null || host.matchState == null) return 0;
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;
            IReadOnlyList<TrapFloorCollapsedFloorState> collapsed = host.trapFloorCollapseState.CollapsedFloors;
            double halfWidth = template.Grid.CellWidth * 0.5d;
            double halfHeight = template.Grid.CellHeight * 0.5d;
            int rows = System.Math.Max(1, template.GridRows);
            int aside = CountTilesAlreadyAside(collapsed, halfWidth, halfHeight);
            int moved = 0;
            for (int i = 0; i < collapsed.Count; i++)
            {
                TrapFloorCollapsedFloorState floor = collapsed[i];
                if (floor.IsTakenPrize
                    || !host.Registry.TryGetCardView(floor.FloorCardId, out CardView view)
                    || !view.IsBound
                    || view.BoundState == null
                    || !view.BoundState.ContainerId.IsEmpty // a lifted tile someone stacked or put in a hand
                    || !template.TryGetCellPosition(host.matchState, floor.Coordinate, out TableCoordinate cell)
                    || !IsInCell(view.BoundState.Pose.Position, cell, halfWidth, halfHeight))
                {
                    continue;
                }

                // Beside the board: a column to the right, one tile per row, then a second layer.
                int row = 1 + (aside % rows);
                if (!template.TryGetCellPosition(
                        host.matchState,
                        new TrapFloorCoordinate(template.GridColumns, row),
                        out TableCoordinate edge))
                {
                    continue;
                }

                TabletopObjectState state = view.BoundState;
                TabletopPose pose = new TabletopPose(
                    new TableCoordinate(edge.X + (1.6d * template.Grid.ColumnPitch), edge.Y),
                    state.Pose.RotationDegrees,
                    state.Pose.Layer + (aside / rows),
                    state.Pose.LocalOrder);
                if (host.MoveLoosePiece(view, state, pose))
                {
                    moved++;
                    aside++;
                }
            }

            return moved;
        }

        // Collapsed tiles players (or the game) already took off: the next one goes after them.
        private int CountTilesAlreadyAside(IReadOnlyList<TrapFloorCollapsedFloorState> collapsed, double halfWidth, double halfHeight)
        {
            int count = 0;
            for (int i = 0; i < collapsed.Count; i++)
            {
                TrapFloorCollapsedFloorState floor = collapsed[i];
                if (!floor.IsTakenPrize
                    && host.matchState.Cards.TryGetValue(floor.FloorCardId, out CardInstanceState card)
                    && host.trapFloorTemplate.TryGetCellPosition(host.matchState, floor.Coordinate, out TableCoordinate cell)
                    && !IsInCell(card.BaseState.Pose.Position, cell, halfWidth, halfHeight))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsInCell(TableCoordinate position, TableCoordinate cell, double halfWidth, double halfHeight) =>
            System.Math.Abs(position.X - cell.X) <= halfWidth && System.Math.Abs(position.Y - cell.Y) <= halfHeight;
    }
}
