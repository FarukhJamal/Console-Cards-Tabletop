using System;
using System.Runtime.CompilerServices;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

[assembly: InternalsVisibleTo("ConsoleCards.Tests.EditMode.Presentation")]

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// Projects local pointer positions onto a mathematical tabletop plane.
    /// </summary>
    public sealed class TabletopPointerProjector
    {
        public TabletopPointerProjector(
            UnityCamera targetCamera,
            TabletopCoordinateConverter coordinateConverter,
            float tabletopHeight = 0f)
        {
            if (targetCamera == null)
            {
                throw new ArgumentNullException(nameof(targetCamera));
            }

            if (!targetCamera.orthographic)
            {
                throw new ArgumentException("TabletopPointerProjector requires an orthographic Camera.", nameof(targetCamera));
            }

            if (coordinateConverter == null)
            {
                throw new ArgumentNullException(nameof(coordinateConverter));
            }

            if (!IsFinite(tabletopHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(tabletopHeight));
            }

            TargetCamera = targetCamera;
            CoordinateConverter = coordinateConverter;
            TabletopHeight = tabletopHeight;
        }

        public UnityCamera TargetCamera { get; }

        public TabletopCoordinateConverter CoordinateConverter { get; }

        public float TabletopHeight { get; }

        public bool TryProjectScreenPoint(Vector2 screenPosition, out TableCoordinate coordinate)
        {
            ValidateFinite(screenPosition);

            Ray ray = TargetCamera.ScreenPointToRay(screenPosition);
            return TryProjectRay(ray, out coordinate);
        }

        internal bool TryProjectRay(Ray ray, out TableCoordinate coordinate)
        {
            ValidateFinite(ray.origin, nameof(ray));
            ValidateFinite(ray.direction, nameof(ray));
            ValidateDirection(ray.direction);

            float denominator = ray.direction.y;
            if (denominator == 0f)
            {
                coordinate = TableCoordinate.Zero;
                return false;
            }

            float distance = (TabletopHeight - ray.origin.y) / denominator;
            if (distance < 0f)
            {
                coordinate = TableCoordinate.Zero;
                return false;
            }

            Vector3 intersection = ray.origin + ray.direction * distance;
            ValidateFinite(intersection, nameof(ray));

            coordinate = CoordinateConverter.ToTableCoordinate(intersection);
            return true;
        }

        private static void ValidateFinite(Vector2 value)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static void ValidateFinite(Vector3 value, string parameterName)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateDirection(Vector3 direction)
        {
            double squaredMagnitude =
                (double)direction.x * direction.x
                + (double)direction.y * direction.y
                + (double)direction.z * direction.z;

            if (squaredMagnitude == 0.0 || !IsFinite(squaredMagnitude))
            {
                throw new ArgumentOutOfRangeException(nameof(direction));
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
