using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Randomness;
using ConsoleCards.Core.Results;

namespace ConsoleCards.Games.TrapFloor
{
    public enum TrapFloorFloormasterCardCategory
    {
        Trap = 0,
        Coin = 1,
        Item = 2,
    }

    public sealed class TrapFloorPendingFloormasterCard
    {
        internal TrapFloorPendingFloormasterCard(
            PlayerId searchingPlayerId,
            TabletopObjectId cardId,
            TrapFloorFloormasterCardCategory category)
        {
            if (searchingPlayerId.IsEmpty)
            {
                throw new ArgumentException("A pending Floormaster Card requires the searching Player.", nameof(searchingPlayerId));
            }

            if (cardId.IsEmpty)
            {
                throw new ArgumentException("A pending Floormaster Card requires a Card ID.", nameof(cardId));
            }

            if (!Enum.IsDefined(typeof(TrapFloorFloormasterCardCategory), category))
            {
                throw new ArgumentOutOfRangeException(nameof(category));
            }

            SearchingPlayerId = searchingPlayerId;
            CardId = cardId;
            Category = category;
        }

        public PlayerId SearchingPlayerId { get; }

        public TabletopObjectId CardId { get; }

        public TrapFloorFloormasterCardCategory Category { get; }
    }

    /// <summary>
    /// Minimum authoritative Trap Floor state for the single unresolved Search result.
    /// </summary>
    public sealed class TrapFloorFloormasterLifecycleState
    {
        public TrapFloorFloormasterLifecycleState(MatchId matchId)
        {
            if (matchId.IsEmpty)
            {
                throw new ArgumentException("Floormaster lifecycle state requires a Match ID.", nameof(matchId));
            }

            MatchId = matchId;
        }

        public MatchId MatchId { get; }

        public TrapFloorPendingFloormasterCard PendingCard { get; private set; }

        public bool HasPendingCard => PendingCard != null;

        internal void SetPendingCard(TrapFloorPendingFloormasterCard pendingCard)
        {
            if (pendingCard == null)
            {
                throw new ArgumentNullException(nameof(pendingCard));
            }

            if (PendingCard != null)
            {
                throw new InvalidOperationException("A pending Floormaster Card already exists.");
            }

            PendingCard = pendingCard;
        }

        internal void ClearPendingCard()
        {
            PendingCard = null;
        }
    }

    public enum TrapFloorFloormasterLifecycleError
    {
        None = 0,
        MatchRequired,
        RequestRequired,
        MatchIdMismatch,
        MatchRevisionConflict,
        MatchTemplateMismatch,
        LifecycleStateMismatch,
        ActorNotParticipating,
        PendingCardBlocksSearch,
        PendingCardMissing,
        PendingCardIdentityMismatch,
        OfficialDeckMissing,
        OfficialDeckInvalid,
        OfficialDiscardMissing,
        OfficialDiscardInvalid,
        OfficialContentStateInvalid,
        OfficialCardUnavailable,
        RandomnessFailed,
        RevisionOverflow,
    }

    public sealed class TrapFloorFloormasterSearchRequest
    {
        public TrapFloorFloormasterSearchRequest(CommandContext context)
        {
            Context = context;
        }

        public CommandContext Context { get; }
    }

    public sealed class CompletePendingFloormasterCardRequest
    {
        public CompletePendingFloormasterCardRequest(
            CommandContext context,
            TabletopObjectId pendingCardId)
        {
            if (pendingCardId.IsEmpty)
            {
                throw new ArgumentException("Trigger completion requires the pending Card ID.", nameof(pendingCardId));
            }

            Context = context;
            PendingCardId = pendingCardId;
        }

        public CommandContext Context { get; }

        public TabletopObjectId PendingCardId { get; }
    }

