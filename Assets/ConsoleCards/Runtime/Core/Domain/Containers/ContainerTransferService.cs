using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;

namespace ConsoleCards.Core.Domain.Containers
{
    public sealed class ContainerTransferService
    {
        public ContainerTransferResult PlaceIntoContainer(
            TabletopObjectState objectState,
            ContainerState destination,
            int destinationIndex = -1)
        {
            if (objectState == null)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.ObjectStateRequired);
            }

            if (destination == null)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.DestinationRequired);
            }

            if (objectState.Id.IsEmpty)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.ObjectIdEmpty);
            }

            if (objectState.ContainerId != ContainerId.Empty)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.SourceContainerMismatch);
            }

            if (destination.Contains(objectState.Id))
            {
                return ContainerTransferResult.Failure(ContainerTransferError.ObjectAlreadyContained);
            }

            if (destination.IsFull)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.DestinationFull);
            }

            if (!TryResolveDestinationIndex(destination, destinationIndex, out int actualDestinationIndex))
            {
                return ContainerTransferResult.Failure(ContainerTransferError.InvalidDestinationIndex);
            }

            ContainerId originalContainerId = objectState.ContainerId;

            try
            {
                destination.InsertObject(objectState.Id, actualDestinationIndex);
                objectState.SetContainer(destination.Id);
            }
            catch
            {
                if (destination.Contains(objectState.Id))
                {
                    destination.RemoveObject(objectState.Id);
                }

                objectState.SetContainer(originalContainerId);
                throw;
            }

            return ContainerTransferResult.Success(actualDestinationIndex);
        }

        public ContainerTransferResult MoveBetweenContainers(
            TabletopObjectState objectState,
            ContainerState source,
            ContainerState destination,
            int destinationIndex = -1)
        {
            if (objectState == null)
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

            if (objectState.Id.IsEmpty)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.ObjectIdEmpty);
            }

            if (objectState.ContainerId != source.Id)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.SourceContainerMismatch);
            }

            if (!source.Contains(objectState.Id))
            {
                return ContainerTransferResult.Failure(ContainerTransferError.SourceDoesNotContainObject);
            }

            if (destination.Contains(objectState.Id))
            {
                return ContainerTransferResult.Failure(ContainerTransferError.ObjectAlreadyContained);
            }

            if (destination.IsFull)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.DestinationFull);
            }

            if (!TryResolveDestinationIndex(destination, destinationIndex, out int actualDestinationIndex))
            {
                return ContainerTransferResult.Failure(ContainerTransferError.InvalidDestinationIndex);
            }

            IReadOnlyList<TabletopObjectId> originalSourceOrder = CopyOrder(source);
            IReadOnlyList<TabletopObjectId> originalDestinationOrder = CopyOrder(destination);
            ContainerId originalContainerId = objectState.ContainerId;

            try
            {
                source.RemoveObject(objectState.Id);
                destination.InsertObject(objectState.Id, actualDestinationIndex);
                objectState.SetContainer(destination.Id);
            }
            catch
            {
                RestoreContainerOrder(source, originalSourceOrder);
                RestoreContainerOrder(destination, originalDestinationOrder);
                objectState.SetContainer(originalContainerId);
                throw;
            }

            return ContainerTransferResult.Success(actualDestinationIndex);
        }

        public ContainerTransferResult RemoveFromContainer(
            TabletopObjectState objectState,
            ContainerState source)
        {
            if (objectState == null)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.ObjectStateRequired);
            }

            if (source == null)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.SourceRequired);
            }

            if (objectState.Id.IsEmpty)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.ObjectIdEmpty);
            }

            if (objectState.ContainerId != source.Id)
            {
                return ContainerTransferResult.Failure(ContainerTransferError.SourceContainerMismatch);
            }

            if (!source.Contains(objectState.Id))
            {
                return ContainerTransferResult.Failure(ContainerTransferError.SourceDoesNotContainObject);
            }

            IReadOnlyList<TabletopObjectId> originalSourceOrder = CopyOrder(source);
            ContainerId originalContainerId = objectState.ContainerId;

            try
            {
                source.RemoveObject(objectState.Id);
                objectState.SetContainer(ContainerId.Empty);
            }
            catch
            {
                RestoreContainerOrder(source, originalSourceOrder);
                objectState.SetContainer(originalContainerId);
                throw;
            }

            return ContainerTransferResult.Success(-1);
        }

        private static IReadOnlyList<TabletopObjectId> CopyOrder(ContainerState container)
        {
            return new List<TabletopObjectId>(container.ObjectIds);
        }

        private static void RestoreContainerOrder(
            ContainerState container,
            IReadOnlyList<TabletopObjectId> originalOrder)
        {
            HashSet<TabletopObjectId> originalMembers = new HashSet<TabletopObjectId>(originalOrder);
            List<TabletopObjectId> currentOrder = new List<TabletopObjectId>(container.ObjectIds);

            foreach (TabletopObjectId objectId in currentOrder)
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

        private static bool TryResolveDestinationIndex(
            ContainerState destination,
            int requestedDestinationIndex,
            out int actualDestinationIndex)
        {
            if (requestedDestinationIndex == -1)
            {
                actualDestinationIndex = destination.Count;
                return true;
            }

            if (requestedDestinationIndex < 0 || requestedDestinationIndex > destination.Count)
            {
                actualDestinationIndex = -1;
                return false;
            }

            actualDestinationIndex = requestedDestinationIndex;
            return true;
        }
    }
}
