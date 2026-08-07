using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.PlayAreas
{
    public sealed class PlayAreaState
    {
        public PlayAreaState(
            PlayAreaId id,
            TabletopBounds bounds,
            TabletopBounds focusRegion)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("Play Area ID cannot be empty.", nameof(id));
            }

            if (!bounds.Contains(focusRegion))
            {
                throw new ArgumentException("Play Area focus region must be contained by its bounds.", nameof(focusRegion));
            }

            Id = id;
            Bounds = bounds;
            FocusRegion = focusRegion;
        }

        public PlayAreaId Id { get; }

        public TabletopBounds Bounds { get; }

        public TabletopBounds FocusRegion { get; }

        public TableCoordinate FocusCoordinate => FocusRegion.Center;
    }
}
