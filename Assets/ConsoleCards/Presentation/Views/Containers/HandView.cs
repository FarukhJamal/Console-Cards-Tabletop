using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Views.Containers
{
    public sealed class HandView : MonoBehaviour, IContainerView
    {
        [SerializeField] private Transform layoutAnchor;
        [SerializeField] private float horizontalSpacing = 0.75f;
        [SerializeField] private float fanAngleDegrees = 12f;
        [SerializeField] private float verticalOffset = 0.005f;

        private readonly List<CardView> suppliedCardViews = new List<CardView>();
        private readonly List<CardView> layoutAppliedCards = new List<CardView>();
        private ContainerState containerState;
        private TabletopCoordinateConverter converter;
        private bool isBound;

        public bool IsBound => isBound;

        public ContainerId ContainerId => isBound ? containerState.Id : ContainerId.Empty;

        public ContainerState ContainerState => isBound ? containerState : null;

        public Transform LayoutAnchor => layoutAnchor;

        public int VisibleCardCount { get; private set; }

        public float HorizontalSpacing
        {
            get => horizontalSpacing;
            set
            {
                ContainerViewBinding.ValidateFiniteNonNegative(value, nameof(value));
                horizontalSpacing = value;
            }
        }

        public float FanAngleDegrees
        {
            get => fanAngleDegrees;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                fanAngleDegrees = value;
            }
        }

        public float VerticalOffset
        {
            get => verticalOffset;
            set
            {
                ContainerViewBinding.ValidateFiniteNonNegative(value, nameof(value));
                verticalOffset = value;
            }
        }

        public void Bind(
            ContainerState handContainer,
            Transform anchor,
            TabletopCoordinateConverter coordinateConverter,
            IReadOnlyList<CardView> cardViews)
        {
            ContainerViewBinding.ValidateContainer(handContainer, ContainerKind.Hand);
            ContainerViewBinding.ValidateAnchor(anchor);
            ContainerViewBinding.ValidateConverter(coordinateConverter);
            ContainerViewBinding.ValidateFiniteNonNegative(horizontalSpacing, nameof(horizontalSpacing));
            ContainerViewBinding.ValidateFiniteNonNegative(verticalOffset, nameof(verticalOffset));
            if (float.IsNaN(fanAngleDegrees) || float.IsInfinity(fanAngleDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(fanAngleDegrees));
            }

            Dictionary<TabletopObjectId, CardView> lookup = ContainerViewBinding.BuildLookup(cardViews);
            List<CardView> resolvedCards = ContainerViewBinding.ResolveOrderedCards(handContainer, lookup);
            List<CardLayoutPlan> plan = BuildLayoutPlan(anchor, coordinateConverter, resolvedCards);

            ContainerViewBinding.ClearAppliedCards(layoutAppliedCards);
            containerState = handContainer;
            layoutAnchor = anchor;
            converter = coordinateConverter;
            suppliedCardViews.Clear();
            suppliedCardViews.AddRange(cardViews);
            isBound = true;
            ApplyPlan(plan);
        }

        public void ApplyAcceptedLayout()
        {
            EnsureBound();

            Dictionary<TabletopObjectId, CardView> lookup = ContainerViewBinding.BuildLookup(suppliedCardViews);
            List<CardView> resolvedCards = ContainerViewBinding.ResolveOrderedCards(containerState, lookup);
            List<CardLayoutPlan> plan = BuildLayoutPlan(layoutAnchor, converter, resolvedCards);

            ApplyPlan(plan);
        }

        public void Unbind()
        {
            ContainerViewBinding.ClearAppliedCards(layoutAppliedCards);
            containerState = null;
            converter = null;
            suppliedCardViews.Clear();
            VisibleCardCount = 0;
            isBound = false;
        }

        private List<CardLayoutPlan> BuildLayoutPlan(
            Transform anchor,
            TabletopCoordinateConverter coordinateConverter,
            IReadOnlyList<CardView> orderedCards)
        {
            List<CardLayoutPlan> plan = new List<CardLayoutPlan>(orderedCards.Count);
            float center = (orderedCards.Count - 1) * 0.5f;
            for (int i = 0; i < orderedCards.Count; i++)
            {
                float centeredIndex = i - center;
                Vector3 worldPosition = anchor.position + (anchor.right * centeredIndex * horizontalSpacing);
                float rotation = NormalizeAngle(anchor.eulerAngles.y + CalculateFanRotation(centeredIndex, orderedCards.Count));
                plan.Add(new CardLayoutPlan(
                    orderedCards[i],
                    ContainerViewBinding.PoseFromWorld(coordinateConverter, worldPosition, rotation),
                    i * verticalOffset));
            }

            return plan;
        }

        private void ApplyPlan(IReadOnlyList<CardLayoutPlan> plan)
        {
            transform.SetPositionAndRotation(layoutAnchor.position, layoutAnchor.rotation);
            ContainerViewBinding.ApplyPlan(plan, layoutAppliedCards, containerState.Id);
            VisibleCardCount = plan.Count;
        }

        private float CalculateFanRotation(float centeredIndex, int count)
        {
            if (count <= 1)
            {
                return 0f;
            }

            return centeredIndex * fanAngleDegrees;
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private void EnsureBound()
        {
            if (!isBound)
            {
                throw new InvalidOperationException("HandView is not bound.");
            }
        }
    }
}
