using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;

namespace ConsoleCards.Application.UseCases
{
    public sealed class MergeStacksUseCase
    {
        public MergeStacksResult Execute(MatchState matchState, MergeStacksCommand command)
        {
            if (matchState == null)
            {
                return MergeStacksResult.Failure(CommandResultStatus.Invalid, MergeStacksError.MatchMissing);
            }

            if (command == null)
            {
                return MergeStacksResult.Failure(CommandResultStatus.Invalid, MergeStacksError.CommandMissing);
            }

            if (command.Context.MatchId != matchState.Id)
            {
                return MergeStacksResult.Failure(CommandResultStatus.Invalid, MergeStacksError.MatchMismatch);
            }

            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return MergeStacksResult.Failure(CommandResultStatus.Conflict, MergeStacksError.RevisionConflict);
            }

            if (command.SourceStackContainerId == command.DestinationStackContainerId)
            {
                return MergeStacksResult.Failure(CommandResultStatus.Invalid, MergeStacksError.SameStack);
            }

            if (!matchState.Containers.TryGetValue(command.SourceStackContainerId, out ContainerState source))
            {
                return MergeStacksResult.Failure(CommandResultStatus.Rejected, MergeStacksError.SourceStackMissing);
            }

            if (!matchState.Containers.TryGetValue(command.DestinationStackContainerId, out ContainerState destination))
            {
                return MergeStacksResult.Failure(CommandResultStatus.Rejected, MergeStacksError.DestinationStackMissing);
            }

            if (source.Kind != ContainerKind.Stack)
            {
                return MergeStacksResult.Failure(CommandResultStatus.Rejected, MergeStacksError.SourceContainerNotStack);
            }

            if (destination.Kind != ContainerKind.Stack)
            {
                return MergeStacksResult.Failure(CommandResultStatus.Rejected, MergeStacksError.DestinationContainerNotStack);
            }

            if (source.Count == 0)
            {
                return MergeStacksResult.Failure(CommandResultStatus.Rejected, MergeStacksError.SourceStackEmpty);
            }

            if (destination.Capacity > 0 && destination.Count + source.Count > destination.Capacity)
            {
                return MergeStacksResult.Failure(CommandResultStatus.Rejected, MergeStacksError.DestinationCapacityExceeded);
            }

            List<TabletopObjectId> sourceObjectIds = new List<TabletopObjectId>(source.ObjectIds);
            Dictionary<TabletopObjectId, TabletopObjectState> sourceObjects =
                new Dictionary<TabletopObjectId, TabletopObjectState>();

            foreach (TabletopObjectId objectId in sourceObjectIds)
            {
                if (!matchState.ContainsObject(objectId))
                {
                    return MergeStacksResult.Failure(CommandResultStatus.Rejected, MergeStacksError.ObjectMissing);
                }

                TabletopObjectState objectState = matchState.GetObject(objectId);
                if (objectState.ContainerId != command.SourceStackContainerId)
                {
                    return MergeStacksResult.Failure(CommandResultStatus.Rejected, MergeStacksError.ObjectContainerMismatch);
                }

                if (objectState.IsUserLocked)
                {
                    return MergeStacksResult.Failure(CommandResultStatus.Rejected, MergeStacksError.ObjectUserLocked);
                }

                sourceObjects.Add(objectId, objectState);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return MergeStacksResult.Failure(CommandResultStatus.Conflict, MergeStacksError.RevisionOverflow);
            }

            if (!CanRemoveAfterTransfer(matchState, source.Id))
            {
                return MergeStacksResult.Failure(
                    CommandResultStatus.Rejected,
                    MergeStacksError.SourceContainerRemovalFailed);
            }

            List<TabletopObjectId> originalSourceOrder = new List<TabletopObjectId>(source.ObjectIds);
            List<TabletopObjectId> originalDestinationOrder = new List<TabletopObjectId>(destination.ObjectIds);
            Dictionary<TabletopObjectId, ContainerId> originalObjectContainerIds =
                CaptureObjectContainerIds(sourceObjects);

            ContainerBatchTransferService transferService = new ContainerBatchTransferService();

