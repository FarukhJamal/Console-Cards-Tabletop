using System;
using ConsoleCards.Core.Coordinates;

namespace ConsoleCards.Presentation.Camera
{
    /// <summary>
    /// Local Presentation Camera focus bookmark containing a logical focus coordinate and zoom.
    /// </summary>
    public readonly struct TabletopCameraBookmark : IEquatable<TabletopCameraBookmark>
    {
        public TabletopCameraBookmark(string name, TableCoordinate focusCoordinate, float orthographicSize)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Bookmark name must not be empty or whitespace.", nameof(name));
            }

            if (!IsFinite(focusCoordinate.X) || !IsFinite(focusCoordinate.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(focusCoordinate));
            }

            if (!IsFinite(orthographicSize) || orthographicSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(orthographicSize));
            }

            Name = name;
            FocusCoordinate = focusCoordinate;
            OrthographicSize = orthographicSize;
        }

        public string Name { get; }

        public TableCoordinate FocusCoordinate { get; }

        public float OrthographicSize { get; }

        public bool Equals(TabletopCameraBookmark other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal)
                && FocusCoordinate.Equals(other.FocusCoordinate)
                && OrthographicSize.Equals(other.OrthographicSize);
        }

        public override bool Equals(object obj)
        {
            return obj is TabletopCameraBookmark other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, FocusCoordinate, OrthographicSize);
        }

        public override string ToString()
        {
            return $"Name: {Name}, FocusCoordinate: {FocusCoordinate}, OrthographicSize: {OrthographicSize}";
        }

        public static bool operator ==(TabletopCameraBookmark left, TabletopCameraBookmark right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TabletopCameraBookmark left, TabletopCameraBookmark right)
        {
            return !left.Equals(right);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
