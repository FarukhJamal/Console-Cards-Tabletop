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

        /// <summary>
        /// U02 (playtest: "unexpected rotation when players intend to zoom"): true when the pointer is over this view.
        /// When set, the wheel turns the selected piece only while the pointer is over it, and zooms otherwise.
        /// </summary>
        public Func<TabletopObjectView, bool> PointerIsOver { get; set; }

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

            // P-5 (owner, 2026-11-13, as in Tabletop Simulator): the wheel only zooms; pieces turn with the keys (Q / E) on
            // the piece under the pointer, the selection or the carried piece. The ObjectRotation route stays for callers
            // that still name it, but the wheel never takes it.
            return TabletopScrollInputRoute.CameraZoom;
        }

        /// <summary>
        /// P-5: the wheel zooms the camera whenever no piece is being dragged (a selected piece, or one in a pile or a
        /// hand, no longer keeps the wheel). While a piece is carried the wheel still raises or lowers it.
        /// </summary>
        public bool WheelZoomsCamera()
        {
            if (interactionRouter != null && interactionRouter.HasActiveInteraction) return false;
            return !MoveCoordinator.HasActiveInteraction;
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
