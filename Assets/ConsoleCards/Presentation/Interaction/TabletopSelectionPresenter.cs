using System;
using ConsoleCards.Presentation.Views;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// Projects one local selection state onto the prototype object's explicit selection visuals.
    /// </summary>
    public sealed class TabletopSelectionPresenter
    {
        public TabletopSelectionPresenter(
            TabletopSelectionState selectionState,
            TabletopSelectionVisual cardSelectionVisual,
            TabletopSelectionVisual pawnSelectionVisual,
            TabletopSelectionVisual tokenSelectionVisual)
        {
            if (selectionState == null)
            {
                throw new ArgumentNullException(nameof(selectionState));
            }

            if (cardSelectionVisual == null)
            {
                throw new ArgumentNullException(nameof(cardSelectionVisual));
            }

            if (pawnSelectionVisual == null)
            {
                throw new ArgumentNullException(nameof(pawnSelectionVisual));
            }

            if (tokenSelectionVisual == null)
            {
                throw new ArgumentNullException(nameof(tokenSelectionVisual));
            }

            ValidateConfiguredVisual(cardSelectionVisual, nameof(cardSelectionVisual));
            ValidateConfiguredVisual(pawnSelectionVisual, nameof(pawnSelectionVisual));
            ValidateConfiguredVisual(tokenSelectionVisual, nameof(tokenSelectionVisual));
            ValidateDistinctVisuals(cardSelectionVisual, pawnSelectionVisual, tokenSelectionVisual);
            ValidateDistinctViewTargets(cardSelectionVisual, pawnSelectionVisual, tokenSelectionVisual);

            SelectionState = selectionState;
            CardSelectionVisual = cardSelectionVisual;
            PawnSelectionVisual = pawnSelectionVisual;
            TokenSelectionVisual = tokenSelectionVisual;
        }

        public TabletopSelectionState SelectionState { get; }

        public TabletopSelectionVisual CardSelectionVisual { get; }

        public TabletopSelectionVisual PawnSelectionVisual { get; }

        public TabletopSelectionVisual TokenSelectionVisual { get; }

        public void Refresh()
        {
            SelectionState.ClearUnavailable();
            TabletopObjectView selectedView = SelectionState.SelectedView;

            CardSelectionVisual.SetSelected(ReferenceEquals(CardSelectionVisual.ObjectView, selectedView));
            PawnSelectionVisual.SetSelected(ReferenceEquals(PawnSelectionVisual.ObjectView, selectedView));
            TokenSelectionVisual.SetSelected(ReferenceEquals(TokenSelectionVisual.ObjectView, selectedView));
        }

        public void Clear()
        {
            CardSelectionVisual.SetSelected(false);
            PawnSelectionVisual.SetSelected(false);
            TokenSelectionVisual.SetSelected(false);
        }

        private static void ValidateConfiguredVisual(
            TabletopSelectionVisual visual,
            string parameterName)
        {
            if (!visual.IsConfigured)
            {
                throw new ArgumentException("Selection visual must be configured.", parameterName);
            }
        }

        private static void ValidateDistinctVisuals(
            TabletopSelectionVisual cardSelectionVisual,
            TabletopSelectionVisual pawnSelectionVisual,
            TabletopSelectionVisual tokenSelectionVisual)
        {
            if (ReferenceEquals(cardSelectionVisual, pawnSelectionVisual)
                || ReferenceEquals(cardSelectionVisual, tokenSelectionVisual)
                || ReferenceEquals(pawnSelectionVisual, tokenSelectionVisual))
            {
                throw new ArgumentException("Selection visuals must be different components.");
            }
        }

        private static void ValidateDistinctViewTargets(
            TabletopSelectionVisual cardSelectionVisual,
            TabletopSelectionVisual pawnSelectionVisual,
            TabletopSelectionVisual tokenSelectionVisual)
        {
            if (ReferenceEquals(cardSelectionVisual.ObjectView, pawnSelectionVisual.ObjectView)
                || ReferenceEquals(cardSelectionVisual.ObjectView, tokenSelectionVisual.ObjectView)
                || ReferenceEquals(pawnSelectionVisual.ObjectView, tokenSelectionVisual.ObjectView))
            {
                throw new ArgumentException("Selection visuals must target different TabletopObjectView instances.");
            }
        }
    }
}
