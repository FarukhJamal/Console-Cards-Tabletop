using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.Containers
{
    /// <summary>
    /// Stores the authoritative tabletop pose for a placed Deck, Stack, or Discard Pile, or the zone of a
    /// Hand (pose plus extent; the Hand has no model, the zone is where cards can be dropped into it).
    /// Membership, kind, ownership, capacity, and visibility remain owned by ContainerState.
    /// </summary>
    public sealed class ContainerPlacementState
    {
        public ContainerPlacementState(
            ContainerId containerId,
            TabletopPose pose,
            float? surfaceHeight = null,
            float? extentWidth = null,
            float? extentDepth = null)
        {
            if (containerId.IsEmpty)
            {
                throw new ArgumentException("Container ID cannot be empty.", nameof(containerId));
            }

            ContainerId = containerId;
            if (extentWidth.HasValue != extentDepth.HasValue)
            {
                throw new ArgumentException("A placement extent needs both a width and a depth.", nameof(extentWidth));
            }

            if (extentWidth.HasValue)
            {
                ValidateExtent(extentWidth.Value, nameof(extentWidth));
                ValidateExtent(extentDepth.Value, nameof(extentDepth));
                HasExtent = true;
                ExtentWidth = extentWidth.Value;
                ExtentDepth = extentDepth.Value;
            }

            SetPose(pose, surfaceHeight);
        }

        public ContainerId ContainerId { get; }

        /// <summary>True for a zone placement (a Hand zone): the extent is its footprint on the table.</summary>
        public bool HasExtent { get; }

        /// <summary>Zone width along the zone's local x, in table units; 0 without an extent.</summary>
        public float ExtentWidth { get; }

        /// <summary>Zone depth along the zone's local y (table depth), in table units; 0 without an extent.</summary>
        public float ExtentDepth { get; }

        public TabletopPose Pose { get; private set; }

        /// <summary>
        /// Accepted surface world Y for a non-physical anchor. Null retains authored layout height.
        /// This excludes preview lift and contained Card thickness/order offsets.
        /// </summary>
        public float? SurfaceHeight { get; private set; }

        public void SetPose(TabletopPose pose, float? surfaceHeight = null)
        {
            ValidatePose(pose, nameof(pose));
            if (surfaceHeight.HasValue
                && (float.IsNaN(surfaceHeight.Value) || float.IsInfinity(surfaceHeight.Value)))
            {
                throw new ArgumentOutOfRangeException(nameof(surfaceHeight));
            }

            Pose = pose;
            SurfaceHeight = surfaceHeight;
        }

        private static void ValidateExtent(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Placement extent must be finite and above zero.");
            }
        }

        private static void ValidatePose(TabletopPose pose, string parameterName)
        {
            if (double.IsNaN(pose.Position.X) || double.IsInfinity(pose.Position.X))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Pose X must be finite.");
            }

            if (double.IsNaN(pose.Position.Y) || double.IsInfinity(pose.Position.Y))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Pose Y must be finite.");
            }

            if (float.IsNaN(pose.RotationDegrees) || float.IsInfinity(pose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Pose rotation must be finite.");
            }
        }
    }
}
