using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;

namespace ConsoleCards.Application.UseCases
{
    public sealed class TransferCardUseCase
    {
        public TransferCardResult Execute(MatchState matchState, TransferCardCommand command)
        {
            if (matchState == null)
            {
                return TransferCardResult.Failure(CommandResultStatus.Invalid, TransferCardError.MatchMissing);
            }

            if (command == null)
            {
                return TransferCardResult.Failure(CommandResultStatus.Invalid, TransferCardError.CommandMissing);
            }

            if (command.Context.MatchId != matchState.Id)
            {
                return TransferCardResult.Failure(CommandResultStatus.Invalid, TransferCardError.MatchMismatch);
            }

            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return TransferCardResult.Failure(CommandResultStatus.Conflict, TransferCardError.RevisionConflict);
            }

            if (!matchState.ContainsObject(command.CardObjectId))
            {
                return TransferCardResult.Failure(CommandResultStatus.Rejected, TransferCardError.ObjectMissing);
            }

            TabletopObjectState cardObject = matchState.GetObject(command.CardObjectId);
            if (cardObject.Kind != TabletopObjectKind.Card)
            {
                return TransferCardResult.Failure(CommandResultStatus.Rejected, TransferCardError.ObjectNotCard);
            }

            if (cardObject.IsUserLocked)
            {
                return TransferCardResult.Failure(CommandResultStatus.Rejected, TransferCardError.ObjectUserLocked);
            }

            if (command.ExpectedSourceContainerId == command.DestinationContainerId)
            {
                return TransferCardResult.Failure(CommandResultStatus.Invalid, TransferCardError.SameLocation);
            }

            if (cardObject.ContainerId != command.ExpectedSourceContainerId)
            {
                return TransferCardResult.Failure(CommandResultStatus.Rejected, TransferCardError.SourceContainerMismatch);
            }

            ContainerState source = null;
            if (!ValidateSource(matchState, command, out source, out TransferCardError sourceError))
            {
                return TransferCardResult.Failure(CommandResultStatus.Rejected, sourceError);
            }

            if (HasUnexpectedMembership(
                matchState,
                command.CardObjectId,
                command.ExpectedSourceContainerId,
                command.DestinationContainerId))
            {
                return TransferCardResult.Failure(
                    CommandResultStatus.Rejected,
                    TransferCardError.ObjectFoundInUnexpectedContainer);
            }

            ContainerState destination = null;
            if (!command.DestinationContainerId.IsEmpty)
            {
                if (!matchState.Containers.TryGetValue(command.DestinationContainerId, out destination))
                {
                    return TransferCardResult.Failure(
                        CommandResultStatus.Rejected,
                        TransferCardError.DestinationContainerMissing);
                }

                if (destination.IsFull)
                {
                    return TransferCardResult.Failure(
                        CommandResultStatus.Rejected,
                        TransferCardError.DestinationCapacityExceeded);
                }

                if (destination.Contains(command.CardObjectId))
                {
                    return TransferCardResult.Failure(
                        CommandResultStatus.Rejected,
                        TransferCardError.DestinationAlreadyContainsObject);
                }
            }

            if (command.DestinationContainerId.IsEmpty && !command.TargetTablePose.HasValue)
            {
                return TransferCardResult.Failure(
                    CommandResultStatus.Invalid,
                    TransferCardError.TargetTablePoseMissing);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return TransferCardResult.Failure(CommandResultStatus.Conflict, TransferCardError.RevisionOverflow);
            }

            TabletopPose targetTablePose = default;
            if (command.DestinationContainerId.IsEmpty
                && !LooseCardOrderResolver.TryResolveTopPose(
                    matchState,
                    cardObject.Id,
                    command.TargetTablePose.Value,
                    out targetTablePose))
            {
                return TransferCardResult.Failure(
                    CommandResultStatus.Conflict,
                    TransferCardError.LooseCardOrderOverflow);
            }

            if (command.PhysicalState != null && (!command.DestinationContainerId.IsEmpty
                || command.PhysicalState.Mode != PhysicalObjectMode.Dynamic
                || command.PhysicalState.ControllingPlayerId != command.Context.RequestedByPlayerId))
                return TransferCardResult.Failure(CommandResultStatus.Invalid, TransferCardError.PhysicalStateInvalid);

            TabletopPoseSnapshot cardSnapshot = TabletopPoseSnapshot.Capture(cardObject, source);
            ContainerTransferService transferService = new ContainerTransferService();

            try
            {
                ContainerTransferResult transferResult = ExecuteTransfer(
                    transferService,
                    cardObject,
                    source,
                    destination,
                    command);

                if (!transferResult.Succeeded)
                {
                    return MapTransferFailure(transferResult.Error);
                }

                if (command.DestinationContainerId.IsEmpty)
                {
                    cardObject.SetPose(targetTablePose);
                    cardObject.SetPhysicalState(command.PhysicalState);
                }
                else cardObject.SetPhysicalState(null);

                long revision = matchState.AdvanceRevision();
                return TransferCardResult.Accepted(revision);
            }
            catch
            {
                RollBackTransfer(
                    transferService,
                    cardObject,
                    source,
                    destination,
                    command,
                    cardSnapshot);
                throw;
            }
        }

