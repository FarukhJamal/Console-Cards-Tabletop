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
        [SerializeField] private MeshFilter physicalBodyMesh;
        [SerializeField] private PhysicalDieDefinition[] physicalDefinitions;
        private PhysicalDieDefinition physicalDefinition;
        public bool HasPhysicalDefinition => physicalDefinition != null;

        public bool TryResolvePhysicalValue(out int value)
        {
            value = 0;
            Quaternion orientation = PhysicalObject != null && PhysicalObject.Body != null
                ? PhysicalObject.Body.rotation : transform.rotation;
            return physicalDefinition != null && physicalDefinition.TryRead(orientation, out value);
        }

        public void ConfigurePhysicalShape(int sideCount)
        {
            if (physicalBodyMesh == null || physicalDefinition != null) return;
            foreach (PhysicalDieDefinition definition in physicalDefinitions)
                if (definition != null && definition.SideCount == sideCount) physicalDefinition = definition;
            if (physicalDefinition == null) throw new InvalidOperationException("No authored physical Die face mapping.");
            physicalDefinition.Build(transform, physicalBodyMesh, resultLabel);
        }

        private void OnDestroy()
        {
            if (physicalDefinition != null && physicalBodyMesh != null) Destroy(physicalBodyMesh.sharedMesh);
        }

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
            ConfigurePhysicalShape(state.SideCount);
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
                resultLabel.text = dieState.BaseState.PhysicalState?.Mode == PhysicalObjectMode.SleepingUnresolved
                    ? $"d{dieState.SideCount}\ncocked" : $"d{dieState.SideCount}\n{dieState.CurrentValue}";
            }
        }

        protected override void OnUnbound()
        {
            dieState = null;
        }
    }
}
