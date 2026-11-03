using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Views.Containers
{
    /// <summary>
    /// Another seat's hand as others see it. H3 (owner's test, 2026-11-02: "show it on the edge of the table on which the
    /// player is sitting"): the cards stand in a row along that seat's edge, leaning back towards their owner, so the
    /// others see their backs and how many there are (with the count above). With <see cref="standingRow"/> off, the
    /// older look: a neat face-down pile on that seat's hand zone with a count label, so the number of cards shows but no faces. Cards rest on the table like a plate-less stack
    /// (surface + PivotToBottom + RestClearance + i × step). Face-down is presentation only; the cards'
    /// accepted faces are restored when they leave the pile or the View is unbound.
    /// </summary>
    public sealed class HiddenHandView : MonoBehaviour, IContainerLayoutView
    {
        private const float LabelLift = 0.02f;

        [SerializeField] private float cardStep = 0.02f;
        [SerializeField] private bool standingRow = true;
        [SerializeField, Range(0f, 85f)] private float standingLean = 62f; // degrees up from lying flat
        [SerializeField, Range(0.2f, 1f)] private float standingOverlap = 0.58f; // of a card's width between two cards
        [SerializeField, Min(1f)] private float standingMaxRowCards = 7f; // the row is never wider than this many cards
        [SerializeField] private float standingLayerGap = 0.004f; // world units between neighbours (no flicker)
        private static readonly List<CardLayoutPlan> NoPlan = new List<CardLayoutPlan>(0);

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
            if (standingRow)
            {
                ApplyStandingRow(orderedCards, surface);
                return;
            }

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

        // H3: the row. Cards are spread along the seat's edge (its pose's right), stood up about their bottom edge so
        // their backs face the middle of the table, each a hair in front of the one before so they never flicker.
        private void ApplyStandingRow(List<CardView> orderedCards, Vector3 surface)
        {
            Quaternion seatRotation = converter.ToWorldRotation(placementState.Pose);
            transform.SetPositionAndRotation(surface, seatRotation);
            ContainerViewBinding.ApplyPlan(NoPlan, layoutAppliedCards, containerState.Id); // cards that left let go

            Vector3 right = seatRotation * Vector3.right;
            Vector3 forward = seatRotation * Vector3.forward;
            Vector3 toMiddle = converter.ToWorldPosition(TabletopPose.Default) - surface;
            toMiddle.y = 0f;
            // Lean so the card's back (its top while lying face down) turns towards the middle of the table.
            float sign = toMiddle.sqrMagnitude < 0.0001f || Vector3.Dot(forward, toMiddle) >= 0f ? 1f : -1f;
            Quaternion lean = Quaternion.AngleAxis(sign * standingLean, right);

            int count = orderedCards.Count;
            float width = count > 0 ? CardWidth(orderedCards[0]) : 0.7f;
            float length = count > 0 ? CardLength(orderedCards[0]) : 1f;
            float spacing = width * standingOverlap;
            if (count > 1) spacing = Mathf.Min(spacing, (width * standingMaxRowCards) / (count - 1));
            float rise = 0.5f * length * Mathf.Sin(standingLean * Mathf.Deg2Rad) + ComponentRestHeight.RestClearance;
            Vector3 normal = lean * Vector3.up;
            float topHeight = surface.y;
            for (int i = 0; i < count; i++)
            {
                CardView card = orderedCards[i];
                card.ClearOverlayPresentation();
                float along = (i - ((count - 1) * 0.5f)) * spacing;
                Vector3 position = surface + (right * along) + (Vector3.up * rise) + (normal * (i * standingLayerGap));
                card.ApplyContainerWorldPose(position, lean * seatRotation);
                layoutAppliedCards.Add(card);
                ShowFaceDown(card);
                topHeight = Mathf.Max(topHeight, position.y + rise);
            }

            VisibleCardCount = count;
            if (countLabel != null)
            {
                countLabel.gameObject.SetActive(count > 0);
                countLabel.text = count.ToString();
                countLabel.transform.localPosition = new Vector3(0f, (topHeight - surface.y) + LabelLift, 0f);
                countLabel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        // A card's size on the table from its collider (read even while the collider is off in a container).
        private static float CardWidth(CardView card) => CardSide(card, false);

        private static float CardLength(CardView card) => CardSide(card, true);

        private static float CardSide(CardView card, bool longSide)
        {
            Collider collider = card != null && card.PhysicalObject != null ? card.PhysicalObject.PhysicalCollider : null;
            if (collider is BoxCollider box)
            {
                Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
                float a = Mathf.Abs(size.x);
                float b = Mathf.Abs(size.z);
                float side = longSide ? Mathf.Max(a, b) : Mathf.Min(a, b);
                if (side > 0.01f) return side;
            }

            return longSide ? 1f : 0.7f;
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
