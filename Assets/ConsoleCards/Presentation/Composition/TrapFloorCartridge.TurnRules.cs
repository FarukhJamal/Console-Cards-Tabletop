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
    /// (a 1-tile move: where it came from; a straight move: the tile behind it). When a 2-tile move turned (an L), the
    /// game cannot see which of the 2 tiles in between was crossed (owner, 2026-11-17: follow the rulebook): if only one
    /// can be stood on, that one; if both can, the player picks. A face-down tile it steps back onto is turned over and
    /// takes effect.</item>
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
        // A 2-tile move that turned (an L): the 2 tiles it may have crossed (null: not such a move).
        internal TrapFloorCoordinate? stepBackTurnA;
        internal TrapFloorCoordinate? stepBackTurnB;

        // The landing scan saw the turn player's pawn go from one tile to another.
        private void NoteStepBack(TrapFloorCoordinate? from, TrapFloorCoordinate to)
        {
            stepBackTile = null;
            stepBackTurnA = null;
            stepBackTurnB = null;
            if (!from.HasValue) return; // onto the floor from beside the board: back to where the move began
            int dx = to.X - from.Value.X;
            int dy = to.Y - from.Value.Y;
            if (dx == 0 && dy == 0) return;
            if (dx != 0 && dy != 0)
            {
                // The move turned (an L): it crossed one of these 2 tiles (owner, 2026-11-17: the rulebook's tile before).
                if (Math.Abs(dx) != 1 || Math.Abs(dy) != 1) return;
                stepBackTurnA = new TrapFloorCoordinate(to.X, from.Value.Y);
                stepBackTurnB = new TrapFloorCoordinate(from.Value.X, to.Y);
                stepBackLanding = to;
                return;
            }

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
            if (stepBackTile.HasValue && stepBackLanding.Equals(pawnTile)) return stepBackTile;
            int crossed = TurnedStepBackTiles(pawnTile, turnedStepBackBuffer);
            return crossed == 1 ? turnedStepBackBuffer[0] : origin; // 2: the player picks (ReturnPawnToMoveOrigin)
        }

        private readonly List<TrapFloorCoordinate> turnedStepBackBuffer = new List<TrapFloorCoordinate>(2);

        /// <summary>
        /// After a 2-tile move that turned and landed on <paramref name="pawnTile"/>: the tiles in between it may have
        /// crossed that a pawn can stand on now (0, 1 or 2), in <paramref name="into"/>.
        /// </summary>
        internal int TurnedStepBackTiles(TrapFloorCoordinate pawnTile, List<TrapFloorCoordinate> into)
        {
            into.Clear();
            if (!stepBackTurnA.HasValue || !stepBackTurnB.HasValue || !stepBackLanding.Equals(pawnTile)) return 0;
            if (IsWalkableTile(stepBackTurnA.Value.X, stepBackTurnA.Value.Y)) into.Add(stepBackTurnA.Value);
            if (IsWalkableTile(stepBackTurnB.Value.X, stepBackTurnB.Value.Y)) into.Add(stepBackTurnB.Value);
            return into.Count;
        }

        // After a 2-tile move that turned, both tiles in between can be stood on: the player says which one they crossed
        // (owner, 2026-11-17). A face-down tile shows only its back. The choice is needed, so closing asks again.
        private void AskStepBackTile(PlayerId player, TabletopObjectId fromFloorCardId, TrapFloorCoordinate pawnTile, List<TrapFloorCoordinate> crossed)
        {
            List<FocusedCardOptionModel> options = new List<FocusedCardOptionModel>(crossed.Count);
            for (int i = 0; i < crossed.Count; i++)
            {
                if (!host.trapFloorTemplate.TryGetFloorCardId(crossed[i], out TabletopObjectId id)
                    || !host.trapFloorTemplate.TryGetFloorCardState(host.matchState, id, out TrapFloorFloorCardState floor)
                    || !host.matchState.Cards.TryGetValue(id, out CardInstanceState card))
                {
                    continue;
                }

                bool known = floor.IsRevealed;
                host.TryGetAuthoredCardDefinition(card.BaseState.DefinitionId, out CardDefinition art);
                options.Add(new FocusedCardOptionModel(
                    id,
                    known ? floor.Content.DisplayName : "Face-down tile",
                    $"Floor {crossed[i]}",
                    art == null ? null : known ? host.FrontArt(art) : host.BackArt(art),
                    true));
            }

            if (options.Count == 0) return;
            int round = host.trapFloorTurnState != null ? host.trapFloorTurnState.CurrentRound : -1;
            ShowSelectionFor(player, new FocusedCardSelectionModel(
                "STEP BACK — WHICH TILE DID YOU CROSS?",
                $"{FormatPlayerShortName(player)} turned on the way to {pawnTile}. Pick the tile the pawn crossed: it steps back there.",
                "A face-down tile there turns over and takes effect.",
                "Step back",
                options,
                1,
                selected => selected.Count == 1,
                selected =>
                {
                    host.runtimeUi?.CloseFocusedCardSelection();
                    if (!StillOnPickupTile(player, fromFloorCardId, round)
                        || !host.trapFloorTemplate.TryGetFloorCoordinate(selected[0], out TrapFloorCoordinate target))
                    {
                        return;
                    }

                    StepBackTo(player, target);
                },
                () =>
                {
                    host.runtimeUi?.CloseFocusedCardSelection();
                    if (StillOnPickupTile(player, fromFloorCardId, round)) AskStepBackTile(player, fromFloorCardId, pawnTile, crossed);
                }));
        }

        // The pawn still stands on the tile it took something from, in the same turn.
        private bool StillOnPickupTile(PlayerId player, TabletopObjectId fromFloorCardId, int round) =>
            host.trapFloorTurnState != null
            && host.trapFloorTurnState.CurrentRound == round
            && host.trapFloorTurnState.ActivePlayerId == player
            && TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)
            && TryReadPawnTile(setup.PawnId, out TrapFloorCoordinate tile)
            && host.trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId onId)
            && onId == fromFloorCardId;

        /// <summary>True when a pickup on <paramref name="pawnTile"/> may send the pawn to <paramref name="target"/>.</summary>
        internal bool IsPickupReturnTarget(PlayerId player, TrapFloorCoordinate pawnTile, TrapFloorCoordinate target)
        {
            TrapFloorCoordinate? origin = PickupReturnTile(player, pawnTile);
            if (!origin.HasValue) return false;
            if (origin.Value == target) return true;
            List<TrapFloorCoordinate> crossed = new List<TrapFloorCoordinate>(2);
            return TurnedStepBackTiles(pawnTile, crossed) == 2 && crossed.Contains(target);
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
            internal TrapFloorCoordinate? StepBackTurnA;
            internal TrapFloorCoordinate? StepBackTurnB;
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
                StepBackTurnA = stepBackTurnA,
                StepBackTurnB = stepBackTurnB,
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
            stepBackTurnA = record.StepBackTurnA;
            stepBackTurnB = record.StepBackTurnB;
            tooFarStateIndex = -1;
        }
    }
}
