using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.UseCases
{
    public enum DuplicateTabletopComponentError
    {
        None,
        MatchRequired,
        RequestRequired,
        MatchIdMismatch,
        RevisionConflict,
        ActorNotActive,
        SourceMissing,
        SourceMustBeLoose,
        SourceKindUnsupported,
        RevisionOverflow,
        CreationRejected,
    }

    public sealed class DuplicateTabletopComponentRequest
    {
        public DuplicateTabletopComponentRequest(
            CommandContext context,
            TabletopObjectId sourceObjectId,
            TabletopPose placementPose)
        {
            if (!IsFinite(placementPose.Position.X)
                || !IsFinite(placementPose.Position.Y)
                || !IsFinite(placementPose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(placementPose), "Duplicate placement pose must be finite.");
            }

            Context = context;
            SourceObjectId = sourceObjectId;
            PlacementPose = placementPose;
        }

        public CommandContext Context { get; }
        public TabletopObjectId SourceObjectId { get; }
        public TabletopPose PlacementPose { get; }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct DuplicateTabletopComponentResult
    {
        private DuplicateTabletopComponentResult(
            CommandResult commandResult,
            DuplicateTabletopComponentError error,
            CreateTabletopComponentResult creationResult)
        {
            CommandResult = commandResult;
            Error = error;
            CreationResult = creationResult;
        }

        public CommandResult CommandResult { get; }
        public DuplicateTabletopComponentError Error { get; }
        public CreateTabletopComponentResult CreationResult { get; }
        public bool Succeeded => CommandResult.Succeeded;
        public long Revision => CommandResult.Revision;

        internal static DuplicateTabletopComponentResult Accepted(
            CreateTabletopComponentResult creationResult)
        {
            return new DuplicateTabletopComponentResult(
                creationResult.CommandResult,
                DuplicateTabletopComponentError.None,
                creationResult);
        }

        internal static DuplicateTabletopComponentResult Failure(
            CommandResultStatus status,
            DuplicateTabletopComponentError error)
        {
            return new DuplicateTabletopComponentResult(
                CommandResult.Failure(status),
                error,
                default);
        }
    }

    /// <summary>
    /// Validates an authoritative loose source and delegates creation of one unassociated generic copy
    /// to the shared component-creation operation.
    /// </summary>
    public sealed class DuplicateTabletopComponentUseCase
    {
        private readonly CreateTabletopComponentUseCase creationUseCase;

        public DuplicateTabletopComponentUseCase(CreateTabletopComponentUseCase creationUseCase)
        {
            this.creationUseCase = creationUseCase ?? throw new ArgumentNullException(nameof(creationUseCase));
        }

        public DuplicateTabletopComponentResult Execute(
            MatchState matchState,
            IReadOnlyList<PlayerId> activePlayerIds,
            DuplicateTabletopComponentRequest request)
        {
            DuplicateTabletopComponentResult? failure = ValidateCommon(matchState, activePlayerIds, request);
            if (failure.HasValue)
            {
                return failure.Value;
            }

            if (request.SourceObjectId.IsEmpty || !matchState.ContainsObject(request.SourceObjectId))
            {
                return DuplicateTabletopComponentResult.Failure(
                    CommandResultStatus.Rejected,
                    DuplicateTabletopComponentError.SourceMissing);
            }

            TabletopObjectState sourceState = matchState.GetObject(request.SourceObjectId);
            if (!sourceState.ContainerId.IsEmpty)
            {
                return DuplicateTabletopComponentResult.Failure(
                    CommandResultStatus.Rejected,
                    DuplicateTabletopComponentError.SourceMustBeLoose);
            }

            TabletopComponentKind componentKind;
            int dieSideCount = 0;
            CardFace cardFace = CardFace.FaceUp;
            switch (sourceState.Kind)
            {
                case TabletopObjectKind.Card:
                    componentKind = TabletopComponentKind.Card;
                    cardFace = matchState.Cards[request.SourceObjectId].Face;
                    break;
                case TabletopObjectKind.Pawn:
                    componentKind = TabletopComponentKind.Pawn;
                    break;
                case TabletopObjectKind.Token:
                    componentKind = TabletopComponentKind.Token;
                    break;
                case TabletopObjectKind.Die:
                    componentKind = TabletopComponentKind.Die;
                    DieState die = matchState.Dice[request.SourceObjectId];
                    dieSideCount = die.SideCount;
                    break;
                default:
                    return DuplicateTabletopComponentResult.Failure(
                        CommandResultStatus.Rejected,
                        DuplicateTabletopComponentError.SourceKindUnsupported);
            }

            CreateTabletopComponentResult result = creationUseCase.Execute(
                matchState,
                activePlayerIds,
                new CreateTabletopComponentRequest(
                    request.Context,
                    componentKind,
                    request.PlacementPose,
                    dieSideCount,
                    cardFace));
            return result.Succeeded
                ? DuplicateTabletopComponentResult.Accepted(result)
                : DuplicateTabletopComponentResult.Failure(
                    result.CommandResult.Status,
                    DuplicateTabletopComponentError.CreationRejected);
        }

        private static DuplicateTabletopComponentResult? ValidateCommon(
            MatchState matchState,
            IReadOnlyList<PlayerId> activePlayerIds,
            DuplicateTabletopComponentRequest request)
        {
            if (matchState == null)
            {
                return DuplicateTabletopComponentResult.Failure(
                    CommandResultStatus.Invalid,
                    DuplicateTabletopComponentError.MatchRequired);
            }

            if (request == null)
            {
                return DuplicateTabletopComponentResult.Failure(
                    CommandResultStatus.Invalid,
                    DuplicateTabletopComponentError.RequestRequired);
            }

            if (request.Context.MatchId != matchState.Id)
            {
                return DuplicateTabletopComponentResult.Failure(
                    CommandResultStatus.Invalid,
                    DuplicateTabletopComponentError.MatchIdMismatch);
            }

            if (request.Context.ExpectedRevision.HasValue
                && request.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return DuplicateTabletopComponentResult.Failure(
                    CommandResultStatus.Conflict,
                    DuplicateTabletopComponentError.RevisionConflict);
            }

            if (!Contains(activePlayerIds, request.Context.RequestedByPlayerId))
            {
                return DuplicateTabletopComponentResult.Failure(
                    CommandResultStatus.Rejected,
                    DuplicateTabletopComponentError.ActorNotActive);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return DuplicateTabletopComponentResult.Failure(
                    CommandResultStatus.Conflict,
                    DuplicateTabletopComponentError.RevisionOverflow);
            }

            return null;
        }

        private static bool Contains(IReadOnlyList<PlayerId> players, PlayerId playerId)
        {
            if (players == null)
            {
                return false;
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == playerId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
