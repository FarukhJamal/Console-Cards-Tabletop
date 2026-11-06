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
        /// AUTH-RULES-2: the rules state the host keeps after a joined computer's pushed table: always its own
        /// (<see cref="TrapFloorRulesPushGuard"/>); <paramref name="refused"/> lists what the push tried to change. Null when
        /// this table has no Trap Floor state (another game: unchanged behaviour).
        /// </summary>
        internal ICartridgeUndoState HostMergePushedRules(ICartridgeUndoState pushed, PlayerId sender, List<string> refused)
        {
            if (!(CaptureUndoState() is TrapFloorUndoState mine)) return null;
            if (!(pushed is TrapFloorUndoState theirs))
            {
                refused?.Add("no rules state in the push");
                return mine;
            }

            TrapFloorSessionStateSnapshot kept = TrapFloorRulesPushGuard.Merge(mine.Snapshot, theirs.Snapshot, sender, refused);
            return new TrapFloorUndoState(kept);
        }

        // ---------- AUTH-EFFECTS ----------

        private TrapFloorEffectExecutor EffectExecutor()
        {
            if (hostEvidence == null) hostEvidence = new HostEvidence(this);
            return new TrapFloorEffectExecutor(
                host.trapFloorAbilityResolutionService,
                host.trapFloorAbilityResolutionState,
                host.trapFloorTurnService,
                host.trapFloorTurnState,
                host.trapFloorTemplate,
                hostEvidence,
                DefinitionData);
        }

        // This table's own text for a card (never the sender's).
        private CardDefinitionData DefinitionData(ObjectDefinitionId definitionId) =>
            host.TryGetAuthoredCardDefinition(definitionId, out CardDefinition definition) ? definition.ToData() : null;

        // An effect command ran on this table. Another computer's step: its result shows here (lit tiles, statuses, the
        // player card). A rule read the floor dice or a d4: the host's next result for them is a new one (AUTH-DICE).
        private void OnEffectAccepted(bool floorDiceRead = false, TabletopObjectId dieRead = default)
        {
            if (floorDiceRead) FloorDiceRead();
            if (!dieRead.IsEmpty) hostDice.Consumed(dieRead);
            if (!host.ViewSync.Ready || !host.Replicated.Replaying) return;
            RefreshMovementAssistancePresentation();
            RefreshNeutralizedTrapPresentation();
            RefreshTrapFloorStatusUi();
        }

        private void FloorDiceRead()
        {
            if (host.trapFloorTemplate == null) return;
            hostDice.Consumed(host.trapFloorTemplate.FloorfallXAxisDieId);
            hostDice.Consumed(host.trapFloorTemplate.FloorfallYAxisDieId);
        }

        // The command context of a player who plays on this computer, for a step about another player (a collapse that
        // drops someone, a stuck player): the step is made here, by the player whose action caused it.
        private CommandContext LocalContextFor(PlayerId preferred)
        {
            if (!preferred.IsEmpty && host.IsPlayerOnThisComputer(preferred)) return host.CreateCommandContext(preferred);
            PlayerId active = host.trapFloorTurnState != null ? host.trapFloorTurnState.ActivePlayerId : PlayerId.Empty;
            if (!active.IsEmpty && host.IsPlayerOnThisComputer(active)) return host.CreateCommandContext(active);
            return host.CreateCommandContext();
        }

        /// <summary>AUTH-EFFECTS: a player is out for the floor, for a reason every table checks for itself.</summary>
        internal TrapFloorTurnAdvanceResult SubmitElimination(PlayerId player, TrapFloorEliminationReason reason, PlayerId causedBy)
        {
            return host.TableAuthority.Submit<TrapFloorEliminatePlayerCommand, TrapFloorTurnAdvanceResult>(
                new TrapFloorEliminatePlayerCommand(LocalContextFor(causedBy), player, reason));
        }

        private sealed class HostEvidence : ITrapFloorEffectEvidence
        {
            private readonly TrapFloorCartridge game;

            public HostEvidence(TrapFloorCartridge game) => this.game = game;

            public bool IsSurvivalEndgame => game.IsSurvivalEndgame();

            public bool IsEscaped(PlayerId player) =>
                game.host.trapFloorObjectiveState != null && game.host.trapFloorObjectiveState.IsPlayerEscaped(player);

            public bool StandsOnFloorCard(PlayerId player, TabletopObjectId floorCardId) => game.HostSeesPawnOnTile(player, floorCardId);

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

        // ---------- AUTH-TILE ----------

        // Full: a landing reveal, a Key claim and an escape are made by the player whose pawn stands on that tile on the
        // host's table (as a prize already is, AUTH-PRIZE). Free / Reminders: tabletop freedom.
        private bool HostAllowsTileCommand(object command, PlayerId sender)
        {
            if (host.trapFloorTemplate == null || host.matchState == null || host.trapFloorTurnState == null) return true;
            // Every level: whether an escape wins the floor, and whether it uses a Key, are the host's to work out (the
            // sender's own computer works them out the same way; a request that says otherwise is not run).
            if (command is TrapFloorAttemptEscapeCommand claim
                && host.trapFloorObjectiveState != null
                && IsOpenExit(claim.FloorCardId, out bool grand)
                && (claim.WinsFloor != !AnyOtherPlayerStillIn(sender) || claim.UsesKey != !grand))
            {
                hostRefusal = "The host refused that escape: the floor result it names is not the host's.";
                Debug.Log("[Authority] Escape refused (its floor result or Key use is not the host's).");
                return false;
            }

            if (PlayAssist.Level != PlayAssistLevel.Full) return true;
            TabletopObjectId floorCardId;
            string what;
            switch (command)
            {
                case TrapFloorRevealFloorCommand reveal: floorCardId = reveal.FloorCardId; what = "turn that tile over"; break;
                case TrapFloorClaimKeyCommand key: floorCardId = key.FloorCardId; what = "take that Key"; break;
                case TrapFloorAttemptEscapeCommand escape: floorCardId = escape.FloorCardId; what = "use that exit"; break;
                default: return true;
            }

            if (HostSeesPawnOnTile(sender, floorCardId)) return true;
            hostRefusal = $"The host refused that: on the host's table your pawn is not on that tile, so you cannot {what}.";
            Debug.Log($"[Authority] {command.GetType().Name} refused (Full, the pawn is not on that tile).");
            return false;
        }

        // ---------- AUTH-UNDO-2 ----------

        private readonly HashSet<CommandId> hostRolledSteps = new HashSet<CommandId>();

        /// <summary>
        /// Full: true when the step Undo would take back is a rules result (a tile turned over, a Trap applied or
        /// launched, a status scheduled, a Blind direction rolled, a collapse, an elimination, or a die the host rolled).
        /// Read from the step's own before and after tables, so a step made of several parts is judged as a whole.
        /// </summary>
        internal bool IsRulesResultStep(ICartridgeUndoState before, ICartridgeUndoState after, CommandId stepId)
        {
            if (PlayAssist.Level != PlayAssistLevel.Full) return false;
            if (!stepId.IsEmpty && hostRolledSteps.Contains(stepId)) return true;
            if (!(before is TrapFloorUndoState a) || !(after is TrapFloorUndoState b)) return false;
            return TrapFloorUndoEligibility.IsRulesResult(a.Snapshot, b.Snapshot);
        }

        // COLLAPSE-ONCE (s143): one collapse per logical floor turn, remembered outside the Undo history.
        private readonly TrapFloorFloorTurnLedger floorTurnLedger = new TrapFloorFloorTurnLedger();
        private MatchId floorTurnLedgerMatch;

        private long FloorTurnLedgerKey()
        {
            if (host.matchState != null && host.matchState.Id != floorTurnLedgerMatch)
            {
                floorTurnLedger.Clear(); // a new floor (or a new table) starts its rounds again
                floorTurnLedgerMatch = host.matchState.Id;
            }

            return TrapFloorHostDice.TurnKeyOf(host.trapFloorTurnState);
        }

        internal bool FloorTurnMayCollapse() =>
            floorTurnLedger.MayCollapse(FloorTurnLedgerKey(), TrapFloorFloorTurnLedger.CollapseHoles(host.trapFloorCollapseState));

        internal void RecordFloorTurnCollapse() =>
            floorTurnLedger.RecordCollapse(FloorTurnLedgerKey(), TrapFloorFloorTurnLedger.CollapseHoles(host.trapFloorCollapseState));

        // ---------- AUTH-DICE ----------

        private readonly TrapFloorHostDice hostDice = new TrapFloorHostDice();

        /// <summary>
        /// Full: the dice a rule reads now. The two floor dice (a collapse, a Crane), and a d4 while the turn player's
        /// Blind direction waits for its roll. Read from this table only.
        /// </summary>
        internal bool IsRulesDie(TabletopObjectId dieId)
        {
            if (PlayAssist.Level != PlayAssistLevel.Full || host.trapFloorTemplate == null || host.matchState == null) return false;
            if (dieId == host.trapFloorTemplate.FloorfallXAxisDieId || dieId == host.trapFloorTemplate.FloorfallYAxisDieId) return true;
            return host.matchState.Dice.TryGetValue(dieId, out DieState die)
                && die.SideCount == 4
                && TryGetActiveBlindStatus(out TrapFloorBlindStatusState blind)
                && !blind.IsDirectionResolved;
        }

        /// <summary>
        /// Host, Full: a joined computer's die came to rest with a face a rule reads. The host draws the result itself
        /// and turns the die to show it; the command sent on (to everyone, the thrower too) carries the host's result.
        /// False: not a rules die, or the die cannot be turned to the host's face here (then nothing is replaced).
        /// </summary>
        internal bool TryHostRulesDie(CommitPhysicalObjectCommand commit, out CommitPhysicalObjectCommand rewritten)
        {
            rewritten = null;
            if (commit == null
                || !commit.SettledDieValue.HasValue
                || commit.State == null
                || commit.State.Mode != PhysicalObjectMode.Sleeping
                || !IsRulesDie(commit.ObjectId)
                || !host.matchState.Dice.TryGetValue(commit.ObjectId, out DieState die)
                || !host.Registry.TryGetDieView(commit.ObjectId, out DieView dieView))
            {
                return false;
            }

            TrapFloorTemplateDefinition template = host.trapFloorTemplate;
            bool xAxis = commit.ObjectId == template.FloorfallXAxisDieId;
            bool yAxis = commit.ObjectId == template.FloorfallYAxisDieId;
            int value = hostDice.Decide(
                commit.ObjectId,
                die.SideCount,
                TrapFloorHostDice.TurnKeyOf(host.trapFloorTurnState),
                v => (xAxis && v > template.GridColumns) || (yAxis && v > template.GridRows), // the rules ask for a re-roll
                sides => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, sides + 1));
            if (commit.RecordMode == AuthoritativeActionRecordMode.Transaction) hostRolledSteps.Add(commit.Context.Id);
            // DICE-LOOP (live test, 2026-11-06): the die already shows the host's result (the throw happened to land on it,
            // or it settled again after the host turned it). Nothing is replaced, so the thrower's die is not turned
            // again: turning it woke its physics, it settled again, and the host turned it again, without end.
            if (value == commit.SettledDieValue.Value) return false;
            PhysicalRotation thrown = commit.State.Rotation;
            float yaw = new Quaternion(thrown.X, thrown.Y, thrown.Z, thrown.W).eulerAngles.y;
            if (!dieView.TryGetValueRotation(value, yaw, out Quaternion shown)) return false;
            rewritten = new CommitPhysicalObjectCommand(
                commit.Context,
                commit.ObjectId,
                commit.State.WithRotation(new PhysicalRotation(shown.x, shown.y, shown.z, shown.w)),
                commit.ExpectedPhysicalRevision,
                value,
                commit.RecordMode);
            Debug.Log($"[Authority] Rules die (d{die.SideCount}) thrown{(host.IsPlayerOnThisComputer(commit.Context.RequestedByPlayerId) && !host.Replicated.Replaying ? " here" : " by another computer")}: "
                + $"the host's result is {value} (it showed {commit.SettledDieValue.Value}).");
            return true;
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
