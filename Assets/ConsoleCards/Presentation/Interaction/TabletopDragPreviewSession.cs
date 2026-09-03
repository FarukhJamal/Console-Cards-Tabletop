using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    public sealed class TabletopDragPreviewSession
    {
        private readonly TabletopPresentationTransitionController transitions;
        private readonly float pickupLift;
        private readonly float dragLift;
        private readonly float pickupResponseDuration;
        private readonly float dragFollowSmoothing;
        private readonly float settleDuration;
        private readonly float returnDuration;

        private TabletopObjectView activeView;
        private TabletopObjectView pressedView;

        public TabletopDragPreviewSession()
        {
        }

        internal TabletopDragPreviewSession(
            TabletopPresentationTransitionController transitions,
            float pickupLift,
            float dragLift,
            float pickupResponseDuration,
            float dragFollowSmoothing,
            float settleDuration,
            float returnDuration)
        {
            this.transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            this.pickupLift = ValidateNonNegative(pickupLift, nameof(pickupLift));
            this.dragLift = ValidateNonNegative(dragLift, nameof(dragLift));
            this.pickupResponseDuration = ValidateNonNegative(pickupResponseDuration, nameof(pickupResponseDuration));
            this.dragFollowSmoothing = ValidateNonNegative(dragFollowSmoothing, nameof(dragFollowSmoothing));
            this.settleDuration = ValidateNonNegative(settleDuration, nameof(settleDuration));
            this.returnDuration = ValidateNonNegative(returnDuration, nameof(returnDuration));
        }

        public bool IsActive => activeView != null;

        public TabletopObjectView ActiveView => IsActive ? activeView : null;

        public TabletopObjectId ActiveObjectId => IsActive ? activeView.ObjectId : TabletopObjectId.Empty;

        public TabletopPose CurrentPreviewPose => IsActive ? activeView.PreviewPose : TabletopPose.Default;

        public void BeginPress(TabletopObjectView view)
        {
            ValidateView(view, nameof(view));
            if (pressedView != null || IsActive)
            {
                throw new InvalidOperationException("A drag press response is already active.");
            }

            pressedView = view;
            if (view.PhysicalObject == null) transitions?.BeginPickup(view.transform, pickupLift, pickupResponseDuration);
        }

        public void Begin(TabletopObjectView view)
        {
            ValidateView(view, nameof(view));

            if (IsActive)
            {
                throw new InvalidOperationException("A drag preview session is already active.");
            }

            if (view.IsPreviewing)
            {
                throw new InvalidOperationException("The supplied TabletopObjectView is already previewing.");
            }

            if (pressedView != null && !ReferenceEquals(pressedView, view))
            {
                throw new InvalidOperationException("The pressed View does not match the drag preview View.");
            }

            activeView = view;
            pressedView = null;
        }

        public void UpdatePosition(TableCoordinate coordinate)
        {
            TabletopObjectView view = GetActiveView();
            if (!IsFinite(coordinate.X) || !IsFinite(coordinate.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(coordinate));
            }

            TabletopPose acceptedPose = view.BoundState.Pose;
            TabletopPose preview = new TabletopPose(
                coordinate,
                acceptedPose.RotationDegrees,
                acceptedPose.Layer,
                acceptedPose.LocalOrder);

            ApplyPreview(view, preview);
        }

        public void UpdatePose(TabletopPose pose)
        {
            ApplyPreview(GetActiveView(), pose);
        }

        public void ReconcileAndEnd()
        {
            TabletopObjectView view = GetActiveView();
            TabletopTransformSnapshot start = transitions != null
                ? transitions.StopAndCapture(view.transform)
                : default;
            view.ReconcileAcceptedState();
            transitions?.AnimateFromCurrentResult(view.transform, start, settleDuration);
            activeView = null;
        }

        public void EndPreviewWithoutReconcile()
        {
            EndPreviewWithoutReconcileAndCapture();
        }

        internal TabletopTransformSnapshot EndPreviewWithoutReconcileAndCapture()
        {
            TabletopObjectView view = GetActiveView();
            TabletopTransformSnapshot snapshot = transitions != null
                ? transitions.StopAndCapture(view.transform)
                : default;
            view.ClearPreviewWithoutReconcile();
            activeView = null;
            return snapshot;
        }

        public void CancelAndEnd()
        {
            TabletopObjectView view = GetActiveView();
            view.PhysicalObject?.Cancel();
            TabletopTransformSnapshot start = transitions != null
                ? transitions.StopAndCapture(view.transform)
                : default;
            view.ReconcileAcceptedState();
            transitions?.AnimateFromCurrentResult(view.transform, start, returnDuration);
            activeView = null;
        }

        public void EndPressAndReturn(TabletopObjectView view)
        {
            ValidatePressedView(view);
            TabletopTransformSnapshot start = transitions != null
                ? transitions.StopAndCapture(view.transform)
                : default;
            view.ReconcileAcceptedState();
            transitions?.AnimateFromCurrentResult(view.transform, start, returnDuration);
            pressedView = null;
        }

        internal TabletopTransformSnapshot EndPressWithoutReconcileAndCapture(TabletopObjectView view)
        {
            ValidatePressedView(view);
            TabletopTransformSnapshot snapshot = transitions != null
                ? transitions.StopAndCapture(view.transform)
                : default;
            pressedView = null;
            return snapshot;
        }

        internal void AnimateReturnFrom(
            TabletopObjectView view,
            TabletopTransformSnapshot start)
        {
            ValidateView(view, nameof(view));
            transitions?.AnimateFromCurrentResult(view.transform, start, returnDuration);
        }

        public void Reset()
        {
            activeView?.PhysicalObject?.Cancel();
            if (pressedView != null)
            {
                if (pressedView.IsBound)
                {
                    transitions?.Stop(pressedView.transform, true);
                    pressedView.ReconcileAcceptedState();
                }

                pressedView = null;
            }

            if (activeView == null)
            {
                activeView = null;
                return;
            }

            if (activeView.IsBound)
            {
                transitions?.Stop(activeView.transform, true);
                activeView.ReconcileAcceptedState();
            }

            activeView = null;
        }

        private TabletopObjectView GetActiveView()
        {
            if (activeView == null)
            {
                activeView = null;
                throw new InvalidOperationException("No drag preview session is active.");
            }

            return activeView;
        }

        private void ApplyPreview(TabletopObjectView view, TabletopPose pose)
        {
            if (transitions == null)
            {
                view.ApplyPreviewPose(pose);
                return;
            }

            TabletopTransformSnapshot current = transitions.StopAndCapture(view.transform);
            view.ApplyPreviewPose(pose);
            Vector3 targetPosition = view.transform.position;
            Quaternion targetRotation = view.transform.rotation;
            view.transform.SetPositionAndRotation(current.Position, current.Rotation);
            view.transform.localScale = current.LocalScale;
            transitions.Follow(
                view.transform,
                targetPosition,
                targetRotation,
                dragLift,
                dragFollowSmoothing);
        }

        private static void ValidateView(TabletopObjectView view, string parameterName)
        {
            if (view == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (!view.IsBound)
            {
                throw new ArgumentException("Drag preview requires a bound TabletopObjectView.", parameterName);
            }
        }

        private void ValidatePressedView(TabletopObjectView view)
        {
            ValidateView(view, nameof(view));
            if (pressedView == null || !ReferenceEquals(pressedView, view))
            {
                throw new InvalidOperationException("The supplied View does not own the active press response.");
            }
        }

        private static float ValidateNonNegative(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
