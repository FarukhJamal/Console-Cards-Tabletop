using System;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    public sealed class ShuffleDeckCommand : ITabletopCommand
    {
        public ShuffleDeckCommand(
            CommandContext context,
            ContainerId deckContainerId)
            : this(context, deckContainerId, null)
        {
        }

        public ShuffleDeckCommand(
            CommandContext context,
            ContainerId deckContainerId,
            int seed)
            : this(context, deckContainerId, (int?)seed)
        {
        }

        private ShuffleDeckCommand(
            CommandContext context,
            ContainerId deckContainerId,
            int? seed)
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

        /// <summary>
        /// Gets the deterministic seed supplied by legacy/replay callers. Normal Player-initiated
        /// shuffles omit this value and obtain decisions from the injected authoritative random source.
        /// </summary>
        public int? Seed { get; }
    }
}
