using System;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views;

namespace ConsoleCards.Presentation.Input
{
    public sealed class TabletopInteractionInputRoutingPolicy
    {
        public TabletopInteractionInputRoutingPolicy(
            TabletopSelectionState selectionState,
            TabletopMoveInteractionCoordinator moveCoordinator)
        {
            SelectionState = selectionState ?? throw new ArgumentNullException(nameof(selectionState));
            MoveCoordinator = moveCoordinator ?? throw new ArgumentNullException(nameof(moveCoordinator));
        }

        public TabletopSelectionState SelectionState { get; }

        public TabletopMoveInteractionCoordinator MoveCoordinator { get; }

        public TabletopScrollInputRoute ResolveScrollRoute()
        {
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
            return selectedView.IsPreviewing
                ? TabletopScrollInputRoute.Suppressed
                : TabletopScrollInputRoute.ObjectRotation;
        }
    }
}
