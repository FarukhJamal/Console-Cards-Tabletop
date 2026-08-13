using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.UseCases
{
    public enum CreateGenericCardBatchError
    {
        None,
        MatchRequired,
        RequestRequired,
        MatchIdMismatch,
        RevisionConflict,
        ActorNotActive,
        QuantityInvalid,
        RevisionOverflow,
        IdentityAllocationFailed,
        LooseCardOrderOverflow,
    }

    public sealed class CreateGenericCardBatchRequest
    {
        public CreateGenericCardBatchRequest(
            CommandContext context,
            int quantity,
            TabletopPose anchorPose)
        {
            if (!IsFinite(anchorPose.Position.X)
                || !IsFinite(anchorPose.Position.Y)
                || !IsFinite(anchorPose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(anchorPose), "Card batch anchor pose must be finite.");
            }

            Context = context;
            Quantity = quantity;
            AnchorPose = anchorPose;
        }

        public CommandContext Context { get; }
        public int Quantity { get; }
        public TabletopPose AnchorPose { get; }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct CreateGenericCardBatchResult
    {
        private CreateGenericCardBatchResult(
            CommandResult commandResult,
            CreateGenericCardBatchError error,
            IReadOnlyList<TabletopObjectId> cardIds)
        {
            CommandResult = commandResult;
            Error = error;
            CardIds = cardIds ?? Array.Empty<TabletopObjectId>();
        }

        public CommandResult CommandResult { get; }
        public CreateGenericCardBatchError Error { get; }
        public IReadOnlyList<TabletopObjectId> CardIds { get; }
        public bool Succeeded => CommandResult.Succeeded;
        public long Revision => CommandResult.Revision;

        internal static CreateGenericCardBatchResult Accepted(
            long revision,
            IReadOnlyList<TabletopObjectId> cardIds)
        {
            return new CreateGenericCardBatchResult(
                CommandResult.Accepted(revision),
                CreateGenericCardBatchError.None,
                cardIds);
        }

        internal static CreateGenericCardBatchResult Failure(
            CommandResultStatus status,
            CreateGenericCardBatchError error)
        {
            return new CreateGenericCardBatchResult(
                CommandResult.Failure(status),
                error,
                Array.Empty<TabletopObjectId>());
        }
    }

    public static class GenericCardBatchLayout
    {
        public const int MaximumLooseBatchQuantity = 20;
        public const double HorizontalSpacing = 0.9d;
        public const double VerticalSpacing = 1.2d;

        public static TabletopPose ResolvePose(
            TabletopPose anchorPose,
            int index,
            int quantity,
            int localOrder)
        {
            if (quantity < 1 || index < 0 || index >= quantity)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            int columns = (int)Math.Ceiling(Math.Sqrt(quantity));
            int rows = (int)Math.Ceiling(quantity / (double)columns);
            int column = index % columns;
            int row = index / columns;
            double localX = (column - ((columns - 1) * 0.5d)) * HorizontalSpacing;
            double localY = (((rows - 1) * 0.5d) - row) * VerticalSpacing;
            double radians = anchorPose.RotationDegrees * (Math.PI / 180d);
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            double rotatedX = (localX * cosine) + (localY * sine);
            double rotatedY = (-localX * sine) + (localY * cosine);
            return new TabletopPose(
                new TableCoordinate(
                    anchorPose.Position.X + rotatedX,
                    anchorPose.Position.Y + rotatedY),
                anchorPose.RotationDegrees,
                anchorPose.Layer,
                localOrder);
        }
    }

    /// <summary>
    /// Atomically creates a deterministic loose-tabletop batch of generic Cards.
    /// </summary>
    public sealed class CreateGenericCardBatchUseCase
    {
        private const int IdentityAllocationAttempts = 32;
        private readonly ITabletopComponentIdentitySource identitySource;

        public CreateGenericCardBatchUseCase(ITabletopComponentIdentitySource identitySource)
        {
            this.identitySource = identitySource ?? throw new ArgumentNullException(nameof(identitySource));
        }

        public CreateGenericCardBatchResult Execute(
            MatchState matchState,
            IReadOnlyList<PlayerId> activePlayerIds,
            CreateGenericCardBatchRequest request)
        {
            CreateGenericCardBatchResult? failure = Validate(matchState, activePlayerIds, request);
            if (failure.HasValue)
            {
                return failure.Value;
            }

            if (!TryAllocateObjectIds(matchState, request.Quantity, out List<TabletopObjectId> cardIds))
            {
                return CreateGenericCardBatchResult.Failure(
                    CommandResultStatus.Conflict,
                    CreateGenericCardBatchError.IdentityAllocationFailed);
            }

            if (!LooseCardOrderResolver.TryResolveTopPose(
                    matchState,
                    TabletopObjectId.Empty,
                    request.AnchorPose,
                    out TabletopPose topPose)
                || topPose.LocalOrder > int.MaxValue - (request.Quantity - 1))
            {
                return CreateGenericCardBatchResult.Failure(
                    CommandResultStatus.Conflict,
                    CreateGenericCardBatchError.LooseCardOrderOverflow);
            }

            List<CardInstanceState> cards = new List<CardInstanceState>(request.Quantity);
            for (int i = 0; i < request.Quantity; i++)
            {
                TabletopPose pose = GenericCardBatchLayout.ResolvePose(
                    request.AnchorPose,
                    i,
                    request.Quantity,
                    topPose.LocalOrder + i);
                TabletopObjectState baseState = new TabletopObjectState(
                    cardIds[i],
                    ToolboxComponentDefinitions.Card,
                    TabletopObjectKind.Card,
                    pose,
                    ContainerId.Empty,
                    request.Context.RequestedByPlayerId,
                    ObjectVisibility.Public,
                    false);
                cards.Add(new CardInstanceState(baseState, CardFace.FaceUp));
            }

            matchState.AddUncontainedCards(cards);
            return CreateGenericCardBatchResult.Accepted(matchState.AdvanceRevision(), cardIds);
        }

        private static CreateGenericCardBatchResult? Validate(
            MatchState matchState,
            IReadOnlyList<PlayerId> activePlayerIds,
            CreateGenericCardBatchRequest request)
        {
            if (matchState == null)
            {
                return CreateGenericCardBatchResult.Failure(
                    CommandResultStatus.Invalid,
                    CreateGenericCardBatchError.MatchRequired);
            }

            if (request == null)
            {
                return CreateGenericCardBatchResult.Failure(
                    CommandResultStatus.Invalid,
                    CreateGenericCardBatchError.RequestRequired);
            }

            if (request.Context.MatchId != matchState.Id)
            {
                return CreateGenericCardBatchResult.Failure(
                    CommandResultStatus.Invalid,
                    CreateGenericCardBatchError.MatchIdMismatch);
            }

            if (request.Context.ExpectedRevision.HasValue
                && request.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return CreateGenericCardBatchResult.Failure(
                    CommandResultStatus.Conflict,
                    CreateGenericCardBatchError.RevisionConflict);
            }

            if (!Contains(activePlayerIds, request.Context.RequestedByPlayerId))
            {
                return CreateGenericCardBatchResult.Failure(
                    CommandResultStatus.Rejected,
                    CreateGenericCardBatchError.ActorNotActive);
            }

            if (request.Quantity < 1 || request.Quantity > GenericCardBatchLayout.MaximumLooseBatchQuantity)
            {
                return CreateGenericCardBatchResult.Failure(
                    CommandResultStatus.Invalid,
                    CreateGenericCardBatchError.QuantityInvalid);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return CreateGenericCardBatchResult.Failure(
                    CommandResultStatus.Conflict,
                    CreateGenericCardBatchError.RevisionOverflow);
            }

            return null;
        }

        private bool TryAllocateObjectIds(
            MatchState matchState,
            int count,
            out List<TabletopObjectId> objectIds)
        {
            objectIds = new List<TabletopObjectId>(count);
            HashSet<TabletopObjectId> allocated = new HashSet<TabletopObjectId>();
            for (int i = 0; i < count; i++)
            {
                bool found = false;
                for (int attempt = 0; attempt < IdentityAllocationAttempts; attempt++)
                {
                    TabletopObjectId candidate = identitySource.NextObjectId();
                    if (!candidate.IsEmpty
                        && !matchState.ContainsObject(candidate)
                        && allocated.Add(candidate))
                    {
                        objectIds.Add(candidate);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    objectIds.Clear();
                    return false;
                }
            }

            return true;
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
