using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Views.Containers
{
    /// <summary>
    /// Another seat's hand as others see it: a neat face-down pile on that seat's hand zone with a count
    /// label, so the number of cards shows but no faces. Cards rest on the table like a plate-less stack
    /// (surface + PivotToBottom + RestClearance + i × step). Face-down is presentation only; the cards'
    /// accepted faces are restored when they leave the pile or the View is unbound.
    /// </summary>
    public sealed class HiddenHandView : MonoBehaviour, IContainerLayoutView
    {
        private const float LabelLift = 0.02f;

        [SerializeField] private float cardStep = 0.02f;

        private readonly List<CardView> suppliedCardViews = new List<CardView>();
        private readonly List<CardView> layoutAppliedCards = new List<CardView>();
        private ContainerState containerState;
        private ContainerPlacementState placementState;
        private TabletopCoordinateConverter converter;
        private TextMesh countLabel;
        private bool isBound;

        public bool IsBound => isBound;

        public ContainerId ContainerId => isBound ? containerState.Id : ContainerId.Empty;

        public ContainerState ContainerState => isBound ? containerState : null;

        public ContainerPlacementState PlacementState => isBound ? placementState : null;

        public int VisibleCardCount { get; private set; }

        public void Bind(
            ContainerState handContainer,
            ContainerPlacementState placement,
            TabletopCoordinateConverter coordinateConverter,
            IReadOnlyList<CardView> cardViews,
            TextMesh label)
        {
            ContainerViewBinding.ValidateContainer(handContainer, ContainerKind.Hand);
            ContainerViewBinding.ValidatePlacement(handContainer, placement);
            ContainerViewBinding.ValidateConverter(coordinateConverter);
            ContainerViewBinding.ValidateFiniteNonNegative(cardStep, nameof(cardStep));
            if (cardViews == null)
            {
                throw new ArgumentNullException(nameof(cardViews));
            }

            RestoreAppliedFaces(false);
            ContainerViewBinding.ClearAppliedCards(layoutAppliedCards);
            containerState = handContainer;
            placementState = placement;
            converter = coordinateConverter;
            countLabel = label;
            suppliedCardViews.Clear();
            suppliedCardViews.AddRange(cardViews);
            isBound = true;
            ApplyAcceptedLayout();
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

        public void ApplyAcceptedLayout()
        {
            EnsureBound();
            RestoreAppliedFaces(true);
            Dictionary<TabletopObjectId, CardView> lookup = ContainerViewBinding.BuildLookup(suppliedCardViews);
            List<CardView> orderedCards = ContainerViewBinding.ResolveOrderedCards(containerState, lookup);
            Vector3 surface = ContainerViewBinding.PlacementWorldPosition(placementState, converter);
            float poseHeight = converter.ToWorldPosition(placementState.Pose).y;
            float step = Mathf.Max(cardStep, ContainerViewBinding.MinimumPhysicalCardSeparation);
            List<CardLayoutPlan> plan = new List<CardLayoutPlan>(orderedCards.Count);
            float topHeight = surface.y;
            for (int i = 0; i < orderedCards.Count; i++)
            {
                CardView card = orderedCards[i];
                card.ClearOverlayPresentation();
                float restingPivotY = surface.y
                    + ComponentRestHeight.PivotToBottom(card.transform)
                    + ComponentRestHeight.RestClearance
                    + (i * step);
                plan.Add(new CardLayoutPlan(card, placementState.Pose, restingPivotY - poseHeight));
                topHeight = restingPivotY;
            }

            transform.SetPositionAndRotation(surface, converter.ToWorldRotation(placementState.Pose));
            ContainerViewBinding.ApplyPlan(plan, layoutAppliedCards, containerState.Id);
            for (int i = 0; i < plan.Count; i++)
            {
                ShowFaceDown(plan[i].CardView);
            }

            VisibleCardCount = plan.Count;
            if (countLabel != null)
            {
                countLabel.gameObject.SetActive(plan.Count > 0);
                countLabel.text = plan.Count.ToString();
                countLabel.transform.localPosition = new Vector3(0f, (topHeight - surface.y) + LabelLift, 0f);
                countLabel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        public void Unbind()
        {
            RestoreAppliedFaces(false);
            ContainerViewBinding.ClearAppliedCards(layoutAppliedCards);
            if (countLabel != null)
            {
                countLabel.gameObject.SetActive(false);
            }

            containerState = null;
            placementState = null;
            converter = null;
            countLabel = null;
            suppliedCardViews.Clear();
            VisibleCardCount = 0;
            isBound = false;
        }

        private static void ShowFaceDown(CardView card)
        {
            if (card == null || !card.IsFacePresentationConfigured)
            {
                return;
            }

            card.FaceUpVisualRoot.SetActive(false);
            card.FaceDownVisualRoot.SetActive(true);
        }

        // Restores the accepted face of applied cards; when onlyDeparted is true, only of cards that have
        // left this hand (cards still in it are re-laid face-down right after).
        private void RestoreAppliedFaces(bool onlyDeparted)
        {
            for (int i = 0; i < layoutAppliedCards.Count; i++)
            {
                CardView card = layoutAppliedCards[i];
                if (card == null
                    || !card.IsBound
                    || card.CardState == null
                    || !card.IsFacePresentationConfigured)
                {
                    continue;
                }

                if (onlyDeparted
                    && containerState != null
                    && card.CardState.BaseState.ContainerId == containerState.Id)
                {
                    continue;
                }

                card.ApplyAcceptedFacePresentation();
            }
        }

        private void EnsureBound()
        {
            if (!isBound)
            {
                throw new InvalidOperationException("HiddenHandView is not bound.");
            }
        }
    }
}
