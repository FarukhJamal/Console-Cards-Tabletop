using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ConsoleCards.Core.Identifiers;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>Resolves only explicitly configured Token Container areas.</summary>
    public sealed class TokenDropTargetResolver
    {
        private const int HitBufferCapacity = 32;
        private readonly RaycastHit[] hitBuffer = new RaycastHit[HitBufferCapacity];

        public TokenDropTargetResolver(
            UnityCamera targetCamera,
            LayerMask layerMask,
            float maximumDistance)
        {
            TargetCamera = targetCamera != null
                ? targetCamera
                : throw new ArgumentNullException(nameof(targetCamera));
            LayerMask = layerMask;
            if (float.IsNaN(maximumDistance) || float.IsInfinity(maximumDistance) || maximumDistance <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDistance));
            }

            MaximumDistance = maximumDistance;
        }

        public UnityCamera TargetCamera { get; }

        public LayerMask LayerMask { get; }

        public float MaximumDistance { get; }

        public bool TryResolve(Vector2 screenPosition, out ContainerId containerId)
        {
            Physics.SyncTransforms();
            int hitCount = Physics.RaycastNonAlloc(
                TargetCamera.ScreenPointToRay(screenPosition),
                hitBuffer,
                MaximumDistance,
                LayerMask,
                QueryTriggerInteraction.Collide);
            if (hitCount > 1)
            {
                Array.Sort(hitBuffer, 0, hitCount, RaycastHitDistanceComparer.Instance);
            }

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = hitBuffer[i].collider;
                TabletopTokenContainerDropTarget target =
                    collider != null ? collider.GetComponentInParent<TabletopTokenContainerDropTarget>() : null;
                if (target == null
                    || !target.isActiveAndEnabled
                    || !target.IsConfigured
                    || !ReferenceEquals(target.TargetCollider, collider)
                    || !collider.enabled
                    || target.ContainerView == null
                    || !target.ContainerView.IsBound)
                {
                    continue;
                }

                containerId = target.ContainerId;
                return true;
            }

            containerId = ContainerId.Empty;
            return false;
        }

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit left, RaycastHit right)
            {
                int distance = left.distance.CompareTo(right.distance);
                if (distance != 0)
                {
                    return distance;
                }

                int leftId = left.collider == null ? 0 : RuntimeHelpers.GetHashCode(left.collider);
                int rightId = right.collider == null ? 0 : RuntimeHelpers.GetHashCode(right.collider);
                return leftId.CompareTo(rightId);
            }
        }
    }
}
