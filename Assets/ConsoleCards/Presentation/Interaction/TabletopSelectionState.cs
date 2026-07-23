using System;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// Local Presentation selection state for one hovered object and one primary selected object.
    /// </summary>
    public sealed class TabletopSelectionState
    {
        private TabletopObjectView hoveredView;
        private TabletopObjectView selectedView;

        public TabletopObjectView HoveredView => hoveredView;

        public TabletopObjectView SelectedView => selectedView;

        public bool HasHoveredObject => IsAvailable(hoveredView);

        public bool HasSelection => IsAvailable(selectedView);

        public TabletopObjectId HoveredObjectId => IsAvailable(hoveredView)
            ? hoveredView.ObjectId
            : TabletopObjectId.Empty;

        public TabletopObjectId SelectedObjectId => IsAvailable(selectedView)
            ? selectedView.ObjectId
            : TabletopObjectId.Empty;

        public void SetHovered(TabletopObjectView view)
        {
            if (view == null)
            {
                ClearHovered();
                return;
            }

            ValidateBoundView(view, nameof(view));
            hoveredView = view;
        }

        public void ClearHovered()
        {
            hoveredView = null;
        }

        public void Select(TabletopObjectView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            ValidateBoundView(view, nameof(view));
            selectedView = view;
        }

        public void ClearSelection()
        {
            selectedView = null;
        }

        public void ClearAll()
        {
            hoveredView = null;
            selectedView = null;
        }

        public bool ClearUnavailable()
        {
            bool changed = false;

            if (!ReferenceEquals(hoveredView, null) && !IsAvailable(hoveredView))
            {
                hoveredView = null;
                changed = true;
            }

            if (!ReferenceEquals(selectedView, null) && !IsAvailable(selectedView))
            {
                selectedView = null;
                changed = true;
            }

            return changed;
        }

        private static void ValidateBoundView(TabletopObjectView view, string parameterName)
        {
            if (!view.IsBound)
            {
                throw new ArgumentException("Tabletop object View must be bound.", parameterName);
            }
        }

        private static bool IsAvailable(TabletopObjectView view)
        {
            return !ReferenceEquals(view, null)
                && view != null
                && view.IsBound
                && view.isActiveAndEnabled;
        }
    }
}
