using System;

namespace ConsoleCards.Application.Random
{
    /// <summary>
    /// Stable xorshift32 generator for deterministic Fisher-Yates shuffles.
    /// The signed seed bits are reinterpreted as uint; a zero state is replaced with 0x6D2B79F5.
    /// Range reduction uses stable uint modulo. The small modulo bias is accepted for the M3 MVP.
    /// </summary>
    internal struct StableShuffleRandom
    {
        private const uint ZeroSeedReplacement = 0x6D2B79F5u;

        private uint state;

        public StableShuffleRandom(int seed)
        {
            state = unchecked((uint)seed);

            if (state == 0u)
            {
                state = ZeroSeedReplacement;
            }
        }

        public int NextInclusiveUpperExclusive(int upperExclusive)
        {
            if (upperExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(upperExclusive), "Upper exclusive bound must be greater than zero.");
            }

            return (int)(NextUInt32() % (uint)upperExclusive);
        }

        private uint NextUInt32()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }
    }
}
