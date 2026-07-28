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
            TabletopPose pose)
        {
            if (containerId.IsEmpty)
            {
                throw new ArgumentException("Container ID cannot be empty.", nameof(containerId));
            }

            ValidatePose(pose, nameof(pose));

            ContainerId = containerId;
            Pose = pose;
        }

        public ContainerId ContainerId { get; }

        public TabletopPose Pose { get; private set; }

        public void SetPose(TabletopPose pose)
        {
            ValidatePose(pose, nameof(pose));

            Pose = pose;
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
