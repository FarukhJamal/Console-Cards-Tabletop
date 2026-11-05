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

        /// <summary>Optional Console symbol magnet (doc 22 §14). Null keeps plain nearest-target resolution.</summary>
        public ICardDropMagnet Magnet { get; set; }

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

        /// <summary>
        /// CONSOLE-1 evidence switch (off by default): when true, drag releases log the full resolution trace (every
        /// hit and why it was used or skipped, and on a table drop the colliders on all layers and the nearest drop
        /// targets). Turn on from a debugger or a test to diagnose a drop that lands somewhere unexpected.
        /// </summary>
        public static bool VerboseReleaseTrace { get; set; }

        /// <summary>CONSOLE-1 evidence: when true, the next resolves record what was hit and why in LastTrace.</summary>
        public bool TraceEnabled { get; set; }

        /// <summary>CONSOLE-1 evidence: the hits of the last traced resolve and the reason each was used or skipped.</summary>
        public string LastTrace { get; private set; } = string.Empty;

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

            System.Text.StringBuilder trace = TraceEnabled ? new System.Text.StringBuilder() : null;
            trace?.Append($"pointer=({screenPosition.x:0},{screenPosition.y:0}) hits={hitCount} excluded={excludedContainerId}");
            ContainerId directContainerId = ContainerId.Empty;
            for (int i = 0; i < hitCount; i++)
            {
                if (!TryGetValidContainerTarget(
                    hitBuffer[i].collider,
                    excludedContainerId,
                    out ContainerId containerId,
                    out string reason))
                {
                    trace?.Append($" [{DescribeCollider(hitBuffer[i])} skip:{reason}]");
                    continue;
                }

                trace?.Append($" [{DescribeCollider(hitBuffer[i])} use:{containerId}]");
                directContainerId = containerId;
                break;
            }

            ICardDropMagnet magnet = Magnet;
            if (magnet != null && magnet.IsActive)
            {
                bool hasPoint = TryGetPointerCoordinate(screenPosition, out TableCoordinate point);
                if (magnet.TryAdjust(directContainerId, hasPoint, point, out ContainerId adjustedContainerId))
                {
                    trace?.Append($" magnet:{directContainerId}->{adjustedContainerId}");
                    directContainerId = adjustedContainerId == excludedContainerId
                        ? ContainerId.Empty
                        : adjustedContainerId;
                }
            }

            if (trace != null && directContainerId.IsEmpty && VerboseReleaseTrace)
            {
                // Everything under the pointer on any layer, to show a drop target the mask or distance misses.
                RaycastHit[] all = Physics.RaycastAll(ray, 1000f, Physics.AllLayers, QueryTriggerInteraction.Collide);
                Array.Sort(all, RaycastHitDistanceComparer.Instance);
                trace.Append($" | all-layers mask={ContainerLayerMask.value} max={MaximumDistance}:");
                for (int i = 0; i < all.Length && i < 8; i++)
                {
                    Collider c = all[i].collider;
                    TabletopContainerDropTarget t = c != null ? c.GetComponentInParent<TabletopContainerDropTarget>() : null;
                    trace.Append($" [{DescribeCollider(all[i])} trig={(c != null && c.isTrigger)} dt={(t != null ? (t.IsConfigured ? "cfg" : "uncfg") + (t.isActiveAndEnabled ? "" : "-off") : "none")}]");
                }

                // The nearest drop targets to where the ray meets the table, whatever their state.
                Vector3 probe = all.Length > 0 ? all[all.Length - 1].point : ray.GetPoint(10f);
                TabletopContainerDropTarget[] targets = UnityEngine.Object.FindObjectsByType<TabletopContainerDropTarget>(FindObjectsInactive.Include);
                Array.Sort(targets, (a, b) => DistanceTo(a, probe).CompareTo(DistanceTo(b, probe)));
                trace.Append($" | nearest targets to {probe}:");
                int listed = 0;
                for (int i = 0; i < targets.Length && listed < 6; i++)
                {
                    TabletopContainerDropTarget t = targets[i];
                    if (t.TargetCollider == null || !t.TargetCollider.enabled) continue;
                    listed++;
                    Vector3 sp = TargetCamera.WorldToScreenPoint(t.TargetCollider.bounds.center);
                    trace.Append($" [ON {t.name}<{(t.transform.parent != null ? t.transform.parent.name : "-")}> screen=({sp.x:0},{sp.y:0}) of {Screen.width}x{Screen.height} size={t.TargetCollider.bounds.size}]");
                }
                trace.Append(" | nearest any:");
                for (int i = 0; i < targets.Length && i < 3; i++)
                {
                    TabletopContainerDropTarget t = targets[i];
                    Collider c = t.TargetCollider;
                    trace.Append($" [{t.name}<{(t.transform.parent != null ? t.transform.parent.name : "-")}> go={t.gameObject.activeInHierarchy} dt={t.enabled}/{t.IsConfigured} col={(c != null ? $"{c.enabled} L{c.gameObject.layer} {c.bounds.center} {c.bounds.size}" : "null")} d={DistanceTo(t, probe):0.000}]");
                }
            }

            if (trace != null)
            {
                trace.Append($" result={(directContainerId.IsEmpty ? "table" : directContainerId.ToString())}");
                LastTrace = trace.ToString();
            }

            if (!directContainerId.IsEmpty)
            {
                target = CardDropTarget.ForContainer(directContainerId);
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

        private bool TryGetPointerCoordinate(Vector2 screenPosition, out TableCoordinate coordinate)
        {
            if (PhysicalSurfaces != null)
            {
                if (PhysicalSurfaces.TryPointer(screenPosition, out RaycastHit surface))
                {
                    coordinate = PhysicalSurfaces.Coordinate(surface.point);
                    return true;
                }

                coordinate = default;
                return false;
            }

            return PointerProjector.TryProjectScreenPoint(screenPosition, out coordinate);
        }

        private static float DistanceTo(TabletopContainerDropTarget target, Vector3 point)
        {
            Collider c = target.TargetCollider;
            Vector3 centre = c != null ? c.bounds.center : target.transform.position;
            if (c != null && !c.enabled) centre = c.transform.TransformPoint(c is BoxCollider box ? box.center : Vector3.zero);
            return Vector3.Distance(centre, point);
        }

        private static string DescribeCollider(RaycastHit hit)
        {
            Collider c = hit.collider;
            return c == null
                ? "null"
                : $"{c.name}<{(c.transform.parent != null ? c.transform.parent.name : "-")}> L{c.gameObject.layer} d{hit.distance:0.000}";
        }

        private static bool TryGetValidContainerTarget(
            Collider hitCollider,
            ContainerId excludedContainerId,
            out ContainerId containerId)
        {
            return TryGetValidContainerTarget(hitCollider, excludedContainerId, out containerId, out _);
        }

        private static bool TryGetValidContainerTarget(
            Collider hitCollider,
            ContainerId excludedContainerId,
            out ContainerId containerId,
            out string reason)
        {
            containerId = ContainerId.Empty;
            reason = string.Empty;

            if (hitCollider == null || !hitCollider.enabled)
            {
                reason = "collider-off";
                return false;
            }

            TabletopContainerDropTarget dropTarget = hitCollider.GetComponentInParent<TabletopContainerDropTarget>();
            if (dropTarget == null)
            {
                reason = "no-drop-target";
                return false;
            }

            if (!dropTarget.IsConfigured
                || !dropTarget.isActiveAndEnabled
                || dropTarget.TargetCollider == null
                || !ReferenceEquals(dropTarget.TargetCollider, hitCollider)
                || !dropTarget.TargetCollider.enabled)
            {
                reason = !dropTarget.IsConfigured ? "unconfigured"
                    : !dropTarget.isActiveAndEnabled ? "target-off"
                    : !ReferenceEquals(dropTarget.TargetCollider, hitCollider) ? "not-target-collider"
                    : "target-collider-off";
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
                reason = "view-unbound";
                return false;
            }

            if (!excludedContainerId.IsEmpty && containerView.ContainerId == excludedContainerId)
            {
                reason = "excluded";
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
