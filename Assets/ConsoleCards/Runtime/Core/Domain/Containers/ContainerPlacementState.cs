using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.Containers
{
    /// <summary>
    /// Stores the authoritative tabletop pose for a placed Deck, Stack, or Discard Pile.
    /// Membership, kind, ownership, capacity, and visibility remain owned by ContainerState.
    /// </summary>
    public sealed class ContainerPlacementState
    {
        public ContainerPlacementState(
            ContainerId containerId,
            TabletopPose pose,
            float? surfaceHeight = null)
        {
            if (containerId.IsEmpty)
            {
                throw new ArgumentException("Container ID cannot be empty.", nameof(containerId));
            }

            ContainerId = containerId;
            SetPose(pose, surfaceHeight);
        }

        public ContainerId ContainerId { get; }

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
