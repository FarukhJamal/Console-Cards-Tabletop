using System;
using ConsoleCards.Core.Domain;
using ConsoleCards.Presentation.Coordinates;

namespace ConsoleCards.Presentation.Views
{
    public sealed class TokenView : TabletopObjectView
    {
        private TokenState tokenState;

        public TokenState TokenState => tokenState;

        public void Bind(
            TokenState state,
            TabletopCoordinateConverter converter)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            BindBase(state.BaseState, converter, TabletopObjectKind.Token);
            tokenState = state;
        }

        protected override void OnUnbound()
        {
            tokenState = null;
        }
    }
}
