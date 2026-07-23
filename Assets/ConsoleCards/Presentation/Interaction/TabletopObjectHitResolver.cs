using System;
using ConsoleCards.Presentation.Views;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// Resolves screen-pointer hits against tabletop object Colliders.
    /// </summary>
    public sealed class TabletopObjectHitResolver
    {
        public TabletopObjectHitResolver(
            UnityCamera targetCamera,
            LayerMask interactionLayerMask,
            float maximumDistance)
        {
            if (targetCamera == null)
            {
                throw new ArgumentNullException(nameof(targetCamera));
            }

            if (!targetCamera.orthographic)
            {
                throw new ArgumentException("TabletopObjectHitResolver requires an orthographic Camera.", nameof(targetCamera));
            }

            if (!IsFinite(maximumDistance) || maximumDistance <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDistance));
            }

            TargetCamera = targetCamera;
            InteractionLayerMask = interactionLayerMask;
            MaximumDistance = maximumDistance;
        }

        public UnityCamera TargetCamera { get; }

        public LayerMask InteractionLayerMask { get; }

        public float MaximumDistance { get; }

        public bool TryResolve(Vector2 screenPosition, out TabletopObjectView view)
        {
            ValidateFinite(screenPosition);

            Ray ray = TargetCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                MaximumDistance,
                InteractionLayerMask,
                QueryTriggerInteraction.Collide))
            {
                view = null;
                return false;
            }

            TabletopObjectView resolvedView = hit.collider.GetComponentInParent<TabletopObjectView>();
            if (resolvedView == null
                || !resolvedView.IsBound
                || !resolvedView.isActiveAndEnabled)
            {
                view = null;
                return false;
            }

            view = resolvedView;
            return true;
        }

        private static void ValidateFinite(Vector2 value)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
