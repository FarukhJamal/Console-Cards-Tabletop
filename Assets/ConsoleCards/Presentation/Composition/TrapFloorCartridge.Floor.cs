using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
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
        internal bool IsCurrentFloorEnded => IsFloorEnded(); // M1a: the host checks a joined computer's deal request

        private bool IsFloorEnded() =>
            host.trapFloorTurnState != null
            && (host.trapFloorTurnState.IsCurrentFloorFailed
                || (host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsWon));

        internal string FloorEndedActionLabel() => FloorEndedActionLabel(false);

        internal string FloorEndedActionLabel(bool won)
        {
            switch (RunStepAfter(won))
            {
                case TrapFloorRunStep.NextStage: return $"Start Stage {host.trapFloorStageIndex + 2}"; // STAGES-1
                case TrapFloorRunStep.NextFloor:
                    int rows = NextFloorSize(out int columns);
                    return $"Start next phase ({columns}×{rows})"; // FLOOR-2
                default: return "Play Again"; // GAME WON / GAME OVER: a fresh run from Floor 1
            }
        }

        // P8A (owner, 2026-11-13: "Floor won vs floor lost"): won goes on (next floor or stage); the last floor won is GAME
        // WON; a lost floor is GAME OVER on any floor (never the next floor).
        internal TrapFloorRunStep RunStepAfter(bool won) =>
            TrapFloorRunProgression.After(
                won,
                host.trapFloorTemplate == null || host.trapFloorTemplate.IsLastFloor,
                StartsNextStage(true));

        // STAGES-1: winning a stage's last floor (3×3) starts the next stage at full size, while there is one.
        private bool StartsNextStage(bool won) =>
            won
            && host.trapFloorTemplate != null
            && host.trapFloorTemplate.IsLastFloor
            && host.trapFloorStageIndex < host.TrapFloorStageCount - 1;

        // P8A: deals what follows this floor's end, on the computer that deals (offline, or the host for everyone): the
        // next floor or stage of the same run (its players keep their Avatar and unused Abilities), or a fresh run.
        internal void DealAfterFloorEnd()
        {
            if (!IsFloorEnded()) return; // Undo took the floor's end back
            bool won = !host.trapFloorTurnState.IsCurrentFloorFailed || FloorFailedCountsAsWin(); // MODE-1
            TrapFloorRunStep step = RunStepAfter(won);
            Debug.Log($"[Floor] Floor {host.trapFloorTemplate.FloorIndex + 1} {(won ? "won" : "lost")}: {step}.");
            switch (step)
            {
                case TrapFloorRunStep.NextFloor: DealFloor(host.trapFloorTemplate.FloorIndex + 1, 0, true); break;
                case TrapFloorRunStep.NextStage: DealFloor(0, host.trapFloorStageIndex + 1, true); break;
                default: DealFloor(0); break; // Play Again: a new run, chosen fresh
            }
        }

        private string StageName(int stageIndex)
        {
            TrapFloorStage stage = host.trapFloorFloorPlan?.StageAt(stageIndex);
            return stage != null ? stage.Name : $"Stage {stageIndex + 1}";
        }

        internal string FloorEndedChoiceText() => FloorEndedChoiceText(false);

        // MODE-1: a Survival floor where someone escaped is won even when the rest are out.
        internal string FloorEndedChoiceText(bool won)
        {
            switch (RunStepAfter(won))
            {
                case TrapFloorRunStep.GameWon: return "That was the last floor: the game is won. Choose Play Again to start a new game.";
                case TrapFloorRunStep.GameOver: return "GAME OVER: the run is lost. Choose Play Again to start a new game from Floor 1.";
                default: return $"Choose {FloorEndedActionLabel(won)} in the turn menu.";
            }
        }

        private TrapFloorTemplateDefinition winRecordedFor;

        // FLOOREND-GUEST (2026-11-06): the floor-end panel was shown only on the computer whose game step ended the
        // floor (live run: the host's collapse), so the other players saw "ALL PLAYERS ELIMINATED" with no result or
        // next step. The panel is player feedback, not host administration: online, a floor end that arrives from
        // another computer (its table or a replayed command) now shows the same panel here. Start on a joined
        // computer asks the host to deal (existing MsgDeal path, refused unless the rules ended the floor).
        private TrapFloorTemplateDefinition floorEndShownFor;

        // NOTICE-REMOTE (2026-11-06, three-computer run): "X fell … ELIMINATED" was written only on the computer whose
        // game step knocked X out; the others (the eliminated player's own computer included) only saw the HUD change.
        // Online, a table or a replayed command from another computer is compared with what was here before it, and each
        // player newly out (or escaped) is announced. A player knocked out on this computer is announced by its own step.
        internal List<PlayerId> OutPlayersNow()
        {
            List<PlayerId> outPlayers = new List<PlayerId>();
            if (!host.IsOnlineTable || host.trapFloorTurnState == null) return outPlayers;
            IReadOnlyList<PlayerId> order = host.trapFloorTurnState.PlayerOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (host.trapFloorTurnState.GetPlayerState(order[i]) == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                    outPlayers.Add(order[i]);
            }

            return outPlayers;
        }

        internal void AnnounceOutChanges(List<PlayerId> before)
        {
            if (before == null || !host.IsOnlineTable || host.trapFloorTurnState == null) return;
            IReadOnlyList<PlayerId> order = host.trapFloorTurnState.PlayerOrder;
            for (int i = 0; i < order.Count; i++)
            {
                PlayerId player = order[i];
                if (before.Contains(player)
                    || host.trapFloorTurnState.GetPlayerState(player) != TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                    continue;
                bool escaped = host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsPlayerEscaped(player);
                string name = FormatPlayerName(player);
                Debug.Log($"[Floor] {name} {(escaped ? "escaped" : "is out for this floor")} (from another computer).");
                host.FeedbackLine(
                    escaped ? $"{name.ToUpperInvariant()} ESCAPED!" : $"{name.ToUpperInvariant()} is ELIMINATED for this floor.",
                    escaped ? FeedbackSeverity.Info : FeedbackSeverity.Loss);
            }
        }

        private void ShowFloorEndReachedElsewhere()
        {
            if (!host.IsOnlineTable || host.trapFloorTemplate == null || host.trapFloorTurnState == null) return;
            if (!IsFloorEnded())
            {
                if (ReferenceEquals(floorEndShownFor, host.trapFloorTemplate)) floorEndShownFor = null; // Undo took it back
                return;
            }

            if (ReferenceEquals(floorEndShownFor, host.trapFloorTemplate)) return;
            bool won = !host.trapFloorTurnState.IsCurrentFloorFailed || FloorFailedCountsAsWin();
            Debug.Log($"[Floor] Floor {host.trapFloorTemplate.FloorIndex + 1} ended on another computer ({(won ? "won" : "lost")}): showing the result here.");
            ShowFloorEnded(won);
        }

        // R3: the team that won the game (Team Play, last floor won), from this floor's escapes in the order they came.
        private bool TryGetWinningTeam(out string line)
        {
            line = string.Empty;
            if (host.trapFloorActivityFeed == null) return false;
            int[] escaped = new int[2];
            int first = -1;
            IReadOnlyList<TrapFloorActivityEntry> entries = host.trapFloorActivityFeed.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Kind != TrapFloorActivityKind.PlayerEscaped) continue;
                int team = TeamOf(entries[i].ActorPlayerId);
                if (team < 0 || team > 1) continue;
                escaped[team]++;
                if (first < 0) first = team;
            }

            if (first < 0) return false;
            int winner = escaped[0] == escaped[1] ? first : escaped[0] > escaped[1] ? 0 : 1;
            line = escaped[1 - winner] == 0
                ? $"{TeamName(winner)} wins the game."
                : $"{TeamName(winner)} wins the game ({escaped[winner]} out to {escaped[1 - winner]}"
                    + (escaped[0] == escaped[1] ? ", and out first)." : ").");
            return true;
        }

        // The floor's end: says what happened and offers the next floor (or a new game after the last one).
        internal void ShowFloorEnded(bool won, string detail = null)
        {
            if (host.runtimeUi == null || host.trapFloorTemplate == null) return;
            floorEndShownFor = host.trapFloorTemplate; // FLOOREND-GUEST: shown here once for this floor
            string recap = host.TakeFloorRecap(); // doc 57: what the floor showed
            if (!string.IsNullOrEmpty(recap)) detail = string.IsNullOrEmpty(detail) ? recap : $"{detail} · {recap}";
            TrapFloorTemplateDefinition floor = host.trapFloorTemplate;
            int number = floor.FloorIndex + 1;
            TrapFloorRunStep step = RunStepAfter(won); // P8A: lost is GAME OVER on any floor
            bool nextStage = step == TrapFloorRunStep.NextStage; // STAGES-1
            bool last = !TrapFloorRunProgression.GoesOn(step); // GAME WON or GAME OVER: the run ends here
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
                    : $"Everyone is out on Floor {number}: the run is lost. Play Again starts a new game from Floor 1 (Avatars are chosen again).";
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
                    + "come back. Each player keeps their Avatar and the Abilities they still hold; Hands are dealt again and the "
                    + "Controller deck is shuffled. This cannot be undone.";
            }

            // R3 (owner, 2026-11-14, accepted as proposed): in Team Play the game is won by the team any of whose players
            // escaped the last floor; when both did, by the one with more players out, then the one out first.
            if (won && last && WinMode == TrapFloorWinMode.TeamPlay && TryGetWinningTeam(out string teamLine))
            {
                next = $"{teamLine} {next}";
                detail = string.IsNullOrEmpty(detail) ? teamLine : $"{teamLine} · {detail}";
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
                        DealAfterFloorEnd(); // P8A: the run's next step, decided by the rules state
                    },
                    "Not yet",
                    () => host.runtimeUi?.CloseTableMilestone(),
                    // Owner decision C (2026-11-05): Free never deals the next floor by itself.
                    last || host.IsOnlineTable || PlayAssist.IsFree ? 0f : FloorEndedAutoDealSeconds))) // online: no countdown; a player presses Start
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
                            if (IsFloorEnded()) DealAfterFloorEnd(); // P8A
                        }),
                    new PopupActionOption("Not yet", true, close),
                },
                close,
                _ => close());
        }

        // ---------- Free mode: the floor is ended by hand (owner, 2026-11-05) ----------
        // "Add a manual End Floor action… completed, failed, or abandoned… allow manual recording of the winner or result.
        // Do not automatically resolve the floor or advance to the next one in Free Mode." The rules state (won / lost)
        // is not touched: the result is the players' call, written to the log, and the next floor is dealt only when a
        // player presses Next floor. Online, the host (the session controller) ends the floor.

        internal const string ManualEndFloorLabel = "End Floor…";

        internal void AddManualEndFloor(System.Collections.Generic.List<PopupActionOption> actions)
        {
            if (host.trapFloorTemplate == null || host.trapFloorTurnState == null || IsFloorEnded()) return;
            bool mayEnd = !host.IsOnlineTable || (host.HasOnline && host.Online.IsHosting);
            if (PlayAssist.IsFree) actions.Add(new PopupActionOption(ManualEndFloorLabel, mayEnd, ShowManualEndFloor));
            else actions.Add(new PopupActionOption(EndGameLabel, mayEnd, ShowEndGameAndRestart)); // P8A
        }

        // ---------- Full / Reminders: end the game and restart (owner, 2026-11-13) ----------
        // "The situation here … you cannot win it. … If it cannot be done [detecting it], I think it's fine for now. But
        // then there should be an option of ending the game and restarting it." The table does not judge whether the
        // floor can still be won (the no-escape check stays conservative, GB-1): the players decide. Online, the host
        // (who deals) ends it; a joined player sees it greyed.
        internal const string EndGameLabel = "End Game…";

        private void ShowEndGameAndRestart()
        {
            if (host.runtimeUi == null || host.trapFloorTemplate == null) return;
            if (host.IsOnlineTable && !(host.HasOnline && host.Online.IsHosting)) return; // the host deals online
            Action close = () => host.runtimeUi?.CloseTabletopPopup();
            host.runtimeUi.ShowContextMenu(
                new Vector2((Screen.width * 0.5f) - 200f, Screen.height * 0.6f),
                "END THIS GAME?",
                "Ends this game for everyone at the table and deals a new game from floor 1 (for example when nobody can still reach an exit). This cannot be undone.",
                new[]
                {
                    new PopupActionOption("End game & restart", true, () =>
                    {
                        close();
                        int number = host.trapFloorTemplate != null ? host.trapFloorTemplate.FloorIndex + 1 : 0;
                        string line = $"The game was ended on floor {number} and restarted.";
                        host.FeedbackLine(line, FeedbackSeverity.Warning);
                        Debug.Log($"[Floor] {line}");
                        DealFloor(0); // a new run: no Avatars or Abilities carried
                    }),
                    new PopupActionOption("Cancel", true, close),
                },
                close,
                _ => close());
        }

        private void ShowManualEndFloor()
        {
            if (host.runtimeUi == null || host.trapFloorTemplate == null) return;
            Action close = () => host.runtimeUi?.CloseTabletopPopup();
            int number = host.trapFloorTemplate.FloorIndex + 1;
            host.runtimeUi.ShowContextMenu(
                new Vector2((Screen.width * 0.5f) - 200f, Screen.height * 0.6f),
                $"END FLOOR {number}",
                "Free mode: the table does not judge the floor. Say how it ended; nothing is dealt until you press Next floor.",
                new[]
                {
                    new PopupActionOption("Completed…", true, () => { close(); ShowManualWinnerChoice(); }),
                    new PopupActionOption("Failed", true, () => { close(); FinishManualFloor("failed", null); }),
                    new PopupActionOption("Abandoned", true, () => { close(); FinishManualFloor("abandoned", null); }),
                    new PopupActionOption("Cancel", true, close),
                },
                close,
                _ => close());
        }

        private void ShowManualWinnerChoice()
        {
            Action close = () => host.runtimeUi?.CloseTabletopPopup();
            System.Collections.Generic.List<PopupActionOption> options = new System.Collections.Generic.List<PopupActionOption>();
            System.Collections.Generic.IReadOnlyList<PlayerId> order = host.trapFloorTurnState.PlayerOrder;
            for (int i = 0; i < order.Count; i++)
            {
                PlayerId player = order[i];
                string label = host.PlayerLabel(player);
                options.Add(new PopupActionOption($"Winner: {label}", true, () => { close(); FinishManualFloor("completed", label); }));
            }

            options.Add(new PopupActionOption("Everyone still in", true, () => { close(); FinishManualFloor("completed", "everyone still in"); }));
            options.Add(new PopupActionOption("No winner recorded", true, () => { close(); FinishManualFloor("completed", null); }));
            options.Add(new PopupActionOption("Cancel", true, close));
            host.runtimeUi.ShowContextMenu(
                new Vector2((Screen.width * 0.5f) - 200f, Screen.height * 0.6f),
                "FLOOR COMPLETED",
                "Record who won it (optional). The rules are not checked in Free mode.",
                options.ToArray(),
                close,
                _ => close());
        }

        private void FinishManualFloor(string result, string winner)
        {
            if (host.trapFloorTemplate == null || host.runtimeUi == null) return;
            TrapFloorTemplateDefinition floor = host.trapFloorTemplate;
            int number = floor.FloorIndex + 1;
            bool last = floor.IsLastFloor;
            string line = $"Floor {number} marked {result} by hand" + (string.IsNullOrEmpty(winner) ? "." : $"; winner: {winner}.");
            host.FeedbackLine(line, FeedbackSeverity.Warning); // in the log, and shown once
            Debug.Log($"[Floor] {line}");
            bool goOn = result == "completed" && !last; // P8A: only a won floor goes on (failed or abandoned: a new game)
            host.runtimeUi.ShowTableMilestone(new TableMilestoneModel(
                $"FLOOR {number} {result.ToUpperInvariant()}",
                result == "completed" ? TableNoticeTone.Won : TableNoticeTone.Caution,
                goOn ? $"Floor {number + 1} of {floor.FloorCount}" : "A new game",
                string.IsNullOrEmpty(winner) ? "Marked by hand (Free mode)" : $"Winner: {winner} (marked by hand)",
                goOn ? "Next floor" : "New game",
                () =>
                {
                    host.runtimeUi?.CloseTableMilestone();
                    if (host.IsOnlineTable && !(host.HasOnline && host.Online.IsHosting)) return; // the host deals online
                    if (goOn) DealFloor(floor.FloorIndex + 1, 0, true);
                    else DealFloor(0);
                },
                "Not yet",
                () => host.runtimeUi?.CloseTableMilestone(),
                0f)); // Free: never a countdown
        }

        // Sets the table up again at the given floor (0 = a new game at full size, or the start of stage
        // <paramref name="stageIndex"/>), keeping the mode and seats.
        private void DealFloor(int floorIndex, int stageIndex = 0, bool keepRun = false)
        {
            if (host.activeSession == null) return;
            PendingWinMode = null; // MODE-1: the next floor keeps this game's win mode and turn direction
            PendingAnticlockwise = null;
            host.trapFloorNextFloorIndex = floorIndex;
            host.trapFloorNextStageIndex = floorIndex == 0 ? Math.Max(0, stageIndex) : host.trapFloorStageIndex;
            // D3 / P8A (owner, 2026-11-13: "Avatar selection happens once per run"): a run that goes on keeps each seat's
            // Avatar and the Abilities it still holds; a new run (Play Again) starts with neither.
            host.trapFloorCarriedAvatars = keepRun ? CaptureSeatAvatars() : null;
            List<TrapFloorCarriedAbility> carried = keepRun ? TrapFloorRunCarry.Capture(host.matchState, host.trapFloorTemplate) : null;
            bool dealt;
            host.gameTemplatesPanelError = null; // NEXTFLOOR-1: only this deal's reason is reported
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

            if (!dealt)
            {
                if (host.IsOnlineTable && !(host.HasOnline && host.Online.IsHosting)) return; // a joined computer asked the host
                // NEXTFLOOR-1: a failed deal used to be silent at the table (the reason only reached the Games panel).
                string why = string.IsNullOrEmpty(host.gameTemplatesPanelError) ? "the reason is in the log" : host.gameTemplatesPanelError;
                Debug.LogWarning($"[Floor] Floor {floorIndex + 1} could not be dealt: {why}");
                host.ShowMessage($"Floor {floorIndex + 1} could not be dealt: {why}");
                return;
            }

            if (host.trapFloorTemplate == null) return;
            if (carried != null) ApplyCarriedAbilities(carried); // P8A: before the new table is shared
            ReviewFloorStanding("deal"); // P7 (s162): every floor holds one standing Grand Exit
            host.ShowMessage(floorIndex == 0
                ? stageIndex > 0 ? $"STAGE {stageIndex + 1}: {StageName(stageIndex)} — Floor 1." : "A new game: Floor 1."
                : $"FLOOR {host.trapFloorTemplate.FloorIndex + 1} of {host.trapFloorTemplate.FloorCount} — "
                    + $"{host.trapFloorTemplate.GridColumns}×{host.trapFloorTemplate.GridRows}, dealt fresh.");
        }
    }
}
