using System;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;

namespace ConsoleCards.Presentation.Interaction
{
    public sealed class TabletopRotationCoordinator
    {
        private readonly TabletopSelectionState selectionState;
        private readonly LocalInteractionLockService lockService;
        private readonly RotateObjectUseCase rotateUseCase;

        public TabletopRotationCoordinator(
            MatchState matchState,
            PlayerId requestedByPlayerId,
            InteractionOwnerId interactionOwnerId,
            TabletopSelectionState selectionState,
            LocalInteractionLockService lockService,
            RotateObjectUseCase rotateUseCase)
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
            this.rotateUseCase = rotateUseCase ?? throw new ArgumentNullException(nameof(rotateUseCase));

            RequestedByPlayerId = requestedByPlayerId;
            InteractionOwnerId = interactionOwnerId;
        }

        public MatchState MatchState { get; }

        public PlayerId RequestedByPlayerId { get; }

        public InteractionOwnerId InteractionOwnerId { get; }

        public TabletopSelectionState SelectionState => selectionState;

        public LocalInteractionLockService LockService => lockService;

        public RotateObjectUseCase RotateUseCase => rotateUseCase;

        public RotationInteractionResult RotateSelected(float rotationDeltaDegrees)
        {
            ValidateRotationDelta(rotationDeltaDegrees, nameof(rotationDeltaDegrees));

            if (rotationDeltaDegrees == 0f)
            {
                return RotationInteractionResult.NoRotationRequested();
            }

            bool hadStoredSelectedView = !ReferenceEquals(selectionState.SelectedView, null);
            selectionState.ClearUnavailable();
            if (!selectionState.HasSelection)
            {
                return hadStoredSelectedView
                    ? RotationInteractionResult.SelectionUnavailable()
                    : RotationInteractionResult.NoSelection();
            }

            TabletopObjectView selectedView = selectionState.SelectedView;
            if (selectedView.IsPreviewing)
            {
                throw new InvalidOperationException("Selected object is already in a temporary preview state.");
            }

            if (selectedView.BoundState.IsUserLocked)
            {
                return RotationInteractionResult.ObjectUserLocked();
            }

            InteractionLockAcquireResult acquireResult = lockService.Acquire(
                selectedView.ObjectId,
                InteractionOwnerId);
            if (acquireResult.Status == InteractionLockAcquireStatus.Conflict)
            {
                return RotationInteractionResult.LocalLockConflict();
            }

            bool acquiredByThisCall = acquireResult.Status == InteractionLockAcquireStatus.Acquired;
            try
            {
                float targetRotationDegrees = CalculateTargetRotation(
                    selectedView.BoundState.Pose.RotationDegrees,
                    rotationDeltaDegrees);
                CommandContext context = new CommandContext(
                    CommandId.New(),
                    MatchState.Id,
                    RequestedByPlayerId,
                    MatchState.Revision);
                RotateObjectCommand command = new RotateObjectCommand(
                    context,
                    selectedView.ObjectId,
                    targetRotationDegrees);

                RotateObjectResult rotateResult = rotateUseCase.Execute(MatchState, command);
                if (selectedView != null && selectedView.IsBound)
                {
                    selectedView.ApplyAcceptedState();
                }

                return RotationInteractionResult.FromRotateResult(rotateResult);
            }
            finally
            {
                if (acquiredByThisCall)
                {
                    lockService.Release(selectedView.ObjectId, InteractionOwnerId);
                }
            }
        }

        private static float CalculateTargetRotation(float currentRotationDegrees, float rotationDeltaDegrees)
        {
            double targetRotationDegrees = (double)currentRotationDegrees + rotationDeltaDegrees;
            if (!IsFinite(targetRotationDegrees)
                || targetRotationDegrees > float.MaxValue
                || targetRotationDegrees < -float.MaxValue)
            {
                throw new OverflowException("Calculated target rotation is outside finite float range.");
            }

            float targetRotation = (float)targetRotationDegrees;
            if (!IsFinite(targetRotation))
            {
                throw new OverflowException("Calculated target rotation is not finite.");
            }

            return targetRotation;
        }

        private static void ValidateRotationDelta(float rotationDeltaDegrees, string parameterName)
        {
            if (!IsFinite(rotationDeltaDegrees))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
