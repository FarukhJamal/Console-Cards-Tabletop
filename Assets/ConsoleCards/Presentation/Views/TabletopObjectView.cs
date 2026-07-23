using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Views
{
    /// <summary>
    /// Base Unity View that projects an accepted tabletop object pose from Runtime State.
    /// </summary>
    public abstract class TabletopObjectView : MonoBehaviour
    {
        private TabletopObjectState boundState;
        private TabletopCoordinateConverter coordinateConverter;
        private bool isBound;
        private bool isPreviewing;
        private TabletopPose previewPose;

        public bool IsBound => isBound;

        public TabletopObjectId ObjectId => isBound ? boundState.Id : TabletopObjectId.Empty;

        public TabletopObjectState BoundState => isBound ? boundState : null;

        public bool IsPreviewing => isPreviewing;

        public TabletopPose PreviewPose => isPreviewing ? previewPose : TabletopPose.Default;

        protected void BindBase(
            TabletopObjectState state,
            TabletopCoordinateConverter converter,
            TabletopObjectKind expectedKind)
        {
            ValidateBinding(state, converter, expectedKind);
            converter.ToWorldPosition(state.Pose);
            converter.ToWorldRotation(state.Pose);

            boundState = state;
            coordinateConverter = converter;
            isBound = true;

            ApplyAcceptedState();
        }

        public void ApplyAcceptedState()
        {
            EnsureBound();

            Vector3 worldPosition = coordinateConverter.ToWorldPosition(boundState.Pose);
            Quaternion worldRotation = coordinateConverter.ToWorldRotation(boundState.Pose);

            transform.SetPositionAndRotation(worldPosition, worldRotation);
            ClearPreviewState();
        }

        public void ApplyPreviewPose(TabletopPose pose)
        {
            EnsureBound();

            ValidateFinitePreviewPose(pose);
            Vector3 worldPosition = coordinateConverter.ToWorldPosition(pose);
            Quaternion worldRotation = coordinateConverter.ToWorldRotation(pose);

            previewPose = pose;
            isPreviewing = true;
            transform.SetPositionAndRotation(worldPosition, worldRotation);
        }

        public void ReconcileAcceptedState()
        {
            ApplyAcceptedState();
        }

        public virtual void Unbind()
        {
            boundState = null;
            coordinateConverter = null;
            isBound = false;
            ClearPreviewState();

            OnUnbound();
        }

        protected virtual void OnUnbound()
        {
        }

        private static void ValidateBinding(
            TabletopObjectState state,
            TabletopCoordinateConverter converter,
            TabletopObjectKind expectedKind)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            if (state.Kind != expectedKind)
            {
                throw new ArgumentException($"Tabletop object kind must be {expectedKind}.", nameof(state));
            }

            if (state.Id.IsEmpty)
            {
                throw new ArgumentException("Tabletop object ID cannot be empty.", nameof(state));
            }

            if (state.DefinitionId.IsEmpty)
            {
                throw new ArgumentException("Object definition ID cannot be empty.", nameof(state));
            }
        }

        private static void ValidateFinitePreviewPose(TabletopPose pose)
        {
            if (!IsFinite(pose.Position.X) || !IsFinite(pose.Position.Y) || !IsFinite(pose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(pose));
            }
        }

        private void EnsureBound()
        {
            if (!isBound)
            {
                throw new InvalidOperationException("TabletopObjectView is not bound to Runtime State.");
            }
        }

        private void ClearPreviewState()
        {
            previewPose = TabletopPose.Default;
            isPreviewing = false;
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
