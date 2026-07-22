using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class ContainerTransferServiceTests
    {
        [Test]
        public void PlaceIntoContainer_WhenAppending_SucceedsAndAppends()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState destination = CreateContainer();
            TabletopObjectState firstObject = CreateObject();
            TabletopObjectState secondObject = CreateObject();

            service.PlaceIntoContainer(firstObject, destination);
            ContainerTransferResult result = service.PlaceIntoContainer(secondObject, destination);

            Assert.That(result, Is.EqualTo(ContainerTransferResult.Success(1)));
            Assert.That(destination.ObjectIds, Is.EqualTo(new[] { firstObject.Id, secondObject.Id }));
        }

        [Test]
        public void PlaceIntoContainer_WhenExplicitIndexIsValid_InsertsAtIndex()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState destination = CreateContainer();
            TabletopObjectState firstObject = CreateObject();
            TabletopObjectState secondObject = CreateObject();
            TabletopObjectState insertedObject = CreateObject();
            service.PlaceIntoContainer(firstObject, destination);
            service.PlaceIntoContainer(secondObject, destination);

            ContainerTransferResult result = service.PlaceIntoContainer(insertedObject, destination, 1);

            Assert.That(result, Is.EqualTo(ContainerTransferResult.Success(1)));
            Assert.That(destination.ObjectIds, Is.EqualTo(new[] { firstObject.Id, insertedObject.Id, secondObject.Id }));
        }

        [Test]
        public void PlaceIntoContainer_WhenSuccessful_UpdatesObjectContainerId()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState destination = CreateContainer();
            TabletopObjectState objectState = CreateObject();

            service.PlaceIntoContainer(objectState, destination);

            Assert.That(objectState.ContainerId, Is.EqualTo(destination.Id));
        }

        [Test]
        public void PlaceIntoContainer_WhenObjectAlreadyAssigned_ReturnsSourceContainerMismatch()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState destination = CreateContainer();
            TabletopObjectState objectState = CreateObject(containerId: ContainerId.New());

            ContainerTransferResult result = service.PlaceIntoContainer(objectState, destination);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.SourceContainerMismatch));
        }

        [Test]
        public void PlaceIntoContainer_WhenDestinationAlreadyContainsObject_ReturnsObjectAlreadyContained()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState destination = CreateContainer();
            TabletopObjectId objectId = TabletopObjectId.New();
            service.PlaceIntoContainer(CreateObject(id: objectId), destination);
            TabletopObjectState duplicateObject = CreateObject(id: objectId);

            ContainerTransferResult result = service.PlaceIntoContainer(duplicateObject, destination);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.ObjectAlreadyContained));
        }

        [Test]
        public void PlaceIntoContainer_WhenDestinationIsFull_ReturnsDestinationFull()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState destination = CreateContainer(capacity: 1);
            service.PlaceIntoContainer(CreateObject(), destination);

            ContainerTransferResult result = service.PlaceIntoContainer(CreateObject(), destination);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.DestinationFull));
        }

        [Test]
        public void PlaceIntoContainer_WhenIndexIsInvalid_ReturnsInvalidDestinationIndex()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState destination = CreateContainer();

            ContainerTransferResult result = service.PlaceIntoContainer(CreateObject(), destination, 1);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.InvalidDestinationIndex));
        }

        [Test]
        public void PlaceIntoContainer_WhenFailure_LeavesStateUnchanged()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState destination = CreateContainer();
            TabletopObjectState existingObject = CreateObject();
            TabletopObjectState failedObject = CreateObject();
            service.PlaceIntoContainer(existingObject, destination);

            ContainerTransferResult result = service.PlaceIntoContainer(failedObject, destination, 3);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.InvalidDestinationIndex));
            Assert.That(destination.ObjectIds, Is.EqualTo(new[] { existingObject.Id }));
            Assert.That(failedObject.ContainerId, Is.EqualTo(ContainerId.Empty));
        }

        [Test]
        public void MoveBetweenContainers_WhenValid_MovesAndUpdatesAllStates()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState source = CreateContainer();
            ContainerState destination = CreateContainer();
            TabletopObjectState objectState = CreateObject();
            service.PlaceIntoContainer(objectState, source);

            ContainerTransferResult result = service.MoveBetweenContainers(objectState, source, destination);

            Assert.That(result, Is.EqualTo(ContainerTransferResult.Success(0)));
            Assert.That(source.ObjectIds, Is.Empty);
            Assert.That(destination.ObjectIds, Is.EqualTo(new[] { objectState.Id }));
            Assert.That(objectState.ContainerId, Is.EqualTo(destination.Id));
        }

        [Test]
        public void MoveBetweenContainers_PreservesDestinationInsertionOrder()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState source = CreateContainer();
            ContainerState destination = CreateContainer();
            TabletopObjectState firstDestinationObject = CreateObject();
            TabletopObjectState secondDestinationObject = CreateObject();
            TabletopObjectState movedObject = CreateObject();
            service.PlaceIntoContainer(firstDestinationObject, destination);
            service.PlaceIntoContainer(secondDestinationObject, destination);
            service.PlaceIntoContainer(movedObject, source);

            service.MoveBetweenContainers(movedObject, source, destination, 1);

            Assert.That(destination.ObjectIds, Is.EqualTo(new[] { firstDestinationObject.Id, movedObject.Id, secondDestinationObject.Id }));
        }

        [Test]
        public void MoveBetweenContainers_WhenSourceMismatch_ReturnsSourceContainerMismatch()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState source = CreateContainer();
            ContainerState destination = CreateContainer();
            TabletopObjectState objectState = CreateObject();
            service.PlaceIntoContainer(objectState, source);
            objectState.SetContainer(ContainerId.New());

            ContainerTransferResult result = service.MoveBetweenContainers(objectState, source, destination);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.SourceContainerMismatch));
        }

        [Test]
        public void MoveBetweenContainers_WhenSourceDoesNotContainObject_ReturnsSourceDoesNotContainObject()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState source = CreateContainer();
            ContainerState destination = CreateContainer();
            TabletopObjectState objectState = CreateObject(containerId: source.Id);

            ContainerTransferResult result = service.MoveBetweenContainers(objectState, source, destination);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.SourceDoesNotContainObject));
        }

        [Test]
        public void MoveBetweenContainers_WhenSourceAndDestinationAreSame_ReturnsSameContainer()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState source = CreateContainer();
            TabletopObjectState objectState = CreateObject();
            service.PlaceIntoContainer(objectState, source);

            ContainerTransferResult result = service.MoveBetweenContainers(objectState, source, source);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.SameContainer));
        }

        [Test]
        public void MoveBetweenContainers_WhenDestinationIsFull_ReturnsDestinationFull()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState source = CreateContainer();
            ContainerState destination = CreateContainer(capacity: 1);
            TabletopObjectState movedObject = CreateObject();
            TabletopObjectState destinationObject = CreateObject();
            service.PlaceIntoContainer(movedObject, source);
            service.PlaceIntoContainer(destinationObject, destination);

            ContainerTransferResult result = service.MoveBetweenContainers(movedObject, source, destination);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.DestinationFull));
        }

        [Test]
        public void MoveBetweenContainers_WhenFailure_LeavesStatesUnchanged()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState source = CreateContainer();
            ContainerState destination = CreateContainer();
            TabletopObjectState movedObject = CreateObject();
            TabletopObjectState destinationObject = CreateObject();
            service.PlaceIntoContainer(movedObject, source);
            service.PlaceIntoContainer(destinationObject, destination);

            ContainerTransferResult result = service.MoveBetweenContainers(movedObject, source, destination, 3);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.InvalidDestinationIndex));
            Assert.That(source.ObjectIds, Is.EqualTo(new[] { movedObject.Id }));
            Assert.That(destination.ObjectIds, Is.EqualTo(new[] { destinationObject.Id }));
            Assert.That(movedObject.ContainerId, Is.EqualTo(source.Id));
        }

        [Test]
        public void RemoveFromContainer_WhenValid_RemovesAndClearsContainerId()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState source = CreateContainer();
            TabletopObjectState objectState = CreateObject();
            service.PlaceIntoContainer(objectState, source);

            ContainerTransferResult result = service.RemoveFromContainer(objectState, source);

            Assert.That(result, Is.EqualTo(ContainerTransferResult.Success(-1)));
            Assert.That(source.ObjectIds, Is.Empty);
            Assert.That(objectState.ContainerId, Is.EqualTo(ContainerId.Empty));
        }

        [Test]
        public void RemoveFromContainer_WhenSourceMismatch_ReturnsSourceContainerMismatch()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState source = CreateContainer();
            TabletopObjectState objectState = CreateObject(containerId: ContainerId.New());

            ContainerTransferResult result = service.RemoveFromContainer(objectState, source);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.SourceContainerMismatch));
        }

        [Test]
        public void RemoveFromContainer_WhenSourceDoesNotContainObject_ReturnsSourceDoesNotContainObject()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState source = CreateContainer();
            TabletopObjectState objectState = CreateObject(containerId: source.Id);

            ContainerTransferResult result = service.RemoveFromContainer(objectState, source);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.SourceDoesNotContainObject));
        }

        [Test]
        public void RemoveFromContainer_WhenFailure_LeavesStateUnchanged()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState source = CreateContainer();
            TabletopObjectState objectState = CreateObject();
            service.PlaceIntoContainer(objectState, source);
            objectState.SetContainer(ContainerId.New());

            ContainerTransferResult result = service.RemoveFromContainer(objectState, source);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.SourceContainerMismatch));
            Assert.That(source.ObjectIds, Is.EqualTo(new[] { objectState.Id }));
            Assert.That(objectState.ContainerId, Is.Not.EqualTo(ContainerId.Empty));
        }

        [Test]
        public void PlaceIntoContainer_WhenObjectStateIsNull_ReturnsObjectStateRequired()
        {
            ContainerTransferService service = new ContainerTransferService();

            ContainerTransferResult result = service.PlaceIntoContainer(null, CreateContainer());

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.ObjectStateRequired));
        }

        [Test]
        public void PlaceIntoContainer_WhenDestinationIsNull_ReturnsDestinationRequired()
        {
            ContainerTransferService service = new ContainerTransferService();

            ContainerTransferResult result = service.PlaceIntoContainer(CreateObject(), null);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.DestinationRequired));
        }

        [Test]
        public void MoveBetweenContainers_WhenObjectStateIsNull_ReturnsObjectStateRequired()
        {
            ContainerTransferService service = new ContainerTransferService();

            ContainerTransferResult result = service.MoveBetweenContainers(null, CreateContainer(), CreateContainer());

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.ObjectStateRequired));
        }

        [Test]
        public void MoveBetweenContainers_WhenSourceIsNull_ReturnsSourceRequired()
        {
            ContainerTransferService service = new ContainerTransferService();

            ContainerTransferResult result = service.MoveBetweenContainers(CreateObject(), null, CreateContainer());

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.SourceRequired));
        }

        [Test]
        public void MoveBetweenContainers_WhenDestinationIsNull_ReturnsDestinationRequired()
        {
            ContainerTransferService service = new ContainerTransferService();

            ContainerTransferResult result = service.MoveBetweenContainers(CreateObject(), CreateContainer(), null);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.DestinationRequired));
        }

        [Test]
        public void RemoveFromContainer_WhenObjectStateIsNull_ReturnsObjectStateRequired()
        {
            ContainerTransferService service = new ContainerTransferService();

            ContainerTransferResult result = service.RemoveFromContainer(null, CreateContainer());

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.ObjectStateRequired));
        }

        [Test]
        public void RemoveFromContainer_WhenSourceIsNull_ReturnsSourceRequired()
        {
            ContainerTransferService service = new ContainerTransferService();

            ContainerTransferResult result = service.RemoveFromContainer(CreateObject(), null);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.SourceRequired));
        }

        private static ContainerState CreateContainer(int capacity = 0)
        {
            return new ContainerState(
                ContainerId.New(),
                ContainerKind.Generic,
                SeatId.Empty,
                ObjectVisibility.Public,
                capacity);
        }

        private static TabletopObjectState CreateObject(
            TabletopObjectId? id = null,
            ContainerId? containerId = null)
        {
            return new TabletopObjectState(
                id ?? TabletopObjectId.New(),
                ObjectDefinitionId.New(),
                TabletopObjectKind.Card,
                TabletopPose.Default,
                containerId ?? ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);
        }
    }
}
