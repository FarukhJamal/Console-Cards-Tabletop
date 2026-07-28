using System;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    public sealed class MergeStacksCommand : ITabletopCommand
    {
        public MergeStacksCommand(
            CommandContext context,
            ContainerId sourceStackContainerId,
            ContainerId destinationStackContainerId)
        {
            if (sourceStackContainerId.IsEmpty)
            {
                throw new ArgumentException("Source Stack Container ID cannot be empty.", nameof(sourceStackContainerId));
            }

            if (destinationStackContainerId.IsEmpty)
            {
                throw new ArgumentException("Destination Stack Container ID cannot be empty.", nameof(destinationStackContainerId));
            }

            if (sourceStackContainerId == destinationStackContainerId)
            {
                throw new ArgumentException("Source and destination Stack Container IDs must be different.", nameof(destinationStackContainerId));
            }

            Context = context;
            SourceStackContainerId = sourceStackContainerId;
            DestinationStackContainerId = destinationStackContainerId;
        }

        public CommandContext Context { get; }

        public ContainerId SourceStackContainerId { get; }

        public ContainerId DestinationStackContainerId { get; }
    }
}
