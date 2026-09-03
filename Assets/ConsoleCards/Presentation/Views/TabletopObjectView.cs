using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Interaction;
using UnityEngine;

namespace ConsoleCards.Presentation.Views
{
    /// <summary>
    /// Projects accepted layout or physical Runtime State without competing with active loose simulation.
    /// </summary>
    public abstract class TabletopObjectView : MonoBehaviour
    {
        private TabletopObjectState boundState;
        private TabletopCoordinateConverter coordinateConverter;
        private bool isBound;
        private bool isPreviewing;
        private TabletopPose previewPose;
        private bool isContainerLayoutApplied;
        private TabletopPose containerLayoutPose;

        public bool IsBound => isBound;
        public PhysicalLooseObject PhysicalObject { get; internal set; }
        public void RefreshAcceptedAppearance() => OnAcceptedStateApplied();

        public TabletopObjectId ObjectId => isBound ? boundState.Id : TabletopObjectId.Empty;

        public TabletopObjectState BoundState => isBound ? boundState : null;

        public bool IsPreviewing => isPreviewing;

        public TabletopPose PreviewPose => isPreviewing ? previewPose : TabletopPose.Default;

        public bool IsContainerLayoutApplied => isContainerLayoutApplied;

        public TabletopPose ContainerLayoutPose => isContainerLayoutApplied ? containerLayoutPose : TabletopPose.Default;

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

            if (PhysicalObject != null && boundState.ContainerId.IsEmpty)
            {
                PhysicalObject.ApplyAccepted();
                ClearPreviewState();
                OnAcceptedStateApplied();
                return;
            }
            PhysicalObject?.DisableForContainer();

            Vector3 worldPosition = coordinateConverter.ToWorldPosition(boundState.Pose);
            Quaternion worldRotation = coordinateConverter.ToWorldRotation(boundState.Pose);

            transform.SetPositionAndRotation(worldPosition, worldRotation);
            ClearPreviewState();
            OnAcceptedStateApplied();
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

        public void ClearPreviewWithoutReconcile()
        {
            EnsureBound();

            if (!isPreviewing && PhysicalObject == null)
            {
                throw new InvalidOperationException("TabletopObjectView is not previewing.");
            }

            ClearPreviewState();
        }

        public void ApplyContainerLayoutPose(TabletopPose pose)
        {
            ApplyContainerLayoutPose(pose, 0f);
        }

        public void ApplyContainerLayoutPose(TabletopPose pose, float additionalWorldHeight)
        {
            EnsureBound();
            PhysicalObject?.DisableForContainer();

            if (boundState.ContainerId.IsEmpty)
            {
                throw new InvalidOperationException("Container layout can only be applied to contained objects.");
            }

            ValidateFiniteContainerLayoutPose(pose);
            if (!IsFinite(additionalWorldHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(additionalWorldHeight));
            }

            Vector3 worldPosition = coordinateConverter.ToWorldPosition(pose);
            Quaternion worldRotation = coordinateConverter.ToWorldRotation(pose);

            containerLayoutPose = pose;
            isContainerLayoutApplied = true;
            transform.SetPositionAndRotation(
                worldPosition + (Vector3.up * additionalWorldHeight),
                worldRotation);
        }

        public void ClearContainerLayout()
        {
            containerLayoutPose = TabletopPose.Default;
            isContainerLayoutApplied = false;
        }

        public void ClearContainerLayoutAndReconcile()
        {
            ClearContainerLayout();
            ApplyAcceptedState();
        }

        public void ReconcileAcceptedState()
        {
            ApplyAcceptedState();
        }

        public virtual void Unbind()
        {
            PhysicalObject?.DisableForContainer();
            PhysicalObject = null;
            boundState = null;
            coordinateConverter = null;
            isBound = false;
            ClearContainerLayout();
            ClearPreviewState();

            OnUnbound();
        }

        protected virtual void OnUnbound()
        {
        }

        protected virtual void OnAcceptedStateApplied()
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

        private static void ValidateFiniteContainerLayoutPose(TabletopPose pose)
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
