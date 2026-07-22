using System;

namespace ConsoleCards.Core.Domain
{
    public sealed class PawnState
    {
        public PawnState(TabletopObjectState baseState)
        {
            if (baseState == null)
            {
                throw new ArgumentNullException(nameof(baseState));
            }

            if (baseState.Kind != TabletopObjectKind.Pawn)
            {
                throw new ArgumentException("Base state kind must be Pawn.", nameof(baseState));
            }

            BaseState = baseState;
        }

        public TabletopObjectState BaseState { get; }
    }
}
