using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;

namespace ConsoleCards.Application.UseCases
{
    /// <summary>
    /// Performs a freeform physical Token transfer while preserving Container membership invariants.
    /// </summary>
    public sealed class TransferTokenUseCase
    {
        public TransferTokenResult Execute(MatchState matchState, TransferTokenCommand command)
        {
            if (matchState == null)
            {
                return Failure(CommandResultStatus.Invalid, TransferTokenError.MatchMissing);
            }

            if (command == null)
            {
                return Failure(CommandResultStatus.Invalid, TransferTokenError.CommandMissing);
            }

            if (command.Context.MatchId != matchState.Id)
            {
                return Failure(CommandResultStatus.Invalid, TransferTokenError.MatchMismatch);
            }

            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return Failure(CommandResultStatus.Conflict, TransferTokenError.RevisionConflict);
            }

            if (!matchState.ContainsObject(command.TokenObjectId))
            {
                return Failure(CommandResultStatus.Rejected, TransferTokenError.ObjectMissing);
            }

            TabletopObjectState token = matchState.GetObject(command.TokenObjectId);
            if (token.Kind != TabletopObjectKind.Token)
            {
                return Failure(CommandResultStatus.Rejected, TransferTokenError.ObjectNotToken);
            }

            if (token.IsUserLocked)
            {
                return Failure(CommandResultStatus.Rejected, TransferTokenError.ObjectUserLocked);
            }

            if (token.ContainerId != command.ExpectedSourceContainerId)
            {
                return Failure(CommandResultStatus.Rejected, TransferTokenError.SourceContainerMismatch);
            }

            ContainerState source = null;
            if (!command.ExpectedSourceContainerId.IsEmpty)
            {
                if (!matchState.Containers.TryGetValue(command.ExpectedSourceContainerId, out source))
                {
                    return Failure(CommandResultStatus.Rejected, TransferTokenError.SourceContainerMissing);
                }

                if (!source.Contains(token.Id))
                {
                    return Failure(CommandResultStatus.Rejected, TransferTokenError.SourceMembershipMissing);
                }
            }

            if (HasUnexpectedMembership(matchState, token.Id, command.ExpectedSourceContainerId, command.DestinationContainerId))
            {
                return Failure(CommandResultStatus.Rejected, TransferTokenError.ObjectFoundInUnexpectedContainer);
            }

            ContainerState destination = null;
            if (!command.DestinationContainerId.IsEmpty)
            {
                if (!matchState.Containers.TryGetValue(command.DestinationContainerId, out destination))
                {
                    return Failure(CommandResultStatus.Rejected, TransferTokenError.DestinationContainerMissing);
                }

                if (destination.IsFull)
                {
                    return Failure(CommandResultStatus.Rejected, TransferTokenError.DestinationCapacityExceeded);
                }

                if (destination.Contains(token.Id))
                {
                    return Failure(CommandResultStatus.Rejected, TransferTokenError.DestinationAlreadyContainsObject);
                }
            }

            if (command.DestinationContainerId.IsEmpty && !command.TargetTablePose.HasValue)
            {
                return Failure(CommandResultStatus.Invalid, TransferTokenError.TargetTablePoseMissing);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return Failure(CommandResultStatus.Conflict, TransferTokenError.RevisionOverflow);
            }

            TabletopPose originalPose = token.Pose;
            int originalSourceIndex = source == null ? -1 : source.IndexOf(token.Id);
            ContainerTransferService transferService = new ContainerTransferService();

            try
            {
                ContainerTransferResult transferResult;
                if (source == null)
                {
                    transferResult = transferService.PlaceIntoContainer(token, destination);
                }
                else if (destination == null)
                {
                    transferResult = transferService.RemoveFromContainer(token, source);
                }
                else
                {
                    transferResult = transferService.MoveBetweenContainers(token, source, destination);
                }

                if (!transferResult.Succeeded)
                {
                    return MapTransferFailure(transferResult.Error);
                }

                if (destination == null)
                {
                    token.SetPose(command.TargetTablePose.Value);
                }

                return TransferTokenResult.Accepted(matchState.AdvanceRevision());
            }
            catch
            {
                RollBack(
                    transferService,
                    token,
                    source,
                    destination,
                    originalSourceIndex,
                    originalPose);
                throw;
            }
        }

        private static bool HasUnexpectedMembership(
            MatchState matchState,
            TabletopObjectId objectId,
            ContainerId expectedSourceContainerId,
            ContainerId destinationContainerId)
        {
            foreach (ContainerState container in matchState.Containers.Values)
            {
                if (container.Id != expectedSourceContainerId
                    && container.Id != destinationContainerId
                    && container.Contains(objectId))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RollBack(
            ContainerTransferService transferService,
            TabletopObjectState token,
            ContainerState source,
            ContainerState destination,
            int originalSourceIndex,
            TabletopPose originalPose)
        {
            if (destination != null && token.ContainerId == destination.Id && destination.Contains(token.Id))
            {
                if (source == null)
                {
                    transferService.RemoveFromContainer(token, destination);
                }
                else
                {
                    transferService.MoveBetweenContainers(token, destination, source, originalSourceIndex);
                }
            }
            else if (source != null && token.ContainerId.IsEmpty && !source.Contains(token.Id))
            {
                transferService.PlaceIntoContainer(token, source, originalSourceIndex);
            }

            token.SetPose(originalPose);
        }

        private static TransferTokenResult MapTransferFailure(ContainerTransferError error)
        {
            switch (error)
            {
                case ContainerTransferError.DestinationFull:
                    return Failure(CommandResultStatus.Rejected, TransferTokenError.DestinationCapacityExceeded);
                case ContainerTransferError.ObjectAlreadyContained:
                    return Failure(CommandResultStatus.Rejected, TransferTokenError.DestinationAlreadyContainsObject);
                case ContainerTransferError.SourceRequired:
                    return Failure(CommandResultStatus.Rejected, TransferTokenError.SourceContainerMissing);
                case ContainerTransferError.SourceDoesNotContainObject:
                    return Failure(CommandResultStatus.Rejected, TransferTokenError.SourceMembershipMissing);
                case ContainerTransferError.SourceContainerMismatch:
                    return Failure(CommandResultStatus.Rejected, TransferTokenError.SourceContainerMismatch);
                case ContainerTransferError.SameContainer:
                    return Failure(CommandResultStatus.Invalid, TransferTokenError.SameLocation);
                default:
                    return Failure(CommandResultStatus.Rejected, TransferTokenError.SourceContainerMismatch);
            }
        }

        private static TransferTokenResult Failure(CommandResultStatus status, TransferTokenError error)
        {
            return TransferTokenResult.Failure(status, error);
        }
    }
}
