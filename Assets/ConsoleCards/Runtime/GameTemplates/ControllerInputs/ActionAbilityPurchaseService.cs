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
    public enum ActionAbilityPurchaseError
    {
        None = 0,
        MatchMissing = 1,
        GameDefinitionMissing = 2,
        CommandMissing = 3,
        MatchMismatch = 4,
        RevisionConflict = 5,
        SeatMissing = 6,
        ActorDoesNotOwnSeat = 7,
        HandMissing = 8,
        ActionAreaMissing = 9,
        ActionAreaOwnedByAnotherSeat = 10,
        InvalidActionAreaKind = 11,
        ActionAreaFull = 12,
        CardDefinitionMissing = 13,
        CardDefinitionIdInvalid = 14,
        CardNotPurchasable = 15,
        GrantedCardIdAlreadyExists = 16,
        UnsupportedPaymentConfiguration = 17,
        InvalidControllerCardDefinition = 18,
        HandObjectInvalid = 19,
        CannotAfford = 20,
        PaymentCardLocked = 21,
        RevisionOverflow = 22,
        MutationFailed = 23,
        PhysicalPaymentMissing = 24,
        PhysicalPaymentInvalid = 25,
        PaymentCardNotInConsole = 26,
    }

    public sealed class ConfirmPhysicalActionOrAbilityPurchaseCommand
    {
        private readonly ReadOnlyCollection<TabletopObjectId> paymentCardIds;
        private readonly ReadOnlyCollection<ContainerId> consoleSideSlotContainerIds;

        public ConfirmPhysicalActionOrAbilityPurchaseCommand(
            CommandContext context,
            SeatId playerSeatId,
            ContainerId actionAreaContainerId,
            string cardDefinitionStableId,
            TabletopObjectId grantedCardId,
            IReadOnlyList<TabletopObjectId> selectedPaymentCardIds,
            IReadOnlyList<ContainerId> allowedConsoleSideSlotContainerIds)
        {
            if (playerSeatId.IsEmpty) throw new ArgumentException("Player Seat ID is required.", nameof(playerSeatId));
            if (actionAreaContainerId.IsEmpty) throw new ArgumentException("Action Area Container ID is required.", nameof(actionAreaContainerId));
            if (string.IsNullOrWhiteSpace(cardDefinitionStableId)) throw new ArgumentException("Card Definition ID is required.", nameof(cardDefinitionStableId));
            if (grantedCardId.IsEmpty) throw new ArgumentException("Granted Card ID is required.", nameof(grantedCardId));
            if (selectedPaymentCardIds == null) throw new ArgumentNullException(nameof(selectedPaymentCardIds));
            if (allowedConsoleSideSlotContainerIds == null) throw new ArgumentNullException(nameof(allowedConsoleSideSlotContainerIds));
            if (selectedPaymentCardIds.Count == 0)
                throw new ArgumentException("Physical purchase confirmation requires payment Cards.", nameof(selectedPaymentCardIds));
            if (allowedConsoleSideSlotContainerIds.Count == 0)
                throw new ArgumentException("Physical purchase confirmation requires Console Side Slots.", nameof(allowedConsoleSideSlotContainerIds));

            Context = context;
            PlayerSeatId = playerSeatId;
            ActionAreaContainerId = actionAreaContainerId;
            CardDefinitionStableId = cardDefinitionStableId;
            GrantedCardId = grantedCardId;
            paymentCardIds = new ReadOnlyCollection<TabletopObjectId>(
                new List<TabletopObjectId>(selectedPaymentCardIds));
            consoleSideSlotContainerIds = new ReadOnlyCollection<ContainerId>(
                new List<ContainerId>(allowedConsoleSideSlotContainerIds));
        }

        public CommandContext Context { get; }
        public SeatId PlayerSeatId { get; }
        public ContainerId ActionAreaContainerId { get; }
        public string CardDefinitionStableId { get; }
        public TabletopObjectId GrantedCardId { get; }
        public IReadOnlyList<TabletopObjectId> PaymentCardIds => paymentCardIds;
        public IReadOnlyList<ContainerId> ConsoleSideSlotContainerIds => consoleSideSlotContainerIds;
    }

    public sealed class PurchaseActionOrAbilityCommand
    {
        public PurchaseActionOrAbilityCommand(
            CommandContext context,
            SeatId playerSeatId,
            ContainerId actionAreaContainerId,
            string cardDefinitionStableId,
            TabletopObjectId grantedCardId)
        {
            if (playerSeatId.IsEmpty) throw new ArgumentException("Player Seat ID is required.", nameof(playerSeatId));
            if (actionAreaContainerId.IsEmpty) throw new ArgumentException("Action Area Container ID is required.", nameof(actionAreaContainerId));
            if (string.IsNullOrWhiteSpace(cardDefinitionStableId)) throw new ArgumentException("Card Definition ID is required.", nameof(cardDefinitionStableId));
            if (grantedCardId.IsEmpty) throw new ArgumentException("Granted Card ID is required.", nameof(grantedCardId));
            Context = context;
            PlayerSeatId = playerSeatId;
            ActionAreaContainerId = actionAreaContainerId;
            CardDefinitionStableId = cardDefinitionStableId;
            GrantedCardId = grantedCardId;
        }

        public CommandContext Context { get; }
        public SeatId PlayerSeatId { get; }
        public ContainerId ActionAreaContainerId { get; }
        public string CardDefinitionStableId { get; }
        public TabletopObjectId GrantedCardId { get; }
    }

    public sealed class ActionAbilityPurchaseResult
    {
        private static readonly ReadOnlyCollection<TabletopObjectId> EmptyPayment =
            new ReadOnlyCollection<TabletopObjectId>(new List<TabletopObjectId>());

        private ActionAbilityPurchaseResult(
            bool succeeded,
            long revision,
            TabletopObjectId grantedCardId,
            IReadOnlyList<TabletopObjectId> consumedCardIds,
            ActionAbilityPurchaseError error)
        {
            Succeeded = succeeded;
            Revision = revision;
            GrantedCardId = grantedCardId;
            ConsumedCardIds = consumedCardIds;
            Error = error;
        }

        public bool Succeeded { get; }
        public long Revision { get; }
        public TabletopObjectId GrantedCardId { get; }
        public IReadOnlyList<TabletopObjectId> ConsumedCardIds { get; }
        public ActionAbilityPurchaseError Error { get; }

        public static ActionAbilityPurchaseResult Accepted(
            long revision,
            TabletopObjectId grantedCardId,
            IReadOnlyList<TabletopObjectId> consumedCardIds)
        {
            return new ActionAbilityPurchaseResult(
                true,
                revision,
                grantedCardId,
                new ReadOnlyCollection<TabletopObjectId>(
                    new List<TabletopObjectId>(consumedCardIds ?? throw new ArgumentNullException(nameof(consumedCardIds)))),
                ActionAbilityPurchaseError.None);
        }

        public static ActionAbilityPurchaseResult Failure(ActionAbilityPurchaseError error)
        {
            if (error == ActionAbilityPurchaseError.None)
                throw new ArgumentException("A failed purchase requires an error.", nameof(error));
            return new ActionAbilityPurchaseResult(false, -1, TabletopObjectId.Empty, EmptyPayment, error);
        }
    }

    /// <summary>
    /// Validates an authored Controller-input payment and grants one authored Card into the
    /// Player's Action/Ability area. Physical-payment confirmation leaves the payment Cards in
    /// their Console slots; ordinary freeform Card transfer remains independent.
    /// </summary>
    public sealed class ActionAbilityPurchaseService
    {
        public ActionAbilityPurchaseResult ConfirmPhysicalPaymentPurchase(
            MatchState matchState,
            GameDefinitionData gameDefinition,
            ConfirmPhysicalActionOrAbilityPurchaseCommand command)
        {
            if (matchState == null)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.MatchMissing);
            if (gameDefinition == null)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.GameDefinitionMissing);
            if (command == null)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.CommandMissing);
            if (command.Context.MatchId != matchState.Id)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.MatchMismatch);
            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.RevisionConflict);
            }

            if (!matchState.Seats.TryGetValue(command.PlayerSeatId, out SeatState seat))
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.SeatMissing);
            if (seat.OccupantPlayerId != command.Context.RequestedByPlayerId)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.ActorDoesNotOwnSeat);
            if (!matchState.Containers.TryGetValue(command.ActionAreaContainerId, out ContainerState actionArea))
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.ActionAreaMissing);
            if (actionArea.OwnerSeatId != command.PlayerSeatId)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.ActionAreaOwnedByAnotherSeat);
            if (actionArea.Kind != ContainerKind.Stack && actionArea.Kind != ContainerKind.Generic)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.InvalidActionAreaKind);
            if (actionArea.IsFull)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.ActionAreaFull);
            if (!gameDefinition.TryGetCard(command.CardDefinitionStableId, out CardDefinitionData purchasedDefinition))
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.CardDefinitionMissing);
            if (!Guid.TryParse(purchasedDefinition.StableId, out Guid parsedDefinitionId))
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.CardDefinitionIdInvalid);
            if (purchasedDefinition.Quantity < 1)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.CardNotPurchasable);
            if (matchState.ContainsObject(command.GrantedCardId))
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.GrantedCardIdAlreadyExists);

            ControllerConfigurationData controllerConfiguration = gameDefinition.ControllerConfiguration;
            if (controllerConfiguration == null
                || !controllerConfiguration.CostsAreAllOrNothing
                || controllerConfiguration.SharedPaymentAllowed)
            {
                return ActionAbilityPurchaseResult.Failure(
                    ActionAbilityPurchaseError.UnsupportedPaymentConfiguration);
            }

            ControllerInputCostEvaluation payment = new ControllerInputCostEvaluator().EvaluateExact(
                matchState,
                command.PaymentCardIds,
                purchasedDefinition.InputCost,
                gameDefinition);
            if (!payment.CanPay)
                return ActionAbilityPurchaseResult.Failure(
                    payment.Error == ControllerInputCostEvaluationError.InvalidControllerCardDefinition
                        ? ActionAbilityPurchaseError.InvalidControllerCardDefinition
                        : ActionAbilityPurchaseError.PhysicalPaymentInvalid);
            if (!ValidatePhysicalPaymentContainers(matchState, command))
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.PaymentCardNotInConsole);
            for (int i = 0; i < payment.PaymentCardIds.Count; i++)
            {
                if (matchState.Cards[payment.PaymentCardIds[i]].BaseState.IsUserLocked)
                    return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.PaymentCardLocked);
            }

            if (matchState.Revision == long.MaxValue)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.RevisionOverflow);

            CardInstanceState grantedCard = new CardInstanceState(
                new TabletopObjectState(
                    command.GrantedCardId,
                    new ObjectDefinitionId(parsedDefinitionId),
                    TabletopObjectKind.Card,
                    TabletopPose.Default,
                    ContainerId.Empty,
                    command.Context.RequestedByPlayerId,
                    actionArea.Visibility,
                    false),
                CardFace.FaceUp);
            ContainerTransferService transfer = new ContainerTransferService();
            try
            {
                matchState.AddUncontainedCard(grantedCard);
                ContainerTransferResult placement = transfer.PlaceIntoContainer(grantedCard.BaseState, actionArea);
                if (!placement.Succeeded)
                    throw new InvalidOperationException($"Purchased Card placement failed: {placement.Error}.");
            }
            catch
            {
                if (matchState.ContainsObject(grantedCard.BaseState.Id))
                {
                    if (grantedCard.BaseState.ContainerId == actionArea.Id
                        && actionArea.Contains(grantedCard.BaseState.Id))
                        transfer.RemoveFromContainer(grantedCard.BaseState, actionArea);
                    matchState.RemoveObject(grantedCard.BaseState.Id);
                }
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.MutationFailed);
            }

            long revision = matchState.AdvanceRevision(
                command.Context.Id,
                command.Context.RequestedByPlayerId,
                AuthoritativeActionKind.PurchaseActionOrAbility);
            return ActionAbilityPurchaseResult.Accepted(
                revision,
                command.GrantedCardId,
                Array.Empty<TabletopObjectId>());
        }

        public ActionAbilityPurchaseResult Purchase(
            MatchState matchState,
            GameDefinitionData gameDefinition,
            PurchaseActionOrAbilityCommand command)
        {
            if (matchState == null)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.MatchMissing);
            if (gameDefinition == null)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.GameDefinitionMissing);
            if (command == null)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.CommandMissing);
            if (command.Context.MatchId != matchState.Id)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.MatchMismatch);
            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.RevisionConflict);
            }

            if (!matchState.Seats.TryGetValue(command.PlayerSeatId, out SeatState seat))
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.SeatMissing);
            if (seat.OccupantPlayerId != command.Context.RequestedByPlayerId)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.ActorDoesNotOwnSeat);
            if (!matchState.TryGetSeatHand(command.PlayerSeatId, out ContainerState hand))
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.HandMissing);
            if (!matchState.Containers.TryGetValue(command.ActionAreaContainerId, out ContainerState actionArea))
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.ActionAreaMissing);
            if (actionArea.OwnerSeatId != command.PlayerSeatId)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.ActionAreaOwnedByAnotherSeat);
            if (actionArea.Kind != ContainerKind.Stack && actionArea.Kind != ContainerKind.Generic)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.InvalidActionAreaKind);
            if (actionArea.IsFull)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.ActionAreaFull);
            if (!gameDefinition.TryGetCard(command.CardDefinitionStableId, out CardDefinitionData purchasedDefinition))
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.CardDefinitionMissing);
            if (!Guid.TryParse(purchasedDefinition.StableId, out Guid parsedDefinitionId))
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.CardDefinitionIdInvalid);
            if (purchasedDefinition.Quantity < 1)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.CardNotPurchasable);
            if (matchState.ContainsObject(command.GrantedCardId))
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.GrantedCardIdAlreadyExists);

            ControllerConfigurationData controllerConfiguration = gameDefinition.ControllerConfiguration;
            if (controllerConfiguration == null
                || !controllerConfiguration.CostsAreAllOrNothing
                || controllerConfiguration.SharedPaymentAllowed)
            {
                return ActionAbilityPurchaseResult.Failure(
                    ActionAbilityPurchaseError.UnsupportedPaymentConfiguration);
            }

            ControllerInputCostEvaluation evaluation = new ControllerInputCostEvaluator().Evaluate(
                matchState,
                hand,
                purchasedDefinition.InputCost,
                gameDefinition);
            if (!evaluation.CanPay)
                return ActionAbilityPurchaseResult.Failure(MapCostError(evaluation.Error));
            for (int i = 0; i < evaluation.PaymentCardIds.Count; i++)
            {
                if (matchState.Cards[evaluation.PaymentCardIds[i]].BaseState.IsUserLocked)
                    return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.PaymentCardLocked);
            }

            if (matchState.Revision == long.MaxValue)
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.RevisionOverflow);

            CardInstanceState grantedCard = new CardInstanceState(
                new TabletopObjectState(
                    command.GrantedCardId,
                    new ObjectDefinitionId(parsedDefinitionId),
                    TabletopObjectKind.Card,
                    TabletopPose.Default,
                    ContainerId.Empty,
                    command.Context.RequestedByPlayerId,
                    actionArea.Visibility,
                    false),
                CardFace.FaceUp);
            List<CardInstanceState> paymentCards = ResolvePaymentCards(matchState, evaluation.PaymentCardIds);
            List<TabletopObjectId> originalHandOrder = new List<TabletopObjectId>(hand.ObjectIds);
            ContainerTransferService transfer = new ContainerTransferService();

            try
            {
                for (int i = 0; i < paymentCards.Count; i++)
                {
                    CardInstanceState paymentCard = paymentCards[i];
                    ContainerTransferResult removal = transfer.RemoveFromContainer(paymentCard.BaseState, hand);
                    if (!removal.Succeeded) throw new InvalidOperationException($"Controller payment removal failed: {removal.Error}.");
                    matchState.RemoveObject(paymentCard.BaseState.Id);
                }

                matchState.AddUncontainedCard(grantedCard);
                ContainerTransferResult placement = transfer.PlaceIntoContainer(grantedCard.BaseState, actionArea);
                if (!placement.Succeeded) throw new InvalidOperationException($"Purchased Card placement failed: {placement.Error}.");

            }
            catch
            {
                RollBackMutation(matchState, hand, actionArea, grantedCard, paymentCards, originalHandOrder, transfer);
                return ActionAbilityPurchaseResult.Failure(ActionAbilityPurchaseError.MutationFailed);
            }

            long revision = matchState.AdvanceRevision(
                command.Context.Id,
                command.Context.RequestedByPlayerId,
                AuthoritativeActionKind.PurchaseActionOrAbility);
            return ActionAbilityPurchaseResult.Accepted(
                revision,
                command.GrantedCardId,
                evaluation.PaymentCardIds);
        }

        private static bool ValidatePhysicalPaymentContainers(
            MatchState matchState,
            ConfirmPhysicalActionOrAbilityPurchaseCommand command)
        {
            HashSet<ContainerId> allowed = new HashSet<ContainerId>();
            for (int i = 0; i < command.ConsoleSideSlotContainerIds.Count; i++)
            {
                ContainerId containerId = command.ConsoleSideSlotContainerIds[i];
                if (containerId.IsEmpty
                    || !allowed.Add(containerId)
                    || !matchState.Containers.TryGetValue(containerId, out ContainerState slot)
                    || slot.Kind != ContainerKind.ConsoleSlot
                    || slot.OwnerSeatId != command.PlayerSeatId)
                    return false;
            }

            for (int i = 0; i < command.PaymentCardIds.Count; i++)
            {
                TabletopObjectId cardId = command.PaymentCardIds[i];
                if (!matchState.Cards.TryGetValue(cardId, out CardInstanceState card)
                    || !allowed.Contains(card.BaseState.ContainerId)
                    || !matchState.Containers[card.BaseState.ContainerId].Contains(cardId))
                    return false;
            }
            return true;
        }

        private static List<CardInstanceState> ResolvePaymentCards(
            MatchState matchState,
            IReadOnlyList<TabletopObjectId> paymentCardIds)
        {
            List<CardInstanceState> cards = new List<CardInstanceState>(paymentCardIds.Count);
            for (int i = 0; i < paymentCardIds.Count; i++) cards.Add(matchState.Cards[paymentCardIds[i]]);
            return cards;
        }

        private static void RollBackMutation(
            MatchState matchState,
            ContainerState hand,
            ContainerState actionArea,
            CardInstanceState grantedCard,
            IReadOnlyList<CardInstanceState> paymentCards,
            IReadOnlyList<TabletopObjectId> originalHandOrder,
            ContainerTransferService transfer)
        {
            if (matchState.ContainsObject(grantedCard.BaseState.Id))
            {
                if (grantedCard.BaseState.ContainerId == actionArea.Id && actionArea.Contains(grantedCard.BaseState.Id))
                    transfer.RemoveFromContainer(grantedCard.BaseState, actionArea);
                matchState.RemoveObject(grantedCard.BaseState.Id);
            }

            for (int i = 0; i < paymentCards.Count; i++)
            {
                CardInstanceState paymentCard = paymentCards[i];
                if (!matchState.ContainsObject(paymentCard.BaseState.Id))
                    matchState.AddUncontainedCard(paymentCard);
            }

            for (int originalIndex = 0; originalIndex < originalHandOrder.Count; originalIndex++)
            {
                TabletopObjectId objectId = originalHandOrder[originalIndex];
                if (!hand.Contains(objectId) && matchState.Cards.TryGetValue(objectId, out CardInstanceState paymentCard))
                {
                    ContainerTransferResult restore = transfer.PlaceIntoContainer(
                        paymentCard.BaseState,
                        hand,
                        originalIndex);
                    if (!restore.Succeeded)
                        throw new InvalidOperationException($"Controller payment rollback failed: {restore.Error}.");
                }
            }
        }

        private static ActionAbilityPurchaseError MapCostError(ControllerInputCostEvaluationError error)
        {
            switch (error)
            {
                case ControllerInputCostEvaluationError.InvalidControllerCardDefinition:
                    return ActionAbilityPurchaseError.InvalidControllerCardDefinition;
                case ControllerInputCostEvaluationError.HandObjectMissing:
                case ControllerInputCostEvaluationError.HandObjectNotCard:
                    return ActionAbilityPurchaseError.HandObjectInvalid;
                case ControllerInputCostEvaluationError.InsufficientInput:
                    return ActionAbilityPurchaseError.CannotAfford;
                default:
                    return ActionAbilityPurchaseError.UnsupportedPaymentConfiguration;
            }
        }
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
        public IReadOnlyList<TabletopObjectId> SelectedCardIds => selectedCardIds.AsReadOnly();
        public bool IsPaymentComplete => IsActive
            && selectedCardIds.Count > 0
            && insertedCardIds.Count == selectedCardIds.Count;

        public bool IsInserted(TabletopObjectId cardId) => insertedCardIds.Contains(cardId);

        public void Begin(
            PlayerId playerId,
            string purchasedCardDefinitionStableId,
            IEnumerable<TabletopObjectId> paymentCardIds)
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
            IEnumerable<TabletopObjectId> insertedPaymentCardIds)
        {
            Clear();
            if (!isActive) return;
            Begin(playerId, purchasedCardDefinitionStableId, paymentCardIds);
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

        private PendingControllerPurchaseStateSnapshot(PendingControllerPurchaseState state)
        {
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
                insertedCardIds);
            return state;
        }
    }
}