            try
            {
                ContainerTransferResult transferResult = transferService.TransferOrdered(
                    sourceObjects,
                    source,
                    destination,
                    sourceObjectIds);

                if (!transferResult.Succeeded)
                {
                    return MapTransferFailure(transferResult.Error);
                }

                if (source.Count != 0)
                {
                    RestoreMutation(source, destination, sourceObjects, originalSourceOrder, originalDestinationOrder, originalObjectContainerIds);
                    return MergeStacksResult.Failure(
                        CommandResultStatus.Rejected,
                        MergeStacksError.SourceContainerRemovalFailed);
                }

                try
                {
                    matchState.RemoveEmptyContainer(source.Id);
                }
                catch (ArgumentException)
                {
                    RestoreMutation(source, destination, sourceObjects, originalSourceOrder, originalDestinationOrder, originalObjectContainerIds);
                    return MergeStacksResult.Failure(
                        CommandResultStatus.Rejected,
                        MergeStacksError.SourceContainerRemovalFailed);
                }
                catch (InvalidOperationException)
                {
                    RestoreMutation(source, destination, sourceObjects, originalSourceOrder, originalDestinationOrder, originalObjectContainerIds);
                    return MergeStacksResult.Failure(
                        CommandResultStatus.Rejected,
                        MergeStacksError.SourceContainerRemovalFailed);
                }
                catch (KeyNotFoundException)
                {
                    RestoreMutation(source, destination, sourceObjects, originalSourceOrder, originalDestinationOrder, originalObjectContainerIds);
                    return MergeStacksResult.Failure(
                        CommandResultStatus.Rejected,
                        MergeStacksError.SourceContainerRemovalFailed);
                }

                long revision = matchState.AdvanceRevision();
                return MergeStacksResult.Accepted(revision);
            }
            catch
            {
                if (matchState.Containers.ContainsKey(source.Id))
                {
                    RestoreMutation(source, destination, sourceObjects, originalSourceOrder, originalDestinationOrder, originalObjectContainerIds);
                }

                throw;
            }
        }

        private static bool CanRemoveAfterTransfer(MatchState matchState, ContainerId sourceContainerId)
        {
            foreach (SeatState seat in matchState.Seats.Values)
            {
                if (seat.HandContainerId == sourceContainerId || seat.Console.ContainsSlot(sourceContainerId))
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<TabletopObjectId, ContainerId> CaptureObjectContainerIds(
            IReadOnlyDictionary<TabletopObjectId, TabletopObjectState> sourceObjects)
        {
            Dictionary<TabletopObjectId, ContainerId> originalObjectContainerIds =
                new Dictionary<TabletopObjectId, ContainerId>();

            foreach (KeyValuePair<TabletopObjectId, TabletopObjectState> pair in sourceObjects)
            {
                originalObjectContainerIds.Add(pair.Key, pair.Value.ContainerId);
            }

            return originalObjectContainerIds;
        }

        private static void RestoreMutation(
            ContainerState source,
            ContainerState destination,
            IReadOnlyDictionary<TabletopObjectId, TabletopObjectState> movedObjects,
            IReadOnlyList<TabletopObjectId> originalSourceOrder,
            IReadOnlyList<TabletopObjectId> originalDestinationOrder,
            IReadOnlyDictionary<TabletopObjectId, ContainerId> originalObjectContainerIds)
        {
            if (source.Count == 0 && originalSourceOrder.All(destination.Contains))
            {
                ContainerBatchTransferService transferService = new ContainerBatchTransferService();
                transferService.TransferOrdered(
                    movedObjects,
                    destination,
                    source,
                    originalSourceOrder);
            }

            if (source.Count == originalSourceOrder.Count)
            {
                source.ReplaceOrder(originalSourceOrder);
            }

            if (destination.Count == originalDestinationOrder.Count)
            {
                destination.ReplaceOrder(originalDestinationOrder);
            }

            foreach (KeyValuePair<TabletopObjectId, ContainerId> pair in originalObjectContainerIds)
            {
                movedObjects[pair.Key].SetContainer(pair.Value);
            }
        }

        private static MergeStacksResult MapTransferFailure(ContainerTransferError error)
        {
            switch (error)
            {
                case ContainerTransferError.DestinationFull:
                    return MergeStacksResult.Failure(
                        CommandResultStatus.Rejected,
                        MergeStacksError.DestinationCapacityExceeded);

                case ContainerTransferError.ObjectStateMissing:
                case ContainerTransferError.ObjectStateRequired:
                    return MergeStacksResult.Failure(
                        CommandResultStatus.Rejected,
                        MergeStacksError.ObjectMissing);

                case ContainerTransferError.SourceContainerMismatch:
                    return MergeStacksResult.Failure(
                        CommandResultStatus.Rejected,
                        MergeStacksError.ObjectContainerMismatch);

                case ContainerTransferError.SameContainer:
                    return MergeStacksResult.Failure(CommandResultStatus.Invalid, MergeStacksError.SameStack);

                default:
                    return MergeStacksResult.Failure(
                        CommandResultStatus.Rejected,
                        MergeStacksError.SourceContainerRemovalFailed);
            }
        }
    }
}
