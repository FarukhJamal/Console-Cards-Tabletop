using System;
using ConsoleCards.Core.Randomness;

namespace ConsoleCards.Application.Random
{
    /// <summary>
    /// Local runtime random source backed by System.Random. Seeded construction supports deterministic callers.
    /// </summary>
    public sealed class SystemRandomValueSource : IRandomValueSource
    {
        private readonly System.Random random;

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

        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            if (minimumInclusive >= maximumExclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumExclusive),
                    "The exclusive maximum must be greater than the inclusive minimum.");
            }

            return random.Next(minimumInclusive, maximumExclusive);
        }
    }
}
