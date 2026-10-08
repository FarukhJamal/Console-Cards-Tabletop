using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;

namespace ConsoleCards.Application.UseCases
{
    public enum CollectHandIntoDeckError
    {
        None,
        MatchRequired,
        RequestRequired,
        MatchIdMismatch,
        RevisionConflict,
        HandMissing,
        HandEmpty,
        ObjectUserLocked,
        RevisionOverflow,
        IdentityAllocationFailed,
        PhysicalSurfaceRequired,
    }

    public sealed class CollectHandIntoDeckRequest
    {
        public CollectHandIntoDeckRequest(CommandContext context, ContainerId handContainerId, TabletopPose deckPose)
        {
            if (double.IsNaN(deckPose.Position.X) || double.IsInfinity(deckPose.Position.X)
                || double.IsNaN(deckPose.Position.Y) || double.IsInfinity(deckPose.Position.Y)
                || float.IsNaN(deckPose.RotationDegrees) || float.IsInfinity(deckPose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(deckPose), "Deck pose must be finite.");
            }

            Context = context;
            HandContainerId = handContainerId;
            DeckPose = deckPose;
        }

        public CommandContext Context { get; }
        public ContainerId HandContainerId { get; }
        public TabletopPose DeckPose { get; }
    }

    public readonly struct CollectHandIntoDeckResult
    {
        private CollectHandIntoDeckResult(
            CommandResult commandResult,
            CollectHandIntoDeckError error,
            ContainerId deckContainerId,
            IReadOnlyList<TabletopObjectId> cardIds)
        {
            CommandResult = commandResult;
            Error = error;
            DeckContainerId = deckContainerId;
            CardIds = cardIds ?? Array.Empty<TabletopObjectId>();
        }

        public CommandResult CommandResult { get; }
        public CollectHandIntoDeckError Error { get; }
        public ContainerId DeckContainerId { get; }
        public IReadOnlyList<TabletopObjectId> CardIds { get; }
        public bool Succeeded => CommandResult.Succeeded;

        internal static CollectHandIntoDeckResult Accepted(
            long revision,
            ContainerId deckContainerId,
            IReadOnlyList<TabletopObjectId> cardIds) =>
            new CollectHandIntoDeckResult(
                CommandResult.Accepted(revision),
                CollectHandIntoDeckError.None,
                deckContainerId,
                cardIds);

        internal static CollectHandIntoDeckResult Failure(
            CommandResultStatus status,
            CollectHandIntoDeckError error) =>
            new CollectHandIntoDeckResult(
                CommandResult.Failure(status),
                error,
                ContainerId.Empty,
                Array.Empty<TabletopObjectId>());
    }

    /// <summary>
    /// Turning a table's Hand off (doc 22, H-E): every card in the Hand goes, in hand order and face down, into a
    /// new placed Deck at the requested pose. One command, so one Undo returns the cards to the Hand.
    /// </summary>
    public sealed class CollectHandIntoDeckUseCase
    {
        private const int IdentityAllocationAttempts = 32;
        private readonly ITabletopComponentIdentitySource identitySource;
        private readonly Func<TabletopPose, float?> resolveSurfaceHeight;

        public CollectHandIntoDeckUseCase(
            ITabletopComponentIdentitySource identitySource,
            Func<TabletopPose, float?> resolveSurfaceHeight = null)
        {
            this.identitySource = identitySource ?? throw new ArgumentNullException(nameof(identitySource));
            this.resolveSurfaceHeight = resolveSurfaceHeight;
        }

        public CollectHandIntoDeckResult Execute(MatchState matchState, CollectHandIntoDeckRequest request)
        {
            if (matchState == null)
            {
                return CollectHandIntoDeckResult.Failure(CommandResultStatus.Invalid, CollectHandIntoDeckError.MatchRequired);
            }

            if (request == null)
            {
                return CollectHandIntoDeckResult.Failure(CommandResultStatus.Invalid, CollectHandIntoDeckError.RequestRequired);
            }

            if (request.Context.MatchId != matchState.Id)
            {
                return CollectHandIntoDeckResult.Failure(CommandResultStatus.Invalid, CollectHandIntoDeckError.MatchIdMismatch);
            }

            if (request.Context.ExpectedRevision.HasValue
                && request.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return CollectHandIntoDeckResult.Failure(CommandResultStatus.Conflict, CollectHandIntoDeckError.RevisionConflict);
            }

            if (!matchState.Containers.TryGetValue(request.HandContainerId, out ContainerState hand)
                || hand.Kind != ContainerKind.Hand)
            {
                return CollectHandIntoDeckResult.Failure(CommandResultStatus.Rejected, CollectHandIntoDeckError.HandMissing);
            }

            if (hand.Count == 0)
            {
                return CollectHandIntoDeckResult.Failure(CommandResultStatus.Rejected, CollectHandIntoDeckError.HandEmpty);
            }

            List<TabletopObjectId> cardIds = new List<TabletopObjectId>(hand.ObjectIds);
            Dictionary<TabletopObjectId, TabletopObjectState> objects =
                new Dictionary<TabletopObjectId, TabletopObjectState>(cardIds.Count);
            for (int i = 0; i < cardIds.Count; i++)
            {
                TabletopObjectState objectState = matchState.GetObject(cardIds[i]);
                if (objectState.IsUserLocked)
                {
                    return CollectHandIntoDeckResult.Failure(CommandResultStatus.Rejected, CollectHandIntoDeckError.ObjectUserLocked);
                }

                objects.Add(cardIds[i], objectState);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return CollectHandIntoDeckResult.Failure(CommandResultStatus.Conflict, CollectHandIntoDeckError.RevisionOverflow);
            }

            float? surfaceHeight = resolveSurfaceHeight?.Invoke(request.DeckPose);
            if (resolveSurfaceHeight != null && !surfaceHeight.HasValue)
            {
                return CollectHandIntoDeckResult.Failure(CommandResultStatus.Rejected, CollectHandIntoDeckError.PhysicalSurfaceRequired);
            }

            if (!TryAllocateContainerId(matchState, out ContainerId deckId))
            {
                return CollectHandIntoDeckResult.Failure(CommandResultStatus.Conflict, CollectHandIntoDeckError.IdentityAllocationFailed);
            }

            ContainerState deck = new ContainerState(deckId, ContainerKind.Deck, SeatId.Empty, ObjectVisibility.Public, 0);
            matchState.AddEmptyPlacedContainer(deck, new ContainerPlacementState(deckId, request.DeckPose, surfaceHeight));
            ContainerTransferResult transfer = new ContainerBatchTransferService().TransferOrdered(objects, hand, deck, cardIds);
            if (!transfer.Succeeded)
            {
                matchState.RemoveEmptyContainer(deckId);
                return CollectHandIntoDeckResult.Failure(CommandResultStatus.Rejected, CollectHandIntoDeckError.HandMissing);
            }

            for (int i = 0; i < cardIds.Count; i++)
            {
                if (matchState.Cards.TryGetValue(cardIds[i], out CardInstanceState card))
                {
                    card.SetFace(CardFace.FaceDown);
                }

                objects[cardIds[i]].SetPhysicalState(null);
            }

            long revision = matchState.AdvanceRevision(
                request.Context.Id, request.Context.RequestedByPlayerId, AuthoritativeActionKind.CreateComponent);
            return CollectHandIntoDeckResult.Accepted(revision, deckId, cardIds);
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
    }
}
