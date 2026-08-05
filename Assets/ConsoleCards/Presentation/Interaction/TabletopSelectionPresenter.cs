using System;
using System.Collections.Generic;
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
            : this(
                selectionState,
                CreateSingleCardSelectionVisualList(cardSelectionVisual),
                pawnSelectionVisual,
                tokenSelectionVisual)
        {
        }

        public TabletopSelectionPresenter(
            TabletopSelectionState selectionState,
            IReadOnlyList<TabletopSelectionVisual> cardSelectionVisuals,
            TabletopSelectionVisual pawnSelectionVisual,
            TabletopSelectionVisual tokenSelectionVisual)
        {
            if (selectionState == null)
            {
                throw new ArgumentNullException(nameof(selectionState));
            }

            if (cardSelectionVisuals == null)
            {
                throw new ArgumentNullException(nameof(cardSelectionVisuals));
            }

            if (pawnSelectionVisual == null)
            {
                throw new ArgumentNullException(nameof(pawnSelectionVisual));
            }

            if (tokenSelectionVisual == null)
            {
                throw new ArgumentNullException(nameof(tokenSelectionVisual));
            }

            ValidateCardVisuals(cardSelectionVisuals);
            ValidateConfiguredVisual(pawnSelectionVisual, nameof(pawnSelectionVisual));
            ValidateConfiguredVisual(tokenSelectionVisual, nameof(tokenSelectionVisual));
            ValidateDistinctVisuals(cardSelectionVisuals, pawnSelectionVisual, tokenSelectionVisual);
            ValidateDistinctViewTargets(cardSelectionVisuals, pawnSelectionVisual, tokenSelectionVisual);

            SelectionState = selectionState;
            CardSelectionVisual = cardSelectionVisuals[0];
            CardSelectionVisuals = new List<TabletopSelectionVisual>(cardSelectionVisuals).AsReadOnly();
            PawnSelectionVisual = pawnSelectionVisual;
            TokenSelectionVisual = tokenSelectionVisual;
        }

        public TabletopSelectionState SelectionState { get; }

        public TabletopSelectionVisual CardSelectionVisual { get; }

        public IReadOnlyList<TabletopSelectionVisual> CardSelectionVisuals { get; }

        public TabletopSelectionVisual PawnSelectionVisual { get; }

        public TabletopSelectionVisual TokenSelectionVisual { get; }

        private static IReadOnlyList<TabletopSelectionVisual> CreateSingleCardSelectionVisualList(
            TabletopSelectionVisual cardSelectionVisual)
        {
            if (cardSelectionVisual == null)
            {
                throw new ArgumentNullException(nameof(cardSelectionVisual));
            }

            return new[] { cardSelectionVisual };
        }

        public void Refresh()
        {
            SelectionState.ClearUnavailable();
            TabletopObjectView selectedView = SelectionState.SelectedView;

            for (int i = 0; i < CardSelectionVisuals.Count; i++)
            {
                TabletopSelectionVisual cardSelectionVisual = CardSelectionVisuals[i];
                cardSelectionVisual.SetSelected(ReferenceEquals(cardSelectionVisual.ObjectView, selectedView));
            }

            PawnSelectionVisual.SetSelected(ReferenceEquals(PawnSelectionVisual.ObjectView, selectedView));
            TokenSelectionVisual.SetSelected(ReferenceEquals(TokenSelectionVisual.ObjectView, selectedView));
        }

        public void Clear()
        {
            for (int i = 0; i < CardSelectionVisuals.Count; i++)
            {
                CardSelectionVisuals[i].SetSelected(false);
            }

            PawnSelectionVisual.SetSelected(false);
            TokenSelectionVisual.SetSelected(false);
        }

        private static void ValidateCardVisuals(IReadOnlyList<TabletopSelectionVisual> cardSelectionVisuals)
        {
            if (cardSelectionVisuals.Count == 0)
            {
                throw new ArgumentException("At least one Card selection visual is required.", nameof(cardSelectionVisuals));
            }

            for (int i = 0; i < cardSelectionVisuals.Count; i++)
            {
                if (cardSelectionVisuals[i] == null)
                {
                    throw new ArgumentException("Card selection visual collection cannot contain null entries.", nameof(cardSelectionVisuals));
                }

                ValidateConfiguredVisual(cardSelectionVisuals[i], nameof(cardSelectionVisuals));
            }
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
            IReadOnlyList<TabletopSelectionVisual> cardSelectionVisuals,
            TabletopSelectionVisual pawnSelectionVisual,
            TabletopSelectionVisual tokenSelectionVisual)
        {
            HashSet<TabletopSelectionVisual> seen = new HashSet<TabletopSelectionVisual>();
            for (int i = 0; i < cardSelectionVisuals.Count; i++)
            {
                if (!seen.Add(cardSelectionVisuals[i]))
                {
                    throw new ArgumentException("Selection visuals must be different components.");
                }
            }

            if (!seen.Add(pawnSelectionVisual) || !seen.Add(tokenSelectionVisual))
            {
                throw new ArgumentException("Selection visuals must be different components.");
            }
        }

        private static void ValidateDistinctViewTargets(
            IReadOnlyList<TabletopSelectionVisual> cardSelectionVisuals,
            TabletopSelectionVisual pawnSelectionVisual,
            TabletopSelectionVisual tokenSelectionVisual)
        {
            HashSet<TabletopObjectView> seen = new HashSet<TabletopObjectView>();
            for (int i = 0; i < cardSelectionVisuals.Count; i++)
            {
                if (!seen.Add(cardSelectionVisuals[i].ObjectView))
                {
                    throw new ArgumentException("Selection visuals must target different TabletopObjectView instances.");
                }
            }

            if (!seen.Add(pawnSelectionVisual.ObjectView) || !seen.Add(tokenSelectionVisual.ObjectView))
            {
                throw new ArgumentException("Selection visuals must target different TabletopObjectView instances.");
            }
        }
    }
}
