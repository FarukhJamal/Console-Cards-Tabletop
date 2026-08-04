using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Views.Containers
{
    public sealed class StackView : MonoBehaviour
    {
        [SerializeField] private float verticalOffset = 0.015f;
        [SerializeField] private float tableOffsetPerCard = 0.04f;

        private readonly List<CardView> suppliedCardViews = new List<CardView>();
        private readonly List<CardView> layoutAppliedCards = new List<CardView>();
        private ContainerState containerState;
        private ContainerPlacementState placementState;
        private TabletopCoordinateConverter converter;
        private bool isBound;

        public bool IsBound => isBound;

        public ContainerId ContainerId => isBound ? containerState.Id : ContainerId.Empty;

        public ContainerState ContainerState => isBound ? containerState : null;

        public ContainerPlacementState PlacementState => isBound ? placementState : null;

        public int VisibleCardCount { get; private set; }

        public float VerticalOffset
        {
            get => verticalOffset;
            set
            {
                ContainerViewBinding.ValidateFiniteNonNegative(value, nameof(value));
                verticalOffset = value;
            }
        }

        public float TableOffsetPerCard
        {
            get => tableOffsetPerCard;
            set
            {
                ContainerViewBinding.ValidateFiniteNonNegative(value, nameof(value));
                tableOffsetPerCard = value;
            }
        }

        public void Bind(
            ContainerState container,
            ContainerPlacementState placement,
            TabletopCoordinateConverter coordinateConverter,
            IReadOnlyList<CardView> cardViews)
        {
            ContainerViewBinding.ValidateContainer(container, ContainerKind.Stack);
            ContainerViewBinding.ValidatePlacement(container, placement);
            ContainerViewBinding.ValidateConverter(coordinateConverter);
            ContainerViewBinding.ValidateFiniteNonNegative(verticalOffset, nameof(verticalOffset));
            ContainerViewBinding.ValidateFiniteNonNegative(tableOffsetPerCard, nameof(tableOffsetPerCard));
            Dictionary<TabletopObjectId, CardView> lookup = ContainerViewBinding.BuildLookup(cardViews);
            List<CardView> resolvedCards = ContainerViewBinding.ResolveOrderedCards(container, lookup);
            List<CardLayoutPlan> plan = BuildLayoutPlan(placement, resolvedCards);

            ContainerViewBinding.ClearAppliedCards(layoutAppliedCards);
            containerState = container;
            placementState = placement;
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
            List<CardLayoutPlan> plan = BuildLayoutPlan(placementState, resolvedCards);

            ApplyPlan(plan);
        }

        public void Unbind()
        {
            ContainerViewBinding.ClearAppliedCards(layoutAppliedCards);
            containerState = null;
            placementState = null;
            converter = null;
            suppliedCardViews.Clear();
            VisibleCardCount = 0;
            isBound = false;
        }

        private List<CardLayoutPlan> BuildLayoutPlan(
            ContainerPlacementState placement,
            IReadOnlyList<CardView> orderedCards)
        {
            List<CardLayoutPlan> plan = new List<CardLayoutPlan>(orderedCards.Count);
            for (int i = 0; i < orderedCards.Count; i++)
            {
                TableCoordinate coordinate = new TableCoordinate(
                    placement.Pose.Position.X + (i * tableOffsetPerCard),
                    placement.Pose.Position.Y + (i * tableOffsetPerCard));
                TabletopPose pose = ContainerViewBinding.CreatePose(
                    coordinate,
                    placement.Pose.RotationDegrees,
                    placement.Pose);
                plan.Add(new CardLayoutPlan(orderedCards[i], pose, i * verticalOffset));
            }

            return plan;
        }

        private void ApplyPlan(IReadOnlyList<CardLayoutPlan> plan)
        {
            transform.SetPositionAndRotation(
                converter.ToWorldPosition(placementState.Pose),
                converter.ToWorldRotation(placementState.Pose));
            ContainerViewBinding.ApplyPlan(plan, layoutAppliedCards, containerState.Id);
            VisibleCardCount = plan.Count;
        }

        private void EnsureBound()
        {
            if (!isBound)
            {
                throw new InvalidOperationException("StackView is not bound.");
            }
        }
    }
}
