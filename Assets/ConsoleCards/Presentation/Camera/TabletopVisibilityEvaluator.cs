using System;
using ConsoleCards.Core.Coordinates;

namespace ConsoleCards.Presentation.Camera
{
    /// <summary>
    /// Calculates local orthographic Camera visibility bounds in logical Tabletop Space.
    /// </summary>
    /// <remarks>
    /// This is a Presentation optimization aid only. It does not decide object existence,
    /// Match State authority, network replication, privacy, or Game Rules.
    /// </remarks>
    public sealed class TabletopVisibilityEvaluator
    {
        public TabletopVisibilityEvaluator(float worldUnitsPerTableUnit, double cullingMarginTableUnits)
        {
            if (!IsFinite(worldUnitsPerTableUnit) || worldUnitsPerTableUnit <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(worldUnitsPerTableUnit));
            }

            if (!IsFinite(cullingMarginTableUnits) || cullingMarginTableUnits < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(cullingMarginTableUnits));
            }

            WorldUnitsPerTableUnit = worldUnitsPerTableUnit;
            CullingMarginTableUnits = cullingMarginTableUnits;
        }

        public float WorldUnitsPerTableUnit { get; }

        public double CullingMarginTableUnits { get; }

        public TabletopCameraViewBounds CalculateViewBounds(TabletopCameraState cameraState, float cameraAspect)
        {
            if (cameraState == null)
            {
                throw new ArgumentNullException(nameof(cameraState));
            }

            if (!IsFinite(cameraAspect) || cameraAspect <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cameraAspect));
            }

            double verticalHalfExtentWithoutMargin = (double)cameraState.OrthographicSize / WorldUnitsPerTableUnit;
            double horizontalHalfExtentWithoutMargin = verticalHalfExtentWithoutMargin * cameraAspect;
            double verticalHalfExtent = verticalHalfExtentWithoutMargin + CullingMarginTableUnits;
            double horizontalHalfExtent = horizontalHalfExtentWithoutMargin + CullingMarginTableUnits;

            if (!IsFinite(verticalHalfExtent) || !IsFinite(horizontalHalfExtent))
            {
                throw new OverflowException("Calculated view half-extents are not finite.");
            }

            TableCoordinate focus = cameraState.FocusCoordinate;
            double minimumX = focus.X - horizontalHalfExtent;
            double maximumX = focus.X + horizontalHalfExtent;
            double minimumY = focus.Y - verticalHalfExtent;
            double maximumY = focus.Y + verticalHalfExtent;

            if (!IsFinite(minimumX) || !IsFinite(maximumX) || !IsFinite(minimumY) || !IsFinite(maximumY))
            {
                throw new OverflowException("Calculated view bounds are not finite.");
            }

            return new TabletopCameraViewBounds(minimumX, maximumX, minimumY, maximumY);
        }

        public bool IsPointVisible(
            TableCoordinate coordinate,
            TabletopCameraState cameraState,
            float cameraAspect)
        {
            TabletopCameraViewBounds bounds = CalculateViewBounds(cameraState, cameraAspect);

            return bounds.Contains(coordinate);
        }

        public bool IsBoundsVisible(
            TableCoordinate center,
            double halfWidth,
            double halfHeight,
            TabletopCameraState cameraState,
            float cameraAspect)
        {
            TabletopCameraViewBounds bounds = CalculateViewBounds(cameraState, cameraAspect);

            return bounds.Intersects(center, halfWidth, halfHeight);
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
