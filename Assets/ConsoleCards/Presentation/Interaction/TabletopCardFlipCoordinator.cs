using System;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;

namespace ConsoleCards.Presentation.Interaction
{
    public sealed class TabletopCardFlipCoordinator
    {
        private readonly TabletopSelectionState selectionState;
        private readonly LocalInteractionLockService lockService;
        private readonly FlipCardUseCase flipUseCase;

        public TabletopCardFlipCoordinator(
            MatchState matchState,
            PlayerId requestedByPlayerId,
            InteractionOwnerId interactionOwnerId,
            TabletopSelectionState selectionState,
            LocalInteractionLockService lockService,
            FlipCardUseCase flipUseCase)
        {
            MatchState = matchState ?? throw new ArgumentNullException(nameof(matchState));
            if (requestedByPlayerId.IsEmpty)
            {
                throw new ArgumentException("Requested by Player ID cannot be empty.", nameof(requestedByPlayerId));
            }

            if (interactionOwnerId.IsEmpty)
            {
                throw new ArgumentException("Interaction owner ID cannot be empty.", nameof(interactionOwnerId));
            }

            this.selectionState = selectionState ?? throw new ArgumentNullException(nameof(selectionState));
            this.lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
            this.flipUseCase = flipUseCase ?? throw new ArgumentNullException(nameof(flipUseCase));

            RequestedByPlayerId = requestedByPlayerId;
            InteractionOwnerId = interactionOwnerId;
        }

        public MatchState MatchState { get; }

        public PlayerId RequestedByPlayerId { get; }

        public InteractionOwnerId InteractionOwnerId { get; }

        public TabletopSelectionState SelectionState => selectionState;

        public LocalInteractionLockService LockService => lockService;

        public FlipCardUseCase FlipUseCase => flipUseCase;

        public FlipInteractionResult FlipSelected()
        {
            bool hadStoredSelectedView = !ReferenceEquals(selectionState.SelectedView, null);
            selectionState.ClearUnavailable();
            if (!selectionState.HasSelection)
            {
                return hadStoredSelectedView
                    ? FlipInteractionResult.SelectionUnavailable()
                    : FlipInteractionResult.NoSelection();
            }

            TabletopObjectView selectedView = selectionState.SelectedView;
            if (!(selectedView is CardView cardView))
            {
                return FlipInteractionResult.SelectionNotCard();
            }

            if (cardView.IsPreviewing)
            {
                throw new InvalidOperationException("Selected card is already in a temporary preview state.");
            }

            if (cardView.BoundState.IsUserLocked)
            {
                return FlipInteractionResult.ObjectUserLocked();
            }

            InteractionLockAcquireResult acquireResult = lockService.Acquire(
                cardView.ObjectId,
                InteractionOwnerId);
            if (acquireResult.Status == InteractionLockAcquireStatus.Conflict)
            {
                return FlipInteractionResult.LocalLockConflict();
            }

            bool acquiredByThisCall = acquireResult.Status == InteractionLockAcquireStatus.Acquired;
            try
            {
                CardFace targetFace = GetOppositeFace(cardView.CardState.Face);
                CommandContext context = new CommandContext(
                    CommandId.New(),
                    MatchState.Id,
                    RequestedByPlayerId,
                    MatchState.Revision);
                FlipCardCommand command = new FlipCardCommand(
                    context,
                    cardView.ObjectId,
                    targetFace);

                FlipCardResult flipResult = flipUseCase.Execute(MatchState, command);
                if (cardView != null && cardView.IsBound)
                {
                    cardView.ApplyAcceptedState();
                }

                return FlipInteractionResult.FromFlipResult(flipResult);
            }
            finally
            {
                if (acquiredByThisCall)
                {
                    lockService.Release(cardView.ObjectId, InteractionOwnerId);
                }
            }
        }

        private static CardFace GetOppositeFace(CardFace face)
        {
            switch (face)
            {
                case CardFace.FaceUp:
                    return CardFace.FaceDown;
                case CardFace.FaceDown:
                    return CardFace.FaceUp;
                default:
                    throw new InvalidOperationException("Selected card has an unsupported face value.");
            }
        }
    }
}
