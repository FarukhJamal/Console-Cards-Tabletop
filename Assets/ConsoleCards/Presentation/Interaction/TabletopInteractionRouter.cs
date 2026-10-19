using System;
using System.Runtime.ExceptionServices;
using ConsoleCards.Core.Domain;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    public sealed class TabletopInteractionRouter
    {
        private readonly TabletopObjectHitResolver hitResolver;
        private readonly TabletopMoveInteractionCoordinator moveCoordinator;
        private readonly ContainedCardDragCoordinator containedCardDragCoordinator;
        private readonly TabletopSelectionState selectionState;

        private TabletopInteractionRoute activeRoute;
        private float pressStartedAt;

        public TabletopInteractionRouter(
            TabletopObjectHitResolver hitResolver,
            TabletopMoveInteractionCoordinator moveCoordinator,
            ContainedCardDragCoordinator containedCardDragCoordinator,
            TabletopSelectionState selectionState)
        {
            this.hitResolver = hitResolver ?? throw new ArgumentNullException(nameof(hitResolver));
            this.moveCoordinator = moveCoordinator ?? throw new ArgumentNullException(nameof(moveCoordinator));
            this.containedCardDragCoordinator = containedCardDragCoordinator ?? throw new ArgumentNullException(nameof(containedCardDragCoordinator));
            this.selectionState = selectionState ?? throw new ArgumentNullException(nameof(selectionState));
            activeRoute = TabletopInteractionRoute.None;
        }

        public TabletopInteractionRoute ActiveRoute => activeRoute;

        public bool HasActiveInteraction => activeRoute != TabletopInteractionRoute.None;

        /// <summary>
        /// Optional local hand. A card that ends a real drag in it (accepted, put back, cancelled or
        /// dropped on no target) is deselected so it settles into the row instead of taking the selected
        /// lift; a plain click still selects, and hover lift is unaffected.
        /// </summary>
        public HandView HandView { get; set; }

        /// <summary>
        /// Optional: picks up a whole stack when a card in it is pressed and held still for
        /// <see cref="LongPressSeconds"/> (owner, 2026-10-18). Moving first still takes the top card out.
        /// </summary>
        public ITabletopCarryHandler CarryHandler { get; set; }

        /// <summary>Optional: Shift-press on a loose piece aims a flick instead of picking it up (UX-1).</summary>
        public ITabletopFlickHandler FlickHandler { get; set; }

        /// <summary>How long a still press on a stack card waits before the whole stack is picked up.</summary>
        public float LongPressSeconds { get; set; } = 0.45f;

        public bool TryBegin(Vector2 screenPosition)
        {
            ValidateScreenPosition(screenPosition, nameof(screenPosition));
            EnsureNoActiveInteraction();

            if (moveCoordinator.HasActiveInteraction || containedCardDragCoordinator.HasActiveInteraction)
            {
                throw new InvalidOperationException("An interaction lifecycle is already active.");
            }

            if (FlickHandler != null
                && FlickHandler.WantsFlick()
                && hitResolver.TryResolve(screenPosition, out TabletopObjectView flickView)
                && FlickHandler.TryBeginFlick(flickView, screenPosition))
            {
                selectionState.ClearHovered();
                activeRoute = TabletopInteractionRoute.Flick;
                return true;
            }

            if (hitResolver.TryResolve(screenPosition, out TabletopObjectView resolvedView)
                && TryGetContainedCardView(resolvedView, out CardView containedCardView))
            {
                if (!containedCardDragCoordinator.TryBegin(containedCardView, screenPosition))
                {
                    return false;
                }

                selectionState.Select(containedCardView);
                selectionState.ClearHovered();
                activeRoute = TabletopInteractionRoute.ContainedCardDrag;
                pressStartedAt = Time.unscaledTime;
                return true;
            }

            if (!moveCoordinator.TryBeginPress(screenPosition))
            {
                return false;
            }

            activeRoute = TabletopInteractionRoute.TabletopMove;
            return true;
        }

        public void UpdatePointer(Vector2 screenPosition)
        {
            ValidateScreenPosition(screenPosition, nameof(screenPosition));

            switch (activeRoute)
            {
                case TabletopInteractionRoute.TabletopMove:
                    moveCoordinator.UpdatePointer(screenPosition);
                    return;
                case TabletopInteractionRoute.ContainedCardDrag:
                    if (TryStartCarry(screenPosition))
                    {
                        return;
                    }

                    containedCardDragCoordinator.UpdatePointer(screenPosition);
                    return;
                case TabletopInteractionRoute.StackCarry:
                    CarryHandler.UpdateCarry(screenPosition);
                    return;
                case TabletopInteractionRoute.Flick:
                    FlickHandler.UpdateFlick(screenPosition);
                    return;
                case TabletopInteractionRoute.None:
                    throw new InvalidOperationException("No tabletop interaction route is active.");
                default:
                    throw new InvalidOperationException("Unsupported tabletop interaction route.");
            }
        }

        public TabletopInteractionReleaseResult Release(Vector2 screenPosition)
        {
            ValidateScreenPosition(screenPosition, nameof(screenPosition));

            switch (activeRoute)
            {
                case TabletopInteractionRoute.TabletopMove:
                    try
                    {
                        TabletopInteractionReleaseResult moveResult = TabletopInteractionReleaseResult.FromMove(
                            moveCoordinator.ReleasePointer(screenPosition));
                        ClearSelectionAfterHandDrop(moveResult);
                        return moveResult;
                    }
                    finally
                    {
                        activeRoute = TabletopInteractionRoute.None;
                    }
                case TabletopInteractionRoute.ContainedCardDrag:
                    try
                    {
                        TabletopInteractionReleaseResult containedResult = TabletopInteractionReleaseResult.FromContainedCard(
                            containedCardDragCoordinator.Release(screenPosition));
                        ClearSelectionAfterHandDrop(containedResult);
                        return containedResult;
                    }
                    finally
                    {
                        activeRoute = TabletopInteractionRoute.None;
                    }
                case TabletopInteractionRoute.StackCarry:
                    // The route ends first: committing the carry may rebuild the table's interaction graph.
                    activeRoute = TabletopInteractionRoute.None;
                    CarryHandler.ReleaseCarry(screenPosition);
                    return TabletopInteractionReleaseResult.FromCarry();
                case TabletopInteractionRoute.Flick:
                    activeRoute = TabletopInteractionRoute.None;
                    FlickHandler.ReleaseFlick(screenPosition);
                    return TabletopInteractionReleaseResult.FromFlick();
                case TabletopInteractionRoute.None:
                    return TabletopInteractionReleaseResult.NoActiveInteraction();
                default:
                    activeRoute = TabletopInteractionRoute.None;
                    throw new InvalidOperationException("Unsupported tabletop interaction route.");
            }
        }

        // A press on a stack card held still long enough picks up the whole stack instead of its top card.
        private bool TryStartCarry(Vector2 screenPosition)
        {
            if (CarryHandler == null
                || containedCardDragCoordinator.Phase != TabletopInteractionPhase.Pressed
                || Time.unscaledTime - pressStartedAt < LongPressSeconds)
            {
                return false;
            }

            CardView pressedCard = containedCardDragCoordinator.ActiveCardView;
            if (pressedCard == null || !CarryHandler.CanCarry(pressedCard))
            {
                return false;
            }

            containedCardDragCoordinator.Cancel();
            selectionState.ClearSelection();
            activeRoute = CarryHandler.TryBeginCarry(pressedCard, screenPosition)
                ? TabletopInteractionRoute.StackCarry
                : TabletopInteractionRoute.None;
            return true;
        }

        private void ClearSelectionAfterHandDrop(TabletopInteractionReleaseResult result)
        {
            bool endedHandDrag =
                (result.MoveResult.HasValue
                    && result.MoveResult.Value.Status == MoveInteractionReleaseStatus.CardTransferAccepted)
                || (result.ContainedCardResult.HasValue
                    && result.ContainedCardResult.Value.Status != ContainedCardDragReleaseStatus.ClickReleased);
            if (endedHandDrag)
            {
                ClearSelectionIfInHand();
            }
        }

        private void ClearSelectionIfInHand()
        {
            HandView hand = HandView;
            if (hand == null || !hand.IsBound)
            {
                return;
            }

            if (selectionState.SelectedView is CardView card
                && card.BoundState != null
                && card.BoundState.ContainerId == hand.ContainerId)
            {
                selectionState.ClearSelection();
            }
        }

        public void Cancel()
        {
            switch (activeRoute)
            {
                case TabletopInteractionRoute.TabletopMove:
                    try
                    {
                        moveCoordinator.Cancel();
                    }
                    finally
                    {
                        activeRoute = TabletopInteractionRoute.None;
                    }

                    return;
                case TabletopInteractionRoute.ContainedCardDrag:
                    try
                    {
                        containedCardDragCoordinator.Cancel();
                        ClearSelectionIfInHand();
                    }
                    finally
                    {
                        activeRoute = TabletopInteractionRoute.None;
                    }

                    return;
                case TabletopInteractionRoute.StackCarry:
                    activeRoute = TabletopInteractionRoute.None;
                    CarryHandler?.CancelCarry();
                    return;
                case TabletopInteractionRoute.Flick:
                    activeRoute = TabletopInteractionRoute.None;
                    FlickHandler?.CancelFlick();
                    return;
                case TabletopInteractionRoute.None:
                    return;
                default:
                    activeRoute = TabletopInteractionRoute.None;
                    throw new InvalidOperationException("Unsupported tabletop interaction route.");
            }
        }

        public void Reset()
        {
            Exception firstFailure = null;
            if (activeRoute == TabletopInteractionRoute.StackCarry)
            {
                activeRoute = TabletopInteractionRoute.None;
                CarryHandler?.CancelCarry();
            }
            else if (activeRoute == TabletopInteractionRoute.Flick)
            {
                activeRoute = TabletopInteractionRoute.None;
                FlickHandler?.CancelFlick();
            }

            try
            {
                if (activeRoute == TabletopInteractionRoute.TabletopMove)
                {
                    moveCoordinator.Reset();
                }
                else
                {
                    containedCardDragCoordinator.Reset();
                }
            }
            catch (Exception exception)
            {
                firstFailure = exception;
            }

            try
            {
                if (activeRoute == TabletopInteractionRoute.ContainedCardDrag)
                {
                    containedCardDragCoordinator.Reset();
                }
                else
                {
                    moveCoordinator.Reset();
                }
            }
            catch (Exception exception)
            {
                if (firstFailure == null)
                {
                    firstFailure = exception;
                }
            }
            finally
            {
                activeRoute = TabletopInteractionRoute.None;
            }

            if (firstFailure != null)
            {
                ExceptionDispatchInfo.Capture(firstFailure).Throw();
            }
        }

        private static bool TryGetContainedCardView(
            TabletopObjectView view,
            out CardView cardView)
        {
            cardView = view as CardView;
            return cardView != null
                && cardView.CardState != null
                && cardView.BoundState != null
                && cardView.BoundState.Kind == TabletopObjectKind.Card
                && !cardView.CardState.BaseState.ContainerId.IsEmpty;
        }

        private void EnsureNoActiveInteraction()
        {
            if (HasActiveInteraction)
            {
                throw new InvalidOperationException("A tabletop interaction route is already active.");
            }
        }

        private static void ValidateScreenPosition(Vector2 screenPosition, string parameterName)
        {
            if (!IsFinite(screenPosition.x) || !IsFinite(screenPosition.y))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
