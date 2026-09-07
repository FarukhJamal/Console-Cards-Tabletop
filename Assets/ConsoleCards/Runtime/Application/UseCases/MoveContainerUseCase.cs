using System;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.UseCases
{
    /// <summary>
    /// Moves the authoritative placement anchor of a Deck, Stack, or Console.
    /// A Console is addressed through one of its stable Slot Container IDs.
    /// Contained Card/Slot membership and order are not mutated.
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

            bool isFixedCollection = container.Kind == ContainerKind.Deck
                || container.Kind == ContainerKind.Stack;
            bool isConsoleSlot = container.Kind == ContainerKind.ConsoleSlot;
            if (!isFixedCollection && !isConsoleSlot)
            {
                return MoveContainerResult.Failure(
                    CommandResultStatus.Rejected,
                    MoveContainerError.ContainerNotMovable);
            }

            ContainerPlacementState placement = null;
            SeatState owningSeat = null;
            PlacedConsoleState placedConsole = null;
            if (isFixedCollection
                && !matchState.TryGetContainerPlacement(command.ContainerId, out placement))
            {
                return MoveContainerResult.Failure(
                    CommandResultStatus.Rejected,
                    MoveContainerError.PlacementNotFound);
            }

            if (isConsoleSlot
                && !TryResolveConsole(
                    matchState,
                    command.ContainerId,
                    out owningSeat,
                    out placedConsole))
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

            if (placement != null)
            {
                placement.SetPose(command.TargetPose, surfaceHeight);
            }
            else if (owningSeat != null)
            {
                owningSeat.SetConsolePlacement(command.TargetPose, surfaceHeight);
            }
            else
            {
                placedConsole.SetPose(command.TargetPose, surfaceHeight);
            }

            long revision = matchState.AdvanceRevision();
            return MoveContainerResult.Accepted(revision);
        }

        private static bool TryResolveConsole(
            MatchState matchState,
            ContainerId slotContainerId,
            out SeatState owningSeat,
            out PlacedConsoleState placedConsole)
        {
            foreach (SeatState seat in matchState.Seats.Values)
            {
                if (seat.Console.ContainsSlot(slotContainerId))
                {
                    owningSeat = seat;
                    placedConsole = null;
                    return true;
                }
            }

            foreach (PlacedConsoleState candidate in matchState.PlacedConsoles.Values)
            {
                if (candidate.Console.ContainsSlot(slotContainerId))
                {
                    owningSeat = null;
                    placedConsole = candidate;
                    return true;
                }
            }

            owningSeat = null;
            placedConsole = null;
            return false;
        }
    }
}
