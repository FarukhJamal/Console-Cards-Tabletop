using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// MODE-1 (owner, 2026-10-22): the win mode, chosen at New table… apart from the difficulty.
    /// Regular (default): the Grand Exit wins when every player still in stands on it with the Keys; the Secret Exit is
    /// a bonus instant win for one player holding a Key. Team: one escape wins. Survival: every player still in must
    /// escape (escaped pawns leave the floor), the Grand Exit needs 3 Keys, and once it is face up tiles trigger again
    /// and prizes are off (MODE-2).
    /// </summary>
    internal enum TrapFloorWinMode
    {
        Regular = 0,
        Team = 1,
        Survival = 2,
    }

    /// <summary>
    /// The win modes (MODE-1, MODE-2), Impossible's turn order (IMP-1) and the turn direction (DIR-1). Event-time only,
    /// except <see cref="UpdateTurnOrderRule"/> (one field write per frame).
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        internal const string ImpossibleModeStableId = "trap-floor-mode-impossible";
        internal const int SurvivalKeyCount = 3;

        // Chosen at New table… and kept for the next floors, Play Again and Reset.
        internal TrapFloorWinMode WinMode { get; set; } = TrapFloorWinMode.Regular;

        // DIR-1 (owner, 2026-10-22: "players decide clockwise or anticlockwise when more than 2"): false plays in seat
        // order, true the other way round (the first player stays first).
        internal bool Anticlockwise { get; set; }

        // MODE-1 / DIR-1: chosen in New table…, applied when that table is dealt (ApplyPendingTableChoices).
        internal TrapFloorWinMode? PendingWinMode { get; set; }
        internal bool? PendingAnticlockwise { get; set; }

        internal void ApplyPendingTableChoices()
        {
            if (PendingWinMode.HasValue) WinMode = PendingWinMode.Value;
            if (PendingAnticlockwise.HasValue) Anticlockwise = PendingAnticlockwise.Value;
            PendingWinMode = null;
            PendingAnticlockwise = null;
        }

        internal static string WinModeName(TrapFloorWinMode mode)
        {
            switch (mode)
            {
                case TrapFloorWinMode.Team: return "Team";
                case TrapFloorWinMode.Survival: return "Survival";
                default: return "Regular";
            }
        }

        internal static string WinModeHint(TrapFloorWinMode mode)
        {
            switch (mode)
            {
                case TrapFloorWinMode.Team: return "Team: one player escaping wins the floor.";
                case TrapFloorWinMode.Survival: return "Survival: every player still in must escape; 3 Keys open the Grand Exit.";
                default: return "Regular: everyone still in on the Grand Exit with the Keys, or a Key holder on the Secret Exit.";
            }
        }

        internal static string TurnsName(bool anticlockwise) => anticlockwise ? "Anticlockwise" : "Clockwise";

        internal bool IsImpossibleMode =>
            host.trapFloorTemplate != null
            && host.trapFloorTemplate.ActiveMode != null
            && string.Equals(host.trapFloorTemplate.ActiveMode.StableId, ImpossibleModeStableId, StringComparison.OrdinalIgnoreCase);

        // DIR-1: the turn order for a new table: seat order, or the first player then the others the other way round.
        internal IEnumerable<PlayerId> OrderedTurnPlayers(IEnumerable<PlayerId> seatOrder)
        {
            List<PlayerId> order = new List<PlayerId>(seatOrder);
            if (Anticlockwise && order.Count > 2) order.Reverse(1, order.Count - 1);
            return order;
        }

        // IMP-1 (owner, 2026-10-22, Impossible): once a player has escaped, a floor turn follows every player turn.
        // Read by the turn state on its next advance; derived from the mode and the escapes, so Undo needs nothing.
        internal void UpdateTurnOrderRule()
        {
            if (host.trapFloorTurnState == null) return;
            host.trapFloorTurnState.FloorTurnAfterEachPlayer =
                IsImpossibleMode
                && host.trapFloorObjectiveState != null
                && host.trapFloorObjectiveState.EscapedPlayerCount > 0;
        }

        // The Keys the Grand Exit needs: the difficulty's, and 3 in Survival (no more than this floor holds).
        internal int KeysNeededForGrandExit()
        {
            int needed = host.trapFloorObjectiveState != null ? host.trapFloorObjectiveState.RequiredKeyCount : 0;
            if (WinMode != TrapFloorWinMode.Survival) return needed;
            return Math.Max(needed, Math.Min(SurvivalKeyCount, KeyTileCount()));
        }

        // The Key tiles a floor holds never change while it is played: counted once per floor (no per-frame scan).
        private TrapFloorTemplateDefinition keyCountTemplate;
        private int keyCountCached;

        private int KeyTileCount()
        {
            if (ReferenceEquals(keyCountTemplate, host.trapFloorTemplate)) return keyCountCached;
            int keys = 0;
            if (host.trapFloorTemplate == null || host.matchState == null) return keys;
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> cell in host.trapFloorTemplate.FloorCardIds)
            {
                if (host.trapFloorTemplate.TryGetFloorCardState(host.matchState, cell.Value, out TrapFloorFloorCardState floor)
                    && floor.Content.Category == TrapFloorFloorContentCategory.Key)
                {
                    keys++;
                }
            }

            if (keys > 0)
            {
                keyCountTemplate = host.trapFloorTemplate; // counted once the floor's cards are there
                keyCountCached = keys;
            }

            return keys;
        }

        // MODE-2: Survival's endgame starts when the Grand Exit tile is face up.
        internal bool IsSurvivalEndgame()
        {
            if (WinMode != TrapFloorWinMode.Survival || host.trapFloorTemplate == null || host.matchState == null) return false;
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> cell in host.trapFloorTemplate.FloorCardIds)
            {
                if (host.trapFloorTemplate.TryGetFloorCardState(host.matchState, cell.Value, out TrapFloorFloorCardState floor)
                    && floor.IsRevealed
                    && TrapFloorObjectiveUseCase.IsGrandExit(floor.Content))
                {
                    return true;
                }
            }

            return false;
        }

        // Survival: a floor where every player is out but someone escaped is won (knocked-out players do not count).
        internal bool FloorFailedCountsAsWin() =>
            WinMode == TrapFloorWinMode.Survival
            && host.trapFloorObjectiveState != null
            && host.trapFloorObjectiveState.EscapedPlayerCount > 0;

        // MODE-1: Team and Survival exits after the turn player lands (Regular keeps TryTakeExitsAfterMove's rules).
        private void TryTakeExitsForMode(TabletopObjectId landedFloorCardId, PlayerId active)
        {
            if (landedFloorCardId.IsEmpty || active.IsEmpty || !IsOpenExit(landedFloorCardId, out bool grand)) return;
            if (host.trapFloorObjectiveState.IsPlayerEscaped(active)) return;
            if (grand)
            {
                int needed = KeysNeededForGrandExit();
                if (host.trapFloorObjectiveState.CollectedKeyCount < needed)
                {
                    host.FeedbackLine(
                        $"GRAND EXIT — locked: the team needs {needed} Key(s), {host.trapFloorObjectiveState.CollectedKeyCount} claimed. "
                            + "Land on a Key to take it.",
                        FeedbackSeverity.Warning);
                    return;
                }
            }
            else if (!host.trapFloorObjectiveUseCase.HasClaimedKey(active))
            {
                host.FeedbackLine(
                    "SECRET EXIT — you need a Key you took: land on a Key, then come back here.",
                    FeedbackSeverity.Warning);
                return;
            }

            bool winsFloor = WinMode == TrapFloorWinMode.Team || !AnyOtherPlayerStillIn(active);
            host.SessionService.BeginUndoBatch();
            bool escaped;
            try
            {
                escaped = AttemptTrapFloorEscape(landedFloorCardId, active, winsFloor);
                if (escaped && !winsFloor)
                {
                    // Survival: the pawn leaves the floor and the player takes no more turns this floor.
                    EndAbilityOffer(); // as End Turn does: the Ability draw not bought goes back
                    SendEscapedPawnAway(active);
                    UpdateTurnOrderRule(); // IMP-1: the first escape changes Impossible's turn order from here on
                    host.trapFloorTurnService.MarkPlayerEliminatedForCurrentRound(
                        host.matchState,
                        host.CreateCommandContext(active),
                        active);
                }
            }
            finally
            {
                host.SessionService.EndUndoBatch();
            }

            if (!escaped || winsFloor) return;
            RefreshTrapFloorStatusUi();
            host.FeedbackLine(
                $"{FormatPlayerName(active).ToUpperInvariant()} ESCAPED! Still to escape: {StillInNames(active)}.",
                FeedbackSeverity.Info);
        }

        // Every player still in (not knocked out, not escaped) other than one.
        private bool AnyOtherPlayerStillIn(PlayerId except)
        {
            IReadOnlyList<PlayerId> order = host.trapFloorTurnState.PlayerOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] != except
                    && !host.trapFloorObjectiveState.IsPlayerEscaped(order[i])
                    && host.trapFloorTurnState.GetPlayerState(order[i]) != TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                {
                    return true;
                }
            }

            return false;
        }

        private string StillInNames(PlayerId except)
        {
            StringBuilder names = new StringBuilder();
            IReadOnlyList<PlayerId> order = host.trapFloorTurnState.PlayerOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] == except
                    || host.trapFloorObjectiveState.IsPlayerEscaped(order[i])
                    || host.trapFloorTurnState.GetPlayerState(order[i]) == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                {
                    continue;
                }

                if (names.Length > 0) names.Append(", ");
                names.Append(FormatPlayerName(order[i]));
            }

            return names.Length > 0 ? names.ToString() : "nobody";
        }

        // Survival: an escaped pawn is set beside the board, left of the floor, one row per seat.
        private void SendEscapedPawnAway(PlayerId player)
        {
            if (!TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)) return;
            int row = Mathf.Clamp(setup.LayoutSeatIndex + 1, 1, Math.Max(1, host.trapFloorTemplate.GridRows));
            if (!host.trapFloorTemplate.TryGetCellPosition(host.matchState, new TrapFloorCoordinate(1, row), out TableCoordinate edge)) return;
            for (int v = 0; v < host.Registry.pawnViews.Count; v++)
            {
                PawnView view = host.Registry.pawnViews[v];
                if (view == null || !view.IsBound || view.ObjectId != setup.PawnId || view.BoundState == null) continue;
                TabletopObjectState state = view.BoundState;
                TabletopPose pose = new TabletopPose(
                    new TableCoordinate(edge.X - (1.6d * host.trapFloorTemplate.Grid.ColumnPitch), edge.Y),
                    state.Pose.RotationDegrees,
                    state.Pose.Layer,
                    state.Pose.LocalOrder);
                host.MoveLoosePiece(view, state, pose);
                SyncLandingPawnTiles(); // the pawn left the grid: no landing to check
                return;
            }
        }
    }
}
