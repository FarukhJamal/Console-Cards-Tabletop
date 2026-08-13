using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.Consoles
{
    /// <summary>
    /// Authoritative placement for an unowned, freeform universal Console.
    /// Its Slot membership remains represented by the shared ConsoleState/ContainerState model.
    /// </summary>
    public sealed class PlacedConsoleState
    {
        public PlacedConsoleState(ConsoleId id, TabletopPose pose, ConsoleState console)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("Console ID cannot be empty.", nameof(id));
            }

            if (console == null)
            {
                throw new ArgumentNullException(nameof(console));
            }

            if (console.IsOwnedBySeat)
            {
                throw new ArgumentException("A placed freeform Console cannot own a Seat.", nameof(console));
            }

            ValidatePose(pose, nameof(pose));
            Id = id;
            Pose = pose;
            Console = console;
        }

        public ConsoleId Id { get; }

        public TabletopPose Pose { get; private set; }

        public ConsoleState Console { get; }

        public void SetPose(TabletopPose pose)
        {
            ValidatePose(pose, nameof(pose));
            Pose = pose;
        }

        private static void ValidatePose(TabletopPose pose, string parameterName)
        {
            if (double.IsNaN(pose.Position.X)
                || double.IsInfinity(pose.Position.X)
                || double.IsNaN(pose.Position.Y)
                || double.IsInfinity(pose.Position.Y)
                || float.IsNaN(pose.RotationDegrees)
                || float.IsInfinity(pose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Placed Console pose must be finite.");
            }
        }
    }
}
