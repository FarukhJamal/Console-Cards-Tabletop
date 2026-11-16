using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Rules v1.151126, step 4 (owner, 2026-11-15 and 2026-11-17):
    /// <list type="bullet">
    /// <item>After a Key, a Friend or a floor Ability is taken, the pawn steps back to the tile it stood on just before
    /// (a 1-tile move: where it came from; a straight move: the tile behind it). When a 2-tile move turned, the tile in
    /// between is not known, so the pawn goes back to where the move began. A face-down tile it steps back onto is
    /// turned over and takes effect.</item>
    /// <item>A turn is a move or one Action Ability; after an Action no Reaction may be used. Full refuses (the host
    /// too), Reminders reminds, Free does nothing.</item>
    /// </list>
    /// Event-time only.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        // The tile the active pawn stood on just before its latest landing, and that landing's tile (null: not known).
        internal TrapFloorCoordinate? stepBackTile;
        internal TrapFloorCoordinate stepBackLanding;

        // The landing scan saw the turn player's pawn go from one tile to another.
        private void NoteStepBack(TrapFloorCoordinate? from, TrapFloorCoordinate to)
        {
            stepBackTile = null;
            if (!from.HasValue) return; // onto the floor from beside the board: back to where the move began
            int dx = to.X - from.Value.X;
            int dy = to.Y - from.Value.Y;
            if (dx == 0 && dy == 0) return;
            if (dx != 0 && dy != 0) return; // the move turned (owner, 2026-11-17): back to where the move began
            stepBackTile = new TrapFloorCoordinate(to.X - Math.Sign(dx), to.Y - Math.Sign(dy));
            stepBackLanding = to;
        }

        /// <summary>
        /// Where a pickup sends the turn player's pawn standing on <paramref name="pawnTile"/>: the tile it stood on just
        /// before, else the tile the move began on (null: no move this turn, or not the turn player).
        /// </summary>
        internal TrapFloorCoordinate? PickupReturnTile(PlayerId player, TrapFloorCoordinate pawnTile)
        {
            TrapFloorCoordinate? origin = CurrentMoveOrigin(player); // also starts a new turn's count
            if (!origin.HasValue) return null;
            return stepBackTile.HasValue && stepBackLanding.Equals(pawnTile) ? stepBackTile : origin;
        }

        // The pawn stepped back onto a tile still face down: it turns over and takes effect (Free: the player does it).
        private void RevealSteppedBackTile(PlayerId player, TabletopObjectId floorCardId)
        {
            if (host.trapFloorTemplate == null
                || !host.trapFloorTemplate.TryGetFloorCardState(host.matchState, floorCardId, out TrapFloorFloorCardState floor)
                || floor.IsRevealed
                || (host.trapFloorCollapseState != null && host.trapFloorCollapseState.IsCollapsed(floorCardId)))
            {
                return;
            }

            if (PlayAssist.IsFree)
            {
                host.FeedbackLine("The tile stepped back onto is face down: turn it over (F).", FeedbackSeverity.Info);
                return;
            }

            host.landedFloorCards.Add(floorCardId);
            TriggerLandedTile(floorCardId, player);
        }

        // ---------- a move or an Action ----------

        /// <summary>True when the turn player's pawn has made a move this turn on this table.</summary>
        internal bool HasMovedThisTurn(PlayerId player)
        {
            if (host.trapFloorTurnState == null || host.trapFloorTurnState.ActivePlayerId != player || player.IsEmpty) return false;
            EnsureMoveTurn();
            if (moveSpentByRule) return true;
            if (!moveStartTile.HasValue
                || !TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)
                || !TryReadPawnTile(setup.PawnId, out TrapFloorCoordinate tile))
            {
                return false;
            }

            return moveStartFromWaiting || !tile.Equals(moveStartTile.Value);
        }

        /// <summary>True when the player used an Action Ability in this turn (other than <paramref name="except"/>).</summary>
        internal bool UsedActionThisTurn(PlayerId player, TabletopObjectId except = default)
        {
            if (host.trapFloorTurnState == null || host.matchState == null) return false;
            return TrapFloorMoveOrActionRules.UsedActionThisTurn(
                host.trapFloorAbilityResolutionState,
                host.matchState,
                player,
                host.trapFloorTurnState.CurrentRound,
                DefinitionData,
                except,
                usedThisTurnBuffer);
        }

        private readonly List<TabletopObjectId> usedThisTurnBuffer = new List<TabletopObjectId>();

        // What the card is under the rules (by its "action" / "reaction" tag).
        private bool IsActionCard(TabletopObjectId cardId, out bool reaction)
        {
            reaction = false;
            if (host.matchState == null || !host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card)) return false;
            CardDefinitionData definition = DefinitionData(card.BaseState.DefinitionId);
            if (definition == null) return false;
            reaction = TrapFloorShop.IsReaction(definition);
            return TrapFloorShop.IsAction(definition);
        }

        // Why this card may not be used now under "a move or an Action" (null: it may). Watched only (Full and Reminders).
        private string MoveOrActionProblem(TabletopObjectId cardId, PlayerId player)
        {
            if (!AssistRules.Watches(AssistRule.PawnMoveRules)) return null;
            bool action = IsActionCard(cardId, out bool reaction);
            bool actionUsed = UsedActionThisTurn(player, cardId);
            if (action && actionUsed) return "A turn is a move or one Action: an Action was already used this turn.";
            if (action && HasMovedThisTurn(player)) return "A turn is a move or an Action: you have moved this turn.";
            if (reaction && actionUsed) return "No Reaction after an Action: a Reaction goes only with a move.";
            return null;
        }

        // ---------- the turn's move count, kept with each Undo step ----------

        // Rules v1.151126 (2026-11-17 Play check): the move count (where the move began, a step-back that spent the
        // move, a guided move) is event-time state on this computer, outside the table. Undo and Redo put the table
        // back but left the count as it was, so after undoing a pickup the turn still counted as moved (no move, no
        // Action). Each Undo step now remembers the count beside its rules snapshot (never inside it: that snapshot is
        // sent to the other computers as it is), and a step restored from this computer's own history brings it back.
        // A table that comes from elsewhere leaves the count alone, as before. Once per step, not per frame.
        private sealed class MoveTurnRecord
        {
            internal int Round;
            internal PlayerId Player;
            internal TrapFloorCoordinate? Start;
            internal bool FromWaiting;
            internal bool Spent;
            internal bool Guided;
            internal TrapFloorCoordinate? StepBack;
            internal TrapFloorCoordinate StepBackLanding;
        }

        private static readonly ConditionalWeakTable<object, MoveTurnRecord> moveTurnByStep =
            new ConditionalWeakTable<object, MoveTurnRecord>();

        private void RememberMoveTurn(object step)
        {
            if (step == null) return;
            moveTurnByStep.Remove(step);
            moveTurnByStep.Add(step, new MoveTurnRecord
            {
                Round = moveTurnRound,
                Player = moveTurnPlayer,
                Start = moveStartTile,
                FromWaiting = moveStartFromWaiting,
                Spent = moveSpentByRule,
                Guided = guidedMoveThisTurn,
                StepBack = stepBackTile,
                StepBackLanding = stepBackLanding,
            });
        }

        // The rules state restored from a remembered step carries that step's move count.
        private static void LinkRestoredStep(object step, object restored)
        {
            if (step == null || restored == null || !moveTurnByStep.TryGetValue(step, out MoveTurnRecord record)) return;
            moveTurnByStep.Remove(restored);
            moveTurnByStep.Add(restored, record);
        }

        /// <summary>After Undo, Redo or a rebuild: the move count of the restored step (nothing for a table from elsewhere).</summary>
        internal void RecallMoveTurn(object restored)
        {
            if (restored == null || !moveTurnByStep.TryGetValue(restored, out MoveTurnRecord record)) return;
            moveTurnRound = record.Round;
            moveTurnPlayer = record.Player;
            moveStartTile = record.Start;
            moveStartFromWaiting = record.FromWaiting;
            moveSpentByRule = record.Spent;
            guidedMoveThisTurn = record.Guided;
            stepBackTile = record.StepBack;
            stepBackLanding = record.StepBackLanding;
            tooFarStateIndex = -1;
        }
    }
}
