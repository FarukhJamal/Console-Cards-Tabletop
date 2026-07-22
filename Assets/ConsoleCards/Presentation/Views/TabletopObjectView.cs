using System;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Views
{
    /// <summary>
    /// Base Unity View that projects an accepted tabletop object pose from Runtime State.
    /// </summary>
    public abstract class TabletopObjectView : MonoBehaviour
    {
        private TabletopObjectState boundState;
        private TabletopCoordinateConverter coordinateConverter;
        private bool isBound;

        public bool IsBound => isBound;

        public TabletopObjectId ObjectId => isBound ? boundState.Id : TabletopObjectId.Empty;

        public TabletopObjectState BoundState => isBound ? boundState : null;

        protected void BindBase(
            TabletopObjectState state,
            TabletopCoordinateConverter converter,
            TabletopObjectKind expectedKind)
        {
            ValidateBinding(state, converter, expectedKind);
            converter.ToWorldPosition(state.Pose);
            converter.ToWorldRotation(state.Pose);

            boundState = state;
            coordinateConverter = converter;
            isBound = true;

            ApplyAcceptedState();
        }

        public void ApplyAcceptedState()
        {
            EnsureBound();

            transform.SetPositionAndRotation(
                coordinateConverter.ToWorldPosition(boundState.Pose),
                coordinateConverter.ToWorldRotation(boundState.Pose));
        }

        public virtual void Unbind()
        {
            boundState = null;
            coordinateConverter = null;
            isBound = false;

            OnUnbound();
        }

        protected virtual void OnUnbound()
        {
        }

        private static void ValidateBinding(
            TabletopObjectState state,
            TabletopCoordinateConverter converter,
            TabletopObjectKind expectedKind)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            if (state.Kind != expectedKind)
            {
                throw new ArgumentException($"Tabletop object kind must be {expectedKind}.", nameof(state));
            }

            if (state.Id.IsEmpty)
            {
                throw new ArgumentException("Tabletop object ID cannot be empty.", nameof(state));
            }

            if (state.DefinitionId.IsEmpty)
            {
                throw new ArgumentException("Object definition ID cannot be empty.", nameof(state));
            }
        }

        private void EnsureBound()
        {
            if (!isBound)
            {
                throw new InvalidOperationException("TabletopObjectView is not bound to Runtime State.");
            }
        }
    }
}
