using System;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// Resolves screen-pointer hits against tabletop object Colliders.
    /// </summary>
    public sealed class TabletopObjectHitResolver
    {
        // RaycastNonAlloc returns at most this many hits. When the buffer fills,
        // only the returned hits are inspected, sorted deterministically below.
        private const int HitBufferCapacity = 32;

        private readonly RaycastHit[] hitBuffer;

        public TabletopObjectHitResolver(
            UnityCamera targetCamera,
            LayerMask interactionLayerMask,
            float maximumDistance)
        {
            if (targetCamera == null)
            {
                throw new ArgumentNullException(nameof(targetCamera));
            }

            if (!IsFinite(maximumDistance) || maximumDistance <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDistance));
            }

            TargetCamera = targetCamera;
            InteractionLayerMask = interactionLayerMask;
            MaximumDistance = maximumDistance;
            hitBuffer = new RaycastHit[HitBufferCapacity];
        }

        public UnityCamera TargetCamera { get; }

        public LayerMask InteractionLayerMask { get; }

        public float MaximumDistance { get; }

        /// <summary>
        /// Optional local hand. When the nearest hit is one of its cards, the hand picks the card by
        /// its resting strip in the row; the raycast result stays the fallback.
        /// </summary>
        public HandView HandPicker { get; set; }

        public bool TryResolve(Vector2 screenPosition, out TabletopObjectView view)
        {
            ValidateFinite(screenPosition);

            Physics.SyncTransforms();
            Ray ray = TargetCamera.ScreenPointToRay(screenPosition);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                hitBuffer,
                MaximumDistance,
                InteractionLayerMask,
                QueryTriggerInteraction.Collide);
            if (hitCount == 0)
            {
                view = null;
                return false;
            }

            SortHitsNearestFirst(hitCount);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = hitBuffer[i].collider;
                if (hitCollider == null
                    || !hitCollider.enabled
                    || !hitCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                TabletopObjectView resolvedView = hitCollider.GetComponentInParent<TabletopObjectView>();
                if (resolvedView == null
                    || !resolvedView.isActiveAndEnabled
                    || !resolvedView.IsBound
                    || !IsPickableObjectView(resolvedView))
                {
                    continue;
                }

                view = ResolveHandCard(ray, resolvedView);
                return true;
            }

            view = null;
            return false;
        }

        private TabletopObjectView ResolveHandCard(Ray ray, TabletopObjectView resolvedView)
        {
            HandView hand = HandPicker;
            CardView card = resolvedView as CardView;
            if (hand == null
                || !hand.IsBound
                || card == null
                || card.BoundState == null
                || card.BoundState.ContainerId != hand.ContainerId)
            {
                return resolvedView;
            }

            return hand.TryPickCard(ray, out CardView pickedCard) ? pickedCard : resolvedView;
        }

        private void SortHitsNearestFirst(int hitCount)
        {
            for (int i = 1; i < hitCount; i++)
            {
                RaycastHit candidate = hitBuffer[i];
                int insertionIndex = i - 1;
                while (insertionIndex >= 0
                    && CompareHits(candidate, hitBuffer[insertionIndex]) < 0)
                {
                    hitBuffer[insertionIndex + 1] = hitBuffer[insertionIndex];
                    insertionIndex--;
                }

                hitBuffer[insertionIndex + 1] = candidate;
            }
        }

        private static int CompareHits(RaycastHit left, RaycastHit right)
        {
            int distanceComparison = left.distance.CompareTo(right.distance);
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            Collider leftCollider = left.collider;
            Collider rightCollider = right.collider;
            if (leftCollider == null)
            {
                return rightCollider == null ? 0 : 1;
            }

            if (rightCollider == null)
            {
                return -1;
            }

            return leftCollider.GetEntityId().CompareTo(rightCollider.GetEntityId());
        }

        private static bool IsPickableObjectView(TabletopObjectView view)
        {
            return view is CardView
                || view is PawnView
                || view is TokenView
                || view is DieView;
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
