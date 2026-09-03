using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Games.TrapFloor
{
    public enum TrapFloorRoundPhase
    {
        Start = 0,
        Search,
        Trigger,
        Floorfall,
        End,
        Completed,
    }

    /// <summary>
    /// Trap Floor-owned Runtime State for the approved ten-round schedule.
    /// It records orchestration progress only; it does not model unresolved game effects.
    /// </summary>
    public sealed class TrapFloorRoundState
    {
        public const int FinalRoundNumber = 10;

        private readonly List<PlayerId> participatingPlayerIds;
        private readonly ReadOnlyCollection<PlayerId> readOnlyParticipatingPlayerIds;
        private readonly List<PlayerId> completedSearchTriggerPlayerIds = new List<PlayerId>();
        private readonly ReadOnlyCollection<PlayerId> readOnlyCompletedSearchTriggerPlayerIds;
        private readonly HashSet<PlayerId> completedSearchTriggerPlayers = new HashSet<PlayerId>();

        public TrapFloorRoundState(MatchId matchId, IEnumerable<PlayerId> participatingPlayerIds)
        {
            if (matchId.IsEmpty)
            {
                throw new ArgumentException("Trap Floor round state requires a Match ID.", nameof(matchId));
            }

            if (participatingPlayerIds == null)
            {
                throw new ArgumentNullException(nameof(participatingPlayerIds));
            }

            MatchId = matchId;
            this.participatingPlayerIds = new List<PlayerId>();
            HashSet<PlayerId> uniquePlayers = new HashSet<PlayerId>();
            foreach (PlayerId playerId in participatingPlayerIds)
            {
                if (playerId.IsEmpty)
                {
                    throw new ArgumentException("Participating Player IDs cannot be empty.", nameof(participatingPlayerIds));
                }

                if (!uniquePlayers.Add(playerId))
                {
                    throw new ArgumentException("Participating Player IDs must be unique.", nameof(participatingPlayerIds));
                }

                this.participatingPlayerIds.Add(playerId);
            }

            if (this.participatingPlayerIds.Count == 0)
            {
                throw new ArgumentException("Trap Floor round state requires at least one participating Player.", nameof(participatingPlayerIds));
            }

            readOnlyParticipatingPlayerIds = new ReadOnlyCollection<PlayerId>(this.participatingPlayerIds);
            readOnlyCompletedSearchTriggerPlayerIds =
                new ReadOnlyCollection<PlayerId>(completedSearchTriggerPlayerIds);
            CurrentRoundNumber = 1;
            Phase = TrapFloorRoundPhase.Start;
        }

        public MatchId MatchId { get; }

        public int CurrentRoundNumber { get; private set; }

        public TrapFloorRoundPhase Phase { get; private set; }

        public IReadOnlyList<PlayerId> ParticipatingPlayerIds => readOnlyParticipatingPlayerIds;

        public IReadOnlyList<PlayerId> CompletedSearchTriggerPlayerIds =>
            readOnlyCompletedSearchTriggerPlayerIds;

        public int CompletedSearchTriggerCount => completedSearchTriggerPlayers.Count;

        public PlayerId CurrentTriggerPlayerId { get; private set; }

        public bool HasCurrentTriggerPlayer => !CurrentTriggerPlayerId.IsEmpty;

        public int AcceptedFloorfallCount { get; private set; }

        public bool IsScheduleCompleted => Phase == TrapFloorRoundPhase.Completed;

        public bool HasCompletedSearchTrigger(PlayerId playerId)
        {
            return completedSearchTriggerPlayers.Contains(playerId);
        }

        internal void CompleteStart()
        {
            RequirePhase(TrapFloorRoundPhase.Start);
            Phase = TrapFloorRoundPhase.Search;
        }

        internal void BeginTrigger(PlayerId searchingPlayerId)
        {
            RequirePhase(TrapFloorRoundPhase.Search);
            if (!ContainsParticipatingPlayer(searchingPlayerId)
                || HasCompletedSearchTrigger(searchingPlayerId)
                || HasCurrentTriggerPlayer)
            {
                throw new InvalidOperationException("Trap Floor round state cannot begin this Player's Trigger.");
            }

            CurrentTriggerPlayerId = searchingPlayerId;
            Phase = TrapFloorRoundPhase.Trigger;
        }

        internal void CompleteTrigger(PlayerId searchingPlayerId)
        {
            RequirePhase(TrapFloorRoundPhase.Trigger);
            if (CurrentTriggerPlayerId != searchingPlayerId
                || !completedSearchTriggerPlayers.Add(searchingPlayerId))
            {
                throw new InvalidOperationException("Trap Floor round state cannot complete this Player's Trigger.");
            }

            completedSearchTriggerPlayerIds.Add(searchingPlayerId);
            CurrentTriggerPlayerId = PlayerId.Empty;
            Phase = completedSearchTriggerPlayers.Count == participatingPlayerIds.Count
                ? TrapFloorRoundPhase.Floorfall
                : TrapFloorRoundPhase.Search;
        }

        internal void RecordAcceptedFloorfall()
        {
            RequirePhase(TrapFloorRoundPhase.Floorfall);
            if (AcceptedFloorfallCount == int.MaxValue)
            {
                throw new InvalidOperationException("Trap Floor Floorfall count cannot advance.");
            }

            AcceptedFloorfallCount++;
        }

        internal void CompleteFloorfallPhase()
        {
            RequirePhase(TrapFloorRoundPhase.Floorfall);
            if (AcceptedFloorfallCount < 1)
            {
                throw new InvalidOperationException("At least one accepted Floorfall is required.");
            }

            Phase = TrapFloorRoundPhase.End;
        }

        internal void CompleteEnd()
        {
            RequirePhase(TrapFloorRoundPhase.End);
            if (CurrentRoundNumber == FinalRoundNumber)
            {
                Phase = TrapFloorRoundPhase.Completed;
                return;
            }

            CurrentRoundNumber++;
            completedSearchTriggerPlayers.Clear();
            completedSearchTriggerPlayerIds.Clear();
            CurrentTriggerPlayerId = PlayerId.Empty;
            AcceptedFloorfallCount = 0;
            Phase = TrapFloorRoundPhase.Start;
        }

        private bool ContainsParticipatingPlayer(PlayerId playerId)
        {
            for (int i = 0; i < participatingPlayerIds.Count; i++)
            {
                if (participatingPlayerIds[i] == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        private void RequirePhase(TrapFloorRoundPhase requiredPhase)
        {
            if (Phase != requiredPhase)
            {
                throw new InvalidOperationException(
                    $"Trap Floor phase must be {requiredPhase}, but is {Phase}.");
            }
        }
    }

    public enum TrapFloorRoundOrchestrationError
    {
        None = 0,
        RequestRequired,
        MatchIdMismatch,
        MatchRevisionConflict,
        MatchTemplateMismatch,
        RoundStateMismatch,
        ActorNotParticipating,
        PhaseMismatch,
        PlayerAlreadyCompletedSearchTrigger,
        PendingCardStateMismatch,
        FloorfallRequired,
        FloorfallCountOverflow,
        FloormasterLifecycleRejected,
        RevisionOverflow,
        PhysicalDiceMustSettleOrReroll,
    }

    public sealed class TrapFloorRoundActionRequest
    {
        public TrapFloorRoundActionRequest(CommandContext context)
        {
            Context = context;
        }

        public CommandContext Context { get; }
    }

    public readonly struct TrapFloorRoundActionResult
    {
        private TrapFloorRoundActionResult(
            CommandResult commandResult,
            TrapFloorRoundOrchestrationError error)
        {
            CommandResult = commandResult;
            Error = error;
        }

        public CommandResult CommandResult { get; }

        public TrapFloorRoundOrchestrationError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public long Revision => CommandResult.Revision;

        internal static TrapFloorRoundActionResult Accepted(long revision)
        {
            return new TrapFloorRoundActionResult(
                CommandResult.Accepted(revision),
                TrapFloorRoundOrchestrationError.None);
        }

        internal static TrapFloorRoundActionResult Failure(
            CommandResultStatus status,
            TrapFloorRoundOrchestrationError error)
        {
            return new TrapFloorRoundActionResult(CommandResult.Failure(status), error);
        }
    }

    public readonly struct TrapFloorRoundSearchResult
    {
        private TrapFloorRoundSearchResult(
            CommandResult commandResult,
            TrapFloorRoundOrchestrationError error,
            TrapFloorFloormasterLifecycleError lifecycleError,
            TrapFloorPendingFloormasterCard pendingCard,
            bool reshuffledDiscard)
        {
            CommandResult = commandResult;
            Error = error;
            LifecycleError = lifecycleError;
            PendingCard = pendingCard;
            ReshuffledDiscard = reshuffledDiscard;
        }

        public CommandResult CommandResult { get; }

        public TrapFloorRoundOrchestrationError Error { get; }

        public TrapFloorFloormasterLifecycleError LifecycleError { get; }

        public TrapFloorPendingFloormasterCard PendingCard { get; }

        public bool ReshuffledDiscard { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public long Revision => CommandResult.Revision;

        internal static TrapFloorRoundSearchResult FromLifecycle(
            TrapFloorFloormasterSearchResult lifecycleResult)
        {
            return lifecycleResult.Succeeded
                ? new TrapFloorRoundSearchResult(
                    lifecycleResult.CommandResult,
                    TrapFloorRoundOrchestrationError.None,
                    TrapFloorFloormasterLifecycleError.None,
                    lifecycleResult.PendingCard,
                    lifecycleResult.ReshuffledDiscard)
                : new TrapFloorRoundSearchResult(
                    lifecycleResult.CommandResult,
                    TrapFloorRoundOrchestrationError.FloormasterLifecycleRejected,
                    lifecycleResult.Error,
                    null,
                    false);
        }

        internal static TrapFloorRoundSearchResult Failure(
            CommandResultStatus status,
            TrapFloorRoundOrchestrationError error)
        {
            return new TrapFloorRoundSearchResult(
                CommandResult.Failure(status),
                error,
                TrapFloorFloormasterLifecycleError.None,
                null,
                false);
        }
    }

    public readonly struct TrapFloorRoundTriggerResult
    {
        private TrapFloorRoundTriggerResult(
            CommandResult commandResult,
            TrapFloorRoundOrchestrationError error,
            TrapFloorFloormasterLifecycleError lifecycleError,
            TabletopObjectId completedCardId)
        {
            CommandResult = commandResult;
            Error = error;
            LifecycleError = lifecycleError;
            CompletedCardId = completedCardId;
        }

        public CommandResult CommandResult { get; }

        public TrapFloorRoundOrchestrationError Error { get; }

        public TrapFloorFloormasterLifecycleError LifecycleError { get; }

        public TabletopObjectId CompletedCardId { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public long Revision => CommandResult.Revision;

        internal static TrapFloorRoundTriggerResult FromLifecycle(
            CompletePendingFloormasterCardResult lifecycleResult)
        {
            return lifecycleResult.Succeeded
                ? new TrapFloorRoundTriggerResult(
                    lifecycleResult.CommandResult,
                    TrapFloorRoundOrchestrationError.None,
                    TrapFloorFloormasterLifecycleError.None,
                    lifecycleResult.CompletedCardId)
                : new TrapFloorRoundTriggerResult(
                    lifecycleResult.CommandResult,
                    TrapFloorRoundOrchestrationError.FloormasterLifecycleRejected,
                    lifecycleResult.Error,
                    TabletopObjectId.Empty);
        }

        public static TrapFloorRoundTriggerResult Failure(
            CommandResultStatus status,
            TrapFloorRoundOrchestrationError error)
        {
            return new TrapFloorRoundTriggerResult(
                CommandResult.Failure(status),
                error,
                TrapFloorFloormasterLifecycleError.None,
                TabletopObjectId.Empty);
        }
    }

    public readonly struct TrapFloorRoundFloorfallResult
    {
        private TrapFloorRoundFloorfallResult(
            CommandResult commandResult,
            TrapFloorRoundOrchestrationError error,
            TrapFloorFloorfallTarget? target)
        {
            CommandResult = commandResult;
            Error = error;
            Target = target;
        }

        public CommandResult CommandResult { get; }

        public TrapFloorRoundOrchestrationError Error { get; }

        public TrapFloorFloorfallTarget? Target { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public long Revision => CommandResult.Revision;

        internal static TrapFloorRoundFloorfallResult Accepted(
            long revision,
            TrapFloorFloorfallTarget target)
        {
            return new TrapFloorRoundFloorfallResult(
                CommandResult.Accepted(revision),
                TrapFloorRoundOrchestrationError.None,
                target);
        }

        internal static TrapFloorRoundFloorfallResult Failure(
            CommandResultStatus status,
            TrapFloorRoundOrchestrationError error)
        {
            return new TrapFloorRoundFloorfallResult(CommandResult.Failure(status), error, null);
        }
    }

    /// <summary>
    /// Coordinates the approved Trap Floor phase loop while delegating Card and Floorfall mutations
    /// to their existing authoritative services.
    /// </summary>
    public sealed class TrapFloorRoundOrchestrationService
    {
        private readonly TrapFloorTemplateDefinition template;
        private readonly MatchState matchState;
        private readonly TrapFloorRoundState roundState;
        private readonly TrapFloorFloormasterLifecycleState lifecycleState;
        private readonly TrapFloorFloormasterLifecycleService lifecycleService;
        private readonly TrapFloorFloorfallService floorfallService;

        public TrapFloorRoundOrchestrationService(
            TrapFloorTemplateDefinition template,
            MatchState matchState,
            TrapFloorRoundState roundState,
            TrapFloorFloormasterLifecycleState lifecycleState,
            TrapFloorFloormasterLifecycleService lifecycleService,
            TrapFloorFloorfallService floorfallService)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            this.matchState = matchState ?? throw new ArgumentNullException(nameof(matchState));
            this.roundState = roundState ?? throw new ArgumentNullException(nameof(roundState));
            this.lifecycleState = lifecycleState ?? throw new ArgumentNullException(nameof(lifecycleState));
            this.lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
            this.floorfallService = floorfallService ?? throw new ArgumentNullException(nameof(floorfallService));

            if (roundState.MatchId != matchState.Id || lifecycleState.MatchId != matchState.Id)
            {
                throw new ArgumentException("Trap Floor orchestration state must belong to the supplied Match.");
            }

            if (matchState.GameTemplateId != template.Template.Id)
            {
                throw new ArgumentException("Trap Floor orchestration requires its Template-authored Match.");
            }

            if (lifecycleState.HasPendingCard)
            {
                throw new ArgumentException("Fresh Trap Floor round orchestration cannot begin with a pending Card.");
            }

            ValidateParticipatingPlayersOrThrow();
        }

        public TrapFloorRoundActionResult CompleteStart(TrapFloorRoundActionRequest request)
        {
            TrapFloorRoundActionResult validation = ValidateAction(request, TrapFloorRoundPhase.Start);
            if (!validation.Succeeded)
            {
                return validation;
            }

            if (lifecycleState.HasPendingCard || roundState.HasCurrentTriggerPlayer)
            {
                return ActionFailure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PendingCardStateMismatch);
            }

            roundState.CompleteStart();
            return TrapFloorRoundActionResult.Accepted(matchState.AdvanceRevision());
        }

        public TrapFloorRoundSearchResult Search(TrapFloorFloormasterSearchRequest request)
        {
            if (request == null)
            {
                return TrapFloorRoundSearchResult.Failure(
                    CommandResultStatus.Invalid,
                    TrapFloorRoundOrchestrationError.RequestRequired);
            }

            TrapFloorRoundOrchestrationError contextError = ValidateContext(
                request.Context,
                out CommandResultStatus contextStatus);
            if (contextError != TrapFloorRoundOrchestrationError.None)
            {
                return TrapFloorRoundSearchResult.Failure(contextStatus, contextError);
            }

            if (roundState.Phase != TrapFloorRoundPhase.Search)
            {
                return TrapFloorRoundSearchResult.Failure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PhaseMismatch);
            }

            PlayerId actorId = request.Context.RequestedByPlayerId;
            if (roundState.HasCompletedSearchTrigger(actorId))
            {
                return TrapFloorRoundSearchResult.Failure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PlayerAlreadyCompletedSearchTrigger);
            }

            if (roundState.HasCurrentTriggerPlayer || lifecycleState.HasPendingCard)
            {
                return TrapFloorRoundSearchResult.Failure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PendingCardStateMismatch);
            }

            TrapFloorFloormasterSearchResult lifecycleResult = lifecycleService.Search(
                matchState,
                roundState.ParticipatingPlayerIds,
                request);
            TrapFloorRoundSearchResult result = TrapFloorRoundSearchResult.FromLifecycle(lifecycleResult);
            if (result.Succeeded)
            {
                roundState.BeginTrigger(result.PendingCard.SearchingPlayerId);
            }

            return result;
        }

        public TrapFloorRoundTriggerResult CompleteTrigger(
            CompletePendingFloormasterCardRequest request)
        {
            if (request == null)
            {
                return TrapFloorRoundTriggerResult.Failure(
                    CommandResultStatus.Invalid,
                    TrapFloorRoundOrchestrationError.RequestRequired);
            }

            TrapFloorRoundOrchestrationError contextError = ValidateContext(
                request.Context,
                out CommandResultStatus contextStatus);
            if (contextError != TrapFloorRoundOrchestrationError.None)
            {
                return TrapFloorRoundTriggerResult.Failure(contextStatus, contextError);
            }

            if (roundState.Phase != TrapFloorRoundPhase.Trigger)
            {
                return TrapFloorRoundTriggerResult.Failure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PhaseMismatch);
            }

            TrapFloorPendingFloormasterCard pendingCard = lifecycleState.PendingCard;
            if (pendingCard == null
                || !roundState.HasCurrentTriggerPlayer
                || roundState.CurrentTriggerPlayerId != pendingCard.SearchingPlayerId)
            {
                return TrapFloorRoundTriggerResult.Failure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PendingCardStateMismatch);
            }

            CompletePendingFloormasterCardResult lifecycleResult = lifecycleService.CompleteResolvedCard(
                matchState,
                roundState.ParticipatingPlayerIds,
                request);
            TrapFloorRoundTriggerResult result = TrapFloorRoundTriggerResult.FromLifecycle(lifecycleResult);
            if (result.Succeeded)
            {
                roundState.CompleteTrigger(pendingCard.SearchingPlayerId);
            }

            return result;
        }

        public TrapFloorRoundFloorfallResult RollFloorfall(TrapFloorRoundActionRequest request)
        {
            TrapFloorRoundActionResult validation = ValidateAction(request, TrapFloorRoundPhase.Floorfall);
            if (!validation.Succeeded)
            {
                return TrapFloorRoundFloorfallResult.Failure(
                    validation.CommandResult.Status,
                    validation.Error);
            }

            if (lifecycleState.HasPendingCard || roundState.HasCurrentTriggerPlayer)
            {
                return TrapFloorRoundFloorfallResult.Failure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PendingCardStateMismatch);
            }

            if (roundState.AcceptedFloorfallCount == int.MaxValue)
            {
                return TrapFloorRoundFloorfallResult.Failure(
                    CommandResultStatus.Conflict,
                    TrapFloorRoundOrchestrationError.FloorfallCountOverflow);
            }

            if (floorfallService.UsesPhysicalDice && !floorfallService.CanResolvePhysicalDice(
                new TrapFloorFloorfallContext(roundState.CurrentRoundNumber)))
                return TrapFloorRoundFloorfallResult.Failure(CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PhysicalDiceMustSettleOrReroll);

            TrapFloorFloorfallTarget target = floorfallService.RollAndResolve(
                new TrapFloorFloorfallContext(roundState.CurrentRoundNumber));
            roundState.RecordAcceptedFloorfall();
            return TrapFloorRoundFloorfallResult.Accepted(matchState.Revision, target);
        }

        public TrapFloorRoundActionResult CompleteFloorfallPhase(TrapFloorRoundActionRequest request)
        {
            TrapFloorRoundActionResult validation = ValidateAction(request, TrapFloorRoundPhase.Floorfall);
            if (!validation.Succeeded)
            {
                return validation;
            }

            if (lifecycleState.HasPendingCard || roundState.HasCurrentTriggerPlayer)
            {
                return ActionFailure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PendingCardStateMismatch);
            }

            if (roundState.AcceptedFloorfallCount < 1)
            {
                return ActionFailure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.FloorfallRequired);
            }

            roundState.CompleteFloorfallPhase();
            return TrapFloorRoundActionResult.Accepted(matchState.AdvanceRevision());
        }

        public TrapFloorRoundActionResult CompleteEnd(TrapFloorRoundActionRequest request)
        {
            TrapFloorRoundActionResult validation = ValidateAction(request, TrapFloorRoundPhase.End);
            if (!validation.Succeeded)
            {
                return validation;
            }

            if (lifecycleState.HasPendingCard || roundState.HasCurrentTriggerPlayer)
            {
                return ActionFailure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PendingCardStateMismatch);
            }

            roundState.CompleteEnd();
            return TrapFloorRoundActionResult.Accepted(matchState.AdvanceRevision());
        }

        private TrapFloorRoundActionResult ValidateAction(
            TrapFloorRoundActionRequest request,
            TrapFloorRoundPhase requiredPhase)
        {
            if (request == null)
            {
                return ActionFailure(
                    CommandResultStatus.Invalid,
                    TrapFloorRoundOrchestrationError.RequestRequired);
            }

            TrapFloorRoundOrchestrationError contextError = ValidateContext(
                request.Context,
                out CommandResultStatus contextStatus);
            if (contextError != TrapFloorRoundOrchestrationError.None)
            {
                return ActionFailure(contextStatus, contextError);
            }

            if (roundState.Phase != requiredPhase)
            {
                return ActionFailure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PhaseMismatch);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return ActionFailure(
                    CommandResultStatus.Conflict,
                    TrapFloorRoundOrchestrationError.RevisionOverflow);
            }

            return TrapFloorRoundActionResult.Accepted(matchState.Revision);
        }

        private TrapFloorRoundOrchestrationError ValidateContext(
            CommandContext context,
            out CommandResultStatus status)
        {
            if (context.MatchId != matchState.Id)
            {
                status = CommandResultStatus.Invalid;
                return TrapFloorRoundOrchestrationError.MatchIdMismatch;
            }

            if (context.ExpectedRevision.HasValue && context.ExpectedRevision.Value != matchState.Revision)
            {
                status = CommandResultStatus.Conflict;
                return TrapFloorRoundOrchestrationError.MatchRevisionConflict;
            }

            if (matchState.GameTemplateId != template.Template.Id)
            {
                status = CommandResultStatus.Rejected;
                return TrapFloorRoundOrchestrationError.MatchTemplateMismatch;
            }

            if (roundState.MatchId != matchState.Id || lifecycleState.MatchId != matchState.Id)
            {
                status = CommandResultStatus.Rejected;
                return TrapFloorRoundOrchestrationError.RoundStateMismatch;
            }

            if (!ContainsPlayer(roundState.ParticipatingPlayerIds, context.RequestedByPlayerId)
                || !MatchContainsPlayer(context.RequestedByPlayerId))
            {
                status = CommandResultStatus.Rejected;
                return TrapFloorRoundOrchestrationError.ActorNotParticipating;
            }

            status = CommandResultStatus.Accepted;
            return TrapFloorRoundOrchestrationError.None;
        }

        private void ValidateParticipatingPlayersOrThrow()
        {
            for (int i = 0; i < roundState.ParticipatingPlayerIds.Count; i++)
            {
                if (!MatchContainsPlayer(roundState.ParticipatingPlayerIds[i]))
                {
                    throw new ArgumentException(
                        "Every Trap Floor round participant must occupy a Match Seat.",
                        nameof(roundState));
                }
            }
        }

        private bool MatchContainsPlayer(PlayerId playerId)
        {
            foreach (var seat in matchState.Seats.Values)
            {
                if (seat.OccupantPlayerId == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPlayer(IReadOnlyList<PlayerId> players, PlayerId playerId)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static TrapFloorRoundActionResult ActionFailure(
            CommandResultStatus status,
            TrapFloorRoundOrchestrationError error)
        {
            return TrapFloorRoundActionResult.Failure(status, error);
        }
    }
}
