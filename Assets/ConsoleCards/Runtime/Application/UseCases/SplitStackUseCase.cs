using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;

namespace ConsoleCards.Application.UseCases
{
    public sealed class SplitStackUseCase
    {
        public SplitStackResult Execute(MatchState matchState, SplitStackCommand command)
        {
            if (matchState == null)
            {
                return SplitStackResult.Failure(CommandResultStatus.Invalid, SplitStackError.MatchMissing);
            }

            if (command == null)
            {
                return SplitStackResult.Failure(CommandResultStatus.Invalid, SplitStackError.CommandMissing);
            }

            if (command.Context.MatchId != matchState.Id)
            {
                return SplitStackResult.Failure(CommandResultStatus.Invalid, SplitStackError.MatchMismatch);
            }

            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return SplitStackResult.Failure(CommandResultStatus.Conflict, SplitStackError.RevisionConflict);
            }

            if (command.SourceStackContainerId == command.NewStackContainerId)
            {
                return SplitStackResult.Failure(CommandResultStatus.Invalid, SplitStackError.SameStack);
            }

            if (!matchState.Containers.TryGetValue(command.SourceStackContainerId, out ContainerState source))
            {
                return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.SourceStackMissing);
            }

            if (source.Kind != ContainerKind.Stack)
            {
                return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.SourceContainerNotStack);
            }

            if (source.Count < 2)
            {
                return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.SourceStackTooSmall);
            }

            if (command.SplitSpecification.FirstMovedIndex < 1)
            {
                return SplitStackResult.Failure(CommandResultStatus.Invalid, SplitStackError.InvalidSplitIndex);
            }

            int movedCount;
            try
            {
                movedCount = command.SplitSpecification.GetMovedCount(source.Count);
            }
            catch (ArgumentOutOfRangeException)
            {
                return SplitStackResult.Failure(CommandResultStatus.Invalid, SplitStackError.InvalidSplitIndex);
            }

            if (matchState.Containers.ContainsKey(command.NewStackContainerId))
            {
                return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.NewStackAlreadyExists);
            }

            if (matchState.ContainerPlacements.ContainsKey(command.NewStackContainerId))
            {
                return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.NewStackPlacementAlreadyExists);
            }

            List<TabletopObjectId> originalSourceOrder = new List<TabletopObjectId>(source.ObjectIds);
            List<TabletopObjectId> movedObjectIds = originalSourceOrder
                .Skip(command.SplitSpecification.FirstMovedIndex)
                .Take(movedCount)
                .ToList();
            Dictionary<TabletopObjectId, TabletopObjectState> movedObjects =
                new Dictionary<TabletopObjectId, TabletopObjectState>();

            foreach (TabletopObjectId objectId in movedObjectIds)
            {
                if (!matchState.ContainsObject(objectId))
                {
                    return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.ObjectMissing);
                }

                TabletopObjectState objectState = matchState.GetObject(objectId);
                if (objectState.ContainerId != command.SourceStackContainerId)
                {
                    return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.ObjectContainerMismatch);
                }

                if (objectState.IsUserLocked)
                {
                    return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.ObjectUserLocked);
                }

                movedObjects.Add(objectId, objectState);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return SplitStackResult.Failure(CommandResultStatus.Conflict, SplitStackError.RevisionOverflow);
            }

            ContainerState newStack = new ContainerState(
                command.NewStackContainerId,
                ContainerKind.Stack,
                source.OwnerSeatId,
                source.Visibility,
                source.Capacity);
            ContainerPlacementState newPlacement = new ContainerPlacementState(
                command.NewStackContainerId,
                command.NewStackPose);
            List<TabletopObjectId> remainingObjectIds = originalSourceOrder
                .Take(command.SplitSpecification.FirstMovedIndex)
                .ToList();
            Dictionary<TabletopObjectId, ContainerId> originalObjectContainerIds =
                CaptureObjectContainerIds(movedObjects);

            try
            {
                matchState.AddEmptyPlacedContainer(newStack, newPlacement);
            }
            catch (ArgumentException)
            {
                return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.NewStackCreationFailed);
            }
            catch (InvalidOperationException)
            {
                return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.NewStackCreationFailed);
            }

            try
            {
                ContainerBatchTransferService transferService = new ContainerBatchTransferService();
                ContainerTransferResult transferResult = transferService.TransferOrdered(
                    movedObjects,
                    source,
                    newStack,
                    movedObjectIds);

                if (!transferResult.Succeeded)
                {
                    RemoveNewStack(matchState, newStack.Id);
                    return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.NewStackCreationFailed);
                }

                if (!source.ObjectIds.SequenceEqual(remainingObjectIds)
                    || !newStack.ObjectIds.SequenceEqual(movedObjectIds)
                    || !MovedObjectsReferenceNewStack(movedObjects, newStack.Id)
                    || !RemainingObjectsReferenceSource(matchState, remainingObjectIds, source.Id))
                {
                    RestoreMutation(
                        matchState,
                        source,
                        newStack,
                        movedObjects,
                        movedObjectIds,
                        originalSourceOrder,
                        originalObjectContainerIds);
                    return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.NewStackCreationFailed);
                }

                long revision = matchState.AdvanceRevision();
                return SplitStackResult.Accepted(revision);
            }
            catch
            {
                RestoreMutation(
                    matchState,
                    source,
                    newStack,
                    movedObjects,
                    movedObjectIds,
                    originalSourceOrder,
                    originalObjectContainerIds);
                throw;
            }
        }

        private static Dictionary<TabletopObjectId, ContainerId> CaptureObjectContainerIds(
            IReadOnlyDictionary<TabletopObjectId, TabletopObjectState> movedObjects)
        {
            Dictionary<TabletopObjectId, ContainerId> originalObjectContainerIds =
                new Dictionary<TabletopObjectId, ContainerId>();

            foreach (KeyValuePair<TabletopObjectId, TabletopObjectState> pair in movedObjects)
            {
                originalObjectContainerIds.Add(pair.Key, pair.Value.ContainerId);
            }

            return originalObjectContainerIds;
        }

        private static bool MovedObjectsReferenceNewStack(
            IReadOnlyDictionary<TabletopObjectId, TabletopObjectState> movedObjects,
            ContainerId newStackId)
        {
            foreach (TabletopObjectState objectState in movedObjects.Values)
            {
                if (objectState.ContainerId != newStackId)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool RemainingObjectsReferenceSource(
            MatchState matchState,
            IReadOnlyList<TabletopObjectId> remainingObjectIds,
            ContainerId sourceStackId)
        {
            foreach (TabletopObjectId objectId in remainingObjectIds)
            {
                if (matchState.GetObject(objectId).ContainerId != sourceStackId)
                {
                    return false;
                }
            }

            return true;
        }

        private static void RestoreMutation(
            MatchState matchState,
            ContainerState source,
            ContainerState newStack,
            IReadOnlyDictionary<TabletopObjectId, TabletopObjectState> movedObjects,
            IReadOnlyList<TabletopObjectId> movedObjectIds,
            IReadOnlyList<TabletopObjectId> originalSourceOrder,
            IReadOnlyDictionary<TabletopObjectId, ContainerId> originalObjectContainerIds)
        {
            if (matchState.Containers.ContainsKey(newStack.Id)
                && newStack.Count > 0
                && movedObjectIds.All(newStack.Contains))
            {
                ContainerBatchTransferService transferService = new ContainerBatchTransferService();
                transferService.TransferOrdered(
                    movedObjects,
                    newStack,
                    source,
                    movedObjectIds);
            }

            if (source.Count == originalSourceOrder.Count)
            {
                source.ReplaceOrder(originalSourceOrder);
            }

            foreach (KeyValuePair<TabletopObjectId, ContainerId> pair in originalObjectContainerIds)
            {
                movedObjects[pair.Key].SetContainer(pair.Value);
            }

            if (matchState.Containers.ContainsKey(newStack.Id) && newStack.Count == 0)
            {
                RemoveNewStack(matchState, newStack.Id);
            }
        }

        private static void RemoveNewStack(MatchState matchState, ContainerId newStackId)
        {
            if (matchState.Containers.ContainsKey(newStackId))
            {
                matchState.RemoveEmptyContainer(newStackId);
            }
        }
    }
}
