using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    public sealed class CardTransferInteractionCoordinator
    {
        private readonly ContainerLayoutViewLookup layoutViewLookup;
        private readonly LocalInteractionLockService lockService;
        private readonly TransferCardUseCase transferUseCase;
        private readonly IReadOnlyList<CardView> cardViews;
        private readonly TabletopPresentationTransitionController transitions;
        private readonly float settleDuration;
        private readonly float returnDuration;
        private readonly float handReflowDuration;

        public CardTransferInteractionCoordinator(
            MatchState matchState,
            PlayerId requestedByPlayerId,
            InteractionOwnerId interactionOwnerId,
            LocalInteractionLockService lockService,
            TransferCardUseCase transferUseCase,
            IReadOnlyList<IContainerLayoutView> layoutViews)
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

            this.lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
            this.transferUseCase = transferUseCase ?? throw new ArgumentNullException(nameof(transferUseCase));
            layoutViewLookup = new ContainerLayoutViewLookup(layoutViews);
            cardViews = null;
            transitions = null;
            settleDuration = 0f;
            returnDuration = 0f;
            handReflowDuration = 0f;

            RequestedByPlayerId = requestedByPlayerId;
            InteractionOwnerId = interactionOwnerId;
        }

        internal CardTransferInteractionCoordinator(
            MatchState matchState,
            PlayerId requestedByPlayerId,
            InteractionOwnerId interactionOwnerId,
            LocalInteractionLockService lockService,
            TransferCardUseCase transferUseCase,
            IReadOnlyList<IContainerLayoutView> layoutViews,
            IReadOnlyList<CardView> cardViews,
            TabletopPresentationTransitionController transitions,
            float settleDuration,
            float returnDuration,
            float handReflowDuration)
            : this(
                matchState,
                requestedByPlayerId,
                interactionOwnerId,
                lockService,
                transferUseCase,
                layoutViews)
        {
            this.cardViews = cardViews ?? throw new ArgumentNullException(nameof(cardViews));
            this.transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            this.settleDuration = ValidateNonNegative(settleDuration, nameof(settleDuration));
            this.returnDuration = ValidateNonNegative(returnDuration, nameof(returnDuration));
            this.handReflowDuration = ValidateNonNegative(handReflowDuration, nameof(handReflowDuration));
        }

        public MatchState MatchState { get; }

        public PlayerId RequestedByPlayerId { get; }

        public InteractionOwnerId InteractionOwnerId { get; }

        public LocalInteractionLockService LockService => lockService;

        public TransferCardUseCase TransferUseCase => transferUseCase;

        public ContainerLayoutViewLookup LayoutViewLookup => layoutViewLookup;

        public CardTransferInteractionResult Transfer(
            CardView cardView,
            CardDropTarget target)
        {
            if (!TryValidateCard(cardView, out CardInstanceState matchCard, out CardTransferInteractionResult cardFailure))
            {
                return cardFailure;
            }

            ContainerId sourceContainerId = matchCard.BaseState.ContainerId;
            IContainerLayoutView sourceLayoutView = null;
            IContainerLayoutView destinationLayoutView = null;

            if (target.Kind == CardDropTargetKind.None)
            {
                return CardTransferInteractionResult.NoTarget();
            }

            if (!sourceContainerId.IsEmpty
                && !TryGetCurrentLayoutView(sourceContainerId, out sourceLayoutView))
            {
                return CardTransferInteractionResult.SourceLayoutUnavailable();
            }

            if (target.Kind == CardDropTargetKind.Container)
            {
                if (target.ContainerId.IsEmpty)
                {
                    return CardTransferInteractionResult.NoTarget();
                }

                if (target.ContainerId == sourceContainerId)
                {
                    return CardTransferInteractionResult.SameLocation();
                }

                if (!TryGetCurrentLayoutView(target.ContainerId, out destinationLayoutView))
                {
                    return CardTransferInteractionResult.DestinationLayoutUnavailable();
                }
            }
            else if (target.Kind == CardDropTargetKind.Tabletop)
            {
                if (sourceContainerId.IsEmpty)
                {
                    return CardTransferInteractionResult.SameLocation();
                }
            }
            else
            {
                return CardTransferInteractionResult.NoTarget();
            }

            InteractionLockAcquireResult acquireResult = lockService.Acquire(
                matchCard.BaseState.Id,
                InteractionOwnerId);
            if (acquireResult.Status == InteractionLockAcquireStatus.Conflict)
            {
                return CardTransferInteractionResult.LocalLockConflict();
            }

            bool acquiredByThisCall = acquireResult.Status == InteractionLockAcquireStatus.Acquired;
            try
            {
                IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                    transitions != null
                        ? CaptureAffectedCardTransforms(cardView, sourceContainerId, target.ContainerId)
                        : null;
                TransferCardCommand command = CreateCommand(matchCard, sourceContainerId, target, cardView);
                TransferCardResult transferResult;
                try
                {
                    transferResult = transferUseCase.Execute(MatchState, command);
                    if (!transferResult.Succeeded) cardView.PhysicalObject?.Cancel();

                    ReconcileAfterTransfer(
                        cardView,
                        sourceContainerId,
                        command.DestinationContainerId,
                        sourceLayoutView,
                        destinationLayoutView,
                        transferResult);
                }
                catch
                {
                    cardView.PhysicalObject?.Cancel();
                    ReconcileCurrentPresentation(cardView, sourceLayoutView, destinationLayoutView);
                    if (transitions != null)
                    {
                        transitions.AnimateCardsFromCurrentResults(
                            transitionStarts,
                            returnDuration);
                    }

                    throw;
                }

                if (transitions != null)
                {
                    float duration = transferResult.Succeeded
                        ? ResolveAcceptedDuration(sourceLayoutView, destinationLayoutView)
                        : returnDuration;
                    float arcHeight = transferResult.Succeeded && destinationLayoutView is DiscardPileView
                        ? 0.045f
                        : 0f;
                    transitions.AnimateCardsFromCurrentResults(transitionStarts, duration, arcHeight);
                }

                return CardTransferInteractionResult.FromTransferResult(transferResult);
            }
            finally
            {
                if (acquiredByThisCall)
                {
                    lockService.Release(matchCard.BaseState.Id, InteractionOwnerId);
                }
            }
        }

        private bool TryValidateCard(
            CardView cardView,
            out CardInstanceState matchCard,
            out CardTransferInteractionResult failure)
        {
            matchCard = null;
            failure = default;

            if (cardView == null
                || !cardView.isActiveAndEnabled
                || !cardView.IsBound
                || cardView.CardState == null
                || cardView.BoundState == null)
            {
                failure = CardTransferInteractionResult.CardUnavailable();
                return false;
            }

            if (cardView.BoundState.Kind != TabletopObjectKind.Card)
            {
                failure = CardTransferInteractionResult.CardNotTransferable();
                return false;
            }

            if (cardView.IsPreviewing || cardView.BoundState.IsUserLocked)
            {
                failure = CardTransferInteractionResult.CardNotTransferable();
                return false;
            }

            if (!MatchState.Cards.TryGetValue(cardView.ObjectId, out matchCard)
                || !ReferenceEquals(matchCard, cardView.CardState)
                || !ReferenceEquals(matchCard.BaseState, cardView.BoundState))
            {
                matchCard = null;
                failure = CardTransferInteractionResult.CardUnavailable();
                return false;
            }

            return true;
        }

        private bool TryGetCurrentLayoutView(
            ContainerId containerId,
            out IContainerLayoutView layoutView)
        {
            if (!layoutViewLookup.TryGet(containerId, out layoutView))
            {
                return false;
            }

            return layoutView != null
                && layoutView.IsBound
                && layoutView.ContainerId == containerId
                && layoutView.ContainerState != null
                && layoutView.ContainerState.Id == containerId;
        }

        private TransferCardCommand CreateCommand(
            CardInstanceState card,
            ContainerId sourceContainerId,
            CardDropTarget target, CardView cardView)
        {
            CommandContext context = new CommandContext(
                CommandId.New(),
                MatchState.Id,
                RequestedByPlayerId,
                MatchState.Revision);

            if (target.Kind == CardDropTargetKind.Tabletop)
            {
                TabletopPose acceptedPose = card.BaseState.Pose;
                TabletopPose targetPose = new TabletopPose(
                    target.TabletopPose.Position,
                    acceptedPose.RotationDegrees,
                    acceptedPose.Layer,
                    acceptedPose.LocalOrder);

                return TransferCardCommand.ToTabletop(
                    context,
                    card.BaseState.Id,
                    sourceContainerId,
                    targetPose, cardView.PhysicalObject != null && cardView.PhysicalObject.IsHeld
                        ? cardView.PhysicalObject.ReleaseState() : null);
            }

            return TransferCardCommand.ToContainer(
                context,
                card.BaseState.Id,
                sourceContainerId,
                target.ContainerId);
        }

        private static void ReconcileAfterTransfer(
            CardView cardView,
            ContainerId sourceContainerId,
            ContainerId destinationContainerId,
            IContainerLayoutView sourceLayoutView,
            IContainerLayoutView destinationLayoutView,
            TransferCardResult transferResult)
        {
            try
            {
                if (transferResult.Succeeded)
                {
                    ApplyAcceptedReconciliation(
                        cardView,
                        sourceContainerId,
                        destinationContainerId,
                        sourceLayoutView,
                        destinationLayoutView);
                }
                else
                {
                    ApplyRejectedReconciliation(cardView, sourceContainerId, sourceLayoutView);
                }
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Card transfer interaction reconciliation failed after command execution.", exception);
            }
        }

        private static void ApplyAcceptedReconciliation(
            CardView cardView,
            ContainerId sourceContainerId,
            ContainerId destinationContainerId,
            IContainerLayoutView sourceLayoutView,
            IContainerLayoutView destinationLayoutView)
        {
            if (sourceContainerId.IsEmpty)
            {
                destinationLayoutView.ApplyAcceptedLayout();
                return;
            }

            if (destinationContainerId.IsEmpty)
            {
                sourceLayoutView.ApplyAcceptedLayout();
                cardView.ClearContainerLayoutAndReconcile();
                return;
            }

            sourceLayoutView.ApplyAcceptedLayout();
            destinationLayoutView.ApplyAcceptedLayout();
        }

        private static void ApplyRejectedReconciliation(
            CardView cardView,
            ContainerId sourceContainerId,
            IContainerLayoutView sourceLayoutView)
        {
            if (sourceContainerId.IsEmpty)
            {
                cardView.ClearContainerLayoutAndReconcile();
                return;
            }

            sourceLayoutView.ApplyAcceptedLayout();
        }

        private float ResolveAcceptedDuration(
            IContainerLayoutView sourceLayoutView,
            IContainerLayoutView destinationLayoutView)
        {
            return sourceLayoutView is HandView || destinationLayoutView is HandView
                ? handReflowDuration
                : settleDuration;
        }

        private IReadOnlyDictionary<Transform, TabletopTransformSnapshot> CaptureAffectedCardTransforms(
            CardView activeCard,
            ContainerId sourceContainerId,
            ContainerId destinationContainerId)
        {
            Dictionary<Transform, TabletopTransformSnapshot> starts =
                new Dictionary<Transform, TabletopTransformSnapshot>();
            for (int i = 0; i < cardViews.Count; i++)
            {
                CardView candidate = cardViews[i];
                if (candidate == null || candidate.CardState == null)
                {
                    continue;
                }

                ContainerId candidateContainerId = candidate.CardState.BaseState.ContainerId;
                if (!ReferenceEquals(candidate, activeCard)
                    && (sourceContainerId.IsEmpty || candidateContainerId != sourceContainerId)
                    && (destinationContainerId.IsEmpty || candidateContainerId != destinationContainerId))
                {
                    continue;
                }

                starts[candidate.transform] = transitions.StopAndCapture(candidate.transform);
            }

            return starts;
        }

        private static void ReconcileCurrentPresentation(
            CardView cardView,
            IContainerLayoutView sourceLayoutView,
            IContainerLayoutView destinationLayoutView)
        {
            if (sourceLayoutView != null && sourceLayoutView.IsBound)
            {
                sourceLayoutView.ApplyAcceptedLayout();
            }

            if (destinationLayoutView != null && destinationLayoutView.IsBound)
            {
                destinationLayoutView.ApplyAcceptedLayout();
            }

            if (cardView.CardState != null && cardView.CardState.BaseState.ContainerId.IsEmpty)
            {
                cardView.ClearContainerLayoutAndReconcile();
            }
        }

        private static float ValidateNonNegative(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }
}
