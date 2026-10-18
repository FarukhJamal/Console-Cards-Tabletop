using System;
using ConsoleCards.Application.Authority;
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
        private readonly ITableAuthority tableAuthority;

        public TabletopCardFlipCoordinator(
            MatchState matchState,
            PlayerId requestedByPlayerId,
            InteractionOwnerId interactionOwnerId,
            TabletopSelectionState selectionState,
            LocalInteractionLockService lockService,
            ITableAuthority tableAuthority)
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
            this.tableAuthority = tableAuthority ?? throw new ArgumentNullException(nameof(tableAuthority));

            RequestedByPlayerId = requestedByPlayerId;
            InteractionOwnerId = interactionOwnerId;
        }

        public MatchState MatchState { get; }

        public PlayerId RequestedByPlayerId { get; }

        public InteractionOwnerId InteractionOwnerId { get; }

        public TabletopSelectionState SelectionState => selectionState;

        public LocalInteractionLockService LockService => lockService;

        /// <summary>The command door every change here goes through (R1b).</summary>
        public ITableAuthority TableAuthority => tableAuthority;

        /// <summary>
        /// Called after a card is accepted turned over, so Presentation can play the turn-over (doc 22 §14).
        /// Set once; never changes Runtime State.
        /// </summary>
        public Action<CardView> Flipped { get; set; }

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

            return Flip(cardView);
        }

        internal FlipInteractionResult Flip(CardView cardView)
        {
            if (cardView == null
                || !cardView.isActiveAndEnabled
                || !cardView.IsBound
                || cardView.CardState == null
                || cardView.BoundState == null)
            {
                return FlipInteractionResult.SelectionUnavailable();
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

                FlipCardResult flipResult = tableAuthority.Submit<FlipCardCommand, FlipCardResult>(command);
                if (cardView != null && cardView.IsBound)
                {
                    cardView.ApplyAcceptedState();
                    if (flipResult.Succeeded)
                    {
                        Flipped?.Invoke(cardView);
                    }
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
