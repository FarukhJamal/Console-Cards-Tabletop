using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Games.TrapFloor
{
    public sealed class TrapFloorCollectedKeyState
    {
        internal TrapFloorCollectedKeyState(
            TabletopObjectId floorCardId,
            ObjectDefinitionId contentDefinitionId,
            PlayerId claimedByPlayerId,
            long acceptedRevision)
        {
            FloorCardId = floorCardId;
            ContentDefinitionId = contentDefinitionId;
            ClaimedByPlayerId = claimedByPlayerId;
            AcceptedRevision = acceptedRevision;
        }

        public TabletopObjectId FloorCardId { get; }

        public ObjectDefinitionId ContentDefinitionId { get; }

        public PlayerId ClaimedByPlayerId { get; }

        public long AcceptedRevision { get; }
    }

    /// <summary>
    /// Match-scoped, engine-independent cooperative objective state. Each accepted Key keeps its
    /// authoritative Floor object identity, stable content identity, claiming actor, and revision.
    /// </summary>
    public sealed class TrapFloorObjectiveState
    {
        private readonly List<TrapFloorCollectedKeyState> collectedKeys =
            new List<TrapFloorCollectedKeyState>();
        private readonly ReadOnlyCollection<TrapFloorCollectedKeyState> readOnlyCollectedKeys;
        private readonly Dictionary<TabletopObjectId, TrapFloorCollectedKeyState> claimsByFloorCardId =
            new Dictionary<TabletopObjectId, TrapFloorCollectedKeyState>();

        public TrapFloorObjectiveState(MatchId matchId, int requiredKeyCount)
        {
            if (matchId.IsEmpty)
            {
                throw new ArgumentException("Objective Match ID cannot be empty.", nameof(matchId));
            }

            if (requiredKeyCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredKeyCount));
            }

            MatchId = matchId;
            RequiredKeyCount = requiredKeyCount;
            readOnlyCollectedKeys = collectedKeys.AsReadOnly();
        }

        public MatchId MatchId { get; }

        public int RequiredKeyCount { get; }

        public IReadOnlyList<TrapFloorCollectedKeyState> CollectedKeys => readOnlyCollectedKeys;

        public int CollectedKeyCount => collectedKeys.Count;

        public bool HasRequiredKeys => CollectedKeyCount >= RequiredKeyCount;

        public bool IsWon { get; private set; }

        public PlayerId WinningPlayerId { get; private set; }

        public TabletopObjectId ExitFloorCardId { get; private set; }

        public bool TryGetClaim(
            TabletopObjectId floorCardId,
            out TrapFloorCollectedKeyState claim)
        {
            return claimsByFloorCardId.TryGetValue(floorCardId, out claim);
        }

        internal TrapFloorCollectedKeyState RecordKeyClaim(
            TrapFloorFloorCardState floorCard,
            PlayerId actorPlayerId,
            long acceptedRevision)
        {
            TrapFloorCollectedKeyState claim = new TrapFloorCollectedKeyState(
                floorCard.ObjectId,
                floorCard.Content.Id,
                actorPlayerId,
                acceptedRevision);
            collectedKeys.Add(claim);
            claimsByFloorCardId.Add(claim.FloorCardId, claim);
            return claim;
        }

        internal void RecordVictory(
            TabletopObjectId exitFloorCardId,
            PlayerId actorPlayerId)
        {
            IsWon = true;
            WinningPlayerId = actorPlayerId;
            ExitFloorCardId = exitFloorCardId;
        }

        public void Clear()
        {
            collectedKeys.Clear();
            claimsByFloorCardId.Clear();
            IsWon = false;
            WinningPlayerId = PlayerId.Empty;
            ExitFloorCardId = TabletopObjectId.Empty;
        }
    }

    public sealed class TrapFloorClaimKeyCommand : ITabletopCommand
    {
        public TrapFloorClaimKeyCommand(CommandContext context, TabletopObjectId floorCardId)
        {
            if (floorCardId.IsEmpty)
            {
                throw new ArgumentException("Floor Card ID cannot be empty.", nameof(floorCardId));
            }

            Context = context;
            FloorCardId = floorCardId;
        }

        public CommandContext Context { get; }

        public TabletopObjectId FloorCardId { get; }
    }

    public sealed class TrapFloorAttemptEscapeCommand : ITabletopCommand
    {
        public TrapFloorAttemptEscapeCommand(CommandContext context, TabletopObjectId floorCardId)
        {
            if (floorCardId.IsEmpty)
            {
                throw new ArgumentException("Floor Card ID cannot be empty.", nameof(floorCardId));
            }

            Context = context;
            FloorCardId = floorCardId;
        }

        public CommandContext Context { get; }

        public TabletopObjectId FloorCardId { get; }
    }

    public enum TrapFloorObjectiveError
    {
        None,
        MatchRequired,
        CommandRequired,
        MatchIdMismatch,
        MatchRevisionConflict,
        MatchTemplateMismatch,
        ObjectiveStateMismatch,
        ActivityFeedMismatch,
        ActorNotParticipating,
        FloorCardMissingOrInvalid,
        FloorNotRevealed,
        FloorIsNotKey,
        FloorIsNotExit,
        KeyAlreadyClaimed,
        RequiredKeysMissing,
        GameAlreadyWon,
        RevisionOverflow,
    }

    public readonly struct TrapFloorObjectiveResult
    {
        private TrapFloorObjectiveResult(
            CommandResult commandResult,
            TrapFloorObjectiveError error,
            TrapFloorFloorCardState floorCard,
            TrapFloorCollectedKeyState claimedKey,
            TrapFloorActivityEntry activity,
            int collectedKeyCount,
            int requiredKeyCount,
            bool isWon)
        {
            CommandResult = commandResult;
            Error = error;
            FloorCard = floorCard;
            ClaimedKey = claimedKey;
            Activity = activity;
            CollectedKeyCount = collectedKeyCount;
            RequiredKeyCount = requiredKeyCount;
            IsWon = isWon;
        }

        public CommandResult CommandResult { get; }

        public TrapFloorObjectiveError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public long Revision => CommandResult.Revision;

        public TrapFloorFloorCardState FloorCard { get; }

        public TrapFloorCollectedKeyState ClaimedKey { get; }

        public TrapFloorActivityEntry Activity { get; }

        public int CollectedKeyCount { get; }

        public int RequiredKeyCount { get; }

        public bool IsWon { get; }

        internal static TrapFloorObjectiveResult Accepted(
            long revision,
            TrapFloorFloorCardState floorCard,
            TrapFloorCollectedKeyState claimedKey,
            TrapFloorActivityEntry activity,
            TrapFloorObjectiveState objectiveState)
        {
            return new TrapFloorObjectiveResult(
                CommandResult.Accepted(revision),
                TrapFloorObjectiveError.None,
                floorCard,
                claimedKey,
                activity,
                objectiveState.CollectedKeyCount,
                objectiveState.RequiredKeyCount,
                objectiveState.IsWon);
        }

        internal static TrapFloorObjectiveResult Failure(
            CommandResultStatus status,
            TrapFloorObjectiveError error,
            TrapFloorObjectiveState objectiveState)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Objective failure must use a non-Accepted status.", nameof(status));
            }

            if (error == TrapFloorObjectiveError.None)
            {
                throw new ArgumentException("Objective failure must include an error.", nameof(error));
            }

            return new TrapFloorObjectiveResult(
                CommandResult.Failure(status),
                error,
                null,
                null,
                null,
                objectiveState?.CollectedKeyCount ?? 0,
                objectiveState?.RequiredKeyCount ?? 0,
                objectiveState?.IsWon ?? false);
        }
    }

    public sealed class TrapFloorObjectiveUseCase
    {
        private readonly TrapFloorTemplateDefinition template;
        private readonly TrapFloorObjectiveState objectiveState;
        private readonly TrapFloorActivityFeedState activityFeed;

        public TrapFloorObjectiveUseCase(
            TrapFloorTemplateDefinition template,
            TrapFloorObjectiveState objectiveState,
            TrapFloorActivityFeedState activityFeed)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            this.objectiveState = objectiveState ?? throw new ArgumentNullException(nameof(objectiveState));
            this.activityFeed = activityFeed ?? throw new ArgumentNullException(nameof(activityFeed));
        }

        public TrapFloorObjectiveResult ClaimKey(
            MatchState matchState,
            TrapFloorClaimKeyCommand command)
        {
            TrapFloorObjectiveError validationError = ValidateCommand(
                matchState,
                command,
                command?.Context ?? default);
            if (validationError != TrapFloorObjectiveError.None)
            {
                return Failure(StatusFor(validationError), validationError);
            }

            if (!TryGetRevealedFloor(
                    matchState,
                    command.FloorCardId,
                    out TrapFloorFloorCardState floorCard,
                    out TrapFloorObjectiveError floorError))
            {
                return Failure(CommandResultStatus.Rejected, floorError);
            }

            if (floorCard.Content.Category != TrapFloorFloorContentCategory.Key)
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorObjectiveError.FloorIsNotKey);
            }

            if (objectiveState.TryGetClaim(floorCard.ObjectId, out _))
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorObjectiveError.KeyAlreadyClaimed);
            }

            if (objectiveState.IsWon)
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorObjectiveError.GameAlreadyWon);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return Failure(CommandResultStatus.Conflict, TrapFloorObjectiveError.RevisionOverflow);
            }

            long acceptedRevision = matchState.AdvanceRevision();
            TrapFloorCollectedKeyState claim = objectiveState.RecordKeyClaim(
                floorCard,
                command.Context.RequestedByPlayerId,
                acceptedRevision);
            TrapFloorActivityEntry activity = activityFeed.RecordKeyClaim(
                acceptedRevision,
                command.Context.RequestedByPlayerId,
                floorCard);
            return TrapFloorObjectiveResult.Accepted(
                acceptedRevision,
                floorCard,
                claim,
                activity,
                objectiveState);
        }

        public TrapFloorObjectiveResult AttemptEscape(
            MatchState matchState,
            TrapFloorAttemptEscapeCommand command)
        {
            TrapFloorObjectiveError validationError = ValidateCommand(
                matchState,
                command,
                command?.Context ?? default);
            if (validationError != TrapFloorObjectiveError.None)
            {
                return Failure(StatusFor(validationError), validationError);
            }

            if (!TryGetRevealedFloor(
                    matchState,
                    command.FloorCardId,
                    out TrapFloorFloorCardState floorCard,
                    out TrapFloorObjectiveError floorError))
            {
                return Failure(CommandResultStatus.Rejected, floorError);
            }

            if (floorCard.Content.Category != TrapFloorFloorContentCategory.SecretExit)
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorObjectiveError.FloorIsNotExit);
            }

            if (objectiveState.IsWon)
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorObjectiveError.GameAlreadyWon);
            }

            if (!objectiveState.HasRequiredKeys)
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorObjectiveError.RequiredKeysMissing);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return Failure(CommandResultStatus.Conflict, TrapFloorObjectiveError.RevisionOverflow);
            }

            long acceptedRevision = matchState.AdvanceRevision();
            objectiveState.RecordVictory(
                floorCard.ObjectId,
                command.Context.RequestedByPlayerId);
            TrapFloorActivityEntry activity = activityFeed.RecordVictory(
                acceptedRevision,
                command.Context.RequestedByPlayerId,
                floorCard);
            return TrapFloorObjectiveResult.Accepted(
                acceptedRevision,
                floorCard,
                null,
                activity,
                objectiveState);
        }

        private TrapFloorObjectiveError ValidateCommand(
            MatchState matchState,
            object command,
            CommandContext context)
        {
            if (matchState == null)
            {
                return TrapFloorObjectiveError.MatchRequired;
            }

            if (command == null)
            {
                return TrapFloorObjectiveError.CommandRequired;
            }

            if (context.MatchId != matchState.Id)
            {
                return TrapFloorObjectiveError.MatchIdMismatch;
            }

            if (context.ExpectedRevision.HasValue
                && context.ExpectedRevision.Value != matchState.Revision)
            {
                return TrapFloorObjectiveError.MatchRevisionConflict;
            }

            if (matchState.GameTemplateId != template.Template.Id)
            {
                return TrapFloorObjectiveError.MatchTemplateMismatch;
            }

            if (objectiveState.MatchId != matchState.Id
                || objectiveState.RequiredKeyCount
                    != template.Stage03Configuration.RequiredKeyCount)
            {
                return TrapFloorObjectiveError.ObjectiveStateMismatch;
            }

            if (activityFeed.MatchId != matchState.Id)
            {
                return TrapFloorObjectiveError.ActivityFeedMismatch;
            }

            return MatchContainsPlayer(matchState, context.RequestedByPlayerId)
                ? TrapFloorObjectiveError.None
                : TrapFloorObjectiveError.ActorNotParticipating;
        }

        private bool TryGetRevealedFloor(
            MatchState matchState,
            TabletopObjectId floorCardId,
            out TrapFloorFloorCardState floorCard,
            out TrapFloorObjectiveError error)
        {
            if (!template.TryGetFloorCardState(matchState, floorCardId, out floorCard))
            {
                error = TrapFloorObjectiveError.FloorCardMissingOrInvalid;
                return false;
            }

            if (!floorCard.IsRevealed)
            {
                error = TrapFloorObjectiveError.FloorNotRevealed;
                return false;
            }

            error = TrapFloorObjectiveError.None;
            return true;
        }

        private static bool MatchContainsPlayer(MatchState matchState, PlayerId playerId)
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

        private static CommandResultStatus StatusFor(TrapFloorObjectiveError error)
        {
            switch (error)
            {
                case TrapFloorObjectiveError.MatchRequired:
                case TrapFloorObjectiveError.CommandRequired:
                case TrapFloorObjectiveError.MatchIdMismatch:
                    return CommandResultStatus.Invalid;
                case TrapFloorObjectiveError.MatchRevisionConflict:
                case TrapFloorObjectiveError.RevisionOverflow:
                    return CommandResultStatus.Conflict;
                default:
                    return CommandResultStatus.Rejected;
            }
        }

        private TrapFloorObjectiveResult Failure(
            CommandResultStatus status,
            TrapFloorObjectiveError error)
        {
            return TrapFloorObjectiveResult.Failure(status, error, objectiveState);
        }
    }
}
