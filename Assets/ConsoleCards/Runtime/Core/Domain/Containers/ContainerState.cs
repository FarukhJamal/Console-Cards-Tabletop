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

        /// <summary>
        /// Gets the index of the top item in bottom-to-top container order.
        /// Index 0 is the bottom item; index Count - 1 is the top item.
        /// </summary>
        public int TopIndex => Count - 1;

        public bool IsFull => Capacity > 0 && Count >= Capacity;

        public IReadOnlyList<TabletopObjectId> ObjectIds => readOnlyObjectIds;

        public bool Contains(TabletopObjectId objectId)
        {
            return objectIds.Contains(objectId);
        }

        public int IndexOf(TabletopObjectId objectId)
        {
            if (objectId.IsEmpty)
            {
                throw new ArgumentException("Tabletop object ID cannot be empty.", nameof(objectId));
            }

            return objectIds.IndexOf(objectId);
        }

        /// <summary>
        /// Attempts to read the top item without changing bottom-to-top order.
        /// </summary>
        public bool TryPeekTop(out TabletopObjectId objectId)
        {
            if (objectIds.Count == 0)
            {
                objectId = TabletopObjectId.Empty;
                return false;
            }

            objectId = objectIds[TopIndex];
            return true;
        }

        /// <summary>
        /// Gets the object ID at the specified bottom-to-top order index.
        /// </summary>
        public TabletopObjectId GetObjectAt(int index)
        {
            if (index < 0 || index >= objectIds.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index must reference an existing container member.");
            }

            return objectIds[index];
        }

        /// <summary>
        /// Moves one member to its final destination index within this container.
        /// Moving index 0 to index 2 in [A, B, C] produces [B, C, A].
        /// </summary>
        public void Reorder(int fromIndex, int toIndex)
        {
            ValidateReorderIndex(fromIndex, nameof(fromIndex));
            ValidateReorderIndex(toIndex, nameof(toIndex));

            if (fromIndex == toIndex)
            {
                return;
            }

            TabletopObjectId objectId = objectIds[fromIndex];
            objectIds.RemoveAt(fromIndex);
            objectIds.Insert(toIndex, objectId);
        }

        /// <summary>
        /// Replaces bottom-to-top order without changing membership.
        /// The supplied IDs must be an exact permutation of the current members.
        /// </summary>
        public void ReplaceOrder(IReadOnlyList<TabletopObjectId> orderedObjectIds)
        {
            if (orderedObjectIds == null)
            {
                throw new ArgumentNullException(nameof(orderedObjectIds));
            }

            if (orderedObjectIds.Count != objectIds.Count)
            {
                throw new ArgumentException("Replacement order must contain the same number of object IDs.", nameof(orderedObjectIds));
            }

            HashSet<TabletopObjectId> seenObjectIds = new HashSet<TabletopObjectId>();

            foreach (TabletopObjectId objectId in orderedObjectIds)
            {
                if (objectId.IsEmpty)
                {
                    throw new ArgumentException("Replacement order cannot contain an empty tabletop object ID.", nameof(orderedObjectIds));
                }

                if (!seenObjectIds.Add(objectId))
                {
                    throw new ArgumentException("Replacement order cannot contain duplicate tabletop object IDs.", nameof(orderedObjectIds));
                }

                if (!objectIds.Contains(objectId))
                {
                    throw new ArgumentException("Replacement order cannot contain unknown tabletop object IDs.", nameof(orderedObjectIds));
                }
            }

            foreach (TabletopObjectId objectId in objectIds)
            {
                if (!seenObjectIds.Contains(objectId))
                {
                    throw new ArgumentException("Replacement order must include every current tabletop object ID.", nameof(orderedObjectIds));
                }
            }

            objectIds.Clear();
            objectIds.AddRange(orderedObjectIds);
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

        private void ValidateReorderIndex(int index, string parameterName)
        {
            if (index < 0 || index >= objectIds.Count)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Index must reference an existing container member.");
            }
        }
    }
}
