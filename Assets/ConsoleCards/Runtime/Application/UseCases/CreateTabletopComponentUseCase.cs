using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.UseCases
{
    public enum TabletopComponentKind
    {
        Card,
        Deck,
        Stack,
        Pawn,
        Token,
        Die,
    }

    public enum CreateTabletopComponentError
    {
        None,
        MatchRequired,
        RequestRequired,
        MatchIdMismatch,
        RevisionConflict,
        ActorNotActive,
        ComponentKindInvalid,
        DieSideCountInvalid,
        RevisionOverflow,
        IdentityAllocationFailed,
        LooseCardOrderOverflow,
    }

    public sealed class CreateTabletopComponentRequest
    {
        public CreateTabletopComponentRequest(
            CommandContext context,
            TabletopComponentKind componentKind,
            TabletopPose initialPose,
            int dieSideCount = 0)
        {
            if (!IsFinite(initialPose.Position.X)
                || !IsFinite(initialPose.Position.Y)
                || !IsFinite(initialPose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(initialPose), "Initial component pose must be finite.");
            }

            Context = context;
            ComponentKind = componentKind;
            InitialPose = initialPose;
            DieSideCount = dieSideCount;
        }

        public CommandContext Context { get; }
        public TabletopComponentKind ComponentKind { get; }
        public TabletopPose InitialPose { get; }
        public int DieSideCount { get; }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct CreateTabletopComponentResult
    {
        private CreateTabletopComponentResult(
            CommandResult commandResult,
            CreateTabletopComponentError error,
            TabletopComponentKind componentKind,
            TabletopObjectId objectId,
            ContainerId containerId)
        {
            CommandResult = commandResult;
            Error = error;
            ComponentKind = componentKind;
            ObjectId = objectId;
            ContainerId = containerId;
        }

        public CommandResult CommandResult { get; }
        public CreateTabletopComponentError Error { get; }
        public TabletopComponentKind ComponentKind { get; }
        public TabletopObjectId ObjectId { get; }
        public ContainerId ContainerId { get; }
        public bool Succeeded => CommandResult.Succeeded;
        public long Revision => CommandResult.Revision;

        internal static CreateTabletopComponentResult ObjectAccepted(
            long revision,
            TabletopComponentKind kind,
            TabletopObjectId objectId)
        {
            return new CreateTabletopComponentResult(
                CommandResult.Accepted(revision),
                CreateTabletopComponentError.None,
                kind,
                objectId,
                ContainerId.Empty);
        }

        internal static CreateTabletopComponentResult ContainerAccepted(
            long revision,
            TabletopComponentKind kind,
            ContainerId containerId)
        {
            return new CreateTabletopComponentResult(
                CommandResult.Accepted(revision),
                CreateTabletopComponentError.None,
                kind,
                TabletopObjectId.Empty,
                containerId);
        }

        internal static CreateTabletopComponentResult Failure(
            CommandResultStatus status,
            CreateTabletopComponentError error)
        {
            return new CreateTabletopComponentResult(
                CommandResult.Failure(status),
                error,
                default,
                TabletopObjectId.Empty,
                ContainerId.Empty);
        }
    }

    public interface ITabletopComponentIdentitySource
    {
        TabletopObjectId NextObjectId();
        ContainerId NextContainerId();
    }

    public sealed class GuidTabletopComponentIdentitySource : ITabletopComponentIdentitySource
    {
        public TabletopObjectId NextObjectId() => TabletopObjectId.New();
        public ContainerId NextContainerId() => ContainerId.New();
    }

    public static class ToolboxComponentDefinitions
    {
        public static readonly ObjectDefinitionId Card =
            new ObjectDefinitionId(new Guid("a0010000-0000-4000-8000-000000000001"));
        public static readonly ObjectDefinitionId Pawn =
            new ObjectDefinitionId(new Guid("a0010000-0000-4000-8000-000000000002"));
        public static readonly ObjectDefinitionId Token =
            new ObjectDefinitionId(new Guid("a0010000-0000-4000-8000-000000000003"));
        public static readonly ObjectDefinitionId Die =
            new ObjectDefinitionId(new Guid("a0010000-0000-4000-8000-000000000004"));

        public static bool IsSupportedDieSideCount(int sideCount)
        {
            return sideCount == 4
                || sideCount == 6
                || sideCount == 8
                || sideCount == 10
                || sideCount == 12
                || sideCount == 20;
        }
    }

    /// <summary>
    /// Actor-aware authoritative addition of one supported generic component to an active Match.
    /// </summary>
    public sealed class CreateTabletopComponentUseCase
    {
        private const int IdentityAllocationAttempts = 32;
        private readonly ITabletopComponentIdentitySource identitySource;

        public CreateTabletopComponentUseCase(ITabletopComponentIdentitySource identitySource)
        {
            this.identitySource = identitySource ?? throw new ArgumentNullException(nameof(identitySource));
        }

        public CreateTabletopComponentResult Execute(
            MatchState matchState,
            IReadOnlyList<PlayerId> activePlayerIds,
            CreateTabletopComponentRequest request)
        {
            CreateTabletopComponentResult? failure = Validate(matchState, activePlayerIds, request);
            if (failure.HasValue)
            {
                return failure.Value;
            }

            if (request.ComponentKind == TabletopComponentKind.Deck
                || request.ComponentKind == TabletopComponentKind.Stack)
            {
                if (!TryAllocateContainerId(matchState, out ContainerId containerId))
                {
                    return CreateTabletopComponentResult.Failure(
                        CommandResultStatus.Conflict,
                        CreateTabletopComponentError.IdentityAllocationFailed);
                }

                ContainerKind containerKind = request.ComponentKind == TabletopComponentKind.Deck
                    ? ContainerKind.Deck
                    : ContainerKind.Stack;
                matchState.AddEmptyPlacedContainer(
                    new ContainerState(
                        containerId,
                        containerKind,
                        SeatId.Empty,
                        ObjectVisibility.Public,
                        0),
                    new ContainerPlacementState(containerId, request.InitialPose));
                long revision = matchState.AdvanceRevision();
                return CreateTabletopComponentResult.ContainerAccepted(
                    revision,
                    request.ComponentKind,
                    containerId);
            }

            if (!TryAllocateObjectId(matchState, out TabletopObjectId objectId))
            {
                return CreateTabletopComponentResult.Failure(
                    CommandResultStatus.Conflict,
                    CreateTabletopComponentError.IdentityAllocationFailed);
            }

            TabletopPose initialPose = request.InitialPose;
            if (request.ComponentKind == TabletopComponentKind.Card
                && !LooseCardOrderResolver.TryResolveTopPose(
                    matchState,
                    objectId,
                    request.InitialPose,
                    out initialPose))
            {
                return CreateTabletopComponentResult.Failure(
                    CommandResultStatus.Conflict,
                    CreateTabletopComponentError.LooseCardOrderOverflow);
            }

            TabletopObjectKind objectKind = ToObjectKind(request.ComponentKind);
            TabletopObjectState baseState = new TabletopObjectState(
                objectId,
                DefinitionIdFor(request.ComponentKind),
                objectKind,
                initialPose,
                ContainerId.Empty,
                request.Context.RequestedByPlayerId,
                ObjectVisibility.Public,
                false);

            switch (request.ComponentKind)
            {
                case TabletopComponentKind.Card:
                    matchState.AddUncontainedCard(new CardInstanceState(baseState, CardFace.FaceUp));
                    break;
                case TabletopComponentKind.Pawn:
                    matchState.AddUncontainedPawn(new PawnState(baseState));
                    break;
                case TabletopComponentKind.Token:
                    matchState.AddUncontainedToken(new TokenState(baseState));
                    break;
                case TabletopComponentKind.Die:
                    matchState.AddUncontainedDie(new DieState(baseState, request.DieSideCount, 1));
                    break;
                default:
                    throw new InvalidOperationException("Validated component kind is unsupported.");
            }

            long acceptedRevision = matchState.AdvanceRevision();
            return CreateTabletopComponentResult.ObjectAccepted(
                acceptedRevision,
                request.ComponentKind,
                objectId);
        }

        private static CreateTabletopComponentResult? Validate(
            MatchState matchState,
            IReadOnlyList<PlayerId> activePlayerIds,
            CreateTabletopComponentRequest request)
        {
            if (matchState == null)
            {
                return CreateTabletopComponentResult.Failure(
                    CommandResultStatus.Invalid,
                    CreateTabletopComponentError.MatchRequired);
            }

            if (request == null)
            {
                return CreateTabletopComponentResult.Failure(
                    CommandResultStatus.Invalid,
                    CreateTabletopComponentError.RequestRequired);
            }

            if (request.Context.MatchId != matchState.Id)
            {
                return CreateTabletopComponentResult.Failure(
                    CommandResultStatus.Invalid,
                    CreateTabletopComponentError.MatchIdMismatch);
            }

            if (request.Context.ExpectedRevision.HasValue
                && request.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return CreateTabletopComponentResult.Failure(
                    CommandResultStatus.Conflict,
                    CreateTabletopComponentError.RevisionConflict);
            }

            if (!Contains(activePlayerIds, request.Context.RequestedByPlayerId))
            {
                return CreateTabletopComponentResult.Failure(
                    CommandResultStatus.Rejected,
                    CreateTabletopComponentError.ActorNotActive);
            }

            if (!Enum.IsDefined(typeof(TabletopComponentKind), request.ComponentKind))
            {
                return CreateTabletopComponentResult.Failure(
                    CommandResultStatus.Invalid,
                    CreateTabletopComponentError.ComponentKindInvalid);
            }

            bool validDieConfiguration = request.ComponentKind == TabletopComponentKind.Die
                ? ToolboxComponentDefinitions.IsSupportedDieSideCount(request.DieSideCount)
                : request.DieSideCount == 0;
            if (!validDieConfiguration)
            {
                return CreateTabletopComponentResult.Failure(
                    CommandResultStatus.Invalid,
                    CreateTabletopComponentError.DieSideCountInvalid);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return CreateTabletopComponentResult.Failure(
                    CommandResultStatus.Conflict,
                    CreateTabletopComponentError.RevisionOverflow);
            }

            return null;
        }

        private bool TryAllocateObjectId(MatchState matchState, out TabletopObjectId objectId)
        {
            for (int i = 0; i < IdentityAllocationAttempts; i++)
            {
                TabletopObjectId candidate = identitySource.NextObjectId();
                if (!candidate.IsEmpty && !matchState.ContainsObject(candidate))
                {
                    objectId = candidate;
                    return true;
                }
            }

            objectId = TabletopObjectId.Empty;
            return false;
        }

        private bool TryAllocateContainerId(MatchState matchState, out ContainerId containerId)
        {
            for (int i = 0; i < IdentityAllocationAttempts; i++)
            {
                ContainerId candidate = identitySource.NextContainerId();
                if (!candidate.IsEmpty && !matchState.Containers.ContainsKey(candidate))
                {
                    containerId = candidate;
                    return true;
                }
            }

            containerId = ContainerId.Empty;
            return false;
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

        private static TabletopObjectKind ToObjectKind(TabletopComponentKind kind)
        {
            switch (kind)
            {
                case TabletopComponentKind.Card: return TabletopObjectKind.Card;
                case TabletopComponentKind.Pawn: return TabletopObjectKind.Pawn;
                case TabletopComponentKind.Token: return TabletopObjectKind.Token;
                case TabletopComponentKind.Die: return TabletopObjectKind.Die;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static ObjectDefinitionId DefinitionIdFor(TabletopComponentKind kind)
        {
            switch (kind)
            {
                case TabletopComponentKind.Card: return ToolboxComponentDefinitions.Card;
                case TabletopComponentKind.Pawn: return ToolboxComponentDefinitions.Pawn;
                case TabletopComponentKind.Token: return ToolboxComponentDefinitions.Token;
                case TabletopComponentKind.Die: return ToolboxComponentDefinitions.Die;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
