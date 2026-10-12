using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Views.Containers
{
    public sealed class ConsoleSlotView : MonoBehaviour, IContainerLayoutView
    {
        [SerializeField] private Transform layoutAnchor;
        [SerializeField] private float verticalOffset = 0.01f;
        [SerializeField] private float placementPreviewLift = 0.08f;
        [SerializeField] private float extractionLift = 0.06f;

        private readonly List<CardView> suppliedCardViews = new List<CardView>();
        private readonly List<CardView> layoutAppliedCards = new List<CardView>();
        private ContainerState containerState;
        private TabletopCoordinateConverter converter;
        private bool isBound;
        private Transform orientationReference;
        private float orientationOffsetDegrees;

        public bool IsBound => isBound;

        /// <summary>
        /// The table rotation a card takes in this slot: the Console's yaw plus the slot's layout default
        /// orientation (Landscape = +90). Null when no orientation is configured; cards then keep their own.
        /// </summary>
        public float? CardRotationDegrees => orientationReference != null
            ? Mathf.Repeat(orientationReference.eulerAngles.y + orientationOffsetDegrees, 360f)
            : (float?)null;

        /// <summary>Sets the slot's card orientation from its Console root and the layout default (Presentation-only).</summary>
        public void ConfigureCardOrientation(Transform consoleRoot, float offsetDegrees)
        {
            if (float.IsNaN(offsetDegrees) || float.IsInfinity(offsetDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(offsetDegrees), "Slot orientation offset must be finite.");
            }

            orientationReference = consoleRoot;
            orientationOffsetDegrees = offsetDegrees;
        }

        public ContainerId ContainerId => isBound ? containerState.Id : ContainerId.Empty;

        public ContainerState ContainerState => isBound ? containerState : null;

        public Transform LayoutAnchor => layoutAnchor;

        public int VisibleCardCount { get; private set; }

        public float ExtractionLift => extractionLift;

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
            ContainerState slotContainer,
            Transform anchor,
            TabletopCoordinateConverter coordinateConverter,
            IReadOnlyList<CardView> cardViews)
        {
            ContainerViewBinding.ValidateContainer(slotContainer, ContainerKind.ConsoleSlot);
            ContainerViewBinding.ValidateAnchor(anchor);
            ContainerViewBinding.ValidateConverter(coordinateConverter);
            ContainerViewBinding.ValidateFiniteNonNegative(verticalOffset, nameof(verticalOffset));
            ContainerViewBinding.ValidateFiniteNonNegative(placementPreviewLift, nameof(placementPreviewLift));
            ContainerViewBinding.ValidateFiniteNonNegative(extractionLift, nameof(extractionLift));
            Dictionary<TabletopObjectId, CardView> lookup = ContainerViewBinding.BuildLookup(cardViews);
            List<CardView> resolvedCards = ContainerViewBinding.ResolveOrderedCards(slotContainer, lookup);
            List<CardLayoutPlan> plan = BuildLayoutPlan(anchor, coordinateConverter, resolvedCards);

            ContainerViewBinding.ClearAppliedCards(layoutAppliedCards);
            containerState = slotContainer;
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

        public TabletopPose CreatePlacementPreviewPose(CardView cardView)
        {
            EnsureBound();
            if (cardView == null || !cardView.IsBound || cardView.BoundState == null)
            {
                throw new ArgumentException("Console placement preview requires a bound Card View.", nameof(cardView));
            }

            TabletopPose currentPose = cardView.BoundState.Pose;
            return ContainerViewBinding.CreatePose(
                converter.ToTableCoordinate(layoutAnchor.position),
                CardRotationDegrees ?? cardView.transform.eulerAngles.y,
                currentPose);
        }

        public Vector3 PlacementPreviewWorldPosition()
        {
            EnsureBound();
            return layoutAnchor.position
                + (Vector3.up * ((containerState.Count * PhysicalStackSeparation()) + placementPreviewLift));
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
            float physicalStep = PhysicalStackSeparation();
            TableCoordinate anchorCoordinate = coordinateConverter.ToTableCoordinate(anchor.position);
            float anchorWorldUpOffset = anchor.position.y - coordinateConverter.ToWorldPosition(anchorCoordinate).y;
            float? slotRotation = CardRotationDegrees;
            for (int i = 0; i < orderedCards.Count; i++)
            {
                TabletopPose currentPose = orderedCards[i].BoundState.Pose;
                plan.Add(new CardLayoutPlan(
                    orderedCards[i],
                    ContainerViewBinding.CreatePose(
                        anchorCoordinate,
                        slotRotation ?? currentPose.RotationDegrees,
                        currentPose),
                    anchorWorldUpOffset + (i * physicalStep)));
            }

            return plan;
        }

        private void ApplyPlan(IReadOnlyList<CardLayoutPlan> plan)
        {
            if (layoutAnchor != transform && !layoutAnchor.IsChildOf(transform))
            {
                transform.SetPositionAndRotation(layoutAnchor.position, layoutAnchor.rotation);
            }

            ContainerViewBinding.ApplyPlan(plan, layoutAppliedCards, containerState.Id);
            VisibleCardCount = plan.Count;
        }

        private float PhysicalStackSeparation()
        {
            return Mathf.Max(verticalOffset, ContainerViewBinding.MinimumPhysicalCardSeparation);
        }

        private void EnsureBound()
        {
            if (!isBound)
            {
                throw new InvalidOperationException("ConsoleSlotView is not bound.");
            }
        }
    }
}
