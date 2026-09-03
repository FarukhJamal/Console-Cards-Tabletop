using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    /// <summary>
    /// Requests an authoritative physical Token transfer between the tabletop and Containers.
    /// It carries no Game-specific ownership, economy, or legality semantics.
    /// </summary>
    public sealed class TransferTokenCommand : ITabletopCommand
    {
        private TransferTokenCommand(
            CommandContext context,
            TabletopObjectId tokenObjectId,
            ContainerId expectedSourceContainerId,
            ContainerId destinationContainerId,
            TabletopPose? targetTablePose, PhysicalObjectState physicalState = null)
        {
            if (tokenObjectId.IsEmpty)
            {
                throw new ArgumentException("Token object ID cannot be empty.", nameof(tokenObjectId));
            }

            if (expectedSourceContainerId == destinationContainerId)
            {
                throw new ArgumentException("Expected source and destination cannot be the same.", nameof(destinationContainerId));
            }

            if (destinationContainerId.IsEmpty == !targetTablePose.HasValue)
            {
                throw new ArgumentException(
                    "A tabletop pose is required only when transferring to the tabletop.",
                    nameof(targetTablePose));
            }

            if (targetTablePose.HasValue)
            {
                ValidatePose(targetTablePose.Value, nameof(targetTablePose));
            }

            Context = context;
            TokenObjectId = tokenObjectId;
            ExpectedSourceContainerId = expectedSourceContainerId;
            DestinationContainerId = destinationContainerId;
            TargetTablePose = targetTablePose;
            PhysicalState = physicalState;
        }

        public CommandContext Context { get; }

        public TabletopObjectId TokenObjectId { get; }

        public ContainerId ExpectedSourceContainerId { get; }

        public ContainerId DestinationContainerId { get; }

        public TabletopPose? TargetTablePose { get; }
        public PhysicalObjectState PhysicalState { get; }

        public static TransferTokenCommand ToContainer(
            CommandContext context,
            TabletopObjectId tokenObjectId,
            ContainerId expectedSourceContainerId,
            ContainerId destinationContainerId)
        {
            if (destinationContainerId.IsEmpty)
            {
                throw new ArgumentException("Destination Container ID cannot be empty.", nameof(destinationContainerId));
            }

            return new TransferTokenCommand(
                context,
                tokenObjectId,
                expectedSourceContainerId,
                destinationContainerId,
                null);
        }

        public static TransferTokenCommand ToTabletop(
            CommandContext context,
            TabletopObjectId tokenObjectId,
            ContainerId expectedSourceContainerId,
            TabletopPose targetTablePose, PhysicalObjectState physicalState = null)
        {
            if (expectedSourceContainerId.IsEmpty)
            {
                throw new ArgumentException("A contained Token requires a source Container.", nameof(expectedSourceContainerId));
            }

            return new TransferTokenCommand(
                context,
                tokenObjectId,
                expectedSourceContainerId,
                ContainerId.Empty,
                targetTablePose, physicalState);
        }

        private static void ValidatePose(TabletopPose pose, string parameterName)
        {
            if (!IsFinite(pose.Position.X)
                || !IsFinite(pose.Position.Y)
                || !IsFinite(pose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Target tabletop pose must be finite.");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
