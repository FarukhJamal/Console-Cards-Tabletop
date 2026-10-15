using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.GameTemplates.ControllerInputs
{
    // The old Buy Ability market service (purchase into the Action stack) was removed in G2d (doc 18 §15.4);
    // buying is the Shop now (Trap Floor: TrapFloorBuyFromShopUseCase). What stays here is the pending payment
    // shared by the Shop and by taking a prize from the floor.

    /// <summary>
    /// What a pending payment pays for: a card from the game's Shop (the purchase catalog), or a card taken from
    /// the table (Trap Floor: a prize on the floor). Decides what Confirm Payment does.
    /// </summary>
    public enum PendingPurchaseSource
    {
        Shop = 0,
        Table = 1,
    }

    /// <summary>
    /// Match-scoped assistance for a physical Controller-card purchase payment. Card movement
    /// remains owned by the normal authoritative transfer path; this state records only the
    /// exact selected instances and which of them are currently in the purchaser's Console.
    /// </summary>
    public sealed class PendingControllerPurchaseState
    {
        private readonly List<TabletopObjectId> selectedCardIds = new List<TabletopObjectId>();
        private readonly HashSet<TabletopObjectId> insertedCardIds = new HashSet<TabletopObjectId>();

        public PendingControllerPurchaseState(MatchId matchId)
        {
            if (matchId.IsEmpty)
                throw new ArgumentException("Pending purchase Match ID cannot be empty.", nameof(matchId));
            MatchId = matchId;
        }

        public MatchId MatchId { get; }
        public bool IsActive { get; private set; }
        public PlayerId PlayerId { get; private set; }
        public string PurchasedCardDefinitionStableId { get; private set; } = string.Empty;
        public PendingPurchaseSource Source { get; private set; }
        public IReadOnlyList<TabletopObjectId> SelectedCardIds => selectedCardIds.AsReadOnly();
        public bool IsPaymentComplete => IsActive
            && selectedCardIds.Count > 0
            && insertedCardIds.Count == selectedCardIds.Count;

        public bool IsInserted(TabletopObjectId cardId) => insertedCardIds.Contains(cardId);

        public void Begin(
            PlayerId playerId,
            string purchasedCardDefinitionStableId,
            IEnumerable<TabletopObjectId> paymentCardIds,
            PendingPurchaseSource source = PendingPurchaseSource.Shop)
        {
            if (playerId.IsEmpty)
                throw new ArgumentException("Pending purchase Player ID cannot be empty.", nameof(playerId));
            if (string.IsNullOrWhiteSpace(purchasedCardDefinitionStableId))
                throw new ArgumentException(
                    "Pending purchase requires a purchased Card Definition ID.",
                    nameof(purchasedCardDefinitionStableId));
            if (paymentCardIds == null) throw new ArgumentNullException(nameof(paymentCardIds));

            List<TabletopObjectId> payment = new List<TabletopObjectId>();
            HashSet<TabletopObjectId> unique = new HashSet<TabletopObjectId>();
            foreach (TabletopObjectId cardId in paymentCardIds)
            {
                if (cardId.IsEmpty || !unique.Add(cardId))
                    throw new ArgumentException(
                        "Pending purchase payment Card IDs must be non-empty and unique.",
                        nameof(paymentCardIds));
                payment.Add(cardId);
            }
            if (payment.Count == 0)
                throw new ArgumentException(
                    "Pending purchase requires at least one payment Card.",
                    nameof(paymentCardIds));

            selectedCardIds.Clear();
            selectedCardIds.AddRange(payment);
            insertedCardIds.Clear();
            PlayerId = playerId;
            PurchasedCardDefinitionStableId = purchasedCardDefinitionStableId;
            Source = source;
            IsActive = true;
        }

        public bool RecordConsoleInsertion(
            PlayerId actorPlayerId,
            PlayerId consoleOwnerPlayerId,
            ContainerId slotContainerId,
            TabletopObjectId cardId,
            IReadOnlyList<ContainerId> allowedSideSlotIds)
        {
            if (!IsMatchingInteraction(
                    actorPlayerId,
                    consoleOwnerPlayerId,
                    slotContainerId,
                    cardId,
                    allowedSideSlotIds,
                    true))
                return false;
            return insertedCardIds.Add(cardId);
        }

        public bool RecordConsoleRemoval(
            PlayerId actorPlayerId,
            PlayerId consoleOwnerPlayerId,
            ContainerId slotContainerId,
            TabletopObjectId cardId,
            IReadOnlyList<ContainerId> allowedSideSlotIds)
        {
            if (!IsMatchingInteraction(
                    actorPlayerId,
                    consoleOwnerPlayerId,
                    slotContainerId,
                    cardId,
                    allowedSideSlotIds,
                    false))
                return false;
            return insertedCardIds.Remove(cardId);
        }

        public void Clear()
        {
            selectedCardIds.Clear();
            insertedCardIds.Clear();
            PlayerId = ConsoleCards.Core.Identifiers.PlayerId.Empty;
            PurchasedCardDefinitionStableId = string.Empty;
            Source = PendingPurchaseSource.Shop;
            IsActive = false;
        }

        internal TabletopObjectId[] CopySelectedCardIds() => selectedCardIds.ToArray();

        internal TabletopObjectId[] CopyInsertedCardIds()
        {
            List<TabletopObjectId> copy = new List<TabletopObjectId>();
            for (int i = 0; i < selectedCardIds.Count; i++)
                if (insertedCardIds.Contains(selectedCardIds[i])) copy.Add(selectedCardIds[i]);
            return copy.ToArray();
        }

        internal void Restore(
            bool isActive,
            PlayerId playerId,
            string purchasedCardDefinitionStableId,
            IEnumerable<TabletopObjectId> paymentCardIds,
            IEnumerable<TabletopObjectId> insertedPaymentCardIds,
            PendingPurchaseSource source = PendingPurchaseSource.Shop)
        {
            Clear();
            if (!isActive) return;
            Begin(playerId, purchasedCardDefinitionStableId, paymentCardIds, source);
            if (insertedPaymentCardIds == null) return;
            foreach (TabletopObjectId cardId in insertedPaymentCardIds)
            {
                if (selectedCardIds.Contains(cardId)) insertedCardIds.Add(cardId);
            }
        }

        private bool IsMatchingInteraction(
            PlayerId actorPlayerId,
            PlayerId consoleOwnerPlayerId,
            ContainerId slotContainerId,
            TabletopObjectId cardId,
            IReadOnlyList<ContainerId> allowedSideSlotIds,
            bool requirePendingPlayerActor)
        {
            if (!IsActive
                || (requirePendingPlayerActor && actorPlayerId != PlayerId)
                || consoleOwnerPlayerId != PlayerId
                || cardId.IsEmpty
                || !selectedCardIds.Contains(cardId)
                || allowedSideSlotIds == null)
                return false;
            for (int i = 0; i < allowedSideSlotIds.Count; i++)
                if (allowedSideSlotIds[i] == slotContainerId) return true;
            return false;
        }
    }

    public sealed class PendingControllerPurchaseStateSnapshot
    {
        private readonly bool isActive;
        private readonly PlayerId playerId;
        private readonly string purchasedCardDefinitionStableId;
        private readonly TabletopObjectId[] selectedCardIds;
        private readonly TabletopObjectId[] insertedCardIds;
        private readonly PendingPurchaseSource source;

        private PendingControllerPurchaseStateSnapshot(PendingControllerPurchaseState state)
        {
            source = state.Source;
            MatchId = state.MatchId;
            isActive = state.IsActive;
            playerId = state.PlayerId;
            purchasedCardDefinitionStableId = state.PurchasedCardDefinitionStableId;
            selectedCardIds = state.CopySelectedCardIds();
            insertedCardIds = state.CopyInsertedCardIds();
        }

        public MatchId MatchId { get; }

        public static PendingControllerPurchaseStateSnapshot Capture(
            PendingControllerPurchaseState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new PendingControllerPurchaseStateSnapshot(state);
        }

        public PendingControllerPurchaseState Restore()
        {
            PendingControllerPurchaseState state = new PendingControllerPurchaseState(MatchId);
            state.Restore(
                isActive,
                playerId,
                purchasedCardDefinitionStableId,
                selectedCardIds,
                insertedCardIds,
                source);
            return state;
        }
    }
}
