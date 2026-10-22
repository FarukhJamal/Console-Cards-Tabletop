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
    public enum TrapFloorTakePrizeError
    {
        None = 0,
        MatchRequired = 1,
        CommandRequired = 2,
        MatchIdMismatch = 3,
        MatchRevisionConflict = 4,
        MatchTemplateMismatch = 5,
        ActorIsNotActivePlayer = 6,
        PlayerSetupMissing = 7,
        FloorCardMissingOrInvalid = 8,
        FloorNotRevealed = 9,
        FloorIsHole = 10,
        NotAPrize = 11,
        PawnNotOnTile = 12,
        CardDefinitionMissing = 13,
        GrantedCardIdAlreadyExists = 14,
        PaymentInvalid = 15,
        PaymentCardNotInConsole = 16,
        PaymentCardLocked = 17,
        DestinationMissing = 18,
        RevisionOverflow = 19,
        MutationFailed = 20,
    }

    public sealed class TrapFloorTakePrizeCommand : ITabletopCommand
    {
        public TrapFloorTakePrizeCommand(
            CommandContext context,
            TabletopObjectId floorCardId,
            TabletopObjectId grantedCardId,
            IEnumerable<TabletopObjectId> paymentCardIds,
            TableCoordinate pawnPosition)
        {
            if (floorCardId.IsEmpty) throw new ArgumentException("Floor Card ID cannot be empty.", nameof(floorCardId));
            if (grantedCardId.IsEmpty) throw new ArgumentException("Granted Card ID cannot be empty.", nameof(grantedCardId));
            Context = context;
            FloorCardId = floorCardId;
            GrantedCardId = grantedCardId;
            PawnPosition = pawnPosition;
            PaymentCardIds = new ReadOnlyCollection<TabletopObjectId>(
                new List<TabletopObjectId>(paymentCardIds ?? throw new ArgumentNullException(nameof(paymentCardIds))));
        }

        public CommandContext Context { get; }
        public TabletopObjectId FloorCardId { get; }
        public TabletopObjectId GrantedCardId { get; }
        public IReadOnlyList<TabletopObjectId> PaymentCardIds { get; }

        /// <summary>
        /// Where the taker's pawn stands on the table. A dragged pawn's position lives in its physics state, which
        /// only the host can turn into table coordinates, so the host supplies it.
        /// </summary>
        public TableCoordinate PawnPosition { get; }
    }

    /// <summary>PRIZE-2: moves a taken prize's tile card, which no pawn stands on any more, to its taker.</summary>
    public sealed class TrapFloorCollectPrizeCommand : ITabletopCommand
    {
        public TrapFloorCollectPrizeCommand(CommandContext context, TabletopObjectId floorCardId)
        {
            if (floorCardId.IsEmpty) throw new ArgumentException("Floor Card ID cannot be empty.", nameof(floorCardId));
            Context = context;
            FloorCardId = floorCardId;
        }

        public CommandContext Context { get; }
        public TabletopObjectId FloorCardId { get; }
    }

    public readonly struct TrapFloorTakePrizeResult
    {
        private TrapFloorTakePrizeResult(
            CommandResultStatus status,
            long revision,
            TrapFloorFloorCardState floorCard,
            TabletopObjectId grantedCardId,
            ContainerId destinationContainerId,
            bool placedInHand,
            TrapFloorTakePrizeError error)
        {
            Status = status;
            Revision = revision;
            FloorCard = floorCard;
            GrantedCardId = grantedCardId;
            DestinationContainerId = destinationContainerId;
            PlacedInHand = placedInHand;
            Error = error;
        }

        public CommandResultStatus Status { get; }
        public bool Succeeded => Status == CommandResultStatus.Accepted;
        public long Revision { get; }
        public TrapFloorFloorCardState FloorCard { get; }
        public TabletopObjectId GrantedCardId { get; }
        public ContainerId DestinationContainerId { get; }

        /// <summary>False when the Hand was at its limit and the card went to the Action stack beside the Console.</summary>
        public bool PlacedInHand { get; }

        public TrapFloorTakePrizeError Error { get; }

        internal static TrapFloorTakePrizeResult Accepted(
            long revision,
            TrapFloorFloorCardState floorCard,
            TabletopObjectId grantedCardId,
            ContainerId destinationContainerId,
            bool placedInHand) =>
            new TrapFloorTakePrizeResult(
                CommandResultStatus.Accepted,
                revision,
                floorCard,
                grantedCardId,
                destinationContainerId,
                placedInHand,
                TrapFloorTakePrizeError.None);

        internal static TrapFloorTakePrizeResult Failure(CommandResultStatus status, TrapFloorTakePrizeError error) =>
            new TrapFloorTakePrizeResult(status, -1, null, TabletopObjectId.Empty, ContainerId.Empty, false, error);
    }

    /// <summary>
    /// Takes the prize on a revealed Ability or Friend tile (doc 18 §15.4, G2a). The active Player's pawn must stand
    /// on the tile. An Ability with a cost needs the exact payment placed in the Player's Console side slots; a
    /// Friend, or an Ability with no cost yet, is free. PRIZE-1/2 (owner, 2026-10-22): the tile card itself becomes the
    /// taker's, but stays under their pawn (they would fall otherwise); once no pawn stands on it, CollectTakenPrize
    /// moves it to their Hand, or beside the Console when the Hand is at its limit, and the cell is a gap from then on.
    /// </summary>
    public sealed class TrapFloorTakePrizeUseCase
    {
        private readonly TrapFloorTemplateDefinition template;
        private readonly TrapFloorCollapseState collapseState;
        private readonly TrapFloorTurnState turnState;

        public TrapFloorTakePrizeUseCase(
            TrapFloorTemplateDefinition template,
            TrapFloorCollapseState collapseState,
            TrapFloorTurnState turnState)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            this.collapseState = collapseState ?? throw new ArgumentNullException(nameof(collapseState));
            this.turnState = turnState ?? throw new ArgumentNullException(nameof(turnState));
        }

        /// <summary>True for the floor content a Player can take.</summary>
        public static bool IsPrize(TrapFloorFloorContentDefinition content)
        {
            return content != null
                && !content.FlipMovementModifier.HasValue // Overcharged works on flip and is not taken
                && (content.Category == TrapFloorFloorContentCategory.Ability
                    || content.Category == TrapFloorFloorContentCategory.Friend);
        }

        /// <summary>The cost of taking this content; empty for a Friend or an Ability without a cost yet.</summary>
        public static InputCostData CostOf(TrapFloorFloorContentDefinition content)
        {
            return content != null
                && content.Category == TrapFloorFloorContentCategory.Ability
                && content.AuthoredCard != null
                && content.AuthoredCard.InputCost != null
                    ? content.AuthoredCard.InputCost
                    : new InputCostData(Array.Empty<InputRequirementData>());
        }

        public TrapFloorTakePrizeResult Execute(MatchState matchState, TrapFloorTakePrizeCommand command)
        {
            if (matchState == null) return Failure(CommandResultStatus.Invalid, TrapFloorTakePrizeError.MatchRequired);
            if (command == null) return Failure(CommandResultStatus.Invalid, TrapFloorTakePrizeError.CommandRequired);
            if (command.Context.MatchId != matchState.Id)
                return Failure(CommandResultStatus.Invalid, TrapFloorTakePrizeError.MatchIdMismatch);
            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
                return Failure(CommandResultStatus.Conflict, TrapFloorTakePrizeError.MatchRevisionConflict);
            if (matchState.GameTemplateId != template.Template.Id
                || collapseState.MatchId != matchState.Id
                || turnState.MatchId != matchState.Id)
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.MatchTemplateMismatch);

            PlayerId actor = command.Context.RequestedByPlayerId;
            if (turnState.IsCurrentFloorFailed
                || turnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || turnState.ActivePlayerId != actor)
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.ActorIsNotActivePlayer);
            if (!TryGetPlayerSetup(matchState, actor, out TrapFloorPlayerSetupDefinition player))
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.PlayerSetupMissing);

            if (!template.TryGetFloorCardState(matchState, command.FloorCardId, out TrapFloorFloorCardState floorCard))
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.FloorCardMissingOrInvalid);
            if (!floorCard.IsRevealed)
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.FloorNotRevealed);
            if (collapseState.IsCollapsed(command.FloorCardId))
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.FloorIsHole);
            if (!IsPrize(floorCard.Content))
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.NotAPrize);
            if (!matchState.Pawns.ContainsKey(player.PawnId)
                || !template.TryGetFloorCoordinateAt(matchState, command.PawnPosition, out TrapFloorCoordinate pawnTile)
                || pawnTile != floorCard.Coordinate)
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.PawnNotOnTile);
            if (floorCard.Content.AuthoredCard == null)
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.CardDefinitionMissing);
            if (!matchState.Cards.TryGetValue(command.FloorCardId, out CardInstanceState tileCard)
                || !tileCard.BaseState.ContainerId.IsEmpty)
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.FloorCardMissingOrInvalid);

            TrapFloorTakePrizeError paymentError = ValidatePayment(matchState, player, floorCard.Content, command);
            if (paymentError != TrapFloorTakePrizeError.None)
                return Failure(CommandResultStatus.Rejected, paymentError);

            if (matchState.Revision == long.MaxValue)
                return Failure(CommandResultStatus.Conflict, TrapFloorTakePrizeError.RevisionOverflow);

            // PRIZE-2 (owner, 2026-10-22: "the card from the floor is removed when the player moves next, in his next
            // turn, otherwise that player will fall"): taking pays and makes the card the taker's, but the tile stays
            // under their pawn. CollectTakenPrize moves it to their Hand once no pawn stands on it.
            long revision = checked(matchState.Revision + 1L);
            collapseState.RecordTakenPrize(command.FloorCardId, floorCard.Coordinate, actor, revision);
            tileCard.BaseState.SetOwner(actor);
            ReturnPaymentToControllerDeck(matchState, player, command.PaymentCardIds);
            matchState.AdvanceRevision(command.Context.Id, actor, AuthoritativeActionKind.PurchaseActionOrAbility);
            return TrapFloorTakePrizeResult.Accepted(
                revision,
                floorCard,
                command.FloorCardId,
                ContainerId.Empty,
                false);
        }

        /// <summary>
        /// PRIZE-2: a taken prize whose tile no pawn stands on any more leaves the floor for its taker: the tile card
        /// itself goes into their Hand (or beside their Console when the Hand is full), face up, unlocked, theirs.
        /// Amends the move that took the pawn off (no Undo step of its own).
        /// </summary>
        public TrapFloorTakePrizeResult CollectTakenPrize(MatchState matchState, TrapFloorCollectPrizeCommand command)
        {
            if (matchState == null) return Failure(CommandResultStatus.Invalid, TrapFloorTakePrizeError.MatchRequired);
            if (command == null) return Failure(CommandResultStatus.Invalid, TrapFloorTakePrizeError.CommandRequired);
            if (command.Context.MatchId != matchState.Id)
                return Failure(CommandResultStatus.Invalid, TrapFloorTakePrizeError.MatchIdMismatch);
            if (matchState.GameTemplateId != template.Template.Id || collapseState.MatchId != matchState.Id)
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.MatchTemplateMismatch);
            if (!collapseState.TryGetCollapsedFloor(command.FloorCardId, out TrapFloorCollapsedFloorState taken)
                || !taken.IsTakenPrize
                || !template.TryGetFloorCardState(matchState, command.FloorCardId, out TrapFloorFloorCardState floorCard)
                || !matchState.Cards.TryGetValue(command.FloorCardId, out CardInstanceState tileCard)
                || !tileCard.BaseState.ContainerId.IsEmpty
                || !tileCard.BaseState.IsUserLocked)
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.FloorCardMissingOrInvalid);
            PlayerId taker = taken.CollapsedByPlayerId;
            if (!TryGetPlayerSetup(matchState, taker, out TrapFloorPlayerSetupDefinition player))
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.PlayerSetupMissing);
            if (!TryResolveDestination(template, matchState, player, out ContainerState destination, out bool placedInHand))
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.DestinationMissing);
            if (matchState.Revision == long.MaxValue)
                return Failure(CommandResultStatus.Conflict, TrapFloorTakePrizeError.RevisionOverflow);

            TabletopObjectState tile = tileCard.BaseState;
            TabletopPose previousPose = tile.Pose;
            ObjectVisibility previousVisibility = tile.Visibility;
            PhysicalObjectState previousPhysical = tile.PhysicalState;
            ContainerTransferService transfer = new ContainerTransferService();
            try
            {
                tile.SetUserLocked(false);
                tile.SetOwner(taker);
                tile.SetVisibility(destination != null ? destination.Visibility : ObjectVisibility.Public);
                tile.SetPhysicalState(null);
                if (destination != null)
                {
                    ContainerTransferResult placement = transfer.PlaceIntoContainer(tile, destination);
                    if (!placement.Succeeded)
                        throw new InvalidOperationException($"Taken prize placement failed: {placement.Error}.");
                }
                else
                {
                    tile.SetPose(player.AbilityOverflowPose);
                }

                tileCard.SetFace(CardFace.FaceUp);
            }
            catch
            {
                if (destination != null && tile.ContainerId == destination.Id && destination.Contains(tile.Id))
                    transfer.RemoveFromContainer(tile, destination);
                tile.SetPose(previousPose);
                tile.SetVisibility(previousVisibility);
                tile.SetUserLocked(true);
                tile.SetPhysicalState(previousPhysical);
                return Failure(CommandResultStatus.Rejected, TrapFloorTakePrizeError.MutationFailed);
            }

            long revision = matchState.AdvanceRevision(
                command.Context.Id,
                taker,
                AuthoritativeActionKind.PurchaseActionOrAbility,
                AuthoritativeActionRecordMode.None);
            return TrapFloorTakePrizeResult.Accepted(
                revision,
                floorCard,
                command.FloorCardId,
                destination != null ? destination.Id : ContainerId.Empty,
                placedInHand);
        }

        // TABLE-1 (owner, 2026-10-21, as in the playtest): the Controller Cards that paid go straight back into the
        // Controller deck, at the bottom, face down. Shared with the Shop.
        internal static void ReturnPaymentToControllerDeck(
            MatchState matchState,
            TrapFloorPlayerSetupDefinition player,
            IReadOnlyList<TabletopObjectId> paymentCardIds)
        {
            if (paymentCardIds == null
                || player.ControllerDeckId.IsEmpty
                || !matchState.Containers.TryGetValue(player.ControllerDeckId, out ContainerState deck))
            {
                return;
            }

            ContainerTransferService transfer = new ContainerTransferService();
            for (int i = 0; i < paymentCardIds.Count; i++)
            {
                if (!matchState.Cards.TryGetValue(paymentCardIds[i], out CardInstanceState card)
                    || card.BaseState.ContainerId.IsEmpty
                    || card.BaseState.ContainerId == deck.Id
                    || !matchState.Containers.TryGetValue(card.BaseState.ContainerId, out ContainerState slot))
                {
                    continue;
                }

                if (transfer.MoveBetweenContainers(card.BaseState, slot, deck, 0).Succeeded)
                {
                    card.SetFace(CardFace.FaceDown);
                }
            }
        }

        private TrapFloorTakePrizeError ValidatePayment(
            MatchState matchState,
            TrapFloorPlayerSetupDefinition player,
            TrapFloorFloorContentDefinition content,
            TrapFloorTakePrizeCommand command)
        {
            InputCostData cost = CostOf(content);
            if (cost.IsEmpty)
            {
                return command.PaymentCardIds.Count == 0
                    ? TrapFloorTakePrizeError.None
                    : TrapFloorTakePrizeError.PaymentInvalid;
            }

            ControllerInputCostEvaluation payment = new ControllerInputCostEvaluator().EvaluateExact(
                matchState,
                command.PaymentCardIds,
                cost,
                template.GameDefinition);
            if (!payment.CanPay) return TrapFloorTakePrizeError.PaymentInvalid;
            for (int i = 0; i < command.PaymentCardIds.Count; i++)
            {
                CardInstanceState card = matchState.Cards[command.PaymentCardIds[i]];
                if (!IsCardInPlayerSideSlot(matchState, player, card)) return TrapFloorTakePrizeError.PaymentCardNotInConsole;
                if (card.BaseState.IsUserLocked) return TrapFloorTakePrizeError.PaymentCardLocked;
            }

            return TrapFloorTakePrizeError.None;
        }

        // The Hand, unless it already holds the game's hand limit; then the Action stack beside the Console when the
        // seat has one, otherwise (TABLE-1) null: the card is placed loose beside the Console (AbilityOverflowPose).
        // Shared with the Shop (doc 18 §15.4), so a bought card goes where a taken one does.
        internal static bool TryResolveDestination(
            TrapFloorTemplateDefinition template,
            MatchState matchState,
            TrapFloorPlayerSetupDefinition player,
            out ContainerState destination,
            out bool placedInHand)
        {
            placedInHand = false;
            int handLimit = template.GameDefinition.ControllerConfiguration?.MaximumHandSize ?? 0;
            if (matchState.Containers.TryGetValue(player.HandContainerId, out ContainerState hand)
                && !hand.IsFull
                && (handLimit <= 0 || hand.Count < handLimit))
            {
                destination = hand;
                placedInHand = true;
                return true;
            }

            if (!player.ActionAbilityAreaContainerId.IsEmpty
                && matchState.Containers.TryGetValue(player.ActionAbilityAreaContainerId, out destination)
                && !destination.IsFull)
            {
                return true;
            }

            destination = null;
            return true;
        }

        private bool TryGetPlayerSetup(
            MatchState matchState,
            PlayerId playerId,
            out TrapFloorPlayerSetupDefinition resolvedPlayer)
        {
            for (int i = 0; i < template.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition player = template.Players[i];
                if (matchState.Seats.TryGetValue(player.SeatId, out SeatState seat)
                    && seat.OccupantPlayerId == playerId)
                {
                    resolvedPlayer = player;
                    return true;
                }
            }

            resolvedPlayer = null;
            return false;
        }

        internal static bool IsCardInPlayerSideSlot(
            MatchState matchState,
            TrapFloorPlayerSetupDefinition player,
            CardInstanceState card)
        {
            ContainerId containerId = card.BaseState.ContainerId;
            for (int i = 0; i < player.SideSlotContainerIds.Count; i++)
            {
                if (player.SideSlotContainerIds[i] != containerId) continue;
                return matchState.Containers.TryGetValue(containerId, out ContainerState slot)
                    && slot.Kind == ContainerKind.ConsoleSlot
                    && slot.OwnerSeatId == player.SeatId
                    && slot.Contains(card.BaseState.Id);
            }

            return false;
        }

        private static TrapFloorTakePrizeResult Failure(CommandResultStatus status, TrapFloorTakePrizeError error) =>
            TrapFloorTakePrizeResult.Failure(status, error);
    }
}
