using System;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// Stable one-based coordinate of a Floor Card. Active Grid dimensions belong to the
    /// Trap Floor Template, while coordinate-generation strategies validate against its mapping.
    /// </summary>
    public readonly struct TrapFloorCoordinate : IEquatable<TrapFloorCoordinate>
    {
        public const int MinimumAxisValue = 1;

        public TrapFloorCoordinate(int x, int y)
        {
            ValidateAxis(x, nameof(x));
            ValidateAxis(y, nameof(y));
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public bool Equals(TrapFloorCoordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is TrapFloorCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public override string ToString()
        {
            return $"({X},{Y})";
        }

        public static bool operator ==(TrapFloorCoordinate left, TrapFloorCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TrapFloorCoordinate left, TrapFloorCoordinate right)
        {
            return !left.Equals(right);
        }

        private static void ValidateAxis(int value, string parameterName)
        {
            if (value < MinimumAxisValue)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Trap Floor coordinates must be at least {MinimumAxisValue}.");
            }
        }
    }
}
