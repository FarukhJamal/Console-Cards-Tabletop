using System;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;
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
                shouldReleaseOnFailure = false;
                return true;
            }
            finally
            {
                if (shouldReleaseOnFailure)
                {
                    lockService.Release(resolvedView.ObjectId, InteractionOwnerId);
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

            if (!pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate coordinate))
            {
                return false;
            }

            previewSession.UpdatePosition(coordinate);
            return true;
        }

        public MoveInteractionReleaseResult ReleasePointer(Vector2 screenPosition)
        {
            ValidateScreenPosition(screenPosition, nameof(screenPosition));
            TabletopObjectView view = GetActiveView();
            EnsurePhaseAllows(nameof(ReleasePointer), TabletopInteractionPhase.Pressed, TabletopInteractionPhase.DraggingObject);

            if (stateMachine.Phase == TabletopInteractionPhase.Pressed)
            {
                stateMachine.ReleasePointer();
                lockService.Release(view.ObjectId, InteractionOwnerId);
                activeView = null;
                return MoveInteractionReleaseResult.ClickCompleted();
            }

            stateMachine.ReleasePointer();

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
                previewSession.CancelAndEnd();
            }

            lockService.Release(view.ObjectId, InteractionOwnerId);
            stateMachine.CompleteCancellation();
            activeView = null;
        }

        public void Reset()
        {
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
