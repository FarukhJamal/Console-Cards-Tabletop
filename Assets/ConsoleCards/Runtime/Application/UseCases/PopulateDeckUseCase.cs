using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.UseCases
{
    public enum PopulateDeckError
    {
        None,
        MatchRequired,
        RequestRequired,
        MatchIdMismatch,
        RevisionConflict,
        ActorNotActive,
        DeckMissing,
        ContainerNotDeck,
        DeckNotEmpty,
        QuantityInvalid,
        CapacityExceeded,
        RevisionOverflow,
        IdentityAllocationFailed,
    }

    public sealed class PopulateDeckRequest
    {
        public PopulateDeckRequest(CommandContext context, ContainerId deckContainerId, int quantity)
        {
            Context = context;
            DeckContainerId = deckContainerId;
            Quantity = quantity;
        }

        public CommandContext Context { get; }
        public ContainerId DeckContainerId { get; }
        public int Quantity { get; }
    }

    public readonly struct PopulateDeckResult
    {
        private PopulateDeckResult(
            CommandResult commandResult,
            PopulateDeckError error,
            IReadOnlyList<TabletopObjectId> cardIds)
        {
            CommandResult = commandResult;
            Error = error;
            CardIds = cardIds ?? Array.Empty<TabletopObjectId>();
        }

        public CommandResult CommandResult { get; }
        public PopulateDeckError Error { get; }
        public IReadOnlyList<TabletopObjectId> CardIds { get; }
        public bool Succeeded => CommandResult.Succeeded;
        public long Revision => CommandResult.Revision;

        internal static PopulateDeckResult Accepted(
            long revision,
            IReadOnlyList<TabletopObjectId> cardIds)
        {
            return new PopulateDeckResult(
                CommandResult.Accepted(revision),
                PopulateDeckError.None,
                cardIds);
        }

        internal static PopulateDeckResult Failure(CommandResultStatus status, PopulateDeckError error)
        {
            return new PopulateDeckResult(
                CommandResult.Failure(status),
                error,
                Array.Empty<TabletopObjectId>());
        }
    }

    /// <summary>
    /// Atomically creates generic Cards directly in one authoritative empty Deck.
    /// </summary>
    public sealed class PopulateDeckUseCase
    {
        public const int MaximumQuantity = 100;
        private const int IdentityAllocationAttempts = 32;
        private readonly ITabletopComponentIdentitySource identitySource;

        public PopulateDeckUseCase(ITabletopComponentIdentitySource identitySource)
        {
            this.identitySource = identitySource ?? throw new ArgumentNullException(nameof(identitySource));
        }

        public PopulateDeckResult Execute(
            MatchState matchState,
            IReadOnlyList<PlayerId> activePlayerIds,
            PopulateDeckRequest request)
        {
            PopulateDeckResult? failure = Validate(matchState, activePlayerIds, request, out ContainerState deck);
            if (failure.HasValue)
            {
                return failure.Value;
            }

            if (!TryAllocateObjectIds(matchState, request.Quantity, out List<TabletopObjectId> cardIds))
            {
                return PopulateDeckResult.Failure(
                    CommandResultStatus.Conflict,
                    PopulateDeckError.IdentityAllocationFailed);
            }

            TabletopPose pose = matchState.TryGetContainerPlacement(deck.Id, out ContainerPlacementState placement)
                ? placement.Pose
                : TabletopPose.Default;
            List<CardInstanceState> cards = new List<CardInstanceState>(request.Quantity);
            for (int i = 0; i < request.Quantity; i++)
            {
                TabletopObjectState baseState = new TabletopObjectState(
                    cardIds[i],
                    ToolboxComponentDefinitions.Card,
                    TabletopObjectKind.Card,
                    pose,
                    deck.Id,
                    request.Context.RequestedByPlayerId,
                    ObjectVisibility.Public,
                    false);
                cards.Add(new CardInstanceState(baseState, CardFace.FaceDown));
            }

            matchState.AddCardsToEmptyContainer(deck.Id, cards);
            return PopulateDeckResult.Accepted(matchState.AdvanceRevision(), cardIds);
        }

        private static PopulateDeckResult? Validate(
            MatchState matchState,
            IReadOnlyList<PlayerId> activePlayerIds,
            PopulateDeckRequest request,
            out ContainerState deck)
        {
            deck = null;
            if (matchState == null)
            {
                return PopulateDeckResult.Failure(CommandResultStatus.Invalid, PopulateDeckError.MatchRequired);
            }

            if (request == null)
            {
                return PopulateDeckResult.Failure(CommandResultStatus.Invalid, PopulateDeckError.RequestRequired);
            }

            if (request.Context.MatchId != matchState.Id)
            {
                return PopulateDeckResult.Failure(CommandResultStatus.Invalid, PopulateDeckError.MatchIdMismatch);
            }

            if (request.Context.ExpectedRevision.HasValue
                && request.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return PopulateDeckResult.Failure(CommandResultStatus.Conflict, PopulateDeckError.RevisionConflict);
            }

            if (!Contains(activePlayerIds, request.Context.RequestedByPlayerId))
            {
                return PopulateDeckResult.Failure(CommandResultStatus.Rejected, PopulateDeckError.ActorNotActive);
            }

            if (!matchState.Containers.TryGetValue(request.DeckContainerId, out deck))
            {
                return PopulateDeckResult.Failure(CommandResultStatus.Rejected, PopulateDeckError.DeckMissing);
            }

            if (deck.Kind != ContainerKind.Deck)
            {
                return PopulateDeckResult.Failure(CommandResultStatus.Rejected, PopulateDeckError.ContainerNotDeck);
            }

            if (deck.Count != 0)
            {
                return PopulateDeckResult.Failure(CommandResultStatus.Conflict, PopulateDeckError.DeckNotEmpty);
            }

            if (request.Quantity < 1 || request.Quantity > MaximumQuantity)
            {
                return PopulateDeckResult.Failure(CommandResultStatus.Invalid, PopulateDeckError.QuantityInvalid);
            }

            if (deck.Capacity > 0 && request.Quantity > deck.Capacity)
            {
                return PopulateDeckResult.Failure(CommandResultStatus.Rejected, PopulateDeckError.CapacityExceeded);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return PopulateDeckResult.Failure(CommandResultStatus.Conflict, PopulateDeckError.RevisionOverflow);
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
