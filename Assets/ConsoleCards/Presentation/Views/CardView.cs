using System;
using ConsoleCards.Core.Domain;
using ConsoleCards.Presentation.Coordinates;

namespace ConsoleCards.Presentation.Views
{
    public sealed class CardView : TabletopObjectView
    {
        private CardInstanceState cardState;

        public CardInstanceState CardState => cardState;

        public void Bind(
            CardInstanceState state,
            TabletopCoordinateConverter converter)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            BindBase(state.BaseState, converter, TabletopObjectKind.Card);
            cardState = state;
        }

        protected override void OnUnbound()
        {
            cardState = null;
        }
    }
}
