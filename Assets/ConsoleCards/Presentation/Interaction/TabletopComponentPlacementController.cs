using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// Owns one Presentation-only component placement preview. Authoritative creation is
    /// requested only after the player confirms a projected tabletop pose.
    /// </summary>
    internal sealed class TabletopComponentPlacementController
    {
        private const float PreviewHeight = 0.035f;

        private readonly TabletopPointerProjector pointerProjector;
        private readonly TabletopCoordinateConverter coordinateConverter;
        private readonly Func<TabletopComponentKind, int, TabletopPose, bool> commitPlacement;

        private GameObject previewRoot;
        private TabletopComponentKind componentKind;
        private int dieSideCount;
        private float rotationDegrees;
        private int layer;
        private int localOrder;
        private TabletopPose previewPose;
        private bool hasValidPreviewPose;

        public TabletopComponentPlacementController(
            TabletopPointerProjector pointerProjector,
            TabletopCoordinateConverter coordinateConverter,
            Func<TabletopComponentKind, int, TabletopPose, bool> commitPlacement)
        {
            this.pointerProjector = pointerProjector
                ?? throw new ArgumentNullException(nameof(pointerProjector));
            this.coordinateConverter = coordinateConverter
                ?? throw new ArgumentNullException(nameof(coordinateConverter));
            this.commitPlacement = commitPlacement
                ?? throw new ArgumentNullException(nameof(commitPlacement));
        }

        public bool IsActive => previewRoot != null;

        public TabletopComponentKind ComponentKind => componentKind;

        public int DieSideCount => dieSideCount;

        public void Begin(
            TabletopComponentKind requestedKind,
            int requestedDieSideCount,
            GameObject requestedPreviewRoot,
            float requestedRotationDegrees,
            int requestedLayer,
            int requestedLocalOrder)
        {
            if (requestedPreviewRoot == null)
            {
                throw new ArgumentNullException(nameof(requestedPreviewRoot));
            }

            if (!IsFinite(requestedRotationDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(requestedRotationDegrees));
            }

            Cancel();
            componentKind = requestedKind;
            dieSideCount = requestedDieSideCount;
            previewRoot = requestedPreviewRoot;
            rotationDegrees = requestedRotationDegrees;
            layer = requestedLayer;
            localOrder = requestedLocalOrder;
            hasValidPreviewPose = false;
            previewRoot.SetActive(false);
        }

        /// <summary>
        /// Returns true when an active placement owns this pointer frame.
        /// </summary>
        public bool HandlePointerFrame(
            Vector2 screenPosition,
            bool pointerBlockedByUi,
            bool confirmPressedThisFrame,
            bool cancelPressedThisFrame)
        {
            if (!IsActive)
            {
                return false;
            }

            UpdatePreview(screenPosition, pointerBlockedByUi);
            if (cancelPressedThisFrame)
            {
                Cancel();
                return true;
            }

            if (confirmPressedThisFrame
                && !pointerBlockedByUi
                && hasValidPreviewPose
                && commitPlacement(componentKind, dieSideCount, previewPose))
            {
                CompletePreview();
            }

            return true;
        }

        public void Cancel()
        {
            CompletePreview();
        }

        private void UpdatePreview(Vector2 screenPosition, bool pointerBlockedByUi)
        {
            if (pointerBlockedByUi
                || !pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate coordinate))
            {
                hasValidPreviewPose = false;
                if (previewRoot.activeSelf)
                {
                    previewRoot.SetActive(false);
                }

                return;
            }

            previewPose = new TabletopPose(
                coordinate,
                rotationDegrees,
                layer,
                localOrder);
            Vector3 worldPosition = coordinateConverter.ToWorldPosition(previewPose);
            Quaternion worldRotation = coordinateConverter.ToWorldRotation(previewPose);
            previewRoot.transform.SetPositionAndRotation(
                worldPosition + (Vector3.up * PreviewHeight),
                worldRotation);
            if (!previewRoot.activeSelf)
            {
                previewRoot.SetActive(true);
            }

            hasValidPreviewPose = true;
        }

        private void CompletePreview()
        {
            if (previewRoot != null)
            {
                GameObject root = previewRoot;
                previewRoot = null;
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
            }

            componentKind = default;
            dieSideCount = 0;
            rotationDegrees = 0f;
            layer = 0;
            localOrder = 0;
            previewPose = TabletopPose.Default;
            hasValidPreviewPose = false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
