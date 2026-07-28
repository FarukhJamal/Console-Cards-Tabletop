using System;

namespace ConsoleCards.Core.Domain.Containers
{
    /// <summary>
    /// Describes the first bottom-to-top index moved from a lower Stack into a new upper Stack.
    /// For [A, B, C, D], FirstMovedIndex 2 leaves [A, B] and moves [C, D].
    /// </summary>
    public readonly struct StackSplitSpecification : IEquatable<StackSplitSpecification>
    {
        public StackSplitSpecification(int firstMovedIndex)
        {
            if (firstMovedIndex < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(firstMovedIndex), "First moved index must be at least one.");
            }

            FirstMovedIndex = firstMovedIndex;
        }

        public int FirstMovedIndex { get; }

        public int GetMovedCount(int sourceCount)
        {
            if (sourceCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceCount), "Source count must be at least two.");
            }

            if (FirstMovedIndex >= sourceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceCount), "First moved index must be less than source count.");
            }

            return sourceCount - FirstMovedIndex;
        }

        public bool Equals(StackSplitSpecification other)
        {
            return FirstMovedIndex == other.FirstMovedIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is StackSplitSpecification other && Equals(other);
        }

        public override int GetHashCode()
        {
            return FirstMovedIndex;
        }

        public override string ToString()
        {
            return $"FirstMovedIndex: {FirstMovedIndex}";
        }

        public static bool operator ==(StackSplitSpecification left, StackSplitSpecification right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StackSplitSpecification left, StackSplitSpecification right)
        {
            return !left.Equals(right);
        }
    }
}
