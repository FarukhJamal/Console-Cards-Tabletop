using System;
using ConsoleCards.Core.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Coordinates
{
    /// <summary>
    /// Converts logical Tabletop Space coordinates into Unity render-space positions.
    /// </summary>
    public sealed class TabletopCoordinateConverter
    {
        private readonly Vector3 worldOrigin;
        private readonly Vector3 worldXAxisPerTableUnit;
        private readonly Vector3 worldYAxisPerTableUnit;
        private readonly Vector3 worldUp;
        private readonly Quaternion worldRotation;

        public TabletopCoordinateConverter(
            float worldUnitsPerTableUnit,
            float baseHeight,
            float layerHeight,
            float localOrderHeight)
            : this(
                Vector3.zero,
                Vector3.right * worldUnitsPerTableUnit,
                Vector3.forward * worldUnitsPerTableUnit,
                Vector3.up,
                baseHeight,
                layerHeight,
                localOrderHeight)
        {
        }

        /// <summary>
        /// Creates a converter whose logical X/Y axes are supplied by an authored physical Table frame.
        /// Axis vectors include the current Table scale and represent one logical table unit in world space.
        /// </summary>
        public TabletopCoordinateConverter(
            Vector3 worldOrigin,
            Vector3 worldXAxisPerTableUnit,
            Vector3 worldYAxisPerTableUnit,
            Vector3 worldUp,
            float baseHeight,
            float layerHeight,
            float localOrderHeight)
        {
            ValidateFinite(worldOrigin);
            ValidateFinite(worldXAxisPerTableUnit);
            ValidateFinite(worldYAxisPerTableUnit);
            ValidateFinite(worldUp);
            if (worldXAxisPerTableUnit.sqrMagnitude <= Mathf.Epsilon)
            {
                throw new ArgumentOutOfRangeException(nameof(worldXAxisPerTableUnit));
            }

            if (worldYAxisPerTableUnit.sqrMagnitude <= Mathf.Epsilon)
            {
                throw new ArgumentOutOfRangeException(nameof(worldYAxisPerTableUnit));
            }

            if (worldUp.sqrMagnitude <= Mathf.Epsilon)
            {
                throw new ArgumentOutOfRangeException(nameof(worldUp));
            }

            Vector3 normalizedX = worldXAxisPerTableUnit.normalized;
            Vector3 normalizedY = worldYAxisPerTableUnit.normalized;
            Vector3 normalizedUp = worldUp.normalized;
            if (Mathf.Abs(Vector3.Dot(normalizedX, normalizedY)) > 0.001f
                || Mathf.Abs(Vector3.Dot(normalizedX, normalizedUp)) > 0.001f
                || Mathf.Abs(Vector3.Dot(normalizedY, normalizedUp)) > 0.001f
                || Vector3.Dot(Vector3.Cross(normalizedUp, normalizedY), normalizedX) < 0.999f)
            {
                throw new ArgumentException("Tabletop coordinate axes must form an orthogonal right-handed frame.");
            }

            if (!IsFinite(baseHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(baseHeight));
            }

            if (!IsFinite(layerHeight) || layerHeight < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(layerHeight));
            }

            if (!IsFinite(localOrderHeight) || localOrderHeight < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(localOrderHeight));
            }

            this.worldOrigin = worldOrigin;
            this.worldXAxisPerTableUnit = worldXAxisPerTableUnit;
            this.worldYAxisPerTableUnit = worldYAxisPerTableUnit;
            this.worldUp = normalizedUp;
            worldRotation = Quaternion.LookRotation(normalizedY, normalizedUp);
            WorldUnitsPerTableUnit = (worldXAxisPerTableUnit.magnitude + worldYAxisPerTableUnit.magnitude) * 0.5f;
            BaseHeight = baseHeight;
            LayerHeight = layerHeight;
            LocalOrderHeight = localOrderHeight;
        }

        /// <summary>
        /// Number of Unity world units represented by one logical table unit.
        /// </summary>
        public float WorldUnitsPerTableUnit { get; }

        /// <summary>
        /// Unity world Y value used for table-level coordinates.
        /// </summary>
        public float BaseHeight { get; }

        /// <summary>
        /// Additional Unity world Y offset applied for each logical layer.
        /// </summary>
        public float LayerHeight { get; }

        /// <summary>
        /// Additional Unity world Y offset applied for each local order step.
        /// </summary>
        public float LocalOrderHeight { get; }

        /// <summary>World-space normal of the authored tabletop coordinate frame.</summary>
        public Vector3 WorldUp => worldUp;

        public Vector3 WorldXAxisPerTableUnit => worldXAxisPerTableUnit;

        public Vector3 WorldYAxisPerTableUnit => worldYAxisPerTableUnit;

        /// <summary>
        /// Converts a logical table coordinate to a Unity world position.
        /// </summary>
        public Vector3 ToWorldPosition(TableCoordinate coordinate)
        {
            ValidateFinite(coordinate);

            return ConvertPosition(coordinate, BaseHeight);
        }

        /// <summary>
        /// Converts a logical tabletop pose to a Unity world position.
        /// </summary>
        public Vector3 ToWorldPosition(TabletopPose pose)
        {
            ValidateFinite(pose.Position);
            ValidateFiniteRotation(pose);

            float worldHeight = ConvertToFiniteFloat(
                BaseHeight
                + (double)pose.Layer * LayerHeight
                + (double)pose.LocalOrder * LocalOrderHeight);
            return ConvertPosition(pose.Position, worldHeight);
        }

        /// <summary>
        /// Converts logical tabletop rotation to a Unity world Y-axis rotation.
        /// </summary>
        public Quaternion ToWorldRotation(TabletopPose pose)
        {
            ValidateFiniteRotation(pose);

            return worldRotation * Quaternion.Euler(0f, pose.RotationDegrees, 0f);
        }

        /// <summary>
        /// Converts a Unity world position back through the configured tabletop axes.
        /// </summary>
        public TableCoordinate ToTableCoordinate(Vector3 worldPosition)
        {
            ValidateFinite(worldPosition);

            Vector3 offset = worldPosition - worldOrigin;
            double logicalX = ConvertToFiniteDouble(
                Vector3.Dot(offset, worldXAxisPerTableUnit) / worldXAxisPerTableUnit.sqrMagnitude);
            double logicalY = ConvertToFiniteDouble(
                Vector3.Dot(offset, worldYAxisPerTableUnit) / worldYAxisPerTableUnit.sqrMagnitude);

            return new TableCoordinate(logicalX, logicalY);
        }

        private Vector3 ConvertPosition(TableCoordinate coordinate, float worldHeight)
        {
            float logicalX = ConvertToFiniteFloat(coordinate.X);
            float logicalY = ConvertToFiniteFloat(coordinate.Y);
            Vector3 converted = worldOrigin
                + (worldXAxisPerTableUnit * logicalX)
                + (worldYAxisPerTableUnit * logicalY)
                + (worldUp * worldHeight);
            ValidateFinite(converted);
            return converted;
        }

        private static void ValidateFinite(TableCoordinate coordinate)
        {
            if (!IsFinite(coordinate.X) || !IsFinite(coordinate.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(coordinate));
            }
        }

        private static void ValidateFiniteRotation(TabletopPose pose)
        {
            if (!IsFinite(pose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(pose));
            }
        }

        private static void ValidateFinite(Vector3 worldPosition)
        {
            if (!IsFinite(worldPosition.x) || !IsFinite(worldPosition.y) || !IsFinite(worldPosition.z))
            {
                throw new ArgumentOutOfRangeException(nameof(worldPosition));
            }
        }

        private static float ConvertToFiniteFloat(double value)
        {
            float convertedValue = (float)value;
            if (!IsFinite(convertedValue))
            {
                throw new OverflowException("Converted Unity position component is not finite.");
            }

            return convertedValue;
        }

        private static double ConvertToFiniteDouble(double value)
        {
            if (!IsFinite(value))
            {
                throw new OverflowException("Converted logical table coordinate component is not finite.");
            }

            return value;
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
