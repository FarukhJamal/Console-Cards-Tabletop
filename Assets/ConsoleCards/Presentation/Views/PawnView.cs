using System;
using ConsoleCards.Core.Domain;
using ConsoleCards.Presentation.Coordinates;

namespace ConsoleCards.Presentation.Views
{
    public sealed class PawnView : TabletopObjectView
    {
        private PawnState pawnState;

        public PawnState PawnState => pawnState;

        public void Bind(
            PawnState state,
            TabletopCoordinateConverter converter)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            BindBase(state.BaseState, converter, TabletopObjectKind.Pawn);
            pawnState = state;
        }

        protected override void OnUnbound()
        {
            pawnState = null;
        }
    }
}
