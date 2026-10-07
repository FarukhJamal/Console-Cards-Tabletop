using System;
using ConsoleCards.Application.Results;
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
        // Height above the table a tray card is placed at when it leaves the tray for the table.
        private const float TrayExitLift = 0.05f;

        private readonly LocalInteractionLockService lockService;
        private readonly TabletopInteractionStateMachine stateMachine;
        private readonly TabletopDragPreviewSession previewSession;
        private readonly TabletopPointerProjector pointerProjector;
        private readonly CardDropTargetResolver dropTargetResolver;
        private readonly CardTransferInteractionCoordinator transferCoordinator;
        private readonly ContainerLayoutViewLookup layoutViewLookup;
        private readonly IContainedCardDragFeedback feedback;
        private readonly float magneticDistance;
        private readonly HandView handView;
        private readonly Func<CardView, int, ReorderContainerResult> reorderHandCard;

        private CardView activeCardView;
        private ContainerId sourceContainerId;
        private IContainerLayoutView sourceLayoutView;
        private bool releasesLockOnCompletion;
        private bool handReorderPreviewActive;
        private int handReorderTargetIndex = -1;
        // A physical card dragged from the camera tray follows the tray while the pointer is over it.
        // Leaving the tray starts a short exit flight toward the table pose under the pointer; when it
        // lands it becomes an ordinary held card. Coming back over the tray returns it to the tray.
        private bool trayDragActive;
        private bool trayExitInProgress;
        private bool trayDragOnTable;

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
            magneticDistance = 0f;
            handView = null;
            reorderHandCard = null;

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

        internal ContainedCardDragCoordinator(
            InteractionOwnerId interactionOwnerId,
            LocalInteractionLockService lockService,
            TabletopInteractionStateMachine stateMachine,
            TabletopDragPreviewSession previewSession,
            TabletopPointerProjector pointerProjector,
            CardDropTargetResolver dropTargetResolver,
            CardTransferInteractionCoordinator transferCoordinator,
            ContainerLayoutViewLookup layoutViewLookup,
            IContainedCardDragFeedback feedback,
            float magneticDistance)
            : this(
                interactionOwnerId,
                lockService,
                stateMachine,
                previewSession,
                pointerProjector,
                dropTargetResolver,
                transferCoordinator,
                layoutViewLookup,
                feedback)
        {
            if (!IsFinite(magneticDistance) || magneticDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(magneticDistance));
            }

            this.magneticDistance = magneticDistance;
        }

        internal ContainedCardDragCoordinator(
            InteractionOwnerId interactionOwnerId,
            LocalInteractionLockService lockService,
            TabletopInteractionStateMachine stateMachine,
            TabletopDragPreviewSession previewSession,
            TabletopPointerProjector pointerProjector,
            CardDropTargetResolver dropTargetResolver,
            CardTransferInteractionCoordinator transferCoordinator,
            ContainerLayoutViewLookup layoutViewLookup,
            IContainedCardDragFeedback feedback,
            float magneticDistance,
            HandView handView,
            Func<CardView, int, ReorderContainerResult> reorderHandCard)
            : this(
                interactionOwnerId,
                lockService,
                stateMachine,
                previewSession,
                pointerProjector,
                dropTargetResolver,
                transferCoordinator,
                layoutViewLookup,
                feedback,
                magneticDistance)
        {
            this.handView = handView ?? throw new ArgumentNullException(nameof(handView));
            this.reorderHandCard = reorderHandCard ?? throw new ArgumentNullException(nameof(reorderHandCard));
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
                previewSession.BeginPress(cardView);
                if (currentSourceLayout is ConsoleSlotView consoleSlotView
                    && cardView.PhysicalObject != null)
                {
                    cardView.PhysicalObject.BeginContainedPickup(consoleSlotView.ExtractionLift);
                }
                cardView.PhysicalObject?.PreparePointerAnchor(initialScreenPosition);
                trayDragActive = handView != null
                    && handView.IsTrayMode
                    && ReferenceEquals(currentSourceLayout, handView)
                    && cardView.PhysicalObject != null;
                trayDragOnTable = false;
                feedback?.Begin(sourceContainerId);
                return true;
            }
            catch
            {
                if (acquiredByThisCall)
                {
                    lockService.Release(matchCard.BaseState.Id, InteractionOwnerId);
                }

                previewSession.Reset();
                feedback?.Clear();
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

            if (trayDragActive && !trayDragOnTable)
            {
                if (startedDragging)
                {
                    previewSession.Begin(view);
                    startedDragging = false;
                }

                if (handView.IsScreenPointInTray(screenPosition))
                {
                    trayExitInProgress = false;
                    handView.SetTrayDragPointer(view, screenPosition, true);
                    UpdateHandReorderPreview(view, default(TableCoordinate), screenPosition);
                    UpdateFeedback(screenPosition);
                    return;
                }

                if (!TryResolveTrayTablePose(view, screenPosition, out Vector3 tablePosition, out Quaternion tableRotation))
                {
                    CancelActiveInteraction(ContainedCardDragReleaseStatus.ProjectionFailed);
                    return;
                }

                if (!trayExitInProgress)
                {
                    trayExitInProgress = true;
                    ClearHandReorderPreview(view);
                }

                handView.SetTrayHandoffTarget(view, tablePosition, tableRotation);
                if (!handView.IsTrayHandoffComplete(view))
                {
                    UpdateFeedback(screenPosition);
                    return;
                }

                if (!TryMoveTrayCardToTable(view, screenPosition))
                {
                    CancelActiveInteraction(ContainedCardDragReleaseStatus.Cancelled);
                    return;
                }
            }
            else if (trayDragActive && handView.IsScreenPointInTray(screenPosition))
            {
                // Back over the tray: stop holding it on the table and let it shrink back into the tray.
                view.PhysicalObject.DisableForContainer();
                trayDragOnTable = false;
                trayExitInProgress = false;
                handView.SetTrayDragPointer(view, screenPosition, false);
                UpdateHandReorderPreview(view, default(TableCoordinate), screenPosition);
                UpdateFeedback(screenPosition);
                return;
            }

            if (view.PhysicalObject != null)
            {
                if (startedDragging) previewSession.Begin(view);
                view.PhysicalObject.Follow(screenPosition);
                TryApplyConsolePlacementPreview(view, screenPosition);
                UpdateHandReorderPreview(view, view.PhysicalObject.LayoutCoordinate, screenPosition);
                UpdateFeedback(screenPosition);
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

            if (TryApplyConsolePlacementPreview(view, screenPosition))
            {
                UpdateHandReorderPreview(view, coordinate, screenPosition);
                UpdateFeedback(screenPosition);
                return;
            }

            TabletopPose acceptedPose = view.CardState.BaseState.Pose;
            TabletopPose previewPose = new TabletopPose(
                coordinate,
                acceptedPose.RotationDegrees,
                acceptedPose.Layer,
                acceptedPose.LocalOrder);
            previewSession.UpdatePose(ApplyStackMagnetism(previewPose, screenPosition));
            UpdateHandReorderPreview(view, coordinate, screenPosition);
            UpdateFeedback(screenPosition);
        }

        /// <summary>
        /// Optional local comfort check: true when a container is the player's own hand at its hand cap.
        /// Such a hand is treated as full (red feedback, the card returns, nothing is committed).
        /// </summary>
        public Func<ContainerId, bool> IsContainerAtComfortCap { get; set; }

        /// <summary>Optional notice raised when a drop is refused by the comfort cap.</summary>
        public Action ComfortCapRejected { get; set; }

        public ContainedCardDragReleaseResult Release(Vector2 screenPosition)
        {
            ValidateScreenPosition(screenPosition, nameof(screenPosition));
            CardView view = GetActiveCardView();
            EnsurePhaseAllows(nameof(Release), TabletopInteractionPhase.Pressed, TabletopInteractionPhase.DraggingObject);
            bool keepRejectedFeedback = false;

            if (stateMachine.Phase == TabletopInteractionPhase.Pressed)
            {
                stateMachine.ReleasePointer();
                try
                {
                    TabletopTransformSnapshot start =
                        previewSession.EndPressWithoutReconcileAndCapture(view);
                    RestoreSourceLayout();
                    previewSession.AnimateReturnFrom(view, start);
                    return ContainedCardDragReleaseResult.ClickReleased();
                }
                finally
                {
                    feedback?.Clear();
                    CompleteLifecycleWithoutStateMachineMutation();
                }
            }

            if (trayDragActive
                && !trayDragOnTable
                && trayExitInProgress
                && !TryMoveTrayCardToTable(view, screenPosition))
            {
                // Could not land on the table: treat the release as a return to the tray.
                trayExitInProgress = false;
            }

            if (view.PhysicalObject != null && !(trayDragActive && !trayDragOnTable))
            {
                view.PhysicalObject.Follow(screenPosition);
            }

            stateMachine.ReleasePointer();
            try
            {
                bool resolved = dropTargetResolver.TryResolve(screenPosition, out CardDropTarget target);
                if (trayDragActive
                    && !trayDragOnTable
                    && (!resolved
                        || target.Kind != CardDropTargetKind.Container
                        || target.ContainerId != sourceContainerId))
                {
                    // Released without leaving the tray: it is a reorder or a return.
                    target = CardDropTarget.ForContainer(sourceContainerId);
                    resolved = true;
                }
                if (view.PhysicalObject != null && (!resolved || target.Kind != CardDropTargetKind.Container))
                {
                    target = CardDropTarget.ForTabletop(new TabletopPose(
                        view.PhysicalObject.LayoutCoordinate,
                        view.BoundState.Pose.RotationDegrees, view.BoundState.Pose.Layer, view.BoundState.Pose.LocalOrder));
                    resolved = true;
                }
                if (!resolved)
                {
                    TabletopTransformSnapshot start = EndPresentationWithoutReconcile(view);
                    RestoreSourceLayout();
                    previewSession.AnimateReturnFrom(view, start);
                    stateMachine.BeginCancellation();
                    stateMachine.CompleteCancellation();
                    return ContainedCardDragReleaseResult.ProjectionFailed();
                }

                if (target.Kind == CardDropTargetKind.None)
                {
                    TabletopTransformSnapshot start = EndPresentationWithoutReconcile(view);
                    RestoreSourceLayout();
                    previewSession.AnimateReturnFrom(view, start);
                    stateMachine.BeginCancellation();
                    stateMachine.CompleteCancellation();
                    return ContainedCardDragReleaseResult.NoTarget();
                }

                if (target.Kind == CardDropTargetKind.Container && target.ContainerId == sourceContainerId)
                {
                    if (TryCompleteHandReorder(view, screenPosition, out ContainedCardDragReleaseResult reorderResult))
                    {
                        return reorderResult;
                    }

                    TabletopTransformSnapshot start = EndPresentationWithoutReconcile(view);
                    RestoreSourceLayout();
                    previewSession.AnimateReturnFrom(view, start);
                    stateMachine.BeginCancellation();
                    stateMachine.CompleteCancellation();
                    return ContainedCardDragReleaseResult.SameSource();
                }

                if (target.Kind == CardDropTargetKind.Container
                    && IsContainerAtComfortCap != null
                    && IsContainerAtComfortCap(target.ContainerId))
                {
                    TabletopTransformSnapshot rejectedStart = EndPresentationWithoutReconcile(view);
                    RestoreSourceLayout();
                    previewSession.AnimateReturnFrom(view, rejectedStart);
                    feedback?.ShowRejected(sourceContainerId, target);
                    ComfortCapRejected?.Invoke();
                    stateMachine.BeginCancellation();
                    stateMachine.CompleteCancellation();
                    keepRejectedFeedback = true;
                    return ContainedCardDragReleaseResult.Cancelled();
                }

                TabletopTransformSnapshot transferStart = EndPresentationWithoutReconcile(view);
                CardTransferInteractionResult transferResult = transferCoordinator.Transfer(view, target);
                ContainedCardDragReleaseResult releaseResult = MapTransferResult(
                    transferResult,
                    view,
                    transferStart);
                if (releaseResult.Status == ContainedCardDragReleaseStatus.TransferAccepted)
                {
                    stateMachine.CompleteAcceptance();
                }
                else
                {
                    feedback?.ShowRejected(sourceContainerId, target);
                    stateMachine.BeginCancellation();
                    stateMachine.CompleteCancellation();
                    keepRejectedFeedback = true;
                }

                return releaseResult;
            }
            finally
            {
                if (!keepRejectedFeedback)
                {
                    feedback?.Clear();
                }

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
            feedback?.Clear();
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
                TabletopTransformSnapshot start = EndPresentationWithoutReconcile(GetActiveCardView());
                RestoreSourceLayout();
                previewSession.AnimateReturnFrom(GetActiveCardView(), start);
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

                CardView view = GetActiveCardView();
                TabletopTransformSnapshot start = EndPresentationWithoutReconcile(view);
                RestoreSourceLayout();
                previewSession.AnimateReturnFrom(view, start);
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

        private ContainedCardDragReleaseResult MapTransferResult(
            CardTransferInteractionResult transferResult,
            CardView view,
            TabletopTransformSnapshot transferStart)
        {
            if (transferResult.TransferAttempted)
            {
                return ContainedCardDragReleaseResult.FromTransferResult(transferResult);
            }

            RestoreSourceLayout();
            previewSession.AnimateReturnFrom(view, transferStart);
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
            handReorderPreviewActive = false;
            handReorderTargetIndex = -1;
        }

        private void UpdateHandReorderPreview(
            CardView view,
            TableCoordinate pointerCoordinate,
            Vector2 screenPosition)
        {
            if (handView == null
                || reorderHandCard == null
                || !ReferenceEquals(sourceLayoutView, handView)
                || !dropTargetResolver.TryResolve(screenPosition, out CardDropTarget target)
                || target.Kind != CardDropTargetKind.Container
                || target.ContainerId != sourceContainerId
                || !TryGetHandReorderTargetIndex(view, pointerCoordinate, screenPosition, out int targetIndex))
            {
                ClearHandReorderPreview(view);
                return;
            }

            if (!handReorderPreviewActive || handReorderTargetIndex != targetIndex)
            {
                handView.ApplyReorderPreview(view, targetIndex);
            }

            handReorderPreviewActive = true;
            handReorderTargetIndex = targetIndex;
        }

        private void ClearHandReorderPreview(CardView view)
        {
            if (!handReorderPreviewActive || handView == null || !handView.IsBound)
            {
                handReorderPreviewActive = false;
                handReorderTargetIndex = -1;
                return;
            }

            handView.ClearReorderPreview(view);
            handReorderPreviewActive = false;
            handReorderTargetIndex = -1;
        }

        private bool TryCompleteHandReorder(
            CardView view,
            Vector2 screenPosition,
            out ContainedCardDragReleaseResult releaseResult)
        {
            releaseResult = default;
            TableCoordinate coordinate = default(TableCoordinate);
            if (handView == null
                || reorderHandCard == null
                || !ReferenceEquals(sourceLayoutView, handView)
                || (!handView.IsTrayMode
                    && !pointerProjector.TryProjectScreenPoint(screenPosition, out coordinate))
                || !TryGetHandReorderTargetIndex(view, coordinate, screenPosition, out int targetIndex))
            {
                return false;
            }

            TabletopTransformSnapshot start = EndPresentationWithoutReconcile(view);
            int currentIndex = handView.ContainerState.IndexOf(view.ObjectId);
            if (targetIndex == currentIndex)
            {
                RestoreSourceLayout();
                previewSession.AnimateReturnFrom(view, start);
                stateMachine.BeginCancellation();
                stateMachine.CompleteCancellation();
                releaseResult = ContainedCardDragReleaseResult.SameSource();
                return true;
            }

            ReorderContainerResult result = reorderHandCard(view, targetIndex);
            if (result.Succeeded)
            {
                stateMachine.CompleteAcceptance();
                releaseResult = ContainedCardDragReleaseResult.HandReordered();
            }
            else
            {
                stateMachine.BeginCancellation();
                stateMachine.CompleteCancellation();
                releaseResult = ContainedCardDragReleaseResult.Cancelled();
            }

            handReorderPreviewActive = false;
            handReorderTargetIndex = -1;
            return true;
        }

        private bool TryGetHandReorderTargetIndex(
            CardView view,
            TableCoordinate pointerCoordinate,
            Vector2 screenPosition,
            out int targetIndex)
        {
            return handView.IsTrayMode
                ? handView.TryGetTrayReorderTargetIndex(view, screenPosition, out targetIndex)
                : handView.TryGetReorderTargetIndex(view, pointerCoordinate, out targetIndex);
        }

        /// <summary>
        /// Table pose a tray card lands at: on the surface under the pointer, facing the owner, its
        /// collider bottom slightly above the surface. Uses the authored collider so it is valid while
        /// the card is still in its tray presentation.
        /// </summary>
        private bool TryResolveTrayTablePose(
            CardView view,
            Vector2 screenPosition,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            Vector3 point;
            PhysicalTabletopSurfaces surfaces = dropTargetResolver.PhysicalSurfaces;
            if (surfaces != null && surfaces.TryPointer(screenPosition, out RaycastHit surfaceHit))
            {
                point = surfaceHit.point;
            }
            else if (pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate coordinate))
            {
                point = pointerProjector.CoordinateConverter.ToWorldPosition(coordinate);
            }
            else
            {
                return false;
            }

            Vector3 up = pointerProjector.CoordinateConverter.WorldUp;
            Vector3 facing = Vector3.ProjectOnPlane(handView.TrayRig.Anchor.forward, up);
            rotation = facing.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(facing.normalized, up)
                : view.transform.rotation;
            BoxCollider box = view.GetComponent<BoxCollider>();
            float pivotToBottom = box != null
                ? -(box.center.y - (box.size.y * 0.5f)) * view.BaseLocalScale.y
                : 0f;
            position = point + (up * (pivotToBottom + TrayExitLift));
            return true;
        }

        /// <summary>
        /// The exit flight has landed: restore the table presentation (full size, shadows, solid
        /// colliders) at the table pose and start an ordinary physical hold from there. The card stays a
        /// Hand member until the release is committed.
        /// </summary>
        private bool TryMoveTrayCardToTable(CardView view, Vector2 screenPosition)
        {
            trayExitInProgress = false;
            handView.EndTrayDrag();
            if (!TryResolveTrayTablePose(view, screenPosition, out Vector3 position, out Quaternion rotation))
            {
                return false;
            }

            view.ApplyContainerWorldPose(position, rotation);
            Physics.SyncTransforms();
            if (!view.PhysicalObject.SnapHeldPreview(position, rotation))
            {
                return false;
            }

            view.PhysicalObject.PreparePointerAnchor(screenPosition);
            trayDragOnTable = true;
            return true;
        }

        private TabletopTransformSnapshot EndPresentationWithoutReconcile(CardView view)
        {
            if (previewSession.IsActive)
            {
                return previewSession.EndPreviewWithoutReconcileAndCapture();
            }

            return previewSession.EndPressWithoutReconcileAndCapture(view);
        }

        private TabletopPose ApplyStackMagnetism(TabletopPose previewPose, Vector2 screenPosition)
        {
            if (magneticDistance <= 0f
                || !dropTargetResolver.TryResolve(screenPosition, sourceContainerId, out CardDropTarget target)
                || target.Kind != CardDropTargetKind.Container
                || !TargetWouldAccept(target)
                || !layoutViewLookup.TryGet(target.ContainerId, out IContainerLayoutView layoutView)
                || !(layoutView is StackView stackView)
                || stackView.PlacementState == null)
            {
                return previewPose;
            }

            int destinationIndex = stackView.ContainerState.Count;
            TabletopPose stackPose = stackView.PlacementState.Pose;
            TableCoordinate magneticCoordinate = new TableCoordinate(
                stackPose.Position.X + (destinationIndex * stackView.TableOffsetPerCard),
                stackPose.Position.Y + (destinationIndex * stackView.TableOffsetPerCard));
            double deltaX = magneticCoordinate.X - previewPose.Position.X;
            double deltaY = magneticCoordinate.Y - previewPose.Position.Y;
            double distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (distance >= magneticDistance)
            {
                return previewPose;
            }

            float strength = 1f - Mathf.Clamp01((float)(distance / magneticDistance));
            strength = strength * strength * (3f - (2f * strength));
            TableCoordinate alignedCoordinate = new TableCoordinate(
                previewPose.Position.X + (deltaX * strength),
                previewPose.Position.Y + (deltaY * strength));
            return new TabletopPose(
                alignedCoordinate,
                Mathf.LerpAngle(previewPose.RotationDegrees, stackPose.RotationDegrees, strength),
                previewPose.Layer,
                previewPose.LocalOrder);
        }

        private bool TryApplyConsolePlacementPreview(CardView view, Vector2 screenPosition)
        {
            if (!dropTargetResolver.TryResolve(screenPosition, sourceContainerId, out CardDropTarget target)
                || target.Kind != CardDropTargetKind.Container
                || !TargetWouldAccept(target)
                || !layoutViewLookup.TryGet(target.ContainerId, out IContainerLayoutView layoutView)
                || !(layoutView is ConsoleSlotView consoleSlotView))
            {
                return false;
            }

            if (view.PhysicalObject != null)
            {
                Quaternion currentOrientation = Quaternion.Euler(0f, view.transform.eulerAngles.y, 0f);
                return view.PhysicalObject.SnapHeldPreview(
                    consoleSlotView.PlacementPreviewWorldPosition(),
                    currentOrientation);
            }

            previewSession.UpdatePose(consoleSlotView.CreatePlacementPreviewPose(view));
            return true;
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

            return !container.IsFull
                && (IsContainerAtComfortCap == null || !IsContainerAtComfortCap(target.ContainerId));
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
            handReorderPreviewActive = false;
            handReorderTargetIndex = -1;
            trayDragActive = false;
            trayDragOnTable = false;
            trayExitInProgress = false;
            handView?.EndTrayDrag();
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
