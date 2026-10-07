using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Interaction;
using UnityEngine;
using UnityEngine.Rendering;

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
        // Overlay presentation (e.g. the camera hand tray): scale, shadows and solid colliders are
        // changed while it is active and restored by every other pose path.
        private bool isOverlayPresentation;
        private Vector3 overlayBaseScale;
        private List<Renderer> overlayRenderers;
        private List<ShadowCastingMode> overlayShadowModes;
        private List<bool> overlayReceiveShadows;
        private List<Collider> overlayColliders;
        private List<Collider> overlayTriggerColliders;

        public bool IsBound => isBound;
        public PhysicalLooseObject PhysicalObject { get; internal set; }
        public void RefreshAcceptedAppearance() => OnAcceptedStateApplied();

        public TabletopObjectId ObjectId => isBound ? boundState.Id : TabletopObjectId.Empty;

        public TabletopObjectState BoundState => isBound ? boundState : null;

        public bool IsPreviewing => isPreviewing;

        public TabletopPose PreviewPose => isPreviewing ? previewPose : TabletopPose.Default;

        public bool IsContainerLayoutApplied => isContainerLayoutApplied;

        public TabletopPose ContainerLayoutPose => isContainerLayoutApplied ? containerLayoutPose : TabletopPose.Default;

        public bool IsOverlayPresentation => isOverlayPresentation;

        /// <summary>The authored local scale, also while an overlay presentation scales the View.</summary>
        public Vector3 BaseLocalScale => isOverlayPresentation ? overlayBaseScale : transform.localScale;

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
            ClearOverlayPresentation();

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
            ClearOverlayPresentation();

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
            ClearOverlayPresentation();
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

        /// <summary>
        /// Places a contained object at an explicit world pose (for layouts that are not on the table plane).
        /// Keeps the contained-layout flags of ApplyContainerLayoutPose; physics interpolation stays off.
        /// </summary>
        public void ApplyContainerWorldPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            EnsureBound();
            ClearOverlayPresentation();
            ApplyContainerWorldPoseCore(worldPosition, worldRotation);
        }

        /// <summary>
        /// ApplyContainerWorldPose as an overlay: the View is scaled from its authored scale, casts and
        /// receives no shadows, and its solid colliders become triggers so physics queries ignore it.
        /// Any other pose path restores all three.
        /// </summary>
        internal void ApplyContainerOverlayPose(Vector3 worldPosition, Quaternion worldRotation, float scaleFactor)
        {
            EnsureBound();
            if (!IsFinite(scaleFactor) || scaleFactor <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(scaleFactor));
            }

            BeginOverlayPresentation();
            ApplyContainerWorldPoseCore(worldPosition, worldRotation);
            transform.localScale = overlayBaseScale * scaleFactor;
        }

        /// <summary>Restores scale, shadows and colliders changed by ApplyContainerOverlayPose.</summary>
        internal void ClearOverlayPresentation()
        {
            if (!isOverlayPresentation)
            {
                return;
            }

            isOverlayPresentation = false;
            transform.localScale = overlayBaseScale;
            for (int i = 0; i < overlayRenderers.Count; i++)
            {
                Renderer overlayRenderer = overlayRenderers[i];
                if (overlayRenderer == null) continue;
                overlayRenderer.shadowCastingMode = overlayShadowModes[i];
                overlayRenderer.receiveShadows = overlayReceiveShadows[i];
            }

            for (int i = 0; i < overlayTriggerColliders.Count; i++)
            {
                if (overlayTriggerColliders[i] != null) overlayTriggerColliders[i].isTrigger = false;
            }

            overlayRenderers.Clear();
            overlayShadowModes.Clear();
            overlayReceiveShadows.Clear();
            overlayTriggerColliders.Clear();
        }

        private void ApplyContainerWorldPoseCore(Vector3 worldPosition, Quaternion worldRotation)
        {
            PhysicalObject?.DisableForContainer();
            if (boundState.ContainerId.IsEmpty)
            {
                throw new InvalidOperationException("Container layout can only be applied to contained objects.");
            }

            if (!IsFinite(worldPosition.x) || !IsFinite(worldPosition.y) || !IsFinite(worldPosition.z))
            {
                throw new ArgumentOutOfRangeException(nameof(worldPosition));
            }

            float yaw = worldRotation.eulerAngles.y;
            containerLayoutPose = new TabletopPose(
                coordinateConverter.ToTableCoordinate(worldPosition),
                yaw > 180f ? yaw - 360f : yaw,
                boundState.Pose.Layer,
                boundState.Pose.LocalOrder);
            isContainerLayoutApplied = true;
            transform.SetPositionAndRotation(worldPosition, worldRotation);
        }

        private void BeginOverlayPresentation()
        {
            if (isOverlayPresentation)
            {
                return;
            }

            if (overlayRenderers == null)
            {
                overlayRenderers = new List<Renderer>();
                overlayShadowModes = new List<ShadowCastingMode>();
                overlayReceiveShadows = new List<bool>();
                overlayColliders = new List<Collider>();
                overlayTriggerColliders = new List<Collider>();
            }

            isOverlayPresentation = true;
            overlayBaseScale = transform.localScale;
            GetComponentsInChildren(true, overlayRenderers);
            for (int i = 0; i < overlayRenderers.Count; i++)
            {
                Renderer overlayRenderer = overlayRenderers[i];
                overlayShadowModes.Add(overlayRenderer.shadowCastingMode);
                overlayReceiveShadows.Add(overlayRenderer.receiveShadows);
                overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
                overlayRenderer.receiveShadows = false;
            }

            GetComponentsInChildren(true, overlayColliders);
            for (int i = 0; i < overlayColliders.Count; i++)
            {
                Collider overlayCollider = overlayColliders[i];
                if (overlayCollider.isTrigger
                    || (overlayCollider is MeshCollider meshCollider && !meshCollider.convex))
                {
                    continue;
                }

                overlayCollider.isTrigger = true;
                overlayTriggerColliders.Add(overlayCollider);
            }

            overlayColliders.Clear();
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
            ClearOverlayPresentation();
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
