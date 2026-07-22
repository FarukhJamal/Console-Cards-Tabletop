using System;

namespace ConsoleCards.Core.Domain
{
    public sealed class CardInstanceState
    {
        public CardInstanceState(TabletopObjectState baseState, CardFace face)
        {
            if (baseState == null)
            {
                throw new ArgumentNullException(nameof(baseState));
            }

            if (baseState.Kind != TabletopObjectKind.Card)
            {
                throw new ArgumentException("Base state kind must be Card.", nameof(baseState));
            }

            BaseState = baseState;
            Face = face;
        }

        public TabletopObjectState BaseState { get; }

        public CardFace Face { get; private set; }

        public void SetFace(CardFace face)
        {
            Face = face;
        }
    }
}
