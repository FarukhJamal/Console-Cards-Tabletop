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
            : this(
                selectionState,
                cardSelectionVisuals,
                CreateSingleSelectionVisualList(pawnSelectionVisual),
                CreateSingleSelectionVisualList(tokenSelectionVisual))
        {
        }

        public TabletopSelectionPresenter(
            TabletopSelectionState selectionState,
            IReadOnlyList<TabletopSelectionVisual> cardSelectionVisuals,
            IReadOnlyList<TabletopSelectionVisual> pawnSelectionVisuals,
            IReadOnlyList<TabletopSelectionVisual> tokenSelectionVisuals)
        {
            if (selectionState == null)
            {
                throw new ArgumentNullException(nameof(selectionState));
            }

            if (cardSelectionVisuals == null)
            {
                throw new ArgumentNullException(nameof(cardSelectionVisuals));
            }

            if (pawnSelectionVisuals == null)
            {
                throw new ArgumentNullException(nameof(pawnSelectionVisuals));
            }

            if (tokenSelectionVisuals == null)
            {
                throw new ArgumentNullException(nameof(tokenSelectionVisuals));
            }

            ValidateSelectionVisuals(cardSelectionVisuals, nameof(cardSelectionVisuals));
            ValidateSelectionVisuals(pawnSelectionVisuals, nameof(pawnSelectionVisuals));
            ValidateSelectionVisuals(tokenSelectionVisuals, nameof(tokenSelectionVisuals));
            ValidateDistinctVisuals(cardSelectionVisuals, pawnSelectionVisuals, tokenSelectionVisuals);
            ValidateDistinctViewTargets(cardSelectionVisuals, pawnSelectionVisuals, tokenSelectionVisuals);

            SelectionState = selectionState;
            CardSelectionVisual = cardSelectionVisuals[0];
            CardSelectionVisuals = new List<TabletopSelectionVisual>(cardSelectionVisuals).AsReadOnly();
            PawnSelectionVisual = pawnSelectionVisuals[0];
            PawnSelectionVisuals = new List<TabletopSelectionVisual>(pawnSelectionVisuals).AsReadOnly();
            TokenSelectionVisual = tokenSelectionVisuals[0];
            TokenSelectionVisuals = new List<TabletopSelectionVisual>(tokenSelectionVisuals).AsReadOnly();
        }

        public TabletopSelectionState SelectionState { get; }

        public TabletopSelectionVisual CardSelectionVisual { get; }

        public IReadOnlyList<TabletopSelectionVisual> CardSelectionVisuals { get; }

        public TabletopSelectionVisual PawnSelectionVisual { get; }

        public IReadOnlyList<TabletopSelectionVisual> PawnSelectionVisuals { get; }

        public TabletopSelectionVisual TokenSelectionVisual { get; }

        public IReadOnlyList<TabletopSelectionVisual> TokenSelectionVisuals { get; }

        private static IReadOnlyList<TabletopSelectionVisual> CreateSingleCardSelectionVisualList(
            TabletopSelectionVisual cardSelectionVisual)
        {
            if (cardSelectionVisual == null)
            {
                throw new ArgumentNullException(nameof(cardSelectionVisual));
            }

            return new[] { cardSelectionVisual };
        }

        private static IReadOnlyList<TabletopSelectionVisual> CreateSingleSelectionVisualList(
            TabletopSelectionVisual selectionVisual)
        {
            if (selectionVisual == null)
            {
                throw new ArgumentNullException(nameof(selectionVisual));
            }

            return new[] { selectionVisual };
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

            SetSelected(PawnSelectionVisuals, selectedView);
            SetSelected(TokenSelectionVisuals, selectedView);
        }

        public void Clear()
        {
            for (int i = 0; i < CardSelectionVisuals.Count; i++)
            {
                CardSelectionVisuals[i].SetSelected(false);
            }

            ClearSelection(PawnSelectionVisuals);
            ClearSelection(TokenSelectionVisuals);
        }

        private static void SetSelected(
            IReadOnlyList<TabletopSelectionVisual> selectionVisuals,
            TabletopObjectView selectedView)
        {
            for (int i = 0; i < selectionVisuals.Count; i++)
            {
                TabletopSelectionVisual selectionVisual = selectionVisuals[i];
                selectionVisual.SetSelected(ReferenceEquals(selectionVisual.ObjectView, selectedView));
            }
        }

        private static void ClearSelection(IReadOnlyList<TabletopSelectionVisual> selectionVisuals)
        {
            for (int i = 0; i < selectionVisuals.Count; i++)
            {
                selectionVisuals[i].SetSelected(false);
            }
        }

        private static void ValidateSelectionVisuals(
            IReadOnlyList<TabletopSelectionVisual> selectionVisuals,
            string parameterName)
        {
            if (selectionVisuals.Count == 0)
            {
                throw new ArgumentException("At least one selection visual is required.", parameterName);
            }

            for (int i = 0; i < selectionVisuals.Count; i++)
            {
                if (selectionVisuals[i] == null)
                {
                    throw new ArgumentException("Selection visual collections cannot contain null entries.", parameterName);
                }

                ValidateConfiguredVisual(selectionVisuals[i], parameterName);
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
            IReadOnlyList<TabletopSelectionVisual> pawnSelectionVisuals,
            IReadOnlyList<TabletopSelectionVisual> tokenSelectionVisuals)
        {
            HashSet<TabletopSelectionVisual> seen = new HashSet<TabletopSelectionVisual>();
            AddDistinct(cardSelectionVisuals, seen);
            AddDistinct(pawnSelectionVisuals, seen);
            AddDistinct(tokenSelectionVisuals, seen);
        }

        private static void AddDistinct(
            IReadOnlyList<TabletopSelectionVisual> selectionVisuals,
            ISet<TabletopSelectionVisual> seen)
        {
            for (int i = 0; i < selectionVisuals.Count; i++)
            {
                if (!seen.Add(selectionVisuals[i]))
                {
                    throw new ArgumentException("Selection visuals must be different components.");
                }
            }
        }

        private static void ValidateDistinctViewTargets(
            IReadOnlyList<TabletopSelectionVisual> cardSelectionVisuals,
            IReadOnlyList<TabletopSelectionVisual> pawnSelectionVisuals,
            IReadOnlyList<TabletopSelectionVisual> tokenSelectionVisuals)
        {
            HashSet<TabletopObjectView> seen = new HashSet<TabletopObjectView>();
            AddDistinctViewTargets(cardSelectionVisuals, seen);
            AddDistinctViewTargets(pawnSelectionVisuals, seen);
            AddDistinctViewTargets(tokenSelectionVisuals, seen);
        }

        private static void AddDistinctViewTargets(
            IReadOnlyList<TabletopSelectionVisual> selectionVisuals,
            ISet<TabletopObjectView> seen)
        {
            for (int i = 0; i < selectionVisuals.Count; i++)
            {
                if (!seen.Add(selectionVisuals[i].ObjectView))
                {
                    throw new ArgumentException("Selection visuals must target different TabletopObjectView instances.");
                }
            }
        }
    }
}
