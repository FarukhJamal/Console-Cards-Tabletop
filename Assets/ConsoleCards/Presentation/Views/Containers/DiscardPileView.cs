using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Views.Containers
{
    public sealed class DiscardPileView : MonoBehaviour, IContainerLayoutView
    {
        [SerializeField] private float verticalOffset = 0.012f;
        [SerializeField] private float diagonalTableOffsetPerCard = 0.035f;

        private readonly List<CardView> suppliedCardViews = new List<CardView>();
        private readonly List<CardView> layoutAppliedCards = new List<CardView>();
        private ContainerState containerState;
        private ContainerPlacementState placementState;
        private TabletopCoordinateConverter converter;
        private bool isBound;
        private float restLift;
        private bool restsOnTable;
        private float maximumPileHeight;

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

        public float DiagonalTableOffsetPerCard
        {
            get => diagonalTableOffsetPerCard;
            set
            {
                ContainerViewBinding.ValidateFiniteNonNegative(value, nameof(value));
                diagonalTableOffsetPerCard = value;
            }
        }

        /// <summary>
        /// Plate-less pile (doc 21, C2b): the root rests at the placement surface and each card rests on the
        /// table squared in the bay (surface + PivotToBottom + RestClearance + i x step), with step =
        /// min(card step, maximumHeight / card count), the same rule as a Deck. Without it the legacy
        /// diagonal fan is used. Call before Bind; when already bound the layout is re-applied.
        /// </summary>
        internal void ConfigureTableRest(bool enabled, float maximumHeight)
        {
            if (enabled && (float.IsNaN(maximumHeight) || float.IsInfinity(maximumHeight) || maximumHeight <= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHeight));
            }

            restsOnTable = enabled;
            maximumPileHeight = maximumHeight;
            if (isBound)
            {
                restLift = restsOnTable ? 0f : ComponentRestHeight.RestLift(transform);
                ApplyAcceptedLayout();
            }
        }

        public void Bind(
            ContainerState container,
            ContainerPlacementState placement,
            TabletopCoordinateConverter coordinateConverter,
            IReadOnlyList<CardView> cardViews)
        {
            ContainerViewBinding.ValidateContainer(container, ContainerKind.DiscardPile);
            ContainerViewBinding.ValidatePlacement(container, placement);
            ContainerViewBinding.ValidateConverter(coordinateConverter);
            ContainerViewBinding.ValidateFiniteNonNegative(verticalOffset, nameof(verticalOffset));
            ContainerViewBinding.ValidateFiniteNonNegative(diagonalTableOffsetPerCard, nameof(diagonalTableOffsetPerCard));
            Dictionary<TabletopObjectId, CardView> lookup = ContainerViewBinding.BuildLookup(cardViews);
            List<CardView> resolvedCards = ContainerViewBinding.ResolveOrderedCards(container, lookup);
            restLift = restsOnTable ? 0f : ComponentRestHeight.RestLift(transform);
            converter = coordinateConverter;
            List<CardLayoutPlan> plan = BuildLayoutPlan(placement, resolvedCards);

            ContainerViewBinding.ClearAppliedCards(layoutAppliedCards);
            containerState = container;
            placementState = placement;
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
            float physicalStep = Mathf.Max(
                verticalOffset,
                ContainerViewBinding.MinimumPhysicalCardSeparation);
            if (restsOnTable)
            {
                float surfaceHeight = ContainerViewBinding.PlacementWorldPosition(placement, converter).y;
                float poseHeight = converter.ToWorldPosition(placement.Pose).y;
                float pileStep = orderedCards.Count > 0
                    ? Mathf.Min(physicalStep, maximumPileHeight / orderedCards.Count)
                    : physicalStep;
                for (int i = 0; i < orderedCards.Count; i++)
                {
                    CardView card = orderedCards[i];
                    card.ClearOverlayPresentation();
                    float restingPivotY = surfaceHeight
                        + ComponentRestHeight.PivotToBottom(card.transform)
                        + ComponentRestHeight.RestClearance
                        + (i * pileStep);
                    plan.Add(new CardLayoutPlan(card, placement.Pose, restingPivotY - poseHeight));
                }

                return plan;
            }

            for (int i = 0; i < orderedCards.Count; i++)
            {
                TableCoordinate coordinate = new TableCoordinate(
                    placement.Pose.Position.X + (i * diagonalTableOffsetPerCard),
                    placement.Pose.Position.Y - (i * diagonalTableOffsetPerCard));
                TabletopPose pose = ContainerViewBinding.CreatePose(
                    coordinate,
                    placement.Pose.RotationDegrees,
                    placement.Pose);
                plan.Add(new CardLayoutPlan(
                    orderedCards[i],
                    pose,
                    restLift + ContainerViewBinding.DefaultCardSurfaceClearance + (i * physicalStep)));
            }

            return plan;
        }

        private void ApplyPlan(IReadOnlyList<CardLayoutPlan> plan)
        {
            Vector3 rootPosition = restsOnTable
                ? ContainerViewBinding.PlacementWorldPosition(placementState, converter)
                : converter.ToWorldPosition(placementState.Pose);
            transform.SetPositionAndRotation(
                rootPosition + (Vector3.up * restLift),
                converter.ToWorldRotation(placementState.Pose));
            ContainerViewBinding.ApplyPlan(plan, layoutAppliedCards, containerState.Id);
            VisibleCardCount = plan.Count;
        }

        private void EnsureBound()
        {
            if (!isBound)
            {
                throw new InvalidOperationException("DiscardPileView is not bound.");
            }
        }
    }
}
