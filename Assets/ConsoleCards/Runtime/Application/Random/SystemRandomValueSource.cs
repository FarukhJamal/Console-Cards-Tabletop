using System;
using ConsoleCards.Core.Randomness;

namespace ConsoleCards.Application.Random
{
    /// <summary>
    /// Local runtime random source backed by System.Random. Seeded construction supports deterministic callers.
    /// </summary>
    public sealed class SystemRandomValueSource : IRandomValueSource
    {
        private System.Random random;

        public SystemRandomValueSource()
            : this(new System.Random())
        {
        }

        public SystemRandomValueSource(int seed)
            : this(new System.Random(seed))
        {
        }

        private SystemRandomValueSource(System.Random random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// MP-2: starts the numbers again from a seed, so a command run on several computers draws the same numbers
        /// there (the seed comes from the command's ID). Not used offline. The new sequence is made only when a number
        /// is drawn, so commands that draw none (most of them) cost nothing.
        /// </summary>
        public void Reseed(int seed)
        {
            pendingSeed = seed;
            hasPendingSeed = true;
        }

        private int pendingSeed;
        private bool hasPendingSeed;

        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            if (minimumInclusive >= maximumExclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumExclusive),
                    "The exclusive maximum must be greater than the inclusive minimum.");
            }

            if (hasPendingSeed)
            {
                random = new System.Random(pendingSeed);
                hasPendingSeed = false;
            }

            return random.Next(minimumInclusive, maximumExclusive);
        }
    }
}
