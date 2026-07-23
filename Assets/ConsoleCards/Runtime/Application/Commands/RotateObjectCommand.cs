using System;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    public sealed class RotateObjectCommand : ITabletopCommand
    {
        public RotateObjectCommand(
            CommandContext context,
            TabletopObjectId objectId,
            float targetRotationDegrees)
        {
            if (objectId.IsEmpty)
            {
                throw new ArgumentException("Tabletop object ID cannot be empty.", nameof(objectId));
            }

            if (!IsFinite(targetRotationDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(targetRotationDegrees), "Target rotation must be finite.");
            }

            Context = context;
            ObjectId = objectId;
            TargetRotationDegrees = targetRotationDegrees;
        }

        public CommandContext Context { get; }

        public TabletopObjectId ObjectId { get; }

        public float TargetRotationDegrees { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
