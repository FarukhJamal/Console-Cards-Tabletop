using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.Consoles
{
    public sealed class ConsoleState
    {
        private readonly List<ContainerId> slotContainerIds;
        private readonly ReadOnlyCollection<ContainerId> readOnlySlotContainerIds;

        public ConsoleState(SeatId ownerSeatId, IEnumerable<ContainerId> slotContainerIds)
        {
            if (ownerSeatId.IsEmpty)
            {
                throw new ArgumentException("Owner Seat ID cannot be empty.", nameof(ownerSeatId));
            }

            if (slotContainerIds == null)
            {
                throw new ArgumentNullException(nameof(slotContainerIds));
            }

            OwnerSeatId = ownerSeatId;
            this.slotContainerIds = CopySlotContainerIds(slotContainerIds);
            readOnlySlotContainerIds = this.slotContainerIds.AsReadOnly();
        }

        public SeatId OwnerSeatId { get; }

        public int SlotCount => slotContainerIds.Count;

        public IReadOnlyList<ContainerId> SlotContainerIds => readOnlySlotContainerIds;

        public bool ContainsSlot(ContainerId containerId)
        {
            return slotContainerIds.Contains(containerId);
        }

        private static List<ContainerId> CopySlotContainerIds(IEnumerable<ContainerId> slotContainerIds)
        {
            List<ContainerId> copiedSlotContainerIds = new List<ContainerId>();
            HashSet<ContainerId> seenSlotContainerIds = new HashSet<ContainerId>();

            foreach (ContainerId slotContainerId in slotContainerIds)
            {
                if (slotContainerId.IsEmpty)
                {
                    throw new ArgumentException("Slot Container IDs cannot be empty.", nameof(slotContainerIds));
                }

                if (!seenSlotContainerIds.Add(slotContainerId))
                {
                    throw new ArgumentException("Slot Container IDs cannot contain duplicates.", nameof(slotContainerIds));
                }

                copiedSlotContainerIds.Add(slotContainerId);
            }

            return copiedSlotContainerIds;
        }
    }
}
