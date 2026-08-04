using System;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views;

namespace ConsoleCards.Presentation.Input
{
    public sealed class TabletopInteractionInputRoutingPolicy
    {
        private TabletopInteractionRouter interactionRouter;

        public TabletopInteractionInputRoutingPolicy(
            TabletopSelectionState selectionState,
            TabletopMoveInteractionCoordinator moveCoordinator)
        {
            SelectionState = selectionState ?? throw new ArgumentNullException(nameof(selectionState));
            MoveCoordinator = moveCoordinator ?? throw new ArgumentNullException(nameof(moveCoordinator));
        }

        public TabletopSelectionState SelectionState { get; }

        public TabletopMoveInteractionCoordinator MoveCoordinator { get; }

        public bool HasInteractionRouter => interactionRouter != null;

        public TabletopInteractionRouter InteractionRouter => interactionRouter;

        public void ConfigureInteractionRouter(TabletopInteractionRouter router)
        {
            if (router == null)
            {
                throw new ArgumentNullException(nameof(router));
            }

            if (interactionRouter != null)
            {
                throw new InvalidOperationException("Tabletop interaction input routing policy already has an interaction router.");
            }

            interactionRouter = router;
        }

        public void ClearInteractionRouter()
        {
            interactionRouter = null;
        }

        public TabletopScrollInputRoute ResolveScrollRoute()
        {
            if (interactionRouter != null && interactionRouter.HasActiveInteraction)
            {
                return TabletopScrollInputRoute.Suppressed;
            }

            if (MoveCoordinator.HasActiveInteraction)
            {
                return TabletopScrollInputRoute.Suppressed;
            }

            SelectionState.ClearUnavailable();
            if (!SelectionState.HasSelection)
            {
                return TabletopScrollInputRoute.CameraZoom;
            }

            TabletopObjectView selectedView = SelectionState.SelectedView;
            if (selectedView.IsPreviewing || IsContainedCard(selectedView))
            {
                return TabletopScrollInputRoute.Suppressed;
            }

            return TabletopScrollInputRoute.ObjectRotation;
        }

        private static bool IsContainedCard(TabletopObjectView view)
        {
            CardView cardView = view as CardView;
            return cardView != null
                && cardView.CardState != null
                && !cardView.CardState.BaseState.ContainerId.IsEmpty;
        }
    }
}
