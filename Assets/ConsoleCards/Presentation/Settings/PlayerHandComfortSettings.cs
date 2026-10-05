using System;
using ConsoleCards.Core.Domain.Containers;

namespace ConsoleCards.Presentation.Settings
{
    /// <summary>
    /// Local, per-player comfort limit on how many cards this player keeps in their own hand.
    /// It is a setting, not game state: it never enters the Match state, Undo or authority, and it
    /// only guards this player's own draws and drops into their own hand. A game's own hand limit
    /// (for example a Template's maximum hand size) stays a game rule in the Template.
    /// </summary>
    public sealed class PlayerHandComfortSettings
    {
        public const int DefaultMaxHandCards = 20;
        public const int MinimumMaxHandCards = 1;

        private int maxHandCards = DefaultMaxHandCards;
        private string capLabel;
        private string reachedMessage;

        public PlayerHandComfortSettings()
        {
            RefreshText();
        }

        /// <summary>The cap; values below the minimum are clamped to it.</summary>
        public int MaxHandCards
        {
            get => maxHandCards;
            set
            {
                int clamped = Math.Max(MinimumMaxHandCards, value);
                if (clamped == maxHandCards)
                {
                    return;
                }

                maxHandCards = clamped;
                RefreshText();
            }
        }

        /// <summary>Cached "Hand cap N" label for the developer panel (no per-frame allocation).</summary>
        public string CapLabel => capLabel;

        /// <summary>Cached "Hand cap N reached" message.</summary>
        public string ReachedMessage => reachedMessage;

        /// <summary>How many more cards the hand may take before the cap; never negative.</summary>
        public int RemainingFor(ContainerState hand)
        {
            if (hand == null)
            {
                return maxHandCards;
            }

            return Math.Max(0, maxHandCards - hand.Count);
        }

        private void RefreshText()
        {
            capLabel = $"Hand cap {maxHandCards}";
            reachedMessage = $"Hand cap {maxHandCards} reached";
        }
    }
}
