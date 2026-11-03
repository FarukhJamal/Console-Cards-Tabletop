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
                Vector3 fromPosition = view.transform.position;
                Quaternion fromRotation = view.transform.rotation;
                if (host.MoveLoosePiece(view, state, pose))
                {
                    moved++;
                    aside++;
                    // SMOOTH-1 (owner, 2026-10-31): the tile slides off to its pile instead of jumping there.
                    view.PhysicalObject?.GlideFrom(fromPosition, fromRotation, CollapsedGlideSeconds, 0f);
                }
            }

            return moved;
        }

        private const double CollapsedAsideX = 8.6d;
        private const double CollapsedAsideY = 9d;
        private const double CollapsedAsideStep = 1.2d;
        private const int CollapsedAsidePerLayer = 3;
        private const float CollapsedGlideSeconds = 0.55f;

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

        internal string FloorEndedActionLabel() => FloorEndedActionLabel(false);

        internal string FloorEndedActionLabel(bool won)
        {
            if (StartsNextStage(won)) return $"Start Stage {host.trapFloorStageIndex + 2}"; // STAGES-1
            if (host.trapFloorTemplate == null || host.trapFloorTemplate.IsLastFloor) return "Play Again";
            int rows = NextFloorSize(out int columns);
            return $"Start next phase ({columns}×{rows})"; // FLOOR-2
        }

        // STAGES-1: winning a stage's last floor (3×3) starts the next stage at full size, while there is one.
        private bool StartsNextStage(bool won) =>
            won
            && host.trapFloorTemplate != null
            && host.trapFloorTemplate.IsLastFloor
            && host.trapFloorStageIndex < host.TrapFloorStageCount - 1;

        private string StageName(int stageIndex)
        {
            TrapFloorStage stage = host.trapFloorFloorPlan?.StageAt(stageIndex);
            return stage != null ? stage.Name : $"Stage {stageIndex + 1}";
        }

        internal string FloorEndedChoiceText() => FloorEndedChoiceText(false);

        // MODE-1: a Survival floor where someone escaped is won even when the rest are out.
        internal string FloorEndedChoiceText(bool won)
        {
            if (StartsNextStage(won)) return $"Choose {FloorEndedActionLabel(true)} in the turn menu.";
            return host.trapFloorTemplate == null || host.trapFloorTemplate.IsLastFloor
                ? won
                    ? "That was the last floor: the game is won. Choose Play Again to start over."
                    : "That was the last floor: the game is lost. Choose Play Again to start over."
                : $"Choose {FloorEndedActionLabel()} in the turn menu.";
        }

        private TrapFloorTemplateDefinition winRecordedFor;

        // The floor's end: says what happened and offers the next floor (or a new game after the last one).
        internal void ShowFloorEnded(bool won, string detail = null)
        {
            if (host.runtimeUi == null || host.trapFloorTemplate == null) return;
            TrapFloorTemplateDefinition floor = host.trapFloorTemplate;
            int number = floor.FloorIndex + 1;
            bool nextStage = StartsNextStage(won); // STAGES-1
            bool last = floor.IsLastFloor && !nextStage;
            int stageNumber = host.trapFloorStageIndex + 1;
            if (won && last && !ReferenceEquals(winRecordedFor, floor))
            {
                winRecordedFor = floor; // PROFILE-2: one win in the profile's stats, however often this is shown
                host.EnsureProfile().RecordWin();
            }

            // FLOOR-2 (owner, 2026-10-22: "popup with Starting next phase and OK"): won or lost is the first line.
            string title = last
                ? won ? "YOU WIN THE GAME" : "GAME OVER"
                : nextStage ? $"STAGE {stageNumber} COMPLETE" : "STARTING NEXT PHASE";
            string result = won ? $"Floor {number} won." : $"Floor {number} lost.";
            string next;
            if (last)
            {
                next = won
                    ? "That was the last floor: the team escaped the Trap Floor."
                    : "Everyone is out on the last floor: the team lost.";
            }
            else if (nextStage)
            {
                next = $"{result} {StageName(stageNumber - 1)} is done. Next: Stage {stageNumber + 1}, {StageName(stageNumber)}, "
                    + "from the full floor again, dealt fresh. This cannot be undone.";
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
            if (!last && !nextStage) nextRows = NextFloorSize(out nextColumns);
            string chipTitle = last
                ? won ? "You win the game" : "Game over"
                : nextStage
                    ? $"Stage {stageNumber + 1}: {StageName(stageNumber)}"
                    : $"Floor {number + 1} of {floor.FloorCount}";
            string chipDetail = last
                ? (string.IsNullOrEmpty(detail) ? next : detail)
                : nextStage
                    ? $"{host.trapFloorTemplate.GridColumns + floor.FloorIndex}×{host.trapFloorTemplate.GridRows + floor.FloorIndex}, a new look"
                        + (string.IsNullOrEmpty(detail) ? string.Empty : $" · {detail}")
                    : $"{nextColumns}×{nextRows}, dealt fresh"
                        + (string.IsNullOrEmpty(detail) ? string.Empty : $" · {detail}");
            if (host.runtimeUi.ShowTableMilestone(new TableMilestoneModel(
                    last ? (won ? "GAME WON" : "GAME LOST") : nextStage ? $"STAGE {stageNumber} WON" : $"FLOOR {number} {(won ? "WON" : "LOST")}",
                    won ? TableNoticeTone.Won : TableNoticeTone.Danger,
                    chipTitle,
                    chipDetail,
                    last || nextStage ? FloorEndedActionLabel(won) : "Start",
                    () =>
                    {
                        host.runtimeUi?.CloseTableMilestone();
                        if (!IsFloorEnded()) return; // Undo took the floor's end back while the chip waited
                        if (nextStage) DealFloor(0, stageNumber);
                        else if (last) DealFloor(0);
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
                        last || nextStage ? FloorEndedActionLabel(won) : "OK",
                        true,
                        () =>
                        {
                            close();
                            if (nextStage) DealFloor(0, stageNumber);
                            else if (last) DealFloor(0);
                            else DealFloor(floor.FloorIndex + 1);
                        }),
                    new PopupActionOption("Not yet", true, close),
                },
                close,
                _ => close());
        }

        // Sets the table up again at the given floor (0 = a new game at full size, or the start of stage
        // <paramref name="stageIndex"/>), keeping the mode and seats.
        private void DealFloor(int floorIndex, int stageIndex = 0)
        {
            if (host.activeSession == null) return;
            PendingWinMode = null; // MODE-1: the next floor keeps this game's win mode and turn direction
            PendingAnticlockwise = null;
            host.trapFloorNextFloorIndex = floorIndex;
            host.trapFloorNextStageIndex = floorIndex == 0 ? Math.Max(0, stageIndex) : host.trapFloorStageIndex;
            host.trapFloorCarriedAvatars = CaptureSeatAvatars(); // D3: each seat keeps the Avatar it chose
            bool dealt;
            try
            {
                dealt = host.SessionService.TryReplaceTable(host.activeSession.Selection);
            }
            finally
            {
                host.trapFloorNextFloorIndex = 0;
                host.trapFloorNextStageIndex = 0;
                host.trapFloorCarriedAvatars = null;
            }

            if (!dealt || host.trapFloorTemplate == null) return;
            host.ShowMessage(floorIndex == 0
                ? stageIndex > 0 ? $"STAGE {stageIndex + 1}: {StageName(stageIndex)} — Floor 1." : "A new game: Floor 1."
                : $"FLOOR {host.trapFloorTemplate.FloorIndex + 1} of {host.trapFloorTemplate.FloorCount} — "
                    + $"{host.trapFloorTemplate.GridColumns}×{host.trapFloorTemplate.GridRows}, dealt fresh.");
        }
    }
}
