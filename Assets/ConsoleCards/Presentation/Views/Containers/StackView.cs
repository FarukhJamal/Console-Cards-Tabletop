using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Views.Containers
{
    public sealed class StackView : MonoBehaviour, IContainerLayoutView
    {
        [SerializeField] private float verticalOffset = 0.015f;
        [SerializeField] private float tableOffsetPerCard = 0.04f;

        private readonly List<CardView> suppliedCardViews = new List<CardView>();
        private readonly List<CardView> layoutAppliedCards = new List<CardView>();
        private ContainerState containerState;
        private ContainerPlacementState placementState;
        private Transform layoutAnchor;
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
            Bind(container, placement, transform, coordinateConverter, cardViews);
        }

        public void Bind(
            ContainerState container,
            ContainerPlacementState placement,
            Transform authoredLayoutAnchor,
            TabletopCoordinateConverter coordinateConverter,
            IReadOnlyList<CardView> cardViews)
        {
            ContainerViewBinding.ValidateContainer(container, ContainerKind.Stack);
            ContainerViewBinding.ValidatePlacement(container, placement);
            ContainerViewBinding.ValidateConverter(coordinateConverter);
            if (authoredLayoutAnchor == null)
            {
                throw new ArgumentNullException(nameof(authoredLayoutAnchor));
            }

            if (authoredLayoutAnchor != transform && !authoredLayoutAnchor.IsChildOf(transform))
            {
                throw new ArgumentException(
                    "Stack layout anchor must belong to the Stack hierarchy.",
                    nameof(authoredLayoutAnchor));
            }

            ContainerViewBinding.ValidateFiniteNonNegative(verticalOffset, nameof(verticalOffset));
            ContainerViewBinding.ValidateFiniteNonNegative(tableOffsetPerCard, nameof(tableOffsetPerCard));
            Dictionary<TabletopObjectId, CardView> lookup = ContainerViewBinding.BuildLookup(cardViews);
            List<CardView> resolvedCards = ContainerViewBinding.ResolveOrderedCards(container, lookup);
            SetPlacementTransform(placement, coordinateConverter);
            List<CardLayoutPlan> plan = BuildLayoutPlan(
                placement,
                authoredLayoutAnchor,
                coordinateConverter,
                resolvedCards);

            ContainerViewBinding.ClearAppliedCards(layoutAppliedCards);
            containerState = container;
            placementState = placement;
            layoutAnchor = authoredLayoutAnchor;
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
            SetPlacementTransform(placementState, converter);
            List<CardLayoutPlan> plan = BuildLayoutPlan(
                placementState,
                layoutAnchor,
                converter,
                resolvedCards);

            ApplyPlan(plan);
        }

        public void SetCardViews(IReadOnlyList<CardView> cardViews)
        {
            EnsureBound();
            if (cardViews == null)
            {
                throw new ArgumentNullException(nameof(cardViews));
            }

            suppliedCardViews.Clear();
            suppliedCardViews.AddRange(cardViews);
            ApplyAcceptedLayout();
        }

        public void Unbind()
        {
            ContainerViewBinding.ClearAppliedCards(layoutAppliedCards);
            containerState = null;
            placementState = null;
            layoutAnchor = null;
            converter = null;
            suppliedCardViews.Clear();
            VisibleCardCount = 0;
            isBound = false;
        }

        private List<CardLayoutPlan> BuildLayoutPlan(
            ContainerPlacementState placement,
            Transform authoredLayoutAnchor,
            TabletopCoordinateConverter coordinateConverter,
            IReadOnlyList<CardView> orderedCards)
        {
            List<CardLayoutPlan> plan = new List<CardLayoutPlan>(orderedCards.Count);
            Vector3 placementWorldPosition = coordinateConverter.ToWorldPosition(placement.Pose);
            TableCoordinate anchorCoordinate = coordinateConverter.ToTableCoordinate(authoredLayoutAnchor.position);
            float anchorWorldUpOffset = authoredLayoutAnchor.position.y - placementWorldPosition.y;
            float physicalStep = Mathf.Max(
                verticalOffset,
                ContainerViewBinding.MinimumPhysicalCardSeparation);
            for (int i = 0; i < orderedCards.Count; i++)
            {
                TableCoordinate coordinate = new TableCoordinate(
                    anchorCoordinate.X + (i * tableOffsetPerCard),
                    anchorCoordinate.Y + (i * tableOffsetPerCard));
                TabletopPose pose = ContainerViewBinding.CreatePose(
                    coordinate,
                    placement.Pose.RotationDegrees,
                    placement.Pose);
                plan.Add(new CardLayoutPlan(
                    orderedCards[i],
                    pose,
                    anchorWorldUpOffset + (i * physicalStep)));
            }

            return plan;
        }

        private void ApplyPlan(IReadOnlyList<CardLayoutPlan> plan)
        {
            ContainerViewBinding.ApplyPlan(plan, layoutAppliedCards, containerState.Id);
            VisibleCardCount = plan.Count;
        }

        private void SetPlacementTransform(
            ContainerPlacementState placement,
            TabletopCoordinateConverter coordinateConverter)
        {
            transform.SetPositionAndRotation(
                ContainerViewBinding.PlacementWorldPosition(placement, coordinateConverter),
                coordinateConverter.ToWorldRotation(placement.Pose));
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
