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
            int? seed,
            bool cutOnly = false)
        {
            if (deckContainerId.IsEmpty)
            {
                throw new ArgumentException("Deck Container ID cannot be empty.", nameof(deckContainerId));
            }

            Context = context;
            DeckContainerId = deckContainerId;
            Seed = seed;
            CutOnly = cutOnly;
        }

        /// <summary>
        /// SHUF-1: one cut instead of a shuffle. The cut point (at least one card each side) comes from the same
        /// authoritative random source; the top part goes under the rest.
        /// </summary>
        public static ShuffleDeckCommand Cut(CommandContext context, ContainerId deckContainerId) =>
            new ShuffleDeckCommand(context, deckContainerId, null, true);

        public CommandContext Context { get; }

        public ContainerId DeckContainerId { get; }

        /// <summary>
        /// Gets the deterministic seed supplied by legacy/replay callers. Normal Player-initiated
        /// shuffles omit this value and obtain decisions from the injected authoritative random source.
        /// </summary>
        public int? Seed { get; }

        /// <summary>True for a cut (<see cref="Cut"/>) instead of a full shuffle.</summary>
        public bool CutOnly { get; }
    }
}
