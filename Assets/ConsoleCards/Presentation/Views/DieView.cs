using System;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Views
{
    /// <summary>Projects one authoritative generic Die as a physical tabletop object.</summary>
    public sealed class DieView : TabletopObjectView
    {
        [SerializeField] private TextMesh resultLabel;

        private DieState dieState;

        public DieState DieState => dieState;

        public TextMesh ResultLabel => resultLabel;

        public void Bind(DieState state, TabletopCoordinateConverter converter)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (resultLabel == null)
            {
                throw new InvalidOperationException("DieView requires an authored result label.");
            }

            dieState = state;
            try
            {
                BindBase(state.BaseState, converter, TabletopObjectKind.Die);
            }
            catch
            {
                dieState = null;
                throw;
            }
        }

        protected override void OnAcceptedStateApplied()
        {
            if (dieState != null && resultLabel != null)
            {
                resultLabel.text = $"d{dieState.SideCount}\n{dieState.CurrentValue}";
            }
        }

        protected override void OnUnbound()
        {
            dieState = null;
        }
    }
}
