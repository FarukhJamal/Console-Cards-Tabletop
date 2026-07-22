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

            destination.InsertObject(objectState.Id, actualDestinationIndex);
            objectState.SetContainer(destination.Id);

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

            source.RemoveObject(objectState.Id);
            destination.InsertObject(objectState.Id, actualDestinationIndex);
            objectState.SetContainer(destination.Id);

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

            source.RemoveObject(objectState.Id);
            objectState.SetContainer(ContainerId.Empty);

            return ContainerTransferResult.Success(-1);
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
