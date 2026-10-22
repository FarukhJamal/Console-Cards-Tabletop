using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.ControllerInputs;

namespace ConsoleCards.Games.TrapFloor
{
    public enum TrapFloorTurnPhase
    {
        PlayerTurn = 0,
        FloorTurn = 1,
    }

    public enum TrapFloorCurrentFloorPlayerState
    {
        Active = 0,
        EliminatedForCurrentRound = 1,
    }

    /// <summary>Match-scoped assistance state. It never restricts freeform tabletop actions.</summary>
    public sealed class TrapFloorTurnState
    {
        private readonly ReadOnlyCollection<PlayerId> playerOrder;
        private readonly HashSet<PlayerId> eliminatedPlayerIds;
        private int activePlayerIndex;
        private int floorOperatorIndex;
        // IMP-1: in a floor turn between two player turns, the index of the player who plays next (-1: the floor turn
        // ends the round).
        private int floorResumeIndex = -1;

        public TrapFloorTurnState(MatchId matchId, IEnumerable<PlayerId> participatingPlayerIds)
            : this(
                matchId,
                participatingPlayerIds,
                1,
                TrapFloorTurnPhase.PlayerTurn,
                0,
                -1,
                Array.Empty<PlayerId>(),
                false)
        {
        }

        internal TrapFloorTurnState(
            MatchId matchId,
            IEnumerable<PlayerId> participatingPlayerIds,
            int currentRound,
            TrapFloorTurnPhase phase,
            int activePlayerIndex,
            int floorOperatorIndex,
            IEnumerable<PlayerId> eliminatedPlayerIds,
            bool isCurrentFloorFailed)
        {
            if (matchId.IsEmpty) throw new ArgumentException("Trap Floor turn state requires a Match ID.", nameof(matchId));
            if (participatingPlayerIds == null) throw new ArgumentNullException(nameof(participatingPlayerIds));
            if (currentRound < 1) throw new ArgumentOutOfRangeException(nameof(currentRound));
            if (!Enum.IsDefined(typeof(TrapFloorTurnPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));

            List<PlayerId> players = new List<PlayerId>(participatingPlayerIds);
            if (players.Count < 1)
                throw new ArgumentException("Trap Floor turn state requires at least one participating Player.", nameof(participatingPlayerIds));
            HashSet<PlayerId> uniquePlayers = new HashSet<PlayerId>();
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].IsEmpty || !uniquePlayers.Add(players[i]))
                    throw new ArgumentException("Trap Floor turn Players must be non-empty and unique.", nameof(participatingPlayerIds));
            }

            if (eliminatedPlayerIds == null) throw new ArgumentNullException(nameof(eliminatedPlayerIds));
            HashSet<PlayerId> eliminated = new HashSet<PlayerId>();
            foreach (PlayerId eliminatedPlayerId in eliminatedPlayerIds)
            {
                if (!uniquePlayers.Contains(eliminatedPlayerId) || !eliminated.Add(eliminatedPlayerId))
                {
                    throw new ArgumentException(
                        "Eliminated Trap Floor Players must be unique participants in this turn state.",
                        nameof(eliminatedPlayerIds));
                }
            }

            if (isCurrentFloorFailed != (eliminated.Count == players.Count))
            {
                throw new ArgumentException(
                    "A failed Trap Floor round requires every participating Player to be eliminated for the current round.",
                    nameof(isCurrentFloorFailed));
            }

            if (!isCurrentFloorFailed
                && phase == TrapFloorTurnPhase.PlayerTurn
                && (activePlayerIndex < 0 || activePlayerIndex >= players.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(activePlayerIndex));
            }

            if (!isCurrentFloorFailed
                && phase == TrapFloorTurnPhase.FloorTurn
                && (floorOperatorIndex < 0 || floorOperatorIndex >= players.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(floorOperatorIndex));
            }

