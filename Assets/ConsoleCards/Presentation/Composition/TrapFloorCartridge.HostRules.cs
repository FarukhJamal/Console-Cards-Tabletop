using System.Collections.Generic;
using System.Text;
using System;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views.Containers;
using ConsoleCards.Presentation.Views;
using UnityCamera = UnityEngine.Camera;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The host's side of Trap Floor's rules authority (2026-11-06). Runs only on the host, for what a joined computer
    /// sends. Every fact here is read from the host's own table, never from the sender's message.
    /// <list type="bullet">
    /// <item>AUTH-RULES: a pushed table's rules state is merged by <see cref="TrapFloorRulesPushGuard"/>: the host keeps
    /// its own Keys, escapes, collapse, turn and floor result, applies an elimination only when its table shows why, and
    /// takes the sender's own Trap outcomes and statuses only.</item>
    /// <item>AUTH-MOVE: in Full, a joined computer's pawn move is checked by <see cref="TrapFloorHostMoveRules"/> against
    /// the host's table (turn, seat, out players, holes, straight line, distance, Sticky, Blind, lit tiles). Free and
    /// Reminders keep tabletop freedom: nothing is refused for the game's rules.</item>
    /// </list>
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        private HostEvidence hostEvidence;
        private string hostRefusal;
        private int hostMoveRound = -1;
        private MatchState hostMoveMatch;
        private Guid hostMoveMatchId;
        private PlayerId hostMovePlayer;
        private TrapFloorCoordinate? hostMoveStart;
        private long hostMoveStartRevision;
        private bool hostGuidedMoveDone;
        // Where each pawn last rested on the host's table this turn, as the host accepted it. Physics sends a pawn's
        // carry and release as separate steps, so "where it is now" on the host can already be the release point; the
        // check counts from here instead.
        private readonly Dictionary<TabletopObjectId, TrapFloorCoordinate?> hostRestTiles =
            new Dictionary<TabletopObjectId, TrapFloorCoordinate?>();

        /// <summary>Why the host last refused a joined computer's command (for the notice), or null.</summary>
        internal string HostRefusal => hostRefusal;

        // ---------- AUTH-RULES ----------

        /// <summary>
        /// The rules state the host keeps after a joined computer's pushed table: its own, with what of the push it
        /// accepts. Null when this table has no Trap Floor state (another game: unchanged behaviour).
        /// </summary>
        internal ICartridgeUndoState HostMergePushedRules(
            ICartridgeUndoState pushed,
            PlayerId sender,
            List<string> refused,
            List<PlayerId> acceptedEliminations)
        {
            if (!(CaptureUndoState() is TrapFloorUndoState mine)) return null;
            if (!(pushed is TrapFloorUndoState theirs))
            {
                refused?.Add("no rules state in the push");
                return mine;
            }

            if (hostEvidence == null) hostEvidence = new HostEvidence(this);
            TrapFloorSessionStateSnapshot merged = TrapFloorRulesPushGuard.Merge(
                mine.Snapshot,
                theirs.Snapshot,
                sender,
                hostEvidence,
                refused,
                acceptedEliminations);
            return new TrapFloorUndoState(merged);
        }

        private sealed class HostEvidence : ITrapFloorHostEvidence
        {
            private readonly TrapFloorCartridge game;

            public HostEvidence(TrapFloorCartridge game) => this.game = game;

            public bool FloorTurnAfterEachPlayer =>
                game.host.trapFloorTurnState != null && game.host.trapFloorTurnState.FloorTurnAfterEachPlayer;

            public bool StandsOnHoleOrOffFloor(PlayerId player)
            {
                TabletopComposition host = game.host;
                if (host.trapFloorTemplate == null
                    || host.matchState == null
                    || !game.TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)
                    || !host.matchState.Pawns.ContainsKey(setup.PawnId))
                {
                    return false;
                }

                if (!game.TryReadPawnTile(setup.PawnId, out TrapFloorCoordinate tile)) return true; // off the floor
                if (!host.trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId floorCardId)) return true; // an empty cell
                return host.trapFloorCollapseState != null
                    && host.trapFloorCollapseState.TryGetCollapsedFloor(floorCardId, out TrapFloorCollapsedFloorState collapsed)
                    && !collapsed.IsTakenPrize;
            }

            public bool IsStuckWithoutKey(PlayerId player)
            {
                TabletopComposition host = game.host;
                if (game.WinMode == TrapFloorWinMode.Survival
                    || host.trapFloorObjectiveState == null
                    || host.trapFloorObjectiveUseCase == null
                    || host.trapFloorObjectiveState.IsWon
                    || host.trapFloorObjectiveState.IsPlayerEscaped(player)
                    || game.FreeKeyTileLeft())
                {
                    return false;
                }

                bool hasKey = game.WinMode == TrapFloorWinMode.TeamPlay
                    ? game.TeamHasKey(player)
                    : host.trapFloorObjectiveUseCase.HasClaimedKey(player);
                return !hasKey;
            }
        }

        // ---------- AUTH-MOVE ----------

        // A joined computer's pawn move, on the host, in Full only. True when it may run; else HostRefusal says why.
        private bool HostAllowsPawnMove(PlayerId sender, TabletopObjectId objectId, TableCoordinate? targetPosition)
        {
            if (PlayAssist.Level != PlayAssistLevel.Full) return true; // Free / Reminders: tabletop freedom
            if (host.trapFloorTemplate == null || host.matchState == null || host.trapFloorTurnState == null) return true;
            if (!TryGetPawnOwner(objectId, out PlayerId owner, out TrapFloorPlayerSetupDefinition ownerSetup)) return true; // not a player's pawn
            if (IsFloorEnded()) return true; // nothing more is played on this floor

            TrapFloorTurnState turn = host.trapFloorTurnState;
            bool playerTurn = !turn.IsCurrentFloorFailed && turn.Phase == TrapFloorTurnPhase.PlayerTurn;
            if (!ReferenceEquals(hostMoveMatch, host.matchState))
            {
                // A table put back (Undo, a push, a new floor): where pawns rest is read again from the host's table.
                hostMoveMatch = host.matchState;
                hostRestTiles.Clear();
            }

            if (hostMoveRound != turn.CurrentRound || hostMovePlayer != turn.ActivePlayerId || hostMoveMatchId != host.matchState.Id.Value)
            {
                hostMoveMatchId = host.matchState.Id.Value;
                hostMoveRound = turn.CurrentRound;
                hostMovePlayer = turn.ActivePlayerId;
                hostMoveStart = null;
                hostMoveStartRevision = host.matchState.Revision;
                hostGuidedMoveDone = false;
                hostRestTiles.Clear();
                if (playerTurn
                    && TryGetTrapFloorPlayerSetup(turn.ActivePlayerId, out TrapFloorPlayerSetupDefinition active)
                    && TryReadPawnTile(active.PawnId, out TrapFloorCoordinate activeTile))
                {
                    hostMoveStart = activeTile;
                }
            }

            if (!hostRestTiles.TryGetValue(objectId, out TrapFloorCoordinate? current))
            {
                current = RestingTile(objectId);
                hostRestTiles[objectId] = current;
            }

            TrapFloorCoordinate? target = null;
            if (targetPosition.HasValue
                && host.trapFloorTemplate.TryGetFloorCoordinateAt(host.matchState, targetPosition.Value, out TrapFloorCoordinate targetTile))
            {
                target = targetTile;
            }

            bool owned = owner == sender;
            int allowance = owned && playerTurn && turn.ActivePlayerId == sender ? MoveAllowance(out _) : 0;
            TrapFloorBlindStatusState blind = null;
            bool blindActive = owned && playerTurn && turn.ActivePlayerId == sender && TryGetActiveBlindStatus(out blind);
            TrapFloorHostMoveFacts facts = new TrapFloorHostMoveFacts
            {
                FullRules = true, // only reached in Full (above)
                Sender = sender,
                PawnOwner = owner,
                PawnOwnerOut = turn.GetPlayerState(owner) == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound
                    || (host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsPlayerEscaped(owner)),
                IsPlayerTurn = playerTurn,
                ActivePlayer = turn.ActivePlayerId,
                Current = current,
                TurnStart = owned && turn.ActivePlayerId == sender ? hostMoveStart : current,
                Target = target,
                Columns = host.trapFloorTemplate.GridColumns,
                Rows = host.trapFloorTemplate.GridRows,
                Walkable = (x, y) => IsWalkableTile(x, y),
                Allowance = allowance,
                Stuck = owned && playerTurn && turn.ActivePlayerId == sender && TryGetActiveStickyStatus(out _),
                BlindUnrolled = blindActive && !blind.IsDirectionResolved,
                BlindDirection = blindActive && blind.IsDirectionResolved ? blind.MovementDirection : null,
                LitTiles = LitTilesFor(sender),
                GuidedMoveDone = hostGuidedMoveDone,
                RuleMoveExpected = RuleMoveExpected(sender, ownerSetup, current, target),
            };

            TrapFloorHostMoveVerdict verdict = TrapFloorHostMoveRules.Check(facts);
            if (verdict == TrapFloorHostMoveVerdict.Allowed)
            {
                if (target.HasValue && facts.LitTiles != null && facts.LitTiles.Contains(target.Value)) hostGuidedMoveDone = true;
                hostRestTiles[objectId] = target;
                return true;
            }

            hostRefusal = $"The host refused that move: {TrapFloorHostMoveRules.Describe(verdict, allowance)}";
            Debug.Log($"[Authority] Pawn move refused (Full, {verdict}).");
            return false;
        }

        private bool TryGetPawnOwner(TabletopObjectId pawnId, out PlayerId owner, out TrapFloorPlayerSetupDefinition setup)
        {
            for (int i = 0; i < host.trapFloorTemplate.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition player = host.trapFloorTemplate.Players[i];
                if (player.PawnId != pawnId) continue;
                if (host.matchState.Seats.TryGetValue(player.SeatId, out var seat) && !seat.OccupantPlayerId.IsEmpty)
                {
                    owner = seat.OccupantPlayerId;
                    setup = player;
                    return InTurnOrder(owner);
                }
            }

            owner = PlayerId.Empty;
            setup = null;
            return false;
        }

        // Where a pawn rests on the host's table: its physics place, or, while it is held in a hand, where it was placed.
        private TrapFloorCoordinate? RestingTile(TabletopObjectId pawnId)
        {
            if (!host.matchState.Pawns.TryGetValue(pawnId, out PawnState pawn)) return null;
            PhysicalObjectState physical = pawn.BaseState.PhysicalState;
            if (physical != null && physical.Mode == PhysicalObjectMode.Held)
            {
                return host.trapFloorTemplate.TryGetFloorCoordinateAt(host.matchState, pawn.BaseState.Pose.Position, out TrapFloorCoordinate placed)
                    ? placed
                    : (TrapFloorCoordinate?)null;
            }

            return TryReadPawnTile(pawnId, out TrapFloorCoordinate tile) ? tile : (TrapFloorCoordinate?)null;
        }

        private bool InTurnOrder(PlayerId player)
        {
            IReadOnlyList<PlayerId> order = host.trapFloorTurnState.PlayerOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] == player) return true;
            }

            return false;
        }

        // Dodge / Rush / Spring / Crane: the tiles lit for this player on the host's table, or null.
        private ICollection<TrapFloorCoordinate> LitTilesFor(PlayerId player)
        {
            TrapFloorAbilityResolutionState state = host.trapFloorAbilityResolutionState;
            if (state == null) return null;
            IReadOnlyList<TabletopObjectId> targets = null;
            if (state.ActiveRushAssistance != null && state.ActiveRushAssistance.PlayerId == player)
                targets = state.ActiveRushAssistance.TargetFloorCardIds;
            else if (state.ActiveDodgeAssistance != null && state.ActiveDodgeAssistance.PlayerId == player)
                targets = state.ActiveDodgeAssistance.TargetFloorCardIds;
            if (targets == null || targets.Count == 0) return null;
            HashSet<TrapFloorCoordinate> tiles = new HashSet<TrapFloorCoordinate>();
            for (int i = 0; i < targets.Count; i++)
            {
                if (host.trapFloorTemplate.TryGetFloorCoordinate(targets[i], out TrapFloorCoordinate tile)) tiles.Add(tile);
            }

            return tiles;
        }

        // The rules move the pawn on their own this turn: a Spring or Crane the player revealed this turn (its target
        // comes from the Trap and the dice), or the way back to the starting tile after a Key or a prize was taken.
        private bool RuleMoveExpected(PlayerId sender, TrapFloorPlayerSetupDefinition setup, TrapFloorCoordinate? current, TrapFloorCoordinate? target)
        {
            TrapFloorAbilityResolutionState state = host.trapFloorAbilityResolutionState;
            if (state != null)
            {
                IReadOnlyList<TrapFloorTrapResolutionRecord> traps = state.TrapRecords;
                for (int i = 0; i < traps.Count; i++)
                {
                    TrapFloorTrapResolutionRecord trap = traps[i];
                    if (trap.AffectedPlayerId == sender && trap.Content.IsLaunchTrap && trap.RevealedRevision > hostMoveStartRevision)
                        return true;
                }
            }

            if (setup == null || !target.HasValue || !current.HasValue || target.Value != setup.StartingCorner) return false;
            if (!host.trapFloorTemplate.TryGetFloorCardId(current.Value, out TabletopObjectId onId)) return false;
            bool keyTaken = host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.HasPlayerClaimedKey(onId, sender);
            bool prizeTaken = host.trapFloorCollapseState != null
                && host.trapFloorCollapseState.TryGetCollapsedFloor(onId, out TrapFloorCollapsedFloorState taken)
                && taken.IsTakenPrize;
            return keyTaken || prizeTaken;
        }

        // The resting place a physics commit names, as a table position (null: none).
        private TableCoordinate? CommitTablePosition(CommitPhysicalObjectCommand commit)
        {
            if (commit.State == null || host.coordinateConverter == null) return null;
            return host.coordinateConverter.ToTableCoordinate(
                new Vector3(commit.State.Position.X, commit.State.Position.Y, commit.State.Position.Z));
        }

        // Called by HostAllowsCommand for moves (MoveObject, and physics releases and resting places; not a pawn held in the hand).
        private bool HostAllowsMoveCommand(object command, PlayerId sender)
        {
            if (command is MoveObjectCommand move)
                return HostAllowsPawnMove(sender, move.ObjectId, move.TargetPose.Position);
            // A pawn held in the hand is not placed anywhere yet; its release and its resting place are checked.
            if (command is CommitPhysicalObjectCommand commit && (commit.State == null || commit.State.Mode != PhysicalObjectMode.Held))
                return HostAllowsPawnMove(sender, commit.ObjectId, CommitTablePosition(commit));
            return true;
        }
    }
}
