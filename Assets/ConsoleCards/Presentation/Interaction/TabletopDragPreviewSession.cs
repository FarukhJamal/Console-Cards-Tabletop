using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;

namespace ConsoleCards.Presentation.Interaction
{
    public sealed class TabletopDragPreviewSession
    {
        private TabletopObjectView activeView;

        public bool IsActive => activeView != null;

        public TabletopObjectView ActiveView => IsActive ? activeView : null;

        public TabletopObjectId ActiveObjectId => IsActive ? activeView.ObjectId : TabletopObjectId.Empty;

        public TabletopPose CurrentPreviewPose => IsActive ? activeView.PreviewPose : TabletopPose.Default;

        public void Begin(TabletopObjectView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (!view.IsBound)
            {
                throw new ArgumentException("Drag preview requires a bound TabletopObjectView.", nameof(view));
            }

            if (IsActive)
            {
                throw new InvalidOperationException("A drag preview session is already active.");
            }

            if (view.IsPreviewing)
            {
                throw new InvalidOperationException("The supplied TabletopObjectView is already previewing.");
            }

            activeView = view;
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

            view.ApplyPreviewPose(preview);
        }

        public void UpdatePose(TabletopPose pose)
        {
            GetActiveView().ApplyPreviewPose(pose);
        }

        public void ReconcileAndEnd()
        {
            TabletopObjectView view = GetActiveView();
            view.ReconcileAcceptedState();
            activeView = null;
        }

        public void CancelAndEnd()
        {
            ReconcileAndEnd();
        }

        public void Reset()
        {
            if (activeView == null)
            {
                activeView = null;
                return;
            }

            if (activeView.IsBound)
            {
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

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
