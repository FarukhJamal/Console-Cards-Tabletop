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
        public TabletopCoordinateConverter(
            float worldUnitsPerTableUnit,
            float baseHeight,
            float layerHeight,
            float localOrderHeight)
        {
            if (!IsFinite(worldUnitsPerTableUnit) || worldUnitsPerTableUnit <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(worldUnitsPerTableUnit));
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

            WorldUnitsPerTableUnit = worldUnitsPerTableUnit;
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

        /// <summary>
        /// Converts a logical table coordinate to a Unity world position.
        /// </summary>
        public Vector3 ToWorldPosition(TableCoordinate coordinate)
        {
            ValidateFinite(coordinate);

            float worldX = ConvertToFiniteFloat(coordinate.X * WorldUnitsPerTableUnit);
            float worldZ = ConvertToFiniteFloat(coordinate.Y * WorldUnitsPerTableUnit);

            return new Vector3(worldX, BaseHeight, worldZ);
        }

        /// <summary>
        /// Converts a logical tabletop pose to a Unity world position.
        /// </summary>
        public Vector3 ToWorldPosition(TabletopPose pose)
        {
            ValidateFinite(pose.Position);
            ValidateFiniteRotation(pose);

            float worldX = ConvertToFiniteFloat(pose.Position.X * WorldUnitsPerTableUnit);
            float worldY = ConvertToFiniteFloat(
                BaseHeight
                + (double)pose.Layer * LayerHeight
                + (double)pose.LocalOrder * LocalOrderHeight);
            float worldZ = ConvertToFiniteFloat(pose.Position.Y * WorldUnitsPerTableUnit);

            return new Vector3(worldX, worldY, worldZ);
        }

        /// <summary>
        /// Converts logical tabletop rotation to a Unity world Y-axis rotation.
        /// </summary>
        public Quaternion ToWorldRotation(TabletopPose pose)
        {
            ValidateFiniteRotation(pose);

            return Quaternion.Euler(0f, pose.RotationDegrees, 0f);
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

        private static float ConvertToFiniteFloat(double value)
        {
            float convertedValue = (float)value;
            if (!IsFinite(convertedValue))
            {
                throw new OverflowException("Converted Unity position component is not finite.");
            }

            return convertedValue;
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
