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
            : this(
                selectionState,
                cardSelectionVisuals,
                pawnSelectionVisuals,
                tokenSelectionVisuals,
                Array.Empty<TabletopSelectionVisual>())
        {
        }

        public TabletopSelectionPresenter(
            TabletopSelectionState selectionState,
            IReadOnlyList<TabletopSelectionVisual> cardSelectionVisuals,
            IReadOnlyList<TabletopSelectionVisual> pawnSelectionVisuals,
            IReadOnlyList<TabletopSelectionVisual> tokenSelectionVisuals,
            IReadOnlyList<TabletopSelectionVisual> dieSelectionVisuals)
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

            if (dieSelectionVisuals == null)
            {
                throw new ArgumentNullException(nameof(dieSelectionVisuals));
            }

            ValidateSelectionVisuals(cardSelectionVisuals, nameof(cardSelectionVisuals));
            ValidateSelectionVisuals(pawnSelectionVisuals, nameof(pawnSelectionVisuals));
            ValidateSelectionVisuals(tokenSelectionVisuals, nameof(tokenSelectionVisuals));
            ValidateSelectionVisuals(dieSelectionVisuals, nameof(dieSelectionVisuals));
            ValidateDistinctVisuals(cardSelectionVisuals, pawnSelectionVisuals, tokenSelectionVisuals, dieSelectionVisuals);
            ValidateDistinctViewTargets(cardSelectionVisuals, pawnSelectionVisuals, tokenSelectionVisuals, dieSelectionVisuals);

            SelectionState = selectionState;
            CardSelectionVisual = cardSelectionVisuals.Count > 0
                ? cardSelectionVisuals[0]
                : null;
            CardSelectionVisuals = new List<TabletopSelectionVisual>(cardSelectionVisuals).AsReadOnly();
            PawnSelectionVisual = pawnSelectionVisuals.Count > 0
                ? pawnSelectionVisuals[0]
                : null;
            PawnSelectionVisuals = new List<TabletopSelectionVisual>(pawnSelectionVisuals).AsReadOnly();
            TokenSelectionVisual = tokenSelectionVisuals.Count > 0
                ? tokenSelectionVisuals[0]
                : null;
            TokenSelectionVisuals = new List<TabletopSelectionVisual>(tokenSelectionVisuals).AsReadOnly();
            DieSelectionVisual = dieSelectionVisuals.Count > 0
                ? dieSelectionVisuals[0]
                : null;
            DieSelectionVisuals = new List<TabletopSelectionVisual>(dieSelectionVisuals).AsReadOnly();
        }

        public TabletopSelectionState SelectionState { get; }

        public TabletopSelectionVisual CardSelectionVisual { get; }

        public IReadOnlyList<TabletopSelectionVisual> CardSelectionVisuals { get; }

        public TabletopSelectionVisual PawnSelectionVisual { get; }

        public IReadOnlyList<TabletopSelectionVisual> PawnSelectionVisuals { get; }

        public TabletopSelectionVisual TokenSelectionVisual { get; }

        public IReadOnlyList<TabletopSelectionVisual> TokenSelectionVisuals { get; }

        public TabletopSelectionVisual DieSelectionVisual { get; }

        public IReadOnlyList<TabletopSelectionVisual> DieSelectionVisuals { get; }

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
            SetSelected(DieSelectionVisuals, selectedView);
        }

        public void Clear()
        {
            for (int i = 0; i < CardSelectionVisuals.Count; i++)
            {
                CardSelectionVisuals[i].SetSelected(false);
            }

            ClearSelection(PawnSelectionVisuals);
            ClearSelection(TokenSelectionVisuals);
            ClearSelection(DieSelectionVisuals);
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
            IReadOnlyList<TabletopSelectionVisual> tokenSelectionVisuals,
            IReadOnlyList<TabletopSelectionVisual> dieSelectionVisuals)
        {
            HashSet<TabletopSelectionVisual> seen = new HashSet<TabletopSelectionVisual>();
            AddDistinct(cardSelectionVisuals, seen);
            AddDistinct(pawnSelectionVisuals, seen);
            AddDistinct(tokenSelectionVisuals, seen);
            AddDistinct(dieSelectionVisuals, seen);
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
            IReadOnlyList<TabletopSelectionVisual> tokenSelectionVisuals,
            IReadOnlyList<TabletopSelectionVisual> dieSelectionVisuals)
        {
            HashSet<TabletopObjectView> seen = new HashSet<TabletopObjectView>();
            AddDistinctViewTargets(cardSelectionVisuals, seen);
            AddDistinctViewTargets(pawnSelectionVisuals, seen);
            AddDistinctViewTargets(tokenSelectionVisuals, seen);
            AddDistinctViewTargets(dieSelectionVisuals, seen);
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
