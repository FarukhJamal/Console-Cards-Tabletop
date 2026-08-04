using System;
using System.Runtime.ExceptionServices;
using ConsoleCards.Core.Domain;
using ConsoleCards.Presentation.Views;
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

        public bool TryBegin(Vector2 screenPosition)
        {
            ValidateScreenPosition(screenPosition, nameof(screenPosition));
            EnsureNoActiveInteraction();

            if (moveCoordinator.HasActiveInteraction || containedCardDragCoordinator.HasActiveInteraction)
            {
                throw new InvalidOperationException("An interaction lifecycle is already active.");
            }

            if (hitResolver.TryResolve(screenPosition, out TabletopObjectView resolvedView)
                && TryGetContainedCardView(resolvedView, out CardView containedCardView))
            {
                if (!containedCardDragCoordinator.TryBegin(containedCardView, screenPosition))
                {
                    return false;
                }

                selectionState.Select(containedCardView);
                activeRoute = TabletopInteractionRoute.ContainedCardDrag;
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
                    containedCardDragCoordinator.UpdatePointer(screenPosition);
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
                        return TabletopInteractionReleaseResult.FromMove(
                            moveCoordinator.ReleasePointer(screenPosition));
                    }
                    finally
                    {
                        activeRoute = TabletopInteractionRoute.None;
                    }
                case TabletopInteractionRoute.ContainedCardDrag:
                    try
                    {
                        return TabletopInteractionReleaseResult.FromContainedCard(
                            containedCardDragCoordinator.Release(screenPosition));
                    }
                    finally
                    {
                        activeRoute = TabletopInteractionRoute.None;
                    }
                case TabletopInteractionRoute.None:
                    return TabletopInteractionReleaseResult.NoActiveInteraction();
                default:
                    activeRoute = TabletopInteractionRoute.None;
                    throw new InvalidOperationException("Unsupported tabletop interaction route.");
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
                    }
                    finally
                    {
                        activeRoute = TabletopInteractionRoute.None;
                    }

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
