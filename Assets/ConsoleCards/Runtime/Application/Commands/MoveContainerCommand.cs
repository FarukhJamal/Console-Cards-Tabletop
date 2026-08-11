using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    /// <summary>
    /// Requests an authoritative placement change for one physical Container.
    /// </summary>
    public sealed class MoveContainerCommand : ITabletopCommand
    {
        public MoveContainerCommand(
            CommandContext context,
            ContainerId containerId,
            TabletopPose targetPose)
        {
            if (containerId.IsEmpty)
            {
                throw new ArgumentException("Container ID cannot be empty.", nameof(containerId));
            }

            if (!IsFinite(targetPose.Position.X)
                || !IsFinite(targetPose.Position.Y)
                || !IsFinite(targetPose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(targetPose), "Target pose must be finite.");
            }

            Context = context;
            ContainerId = containerId;
            TargetPose = targetPose;
        }

        public CommandContext Context { get; }

        public ContainerId ContainerId { get; }

        public TabletopPose TargetPose { get; }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