        private static bool ValidateSource(
            MatchState matchState,
            TransferCardCommand command,
            out ContainerState source,
            out TransferCardError error)
        {
            source = null;
            error = TransferCardError.None;

            if (command.ExpectedSourceContainerId.IsEmpty)
            {
                return true;
            }

            if (!matchState.Containers.TryGetValue(command.ExpectedSourceContainerId, out source))
            {
                error = TransferCardError.SourceContainerMissing;
                return false;
            }

            if (!source.Contains(command.CardObjectId))
            {
                error = TransferCardError.SourceMembershipMissing;
                return false;
            }

            return true;
        }

        private static bool HasUnexpectedMembership(
            MatchState matchState,
            TabletopObjectId objectId,
            ContainerId expectedSourceContainerId,
            ContainerId destinationContainerId)
        {
            foreach (ContainerState container in matchState.Containers.Values)
            {
                if (container.Id == expectedSourceContainerId || container.Id == destinationContainerId)
                {
                    continue;
                }

                if (container.Contains(objectId))
                {
                    return true;
                }
            }

            return false;
        }

        private static ContainerTransferResult ExecuteTransfer(
            ContainerTransferService transferService,
            TabletopObjectState cardObject,
            ContainerState source,
            ContainerState destination,
            TransferCardCommand command)
        {
            if (command.ExpectedSourceContainerId.IsEmpty)
            {
                return transferService.PlaceIntoContainer(cardObject, destination);
            }

            if (command.DestinationContainerId.IsEmpty)
            {
                return transferService.RemoveFromContainer(cardObject, source);
            }

            return transferService.MoveBetweenContainers(cardObject, source, destination);
        }

        private static void RollBackTransfer(
            ContainerTransferService transferService,
            TabletopObjectState cardObject,
            ContainerState source,
            ContainerState destination,
            TransferCardCommand command,
            TabletopPoseSnapshot cardSnapshot)
        {
            if (cardObject.ContainerId == command.DestinationContainerId
                && !command.DestinationContainerId.IsEmpty
                && destination != null
                && destination.Contains(cardObject.Id))
            {
                if (command.ExpectedSourceContainerId.IsEmpty)
                {
                    transferService.RemoveFromContainer(cardObject, destination);
                }
                else if (source != null)
                {
                    transferService.MoveBetweenContainers(cardObject, destination, source, cardSnapshot.SourceIndex);
                }
            }
            else if (cardObject.ContainerId.IsEmpty
                && !command.ExpectedSourceContainerId.IsEmpty
                && source != null
                && !source.Contains(cardObject.Id))
            {
                transferService.PlaceIntoContainer(cardObject, source, cardSnapshot.SourceIndex);
            }

            cardObject.SetContainer(cardSnapshot.ContainerId);
            cardObject.SetPose(cardSnapshot.Pose);
            cardObject.SetPhysicalState(cardSnapshot.PhysicalState);
        }

        private static TransferCardResult MapTransferFailure(ContainerTransferError error)
        {
            switch (error)
            {
                case ContainerTransferError.DestinationFull:
                    return TransferCardResult.Failure(
                        CommandResultStatus.Rejected,
                        TransferCardError.DestinationCapacityExceeded);

                case ContainerTransferError.ObjectAlreadyContained:
                    return TransferCardResult.Failure(
                        CommandResultStatus.Rejected,
                        TransferCardError.DestinationAlreadyContainsObject);

                case ContainerTransferError.SourceRequired:
                    return TransferCardResult.Failure(
                        CommandResultStatus.Rejected,
                        TransferCardError.SourceContainerMissing);

                case ContainerTransferError.SourceDoesNotContainObject:
                    return TransferCardResult.Failure(
                        CommandResultStatus.Rejected,
                        TransferCardError.SourceMembershipMissing);

                case ContainerTransferError.SourceContainerMismatch:
                    return TransferCardResult.Failure(
                        CommandResultStatus.Rejected,
                        TransferCardError.SourceContainerMismatch);

                case ContainerTransferError.SameContainer:
                    return TransferCardResult.Failure(
                        CommandResultStatus.Invalid,
                        TransferCardError.SameLocation);

                default:
                    return TransferCardResult.Failure(
                        CommandResultStatus.Rejected,
                        TransferCardError.SourceContainerMismatch);
            }
        }

        private readonly struct TabletopPoseSnapshot
        {
            private TabletopPoseSnapshot(TabletopPose pose, ContainerId containerId, int sourceIndex, PhysicalObjectState physicalState)
            {
                Pose = pose;
                ContainerId = containerId;
                SourceIndex = sourceIndex;
                PhysicalState = physicalState;
            }

            public TabletopPose Pose { get; }

            public ContainerId ContainerId { get; }

            public int SourceIndex { get; }
            public PhysicalObjectState PhysicalState { get; }

            public static TabletopPoseSnapshot Capture(TabletopObjectState cardObject, ContainerState source)
            {
                int sourceIndex = source == null ? -1 : source.IndexOf(cardObject.Id);
                return new TabletopPoseSnapshot(cardObject.Pose, cardObject.ContainerId, sourceIndex, cardObject.PhysicalState);
            }
        }
    }
}
