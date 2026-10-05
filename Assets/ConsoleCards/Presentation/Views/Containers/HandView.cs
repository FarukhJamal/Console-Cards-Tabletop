using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Interaction;
using UnityEngine;

namespace ConsoleCards.Presentation.Views.Containers
{
    public sealed class HandView : MonoBehaviour, IContainerLayoutView
    {
        [SerializeField] private Transform layoutAnchor;
        [SerializeField] private float horizontalSpacing = 0.75f;
        [SerializeField] private float layoutWidth = 5.8f;
        [SerializeField] private float cardLayoutWidth = 1.0f;
        [SerializeField] private float verticalOffset = 0.005f;
        [SerializeField] private float hoverLiftDistance = 0.14f;
        [SerializeField] private float selectedLiftDistance = 0.42f;
        [SerializeField] private float hoverWorldHeight = 0.025f;
        [SerializeField] private float selectedWorldHeight = 0.065f;
        [SerializeField] private float hoverScale = 1.025f;
        [SerializeField] private float selectedScale = 1.06f;
        [SerializeField] private float interactionResponse = 18f;

        private readonly List<CardView> suppliedCardViews = new List<CardView>();
        private readonly List<CardView> layoutAppliedCards = new List<CardView>();
        private readonly Dictionary<TabletopObjectId, HandInteractionPose> interactionPoses =
            new Dictionary<TabletopObjectId, HandInteractionPose>();
        private readonly HashSet<TabletopObjectId> animatingCardIds =
            new HashSet<TabletopObjectId>();
        private readonly List<TabletopObjectId> interactionCleanup =
            new List<TabletopObjectId>();
        private readonly List<CardView> pickOrder = new List<CardView>();
        private readonly List<float> pickHalfDepths = new List<float>();
        private TabletopPresentationTransitionController presentationTransitions;
        private Renderer restSurfaceRenderer;
        private float layoutPitch;
        private ContainerState containerState;
        private TabletopCoordinateConverter converter;
        private TabletopObjectId hoveredCardId;
        private TabletopObjectId selectedCardId;
        private TabletopObjectId draggedCardId;
        private readonly HashSet<TabletopObjectId> assistedSelectedCardIds =
            new HashSet<TabletopObjectId>();
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
            ContainerViewBinding.ValidateFiniteNonNegative(hoverLiftDistance, nameof(hoverLiftDistance));
            ContainerViewBinding.ValidateFiniteNonNegative(selectedLiftDistance, nameof(selectedLiftDistance));
            ContainerViewBinding.ValidateFiniteNonNegative(hoverWorldHeight, nameof(hoverWorldHeight));
            ContainerViewBinding.ValidateFiniteNonNegative(selectedWorldHeight, nameof(selectedWorldHeight));
            ContainerViewBinding.ValidateFiniteNonNegative(interactionResponse, nameof(interactionResponse));
            ContainerViewBinding.ValidateFiniteNonNegative(layoutWidth, nameof(layoutWidth));
            ContainerViewBinding.ValidateFiniteNonNegative(cardLayoutWidth, nameof(cardLayoutWidth));
            if (cardLayoutWidth <= 0f || layoutWidth < cardLayoutWidth)
            {
                throw new ArgumentOutOfRangeException(nameof(layoutWidth));
            }
            if (hoverScale < 1f || float.IsNaN(hoverScale) || float.IsInfinity(hoverScale))
            {
                throw new ArgumentOutOfRangeException(nameof(hoverScale));
            }
            if (selectedScale < 1f || float.IsNaN(selectedScale) || float.IsInfinity(selectedScale))
            {
                throw new ArgumentOutOfRangeException(nameof(selectedScale));
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

        /// <summary>
        /// Optional presentation collaborators: the transition controller, so interaction lifts never
        /// fight a settle animation, and the plate the cards rest on. Without a plate the cards keep
        /// the default surface clearance.
        /// </summary>
        internal void ConfigurePresentation(
            TabletopPresentationTransitionController transitions,
            Renderer restSurface)
        {
            presentationTransitions = transitions;
            restSurfaceRenderer = restSurface;
        }

        /// <summary>
        /// Applies only reusable Hand presentation. Authoritative selection, dragging, and
        /// transfer ownership remain outside this View.
        /// </summary>
        public void SetInteractionState(
            TabletopObjectId hoveredId,
            TabletopObjectId selectedId,
            TabletopObjectId activelyDraggedId,
            IReadOnlyList<TabletopObjectId> assistedSelectedIds = null)
        {
            if (!isBound) return;
            MarkForReturn(hoveredCardId, hoveredId, selectedId);
            MarkForReturn(selectedCardId, hoveredId, selectedId);
            foreach (TabletopObjectId previousId in assistedSelectedCardIds)
            {
                if (!Contains(assistedSelectedIds, previousId)) animatingCardIds.Add(previousId);
            }
            assistedSelectedCardIds.Clear();
            if (assistedSelectedIds != null)
            {
                for (int i = 0; i < assistedSelectedIds.Count; i++)
                {
                    TabletopObjectId assistedId = assistedSelectedIds[i];
                    if (!assistedId.IsEmpty) assistedSelectedCardIds.Add(assistedId);
                }
            }
            hoveredCardId = hoveredId;
            selectedCardId = selectedId;
            draggedCardId = activelyDraggedId;
            if (!hoveredCardId.IsEmpty) animatingCardIds.Add(hoveredCardId);
            if (!selectedCardId.IsEmpty) animatingCardIds.Add(selectedCardId);
            foreach (TabletopObjectId assistedId in assistedSelectedCardIds)
                animatingCardIds.Add(assistedId);
        }

        internal bool TryGetReorderTargetIndex(
            CardView movingCard,
            TableCoordinate pointerCoordinate,
            out int targetIndex)
        {
            EnsureBound();
            targetIndex = -1;
            if (movingCard == null
                || !movingCard.IsBound
                || movingCard.CardState == null
                || movingCard.CardState.BaseState.ContainerId != containerState.Id)
            {
                return false;
            }

            int currentIndex = containerState.IndexOf(movingCard.ObjectId);
            if (currentIndex < 0)
            {
                return false;
            }

            float pitch = CalculatePitch(containerState.Count);
            if (containerState.Count <= 1 || pitch <= 0f)
            {
                targetIndex = currentIndex;
                return true;
            }

            Vector3 pointerWorldPosition = converter.ToWorldPosition(pointerCoordinate);
            float localPointerX = layoutAnchor.InverseTransformPoint(pointerWorldPosition).x;
            float center = (containerState.Count - 1) * 0.5f;
            targetIndex = Mathf.Clamp(
                Mathf.RoundToInt((localPointerX / pitch) + center),
                0,
                containerState.Count - 1);
            return true;
        }

        /// <summary>
        /// Picks the topmost card whose resting (unlifted) strip in the row contains the point where the
        /// ray meets the hand plane. Hover and selection lifts are ignored, so overlapping cards are
        /// picked by what lies under the pointer in the row.
        /// </summary>
        internal bool TryPickCard(Ray ray, out CardView pickedCard)
        {
            pickedCard = null;
            if (!isBound || pickOrder.Count == 0)
            {
                return false;
            }

            Plane plane = new Plane(layoutAnchor.up, layoutAnchor.position);
            if (!plane.Raycast(ray, out float distance))
            {
                return false;
            }

            Vector3 localPoint = layoutAnchor.InverseTransformPoint(ray.GetPoint(distance));
            float halfWidth = cardLayoutWidth * 0.5f;
            float center = (pickOrder.Count - 1) * 0.5f;
            for (int i = pickOrder.Count - 1; i >= 0; i--)
            {
                CardView card = pickOrder[i];
                if (card == null || !card.IsBound)
                {
                    continue;
                }

                if (Mathf.Abs(localPoint.x - ((i - center) * layoutPitch)) <= halfWidth
                    && Mathf.Abs(localPoint.z) <= pickHalfDepths[i])
                {
                    pickedCard = card;
                    return true;
                }
            }

            return false;
        }

        internal void ApplyReorderPreview(CardView movingCard, int targetIndex)
        {
            EnsureBound();
            Dictionary<TabletopObjectId, CardView> lookup = ContainerViewBinding.BuildLookup(suppliedCardViews);
            List<CardView> previewOrder = ContainerViewBinding.ResolveOrderedCards(containerState, lookup);
            int currentIndex = containerState.IndexOf(movingCard.ObjectId);
            if (currentIndex < 0)
            {
                throw new InvalidOperationException("Hand reorder preview Card is not a Hand member.");
            }

            targetIndex = Mathf.Clamp(targetIndex, 0, previewOrder.Count - 1);
            previewOrder.RemoveAt(currentIndex);
            previewOrder.Insert(targetIndex, movingCard);
            ApplyPlanExceptMovingCard(
                BuildLayoutPlan(layoutAnchor, converter, previewOrder),
                movingCard);
        }

        internal void ClearReorderPreview(CardView movingCard)
        {
            EnsureBound();
            Dictionary<TabletopObjectId, CardView> lookup = ContainerViewBinding.BuildLookup(suppliedCardViews);
            List<CardView> authoritativeOrder = ContainerViewBinding.ResolveOrderedCards(containerState, lookup);
            ApplyPlanExceptMovingCard(
                BuildLayoutPlan(layoutAnchor, converter, authoritativeOrder),
                movingCard);
        }

        public void Unbind()
        {
            ResetInteractionPresentation();
            ContainerViewBinding.ClearAppliedCards(layoutAppliedCards);
            containerState = null;
            converter = null;
            suppliedCardViews.Clear();
            VisibleCardCount = 0;
            pickOrder.Clear();
            pickHalfDepths.Clear();
            isBound = false;
        }

        private void LateUpdate()
        {
            if (!isBound || interactionPoses.Count == 0) return;
            float blend = 1f - Mathf.Exp(-interactionResponse * Time.unscaledDeltaTime);
            interactionCleanup.Clear();
            foreach (KeyValuePair<TabletopObjectId, HandInteractionPose> pair in interactionPoses)
            {
                TabletopObjectId cardId = pair.Key;
                HandInteractionPose pose = pair.Value;
                CardView card = pose.CardView;
                if (card == null
                    || !card.IsBound
                    || card.CardState == null
                    || card.CardState.BaseState.ContainerId != containerState.Id)
                {
                    interactionCleanup.Add(cardId);
                    continue;
                }

                if (cardId == draggedCardId || card.IsPreviewing)
                {
                    animatingCardIds.Remove(cardId);
                    continue;
                }

                if (presentationTransitions != null && presentationTransitions.IsAnimating(card.transform))
                {
                    continue;
                }

                bool selected = cardId == selectedCardId || assistedSelectedCardIds.Contains(cardId);
                bool hovered = !selected && cardId == hoveredCardId;
                if (!selected && !hovered && !animatingCardIds.Contains(cardId)) continue;

                float lift = selected ? selectedLiftDistance : hovered ? hoverLiftDistance : 0f;
                float height = selected ? selectedWorldHeight : hovered ? hoverWorldHeight : 0f;
                float scale = selected ? selectedScale : hovered ? hoverScale : 1f;
                Vector3 targetPosition = pose.WorldPosition
                    - (layoutAnchor.forward * lift)
                    + (Vector3.up * height);
                card.transform.position = Vector3.Lerp(card.transform.position, targetPosition, blend);
                card.transform.rotation = Quaternion.Lerp(card.transform.rotation, pose.WorldRotation, blend);
                card.transform.localScale = Vector3.Lerp(
                    card.transform.localScale,
                    pose.LocalScale * scale,
                    blend);

                if (!selected
                    && !hovered
                    && Vector3.SqrMagnitude(card.transform.position - pose.WorldPosition) < 0.000001f
                    && Vector3.SqrMagnitude(card.transform.localScale - pose.LocalScale) < 0.000001f)
                {
                    card.transform.SetPositionAndRotation(pose.WorldPosition, pose.WorldRotation);
                    card.transform.localScale = pose.LocalScale;
                    animatingCardIds.Remove(cardId);
                }
            }

            for (int i = 0; i < interactionCleanup.Count; i++)
            {
                interactionPoses.Remove(interactionCleanup[i]);
                animatingCardIds.Remove(interactionCleanup[i]);
            }
        }

        private List<CardLayoutPlan> BuildLayoutPlan(
            Transform anchor,
            TabletopCoordinateConverter coordinateConverter,
            IReadOnlyList<CardView> orderedCards)
        {
            List<CardLayoutPlan> plan = new List<CardLayoutPlan>(orderedCards.Count);
            float center = (orderedCards.Count - 1) * 0.5f;
            float pitch = CalculatePitch(orderedCards.Count);
            float rotation = NormalizeAngle(anchor.eulerAngles.y);
            float restSurfaceTop = restSurfaceRenderer != null
                ? ComponentRestHeight.TopOf(restSurfaceRenderer)
                : float.NaN;
            for (int i = 0; i < orderedCards.Count; i++)
            {
                float centeredIndex = i - center;
                Vector3 worldPosition = anchor.position + (anchor.right * centeredIndex * pitch);
                TabletopPose pose = ContainerViewBinding.PoseFromWorld(coordinateConverter, worldPosition, rotation);
                plan.Add(new CardLayoutPlan(
                    orderedCards[i],
                    pose,
                    ResolveCardHeight(coordinateConverter, pose, orderedCards[i], restSurfaceTop, i)));
            }

            return plan;
        }

        private void ApplyPlan(IReadOnlyList<CardLayoutPlan> plan)
        {
            transform.SetPositionAndRotation(layoutAnchor.position, layoutAnchor.rotation);
            ContainerViewBinding.ApplyPlan(plan, layoutAppliedCards, containerState.Id);
            UpdateInteractionPoses(plan, null);
            RecordPickOrder(plan);
            VisibleCardCount = plan.Count;
        }

        private void ApplyPlanExceptMovingCard(
            IReadOnlyList<CardLayoutPlan> plan,
            CardView movingCard)
        {
            transform.SetPositionAndRotation(layoutAnchor.position, layoutAnchor.rotation);
            for (int i = 0; i < plan.Count; i++)
            {
                CardLayoutPlan item = plan[i];
                if (!ReferenceEquals(item.CardView, movingCard))
                {
                    item.CardView.ApplyContainerLayoutPose(item.Pose, item.AdditionalWorldHeight);
                }
            }
            UpdateInteractionPoses(plan, movingCard);
            RecordPickOrder(plan);
        }

        private void UpdateInteractionPoses(
            IReadOnlyList<CardLayoutPlan> plan,
            CardView excludedCard)
        {
            interactionCleanup.Clear();
            foreach (TabletopObjectId cardId in interactionPoses.Keys)
                interactionCleanup.Add(cardId);

            for (int i = 0; i < plan.Count; i++)
            {
                CardLayoutPlan item = plan[i];
                TabletopObjectId cardId = item.CardView.ObjectId;
                interactionCleanup.Remove(cardId);
                if (ReferenceEquals(item.CardView, excludedCard)) continue;

                Vector3 localScale = interactionPoses.TryGetValue(cardId, out HandInteractionPose existing)
                    ? existing.LocalScale
                    : item.CardView.transform.localScale;
                interactionPoses[cardId] = new HandInteractionPose(
                    item.CardView,
                    item.CardView.transform.position,
                    item.CardView.transform.rotation,
                    localScale);
            }

            for (int i = 0; i < interactionCleanup.Count; i++)
            {
                interactionPoses.Remove(interactionCleanup[i]);
                animatingCardIds.Remove(interactionCleanup[i]);
            }
        }

        private void MarkForReturn(
            TabletopObjectId previousId,
            TabletopObjectId nextHoveredId,
            TabletopObjectId nextSelectedId)
        {
            if (!previousId.IsEmpty
                && previousId != nextHoveredId
                && previousId != nextSelectedId)
            {
                animatingCardIds.Add(previousId);
            }
        }

        private void ResetInteractionPresentation()
        {
            foreach (HandInteractionPose pose in interactionPoses.Values)
            {
                if (pose.CardView == null) continue;
                pose.CardView.transform.SetPositionAndRotation(pose.WorldPosition, pose.WorldRotation);
                pose.CardView.transform.localScale = pose.LocalScale;
            }
            interactionPoses.Clear();
            animatingCardIds.Clear();
            interactionCleanup.Clear();
            hoveredCardId = TabletopObjectId.Empty;
            selectedCardId = TabletopObjectId.Empty;
            draggedCardId = TabletopObjectId.Empty;
            assistedSelectedCardIds.Clear();
        }

        private static bool Contains(
            IReadOnlyList<TabletopObjectId> cardIds,
            TabletopObjectId cardId)
        {
            if (cardIds == null) return false;
            for (int i = 0; i < cardIds.Count; i++)
                if (cardIds[i] == cardId) return true;
            return false;
        }

        private float CalculatePitch(int count)
        {
            if (count <= 1)
            {
                return 0f;
            }

            // Normal spacing until the row would exceed the hand width, then compress evenly.
            float fittedPitch = (layoutWidth - cardLayoutWidth) / (count - 1);
            return Mathf.Max(0f, Mathf.Min(horizontalSpacing, fittedPitch));
        }

        private float ResolveCardHeight(
            TabletopCoordinateConverter coordinateConverter,
            TabletopPose pose,
            CardView card,
            float restSurfaceTop,
            int index)
        {
            if (float.IsNaN(restSurfaceTop))
            {
                return ContainerViewBinding.DefaultCardSurfaceClearance + (index * verticalOffset);
            }

            // Rest the card's own collider bottom on the plate top, then stack by verticalOffset.
            float restingPivotY = restSurfaceTop
                + ComponentRestHeight.PivotToBottom(card.transform)
                + ComponentRestHeight.RestClearance
                + (index * verticalOffset);
            return restingPivotY - coordinateConverter.ToWorldPosition(pose).y;
        }

        private void RecordPickOrder(IReadOnlyList<CardLayoutPlan> plan)
        {
            layoutPitch = CalculatePitch(plan.Count);
            pickOrder.Clear();
            pickHalfDepths.Clear();
            for (int i = 0; i < plan.Count; i++)
            {
                CardView card = plan[i].CardView;
                pickOrder.Add(card);
                pickHalfDepths.Add(ComponentRestHeight.HalfDepth(card.transform, cardLayoutWidth * 0.7f));
            }
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

        private sealed class HandInteractionPose
        {
            public HandInteractionPose(
                CardView cardView,
                Vector3 worldPosition,
                Quaternion worldRotation,
                Vector3 localScale)
            {
                CardView = cardView;
                WorldPosition = worldPosition;
                WorldRotation = worldRotation;
                LocalScale = localScale;
            }

            public CardView CardView { get; }
            public Vector3 WorldPosition { get; }
            public Quaternion WorldRotation { get; }
            public Vector3 LocalScale { get; }
        }
    }
}
