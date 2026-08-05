using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    public sealed class ContainedCardDragCoordinator
    {
        private readonly LocalInteractionLockService lockService;
        private readonly TabletopInteractionStateMachine stateMachine;
        private readonly TabletopDragPreviewSession previewSession;
        private readonly TabletopPointerProjector pointerProjector;
        private readonly CardDropTargetResolver dropTargetResolver;
        private readonly CardTransferInteractionCoordinator transferCoordinator;
        private readonly ContainerLayoutViewLookup layoutViewLookup;
        private readonly IContainedCardDragFeedback feedback;

        private CardView activeCardView;
        private ContainerId sourceContainerId;
        private IContainerLayoutView sourceLayoutView;
        private bool releasesLockOnCompletion;

        public ContainedCardDragCoordinator(
            InteractionOwnerId interactionOwnerId,
            LocalInteractionLockService lockService,
            TabletopInteractionStateMachine stateMachine,
            TabletopDragPreviewSession previewSession,
            TabletopPointerProjector pointerProjector,
            CardDropTargetResolver dropTargetResolver,
            CardTransferInteractionCoordinator transferCoordinator,
            ContainerLayoutViewLookup layoutViewLookup)
            : this(
                interactionOwnerId,
                lockService,
                stateMachine,
                previewSession,
                pointerProjector,
                dropTargetResolver,
                transferCoordinator,
                layoutViewLookup,
                null)
        {
        }

        public ContainedCardDragCoordinator(
            InteractionOwnerId interactionOwnerId,
            LocalInteractionLockService lockService,
            TabletopInteractionStateMachine stateMachine,
            TabletopDragPreviewSession previewSession,
            TabletopPointerProjector pointerProjector,
            CardDropTargetResolver dropTargetResolver,
            CardTransferInteractionCoordinator transferCoordinator,
            ContainerLayoutViewLookup layoutViewLookup,
            IContainedCardDragFeedback feedback)
        {
            if (interactionOwnerId.IsEmpty)
            {
                throw new ArgumentException("Interaction owner ID cannot be empty.", nameof(interactionOwnerId));
            }

            this.lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
            this.stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            this.previewSession = previewSession ?? throw new ArgumentNullException(nameof(previewSession));
            this.pointerProjector = pointerProjector ?? throw new ArgumentNullException(nameof(pointerProjector));
            this.dropTargetResolver = dropTargetResolver ?? throw new ArgumentNullException(nameof(dropTargetResolver));
            this.transferCoordinator = transferCoordinator ?? throw new ArgumentNullException(nameof(transferCoordinator));
            this.layoutViewLookup = layoutViewLookup ?? throw new ArgumentNullException(nameof(layoutViewLookup));
            this.feedback = feedback;

            if (stateMachine.Phase != TabletopInteractionPhase.Idle)
            {
                throw new ArgumentException("Contained-card drag state machine must begin in Idle.", nameof(stateMachine));
            }

            if (previewSession.IsActive)
            {
                throw new ArgumentException("Contained-card drag preview session must begin inactive.", nameof(previewSession));
            }

            if (transferCoordinator.InteractionOwnerId != interactionOwnerId)
            {
                throw new ArgumentException("Contained-card drag and transfer coordinators must use the same InteractionOwnerId.", nameof(transferCoordinator));
            }

            if (!ReferenceEquals(transferCoordinator.LockService, lockService))
            {
                throw new ArgumentException("Contained-card drag and transfer coordinators must use the same LocalInteractionLockService.", nameof(lockService));
            }

            InteractionOwnerId = interactionOwnerId;
        }

        public InteractionOwnerId InteractionOwnerId { get; }

        public CardView ActiveCardView => activeCardView != null ? activeCardView : null;

        public bool HasActiveInteraction => ActiveCardView != null;

        public TabletopInteractionPhase Phase => stateMachine.Phase;

        public bool TryBegin(
            CardView cardView,
            Vector2 initialScreenPosition)
        {
            ValidateScreenPosition(initialScreenPosition, nameof(initialScreenPosition));
            EnsureNoActiveInteraction();
            EnsurePhaseAllows(nameof(TryBegin), TabletopInteractionPhase.Idle, TabletopInteractionPhase.Hovering);

            if (!TryValidateContainedCard(cardView, out CardInstanceState matchCard, out ContainerId currentSourceId))
            {
                return false;
            }

            if (!TryGetCurrentLayoutView(currentSourceId, out IContainerLayoutView currentSourceLayout))
            {
                return false;
            }

            InteractionLockAcquireResult acquireResult = lockService.Acquire(
                matchCard.BaseState.Id,
                InteractionOwnerId);
            if (acquireResult.Status == InteractionLockAcquireStatus.Conflict)
            {
                return false;
            }

            bool acquiredByThisCall = acquireResult.Status == InteractionLockAcquireStatus.Acquired;
            try
            {
                stateMachine.BeginPress(matchCard.BaseState.Id, initialScreenPosition);
                activeCardView = cardView;
                sourceContainerId = currentSourceId;
                sourceLayoutView = currentSourceLayout;
                releasesLockOnCompletion = acquiredByThisCall;
                feedback?.Begin(sourceContainerId);
                return true;
            }
            catch
            {
                if (acquiredByThisCall)
                {
                    lockService.Release(matchCard.BaseState.Id, InteractionOwnerId);
                }

                ClearLocalLifecycleState();
                throw;
            }
        }

        public void UpdatePointer(Vector2 screenPosition)
        {
            ValidateScreenPosition(screenPosition, nameof(screenPosition));
            CardView view = GetActiveCardView();
            EnsurePhaseAllows(nameof(UpdatePointer), TabletopInteractionPhase.Pressed, TabletopInteractionPhase.DraggingObject);

            bool startedDragging = stateMachine.UpdatePointer(screenPosition);
            if (stateMachine.Phase != TabletopInteractionPhase.DraggingObject)
            {
                return;
            }

            if (!pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate coordinate))
            {
                CancelActiveInteraction(ContainedCardDragReleaseStatus.ProjectionFailed);
                return;
            }

            if (startedDragging)
            {
                previewSession.Begin(view);
            }

            TabletopPose acceptedPose = view.CardState.BaseState.Pose;
            previewSession.UpdatePose(new TabletopPose(
                coordinate,
                acceptedPose.RotationDegrees,
                acceptedPose.Layer,
                acceptedPose.LocalOrder));
            UpdateFeedback(screenPosition);
        }

        public ContainedCardDragReleaseResult Release(Vector2 screenPosition)
        {
            ValidateScreenPosition(screenPosition, nameof(screenPosition));
            CardView view = GetActiveCardView();
            EnsurePhaseAllows(nameof(Release), TabletopInteractionPhase.Pressed, TabletopInteractionPhase.DraggingObject);

            if (stateMachine.Phase == TabletopInteractionPhase.Pressed)
            {
                stateMachine.ReleasePointer();
                try
                {
                    RestoreSourceLayout();
                    return ContainedCardDragReleaseResult.ClickReleased();
                }
                finally
                {
                    CompleteLifecycleWithoutStateMachineMutation();
                }
            }

            stateMachine.ReleasePointer();
            try
            {
                if (!dropTargetResolver.TryResolve(screenPosition, out CardDropTarget target))
                {
                    EndPreviewIfActiveWithoutReconcile();
                    RestoreSourceLayout();
                    stateMachine.BeginCancellation();
                    stateMachine.CompleteCancellation();
                    return ContainedCardDragReleaseResult.ProjectionFailed();
                }

                if (target.Kind == CardDropTargetKind.None)
                {
                    EndPreviewIfActiveWithoutReconcile();
                    RestoreSourceLayout();
                    stateMachine.BeginCancellation();
                    stateMachine.CompleteCancellation();
                    return ContainedCardDragReleaseResult.NoTarget();
                }

                if (target.Kind == CardDropTargetKind.Container && target.ContainerId == sourceContainerId)
                {
                    EndPreviewIfActiveWithoutReconcile();
                    RestoreSourceLayout();
                    stateMachine.BeginCancellation();
                    stateMachine.CompleteCancellation();
                    return ContainedCardDragReleaseResult.SameSource();
                }

                EndPreviewIfActiveWithoutReconcile();
                CardTransferInteractionResult transferResult = transferCoordinator.Transfer(view, target);
                ContainedCardDragReleaseResult releaseResult = MapTransferResult(transferResult);
                if (releaseResult.Status == ContainedCardDragReleaseStatus.TransferAccepted)
                {
                    stateMachine.CompleteAcceptance();
                }
                else
                {
                    feedback?.ShowRejected(sourceContainerId, target);
                    stateMachine.BeginCancellation();
                    stateMachine.CompleteCancellation();
                }

                return releaseResult;
            }
            finally
            {
                feedback?.Clear();
                CompleteLifecycleWithoutStateMachineMutation();
            }
        }

        public void Cancel()
        {
            GetActiveCardView();
            EnsurePhaseAllows(
                nameof(Cancel),
                TabletopInteractionPhase.Pressed,
                TabletopInteractionPhase.DraggingObject,
                TabletopInteractionPhase.AwaitingAcceptance);

            CancelActiveInteraction(ContainedCardDragReleaseStatus.Cancelled);
        }

        public void Reset()
        {
            if (!HasActiveInteraction)
            {
                if (previewSession.IsActive)
                {
                    previewSession.Reset();
                }

                stateMachine.Reset();
                return;
            }

            Exception cleanupFailure = null;
            try
            {
                EndPreviewIfActiveWithoutReconcile();
                RestoreSourceLayout();
            }
            catch (Exception exception)
            {
                cleanupFailure = exception;
            }
            finally
            {
                feedback?.Clear();
                ReleaseLifecycleOwnedLock();
                stateMachine.Reset();
                ClearLocalLifecycleState();
            }

            if (cleanupFailure != null)
            {
                throw new InvalidOperationException("Contained-card drag reset could not restore source layout.", cleanupFailure);
            }
        }

        private void CancelActiveInteraction(ContainedCardDragReleaseStatus status)
        {
            Exception cleanupFailure = null;
            try
            {
                if (stateMachine.Phase != TabletopInteractionPhase.Cancelling
                    && stateMachine.Phase != TabletopInteractionPhase.Idle)
                {
                    stateMachine.BeginCancellation();
                }

                EndPreviewIfActiveWithoutReconcile();
                RestoreSourceLayout();
            }
            catch (Exception exception)
            {
                cleanupFailure = exception;
            }
            finally
            {
                feedback?.Clear();
                ReleaseLifecycleOwnedLock();
                if (stateMachine.Phase == TabletopInteractionPhase.Cancelling)
                {
                    stateMachine.CompleteCancellation();
                }
                else
                {
                    stateMachine.Reset();
                }

                ClearLocalLifecycleState();
            }

            if (cleanupFailure != null)
            {
                throw new InvalidOperationException(
                    $"Contained-card drag {status} cleanup could not restore source layout.",
                    cleanupFailure);
            }
        }

        private ContainedCardDragReleaseResult MapTransferResult(CardTransferInteractionResult transferResult)
        {
            if (transferResult.TransferAttempted)
            {
                return ContainedCardDragReleaseResult.FromTransferResult(transferResult);
            }

            RestoreSourceLayout();
            return ContainedCardDragReleaseResult.Cancelled();
        }

        private bool TryValidateContainedCard(
            CardView cardView,
            out CardInstanceState matchCard,
            out ContainerId currentSourceId)
        {
            matchCard = null;
            currentSourceId = ContainerId.Empty;

            if (cardView == null
                || !cardView.isActiveAndEnabled
                || !cardView.IsBound
                || cardView.CardState == null
                || cardView.BoundState == null)
            {
                return false;
            }

            if (cardView.CardState.BaseState.ContainerId.IsEmpty
                || cardView.CardState.BaseState.IsUserLocked
                || cardView.IsPreviewing)
            {
                return false;
            }

            if (!transferCoordinator.MatchState.Cards.TryGetValue(cardView.ObjectId, out matchCard)
                || !ReferenceEquals(matchCard, cardView.CardState)
                || !ReferenceEquals(matchCard.BaseState, cardView.BoundState)
                || matchCard.BaseState.Kind != TabletopObjectKind.Card)
            {
                matchCard = null;
                return false;
            }

            currentSourceId = matchCard.BaseState.ContainerId;
            return !currentSourceId.IsEmpty;
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

        private void RestoreSourceLayout()
        {
            if (sourceLayoutView == null
                || !sourceLayoutView.IsBound
                || sourceLayoutView.ContainerId != sourceContainerId
                || sourceLayoutView.ContainerState == null
                || sourceLayoutView.ContainerState.Id != sourceContainerId)
            {
                throw new InvalidOperationException("Contained-card drag source layout is unavailable.");
            }

            sourceLayoutView.ApplyAcceptedLayout();
        }

        private void EndPreviewIfActiveWithoutReconcile()
        {
            if (previewSession.IsActive)
            {
                previewSession.EndPreviewWithoutReconcile();
            }
        }

        private void UpdateFeedback(Vector2 screenPosition)
        {
            if (feedback == null)
            {
                return;
            }

            if (!dropTargetResolver.TryResolve(screenPosition, sourceContainerId, out CardDropTarget target))
            {
                feedback.Update(sourceContainerId, CardDropTarget.None(), false);
                return;
            }

            feedback.Update(sourceContainerId, target, TargetWouldAccept(target));
        }

        private bool TargetWouldAccept(CardDropTarget target)
        {
            if (target.Kind == CardDropTargetKind.Tabletop)
            {
                return true;
            }

            if (target.Kind != CardDropTargetKind.Container
                || target.ContainerId.IsEmpty
                || target.ContainerId == sourceContainerId
                || !transferCoordinator.MatchState.Containers.TryGetValue(target.ContainerId, out ContainerState container))
            {
                return false;
            }

            return !container.IsFull;
        }

        private void ReleaseLifecycleOwnedLock()
        {
            if (releasesLockOnCompletion && activeCardView != null && activeCardView.IsBound)
            {
                lockService.Release(activeCardView.ObjectId, InteractionOwnerId);
            }
        }

        private void CompleteLifecycleWithoutStateMachineMutation()
        {
            ReleaseLifecycleOwnedLock();
            if (stateMachine.Phase != TabletopInteractionPhase.Idle)
            {
                stateMachine.Reset();
            }

            ClearLocalLifecycleState();
        }

        private void ClearLocalLifecycleState()
        {
            activeCardView = null;
            sourceContainerId = ContainerId.Empty;
            sourceLayoutView = null;
            releasesLockOnCompletion = false;
        }

        private CardView GetActiveCardView()
        {
            if (activeCardView == null)
            {
                activeCardView = null;
                throw new InvalidOperationException("No contained-card drag interaction is active.");
            }

            return activeCardView;
        }

        private void EnsureNoActiveInteraction()
        {
            if (HasActiveInteraction)
            {
                throw new InvalidOperationException("A contained-card drag interaction is already active.");
            }
        }

        private void EnsurePhaseAllows(string operation, params TabletopInteractionPhase[] allowedPhases)
        {
            for (int i = 0; i < allowedPhases.Length; i++)
            {
                if (stateMachine.Phase == allowedPhases[i])
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"{operation} is not valid while the interaction phase is {stateMachine.Phase}.");
        }

        private static void ValidateScreenPosition(Vector2 screenPosition, string parameterName)
        {
            if (!IsFinite(screenPosition.x) || !IsFinite(screenPosition.y))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