    public readonly struct TrapFloorFloormasterSearchResult
    {
        private TrapFloorFloormasterSearchResult(
            CommandResult commandResult,
            TrapFloorFloormasterLifecycleError error,
            TrapFloorPendingFloormasterCard pendingCard,
            bool reshuffledDiscard)
        {
            CommandResult = commandResult;
            Error = error;
            PendingCard = pendingCard;
            ReshuffledDiscard = reshuffledDiscard;
        }

        public CommandResult CommandResult { get; }

        public TrapFloorFloormasterLifecycleError Error { get; }

        public TrapFloorPendingFloormasterCard PendingCard { get; }

        public bool ReshuffledDiscard { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public long Revision => CommandResult.Revision;

        internal static TrapFloorFloormasterSearchResult Accepted(
            long revision,
            TrapFloorPendingFloormasterCard pendingCard,
            bool reshuffledDiscard)
        {
            return new TrapFloorFloormasterSearchResult(
                CommandResult.Accepted(revision),
                TrapFloorFloormasterLifecycleError.None,
                pendingCard,
                reshuffledDiscard);
        }

        internal static TrapFloorFloormasterSearchResult Failure(
            CommandResultStatus status,
            TrapFloorFloormasterLifecycleError error)
        {
            return new TrapFloorFloormasterSearchResult(
                CommandResult.Failure(status),
                error,
                null,
                false);
        }
    }

    public readonly struct CompletePendingFloormasterCardResult
    {
        private CompletePendingFloormasterCardResult(
            CommandResult commandResult,
            TrapFloorFloormasterLifecycleError error,
            TabletopObjectId completedCardId)
        {
            CommandResult = commandResult;
            Error = error;
            CompletedCardId = completedCardId;
        }

        public CommandResult CommandResult { get; }

        public TrapFloorFloormasterLifecycleError Error { get; }

        public TabletopObjectId CompletedCardId { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public long Revision => CommandResult.Revision;

        internal static CompletePendingFloormasterCardResult Accepted(
            long revision,
            TabletopObjectId completedCardId)
        {
            return new CompletePendingFloormasterCardResult(
                CommandResult.Accepted(revision),
                TrapFloorFloormasterLifecycleError.None,
                completedCardId);
        }

        public static CompletePendingFloormasterCardResult Failure(
            CommandResultStatus status,
            TrapFloorFloormasterLifecycleError error)
        {
            return new CompletePendingFloormasterCardResult(
                CommandResult.Failure(status),
                error,
                TabletopObjectId.Empty);
        }
    }

    /// <summary>
    /// Trap Floor-specific Search and post-Trigger lifecycle. Card effects are deliberately outside this service.
    /// </summary>
    public sealed class TrapFloorFloormasterLifecycleService
    {
        private readonly TrapFloorTemplateDefinition template;
        private readonly IRandomValueSource randomValueSource;
        private readonly TrapFloorFloormasterLifecycleState state;

        public TrapFloorFloormasterLifecycleService(
            TrapFloorTemplateDefinition template,
            IRandomValueSource randomValueSource,
            TrapFloorFloormasterLifecycleState state)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            this.randomValueSource = randomValueSource ?? throw new ArgumentNullException(nameof(randomValueSource));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public TrapFloorFloormasterSearchResult Search(
            MatchState matchState,
            IReadOnlyList<PlayerId> participatingPlayerIds,
            TrapFloorFloormasterSearchRequest request)
        {
            if (matchState == null)
            {
                return SearchFailure(CommandResultStatus.Invalid, TrapFloorFloormasterLifecycleError.MatchRequired);
            }

            if (request == null)
            {
                return SearchFailure(CommandResultStatus.Invalid, TrapFloorFloormasterLifecycleError.RequestRequired);
            }

            TrapFloorFloormasterLifecycleError contextError = ValidateContext(
                matchState,
                participatingPlayerIds,
                request.Context,
                out CommandResultStatus contextStatus);
            if (contextError != TrapFloorFloormasterLifecycleError.None)
            {
                return SearchFailure(contextStatus, contextError);
            }

            if (state.HasPendingCard)
            {
                return SearchFailure(
                    CommandResultStatus.Rejected,
                    TrapFloorFloormasterLifecycleError.PendingCardBlocksSearch);
            }

            TrapFloorFloormasterLifecycleError lifecycleError = ValidateOfficialLifecycle(
                matchState,
                out ContainerState deck,
                out ContainerState discard);
            if (lifecycleError != TrapFloorFloormasterLifecycleError.None)
            {
                return SearchFailure(CommandResultStatus.Rejected, lifecycleError);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return SearchFailure(CommandResultStatus.Conflict, TrapFloorFloormasterLifecycleError.RevisionOverflow);
            }

            bool reshuffledDiscard = false;
            List<TabletopObjectId> shuffledOrder = null;
            TabletopObjectId drawnCardId;
            CardInstanceState drawnCard;
            TrapFloorFloormasterCardCategory category;
            if (deck.Count == 0)
            {
                if (discard.Count == 0)
                {
                    return SearchFailure(
                        CommandResultStatus.Rejected,
                        TrapFloorFloormasterLifecycleError.OfficialCardUnavailable);
                }

                shuffledOrder = CreateShuffledOrder(discard.ObjectIds);
                if (shuffledOrder == null)
                {
                    return SearchFailure(
                        CommandResultStatus.Rejected,
                        TrapFloorFloormasterLifecycleError.RandomnessFailed);
                }

                drawnCardId = shuffledOrder[shuffledOrder.Count - 1];
                if (!TryResolveDrawableCard(
                        matchState,
                        drawnCardId,
                        out drawnCard,
                        out category))
                {
                    return SearchFailure(
                        CommandResultStatus.Rejected,
                        TrapFloorFloormasterLifecycleError.OfficialCardUnavailable);
                }

                ContainerTransferResult recycleResult = MoveAllCards(matchState, discard, deck);
                if (!recycleResult.Succeeded)
                {
                    return SearchFailure(
                        CommandResultStatus.Rejected,
                        TrapFloorFloormasterLifecycleError.OfficialContentStateInvalid);
                }

                deck.ReplaceOrder(shuffledOrder);
                for (int i = 0; i < shuffledOrder.Count; i++)
                {
                    matchState.Cards[shuffledOrder[i]].SetFace(CardFace.FaceDown);
                }

                reshuffledDiscard = true;
            }
            else if (!deck.TryPeekTop(out drawnCardId)
                || !TryResolveDrawableCard(matchState, drawnCardId, out drawnCard, out category))
            {
                return SearchFailure(
                    CommandResultStatus.Rejected,
                    TrapFloorFloormasterLifecycleError.OfficialCardUnavailable);
            }

            ContainerTransferResult drawResult = new ContainerTransferService().RemoveFromContainer(
                drawnCard.BaseState,
                deck);
            if (!drawResult.Succeeded)
            {
                return SearchFailure(
                    CommandResultStatus.Rejected,
                    TrapFloorFloormasterLifecycleError.OfficialContentStateInvalid);
            }

            drawnCard.BaseState.SetPose(template.FloormasterRevealPose);
            drawnCard.SetFace(CardFace.FaceUp);
            TrapFloorPendingFloormasterCard pendingCard = new TrapFloorPendingFloormasterCard(
                request.Context.RequestedByPlayerId,
                drawnCardId,
                category);
            state.SetPendingCard(pendingCard);
            long revision = matchState.AdvanceRevision();
            return TrapFloorFloormasterSearchResult.Accepted(revision, pendingCard, reshuffledDiscard);
        }

        /// <summary>
        /// Records that an external Trigger rule handler has finished the pending Card, then discards it.
        /// This boundary does not execute any Trap, Coin, or Item effect.
        /// </summary>
        public CompletePendingFloormasterCardResult CompleteResolvedCard(
            MatchState matchState,
            IReadOnlyList<PlayerId> participatingPlayerIds,
            CompletePendingFloormasterCardRequest request)
        {
            if (matchState == null)
            {
                return CompletionFailure(CommandResultStatus.Invalid, TrapFloorFloormasterLifecycleError.MatchRequired);
            }

            if (request == null)
            {
                return CompletionFailure(CommandResultStatus.Invalid, TrapFloorFloormasterLifecycleError.RequestRequired);
            }

            TrapFloorFloormasterLifecycleError contextError = ValidateContext(
                matchState,
                participatingPlayerIds,
                request.Context,
                out CommandResultStatus contextStatus);
            if (contextError != TrapFloorFloormasterLifecycleError.None)
            {
                return CompletionFailure(contextStatus, contextError);
            }

            TrapFloorPendingFloormasterCard pendingCard = state.PendingCard;
            if (pendingCard == null)
            {
                return CompletionFailure(
                    CommandResultStatus.Rejected,
                    TrapFloorFloormasterLifecycleError.PendingCardMissing);
            }

            if (request.PendingCardId != pendingCard.CardId)
            {
                return CompletionFailure(
                    CommandResultStatus.Rejected,
                    TrapFloorFloormasterLifecycleError.PendingCardIdentityMismatch);
            }

            TrapFloorFloormasterLifecycleError lifecycleError = ValidateOfficialLifecycle(
                matchState,
                out ContainerState deck,
                out ContainerState discard);
            if (lifecycleError != TrapFloorFloormasterLifecycleError.None)
            {
                return CompletionFailure(CommandResultStatus.Rejected, lifecycleError);
            }

            if (!matchState.Cards.TryGetValue(pendingCard.CardId, out CardInstanceState card)
                || card.BaseState.ContainerId != ContainerId.Empty
                || card.BaseState.IsUserLocked)
            {
                return CompletionFailure(
                    CommandResultStatus.Rejected,
                    TrapFloorFloormasterLifecycleError.OfficialContentStateInvalid);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return CompletionFailure(CommandResultStatus.Conflict, TrapFloorFloormasterLifecycleError.RevisionOverflow);
            }

            ContainerTransferResult transferResult = new ContainerTransferService().PlaceIntoContainer(
                card.BaseState,
                discard);
            if (!transferResult.Succeeded)
            {
                return CompletionFailure(
                    CommandResultStatus.Rejected,
                    TrapFloorFloormasterLifecycleError.OfficialDiscardInvalid);
            }

            state.ClearPendingCard();
            long revision = matchState.AdvanceRevision();
            return CompletePendingFloormasterCardResult.Accepted(revision, pendingCard.CardId);
        }

        private TrapFloorFloormasterLifecycleError ValidateContext(
            MatchState matchState,
            IReadOnlyList<PlayerId> participatingPlayerIds,
            CommandContext context,
            out CommandResultStatus status)
        {
            if (context.MatchId != matchState.Id)
            {
                status = CommandResultStatus.Invalid;
                return TrapFloorFloormasterLifecycleError.MatchIdMismatch;
            }

            if (context.ExpectedRevision.HasValue && context.ExpectedRevision.Value != matchState.Revision)
            {
                status = CommandResultStatus.Conflict;
                return TrapFloorFloormasterLifecycleError.MatchRevisionConflict;
            }

            if (matchState.GameTemplateId != template.Template.Id)
            {
                status = CommandResultStatus.Rejected;
                return TrapFloorFloormasterLifecycleError.MatchTemplateMismatch;
            }

            if (state.MatchId != matchState.Id)
            {
                status = CommandResultStatus.Rejected;
                return TrapFloorFloormasterLifecycleError.LifecycleStateMismatch;
            }

            if (!ContainsPlayer(participatingPlayerIds, context.RequestedByPlayerId)
                || !MatchContainsParticipatingPlayer(matchState, context.RequestedByPlayerId))
            {
                status = CommandResultStatus.Rejected;
                return TrapFloorFloormasterLifecycleError.ActorNotParticipating;
            }

            status = CommandResultStatus.Accepted;
            return TrapFloorFloormasterLifecycleError.None;
        }

        private TrapFloorFloormasterLifecycleError ValidateOfficialLifecycle(
            MatchState matchState,
            out ContainerState deck,
            out ContainerState discard)
        {
            deck = null;
            discard = null;
            if (!matchState.Containers.TryGetValue(template.FloormasterDeckId, out deck))
            {
                return TrapFloorFloormasterLifecycleError.OfficialDeckMissing;
            }

            if (deck.Kind != ContainerKind.Deck)
            {
                return TrapFloorFloormasterLifecycleError.OfficialDeckInvalid;
            }

            if (!matchState.Containers.TryGetValue(template.FloormasterDiscardId, out discard))
            {
                return TrapFloorFloormasterLifecycleError.OfficialDiscardMissing;
            }

            if (discard.Kind != ContainerKind.DiscardPile)
            {
                return TrapFloorFloormasterLifecycleError.OfficialDiscardInvalid;
            }

            HashSet<TabletopObjectId> deckIds = new HashSet<TabletopObjectId>(deck.ObjectIds);
            HashSet<TabletopObjectId> discardIds = new HashSet<TabletopObjectId>(discard.ObjectIds);
            for (int i = 0; i < deck.ObjectIds.Count; i++)
            {
                if (!template.IsOfficialFloormasterCard(deck.ObjectIds[i]))
                {
                    return TrapFloorFloormasterLifecycleError.OfficialContentStateInvalid;
                }
            }

            for (int i = 0; i < discard.ObjectIds.Count; i++)
            {
                if (!template.IsOfficialFloormasterCard(discard.ObjectIds[i]))
                {
                    return TrapFloorFloormasterLifecycleError.OfficialContentStateInvalid;
                }
            }

            TrapFloorPendingFloormasterCard pending = state.PendingCard;
            for (int i = 0; i < template.FloormasterCardIds.Count; i++)
            {
                TabletopObjectId officialCardId = template.FloormasterCardIds[i];
                if (!matchState.Cards.TryGetValue(officialCardId, out CardInstanceState card))
                {
                    return TrapFloorFloormasterLifecycleError.OfficialContentStateInvalid;
                }

                bool inDeck = deckIds.Contains(officialCardId);
                bool inDiscard = discardIds.Contains(officialCardId);
                bool isPending = pending != null && pending.CardId == officialCardId;
                int locationCount = (inDeck ? 1 : 0) + (inDiscard ? 1 : 0) + (isPending ? 1 : 0);
                if (locationCount != 1)
                {
                    return TrapFloorFloormasterLifecycleError.OfficialContentStateInvalid;
                }

                if ((inDeck && card.BaseState.ContainerId != deck.Id)
                    || (inDiscard && card.BaseState.ContainerId != discard.Id)
                    || (isPending && card.BaseState.ContainerId != ContainerId.Empty))
                {
                    return TrapFloorFloormasterLifecycleError.OfficialContentStateInvalid;
                }
            }

            if (pending != null
                && (!template.TryGetFloormasterCardCategory(pending.CardId, out TrapFloorFloormasterCardCategory category)
                    || category != pending.Category))
            {
                return TrapFloorFloormasterLifecycleError.OfficialContentStateInvalid;
            }

            return TrapFloorFloormasterLifecycleError.None;
        }

        private List<TabletopObjectId> CreateShuffledOrder(IReadOnlyList<TabletopObjectId> sourceOrder)
        {
            List<TabletopObjectId> shuffled = new List<TabletopObjectId>(sourceOrder);
            try
            {
                for (int index = shuffled.Count - 1; index > 0; index--)
                {
                    int swapIndex = randomValueSource.NextInt(0, index + 1);
                    if (swapIndex < 0 || swapIndex > index)
                    {
                        return null;
                    }

                    TabletopObjectId value = shuffled[index];
                    shuffled[index] = shuffled[swapIndex];
                    shuffled[swapIndex] = value;
                }
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            return shuffled;
        }

        private bool TryResolveDrawableCard(
            MatchState matchState,
            TabletopObjectId cardId,
            out CardInstanceState card,
            out TrapFloorFloormasterCardCategory category)
        {
            card = null;
            category = default;
            if (!matchState.Cards.TryGetValue(cardId, out CardInstanceState resolvedCard)
                || !template.TryGetFloormasterCardCategory(cardId, out TrapFloorFloormasterCardCategory resolvedCategory)
                || resolvedCard.BaseState.IsUserLocked)
            {
                return false;
            }

            card = resolvedCard;
            category = resolvedCategory;
            return true;
        }

        private static ContainerTransferResult MoveAllCards(
            MatchState matchState,
            ContainerState source,
            ContainerState destination)
        {
            List<TabletopObjectId> objectIds = new List<TabletopObjectId>(source.ObjectIds);
            Dictionary<TabletopObjectId, TabletopObjectState> objects =
                new Dictionary<TabletopObjectId, TabletopObjectState>();
            for (int i = 0; i < objectIds.Count; i++)
            {
                objects.Add(objectIds[i], matchState.Cards[objectIds[i]].BaseState);
            }

            return new ContainerBatchTransferService().TransferOrdered(
                objects,
                source,
                destination,
                objectIds);
        }

        private static bool ContainsPlayer(IReadOnlyList<PlayerId> players, PlayerId playerId)
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

        private static bool MatchContainsParticipatingPlayer(MatchState matchState, PlayerId playerId)
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

        private static TrapFloorFloormasterSearchResult SearchFailure(
            CommandResultStatus status,
            TrapFloorFloormasterLifecycleError error)
        {
            return TrapFloorFloormasterSearchResult.Failure(status, error);
        }

        private static CompletePendingFloormasterCardResult CompletionFailure(
            CommandResultStatus status,
            TrapFloorFloormasterLifecycleError error)
        {
            return CompletePendingFloormasterCardResult.Failure(status, error);
        }
    }
}
