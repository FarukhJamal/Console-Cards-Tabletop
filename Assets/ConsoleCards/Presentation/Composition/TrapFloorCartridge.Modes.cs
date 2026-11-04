using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The game mode (owner, 2026-11-04, TRAP CARDS rules), chosen at New table… apart from the difficulty:
    /// Free for all (default): everyone works toward the exit; a player may hold one Key and needs it for the Grand
    /// Exit. Team Play (2v2, competitive): a Key belongs to the player who took it and one player may carry them all;
    /// a team needs a Key for the Grand Exit. Survival: one 6×6 floor, 3 Keys, Grand Exit only, and a tile collapses
    /// after every player's turn (never the Grand Exit). In every mode a Key used at the Secret Exit is gone, and the
    /// floor ends when every player still in has escaped.
    /// </summary>
    internal enum TrapFloorWinMode
    {
        FreeForAll = 0,
        TeamPlay = 1,
        Survival = 2,
    }

    /// <summary>
    /// The game modes, Survival's turn order and the turn direction (DIR-1). Event-time only, except
    /// <see cref="UpdateTurnOrderRule"/> (one field write per frame).
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        internal const string ImpossibleModeStableId = "trap-floor-mode-impossible";
        internal const int SurvivalKeyCount = 3;

        // Chosen at New table… and kept for the next floors, Play Again and Reset.
        internal TrapFloorWinMode WinMode { get; set; } = TrapFloorWinMode.FreeForAll;

        // DIR-1 (owner, 2026-10-22: "players decide clockwise or anticlockwise when more than 2"): false plays in seat
        // order, true the other way round (the first player stays first).
        internal bool Anticlockwise { get; set; }

        // MODE-1 / DIR-1: chosen in New table…, applied when that table is dealt (ApplyPendingTableChoices).
        internal TrapFloorWinMode? PendingWinMode { get; set; }
        internal bool? PendingAnticlockwise { get; set; }

        // The mode the next deal is made for (the choice just made in New table…, else the mode in play).
        internal TrapFloorWinMode DealWinMode => PendingWinMode ?? WinMode;

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
                case TrapFloorWinMode.TeamPlay: return "Team Play";
                case TrapFloorWinMode.Survival: return "Survival";
                default: return "Free for all";
            }
        }

        internal static string WinModeHint(TrapFloorWinMode mode)
        {
            switch (mode)
            {
                case TrapFloorWinMode.TeamPlay: return "Team Play: 2 v 2. Take the Keys before the other team; your team needs a Key to exit.";
                case TrapFloorWinMode.Survival: return "Survival: one 6×6 floor. A tile collapses after every turn; 3 Keys open the Grand Exit.";
                default: return "Free for all: everyone heads for the exit. One Key each; no Key, no Grand Exit.";
            }
        }

        internal static string TurnsName(bool anticlockwise) => anticlockwise ? "Anticlockwise" : "Clockwise";

        // Owner, 2026-11-04 (TRAP CARDS rules): what each floor holds in each mode.
        //   Team Play:    6×6 3 Keys, Grand + Secret · 5×5 2 Keys, Grand · 4×4 2 Keys, Grand
        //   Free for all: 6×6 3 Keys, Grand + Secret · 5×5 2 Keys, Grand + Secret · 4×4 2 Keys, Grand
        //   Survival:     6×6 only, 3 Keys, Grand
        // The difficulty shapes the rest of the mix (rules: "trap cards and ability cards are more if the difficulty is
        // high; the easier the difficulty the more assisting cards"); a floor deck of the players' replaces it.
        internal static TrapFloorFloorRecipe RecipeFor(
            TrapFloorWinMode mode,
            int floorIndex,
            string difficultyStableId = null,
            IReadOnlyDictionary<string, int> tileCounts = null)
        {
            DifficultyWeights(difficultyStableId, out double traps, out double abilities, out double friends);
            if (mode == TrapFloorWinMode.Survival)
                return new TrapFloorFloorRecipe(1, SurvivalKeyCount, false, SurvivalKeyCount, traps, abilities, friends, tileCounts);
            int keys = floorIndex == 0 ? 3 : 2;
            bool secret = floorIndex == 0 || (floorIndex == 1 && mode == TrapFloorWinMode.FreeForAll);
            return new TrapFloorFloorRecipe(3, keys, secret, 1, traps, abilities, friends, tileCounts,
                mode == TrapFloorWinMode.FreeForAll ? "lasso" : null); // rules: "No lasso in this game mode"
        }

        private static void DifficultyWeights(string stableId, out double traps, out double abilities, out double friends)
        {
            string id = stableId ?? string.Empty;
            if (id.EndsWith("impossible", StringComparison.OrdinalIgnoreCase))
            {
                traps = 1.3d; abilities = 1.2d; friends = 0.5d;
            }
            else if (id.EndsWith("hard", StringComparison.OrdinalIgnoreCase))
            {
                traps = 1.15d; abilities = 1.15d; friends = 0.75d;
            }
            else if (id.EndsWith("easy", StringComparison.OrdinalIgnoreCase))
            {
                traps = 0.85d; abilities = 1d; friends = 1.5d;
            }
            else
            {
                traps = 1d; abilities = 1d; friends = 1d;
            }
        }

        internal TrapFloorFloorRecipe DealRecipe(int floorIndex, string difficultyStableId = null, PlayerDecksFile decks = null) =>
            RecipeFor(DealWinMode, floorIndex, difficultyStableId, FloorTileCounts(decks, floorIndex));

        // Owner, 2026-11-04: the players' own floor deck for a floor (Decks › Trap Floor › Floor n), card ID to copies;
        // null when they left it to the game. Stored as a deck with stage = the floor's number.
        internal static IReadOnlyDictionary<string, int> FloorTileCounts(PlayerDecksFile decks, int floorIndex)
        {
            if (decks == null || decks.decks == null) return null;
            for (int i = 0; i < decks.decks.Count; i++)
            {
                PlayerDeckRecord deck = decks.decks[i];
                if (deck == null || deck.stage != floorIndex + 1 || deck.cards == null) continue;
                Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < deck.cards.Count; c++)
                {
                    PlayerDeckEntry entry = deck.cards[c];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.cardId) || entry.count <= 0) continue;
                    counts.TryGetValue(entry.cardId, out int had);
                    counts[entry.cardId] = had + entry.count;
                }

                return counts.Count > 0 ? counts : null;
            }

            return null;
        }

        // The Keys the Grand Exit needs, counted by the objective: 3 in Survival; else 1 (a player's own Key in Free for
        // all, the team's in Team Play; the table checks whose).
        internal int RequiredKeysForGrandExit() => WinMode == TrapFloorWinMode.Survival ? SurvivalKeyCount : 1;

        internal void ConfigureKeyRules(TrapFloorObjectiveState objective)
        {
            if (objective == null) return;
            objective.KeysAreExclusive = WinMode != TrapFloorWinMode.Survival;
            objective.OneKeyPerPlayer = WinMode == TrapFloorWinMode.FreeForAll;
        }

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

        // Survival (rules: "a collapse will happen after each player turn"): a floor turn follows every player turn.
        internal void UpdateTurnOrderRule()
        {
            if (host.trapFloorTurnState == null) return;
            host.trapFloorTurnState.FloorTurnAfterEachPlayer = WinMode == TrapFloorWinMode.Survival;
        }

        // What the status shows as the Keys the Grand Exit needs.
        internal int KeysNeededForGrandExit()
        {
            if (WinMode != TrapFloorWinMode.Survival) return 1;
            int needed = host.trapFloorObjectiveState != null ? host.trapFloorObjectiveState.RequiredKeyCount : 0;
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

        // The rules have no endgame after the Exit is revealed (MODE-2 is gone, owner 2026-11-04).
        internal bool IsSurvivalEndgame() => false;

        // A floor where every player is out but someone escaped is won (knocked-out players do not count).
        internal bool FloorFailedCountsAsWin() =>
            host.trapFloorObjectiveState != null
            && host.trapFloorObjectiveState.EscapedPlayerCount > 0;

        // Team Play: partners sit opposite each other (seats 1 and 3, seats 2 and 4).
        internal int TeamOf(PlayerId player) =>
            TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup) ? setup.LayoutSeatIndex % 2 : -1;

        internal static string TeamName(int team) => team == 0 ? "Team 1" : "Team 2";

        // Team Play: the player's team holds a Key (a teammate who escaped through the Grand Exit keeps theirs).
        private bool TeamHasKey(PlayerId player)
        {
            int team = TeamOf(player);
            IReadOnlyList<PlayerId> order = host.trapFloorTurnState.PlayerOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (TeamOf(order[i]) == team && host.trapFloorObjectiveUseCase.HasClaimedKey(order[i])) return true;
            }

            return false;
        }

        // Exits after the turn player lands, by mode. The floor ends when every player still in has escaped.
        private void TryTakeExitsForMode(TabletopObjectId landedFloorCardId, PlayerId active)
        {
            if (landedFloorCardId.IsEmpty || active.IsEmpty || !IsOpenExit(landedFloorCardId, out bool grand)) return;
            if (host.trapFloorObjectiveState.IsPlayerEscaped(active)) return;
            if (grand)
            {
                if (WinMode == TrapFloorWinMode.Survival)
                {
                    int needed = KeysNeededForGrandExit();
                    if (host.trapFloorObjectiveState.CollectedKeyCount < needed)
                    {
                        host.FeedbackLine(
                            $"GRAND EXIT — locked: {needed} Keys needed, {host.trapFloorObjectiveState.CollectedKeyCount} taken.",
                            FeedbackSeverity.Warning);
                        return;
                    }
                }
                else if (WinMode == TrapFloorWinMode.TeamPlay && !TeamHasKey(active))
                {
                    host.FeedbackLine($"GRAND EXIT — locked: {TeamName(TeamOf(active))} needs a Key.", FeedbackSeverity.Warning);
                    return;
                }
                else if (WinMode == TrapFloorWinMode.FreeForAll && !host.trapFloorObjectiveUseCase.HasClaimedKey(active))
                {
                    host.FeedbackLine("GRAND EXIT — locked: you need a Key of your own.", FeedbackSeverity.Warning);
                    return;
                }
            }
            else if (!host.trapFloorObjectiveUseCase.HasClaimedKey(active))
            {
                host.FeedbackLine("SECRET EXIT — you need a Key: land on a Key, then come back here.", FeedbackSeverity.Warning);
                return;
            }

            bool winsFloor = !AnyOtherPlayerStillIn(active);
            host.SessionService.BeginUndoBatch();
            bool escaped;
            try
            {
                escaped = AttemptTrapFloorEscape(landedFloorCardId, active, winsFloor, !grand);
                if (escaped && !winsFloor)
                {
                    // The pawn leaves the floor and the player takes no more turns this floor.
                    EndAbilityOffer(); // as End Turn does: the Ability draw not bought goes back
                    SendEscapedPawnAway(active);
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
                $"{FormatPlayerName(active).ToUpperInvariant()} ESCAPED{(grand ? string.Empty : " (the Key is used up)")}. "
                    + $"Still to escape: {StillInNames(active)}.",
                FeedbackSeverity.Info);
            EliminateStuckPlayers();
        }

        // Rules: a player (Free for all) or a team (Team Play) with no Key, when no Key is left to take, is stuck and
        // loses ("if there's no key for the player and you get stuck there, you lose automatically"). Event-time only.
        internal void EliminateStuckPlayers()
        {
            if (WinMode == TrapFloorWinMode.Survival
                || host.trapFloorObjectiveState == null
                || host.trapFloorTurnState == null
                || host.trapFloorObjectiveState.IsWon
                || host.trapFloorTurnState.IsCurrentFloorFailed
                || FreeKeyTileLeft())
            {
                return;
            }

            bool anyOut = false;
            IReadOnlyList<PlayerId> order = host.trapFloorTurnState.PlayerOrder;
            host.SessionService.BeginUndoBatch();
            try
            {
                for (int i = 0; i < order.Count; i++)
                {
                    PlayerId player = order[i];
                    if (host.trapFloorObjectiveState.IsPlayerEscaped(player)
                        || host.trapFloorTurnState.GetPlayerState(player) == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                    {
                        continue;
                    }

                    bool hasKey = WinMode == TrapFloorWinMode.TeamPlay
                        ? TeamHasKey(player)
                        : host.trapFloorObjectiveUseCase.HasClaimedKey(player);
                    if (hasKey) continue;
                    TrapFloorTurnAdvanceResult knocked = host.trapFloorTurnService.MarkPlayerEliminatedForCurrentRound(
                        host.matchState,
                        host.CreateCommandContext(player),
                        player);
                    if (!knocked.Succeeded) continue;
                    anyOut = true;
                    host.FeedbackLine(
                        $"{FormatPlayerName(player).ToUpperInvariant()} has no Key and none is left to take: OUT for this floor.",
                        FeedbackSeverity.Loss);
                    if (host.trapFloorTurnState.IsCurrentFloorFailed) break;
                }
            }
            finally
            {
                host.SessionService.EndUndoBatch();
            }

            if (!anyOut) return;
            RefreshTrapFloorStatusUi();
            if (host.trapFloorTurnState.IsCurrentFloorFailed) ShowFloorEnded(FloorFailedCountsAsWin());
        }

        // A Key tile nobody holds yet and that has not collapsed (face down or face up).
        private bool FreeKeyTileLeft()
        {
            if (host.trapFloorTemplate == null || host.matchState == null) return false;
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> cell in host.trapFloorTemplate.FloorCardIds)
            {
                if (!host.trapFloorTemplate.TryGetFloorCardState(host.matchState, cell.Value, out TrapFloorFloorCardState floor)
                    || floor.Content.Category != TrapFloorFloorContentCategory.Key
                    || (host.trapFloorCollapseState != null && host.trapFloorCollapseState.IsCollapsed(cell.Value))
                    || host.trapFloorObjectiveState.TryGetClaim(cell.Value, out _))
                {
                    continue;
                }

                return true;
            }

            return false;
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
