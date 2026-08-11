using System;
using ConsoleCards.Core.Domain;

namespace ConsoleCards.Core.Domain.Dice
{
    /// <summary>
    /// Authoritative Runtime State for one physical tabletop Die Object Instance.
    /// </summary>
    public sealed class DieState
    {
        public DieState(TabletopObjectState baseState, int sideCount, int currentValue)
        {
            if (baseState == null)
            {
                throw new ArgumentNullException(nameof(baseState));
            }

            if (baseState.Kind != TabletopObjectKind.Die)
            {
                throw new ArgumentException("Base state kind must be Die.", nameof(baseState));
            }

            DieRoll initialRoll = new DieRoll(sideCount, currentValue);
            BaseState = baseState;
            SideCount = initialRoll.SideCount;
            CurrentValue = initialRoll.Value;
        }

        public TabletopObjectState BaseState { get; }

        public int SideCount { get; }

        public int CurrentValue { get; private set; }

        public void SetAcceptedRoll(DieRoll roll)
        {
            if (roll.SideCount != SideCount)
            {
                throw new ArgumentException("Accepted Die result must use this Die's side count.", nameof(roll));
            }

            CurrentValue = roll.Value;
        }
    }
}
