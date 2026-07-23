using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    public sealed class MoveObjectCommand : ITabletopCommand
    {
        public MoveObjectCommand(
            CommandContext context,
            TabletopObjectId objectId,
            TabletopPose targetPose)
        {
            if (objectId.IsEmpty)
            {
                throw new ArgumentException("Tabletop object ID cannot be empty.", nameof(objectId));
            }

            if (!IsFinite(targetPose.Position.X))
            {
                throw new ArgumentOutOfRangeException(nameof(targetPose), "Target pose X coordinate must be finite.");
            }

            if (!IsFinite(targetPose.Position.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(targetPose), "Target pose Y coordinate must be finite.");
            }

            if (!IsFinite(targetPose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(targetPose), "Target pose rotation must be finite.");
            }

            Context = context;
            ObjectId = objectId;
            TargetPose = targetPose;
        }

        public CommandContext Context { get; }

        public TabletopObjectId ObjectId { get; }

        public TabletopPose TargetPose { get; }

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
