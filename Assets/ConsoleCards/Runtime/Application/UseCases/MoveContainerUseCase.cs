using System;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;

namespace ConsoleCards.Application.UseCases
{
    /// <summary>
    /// Moves the authoritative placement anchor of a physical Deck or Stack.
    /// Contained Card membership and order are not mutated.
    /// </summary>
    public sealed class MoveContainerUseCase
    {
        private readonly Func<TabletopPose, float?> resolveSurfaceHeight;

        public MoveContainerUseCase(Func<TabletopPose, float?> resolveSurfaceHeight = null)
        {
            this.resolveSurfaceHeight = resolveSurfaceHeight;
        }

        public MoveContainerResult Execute(MatchState matchState, MoveContainerCommand command)
        {
            if (matchState == null)
            {
                return MoveContainerResult.Failure(
                    CommandResultStatus.Invalid,
                    MoveContainerError.MatchRequired);
            }

            if (command == null)
            {
                return MoveContainerResult.Failure(
                    CommandResultStatus.Invalid,
                    MoveContainerError.CommandRequired);
            }

            if (command.Context.MatchId != matchState.Id)
            {
                return MoveContainerResult.Failure(
                    CommandResultStatus.Invalid,
                    MoveContainerError.MatchIdMismatch);
            }

            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return MoveContainerResult.Failure(
                    CommandResultStatus.Conflict,
                    MoveContainerError.RevisionConflict);
            }

            if (!matchState.Containers.TryGetValue(command.ContainerId, out ContainerState container))
            {
                return MoveContainerResult.Failure(
                    CommandResultStatus.Rejected,
                    MoveContainerError.ContainerNotFound);
            }

            if (container.Kind != ContainerKind.Deck && container.Kind != ContainerKind.Stack)
            {
                return MoveContainerResult.Failure(
                    CommandResultStatus.Rejected,
                    MoveContainerError.ContainerNotMovable);
            }

            if (!matchState.TryGetContainerPlacement(command.ContainerId, out ContainerPlacementState placement))
            {
                return MoveContainerResult.Failure(
                    CommandResultStatus.Rejected,
                    MoveContainerError.PlacementNotFound);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return MoveContainerResult.Failure(
                    CommandResultStatus.Conflict,
                    MoveContainerError.RevisionOverflow);
            }

            float? surfaceHeight = resolveSurfaceHeight?.Invoke(command.TargetPose);
            if (resolveSurfaceHeight != null && !surfaceHeight.HasValue)
            {
                return MoveContainerResult.Failure(
                    CommandResultStatus.Rejected,
                    MoveContainerError.PhysicalSurfaceRequired);
            }

            placement.SetPose(command.TargetPose, surfaceHeight);
            long revision = matchState.AdvanceRevision();
            return MoveContainerResult.Accepted(revision);
        }
    }
}
