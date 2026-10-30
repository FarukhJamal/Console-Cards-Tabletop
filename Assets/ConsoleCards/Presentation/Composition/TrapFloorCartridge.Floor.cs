using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The collapsing tile (owner, 2026-10-21, as in the playtest): after the floor dice pick it, it is a hole at once,
    /// and any player lifts the tile off the board and sets it aside. A tile still lying in its cell when the floor
    /// turn ends is set aside by the game, off the playmat (BOARD-2: in the table's top-right corner). Event-time only.
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

                // BOARD-2 (owner, 2026-10-30: "don't put them on the game board again"): off the playmat, in the
                // table's top-right corner, a row of three, then the next layer on top.
                TabletopObjectState state = view.BoundState;
                TabletopPose pose = new TabletopPose(
                    new TableCoordinate(
                        CollapsedAsideX + ((aside % CollapsedAsidePerLayer) * CollapsedAsideStep),
                        CollapsedAsideY),
                    state.Pose.RotationDegrees,
                    state.Pose.Layer + (aside / CollapsedAsidePerLayer),
                    state.Pose.LocalOrder);
                if (host.MoveLoosePiece(view, state, pose))
                {
                    moved++;
                    aside++;
                }
            }

            return moved;
        }

        private const double CollapsedAsideX = 8.6d;
        private const double CollapsedAsideY = 9d;
        private const double CollapsedAsideStep = 1.2d;
        private const int CollapsedAsidePerLayer = 3;

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

        // ---------- FLOORS-1: floors that shrink (owner, 2026-10-21) ----------
        // A win (an exit) or a loss (everyone out) ends the floor. The next floor is a fresh deal one row and one
        // column smaller (the table is set up again, so hands, Consoles, Abilities, Keys, holes and knock-outs start
        // over; Avatars stay). Winning the last floor (3×3) wins the game; losing it loses the game.

        private int NextFloorSize(out int columns)
        {
            columns = host.trapFloorTemplate.GridColumns - 1;
            return host.trapFloorTemplate.GridRows - 1;
        }

        // POP-2: seconds before the floor-end chip deals the next floor by itself (dealing cannot be undone).
        private const float FloorEndedAutoDealSeconds = 5f;

        // POP-2: the floor is still over (lost, or won through an exit); false after an Undo took that back.
        private bool IsFloorEnded() =>
            host.trapFloorTurnState != null
            && (host.trapFloorTurnState.IsCurrentFloorFailed
                || (host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsWon));

        internal string FloorEndedActionLabel()
        {
            if (host.trapFloorTemplate == null || host.trapFloorTemplate.IsLastFloor) return "Play Again";
            int rows = NextFloorSize(out int columns);
            return $"Start next phase ({columns}×{rows})"; // FLOOR-2
        }

        internal string FloorEndedChoiceText() => FloorEndedChoiceText(false);

        // MODE-1: a Survival floor where someone escaped is won even when the rest are out.
        internal string FloorEndedChoiceText(bool won)
        {
            return host.trapFloorTemplate == null || host.trapFloorTemplate.IsLastFloor
                ? won
                    ? "That was the last floor: the game is won. Choose Play Again to start over."
                    : "That was the last floor: the game is lost. Choose Play Again to start over."
                : $"Choose {FloorEndedActionLabel()} in the turn menu.";
        }

        // The floor's end: says what happened and offers the next floor (or a new game after the last one).
        internal void ShowFloorEnded(bool won, string detail = null)
        {
            if (host.runtimeUi == null || host.trapFloorTemplate == null) return;
            TrapFloorTemplateDefinition floor = host.trapFloorTemplate;
            int number = floor.FloorIndex + 1;
            bool last = floor.IsLastFloor;
            // FLOOR-2 (owner, 2026-10-22: "popup with Starting next phase and OK"): won or lost is the first line.
            string title = last
                ? won ? "YOU WIN THE GAME" : "GAME OVER"
                : "STARTING NEXT PHASE";
            string result = won ? $"Floor {number} won." : $"Floor {number} lost.";
            string next;
            if (last)
            {
                next = won
                    ? "That was the last floor: the team escaped the Trap Floor."
                    : "Everyone is out on the last floor: the team lost.";
            }
            else
            {
                int rows = NextFloorSize(out int columns);
                next = $"{result} Next: Floor {number + 1} of {floor.FloorCount}, {columns}×{rows}, dealt fresh. Knocked-out players "
                    + "come back; Hands, Consoles and Abilities are dealt again (Avatars stay) and the Controller decks "
                    + "are shuffled. This cannot be undone.";
            }

            string body = string.IsNullOrEmpty(detail) ? next : $"{detail}\n{next}";

            // POP-2 (owner, 2026-10-29: "Starting floor 2 is too big"): a chip at the top centre. A next floor deals
            // itself after a short countdown (Esc or hovering stops it; Not yet closes it, the turn menu still offers
            // it); the last floor waits for Play Again.
            int nextColumns = 0;
            int nextRows = 0;
            if (!last) nextRows = NextFloorSize(out nextColumns);
            string chipTitle = last
                ? won ? "You win the game" : "Game over"
                : $"Floor {number + 1} of {floor.FloorCount}";
            string chipDetail = last
                ? (string.IsNullOrEmpty(detail) ? next : detail)
                : $"{nextColumns}×{nextRows}, dealt fresh"
                    + (string.IsNullOrEmpty(detail) ? string.Empty : $" · {detail}");
            if (host.runtimeUi.ShowTableMilestone(new TableMilestoneModel(
                    last ? (won ? "GAME WON" : "GAME LOST") : $"FLOOR {number} {(won ? "WON" : "LOST")}",
                    won ? TableNoticeTone.Won : TableNoticeTone.Danger,
                    chipTitle,
                    chipDetail,
                    last ? FloorEndedActionLabel() : "Start",
                    () =>
                    {
                        host.runtimeUi?.CloseTableMilestone();
                        if (!IsFloorEnded()) return; // Undo took the floor's end back while the chip waited
                        if (last) DealFloor(0);
                        else DealFloor(floor.FloorIndex + 1);
                    },
                    "Not yet",
                    () => host.runtimeUi?.CloseTableMilestone(),
                    last || host.IsOnlineTable ? 0f : FloorEndedAutoDealSeconds))) // online: no countdown; a player presses Start
            {
                return;
            }

            Action close = () => host.runtimeUi?.CloseTabletopPopup();
            host.runtimeUi.ShowContextMenu(
                new Vector2((Screen.width * 0.5f) - 200f, Screen.height * 0.66f),
                title,
                body,
                new[]
                {
                    new PopupActionOption(
                        last ? FloorEndedActionLabel() : "OK",
                        true,
                        () =>
                        {
                            close();
                            if (last) DealFloor(0);
                            else DealFloor(floor.FloorIndex + 1);
                        }),
                    new PopupActionOption("Not yet", true, close),
                },
                close,
                _ => close());
        }

        // Sets the table up again at the given floor (0 = a new game at full size), keeping the mode and seats.
        private void DealFloor(int floorIndex)
        {
            if (host.activeSession == null) return;
            PendingWinMode = null; // MODE-1: the next floor keeps this game's win mode and turn direction
            PendingAnticlockwise = null;
            host.trapFloorNextFloorIndex = floorIndex;
            bool dealt;
            try
            {
                dealt = host.SessionService.TryReplaceTable(host.activeSession.Selection);
            }
            finally
            {
                host.trapFloorNextFloorIndex = 0;
            }

            if (!dealt || host.trapFloorTemplate == null) return;
            host.ShowMessage(floorIndex == 0
                ? "A new game: Floor 1."
                : $"FLOOR {host.trapFloorTemplate.FloorIndex + 1} of {host.trapFloorTemplate.FloorCount} — "
                    + $"{host.trapFloorTemplate.GridColumns}×{host.trapFloorTemplate.GridRows}, dealt fresh.");
        }
    }
}
