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

    /// <summary>Match-scoped assistance state. It never restricts freeform tabletop actions.</summary>
    public sealed class TrapFloorTurnState
    {
        private readonly ReadOnlyCollection<PlayerId> playerOrder;
        private int activePlayerIndex;
        private int floorOperatorIndex;

        public TrapFloorTurnState(MatchId matchId, IEnumerable<PlayerId> participatingPlayerIds)
            : this(
                matchId,
                participatingPlayerIds,
                1,
                TrapFloorTurnPhase.PlayerTurn,
                0,
                -1)
        {
        }

        internal TrapFloorTurnState(
            MatchId matchId,
            IEnumerable<PlayerId> participatingPlayerIds,
            int currentRound,
            TrapFloorTurnPhase phase,
            int activePlayerIndex,
            int floorOperatorIndex)
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

            if (phase == TrapFloorTurnPhase.PlayerTurn
                && (activePlayerIndex < 0 || activePlayerIndex >= players.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(activePlayerIndex));
            }

            if (phase == TrapFloorTurnPhase.FloorTurn
                && (floorOperatorIndex < 0 || floorOperatorIndex >= players.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(floorOperatorIndex));
            }

            MatchId = matchId;
            playerOrder = new ReadOnlyCollection<PlayerId>(players);
            CurrentRound = currentRound;
            Phase = phase;
            this.activePlayerIndex = phase == TrapFloorTurnPhase.PlayerTurn ? activePlayerIndex : -1;
            this.floorOperatorIndex = phase == TrapFloorTurnPhase.FloorTurn ? floorOperatorIndex : -1;
        }

        public MatchId MatchId { get; }
        public int CurrentRound { get; private set; }
        public TrapFloorTurnPhase Phase { get; private set; }
        public IReadOnlyList<PlayerId> PlayerOrder => playerOrder;
        public PlayerId ActivePlayerId =>
            Phase == TrapFloorTurnPhase.PlayerTurn ? playerOrder[activePlayerIndex] : PlayerId.Empty;
        public PlayerId FloorOperatorPlayerId =>
            Phase == TrapFloorTurnPhase.FloorTurn ? playerOrder[floorOperatorIndex] : PlayerId.Empty;

        internal TrapFloorTurnPosition CapturePosition() =>
            new TrapFloorTurnPosition(CurrentRound, Phase, activePlayerIndex, floorOperatorIndex);

        internal void Advance()
        {
            if (Phase == TrapFloorTurnPhase.PlayerTurn)
            {
                if (activePlayerIndex + 1 < playerOrder.Count)
                {
                    activePlayerIndex++;
                    return;
                }

                Phase = TrapFloorTurnPhase.FloorTurn;
                activePlayerIndex = -1;
                floorOperatorIndex = (CurrentRound - 1) % playerOrder.Count;
                return;
            }

            CurrentRound = checked(CurrentRound + 1);
            Phase = TrapFloorTurnPhase.PlayerTurn;
            activePlayerIndex = 0;
            floorOperatorIndex = -1;
        }

        internal void RestorePosition(TrapFloorTurnPosition position)
        {
            CurrentRound = position.Round;
            Phase = position.Phase;
            activePlayerIndex = position.ActivePlayerIndex;
            floorOperatorIndex = position.FloorOperatorIndex;
        }
    }

    internal readonly struct TrapFloorTurnPosition
    {
        public TrapFloorTurnPosition(
            int round,
            TrapFloorTurnPhase phase,
            int activePlayerIndex,
            int floorOperatorIndex)
        {
            Round = round;
            Phase = phase;
            ActivePlayerIndex = activePlayerIndex;
            FloorOperatorIndex = floorOperatorIndex;
        }

        public int Round { get; }
        public TrapFloorTurnPhase Phase { get; }
        public int ActivePlayerIndex { get; }
        public int FloorOperatorIndex { get; }
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
    /// Advances only the optional Trap Floor turn helper. The next Player's existing configured
    /// hand draw is included in the same accepted history snapshot as the turn change.
    /// </summary>
    public sealed class TrapFloorTurnService
    {
        private readonly TrapFloorTemplateDefinition template;
        private readonly TrapFloorTurnState state;
        private readonly ControllerInputHandService handService;

        public TrapFloorTurnService(
            TrapFloorTemplateDefinition template,
            TrapFloorTurnState state,
            ControllerInputHandService handService)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.handService = handService ?? throw new ArgumentNullException(nameof(handService));
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
            if (matchState == null || context.MatchId != matchState.Id || state.MatchId != matchState.Id)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.MatchMismatch);
            if (context.ExpectedRevision.HasValue && context.ExpectedRevision.Value != matchState.Revision)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.RevisionConflict);
            if (state.Phase == TrapFloorTurnPhase.PlayerTurn
                && context.RequestedByPlayerId != state.ActivePlayerId)
            {
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActorIsNotActivePlayer);
            }
            if (state.Phase == TrapFloorTurnPhase.FloorTurn
                && context.RequestedByPlayerId != state.FloorOperatorPlayerId)
            {
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActorIsNotFloorOperator);
            }

            TrapFloorTurnPosition previous = state.CapturePosition();
            state.Advance();
            if (state.Phase == TrapFloorTurnPhase.PlayerTurn)
            {
                if (!TryGetPlayerSetup(matchState, state.ActivePlayerId, out TrapFloorPlayerSetupDefinition player))
                {
                    state.RestorePosition(previous);
                    return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.PlayerSetupMissing);
                }

                CommandContext drawContext = new CommandContext(
                    CommandId.New(),
                    matchState.Id,
                    state.ActivePlayerId,
                    matchState.Revision);
                ControllerInputHandDrawResult draw = handService.DrawUpToConfiguredHandLimit(
                    matchState,
                    template.GameDefinition,
                    new DrawUpToConfiguredHandLimitCommand(
                        drawContext,
                        player.SeatId,
                        player.ControllerDeckId));
                if (!draw.Succeeded)
                {
                    state.RestorePosition(previous);
                    return TrapFloorTurnAdvanceResult.Failure(
                        TrapFloorTurnAdvanceError.ControllerDrawRejected,
                        draw.Error);
                }

                if (draw.Changed)
                    return TrapFloorTurnAdvanceResult.Accepted(draw.Revision, draw.DrawnCount);
            }

            long revision = matchState.AdvanceRevision(
                context.Id,
                context.RequestedByPlayerId,
                AuthoritativeActionKind.Unspecified);
            return TrapFloorTurnAdvanceResult.Accepted(revision, 0);
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
