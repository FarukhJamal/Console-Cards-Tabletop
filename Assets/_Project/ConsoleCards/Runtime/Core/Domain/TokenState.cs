using System;

namespace ConsoleCards.Core.Domain
{
    public sealed class TokenState
    {
        public TokenState(TabletopObjectState baseState)
        {
            if (baseState == null)
            {
                throw new ArgumentNullException(nameof(baseState));
            }

            if (baseState.Kind != TabletopObjectKind.Token)
            {
                throw new ArgumentException("Base state kind must be Token.", nameof(baseState));
            }

            BaseState = baseState;
        }

        public TabletopObjectState BaseState { get; }
    }
}
