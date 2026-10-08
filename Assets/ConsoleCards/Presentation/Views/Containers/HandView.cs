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
    // Runs after the camera controller so the camera tray follows this frame's camera pose.
    [DefaultExecutionOrder(100)]
    public sealed class HandView : MonoBehaviour, IContainerLayoutView
    {
        [SerializeField] private Transform layoutAnchor;
        [SerializeField] private float horizontalSpacing = 0.75f;
        [SerializeField] private float layoutWidth = 5.8f;
        [SerializeField] private float cardLayoutWidth = 1.0f;
        [SerializeField] private float verticalOffset = 0.005f;
        // Hover/selected cards rise and move away from the owner along the hand's forward.
        [SerializeField] private float hoverRaise = 0.08f;
        [SerializeField] private float hoverAwayOffset = 0.16f;
        [SerializeField] private float selectedRaise = 0.16f;
        [SerializeField] private float selectedAwayOffset = 0.32f;
        [SerializeField] private float hoverScale = 1.025f;
        [SerializeField] private float selectedScale = 1.06f;
        [SerializeField] private float interactionResponse = 18f;
        // Camera tray (unscaled tray units). Hover/selected cards rise up the screen (forward), lift
        // toward the camera (up) and grow; neighbours of the hovered card spread apart. Every tray card
        // eases toward its target in tray space, so nothing in the tray ever jumps.
        [SerializeField] private float trayHoverRise = 0.35f;
        [SerializeField] private float traySelectedRise = 0.5f;
        [SerializeField] private float trayHoverLift = 0.15f;
        [SerializeField] private float traySelectedLift = 0.12f;
        [SerializeField] private float trayHoverScale = 1.15f;
        [SerializeField] private float traySelectedScale = 1.2f;
        [SerializeField] private float trayHoverSpread = 0.35f;
        [SerializeField] private float trayCardStep = 0.004f;
        [SerializeField] private float trayResponse = 14f;
        [SerializeField] private float trayDragResponse = 30f;
        [SerializeField] private float trayHandoffDuration = 0.28f;

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
        private HandTrayRig trayRig;
        private CardView trayDraggedCard;
        private Vector2 trayDragScreenPosition;
        private Vector2 trayDragOffset;
        private CardView trayExcludedCard;
        // Current pose of each tray card in tray space; Scale is relative to the tray scale.
        private readonly Dictionary<TabletopObjectId, TrayMotion> trayMotions =
            new Dictionary<TabletopObjectId, TrayMotion>();
        // True while a bind or tray switch lays out: cards are placed at rest instead of easing in.
        private bool trayPlanSnaps;
        private bool trayHandoffActive;
        private float trayHandoffElapsed;
        private TrayMotion trayHandoffStart;
        private Vector3 trayHandoffWorldPosition;
        private Quaternion trayHandoffWorldRotation;

        public bool IsBound => isBound;

        public ContainerId ContainerId => isBound ? containerState.Id : ContainerId.Empty;

        public ContainerState ContainerState => isBound ? containerState : null;

        public Transform LayoutAnchor => layoutAnchor;

        public int VisibleCardCount { get; private set; }

        internal bool IsTrayMode => trayRig != null;

        internal HandTrayRig TrayRig => trayRig;

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
            ContainerViewBinding.ValidateFiniteNonNegative(hoverRaise, nameof(hoverRaise));
            ContainerViewBinding.ValidateFiniteNonNegative(hoverAwayOffset, nameof(hoverAwayOffset));
            ContainerViewBinding.ValidateFiniteNonNegative(selectedRaise, nameof(selectedRaise));
            ContainerViewBinding.ValidateFiniteNonNegative(selectedAwayOffset, nameof(selectedAwayOffset));
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
            trayPlanSnaps = true;
            try
            {
                ApplyPlan(plan);
            }
            finally
            {
                trayPlanSnaps = false;
            }
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
        /// Optional camera tray. While set, cards are laid out on the tray anchor in camera space instead
        /// of on the table; null returns the hand to its table layout.
        /// </summary>
        internal void ConfigureTray(HandTrayRig rig)
        {
            if (ReferenceEquals(trayRig, rig))
            {
                return;
            }

            RestoreTrayPresentation();
            trayRig = rig;
            if (isBound)
            {
                trayPlanSnaps = true;
                try
                {
                    ApplyAcceptedLayout();
                }
                finally
                {
                    trayPlanSnaps = false;
                }
            }
        }

        internal bool IsScreenPointInTray(Vector2 screenPosition)
        {
            return isBound && trayRig != null && trayRig.ContainsScreenPoint(screenPosition);
        }

        /// <summary>
        /// A card dragged inside the tray follows the pointer on the tray plane. A card coming back from
        /// the table shrinks into the tray from where it is, centred on the pointer.
        /// </summary>
        internal void SetTrayDragPointer(CardView card, Vector2 screenPosition, bool keepGrabOffset)
        {
            if (!isBound || trayRig == null || card == null)
            {
                return;
            }

            if (!ReferenceEquals(trayDraggedCard, card))
            {
                trayDragOffset = Vector2.zero;
                if (!keepGrabOffset)
                {
                    // Re-entering from the table: start from where the card is now, centred on the pointer.
                    trayMotions.Remove(card.ObjectId);
                }

                if (keepGrabOffset
                    && trayMotions.TryGetValue(card.ObjectId, out TrayMotion motion)
                    && TryGetTrayLocalPoint(screenPosition, out Vector3 pointerLocal))
                {
                    trayDragOffset = new Vector2(
                        motion.Position.x - pointerLocal.x,
                        motion.Position.z - pointerLocal.z);
                }
            }

            trayHandoffActive = false;
            trayDraggedCard = card;
            trayDragScreenPosition = screenPosition;
        }

        /// <summary>
        /// The pointer left the tray: the card flies and grows toward its table pose (which follows the
        /// pointer) over trayHandoffDuration. The caller then hands it to physics already in place.
        /// </summary>
        internal void SetTrayHandoffTarget(CardView card, Vector3 worldPosition, Quaternion worldRotation)
        {
            if (!isBound || trayRig == null || card == null)
            {
                return;
            }

            if (!trayHandoffActive || !ReferenceEquals(trayDraggedCard, card))
            {
                trayDraggedCard = card;
                trayHandoffActive = true;
                trayHandoffElapsed = 0f;
                trayHandoffStart = trayMotions.TryGetValue(card.ObjectId, out TrayMotion motion)
                    ? motion
                    : CaptureTrayMotion(card);
            }

            trayHandoffWorldPosition = worldPosition;
            trayHandoffWorldRotation = worldRotation;
        }

        internal bool IsTrayHandoffComplete(CardView card)
        {
            return trayHandoffActive
                && ReferenceEquals(trayDraggedCard, card)
                && trayHandoffElapsed >= trayHandoffDuration;
        }

        internal void EndTrayDrag()
        {
            trayDraggedCard = null;
            trayHandoffActive = false;
        }

        internal bool TryGetTrayReorderTargetIndex(
            CardView movingCard,
            Vector2 screenPosition,
            out int targetIndex)
        {
            targetIndex = -1;
            if (!isBound
                || trayRig == null
                || movingCard == null
                || !movingCard.IsBound
                || movingCard.CardState == null
                || movingCard.CardState.BaseState.ContainerId != containerState.Id)
            {
                return false;
            }

            int currentIndex = containerState.IndexOf(movingCard.ObjectId);
            if (currentIndex < 0 || !TryGetTrayLocalPoint(screenPosition, out Vector3 local))
            {
                return false;
            }

            float pitch = CalculatePitch(containerState.Count);
            if (containerState.Count <= 1 || pitch <= 0f)
            {
                targetIndex = currentIndex;
                return true;
            }

            float center = (containerState.Count - 1) * 0.5f;
            targetIndex = Mathf.Clamp(
                Mathf.RoundToInt((local.x / pitch) + center),
                0,
                containerState.Count - 1);
            return true;
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

            Transform pickAnchor = trayRig != null ? trayRig.Anchor : layoutAnchor;
            Plane plane = new Plane(pickAnchor.up, pickAnchor.position);
            if (!plane.Raycast(ray, out float distance))
            {
                return false;
            }

            Vector3 localPoint = pickAnchor.InverseTransformPoint(ray.GetPoint(distance));
            float halfWidth = cardLayoutWidth * 0.5f;
            float center = (pickOrder.Count - 1) * 0.5f;
            for (int i = pickOrder.Count - 1; i >= 0; i--)
            {
                CardView card = pickOrder[i];
                if (card == null || !card.IsBound || IsArrivingInTray(card))
                {
                    continue;
                }

                // In the tray a lifted card also owns the strip it has risen into.
                float lowerDepth = pickHalfDepths[i];
                float upperDepth = lowerDepth + (trayRig != null ? TrayRise(card) : 0f);
                if (Mathf.Abs(localPoint.x - ((i - center) * layoutPitch)) <= halfWidth
                    && localPoint.z >= -lowerDepth
                    && localPoint.z <= upperDepth)
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
            RestoreTrayPresentation();
            trayRig = null;
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
            if (isBound && trayRig != null)
            {
                LateUpdateTray();
                return;
            }

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
                    // The settle tween owns position and eases yaw; pitch and roll are held flat so a hand
                    // card never tilts. The tween ends on the flat resting rotation.
                    card.transform.rotation = FlattenToRest(card.transform.rotation, pose.WorldRotation);
                    continue;
                }

                bool selected = cardId == selectedCardId || assistedSelectedCardIds.Contains(cardId);
                bool hovered = !selected && cardId == hoveredCardId;
                if (!selected && !hovered && !animatingCardIds.Contains(cardId)) continue;

                float away = selected ? selectedAwayOffset : hovered ? hoverAwayOffset : 0f;
                float raise = selected ? selectedRaise : hovered ? hoverRaise : 0f;
                float scale = selected ? selectedScale : hovered ? hoverScale : 1f;
                Vector3 targetPosition = pose.WorldPosition
                    + (layoutAnchor.forward * away)
                    + (Vector3.up * raise);
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
            if (trayRig != null)
            {
                ApplyTrayPlan(plan, null);
                VisibleCardCount = plan.Count;
                return;
            }

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
            if (trayRig != null)
            {
                ApplyTrayPlan(plan, movingCard);
                return;
            }

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
                pickHalfDepths.Add(trayRig != null
                    ? TrayHalfDepth(card)
                    : ComponentRestHeight.HalfDepth(card.transform, cardLayoutWidth * 0.7f));
            }
        }

        private void ApplyTrayPlan(IReadOnlyList<CardLayoutPlan> plan, CardView excludedCard)
        {
            for (int i = 0; i < layoutAppliedCards.Count; i++)
            {
                CardView previousCard = layoutAppliedCards[i];
                if (previousCard != null
                    && previousCard.CardState != null
                    && previousCard.CardState.BaseState.ContainerId != containerState.Id)
                {
                    previousCard.ClearOverlayPresentation();
                    if (previousCard.IsContainerLayoutApplied)
                    {
                        previousCard.ClearContainerLayout();
                    }

                    trayMotions.Remove(previousCard.ObjectId);
                }
            }

            layoutAppliedCards.Clear();
            for (int i = 0; i < plan.Count; i++)
            {
                layoutAppliedCards.Add(plan[i].CardView);
            }

            trayExcludedCard = excludedCard;
            RecordPickOrder(plan);
            trayRig.UpdateFrame(TrayCardDepth(), layoutWidth, traySelectedRise);

            // Cards keep their current pose here (new members start where they are); LateUpdateTray
            // eases them to their slots, so reflows, draws and returns all glide.
            for (int i = 0; i < pickOrder.Count; i++)
            {
                CardView card = pickOrder[i];
                if (card == null
                    || !card.IsBound
                    || ReferenceEquals(card, excludedCard)
                    || ReferenceEquals(card, trayDraggedCard))
                {
                    continue;
                }

                TabletopObjectId cardId = card.ObjectId;
                TrayMotion motion = trayPlanSnaps
                    ? RestTrayMotion(i)
                    : trayMotions.TryGetValue(cardId, out TrayMotion current)
                        ? current
                        : CaptureTrayMotion(card);
                trayMotions[cardId] = motion;
                ApplyTrayMotion(card, motion);
            }
        }

        private void LateUpdateTray()
        {
            trayRig.UpdateFrame(TrayCardDepth(), layoutWidth, traySelectedRise);
            float deltaTime = Time.unscaledDeltaTime;
            float settle = 1f - Mathf.Exp(-trayResponse * deltaTime);
            float follow = 1f - Mathf.Exp(-trayDragResponse * deltaTime);
            int focusIndex = -1;
            if (!hoveredCardId.IsEmpty)
            {
                for (int i = 0; i < pickOrder.Count; i++)
                {
                    if (pickOrder[i] != null && pickOrder[i].IsBound && pickOrder[i].ObjectId == hoveredCardId)
                    {
                        focusIndex = i;
                        break;
                    }
                }
            }

            float center = (pickOrder.Count - 1) * 0.5f;
            for (int i = 0; i < pickOrder.Count; i++)
            {
                CardView card = pickOrder[i];
                if (card == null
                    || !card.IsBound
                    || card.CardState == null
                    || card.CardState.BaseState.ContainerId != containerState.Id)
                {
                    continue;
                }

                TabletopObjectId cardId = card.ObjectId;
                if (ReferenceEquals(card, trayDraggedCard))
                {
                    if (trayHandoffActive)
                    {
                        ApplyTrayHandoff(card, deltaTime);
                    }
                    else
                    {
                        ApplyTrayDragFollow(card, follow);
                    }

                    continue;
                }

                if ((card.PhysicalObject != null && card.PhysicalObject.IsHeld) || card.IsPreviewing)
                {
                    // Held on the table by a drag: forget the tray pose so it re-enters from wherever it is.
                    trayMotions.Remove(cardId);
                    continue;
                }

                if (ReferenceEquals(card, trayExcludedCard))
                {
                    continue;
                }

                if (presentationTransitions != null && presentationTransitions.IsAnimating(card.transform))
                {
                    // Take over draw/return/reorder tweens: continue from the tween's current pose.
                    presentationTransitions.Stop(card.transform, false);
                    trayMotions.Remove(cardId);
                }

                TrayMotion motion = trayMotions.TryGetValue(cardId, out TrayMotion current)
                    ? current
                    : CaptureTrayMotion(card);
                bool selected = cardId == selectedCardId || assistedSelectedCardIds.Contains(cardId);
                bool hovered = !selected && cardId == hoveredCardId;
                float spread = 0f;
                if (focusIndex >= 0 && i != focusIndex)
                {
                    int distance = Mathf.Abs(i - focusIndex);
                    spread = Mathf.Sign(i - focusIndex)
                        * trayHoverSpread
                        * Mathf.Max(0f, 1f - ((distance - 1) * 0.35f));
                }

                Vector3 targetPosition = new Vector3(
                    ((i - center) * layoutPitch) + spread,
                    (i * trayCardStep) + (selected ? traySelectedLift : hovered ? trayHoverLift : 0f),
                    selected ? traySelectedRise : hovered ? trayHoverRise : 0f);
                float targetScale = selected ? traySelectedScale : hovered ? trayHoverScale : 1f;
                motion.Position = Vector3.Lerp(motion.Position, targetPosition, settle);
                motion.Rotation = Quaternion.Slerp(motion.Rotation, Quaternion.identity, settle);
                motion.Scale = Mathf.Lerp(motion.Scale, targetScale, settle);
                trayMotions[cardId] = motion;
                ApplyTrayMotion(card, motion);
            }
        }

        private void ApplyTrayDragFollow(CardView card, float follow)
        {
            TabletopObjectId cardId = card.ObjectId;
            TrayMotion motion = trayMotions.TryGetValue(cardId, out TrayMotion current)
                ? current
                : CaptureTrayMotion(card);
            if (TryGetTrayLocalPoint(trayDragScreenPosition, out Vector3 pointerLocal))
            {
                Vector3 targetPosition = new Vector3(
                    pointerLocal.x + trayDragOffset.x,
                    (pickOrder.Count * trayCardStep) + traySelectedLift,
                    pointerLocal.z + trayDragOffset.y);
                motion.Position = Vector3.Lerp(motion.Position, targetPosition, follow);
            }

            motion.Rotation = Quaternion.Slerp(motion.Rotation, Quaternion.identity, follow);
            motion.Scale = Mathf.Lerp(motion.Scale, traySelectedScale, follow);
            trayMotions[cardId] = motion;
            ApplyTrayMotion(card, motion);
        }

        private void ApplyTrayHandoff(CardView card, float deltaTime)
        {
            trayHandoffElapsed += deltaTime;
            float t = trayHandoffDuration > 0f ? Mathf.Clamp01(trayHandoffElapsed / trayHandoffDuration) : 1f;
            // Ease-out: quick departure, gentle landing, so the switch to a held card happens at rest.
            float remaining = 1f - t;
            float eased = 1f - (remaining * remaining * remaining);
            Transform anchor = trayRig.Anchor;
            float targetScale = 1f / Mathf.Max(0.0001f, trayRig.Scale);
            float startScale = Mathf.Max(0.0001f, trayHandoffStart.Scale);
            TrayMotion motion;
            motion.Position = Vector3.Lerp(
                trayHandoffStart.Position,
                anchor.InverseTransformPoint(trayHandoffWorldPosition),
                eased);
            motion.Rotation = Quaternion.Slerp(
                trayHandoffStart.Rotation,
                Quaternion.Inverse(anchor.rotation) * trayHandoffWorldRotation,
                eased);
            // Interpolated in log space so the growth reads evenly as the card moves away.
            motion.Scale = Mathf.Exp(Mathf.Lerp(Mathf.Log(startScale), Mathf.Log(targetScale), eased));
            trayMotions[card.ObjectId] = motion;
            ApplyTrayMotion(card, motion);
        }

        private void ApplyTrayMotion(CardView card, TrayMotion motion)
        {
            Transform anchor = trayRig.Anchor;
            card.ApplyContainerOverlayPose(
                anchor.TransformPoint(motion.Position),
                anchor.rotation * motion.Rotation,
                trayRig.Scale * motion.Scale);
        }

        private TrayMotion RestTrayMotion(int index)
        {
            TrayMotion motion;
            motion.Position = new Vector3(
                (index - ((pickOrder.Count - 1) * 0.5f)) * layoutPitch,
                index * trayCardStep,
                0f);
            motion.Rotation = Quaternion.identity;
            motion.Scale = 1f;
            return motion;
        }

        private TrayMotion CaptureTrayMotion(CardView card)
        {
            Transform anchor = trayRig.Anchor;
            float baseScale = card.BaseLocalScale.x;
            float relativeScale = baseScale > 0f && trayRig.Scale > 0f
                ? (card.transform.localScale.x / baseScale) / trayRig.Scale
                : 1f;
            TrayMotion motion;
            motion.Position = anchor.InverseTransformPoint(card.transform.position);
            motion.Rotation = Quaternion.Inverse(anchor.rotation) * card.transform.rotation;
            motion.Scale = relativeScale;
            return motion;
        }

        private bool TryGetTrayLocalPoint(Vector2 screenPosition, out Vector3 local)
        {
            local = Vector3.zero;
            if (trayRig == null || trayRig.TargetCamera == null)
            {
                return false;
            }

            Transform anchor = trayRig.Anchor;
            Ray ray = trayRig.TargetCamera.ScreenPointToRay(screenPosition);
            Plane plane = new Plane(anchor.up, anchor.position);
            if (!plane.Raycast(ray, out float distance))
            {
                return false;
            }

            local = anchor.InverseTransformPoint(ray.GetPoint(distance));
            return true;
        }

        // How far a card has currently risen up the screen; picking extends its strip by this much, never past
        // the hover or selected lift, so a card still moving in cannot claim the space above the tray.
        private float TrayRise(CardView card)
        {
            return card != null && trayMotions.TryGetValue(card.ObjectId, out TrayMotion motion)
                ? Mathf.Clamp(motion.Position.z, 0f, MaximumTrayRise())
                : 0f;
        }

        private float MaximumTrayRise()
        {
            return Mathf.Max(traySelectedRise, trayHoverRise);
        }

        // A card flying into the tray from above (a draw, a drop from the table) is not hoverable or pickable
        // until it has come down to the tray strip; otherwise a resting pointer catches it mid-flight.
        private bool IsArrivingInTray(CardView card)
        {
            if (trayRig == null
                || ReferenceEquals(card, trayDraggedCard)
                || !trayMotions.TryGetValue(card.ObjectId, out TrayMotion motion))
            {
                return false;
            }

            return motion.Position.z > MaximumTrayRise() + (cardLayoutWidth * 0.5f);
        }

        // Half depth from the authored (unscaled) collider, so tray scaling does not change picking.
        private float TrayHalfDepth(CardView card)
        {
            BoxCollider box = card.GetComponent<BoxCollider>();
            return box != null
                ? box.size.z * 0.5f * card.BaseLocalScale.z
                : cardLayoutWidth * 0.7f;
        }

        private float TrayCardDepth()
        {
            float halfDepth = 0f;
            for (int i = 0; i < pickHalfDepths.Count; i++)
            {
                halfDepth = Mathf.Max(halfDepth, pickHalfDepths[i]);
            }

            return halfDepth > 0f ? halfDepth * 2f : cardLayoutWidth * 1.4f;
        }

        private void RestoreTrayPresentation()
        {
            if (trayRig == null)
            {
                return;
            }

            for (int i = 0; i < layoutAppliedCards.Count; i++)
            {
                if (layoutAppliedCards[i] != null)
                {
                    layoutAppliedCards[i].ClearOverlayPresentation();
                }
            }

            trayDraggedCard = null;
            trayExcludedCard = null;
            trayHandoffActive = false;
            trayMotions.Clear();
        }

        private static Quaternion FlattenToRest(Quaternion current, Quaternion rest)
        {
            Vector3 restUp = rest * Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(current * Vector3.forward, restUp);
            return forward.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(forward, restUp)
                : rest;
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

        private struct TrayMotion
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale;
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
