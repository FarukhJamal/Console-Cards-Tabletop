using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.Games.TrapFloor
{
    public enum TrapFloorShopError
    {
        None = 0,
        MatchRequired = 1,
        CommandRequired = 2,
        MatchIdMismatch = 3,
        MatchRevisionConflict = 4,
        MatchTemplateMismatch = 5,
        ActorIsNotActivePlayer = 6,
        PlayerSetupMissing = 7,
        NotInShop = 8,
        NotForSale = 9,
        CardDefinitionIdInvalid = 10,
        GrantedCardIdAlreadyExists = 11,
        PaymentInvalid = 12,
        PaymentCardNotInConsole = 13,
        PaymentCardLocked = 14,
        DestinationMissing = 15,
        RevisionOverflow = 16,
        MutationFailed = 17,
        SourceCardNotInAbilityDeck = 18,
    }

    /// <summary>
    /// The Trap Floor Shop (doc 18 §15.4, owner 2026-10-15): every Ability of the game, bought on your own turn by
    /// paying its cost from your Hand through your Console. Apart from taking an Ability from the floor; whether the
    /// Shop is open is the rule setting <see cref="RuleSettingKeys.ShopEnabled"/>, read by the game.
    /// </summary>
    public static class TrapFloorShop
    {
        public const string AbilityCategory = "Ability";
        public const string ActionTag = "action";
        public const string ReactionTag = "reaction";

        /// <summary>An Ability card the Shop lists (Controller Cards and other cards are not sold).</summary>
        public static bool IsShopCard(CardDefinitionData card)
        {
            return card != null
                && !card.RepresentedControllerInput.HasValue
                && string.Equals(card.Category, AbilityCategory, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A listed Ability with a price; one without a cost yet is shown but not for sale.</summary>
        public static bool IsForSale(CardDefinitionData card)
        {
            return IsShopCard(card) && card.InputCost != null && !card.InputCost.IsEmpty;
        }

        public static bool IsAction(CardDefinitionData card) => HasTag(card, ActionTag);

        public static bool IsReaction(CardDefinitionData card) => HasTag(card, ReactionTag);

        /// <summary>Every Ability of the game once each: Actions first, then Reactions, then any other, by name.</summary>
        public static List<CardDefinitionData> Catalog(GameDefinitionData game)
        {
            List<CardDefinitionData> cards = new List<CardDefinitionData>();
            if (game == null) return cards;
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < game.Cards.Count; i++)
            {
                CardDefinitionData card = game.Cards[i];
                if (IsShopCard(card) && seen.Add(card.StableId)) cards.Add(card);
            }

            cards.Sort((a, b) =>
            {
                int byKind = KindOrder(a).CompareTo(KindOrder(b));
                return byKind != 0 ? byKind : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });
            return cards;
        }

        private static int KindOrder(CardDefinitionData card) => IsAction(card) ? 0 : IsReaction(card) ? 1 : 2;

        private static bool HasTag(CardDefinitionData card, string tag)
        {
            if (card == null) return false;
            for (int i = 0; i < card.Tags.Count; i++)
            {
                if (string.Equals(card.Tags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }

    public sealed class TrapFloorBuyFromShopCommand : ITabletopCommand
    {
        public TrapFloorBuyFromShopCommand(
            CommandContext context,
            string cardDefinitionStableId,
            TabletopObjectId grantedCardId,
            IEnumerable<TabletopObjectId> paymentCardIds)
            : this(context, cardDefinitionStableId, grantedCardId, paymentCardIds, TabletopObjectId.Empty)
        {
        }

        /// <summary>ABIL-DECK: with a source card, that card is bought out of the table's Ability deck (no new card).</summary>
        public TrapFloorBuyFromShopCommand(
            CommandContext context,
            string cardDefinitionStableId,
            TabletopObjectId grantedCardId,
            IEnumerable<TabletopObjectId> paymentCardIds,
            TabletopObjectId sourceCardId)
        {
            SourceCardId = sourceCardId;
            if (string.IsNullOrWhiteSpace(cardDefinitionStableId))
                throw new ArgumentException("A Shop purchase needs a Card Definition ID.", nameof(cardDefinitionStableId));
            if (grantedCardId.IsEmpty) throw new ArgumentException("Granted Card ID cannot be empty.", nameof(grantedCardId));
            Context = context;
            CardDefinitionStableId = cardDefinitionStableId;
            GrantedCardId = grantedCardId;
            PaymentCardIds = new ReadOnlyCollection<TabletopObjectId>(
                new List<TabletopObjectId>(paymentCardIds ?? throw new ArgumentNullException(nameof(paymentCardIds))));
        }

        public CommandContext Context { get; }
        public string CardDefinitionStableId { get; }
        public TabletopObjectId GrantedCardId { get; }
        public IReadOnlyList<TabletopObjectId> PaymentCardIds { get; }
        public TabletopObjectId SourceCardId { get; }
    }

    public readonly struct TrapFloorBuyFromShopResult
    {
        private TrapFloorBuyFromShopResult(
            CommandResultStatus status,
            long revision,
            CardDefinitionData card,
            TabletopObjectId grantedCardId,
            ContainerId destinationContainerId,
            bool placedInHand,
            TrapFloorShopError error)
        {
            Status = status;
            Revision = revision;
            Card = card;
            GrantedCardId = grantedCardId;
            DestinationContainerId = destinationContainerId;
            PlacedInHand = placedInHand;
            Error = error;
        }

        public CommandResultStatus Status { get; }
        public bool Succeeded => Status == CommandResultStatus.Accepted;
        public long Revision { get; }
        public CardDefinitionData Card { get; }
        public TabletopObjectId GrantedCardId { get; }
        public ContainerId DestinationContainerId { get; }

        /// <summary>False when the Hand was at its limit and the card went to the Action stack beside the Console.</summary>
        public bool PlacedInHand { get; }

        public TrapFloorShopError Error { get; }

        internal static TrapFloorBuyFromShopResult Accepted(
            long revision,
            CardDefinitionData card,
            TabletopObjectId grantedCardId,
            ContainerId destinationContainerId,
            bool placedInHand) =>
            new TrapFloorBuyFromShopResult(
                CommandResultStatus.Accepted, revision, card, grantedCardId, destinationContainerId, placedInHand,
                TrapFloorShopError.None);

        internal static TrapFloorBuyFromShopResult Failure(CommandResultStatus status, TrapFloorShopError error) =>
            new TrapFloorBuyFromShopResult(status, -1, null, TabletopObjectId.Empty, ContainerId.Empty, false, error);
    }

    /// <summary>
    /// Buys one Ability from the Shop for the active Player (doc 18 §15.4). The exact payment must be in the
    /// Player's Console side slots, as when taking an Ability from the floor. The new card goes to the Player's
    /// Hand, or to the Action stack beside the Console when the Hand is at its limit. Payment cards stay where they
    /// were placed, like every payment.
    /// </summary>
    public sealed class TrapFloorBuyFromShopUseCase
    {
        private readonly TrapFloorTemplateDefinition template;
        private readonly TrapFloorTurnState turnState;

        public TrapFloorBuyFromShopUseCase(TrapFloorTemplateDefinition template, TrapFloorTurnState turnState)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            this.turnState = turnState ?? throw new ArgumentNullException(nameof(turnState));
        }

        public TrapFloorBuyFromShopResult Execute(MatchState matchState, TrapFloorBuyFromShopCommand command)
        {
            if (matchState == null) return Failure(CommandResultStatus.Invalid, TrapFloorShopError.MatchRequired);
            if (command == null) return Failure(CommandResultStatus.Invalid, TrapFloorShopError.CommandRequired);
            if (command.Context.MatchId != matchState.Id)
                return Failure(CommandResultStatus.Invalid, TrapFloorShopError.MatchIdMismatch);
            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
                return Failure(CommandResultStatus.Conflict, TrapFloorShopError.MatchRevisionConflict);
            if (matchState.GameTemplateId != template.Template.Id || turnState.MatchId != matchState.Id)
                return Failure(CommandResultStatus.Rejected, TrapFloorShopError.MatchTemplateMismatch);

            PlayerId actor = command.Context.RequestedByPlayerId;
            if (turnState.IsCurrentFloorFailed
                || turnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || turnState.ActivePlayerId != actor)
                return Failure(CommandResultStatus.Rejected, TrapFloorShopError.ActorIsNotActivePlayer);
            if (!TryGetPlayerSetup(matchState, actor, out TrapFloorPlayerSetupDefinition player))
                return Failure(CommandResultStatus.Rejected, TrapFloorShopError.PlayerSetupMissing);
            if (!template.GameDefinition.TryGetCard(command.CardDefinitionStableId, out CardDefinitionData card)
                || !TrapFloorShop.IsShopCard(card))
                return Failure(CommandResultStatus.Rejected, TrapFloorShopError.NotInShop);
            if (!TrapFloorShop.IsForSale(card))
                return Failure(CommandResultStatus.Rejected, TrapFloorShopError.NotForSale);
            if (!Guid.TryParse(card.StableId, out Guid definitionGuid))
                return Failure(CommandResultStatus.Rejected, TrapFloorShopError.CardDefinitionIdInvalid);
            bool fromDeck = !command.SourceCardId.IsEmpty;
            ContainerState abilityDeck = null;
            CardInstanceState deckCard = null;
            if (fromDeck)
            {
                if (template.AbilityDeckId.IsEmpty
                    || !matchState.Containers.TryGetValue(template.AbilityDeckId, out abilityDeck)
                    || !abilityDeck.Contains(command.SourceCardId)
                    || !matchState.Cards.TryGetValue(command.SourceCardId, out deckCard)
                    || deckCard.BaseState.DefinitionId != new ObjectDefinitionId(definitionGuid))
                    return Failure(CommandResultStatus.Rejected, TrapFloorShopError.SourceCardNotInAbilityDeck);
            }
            else if (matchState.ContainsObject(command.GrantedCardId))
                return Failure(CommandResultStatus.Rejected, TrapFloorShopError.GrantedCardIdAlreadyExists);

            ControllerInputCostEvaluation payment = new ControllerInputCostEvaluator().EvaluateExact(
                matchState,
                command.PaymentCardIds,
                card.InputCost,
                template.GameDefinition);
            if (!payment.CanPay) return Failure(CommandResultStatus.Rejected, TrapFloorShopError.PaymentInvalid);
            for (int i = 0; i < command.PaymentCardIds.Count; i++)
            {
                CardInstanceState paid = matchState.Cards[command.PaymentCardIds[i]];
                if (!TrapFloorTakePrizeUseCase.IsCardInPlayerSideSlot(matchState, player, paid))
                    return Failure(CommandResultStatus.Rejected, TrapFloorShopError.PaymentCardNotInConsole);
                if (paid.BaseState.IsUserLocked)
                    return Failure(CommandResultStatus.Rejected, TrapFloorShopError.PaymentCardLocked);
            }

            if (!TrapFloorTakePrizeUseCase.TryResolveDestination(
                    template, matchState, player, out ContainerState destination, out bool placedInHand))
                return Failure(CommandResultStatus.Rejected, TrapFloorShopError.DestinationMissing);
            if (matchState.Revision == long.MaxValue)
                return Failure(CommandResultStatus.Conflict, TrapFloorShopError.RevisionOverflow);

            if (fromDeck)
            {
                // The drawn card itself leaves the deck for the buyer (face up, theirs): into the Hand, or (TABLE-1)
                // loose beside the Console when the Hand is full.
                ContainerTransferService deckTransfer = new ContainerTransferService();
                ContainerTransferResult moved = destination != null
                    ? deckTransfer.MoveBetweenContainers(deckCard.BaseState, abilityDeck, destination)
                    : deckTransfer.RemoveFromContainer(deckCard.BaseState, abilityDeck);
                if (!moved.Succeeded) return Failure(CommandResultStatus.Rejected, TrapFloorShopError.MutationFailed);
                if (destination == null) deckCard.BaseState.SetPose(player.AbilityOverflowPose);
                deckCard.BaseState.SetOwner(actor);
                deckCard.BaseState.SetVisibility(destination != null ? destination.Visibility : ObjectVisibility.Public);
                deckCard.SetFace(CardFace.FaceUp);
                TrapFloorTakePrizeUseCase.ReturnPaymentToControllerDeck(matchState, player, command.PaymentCardIds);
                long deckRevision = matchState.AdvanceRevision(
                    command.Context.Id,
                    actor,
                    AuthoritativeActionKind.PurchaseActionOrAbility);
                return TrapFloorBuyFromShopResult.Accepted(
                    deckRevision, card, command.SourceCardId, destination != null ? destination.Id : ContainerId.Empty, placedInHand);
            }

            CardInstanceState boughtCard = new CardInstanceState(
                new TabletopObjectState(
                    command.GrantedCardId,
                    new ObjectDefinitionId(definitionGuid),
                    TabletopObjectKind.Card,
                    destination != null ? TabletopPose.Default : player.AbilityOverflowPose,
                    ContainerId.Empty,
                    actor,
                    destination != null ? destination.Visibility : ObjectVisibility.Public,
                    false),
                CardFace.FaceUp);
            ContainerTransferService transfer = new ContainerTransferService();
            try
            {
                matchState.AddUncontainedCard(boughtCard);
                if (destination != null)
                {
                    ContainerTransferResult placement = transfer.PlaceIntoContainer(boughtCard.BaseState, destination);
                    if (!placement.Succeeded)
                        throw new InvalidOperationException($"Shop card placement failed: {placement.Error}.");
                }
            }
            catch
            {
                if (matchState.ContainsObject(boughtCard.BaseState.Id))
                {
                    if (destination != null
                        && boughtCard.BaseState.ContainerId == destination.Id
                        && destination.Contains(boughtCard.BaseState.Id))
                        transfer.RemoveFromContainer(boughtCard.BaseState, destination);
                    matchState.RemoveObject(boughtCard.BaseState.Id);
                }

                return Failure(CommandResultStatus.Rejected, TrapFloorShopError.MutationFailed);
            }

            TrapFloorTakePrizeUseCase.ReturnPaymentToControllerDeck(matchState, player, command.PaymentCardIds);
            long revision = matchState.AdvanceRevision(
                command.Context.Id,
                actor,
                AuthoritativeActionKind.PurchaseActionOrAbility);
            return TrapFloorBuyFromShopResult.Accepted(
                revision, card, command.GrantedCardId, destination != null ? destination.Id : ContainerId.Empty, placedInHand);
        }

        private bool TryGetPlayerSetup(MatchState matchState, PlayerId playerId, out TrapFloorPlayerSetupDefinition resolved)
        {
            for (int i = 0; i < template.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition player = template.Players[i];
                if (matchState.Seats.TryGetValue(player.SeatId, out SeatState seat) && seat.OccupantPlayerId == playerId)
                {
                    resolved = player;
                    return true;
                }
            }

            resolved = null;
            return false;
        }

        private static TrapFloorBuyFromShopResult Failure(CommandResultStatus status, TrapFloorShopError error) =>
            TrapFloorBuyFromShopResult.Failure(status, error);
    }
}
