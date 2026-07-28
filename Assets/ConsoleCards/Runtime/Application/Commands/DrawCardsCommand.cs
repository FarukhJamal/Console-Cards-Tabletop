using System;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    public sealed class DrawCardsCommand : ITabletopCommand
    {
        public DrawCardsCommand(
            CommandContext context,
            ContainerId sourceDeckContainerId,
            ContainerId destinationContainerId,
            int count)
        {
            if (sourceDeckContainerId.IsEmpty)
            {
                throw new ArgumentException("Source Deck Container ID cannot be empty.", nameof(sourceDeckContainerId));
            }

            if (destinationContainerId.IsEmpty)
            {
                throw new ArgumentException("Destination Container ID cannot be empty.", nameof(destinationContainerId));
            }

            if (sourceDeckContainerId == destinationContainerId)
            {
                throw new ArgumentException("Source Deck and destination Container IDs must be different.", nameof(destinationContainerId));
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Draw count must be greater than zero.");
            }

            Context = context;
            SourceDeckContainerId = sourceDeckContainerId;
            DestinationContainerId = destinationContainerId;
            Count = count;
        }

        public CommandContext Context { get; }

        public ContainerId SourceDeckContainerId { get; }

        public ContainerId DestinationContainerId { get; }

        public int Count { get; }
    }
}
