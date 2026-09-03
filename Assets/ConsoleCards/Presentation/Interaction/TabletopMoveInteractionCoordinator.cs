using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    public sealed class TabletopMoveInteractionCoordinator
    {
        private readonly TabletopSelectionState selectionState;
        private readonly TabletopObjectHitResolver hitResolver;
        private readonly TabletopPointerProjector pointerProjector;
        private readonly LocalInteractionLockService lockService;
        private readonly TabletopInteractionStateMachine stateMachine;
        private readonly TabletopDragPreviewSession previewSession;
        private readonly MoveObjectUseCase moveUseCase;
        private readonly CardDropTargetResolver cardDropTargetResolver;
        private readonly CardTransferInteractionCoordinator cardTransferCoordinator;
        private readonly ContainerLayoutViewLookup layoutViewLookup;
        private readonly IContainedCardDragFeedback cardDragFeedback;
        private readonly float magneticDistance;
        private readonly TokenDropTargetResolver tokenDropTargetResolver;
        private readonly TransferTokenUseCase tokenTransferUseCase;
        private readonly IReadOnlyList<TokenContainerView> tokenContainerViews;
        private TabletopObjectView activeView;

        public TabletopMoveInteractionCoordinator(
            MatchState matchState,
            PlayerId requestedByPlayerId,
            InteractionOwnerId interactionOwnerId,
            TabletopSelectionState selectionState,
            TabletopObjectHitResolver hitResolver,
            TabletopPointerProjector pointerProjector,
            LocalInteractionLockService lockService,
            TabletopInteractionStateMachine stateMachine,
            TabletopDragPreviewSession previewSession,
            MoveObjectUseCase moveUseCase)
            : this(
                matchState,
                requestedByPlayerId,
                interactionOwnerId,
                selectionState,
                hitResolver,
                pointerProjector,
                lockService,
                stateMachine,
                previewSession,
                moveUseCase,
                null,
                null)
        {
        }

        public TabletopMoveInteractionCoordinator(
            MatchState matchState,
            PlayerId requestedByPlayerId,
            InteractionOwnerId interactionOwnerId,
            TabletopSelectionState selectionState,
            TabletopObjectHitResolver hitResolver,
            TabletopPointerProjector pointerProjector,
            LocalInteractionLockService lockService,
            TabletopInteractionStateMachine stateMachine,
            TabletopDragPreviewSession previewSession,
            MoveObjectUseCase moveUseCase,
            CardDropTargetResolver cardDropTargetResolver,
            CardTransferInteractionCoordinator cardTransferCoordinator)
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
            this.hitResolver = hitResolver ?? throw new ArgumentNullException(nameof(hitResolver));
            this.pointerProjector = pointerProjector ?? throw new ArgumentNullException(nameof(pointerProjector));
            this.lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
            this.stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            this.previewSession = previewSession ?? throw new ArgumentNullException(nameof(previewSession));
            this.moveUseCase = moveUseCase ?? throw new ArgumentNullException(nameof(moveUseCase));
            this.cardDropTargetResolver = cardDropTargetResolver;
            this.cardTransferCoordinator = cardTransferCoordinator;
            layoutViewLookup = null;
            cardDragFeedback = null;
            magneticDistance = 0f;
            tokenDropTargetResolver = null;
            tokenTransferUseCase = null;
            tokenContainerViews = Array.Empty<TokenContainerView>();

            if (stateMachine.Phase != TabletopInteractionPhase.Idle)
            {
                throw new ArgumentException("Move interaction state machine must begin in Idle.", nameof(stateMachine));
            }

            if (previewSession.IsActive)
            {
                throw new ArgumentException("Move interaction preview session must begin inactive.", nameof(previewSession));
            }

            RequestedByPlayerId = requestedByPlayerId;
            InteractionOwnerId = interactionOwnerId;
        }

        internal TabletopMoveInteractionCoordinator(
            MatchState matchState,
            PlayerId requestedByPlayerId,
            InteractionOwnerId interactionOwnerId,
            TabletopSelectionState selectionState,
            TabletopObjectHitResolver hitResolver,
            TabletopPointerProjector pointerProjector,
            LocalInteractionLockService lockService,
            TabletopInteractionStateMachine stateMachine,
            TabletopDragPreviewSession previewSession,
            MoveObjectUseCase moveUseCase,
            CardDropTargetResolver cardDropTargetResolver,
            CardTransferInteractionCoordinator cardTransferCoordinator,
            ContainerLayoutViewLookup layoutViewLookup,
            IContainedCardDragFeedback cardDragFeedback,
            float magneticDistance)
            : this(
                matchState,
                requestedByPlayerId,
                interactionOwnerId,
                selectionState,
                hitResolver,
                pointerProjector,
                lockService,
                stateMachine,
                previewSession,
                moveUseCase,
                cardDropTargetResolver,
                cardTransferCoordinator)
        {
            this.layoutViewLookup = layoutViewLookup ?? throw new ArgumentNullException(nameof(layoutViewLookup));
            this.cardDragFeedback = cardDragFeedback ?? throw new ArgumentNullException(nameof(cardDragFeedback));
            if (!IsFinite(magneticDistance) || magneticDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(magneticDistance));
            }

            this.magneticDistance = magneticDistance;
            tokenDropTargetResolver = null;
            tokenTransferUseCase = null;
            tokenContainerViews = Array.Empty<TokenContainerView>();
        }

        internal TabletopMoveInteractionCoordinator(
            MatchState matchState,
            PlayerId requestedByPlayerId,
            InteractionOwnerId interactionOwnerId,
            TabletopSelectionState selectionState,
            TabletopObjectHitResolver hitResolver,
            TabletopPointerProjector pointerProjector,
            LocalInteractionLockService lockService,
            TabletopInteractionStateMachine stateMachine,
            TabletopDragPreviewSession previewSession,
            MoveObjectUseCase moveUseCase,
            CardDropTargetResolver cardDropTargetResolver,
            CardTransferInteractionCoordinator cardTransferCoordinator,
            ContainerLayoutViewLookup layoutViewLookup,
            IContainedCardDragFeedback cardDragFeedback,
            float magneticDistance,
            TokenDropTargetResolver tokenResolver,
            TransferTokenUseCase transferTokenUseCase,
            IReadOnlyList<TokenContainerView> containerViews)
            : this(
                matchState,
                requestedByPlayerId,
                interactionOwnerId,
                selectionState,
                hitResolver,
                pointerProjector,
                lockService,
                stateMachine,
                previewSession,
                moveUseCase,
                cardDropTargetResolver,
                cardTransferCoordinator,
                layoutViewLookup,
                cardDragFeedback,
                magneticDistance)
        {
            tokenDropTargetResolver = tokenResolver ?? throw new ArgumentNullException(nameof(tokenResolver));
            tokenTransferUseCase = transferTokenUseCase ?? throw new ArgumentNullException(nameof(transferTokenUseCase));
            tokenContainerViews = containerViews ?? throw new ArgumentNullException(nameof(containerViews));
        }

        public MatchState MatchState { get; }

        public PlayerId RequestedByPlayerId { get; }

        public InteractionOwnerId InteractionOwnerId { get; }

        public TabletopObjectView ActiveView => activeView != null ? activeView : null;

        public bool HasActiveInteraction => ActiveView != null;

        public TabletopInteractionPhase Phase => stateMachine.Phase;

        public bool TryBeginPress(Vector2 screenPosition)
        {
            ValidateScreenPosition(screenPosition, nameof(screenPosition));
            EnsureNoActiveInteraction();
            EnsurePhaseAllows(nameof(TryBeginPress), TabletopInteractionPhase.Idle, TabletopInteractionPhase.Hovering);

            if (!hitResolver.TryResolve(screenPosition, out TabletopObjectView resolvedView))
            {
                selectionState.ClearSelection();
                selectionState.ClearHovered();
                return false;
            }

            if (resolvedView.BoundState.IsUserLocked)
            {
                return false;
            }

            InteractionLockAcquireResult acquireResult = lockService.Acquire(resolvedView.ObjectId, InteractionOwnerId);
            if (!acquireResult.Succeeded)
            {
                return false;
            }

            bool shouldReleaseOnFailure = true;
            try
            {
                selectionState.Select(resolvedView);
                stateMachine.BeginPress(resolvedView.ObjectId, screenPosition);
                activeView = resolvedView;
                previewSession.BeginPress(resolvedView);
                if (resolvedView is CardView)
                {
                    cardDragFeedback?.Begin(ContainerId.Empty);
                }

                shouldReleaseOnFailure = false;
                return true;
            }
            finally
            {
                if (shouldReleaseOnFailure)
                {
                    lockService.Release(resolvedView.ObjectId, InteractionOwnerId);
                    previewSession.Reset();
                    cardDragFeedback?.Clear();
                    activeView = null;
                    stateMachine.Reset();
                }
            }
        }

        public bool UpdatePointer(Vector2 screenPosition)
        {
            ValidateScreenPosition(screenPosition, nameof(screenPosition));
            TabletopObjectView view = GetActiveView();
            EnsurePhaseAllows(nameof(UpdatePointer), TabletopInteractionPhase.Pressed, TabletopInteractionPhase.DraggingObject);

            bool startedDragging = stateMachine.UpdatePointer(screenPosition);
            if (startedDragging)
            {
                previewSession.Begin(view);
            }

            if (stateMachine.Phase != TabletopInteractionPhase.DraggingObject)
            {
                return false;
            }

            if (view.PhysicalObject != null)
            {
                view.PhysicalObject.Follow(screenPosition);
                if (view is CardView) UpdateCardTargetFeedback(screenPosition);
                return true;
            }

            if (!pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate coordinate))
            {
                cardDragFeedback?.Update(ContainerId.Empty, CardDropTarget.None(), false);
                return false;
            }

            if (view is CardView cardView)
            {
                TabletopPose acceptedPose = cardView.CardState.BaseState.Pose;
                TabletopPose previewPose = new TabletopPose(
                    coordinate,
                    acceptedPose.RotationDegrees,
                    acceptedPose.Layer,
                    acceptedPose.LocalOrder);
                previewSession.UpdatePose(ApplyStackMagnetism(previewPose, screenPosition));
                UpdateCardTargetFeedback(screenPosition);
            }
            else
            {
                previewSession.UpdatePosition(coordinate);
            }

            return true;
        }

        public MoveInteractionReleaseResult ReleasePointer(Vector2 screenPosition)
        {
            ValidateScreenPosition(screenPosition, nameof(screenPosition));
            TabletopObjectView view = GetActiveView();
            EnsurePhaseAllows(nameof(ReleasePointer), TabletopInteractionPhase.Pressed, TabletopInteractionPhase.DraggingObject);
            bool keepRejectedFeedback = false;

            try
            {

                if (stateMachine.Phase == TabletopInteractionPhase.Pressed)
                {
                    stateMachine.ReleasePointer();
                    if (!TryReturnContainedTokenAfterPress(view))
                    {
                        previewSession.EndPressAndReturn(view);
                    }
                    lockService.Release(view.ObjectId, InteractionOwnerId);
                    activeView = null;
                    return MoveInteractionReleaseResult.ClickCompleted();
                }

                stateMachine.ReleasePointer();

            if (TryTransferTabletopCardToContainer(
                view,
                screenPosition,
                out MoveInteractionReleaseResult transferReleaseResult,
                out bool transferAccepted))
            {
                lockService.Release(view.ObjectId, InteractionOwnerId);
                if (transferAccepted)
                {
                    stateMachine.CompleteAcceptance();
                }
                else
                {
                    stateMachine.BeginCancellation();
                    stateMachine.CompleteCancellation();
                    keepRejectedFeedback = true;
                }

                activeView = null;
                return transferReleaseResult;
            }

            if (TryTransferToken(
                view,
                screenPosition,
                out MoveInteractionReleaseResult tokenTransferReleaseResult,
                out bool tokenTransferAccepted))
            {
                lockService.Release(view.ObjectId, InteractionOwnerId);
                if (tokenTransferAccepted)
                {
                    stateMachine.CompleteAcceptance();
                }
                else
                {
                    stateMachine.BeginCancellation();
                    stateMachine.CompleteCancellation();
                }

                activeView = null;
                return tokenTransferReleaseResult;
            }

            if (view.PhysicalObject != null)
            {
                previewSession.EndPreviewWithoutReconcile();
                bool accepted = view.PhysicalObject.Release();
                lockService.Release(view.ObjectId, InteractionOwnerId);
                if (accepted) stateMachine.CompleteAcceptance();
                else { stateMachine.BeginCancellation(); stateMachine.CompleteCancellation(); }
                activeView = null;
                return accepted ? MoveInteractionReleaseResult.FromMoveResult(MoveObjectResult.Accepted(MatchState.Revision))
                    : MoveInteractionReleaseResult.ProjectionFailed();
            }

            if (!pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate coordinate))
            {
                stateMachine.BeginCancellation();
                previewSession.CancelAndEnd();
                lockService.Release(view.ObjectId, InteractionOwnerId);
                stateMachine.CompleteCancellation();
                activeView = null;
                return MoveInteractionReleaseResult.ProjectionFailed();
            }

            TabletopPose acceptedPose = view.BoundState.Pose;
            TabletopPose targetPose = new TabletopPose(
                coordinate,
                acceptedPose.RotationDegrees,
                acceptedPose.Layer,
                acceptedPose.LocalOrder);
            CommandContext context = new CommandContext(
                CommandId.New(),
                MatchState.Id,
                RequestedByPlayerId,
                MatchState.Revision);
            MoveObjectCommand command = new MoveObjectCommand(context, view.ObjectId, targetPose);
            MoveObjectResult moveResult = moveUseCase.Execute(MatchState, command);

            if (moveResult.Succeeded)
            {
                previewSession.ReconcileAndEnd();
                lockService.Release(view.ObjectId, InteractionOwnerId);
                stateMachine.CompleteAcceptance();
                activeView = null;
                return MoveInteractionReleaseResult.FromMoveResult(moveResult);
            }

            stateMachine.BeginCancellation();
            previewSession.CancelAndEnd();
            lockService.Release(view.ObjectId, InteractionOwnerId);
            stateMachine.CompleteCancellation();
            activeView = null;
                return MoveInteractionReleaseResult.FromMoveResult(moveResult);
            }
            catch (Exception exception)
            {
                Exception cleanupFailure = null;
                try
                {
                    if (previewSession.IsActive)
                    {
                        previewSession.CancelAndEnd();
                    }
                    else if (!(view is CardView cardView)
                        || cardView.CardState == null
                        || cardView.CardState.BaseState.ContainerId.IsEmpty)
                    {
                        view.ReconcileAcceptedState();
                        ReapplyTokenContainerLayout(view);
                    }
                }
                catch (Exception cleanupException)
                {
                    cleanupFailure = cleanupException;
                }
                finally
                {
                    lockService.Release(view.ObjectId, InteractionOwnerId);
                    stateMachine.Reset();
                    activeView = null;
                }

                if (cleanupFailure != null)
                {
                    throw new InvalidOperationException(
                        "Move interaction failed and Presentation cleanup could not reconcile the active View.",
                        new AggregateException(exception, cleanupFailure));
                }

                throw;
            }
            finally
            {
                if (!keepRejectedFeedback)
                {
                    cardDragFeedback?.Clear();
                }
            }
        }

        private bool TryTransferTabletopCardToContainer(
            TabletopObjectView view,
            Vector2 screenPosition,
            out MoveInteractionReleaseResult releaseResult,
            out bool transferAccepted)
        {
            releaseResult = default;
            transferAccepted = false;

            CardView cardView = view as CardView;
            if (cardView == null
                || cardView.CardState == null
                || !cardView.CardState.BaseState.ContainerId.IsEmpty
                || cardDropTargetResolver == null
                || cardTransferCoordinator == null)
            {
                return false;
            }

            if (!cardDropTargetResolver.TryResolve(screenPosition, out CardDropTarget target)
                || target.Kind != CardDropTargetKind.Container)
            {
                return false;
            }

            TabletopTransformSnapshot transferStart = previewSession.IsActive
                ? previewSession.EndPreviewWithoutReconcileAndCapture()
                : default;

            CardTransferInteractionResult transferResult = cardTransferCoordinator.Transfer(cardView, target);
            releaseResult = MoveInteractionReleaseResult.FromCardTransferResult(transferResult);
            if (transferResult.Succeeded)
            {
                transferAccepted = true;
                return true;
            }

            if (!transferResult.TransferAttempted)
            {
                cardView.PhysicalObject?.Cancel();
                cardView.ClearContainerLayoutAndReconcile();
                previewSession.AnimateReturnFrom(cardView, transferStart);
            }

            cardDragFeedback?.ShowRejected(ContainerId.Empty, target);
            return true;
        }

        private bool TryTransferToken(
            TabletopObjectView view,
            Vector2 screenPosition,
            out MoveInteractionReleaseResult releaseResult,
            out bool transferAccepted)
        {
            releaseResult = default;
            transferAccepted = false;

            TokenView tokenView = view as TokenView;
            if (tokenView == null
                || tokenView.TokenState == null
                || tokenDropTargetResolver == null
                || tokenTransferUseCase == null)
            {
                return false;
            }

            ContainerId sourceContainerId = tokenView.TokenState.BaseState.ContainerId;
            bool hasDestination = tokenDropTargetResolver.TryResolve(
                screenPosition,
                out ContainerId destinationContainerId);
            if (sourceContainerId.IsEmpty && !hasDestination)
            {
                return false;
            }

            TokenContainerView sourceView = FindTokenContainerView(sourceContainerId);
            TokenContainerView destinationView = hasDestination
                ? FindTokenContainerView(destinationContainerId)
                : null;
            TabletopTransformSnapshot transferStart = previewSession.IsActive
                ? previewSession.EndPreviewWithoutReconcileAndCapture()
                : default;

            if ((hasDestination && destinationContainerId == sourceContainerId)
                || (!sourceContainerId.IsEmpty && sourceView == null)
                || (hasDestination && destinationView == null))
            {
                sourceView?.ApplyAcceptedLayout();
                if (sourceView == null)
                {
                    tokenView.ReconcileAcceptedState();
                }

                previewSession.AnimateReturnFrom(tokenView, transferStart);
                releaseResult = MoveInteractionReleaseResult.TokenTransferRejected();
                return true;
            }

            CommandContext context = new CommandContext(
                CommandId.New(),
                MatchState.Id,
                RequestedByPlayerId,
                MatchState.Revision);
            TransferTokenCommand command;
            if (hasDestination)
            {
                command = TransferTokenCommand.ToContainer(
                    context,
                    tokenView.ObjectId,
                    sourceContainerId,
                    destinationContainerId);
            }
            else
            {
                TableCoordinate coordinate;
                if (tokenView.PhysicalObject != null)
                    coordinate = tokenView.PhysicalObject.LayoutCoordinate;
                else if (!pointerProjector.TryProjectScreenPoint(screenPosition, out coordinate))
                {
                    sourceView.ApplyAcceptedLayout();
                    previewSession.AnimateReturnFrom(tokenView, transferStart);
                    releaseResult = MoveInteractionReleaseResult.TokenTransferRejected();
                    return true;
                }

                TabletopPose acceptedPose = tokenView.TokenState.BaseState.Pose;
                command = TransferTokenCommand.ToTabletop(
                    context,
                    tokenView.ObjectId,
                    sourceContainerId,
                    new TabletopPose(
                        coordinate,
                        acceptedPose.RotationDegrees,
                        acceptedPose.Layer,
                        acceptedPose.LocalOrder), tokenView.PhysicalObject?.ReleaseState());
            }

            TransferTokenResult transferResult = tokenTransferUseCase.Execute(MatchState, command);
            releaseResult = MoveInteractionReleaseResult.FromTokenTransferResult(transferResult);
            if (!transferResult.Succeeded)
            {
                tokenView.PhysicalObject?.Cancel();
                sourceView?.ApplyAcceptedLayout();
                destinationView?.ApplyAcceptedLayout();
                if (sourceView == null)
                {
                    tokenView.ClearContainerLayoutAndReconcile();
                }

                previewSession.AnimateReturnFrom(tokenView, transferStart);
                return true;
            }

            sourceView?.ApplyAcceptedLayout();
            if (destinationView != null)
            {
                destinationView.ApplyAcceptedLayout();
            }
            else
            {
                tokenView.ClearContainerLayoutAndReconcile();
            }

            transferAccepted = true;
            return true;
        }

        private TokenContainerView FindTokenContainerView(ContainerId containerId)
        {
            if (containerId.IsEmpty)
            {
                return null;
            }

            for (int i = 0; i < tokenContainerViews.Count; i++)
            {
                TokenContainerView candidate = tokenContainerViews[i];
                if (candidate != null && candidate.IsBound && candidate.ContainerId == containerId)
                {
                    return candidate;
                }
            }

            return null;
        }

        public void Cancel()
        {
            TabletopObjectView view = GetActiveView();
            EnsurePhaseAllows(
                nameof(Cancel),
                TabletopInteractionPhase.Pressed,
                TabletopInteractionPhase.DraggingObject,
                TabletopInteractionPhase.AwaitingAcceptance);

            stateMachine.BeginCancellation();
            if (previewSession.IsActive)
            {
                if (!TryReturnContainedTokenPreview(view))
                {
                    previewSession.CancelAndEnd();
                }
            }
            else if (!TryReturnContainedTokenAfterPress(view))
            {
                previewSession.EndPressAndReturn(view);
            }

            lockService.Release(view.ObjectId, InteractionOwnerId);
            stateMachine.CompleteCancellation();
            activeView = null;
            cardDragFeedback?.Clear();
        }

        public void Reset()
        {
            TabletopObjectView viewToReconcile = activeView;
            if (previewSession.IsActive)
            {
                previewSession.Reset();
            }

            if (activeView != null && activeView.IsBound)
            {
                lockService.Release(activeView.ObjectId, InteractionOwnerId);
            }

            lockService.ReleaseAllForOwner(InteractionOwnerId);
            stateMachine.Reset();
            activeView = null;
            ReapplyTokenContainerLayout(viewToReconcile);
            cardDragFeedback?.Clear();
        }

        private bool TryReturnContainedTokenAfterPress(TabletopObjectView view)
        {
            TokenContainerView sourceView = FindSourceTokenContainerView(view);
            if (sourceView == null)
            {
                return false;
            }

            TabletopTransformSnapshot start = previewSession.EndPressWithoutReconcileAndCapture(view);
            sourceView.ApplyAcceptedLayout();
            previewSession.AnimateReturnFrom(view, start);
            return true;
        }

        private bool TryReturnContainedTokenPreview(TabletopObjectView view)
        {
            TokenContainerView sourceView = FindSourceTokenContainerView(view);
            if (sourceView == null)
            {
                return false;
            }

            TabletopTransformSnapshot start = previewSession.EndPreviewWithoutReconcileAndCapture();
            sourceView.ApplyAcceptedLayout();
            previewSession.AnimateReturnFrom(view, start);
            return true;
        }

        private void ReapplyTokenContainerLayout(TabletopObjectView view)
        {
            FindSourceTokenContainerView(view)?.ApplyAcceptedLayout();
        }

        private TokenContainerView FindSourceTokenContainerView(TabletopObjectView view)
        {
            TokenView tokenView = view as TokenView;
            return tokenView != null && tokenView.TokenState != null
                ? FindTokenContainerView(tokenView.TokenState.BaseState.ContainerId)
                : null;
        }

        private void UpdateCardTargetFeedback(Vector2 screenPosition)
        {
            if (cardDragFeedback == null || cardDropTargetResolver == null)
            {
                return;
            }

            if (!cardDropTargetResolver.TryResolve(screenPosition, out CardDropTarget target))
            {
                cardDragFeedback.Update(ContainerId.Empty, CardDropTarget.None(), false);
                return;
            }

            cardDragFeedback.Update(ContainerId.Empty, target, TargetWouldAccept(target));
        }

        private bool TargetWouldAccept(CardDropTarget target)
        {
            if (target.Kind == CardDropTargetKind.Tabletop)
            {
                return true;
            }

            return target.Kind == CardDropTargetKind.Container
                && !target.ContainerId.IsEmpty
                && MatchState.Containers.TryGetValue(target.ContainerId, out ContainerState container)
                && !container.IsFull;
        }

        private TabletopPose ApplyStackMagnetism(TabletopPose previewPose, Vector2 screenPosition)
        {
            if (magneticDistance <= 0f
                || cardDropTargetResolver == null
                || layoutViewLookup == null
                || !cardDropTargetResolver.TryResolve(screenPosition, out CardDropTarget target)
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
            return new TabletopPose(
                new TableCoordinate(
                    previewPose.Position.X + (deltaX * strength),
                    previewPose.Position.Y + (deltaY * strength)),
                Mathf.LerpAngle(previewPose.RotationDegrees, stackPose.RotationDegrees, strength),
                previewPose.Layer,
                previewPose.LocalOrder);
        }

        private TabletopObjectView GetActiveView()
        {
            if (activeView == null)
            {
                activeView = null;
                throw new InvalidOperationException("No move interaction is active.");
            }

            return activeView;
        }

        private void EnsureNoActiveInteraction()
        {
            if (HasActiveInteraction)
            {
                throw new InvalidOperationException("A move interaction is already active.");
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
