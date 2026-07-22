using System;

namespace ConsoleCards.Core.Coordinates
{
    public readonly struct TableCoordinate : IEquatable<TableCoordinate>
    {
        public TableCoordinate(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }

        public static TableCoordinate Zero => new TableCoordinate(0, 0);

        public bool Equals(TableCoordinate other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is TableCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }

        public static bool operator ==(TableCoordinate left, TableCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TableCoordinate left, TableCoordinate right)
        {
            return !left.Equals(right);
        }
    }
}
