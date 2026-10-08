using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Views.Containers
{
    public sealed class DeckView : MonoBehaviour, IContainerLayoutView
    {
        [SerializeField] private float cardThicknessOffset = 0.02f;

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

        public float CardThicknessOffset
        {
            get => cardThicknessOffset;
            set
            {
                ContainerViewBinding.ValidateFiniteNonNegative(value, nameof(value));
                cardThicknessOffset = value;
            }
        }

        /// <summary>
        /// Plate-less pile (every pile but a legacy split stack, doc 21): the root rests at the placement surface and each card rests on
        /// the table (surface + PivotToBottom + RestClearance + i x step), with step = min(card step,
        /// maximumHeight / card count). Call before Bind; when already bound the layout is re-applied.
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
            ContainerViewBinding.ValidateContainer(container, ContainerKind.Deck);
            ContainerViewBinding.ValidatePlacement(container, placement);
            ContainerViewBinding.ValidateConverter(coordinateConverter);
            ContainerViewBinding.ValidateFiniteNonNegative(cardThicknessOffset, nameof(cardThicknessOffset));
            Dictionary<TabletopObjectId, CardView> lookup = ContainerViewBinding.BuildLookup(cardViews);
            List<CardView> resolvedCards = ContainerViewBinding.ResolveOrderedCards(container, lookup);
            restLift = restsOnTable ? 0f : ComponentRestHeight.RestLift(transform);
            List<CardLayoutPlan> plan = BuildLayoutPlan(placement, coordinateConverter, resolvedCards);

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
            List<CardLayoutPlan> plan = BuildLayoutPlan(placementState, converter, resolvedCards);

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
            TabletopCoordinateConverter coordinateConverter,
            IReadOnlyList<CardView> orderedCards)
        {
            List<CardLayoutPlan> plan = new List<CardLayoutPlan>(orderedCards.Count);
            float surfaceOffset = ContainerViewBinding.PlacementWorldPosition(placement, coordinateConverter).y
                - coordinateConverter.ToWorldPosition(placement.Pose).y;
            float physicalStep = Mathf.Max(
                cardThicknessOffset,
                ContainerViewBinding.MinimumPhysicalCardSeparation);
            if (restsOnTable)
            {
                float surfaceHeight = ContainerViewBinding.PlacementWorldPosition(placement, coordinateConverter).y;
                float poseHeight = coordinateConverter.ToWorldPosition(placement.Pose).y;
                float pileStep = PileStep(physicalStep, maximumPileHeight, orderedCards.Count);
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
                plan.Add(new CardLayoutPlan(
                    orderedCards[i],
                    placement.Pose,
                    surfaceOffset + restLift + ContainerViewBinding.DefaultCardSurfaceClearance + (i * physicalStep)));
            }

            return plan;
        }

        private void ApplyPlan(IReadOnlyList<CardLayoutPlan> plan)
        {
            transform.SetPositionAndRotation(
                ContainerViewBinding.PlacementWorldPosition(placementState, converter) + (Vector3.up * restLift),
                converter.ToWorldRotation(placementState.Pose));
            ContainerViewBinding.ApplyPlan(plan, layoutAppliedCards, containerState.Id);
            VisibleCardCount = plan.Count;
        }

        private static float PileStep(float cardStep, float maximumHeight, int cardCount)
        {
            return cardCount > 0 ? Mathf.Min(cardStep, maximumHeight / cardCount) : cardStep;
        }

        private void EnsureBound()
        {
            if (!isBound)
            {
                throw new InvalidOperationException("DeckView is not bound.");
            }
        }
    }
}
