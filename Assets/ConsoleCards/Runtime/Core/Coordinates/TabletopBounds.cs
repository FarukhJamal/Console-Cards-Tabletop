using System;

namespace ConsoleCards.Core.Coordinates
{
    public readonly struct TabletopBounds : IEquatable<TabletopBounds>
    {
        public TabletopBounds(TableCoordinate minimum, TableCoordinate maximum)
        {
            ValidateFinite(minimum, nameof(minimum));
            ValidateFinite(maximum, nameof(maximum));

            if (maximum.X < minimum.X || maximum.Y < minimum.Y)
            {
                throw new ArgumentException("Tabletop bounds maximum cannot be below its minimum.", nameof(maximum));
            }

            Minimum = minimum;
            Maximum = maximum;
        }

        public TableCoordinate Minimum { get; }

        public TableCoordinate Maximum { get; }

        public TableCoordinate Center => new TableCoordinate(
            Minimum.X + ((Maximum.X - Minimum.X) * 0.5d),
            Minimum.Y + ((Maximum.Y - Minimum.Y) * 0.5d));

        public double Width => Maximum.X - Minimum.X;

        public double Height => Maximum.Y - Minimum.Y;

        public bool Contains(TableCoordinate coordinate)
        {
            ValidateFinite(coordinate, nameof(coordinate));
            return coordinate.X >= Minimum.X
                && coordinate.X <= Maximum.X
                && coordinate.Y >= Minimum.Y
                && coordinate.Y <= Maximum.Y;
        }

        public bool Contains(TabletopBounds bounds)
        {
            return Contains(bounds.Minimum) && Contains(bounds.Maximum);
        }

        public bool Equals(TabletopBounds other)
        {
            return Minimum == other.Minimum && Maximum == other.Maximum;
        }

        public override bool Equals(object obj)
        {
            return obj is TabletopBounds other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Minimum, Maximum);
        }

        public override string ToString()
        {
            return $"Minimum: {Minimum}, Maximum: {Maximum}";
        }

        public static bool operator ==(TabletopBounds left, TabletopBounds right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TabletopBounds left, TabletopBounds right)
        {
            return !left.Equals(right);
        }

        private static void ValidateFinite(TableCoordinate coordinate, string parameterName)
        {
            if (!IsFinite(coordinate.X) || !IsFinite(coordinate.Y))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Tabletop bounds coordinates must be finite.");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
