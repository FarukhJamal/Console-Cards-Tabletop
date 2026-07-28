using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;

namespace ConsoleCards.Core.Domain.Containers
{
    public sealed class ContainerBatchTransferService
    {
        public ContainerTransferResult TransferOrdered(
            IReadOnlyDictionary<TabletopObjectId, TabletopObjectState> objects,
            ContainerState source,
            ContainerState destination,
            IReadOnlyList<TabletopObjectId> objectIdsInTransferOrder)
        {
            ContainerTransferResult validationResult = ValidateTransfer(
                objects,
                source,
                destination,
                objectIdsInTransferOrder);

            if (!validationResult.Succeeded)
            {
                return validationResult;
            }

            TabletopObjectId[] originalSourceOrder = source.ObjectIds.ToArray();
            TabletopObjectId[] originalDestinationOrder = destination.ObjectIds.ToArray();
            Dictionary<TabletopObjectId, ContainerId> originalContainerIds =
                CaptureOriginalContainerIds(objects, objectIdsInTransferOrder);

            try
            {
                foreach (TabletopObjectId objectId in objectIdsInTransferOrder)
                {
                    source.RemoveObject(objectId);
                }

                int destinationIndex = destination.Count;
                foreach (TabletopObjectId objectId in objectIdsInTransferOrder)
                {
                    destination.InsertObject(objectId, destination.Count);
                    objects[objectId].SetContainer(destination.Id);
                }

                return ContainerTransferResult.Success(destinationIndex);
            }
            catch
            {
                RestoreContainerOrder(source, originalSourceOrder);
                RestoreContainerOrder(destination, originalDestinationOrder);
                RestoreObjectContainerIds(objects, originalContainerIds);
                throw;
            }
        }

        private static ContainerTransferResult ValidateTransfer(
            IReadOnlyDictionary<TabletopObjectId, TabletopObjectState> objects,
            ContainerState source,
            ContainerState destination,
            IReadOnlyList<TabletopObjectId> objectIdsInTransferOrder)
        {
            if (objects == null)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.ObjectStateRequired);
            }

            if (source == null)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.SourceRequired);
            }

            if (destination == null)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.DestinationRequired);
            }

            if (source.Id == destination.Id)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.SameContainer);
            }

            if (objectIdsInTransferOrder == null || objectIdsInTransferOrder.Count == 0)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.TransferListRequired);
            }

            if (destination.Capacity > 0
                && destination.Count + objectIdsInTransferOrder.Count > destination.Capacity)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.DestinationFull);
            }

            HashSet<TabletopObjectId> seenObjectIds = new HashSet<TabletopObjectId>();

            foreach (TabletopObjectId objectId in objectIdsInTransferOrder)
            {
                if (objectId.IsEmpty)
                {
                    return ContainerTransferResult.Failure(ContainerTransferError.ObjectIdEmpty);
                }

                if (!seenObjectIds.Add(objectId))
                {
                    return ContainerTransferResult.Failure(ContainerTransferError.DuplicateObjectId);
                }

                if (!source.Contains(objectId))
                {
                    return ContainerTransferResult.Failure(ContainerTransferError.SourceDoesNotContainObject);
                }

                if (destination.Contains(objectId))
                {
                    return ContainerTransferResult.Failure(ContainerTransferError.ObjectAlreadyContained);
                }

                if (!objects.TryGetValue(objectId, out TabletopObjectState objectState))
                {
                    return ContainerTransferResult.Failure(ContainerTransferError.ObjectStateMissing);
                }

                if (objectState.ContainerId != source.Id)
                {
                    return ContainerTransferResult.Failure(ContainerTransferError.SourceContainerMismatch);
                }
            }

            return ContainerTransferResult.Success(destination.Count);
        }

        private static Dictionary<TabletopObjectId, ContainerId> CaptureOriginalContainerIds(
            IReadOnlyDictionary<TabletopObjectId, TabletopObjectState> objects,
            IReadOnlyList<TabletopObjectId> objectIds)
        {
            Dictionary<TabletopObjectId, ContainerId> originalContainerIds = new Dictionary<TabletopObjectId, ContainerId>();

            foreach (TabletopObjectId objectId in objectIds)
            {
                originalContainerIds.Add(objectId, objects[objectId].ContainerId);
            }

            return originalContainerIds;
        }

        private static void RestoreContainerOrder(
            ContainerState container,
            IReadOnlyList<TabletopObjectId> originalOrder)
        {
            HashSet<TabletopObjectId> originalMembers = new HashSet<TabletopObjectId>(originalOrder);

            foreach (TabletopObjectId objectId in container.ObjectIds.ToArray())
            {
                if (!originalMembers.Contains(objectId))
                {
                    container.RemoveObject(objectId);
                }
            }

            for (int index = 0; index < originalOrder.Count; index++)
            {
                TabletopObjectId objectId = originalOrder[index];
                if (!container.Contains(objectId))
                {
                    container.InsertObject(objectId, index);
                }
            }

            container.ReplaceOrder(originalOrder);
        }

        private static void RestoreObjectContainerIds(
            IReadOnlyDictionary<TabletopObjectId, TabletopObjectState> objects,
            IReadOnlyDictionary<TabletopObjectId, ContainerId> originalContainerIds)
        {
            foreach (KeyValuePair<TabletopObjectId, ContainerId> pair in originalContainerIds)
            {
                objects[pair.Key].SetContainer(pair.Value);
            }
        }
    }
}