            if (isCurrentFloorFailed && (activePlayerIndex != -1 || floorOperatorIndex != -1))
            {
                throw new ArgumentException("A failed Trap Floor cannot retain an assisted turn actor.");
            }
            if (!isCurrentFloorFailed
                && phase == TrapFloorTurnPhase.PlayerTurn
                && eliminated.Contains(players[activePlayerIndex]))
            {
                throw new ArgumentException("The active Trap Floor turn Player cannot be eliminated.");
            }
            if (!isCurrentFloorFailed
                && phase == TrapFloorTurnPhase.FloorTurn
                && eliminated.Contains(players[floorOperatorIndex]))
            {
                throw new ArgumentException("The Trap Floor operator cannot be eliminated.");
            }

            MatchId = matchId;
            playerOrder = new ReadOnlyCollection<PlayerId>(players);
            this.eliminatedPlayerIds = eliminated;
            CurrentRound = currentRound;
            Phase = phase;
            IsCurrentFloorFailed = isCurrentFloorFailed;
            this.activePlayerIndex = !isCurrentFloorFailed && phase == TrapFloorTurnPhase.PlayerTurn
                ? activePlayerIndex
                : -1;
            this.floorOperatorIndex = !isCurrentFloorFailed && phase == TrapFloorTurnPhase.FloorTurn
                ? floorOperatorIndex
                : -1;
        }

        // IMP-1: the restored turn state of a floor turn between players (Undo, Redo).
        internal TrapFloorTurnState(
            MatchId matchId,
            IEnumerable<PlayerId> participatingPlayerIds,
            int currentRound,
            TrapFloorTurnPhase phase,
            int activePlayerIndex,
            int floorOperatorIndex,
            IEnumerable<PlayerId> eliminatedPlayerIds,
            bool isCurrentFloorFailed,
            int floorResumeIndex)
            : this(
                matchId,
                participatingPlayerIds,
                currentRound,
                phase,
                activePlayerIndex,
                floorOperatorIndex,
                eliminatedPlayerIds,
                isCurrentFloorFailed)
        {
            this.floorResumeIndex = !isCurrentFloorFailed
                && phase == TrapFloorTurnPhase.FloorTurn
                && floorResumeIndex >= 0
                && floorResumeIndex < playerOrder.Count
                    ? floorResumeIndex
                    : -1;
        }

        public MatchId MatchId { get; }
        public int CurrentRound { get; private set; }
        public TrapFloorTurnPhase Phase { get; private set; }
        public bool IsCurrentFloorFailed { get; private set; }
        public IReadOnlyList<PlayerId> PlayerOrder => playerOrder;
        public PlayerId ActivePlayerId =>
            !IsCurrentFloorFailed && Phase == TrapFloorTurnPhase.PlayerTurn
                ? playerOrder[activePlayerIndex]
                : PlayerId.Empty;
        public PlayerId FloorOperatorPlayerId =>
            !IsCurrentFloorFailed && Phase == TrapFloorTurnPhase.FloorTurn
                ? playerOrder[floorOperatorIndex]
                : PlayerId.Empty;

        internal bool ContainsPlayer(PlayerId playerId) => playerOrder.Contains(playerId);

        public TrapFloorCurrentFloorPlayerState GetPlayerState(PlayerId playerId)
        {
            if (!playerOrder.Contains(playerId))
                throw new ArgumentException("Player is not part of this Trap Floor session.", nameof(playerId));
            return eliminatedPlayerIds.Contains(playerId)
                ? TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound
                : TrapFloorCurrentFloorPlayerState.Active;
        }

        internal PlayerId[] CopyEliminatedPlayerIds()
        {
            List<PlayerId> eliminated = new List<PlayerId>();
            for (int i = 0; i < playerOrder.Count; i++)
            {
                if (eliminatedPlayerIds.Contains(playerOrder[i])) eliminated.Add(playerOrder[i]);
            }
            return eliminated.ToArray();
        }

        internal TrapFloorTurnPosition CapturePosition() =>
            new TrapFloorTurnPosition(
                CurrentRound,
                Phase,
                activePlayerIndex,
                floorOperatorIndex,
                IsCurrentFloorFailed,
                floorResumeIndex);

        /// <summary>
        /// IMP-1 (owner, 2026-10-22, Impossible: "once a player exits the turn order is floor, player, floor, player"):
        /// while true, a floor turn follows every player turn, and the next player plays after it in the same round.
        /// The table sets it from the mode and the escapes before each advance (it is derived, so Undo needs nothing).
        /// </summary>
        public bool FloorTurnAfterEachPlayer { get; set; }

        /// <summary>IMP-1: a floor turn between two player turns (not the one that ends the round).</summary>
        public bool IsFloorTurnBetweenPlayers =>
            !IsCurrentFloorFailed && Phase == TrapFloorTurnPhase.FloorTurn && floorResumeIndex >= 0;

        internal void Advance()
        {
            if (Phase == TrapFloorTurnPhase.PlayerTurn)
            {
                if (IsCurrentFloorFailed)
                    throw new InvalidOperationException("A failed Trap Floor round must be completed from Floor Turn.");

                int nextPlayerIndex = FindNextActivePlayerIndex(activePlayerIndex + 1, false);
                if (nextPlayerIndex >= 0 && FloorTurnAfterEachPlayer)
                {
                    // IMP-1: a floor turn between players; the next player still in plays after it (same round). Any
                    // player rolls, so the operator is simply a player still in.
                    Phase = TrapFloorTurnPhase.FloorTurn;
                    activePlayerIndex = -1;
                    floorOperatorIndex = nextPlayerIndex;
                    floorResumeIndex = nextPlayerIndex;
                    return;
                }

                if (nextPlayerIndex >= 0)
                {
                    activePlayerIndex = nextPlayerIndex;
                    return;
                }

                Phase = TrapFloorTurnPhase.FloorTurn;
                activePlayerIndex = -1;
                floorOperatorIndex = FindNextActivePlayerIndex((CurrentRound - 1) % playerOrder.Count, true);
                floorResumeIndex = -1;
                return;
            }

            if (!IsCurrentFloorFailed && floorResumeIndex >= 0)
            {
                // IMP-1: after a floor turn between players, the next player still in plays (same round).
                int resumeIndex = FindNextActivePlayerIndex(floorResumeIndex, false);
                floorResumeIndex = -1;
                if (resumeIndex >= 0)
                {
                    Phase = TrapFloorTurnPhase.PlayerTurn;
                    activePlayerIndex = resumeIndex;
                    floorOperatorIndex = -1;
                    return;
                }
            }

            CurrentRound = checked(CurrentRound + 1);
            // Owner, 2026-10-21: a knocked-out player stays out for the rest of the floor; only a failed floor
            // (everyone out) brings everyone back.
            if (IsCurrentFloorFailed) eliminatedPlayerIds.Clear();
            IsCurrentFloorFailed = false;
            Phase = TrapFloorTurnPhase.PlayerTurn;
            activePlayerIndex = FindNextActivePlayerIndex(0, false);
            floorOperatorIndex = -1;
            floorResumeIndex = -1;
        }

        internal PlayerId MarkPlayerEliminatedForCurrentRound(PlayerId playerId)
        {
            int playerIndex = playerOrder.IndexOf(playerId);
            if (playerIndex < 0)
                throw new ArgumentException("Player is not part of this Trap Floor session.", nameof(playerId));
            if (IsCurrentFloorFailed)
                throw new InvalidOperationException("Every Player is already eliminated for the current round.");

            if (!eliminatedPlayerIds.Add(playerId))
                throw new InvalidOperationException("The Player is already eliminated for the current round.");

            if (eliminatedPlayerIds.Count == playerOrder.Count)
            {
                IsCurrentFloorFailed = true;
                Phase = TrapFloorTurnPhase.FloorTurn;
                activePlayerIndex = -1;
                floorOperatorIndex = -1;
                floorResumeIndex = -1;
                return playerId;
            }

            if (Phase == TrapFloorTurnPhase.PlayerTurn && playerIndex == activePlayerIndex)
            {
                Advance();
            }
            else if (Phase == TrapFloorTurnPhase.FloorTurn && playerIndex == floorOperatorIndex)
            {
                floorOperatorIndex = FindNextActivePlayerIndex(playerIndex + 1, true);
            }

            return playerId;
        }

        internal void RollBackElimination(PlayerId playerId, TrapFloorTurnPosition previousPosition)
        {
            if (!eliminatedPlayerIds.Remove(playerId))
                throw new InvalidOperationException("Cannot roll back a Player who was not eliminated.");
            RestorePosition(previousPosition);
        }

        internal void RestorePosition(TrapFloorTurnPosition position)
        {
            CurrentRound = position.Round;
            Phase = position.Phase;
            activePlayerIndex = position.ActivePlayerIndex;
            floorOperatorIndex = position.FloorOperatorIndex;
            IsCurrentFloorFailed = position.IsCurrentFloorFailed;
            floorResumeIndex = position.FloorResumeIndex;
        }

        private int FindNextActivePlayerIndex(int startingIndex, bool wrap)
        {
            int checkedCount = wrap ? playerOrder.Count : playerOrder.Count - startingIndex;
            for (int offset = 0; offset < checkedCount; offset++)
            {
                int index = wrap
                    ? (startingIndex + offset) % playerOrder.Count
                    : startingIndex + offset;
                if (!eliminatedPlayerIds.Contains(playerOrder[index])) return index;
            }
            return -1;
        }
    }

    internal readonly struct TrapFloorTurnPosition
    {
        public TrapFloorTurnPosition(
            int round,
            TrapFloorTurnPhase phase,
            int activePlayerIndex,
            int floorOperatorIndex,
            bool isCurrentFloorFailed,
            int floorResumeIndex = -1)
        {
            Round = round;
            Phase = phase;
            ActivePlayerIndex = activePlayerIndex;
            FloorOperatorIndex = floorOperatorIndex;
            IsCurrentFloorFailed = isCurrentFloorFailed;
            FloorResumeIndex = floorResumeIndex;
        }

        public int Round { get; }
        public TrapFloorTurnPhase Phase { get; }
        public int ActivePlayerIndex { get; }
        public int FloorOperatorIndex { get; }
        public bool IsCurrentFloorFailed { get; }
        public int FloorResumeIndex { get; }
    }

    public enum TrapFloorTurnAdvanceError
    {
        None = 0,
        MatchMismatch = 1,
        RevisionConflict = 2,
        ActorIsNotActivePlayer = 3,
        ActorIsNotFloorOperator = 4,
        PlayerSetupMissing = 5,
        ControllerDrawRejected = 6,
        ActivityFeedMismatch = 7,
        CurrentFloorFailed = 8,
        PlayerIsNotParticipant = 9,
        PlayerAlreadyEliminatedForCurrentRound = 10,
    }

    public readonly struct TrapFloorTurnAdvanceResult
    {
        private TrapFloorTurnAdvanceResult(
            bool succeeded,
            long revision,
            int drawnCount,
            TrapFloorTurnAdvanceError error,
            ControllerInputHandDrawError drawError)
        {
            Succeeded = succeeded;
            Revision = revision;
            DrawnCount = drawnCount;
            Error = error;
            DrawError = drawError;
        }

        public bool Succeeded { get; }
        public long Revision { get; }
        public int DrawnCount { get; }
        public TrapFloorTurnAdvanceError Error { get; }
        public ControllerInputHandDrawError DrawError { get; }

        internal static TrapFloorTurnAdvanceResult Accepted(long revision, int drawnCount) =>
            new TrapFloorTurnAdvanceResult(true, revision, drawnCount, TrapFloorTurnAdvanceError.None, ControllerInputHandDrawError.None);

        internal static TrapFloorTurnAdvanceResult Failure(
            TrapFloorTurnAdvanceError error,
            ControllerInputHandDrawError drawError = ControllerInputHandDrawError.None) =>
            new TrapFloorTurnAdvanceResult(false, -1, 0, error, drawError);
    }

    /// <summary>
    /// Advances only the optional Trap Floor turn helper. It never draws or moves tabletop objects.
    /// </summary>
    public sealed class TrapFloorTurnService
    {
        private readonly TrapFloorTemplateDefinition template;
        private readonly TrapFloorTurnState state;
        private readonly ControllerInputHandService handService;
        private readonly TrapFloorActivityFeedState activityFeed;
        private readonly TrapFloorAbilityResolutionState abilityResolutionState;

        public TrapFloorTurnService(
            TrapFloorTemplateDefinition template,
            TrapFloorTurnState state,
            ControllerInputHandService handService,
            TrapFloorActivityFeedState activityFeed)
            : this(template, state, handService, activityFeed, null)
        {
        }

        public TrapFloorTurnService(
            TrapFloorTemplateDefinition template,
            TrapFloorTurnState state,
            ControllerInputHandService handService,
            TrapFloorActivityFeedState activityFeed,
            TrapFloorAbilityResolutionState abilityResolutionState)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.handService = handService ?? throw new ArgumentNullException(nameof(handService));
            this.activityFeed = activityFeed ?? throw new ArgumentNullException(nameof(activityFeed));
            this.abilityResolutionState = abilityResolutionState;
            if (abilityResolutionState != null && abilityResolutionState.MatchId != state.MatchId)
                throw new ArgumentException("Trap Floor temporary status state must belong to the same Match.", nameof(abilityResolutionState));
        }

        public ControllerInputHandDrawResult DrawForCurrentPlayer(
            MatchState matchState,
            CommandContext context)
        {
            if (matchState == null || context.MatchId != matchState.Id || state.MatchId != matchState.Id)
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.MatchMismatch);
            if (state.Phase != TrapFloorTurnPhase.PlayerTurn
                || context.RequestedByPlayerId != state.ActivePlayerId
                || !TryGetPlayerSetup(matchState, state.ActivePlayerId, out TrapFloorPlayerSetupDefinition player))
            {
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.ActorDoesNotOwnSeat);
            }

            return handService.DrawUpToConfiguredHandLimit(
                matchState,
                template.GameDefinition,
                new DrawUpToConfiguredHandLimitCommand(context, player.SeatId, player.ControllerDeckId));
        }

        public TrapFloorTurnAdvanceResult Advance(MatchState matchState, CommandContext context)
        {
            return Advance(matchState, context, false);
        }

        public TrapFloorTurnAdvanceResult Skip(MatchState matchState, CommandContext context)
        {
            if (state.Phase != TrapFloorTurnPhase.PlayerTurn)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActorIsNotActivePlayer);
            return Advance(matchState, context, true);
        }

        /// <summary>
        /// Authoritative boundary for Trap resolution to mark its affected Player eliminated.
        /// Trap failure conditions remain outside this service until they are explicitly authored.
        /// </summary>
        public TrapFloorTurnAdvanceResult MarkPlayerEliminatedForCurrentRound(
            MatchState matchState,
            CommandContext context,
            PlayerId affectedPlayerId)
        {
            return MarkPlayerEliminatedForCurrentRound(
                matchState,
                context,
                affectedPlayerId,
                null);
        }

        internal TrapFloorTurnAdvanceResult MarkPlayerEliminatedForCurrentRound(
            MatchState matchState,
            CommandContext context,
            PlayerId affectedPlayerId,
            TrapFloorTrapResolutionRecord trap)
        {
            TrapFloorTurnAdvanceResult validation = ValidateSessionOperation(matchState, context);
            if (!validation.Succeeded) return validation;
            if (!state.ContainsPlayer(affectedPlayerId))
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.PlayerIsNotParticipant);
            if (state.GetPlayerState(affectedPlayerId)
                == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                return TrapFloorTurnAdvanceResult.Failure(
                    TrapFloorTurnAdvanceError.PlayerAlreadyEliminatedForCurrentRound);

            long acceptedRevision = checked(matchState.Revision + 1L);
            state.MarkPlayerEliminatedForCurrentRound(affectedPlayerId);
            if (trap == null)
            {
                activityFeed.RecordPlayerEliminated(
                    acceptedRevision,
                    affectedPlayerId);
            }
            else
            {
                activityFeed.RecordPlayerEliminated(
                    acceptedRevision,
                    affectedPlayerId,
                    trap);
            }
            if (state.IsCurrentFloorFailed)
                activityFeed.RecordAllPlayersEliminated(acceptedRevision, affectedPlayerId);

            long revision = matchState.AdvanceRevision(
                context.Id,
                context.RequestedByPlayerId,
                AuthoritativeActionKind.Unspecified);
            return TrapFloorTurnAdvanceResult.Accepted(revision, 0);
        }

        private TrapFloorTurnAdvanceResult Advance(
            MatchState matchState,
            CommandContext context,
            bool recordSkip)
        {
            TrapFloorTurnAdvanceResult validation = ValidateTurnActor(matchState, context);
            if (!validation.Succeeded) return validation;

            if (recordSkip)
            {
                activityFeed.RecordSkippedTurn(
                    checked(matchState.Revision + 1L),
                    context.RequestedByPlayerId);
            }

            bool startsNextRound = state.Phase == TrapFloorTurnPhase.FloorTurn && !state.IsFloorTurnBetweenPlayers;
            bool reactivatesPlayers = startsNextRound && state.IsCurrentFloorFailed;
            state.Advance();
            if (startsNextRound)
            {
                abilityResolutionState?.ExpireBlindBeforeRound(state.CurrentRound);
                abilityResolutionState?.ExpireSlowBeforeRound(state.CurrentRound);
                abilityResolutionState?.ExpireStickyBeforeRound(state.CurrentRound);
            }
            if (reactivatesPlayers)
            {
                activityFeed.RecordPlayersReactivated(
                    checked(matchState.Revision + 1L),
                    context.RequestedByPlayerId);
            }

            long revision = matchState.AdvanceRevision(
                context.Id,
                context.RequestedByPlayerId,
                AuthoritativeActionKind.Unspecified);
            return TrapFloorTurnAdvanceResult.Accepted(revision, 0);
        }

        private TrapFloorTurnAdvanceResult ValidateTurnActor(
            MatchState matchState,
            CommandContext context)
        {
            TrapFloorTurnAdvanceResult validation = ValidateSessionOperation(matchState, context);
            if (!validation.Succeeded) return validation;
            if (state.IsCurrentFloorFailed)
            {
                return state.Phase == TrapFloorTurnPhase.FloorTurn
                    && state.ContainsPlayer(context.RequestedByPlayerId)
                        ? TrapFloorTurnAdvanceResult.Accepted(-1, 0)
                        : TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.CurrentFloorFailed);
            }
            if (state.Phase == TrapFloorTurnPhase.PlayerTurn
                && context.RequestedByPlayerId != state.ActivePlayerId)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActorIsNotActivePlayer);
            if (state.Phase == TrapFloorTurnPhase.FloorTurn
                && context.RequestedByPlayerId != state.FloorOperatorPlayerId)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActorIsNotFloorOperator);
            return TrapFloorTurnAdvanceResult.Accepted(-1, 0);
        }

        private TrapFloorTurnAdvanceResult ValidateSessionOperation(
            MatchState matchState,
            CommandContext context)
        {
            if (matchState == null || context.MatchId != matchState.Id || state.MatchId != matchState.Id)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.MatchMismatch);
            if (activityFeed.MatchId != matchState.Id)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActivityFeedMismatch);
            if (context.ExpectedRevision.HasValue && context.ExpectedRevision.Value != matchState.Revision)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.RevisionConflict);
            return TrapFloorTurnAdvanceResult.Accepted(-1, 0);
        }

        private bool TryGetPlayerSetup(
            MatchState matchState,
            PlayerId playerId,
            out TrapFloorPlayerSetupDefinition player)
        {
            for (int i = 0; i < template.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition candidate = template.Players[i];
                if (matchState.Seats.TryGetValue(candidate.SeatId, out var seat)
                    && seat.OccupantPlayerId == playerId)
                {
                    player = candidate;
                    return true;
                }
            }

            player = null;
            return false;
        }
    }
}
