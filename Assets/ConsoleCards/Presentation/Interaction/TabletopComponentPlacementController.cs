using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// Owns one Presentation-only placement preview. Authoritative component creation or
    /// Container movement is requested only after the Player confirms a projected tabletop pose.
    /// </summary>
    internal sealed class TabletopComponentPlacementController
    {
        private const float PreviewHeight = 0.035f;

        private readonly TabletopPointerProjector pointerProjector;
        private readonly TabletopCoordinateConverter coordinateConverter;
        private readonly Func<TabletopComponentKind, int, TabletopPose, bool> commitComponentPlacement;
        private readonly Action<float> rotationChanged;

        private GameObject previewRoot;
        private TabletopComponentKind componentKind;
        private int dieSideCount;
        private float rotationDegrees;
        private int layer;
        private int localOrder;
        private TabletopPose previewPose;
        private bool hasValidPreviewPose;
        private Func<TabletopPose, bool> activeCommitPlacement;

        public TabletopComponentPlacementController(
            TabletopPointerProjector pointerProjector,
            TabletopCoordinateConverter coordinateConverter,
            Func<TabletopComponentKind, int, TabletopPose, bool> commitPlacement,
            Action<float> placementRotationChanged)
        {
            this.pointerProjector = pointerProjector
                ?? throw new ArgumentNullException(nameof(pointerProjector));
            this.coordinateConverter = coordinateConverter
                ?? throw new ArgumentNullException(nameof(coordinateConverter));
            commitComponentPlacement = commitPlacement
                ?? throw new ArgumentNullException(nameof(commitPlacement));
            rotationChanged = placementRotationChanged
                ?? throw new ArgumentNullException(nameof(placementRotationChanged));
        }

        public bool IsActive => previewRoot != null;

        public TabletopComponentKind ComponentKind => componentKind;

        public int DieSideCount => dieSideCount;

        public float RotationDegrees => rotationDegrees;

        public void Begin(
            TabletopComponentKind requestedKind,
            int requestedDieSideCount,
            GameObject requestedPreviewRoot,
            float requestedRotationDegrees,
            int requestedLayer,
            int requestedLocalOrder)
        {
            BeginInternal(
                requestedKind,
                requestedDieSideCount,
                requestedPreviewRoot,
                requestedRotationDegrees,
                requestedLayer,
                requestedLocalOrder,
                pose => commitComponentPlacement(requestedKind, requestedDieSideCount, pose));
        }

        public void BeginContainerMove(
            GameObject requestedPreviewRoot,
            TabletopPose currentPose,
            Func<TabletopPose, bool> commitPlacement)
        {
            BeginInternal(
                default,
                0,
                requestedPreviewRoot,
                currentPose.RotationDegrees,
                currentPose.Layer,
                currentPose.LocalOrder,
                commitPlacement ?? throw new ArgumentNullException(nameof(commitPlacement)));
        }

        public void BeginCustomComponentPlacement(
            TabletopComponentKind requestedKind,
            GameObject requestedPreviewRoot,
            float requestedRotationDegrees,
            int requestedLayer,
            int requestedLocalOrder,
            Func<TabletopPose, bool> commitPlacement)
        {
            BeginInternal(
                requestedKind,
                0,
                requestedPreviewRoot,
                requestedRotationDegrees,
                requestedLayer,
                requestedLocalOrder,
                commitPlacement ?? throw new ArgumentNullException(nameof(commitPlacement)));
        }

        private void BeginInternal(
            TabletopComponentKind requestedKind,
            int requestedDieSideCount,
            GameObject requestedPreviewRoot,
            float requestedRotationDegrees,
            int requestedLayer,
            int requestedLocalOrder,
            Func<TabletopPose, bool> requestedCommitPlacement)
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
            rotationDegrees = NormalizeDegrees(requestedRotationDegrees);
            layer = requestedLayer;
            localOrder = requestedLocalOrder;
            activeCommitPlacement = requestedCommitPlacement;
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
            bool cancelPressedThisFrame,
            float rotateDelta,
            float rotationStepDegrees)
        {
            if (!IsActive)
            {
                return false;
            }

            if (!IsFinite(rotateDelta))
            {
                throw new ArgumentOutOfRangeException(nameof(rotateDelta));
            }

            if (!IsFinite(rotationStepDegrees) || rotationStepDegrees <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(rotationStepDegrees));
            }

            if (!pointerBlockedByUi && rotateDelta != 0f)
            {
                rotationDegrees = NormalizeDegrees(
                    rotationDegrees + (Math.Sign(rotateDelta) * rotationStepDegrees));
                rotationChanged(rotationDegrees);
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
                && activeCommitPlacement != null
                && activeCommitPlacement(previewPose))
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
            activeCommitPlacement = null;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float NormalizeDegrees(float value)
        {
            float normalized = value % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }
    }
}
