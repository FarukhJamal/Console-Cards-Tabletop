using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// Resolves a screen position to the nearest explicit Container drop target, then to an authored physical surface.
    /// The projector fallback is retained only for callers without the physical integration.
    /// Container hits use an instance-local fixed buffer; when the buffer is filled, only the returned hits are sorted
    /// and considered, and resolution still remains read-only and deterministic for that returned set.
    /// </summary>
    public sealed class CardDropTargetResolver
    {
        private const int DefaultHitBufferCapacity = 32;

        private readonly RaycastHit[] hitBuffer;
        public PhysicalTabletopSurfaces PhysicalSurfaces { get; set; }

        public CardDropTargetResolver(
            UnityCamera targetCamera,
            TabletopPointerProjector pointerProjector,
            LayerMask containerLayerMask,
            float maximumDistance,
            QueryTriggerInteraction queryTriggerInteraction)
        {
            if (targetCamera == null)
            {
                throw new ArgumentNullException(nameof(targetCamera));
            }

            if (!targetCamera.orthographic)
            {
                throw new ArgumentException("CardDropTargetResolver requires an orthographic Camera.", nameof(targetCamera));
            }

            if (pointerProjector == null)
            {
                throw new ArgumentNullException(nameof(pointerProjector));
            }

            if (!IsFinite(maximumDistance) || maximumDistance <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDistance));
            }

            TargetCamera = targetCamera;
            PointerProjector = pointerProjector;
            ContainerLayerMask = containerLayerMask;
            MaximumDistance = maximumDistance;
            QueryTriggerInteraction = queryTriggerInteraction;
            hitBuffer = new RaycastHit[DefaultHitBufferCapacity];
        }

        public UnityCamera TargetCamera { get; }

        public TabletopPointerProjector PointerProjector { get; }

        public LayerMask ContainerLayerMask { get; }

        public float MaximumDistance { get; }

        public QueryTriggerInteraction QueryTriggerInteraction { get; }

        public int HitBufferCapacity => hitBuffer.Length;

        public bool TryResolve(Vector2 screenPosition, out CardDropTarget target)
        {
            return TryResolve(screenPosition, ContainerId.Empty, out target);
        }

        public bool TryResolve(
            Vector2 screenPosition,
            ContainerId excludedContainerId,
            out CardDropTarget target)
        {
            ValidateFinite(screenPosition, nameof(screenPosition));

            Physics.SyncTransforms();
            Ray ray = TargetCamera.ScreenPointToRay(screenPosition);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                hitBuffer,
                MaximumDistance,
                ContainerLayerMask,
                QueryTriggerInteraction);

            if (hitCount > 1)
            {
                Array.Sort(hitBuffer, 0, hitCount, RaycastHitDistanceComparer.Instance);
            }

            for (int i = 0; i < hitCount; i++)
            {
                if (!TryGetValidContainerTarget(
                    hitBuffer[i].collider,
                    excludedContainerId,
                    out ContainerId containerId))
                {
                    continue;
                }

                target = CardDropTarget.ForContainer(containerId);
                return true;
            }

            if (PhysicalSurfaces != null)
            {
                if (PhysicalSurfaces.TryPointer(screenPosition, out RaycastHit surface))
                {
                    target = CardDropTarget.ForTabletop(new TabletopPose(PhysicalSurfaces.Coordinate(surface.point), 0f, 0, 0));
                    return true;
                }
                target = CardDropTarget.None();
                return false;
            }
            if (PointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate coordinate))
            {
                target = CardDropTarget.ForTabletop(new TabletopPose(coordinate, 0f, 0, 0));
                return true;
            }

            target = CardDropTarget.None();
            return false;
        }

        private static bool TryGetValidContainerTarget(
            Collider hitCollider,
            ContainerId excludedContainerId,
            out ContainerId containerId)
        {
            containerId = ContainerId.Empty;

            if (hitCollider == null || !hitCollider.enabled)
            {
                return false;
            }

            TabletopContainerDropTarget dropTarget = hitCollider.GetComponentInParent<TabletopContainerDropTarget>();
            if (dropTarget == null
                || !dropTarget.IsConfigured
                || !dropTarget.isActiveAndEnabled
                || dropTarget.TargetCollider == null
                || !ReferenceEquals(dropTarget.TargetCollider, hitCollider)
                || !dropTarget.TargetCollider.enabled)
            {
                return false;
            }

            IContainerView containerView = dropTarget.ContainerView;
            Component viewComponent = containerView as Component;
            if (containerView == null
                || viewComponent == null
                || !containerView.IsBound
                || containerView.ContainerId.IsEmpty
                || containerView.ContainerId != dropTarget.ContainerId
                || containerView.ContainerState == null
                || containerView.ContainerState.Id != containerView.ContainerId
                || !IsComponentActiveAndEnabled(viewComponent))
            {
                return false;
            }

            if (!excludedContainerId.IsEmpty && containerView.ContainerId == excludedContainerId)
            {
                return false;
            }

            containerId = containerView.ContainerId;
            return true;
        }

        private static bool IsComponentActiveAndEnabled(Component component)
        {
            Behaviour behaviour = component as Behaviour;
            if (behaviour != null)
            {
                return behaviour.isActiveAndEnabled;
            }

            return component.gameObject.activeInHierarchy;
        }

        private static void ValidateFinite(Vector2 value, string parameterName)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit left, RaycastHit right)
            {
                int distanceComparison = left.distance.CompareTo(right.distance);
                if (distanceComparison != 0)
                {
                    return distanceComparison;
                }

                int leftId = left.collider == null ? 0 : RuntimeHelpers.GetHashCode(left.collider);
                int rightId = right.collider == null ? 0 : RuntimeHelpers.GetHashCode(right.collider);
                return leftId.CompareTo(rightId);
            }
        }
    }
}
