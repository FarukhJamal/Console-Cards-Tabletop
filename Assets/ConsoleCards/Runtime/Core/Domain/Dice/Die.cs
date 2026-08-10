using System;
using ConsoleCards.Core.Randomness;

namespace ConsoleCards.Core.Domain.Dice
{
    /// <summary>
    /// Immutable definition of one numbered die. Rolling produces a validated value object.
    /// </summary>
    public sealed class Die
    {
        public Die(int sideCount)
        {
            if (sideCount < 2 || sideCount == int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sideCount),
                    "A die must have between 2 and Int32.MaxValue - 1 sides.");
            }

            SideCount = sideCount;
        }

        public int SideCount { get; }

        public DieRoll Roll(IRandomValueSource randomValueSource)
        {
            if (randomValueSource == null)
            {
                throw new ArgumentNullException(nameof(randomValueSource));
            }

            int value = randomValueSource.NextInt(1, SideCount + 1);
            return new DieRoll(SideCount, value);
        }
    }

    /// <summary>
    /// One validated result produced by a die with a known side count.
    /// </summary>
    public readonly struct DieRoll : IEquatable<DieRoll>
    {
        public DieRoll(int sideCount, int value)
        {
            if (sideCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(sideCount), "A die must have at least 2 sides.");
            }

            if (value < 1 || value > sideCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "A die result must be between 1 and its side count.");
            }

            SideCount = sideCount;
            Value = value;
        }

        public int SideCount { get; }

        public int Value { get; }

        public bool Equals(DieRoll other)
        {
            return SideCount == other.SideCount && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is DieRoll other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SideCount, Value);
        }

        public override string ToString()
        {
            return $"d{SideCount}: {Value}";
        }

        public static bool operator ==(DieRoll left, DieRoll right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DieRoll left, DieRoll right)
        {
            return !left.Equals(right);
        }
    }
}
