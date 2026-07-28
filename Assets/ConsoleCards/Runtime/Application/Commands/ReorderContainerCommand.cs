using System;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    public sealed class ReorderContainerCommand : ITabletopCommand
    {
        public ReorderContainerCommand(
            CommandContext context,
            ContainerId containerId,
            TabletopObjectId objectId,
            int fromIndex,
            int toIndex)
        {
            if (containerId.IsEmpty)
            {
                throw new ArgumentException("Container ID cannot be empty.", nameof(containerId));
            }

            if (objectId.IsEmpty)
            {
                throw new ArgumentException("Tabletop object ID cannot be empty.", nameof(objectId));
            }

            if (fromIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fromIndex), "From index cannot be below zero.");
            }

            if (toIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(toIndex), "To index cannot be below zero.");
            }

            Context = context;
            ContainerId = containerId;
            ObjectId = objectId;
            FromIndex = fromIndex;
            ToIndex = toIndex;
        }

        public CommandContext Context { get; }

        public ContainerId ContainerId { get; }

        public TabletopObjectId ObjectId { get; }

        public int FromIndex { get; }

        public int ToIndex { get; }
    }
}
