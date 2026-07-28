using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    public sealed class SplitStackCommand : ITabletopCommand
    {
        public SplitStackCommand(
            CommandContext context,
            ContainerId sourceStackContainerId,
            ContainerId newStackContainerId,
            StackSplitSpecification splitSpecification,
            TabletopPose newStackPose)
        {
            if (sourceStackContainerId.IsEmpty)
            {
                throw new ArgumentException("Source Stack Container ID cannot be empty.", nameof(sourceStackContainerId));
            }

            if (newStackContainerId.IsEmpty)
            {
                throw new ArgumentException("New Stack Container ID cannot be empty.", nameof(newStackContainerId));
            }

            if (sourceStackContainerId == newStackContainerId)
            {
                throw new ArgumentException("Source and new Stack Container IDs must be different.", nameof(newStackContainerId));
            }

            if (splitSpecification.FirstMovedIndex < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(splitSpecification), "Split specification first moved index must be at least one.");
            }

            ValidatePose(newStackPose, nameof(newStackPose));

            Context = context;
            SourceStackContainerId = sourceStackContainerId;
            NewStackContainerId = newStackContainerId;
            SplitSpecification = splitSpecification;
            NewStackPose = newStackPose;
        }

        public CommandContext Context { get; }

        public ContainerId SourceStackContainerId { get; }

        public ContainerId NewStackContainerId { get; }

        public StackSplitSpecification SplitSpecification { get; }

        public TabletopPose NewStackPose { get; }

        private static void ValidatePose(TabletopPose pose, string parameterName)
        {
            if (double.IsNaN(pose.Position.X) || double.IsInfinity(pose.Position.X))
            {
                throw new ArgumentOutOfRangeException(parameterName, "New Stack pose X must be finite.");
            }

            if (double.IsNaN(pose.Position.Y) || double.IsInfinity(pose.Position.Y))
            {
                throw new ArgumentOutOfRangeException(parameterName, "New Stack pose Y must be finite.");
            }

            if (float.IsNaN(pose.RotationDegrees) || float.IsInfinity(pose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(parameterName, "New Stack pose rotation must be finite.");
            }
        }
    }
}
