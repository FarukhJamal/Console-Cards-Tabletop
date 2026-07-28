using System;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    public sealed class ShuffleDeckCommand : ITabletopCommand
    {
        public ShuffleDeckCommand(
            CommandContext context,
            ContainerId deckContainerId,
            int seed)
        {
            if (deckContainerId.IsEmpty)
            {
                throw new ArgumentException("Deck Container ID cannot be empty.", nameof(deckContainerId));
            }

            Context = context;
            DeckContainerId = deckContainerId;
            Seed = seed;
        }

        public CommandContext Context { get; }

        public ContainerId DeckContainerId { get; }

        public int Seed { get; }
    }
}
