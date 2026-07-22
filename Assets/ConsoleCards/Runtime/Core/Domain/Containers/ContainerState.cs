using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.Containers
{
    public sealed class ContainerState
    {
        private readonly List<TabletopObjectId> objectIds;
        private readonly ReadOnlyCollection<TabletopObjectId> readOnlyObjectIds;

        public ContainerState(
            ContainerId id,
            ContainerKind kind,
            SeatId ownerSeatId,
            ObjectVisibility visibility,
            int capacity)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("Container ID cannot be empty.", nameof(id));
            }

            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity cannot be below zero.");
            }

            Id = id;
            Kind = kind;
            OwnerSeatId = ownerSeatId;
            Visibility = visibility;
            Capacity = capacity;
            objectIds = new List<TabletopObjectId>();
            readOnlyObjectIds = objectIds.AsReadOnly();
        }

        public ContainerId Id { get; }

        public ContainerKind Kind { get; }

        public SeatId OwnerSeatId { get; }

        public ObjectVisibility Visibility { get; }

        public int Capacity { get; }

        public int Count => objectIds.Count;

        public bool IsFull => Capacity > 0 && Count >= Capacity;

        public IReadOnlyList<TabletopObjectId> ObjectIds => readOnlyObjectIds;

        public bool Contains(TabletopObjectId objectId)
        {
            return objectIds.Contains(objectId);
        }

        public int IndexOf(TabletopObjectId objectId)
        {
            return objectIds.IndexOf(objectId);
        }

        internal void InsertObject(TabletopObjectId objectId, int index)
        {
            if (objectId.IsEmpty)
            {
                throw new ArgumentException("Tabletop object ID cannot be empty.", nameof(objectId));
            }

            if (Contains(objectId))
            {
                throw new ArgumentException("Container already contains the tabletop object ID.", nameof(objectId));
            }

            if (IsFull)
            {
                throw new InvalidOperationException("Container is full.");
            }

            objectIds.Insert(index, objectId);
        }

        internal void RemoveObject(TabletopObjectId objectId)
        {
            if (!objectIds.Remove(objectId))
            {
                throw new ArgumentException("Container does not contain the tabletop object ID.", nameof(objectId));
            }
        }
    }
}
