using System;
using ConsoleCards.Core.Coordinates;

namespace ConsoleCards.Presentation.Camera
{
    /// <summary>
    /// Stores local Presentation Camera focus and zoom state for the Tabletop.
    /// </summary>
    public sealed class TabletopCameraState
    {
        public TabletopCameraState(
            TableCoordinate focusCoordinate,
            float orthographicSize,
            float minimumOrthographicSize,
            float maximumOrthographicSize)
        {
            ValidateFinite(focusCoordinate);

            if (!IsFinite(minimumOrthographicSize) || minimumOrthographicSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumOrthographicSize));
            }

            if (!IsFinite(maximumOrthographicSize) || maximumOrthographicSize < minimumOrthographicSize)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumOrthographicSize));
            }

            ValidateFinite(orthographicSize, nameof(orthographicSize));

            FocusCoordinate = focusCoordinate;
            MinimumOrthographicSize = minimumOrthographicSize;
            MaximumOrthographicSize = maximumOrthographicSize;
            OrthographicSize = Clamp(orthographicSize);
        }

        public TableCoordinate FocusCoordinate { get; private set; }

        public float OrthographicSize { get; private set; }

        public float MinimumOrthographicSize { get; }

        public float MaximumOrthographicSize { get; }

        public void Pan(double deltaX, double deltaY)
        {
            ValidateFinite(deltaX, nameof(deltaX));
            ValidateFinite(deltaY, nameof(deltaY));

            double nextX = FocusCoordinate.X + deltaX;
            double nextY = FocusCoordinate.Y + deltaY;

            if (!IsFinite(nextX) || !IsFinite(nextY))
            {
                throw new OverflowException("Panning produced a non-finite table coordinate.");
            }

            FocusCoordinate = new TableCoordinate(nextX, nextY);
        }

        public void SetFocus(TableCoordinate coordinate)
        {
            ValidateFinite(coordinate);

            FocusCoordinate = coordinate;
        }

        public void SetFocus(TableCoordinate coordinate, float orthographicSize)
        {
            ValidateFinite(coordinate);
            ValidateFinite(orthographicSize, nameof(orthographicSize));

            FocusCoordinate = coordinate;
            OrthographicSize = Clamp(orthographicSize);
        }

        public void Zoom(float delta)
        {
            ValidateFinite(delta, nameof(delta));

            OrthographicSize = Clamp(OrthographicSize + delta);
        }

        public void SetOrthographicSize(float size)
        {
            ValidateFinite(size, nameof(size));

            OrthographicSize = Clamp(size);
        }

        private float Clamp(float value)
        {
            if (value < MinimumOrthographicSize)
            {
                return MinimumOrthographicSize;
            }

            if (value > MaximumOrthographicSize)
            {
                return MaximumOrthographicSize;
            }

            return value;
        }

        private static void ValidateFinite(TableCoordinate coordinate)
        {
            if (!IsFinite(coordinate.X) || !IsFinite(coordinate.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(coordinate));
            }
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (!IsFinite(value))
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
