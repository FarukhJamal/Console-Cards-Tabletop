using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    public sealed class TransferCardCommand : ITabletopCommand
    {
        public TransferCardCommand(
            CommandContext context,
            TabletopObjectId cardObjectId,
            ContainerId expectedSourceContainerId,
            ContainerId destinationContainerId,
            TabletopPose? targetTablePose, PhysicalObjectState physicalState = null,
            float? destinationRotationDegrees = null)
        {
            if (cardObjectId.IsEmpty)
            {
                throw new ArgumentException("Card object ID cannot be empty.", nameof(cardObjectId));
            }

            if (expectedSourceContainerId == destinationContainerId)
            {
                throw new ArgumentException("Expected source and destination cannot be the same.", nameof(destinationContainerId));
            }

            if (destinationContainerId.IsEmpty && !targetTablePose.HasValue)
            {
                throw new ArgumentException("A target tabletop pose is required when transferring to the tabletop.", nameof(targetTablePose));
            }

            if (!destinationContainerId.IsEmpty && targetTablePose.HasValue)
            {
                throw new ArgumentException("A target tabletop pose is only valid when transferring to the tabletop.", nameof(targetTablePose));
            }

            if (targetTablePose.HasValue)
            {
                ValidatePose(targetTablePose.Value, nameof(targetTablePose));
            }

            if (destinationRotationDegrees.HasValue
                && (destinationContainerId.IsEmpty || !IsFinite(destinationRotationDegrees.Value)))
            {
                throw new ArgumentException(
                    "A destination rotation must be finite and is only valid when transferring into a container.",
                    nameof(destinationRotationDegrees));
            }

            Context = context;
            CardObjectId = cardObjectId;
            ExpectedSourceContainerId = expectedSourceContainerId;
            DestinationContainerId = destinationContainerId;
            TargetTablePose = targetTablePose;
            PhysicalState = physicalState;
            DestinationRotationDegrees = destinationRotationDegrees;
        }

        public CommandContext Context { get; }

        public TabletopObjectId CardObjectId { get; }

        public ContainerId ExpectedSourceContainerId { get; }

        public ContainerId DestinationContainerId { get; }

        public TabletopPose? TargetTablePose { get; }
        public PhysicalObjectState PhysicalState { get; }

        /// <summary>
        /// Rotation the card takes on entering its destination container (a Console slot sets the slot's
        /// orientation). Null keeps the card's own rotation.
        /// </summary>
        public float? DestinationRotationDegrees { get; }

        public static TransferCardCommand ToContainer(
            CommandContext context,
            TabletopObjectId cardObjectId,
            ContainerId expectedSourceContainerId,
            ContainerId destinationContainerId,
            float? destinationRotationDegrees = null)
        {
            return new TransferCardCommand(
                context,
                cardObjectId,
                expectedSourceContainerId,
                destinationContainerId,
                null,
                null,
                destinationRotationDegrees);
        }

        public static TransferCardCommand ToTabletop(
            CommandContext context,
            TabletopObjectId cardObjectId,
            ContainerId expectedSourceContainerId,
            TabletopPose targetTablePose, PhysicalObjectState physicalState = null)
        {
            return new TransferCardCommand(
                context,
                cardObjectId,
                expectedSourceContainerId,
                ContainerId.Empty,
                targetTablePose, physicalState);
        }

        private static void ValidatePose(TabletopPose pose, string parameterName)
        {
            if (!IsFinite(pose.Position.X))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Target tabletop pose X coordinate must be finite.");
            }

            if (!IsFinite(pose.Position.Y))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Target tabletop pose Y coordinate must be finite.");
            }

            if (!IsFinite(pose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Target tabletop pose rotation must be finite.");
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
