using System;
using ConsoleCards.Core.Coordinates;

namespace ConsoleCards.Presentation.Camera
{
    /// <summary>
    /// Represents the inclusive logical Tabletop Space area visible through a local orthographic Camera.
    /// </summary>
    public readonly struct TabletopCameraViewBounds : IEquatable<TabletopCameraViewBounds>
    {
        public TabletopCameraViewBounds(double minimumX, double maximumX, double minimumY, double maximumY)
        {
            ValidateFinite(minimumX, nameof(minimumX));
            ValidateFinite(maximumX, nameof(maximumX));
            ValidateFinite(minimumY, nameof(minimumY));
            ValidateFinite(maximumY, nameof(maximumY));

            if (minimumX > maximumX)
            {
                throw new ArgumentException("Minimum X must be less than or equal to maximum X.", nameof(minimumX));
            }

            if (minimumY > maximumY)
            {
                throw new ArgumentException("Minimum Y must be less than or equal to maximum Y.", nameof(minimumY));
            }

            MinimumX = minimumX;
            MaximumX = maximumX;
            MinimumY = minimumY;
            MaximumY = maximumY;
        }

        public double MinimumX { get; }

        public double MaximumX { get; }

        public double MinimumY { get; }

        public double MaximumY { get; }

        public double Width => MaximumX - MinimumX;

        public double Height => MaximumY - MinimumY;

        public TableCoordinate Center => new TableCoordinate(Midpoint(MinimumX, MaximumX), Midpoint(MinimumY, MaximumY));

        public bool Contains(TableCoordinate coordinate)
        {
            ValidateFinite(coordinate);

            return coordinate.X >= MinimumX
                && coordinate.X <= MaximumX
                && coordinate.Y >= MinimumY
                && coordinate.Y <= MaximumY;
        }

        public bool Intersects(TableCoordinate center, double halfWidth, double halfHeight)
        {
            ValidateFinite(center);
            ValidateNonNegativeFinite(halfWidth, nameof(halfWidth));
            ValidateNonNegativeFinite(halfHeight, nameof(halfHeight));

            double objectMinimumX = center.X - halfWidth;
            double objectMaximumX = center.X + halfWidth;
            double objectMinimumY = center.Y - halfHeight;
            double objectMaximumY = center.Y + halfHeight;

            if (!IsFinite(objectMinimumX)
                || !IsFinite(objectMaximumX)
                || !IsFinite(objectMinimumY)
                || !IsFinite(objectMaximumY))
            {
                throw new OverflowException("Calculated object bounds are not finite.");
            }

            return objectMaximumX >= MinimumX
                && objectMinimumX <= MaximumX
                && objectMaximumY >= MinimumY
                && objectMinimumY <= MaximumY;
        }

        public bool Equals(TabletopCameraViewBounds other)
        {
            return MinimumX.Equals(other.MinimumX)
                && MaximumX.Equals(other.MaximumX)
                && MinimumY.Equals(other.MinimumY)
                && MaximumY.Equals(other.MaximumY);
        }

        public override bool Equals(object obj)
        {
            return obj is TabletopCameraViewBounds other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MinimumX, MaximumX, MinimumY, MaximumY);
        }

        public override string ToString()
        {
            return $"X: [{MinimumX}, {MaximumX}], Y: [{MinimumY}, {MaximumY}]";
        }

        public static bool operator ==(TabletopCameraViewBounds left, TabletopCameraViewBounds right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TabletopCameraViewBounds left, TabletopCameraViewBounds right)
        {
            return !left.Equals(right);
        }

        private static void ValidateFinite(TableCoordinate coordinate)
        {
            if (!IsFinite(coordinate.X) || !IsFinite(coordinate.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(coordinate));
            }
        }

        private static void ValidateNonNegativeFinite(double value, string parameterName)
        {
            if (!IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static double Midpoint(double minimum, double maximum)
        {
            return (minimum * 0.5) + (maximum * 0.5);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
