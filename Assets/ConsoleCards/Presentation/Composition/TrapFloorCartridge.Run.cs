using System.Collections.Generic;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// P8A (owner, 2026-11-13: "Buying sync, Blind trap & floor progression"): the table-side parts of the run rules
    /// (TrapFloorRunRules.cs): a run's kept Abilities put on the next floor's table, the Blind trap's legal steps and its
    /// no-draw rule at the draw itself.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        // DealFloor (a run that goes on): on the computer that dealt the new floor, before it is shared. The joined
        // computers get the same table from the host (its whole table follows a new deal).
        private void ApplyCarriedAbilities(List<TrapFloorCarriedAbility> carried)
        {
            if (host.matchState == null || host.trapFloorTemplate == null || carried == null) return;
            TrapFloorCarryResult result = TrapFloorRunCarry.Apply(host.matchState, host.trapFloorTemplate, carried);
            host.ViewSync.SyncContainer(host.trapFloorTemplate.AbilityDeckId);
            host.ViewSync.SyncContainer(TrapFloorTemplateFactory.StartingAbilitiesId);
            foreach (TrapFloorCarriedAbility item in carried)
            {
                if (!item.Bought) continue;
                if (!item.ContainerId.IsEmpty) host.ViewSync.SyncContainer(item.ContainerId);
                else host.PresentAcceptedObject(item.CardId);
            }

            host.RefreshAcceptedCardFaces();
            Debug.Log($"[Floor] The run goes on: {result.BoughtKept} bought Abilit{(result.BoughtKept == 1 ? "y" : "ies")} kept, "
                + $"{result.StartingKept} starting Abilit{(result.StartingKept == 1 ? "y" : "ies")} kept, {result.StartingLeftOut} used ones not dealt again.");
        }

        // P8A eliminated-pawn fix (owner, 2026-11-13): this pawn's player is out for the rest of this floor.
        internal bool IsEliminatedPawn(TabletopObjectId pawnId)
        {
            if (host.trapFloorTemplate == null || host.trapFloorTurnState == null || host.matchState == null) return false;
            IReadOnlyList<TrapFloorPlayerSetupDefinition> players = host.trapFloorTemplate.Players;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].PawnId != pawnId) continue;
                if (!host.matchState.Seats.TryGetValue(players[i].SeatId, out var seat) || seat.OccupantPlayerId.IsEmpty) return false;
                IReadOnlyList<PlayerId> order = host.trapFloorTurnState.PlayerOrder;
                for (int p = 0; p < order.Count; p++)
                {
                    if (order[p] == seat.OccupantPlayerId)
                        return host.trapFloorTurnState.GetPlayerState(seat.OccupantPlayerId) == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound;
                }

                return false; // not in this floor's turn order
            }

            return false;
        }

        // P8A (Blind): where the player's pawn stands now.
        internal bool TryGetBlindPawnTile(PlayerId player, out TrapFloorCoordinate tile)
        {
            tile = default;
            return TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup) && TryReadPawnTile(setup.PawnId, out tile);
        }

        // P8A (Blind): a tile of this floor that a pawn may step onto (on the floor, not a hole).
        internal bool IsOpenFloorTile(TrapFloorCoordinate tile) =>
            host.trapFloorTemplate != null
            && host.trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId floorCardId)
            && (host.trapFloorCollapseState == null || !host.trapFloorCollapseState.IsCollapsed(floorCardId));

        private int blindDrawWarnedRound = -1;

        /// <summary>
        /// P8A (owner, 2026-11-13: "Full must enforce [No Controller draw next round] at the authoritative draw path"):
        /// every table's draw from a Controller deck refuses a Blind player's draw in Full (the same answer on every
        /// computer, replays included). Reminders lets it through and says it breaks the card's rule; Free is manual.
        /// </summary>
        internal bool RefusesBlindDraw(PlayerId drawer, ContainerId source)
        {
            if (PlayAssist.IsFree || host.trapFloorTemplate == null || host.trapFloorTurnState == null
                || host.trapFloorAbilityResolutionState == null) return false;
            if (!TrapFloorBlindRules.RefusesControllerDraw(host.trapFloorTemplate, host.trapFloorTurnState,
                    host.trapFloorAbilityResolutionState, source, drawer))
                return false;
            bool here = host.IsPlayerOnThisComputer(drawer) && (host.ReplicatedIfMade == null || !host.ReplicatedIfMade.Replaying);
            if (PlayAssist.Level == PlayAssistLevel.Full)
            {
                if (here) host.FeedbackLine("BLIND — no Controller Cards this round (the Blind Trap's rule).", FeedbackSeverity.Warning);
                return true;
            }

            if (here && blindDrawWarnedRound != host.trapFloorTurnState.CurrentRound)
            {
                blindDrawWarnedRound = host.trapFloorTurnState.CurrentRound;
                host.FeedbackLine("Reminder: BLIND — the Blind Trap says no Controller Cards this round.", FeedbackSeverity.Warning);
            }

            return false;
        }
    }
}
