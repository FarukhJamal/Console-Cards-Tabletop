using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Games.TrapFloor
{
    public sealed class TrapFloorCollapsedFloorState
    {
        internal TrapFloorCollapsedFloorState(
            TabletopObjectId floorCardId,
            TrapFloorCoordinate coordinate,
            PlayerId collapsedByPlayerId,
            int xAxisResult,
            int yAxisResult,
            long acceptedRevision)
        {
            FloorCardId = floorCardId;
            Coordinate = coordinate;
            CollapsedByPlayerId = collapsedByPlayerId;
            XAxisResult = xAxisResult;
            YAxisResult = yAxisResult;
            AcceptedRevision = acceptedRevision;
        }

        public TabletopObjectId FloorCardId { get; }
        public TrapFloorCoordinate Coordinate { get; }
        public PlayerId CollapsedByPlayerId { get; }
        public int XAxisResult { get; }
        public int YAxisResult { get; }
        public long AcceptedRevision { get; }
    }

    public sealed class TrapFloorCollapseRollState
    {
        internal TrapFloorCollapseRollState(
            int attemptNumber,
            int xAxisResult,
            int yAxisResult,
            TrapFloorCoordinate coordinate,
            TabletopObjectId floorCardId,
            bool requiredReroll)
        {
            AttemptNumber = attemptNumber;
            XAxisResult = xAxisResult;
            YAxisResult = yAxisResult;
            Coordinate = coordinate;
            FloorCardId = floorCardId;
            RequiredReroll = requiredReroll;
        }

        public int AttemptNumber { get; }
        public int XAxisResult { get; }
        public int YAxisResult { get; }
        public TrapFloorCoordinate Coordinate { get; }
        public TabletopObjectId FloorCardId { get; }
        public bool RequiredReroll { get; }
    }

    /// <summary>
    /// Match-scoped, engine-independent state for permanent Stage-03 Floor holes and the one
    /// in-flight assisted physical-dice operation. Floor identities remain in Match State.
    /// </summary>
    public sealed class TrapFloorCollapseState
    {
        private readonly List<TrapFloorCollapsedFloorState> collapsedFloors =
            new List<TrapFloorCollapsedFloorState>();
        private readonly ReadOnlyCollection<TrapFloorCollapsedFloorState> readOnlyCollapsedFloors;
        private readonly Dictionary<TabletopObjectId, TrapFloorCollapsedFloorState> collapsedById =
            new Dictionary<TabletopObjectId, TrapFloorCollapsedFloorState>();

        public TrapFloorCollapseState(MatchId matchId, int totalFloorCount)
        {
            if (matchId.IsEmpty) throw new ArgumentException("Collapse Match ID cannot be empty.", nameof(matchId));
            if (totalFloorCount < 1) throw new ArgumentOutOfRangeException(nameof(totalFloorCount));
            MatchId = matchId;
            TotalFloorCount = totalFloorCount;
            readOnlyCollapsedFloors = collapsedFloors.AsReadOnly();
        }

        public MatchId MatchId { get; }
        public int TotalFloorCount { get; }
        public IReadOnlyList<TrapFloorCollapsedFloorState> CollapsedFloors => readOnlyCollapsedFloors;
        public int CollapsedFloorCount => collapsedFloors.Count;
        public int UsableFloorCount => TotalFloorCount - CollapsedFloorCount;
        public bool IsBoardExhausted => UsableFloorCount == 0;
        public bool IsCollapsePending { get; private set; }
        public PlayerId PendingActorPlayerId { get; private set; }
        public TrapFloorCollapseRollState LastRoll { get; private set; }

        public PhysicalObjectState LastResolvedXAxisPhysicalState { get; private set; }
        public PhysicalObjectState LastResolvedYAxisPhysicalState { get; private set; }

        public bool IsCollapsed(TabletopObjectId floorCardId)
        {
            return collapsedById.ContainsKey(floorCardId);
        }

        public bool TryGetCollapsedFloor(
            TabletopObjectId floorCardId,
            out TrapFloorCollapsedFloorState collapsedFloor)
        {
            return collapsedById.TryGetValue(floorCardId, out collapsedFloor);
        }

        internal void Begin(
            PlayerId actorPlayerId,
            PhysicalObjectState xAxisLaunchState,
            PhysicalObjectState yAxisLaunchState)
        {
            IsCollapsePending = true;
            PendingActorPlayerId = actorPlayerId;
            LastResolvedXAxisPhysicalState = xAxisLaunchState;
            LastResolvedYAxisPhysicalState = yAxisLaunchState;
            LastRoll = null;
        }

        internal TrapFloorCollapseRollState RecordRoll(
            DieState xAxisDie,
            DieState yAxisDie,
            TrapFloorCoordinate coordinate,
            TabletopObjectId floorCardId,
            bool requiredReroll)
        {
            LastResolvedXAxisPhysicalState = xAxisDie.BaseState.PhysicalState;
            LastResolvedYAxisPhysicalState = yAxisDie.BaseState.PhysicalState;
            LastRoll = new TrapFloorCollapseRollState(
                (LastRoll?.AttemptNumber ?? 0) + 1,
                xAxisDie.CurrentValue,
                yAxisDie.CurrentValue,
                coordinate,
                floorCardId,
                requiredReroll);
            return LastRoll;
        }

        internal TrapFloorCollapsedFloorState RecordCollapse(
            TabletopObjectId floorCardId,
            TrapFloorCoordinate coordinate,
            PlayerId actorPlayerId,
            int xAxisResult,
            int yAxisResult,
            long acceptedRevision)
        {
            TrapFloorCollapsedFloorState collapsed = new TrapFloorCollapsedFloorState(
                floorCardId,
                coordinate,
                actorPlayerId,
                xAxisResult,
                yAxisResult,
                acceptedRevision);
            collapsedFloors.Add(collapsed);
            collapsedById.Add(floorCardId, collapsed);
            IsCollapsePending = false;
            PendingActorPlayerId = PlayerId.Empty;
            return collapsed;
        }

        public void Clear()
        {
            collapsedFloors.Clear();
            collapsedById.Clear();
            IsCollapsePending = false;
            PendingActorPlayerId = PlayerId.Empty;
            LastRoll = null;
            LastResolvedXAxisPhysicalState = null;
            LastResolvedYAxisPhysicalState = null;
        }
    }

    public sealed class TrapFloorBeginCollapseCommand : ITabletopCommand
    {
        public TrapFloorBeginCollapseCommand(CommandContext context) { Context = context; }
        public CommandContext Context { get; }
    }

    public sealed class TrapFloorResolveCollapseCommand : ITabletopCommand
    {
        public TrapFloorResolveCollapseCommand(CommandContext context) { Context = context; }
        public CommandContext Context { get; }
    }

    public enum TrapFloorCollapseError
    {
        None,
        MatchRequired,
        CommandRequired,
        MatchIdMismatch,
        MatchRevisionConflict,
        MatchTemplateMismatch,
        CollapseStateMismatch,
        ActivityFeedMismatch,
        ActorNotParticipating,
        CollapseAlreadyPending,
        CollapseNotPending,
        PendingActorMismatch,
        BoardExhausted,
        OfficialDiceMissing,
        OfficialDiceUnavailable,
        DiceNotSettled,
        DiceResultAlreadyHandled,
        FloorMappingMissing,
        RevisionOverflow,
    }

    public readonly struct TrapFloorCollapseResult
    {
        private TrapFloorCollapseResult(
            CommandResult commandResult,
            TrapFloorCollapseError error,
            TrapFloorCollapseRollState roll,
            TrapFloorCollapsedFloorState collapsedFloor,
            bool rerollRequired,
            bool boardExhausted)
        {
            CommandResult = commandResult;
            Error = error;
            Roll = roll;
            CollapsedFloor = collapsedFloor;
            RerollRequired = rerollRequired;
            BoardExhausted = boardExhausted;
        }

        public CommandResult CommandResult { get; }
        public TrapFloorCollapseError Error { get; }
        public bool Succeeded => CommandResult.Succeeded;
        public long Revision => CommandResult.Revision;
        public TrapFloorCollapseRollState Roll { get; }
        public TrapFloorCollapsedFloorState CollapsedFloor { get; }
        public bool RerollRequired { get; }
        public bool BoardExhausted { get; }

        internal static TrapFloorCollapseResult Accepted(
            long revision,
            TrapFloorCollapseRollState roll = null,
            TrapFloorCollapsedFloorState collapsedFloor = null,
            bool rerollRequired = false,
            bool boardExhausted = false)
        {
            return new TrapFloorCollapseResult(
                CommandResult.Accepted(revision),
                TrapFloorCollapseError.None,
                roll,
                collapsedFloor,
                rerollRequired,
                boardExhausted);
        }

        internal static TrapFloorCollapseResult Failure(
            CommandResultStatus status,
            TrapFloorCollapseError error,
            bool boardExhausted = false)
        {
            return new TrapFloorCollapseResult(
                CommandResult.Failure(status), error, null, null, false, boardExhausted);
        }
    }

    /// <summary>
    /// Validates manual Collapse requests and accepts only settled values from the Template's two
    /// official generic physical d6. Unity launches and simulates the Dice; this use case owns the
    /// permanent Floor mutation and Match revision.
    /// </summary>
    public sealed class TrapFloorCollapseUseCase
    {
        private readonly TrapFloorTemplateDefinition template;
        private readonly TrapFloorCollapseState state;
        private readonly TrapFloorActivityFeedState activityFeed;

        public TrapFloorCollapseUseCase(
            TrapFloorTemplateDefinition template,
            TrapFloorCollapseState state,
            TrapFloorActivityFeedState activityFeed)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.activityFeed = activityFeed ?? throw new ArgumentNullException(nameof(activityFeed));
        }

        public TrapFloorCollapseResult Begin(
            MatchState matchState,
            TrapFloorBeginCollapseCommand command)
        {
            TrapFloorCollapseError error = Validate(matchState, command, command?.Context ?? default);
            if (error != TrapFloorCollapseError.None) return Failure(error);
            if (state.IsBoardExhausted)
                return TrapFloorCollapseResult.Failure(
                    CommandResultStatus.Rejected, TrapFloorCollapseError.BoardExhausted, true);
            if (state.IsCollapsePending) return Failure(TrapFloorCollapseError.CollapseAlreadyPending);
            if (!TryGetOfficialDice(matchState, out DieState xAxisDie, out DieState yAxisDie))
                return Failure(TrapFloorCollapseError.OfficialDiceMissing);
            if (!CanUseLaunchedDie(xAxisDie) || !CanUseLaunchedDie(yAxisDie))
                return Failure(TrapFloorCollapseError.OfficialDiceUnavailable);
            if (matchState.Revision == long.MaxValue) return Failure(TrapFloorCollapseError.RevisionOverflow);

            long revision = matchState.AdvanceRevision();
            state.Begin(
                command.Context.RequestedByPlayerId,
                xAxisDie.BaseState.PhysicalState,
                yAxisDie.BaseState.PhysicalState);
            activityFeed.RecordFloorfallTriggered(
                revision,
                command.Context.RequestedByPlayerId,
                template.FloorfallXAxisDieId,
                template.FloorfallYAxisDieId);
            return TrapFloorCollapseResult.Accepted(revision);
        }

        public TrapFloorCollapseResult ResolveSettled(
            MatchState matchState,
            TrapFloorResolveCollapseCommand command)
        {
            TrapFloorCollapseError error = Validate(matchState, command, command?.Context ?? default);
            if (error != TrapFloorCollapseError.None) return Failure(error);
            if (!state.IsCollapsePending) return Failure(TrapFloorCollapseError.CollapseNotPending);
            if (state.PendingActorPlayerId != command.Context.RequestedByPlayerId)
                return Failure(TrapFloorCollapseError.PendingActorMismatch);
            if (!TryGetOfficialDice(matchState, out DieState xAxisDie, out DieState yAxisDie))
                return Failure(TrapFloorCollapseError.OfficialDiceMissing);
            if (!IsSettled(xAxisDie) || !IsSettled(yAxisDie))
                return Failure(TrapFloorCollapseError.DiceNotSettled);
            if (ReferenceEquals(xAxisDie.BaseState.PhysicalState, state.LastResolvedXAxisPhysicalState)
                && ReferenceEquals(yAxisDie.BaseState.PhysicalState, state.LastResolvedYAxisPhysicalState))
                return Failure(TrapFloorCollapseError.DiceResultAlreadyHandled);

            TrapFloorCoordinate coordinate = new TrapFloorCoordinate(
                xAxisDie.CurrentValue,
                yAxisDie.CurrentValue);
            if (!template.TryGetFloorCardId(coordinate, out TabletopObjectId floorCardId)
                || !template.TryGetFloorCardState(matchState, floorCardId, out TrapFloorFloorCardState floorCard))
                return Failure(TrapFloorCollapseError.FloorMappingMissing);
            if (matchState.Revision == long.MaxValue) return Failure(TrapFloorCollapseError.RevisionOverflow);

            bool rerollRequired = state.IsCollapsed(floorCardId);
            long revision = matchState.AdvanceRevision();
            TrapFloorCollapseRollState roll = state.RecordRoll(
                xAxisDie, yAxisDie, coordinate, floorCardId, rerollRequired);
            activityFeed.RecordFloorfallRoll(
                revision,
                command.Context.RequestedByPlayerId,
                floorCard,
                template.FloorfallXAxisDieId,
                template.FloorfallYAxisDieId,
                xAxisDie.CurrentValue,
                yAxisDie.CurrentValue);
            if (rerollRequired)
            {
                return TrapFloorCollapseResult.Accepted(revision, roll, rerollRequired: true);
            }

            TrapFloorCollapsedFloorState collapsed = state.RecordCollapse(
                floorCardId,
                coordinate,
                command.Context.RequestedByPlayerId,
                xAxisDie.CurrentValue,
                yAxisDie.CurrentValue,
                revision);
            activityFeed.RecordFloorCollapsed(
                revision,
                command.Context.RequestedByPlayerId,
                floorCard,
                template.FloorfallXAxisDieId,
                template.FloorfallYAxisDieId,
                xAxisDie.CurrentValue,
                yAxisDie.CurrentValue);
            return TrapFloorCollapseResult.Accepted(
                revision,
                roll,
                collapsed,
                boardExhausted: state.IsBoardExhausted);
        }

        private TrapFloorCollapseError Validate(
            MatchState matchState,
            object command,
            CommandContext context)
        {
            if (matchState == null) return TrapFloorCollapseError.MatchRequired;
            if (command == null) return TrapFloorCollapseError.CommandRequired;
            if (context.MatchId != matchState.Id) return TrapFloorCollapseError.MatchIdMismatch;
            if (context.ExpectedRevision.HasValue && context.ExpectedRevision.Value != matchState.Revision)
                return TrapFloorCollapseError.MatchRevisionConflict;
            if (matchState.GameTemplateId != template.Template.Id)
                return TrapFloorCollapseError.MatchTemplateMismatch;
            if (state.MatchId != matchState.Id
                || state.TotalFloorCount != template.FloorCardIds.Count)
                return TrapFloorCollapseError.CollapseStateMismatch;
            if (activityFeed.MatchId != matchState.Id)
                return TrapFloorCollapseError.ActivityFeedMismatch;
            return MatchContainsPlayer(matchState, context.RequestedByPlayerId)
                ? TrapFloorCollapseError.None
                : TrapFloorCollapseError.ActorNotParticipating;
        }

        private bool TryGetOfficialDice(
            MatchState matchState,
            out DieState xAxisDie,
            out DieState yAxisDie)
        {
            return matchState.Dice.TryGetValue(template.FloorfallXAxisDieId, out xAxisDie)
                && matchState.Dice.TryGetValue(template.FloorfallYAxisDieId, out yAxisDie)
                && xAxisDie.SideCount == TrapFloorFloorfallService.DieSideCount
                && yAxisDie.SideCount == TrapFloorFloorfallService.DieSideCount;
        }

        private static bool CanUseLaunchedDie(DieState die)
        {
            PhysicalObjectState physical = die.BaseState.PhysicalState;
            return physical != null && physical.Mode == PhysicalObjectMode.Dynamic;
        }

        private static bool IsSettled(DieState die)
        {
            return die.BaseState.PhysicalState?.Mode == PhysicalObjectMode.Sleeping;
        }

        private static bool MatchContainsPlayer(MatchState matchState, PlayerId playerId)
        {
            foreach (var seat in matchState.Seats.Values)
                if (seat.OccupantPlayerId == playerId) return true;
            return false;
        }

        private TrapFloorCollapseResult Failure(TrapFloorCollapseError error)
        {
            CommandResultStatus status;
            switch (error)
            {
                case TrapFloorCollapseError.MatchRequired:
                case TrapFloorCollapseError.CommandRequired:
                case TrapFloorCollapseError.MatchIdMismatch:
                    status = CommandResultStatus.Invalid;
                    break;
                case TrapFloorCollapseError.MatchRevisionConflict:
                case TrapFloorCollapseError.RevisionOverflow:
                    status = CommandResultStatus.Conflict;
                    break;
                default:
                    status = CommandResultStatus.Rejected;
                    break;
            }

            return TrapFloorCollapseResult.Failure(status, error, state?.IsBoardExhausted ?? false);
        }
    }
}
