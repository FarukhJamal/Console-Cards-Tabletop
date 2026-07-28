using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class ContainerOrderingTests
    {
        [Test]
        public void TopIndex_WhenEmpty_ReturnsMinusOne()
        {
            ContainerState container = CreateContainer();

            Assert.That(container.TopIndex, Is.EqualTo(-1));
        }

        [Test]
        public void TopIndex_WhenNonEmpty_ReturnsCountMinusOne()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out _, out _, out TabletopObjectState thirdObject);

            Assert.That(container.TopIndex, Is.EqualTo(2));
            Assert.That(container.GetObjectAt(container.TopIndex), Is.EqualTo(thirdObject.Id));
        }

        [Test]
        public void TryPeekTop_WhenEmpty_ReturnsFalseAndEmptyId()
        {
            ContainerState container = CreateContainer();

            bool result = container.TryPeekTop(out TabletopObjectId objectId);

            Assert.That(result, Is.False);
            Assert.That(objectId, Is.EqualTo(TabletopObjectId.Empty));
        }

        [Test]
        public void TryPeekTop_WhenNonEmpty_ReturnsFinalOrderedItem()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out _, out _, out TabletopObjectState thirdObject);

            bool result = container.TryPeekTop(out TabletopObjectId objectId);

            Assert.That(result, Is.True);
            Assert.That(objectId, Is.EqualTo(thirdObject.Id));
        }

        [Test]
        public void GetObjectAt_ReturnsCorrectItems()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState firstObject, out TabletopObjectState secondObject, out _);

            Assert.That(container.GetObjectAt(0), Is.EqualTo(firstObject.Id));
            Assert.That(container.GetObjectAt(1), Is.EqualTo(secondObject.Id));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void GetObjectAt_WhenIndexIsInvalid_ThrowsArgumentOutOfRangeException(int index)
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out _, out _, out _);

            Assert.That(
                () => container.GetObjectAt(index),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void IndexOf_ReturnsCurrentIndexOrMinusOne()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState firstObject, out TabletopObjectState secondObject, out _);

            Assert.That(container.IndexOf(firstObject.Id), Is.EqualTo(0));
            Assert.That(container.IndexOf(secondObject.Id), Is.EqualTo(1));
            Assert.That(container.IndexOf(TabletopObjectId.New()), Is.EqualTo(-1));
        }

        [Test]
        public void IndexOf_WhenObjectIdIsEmpty_ThrowsArgumentException()
        {
            ContainerState container = CreateContainer();

            Assert.That(
                () => container.IndexOf(TabletopObjectId.Empty),
                Throws.ArgumentException);
        }

        [Test]
        public void Reorder_WhenMovingFirstToLast_UsesFinalDestinationIndex()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState firstObject, out TabletopObjectState secondObject, out TabletopObjectState thirdObject);

            container.Reorder(0, 2);

            Assert.That(container.ObjectIds, Is.EqualTo(new[] { secondObject.Id, thirdObject.Id, firstObject.Id }));
        }

        [Test]
        public void Reorder_WhenMovingLastToFirst_UsesFinalDestinationIndex()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState firstObject, out TabletopObjectState secondObject, out TabletopObjectState thirdObject);

            container.Reorder(2, 0);

            Assert.That(container.ObjectIds, Is.EqualTo(new[] { thirdObject.Id, firstObject.Id, secondObject.Id }));
        }

        [Test]
        public void Reorder_WhenMovingMiddleForward_UsesFinalDestinationIndex()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState firstObject, out TabletopObjectState secondObject, out TabletopObjectState thirdObject, out TabletopObjectState fourthObject);

            container.Reorder(1, 3);

            Assert.That(container.ObjectIds, Is.EqualTo(new[] { firstObject.Id, thirdObject.Id, fourthObject.Id, secondObject.Id }));
        }

        [Test]
        public void Reorder_WhenMovingMiddleBackward_UsesFinalDestinationIndex()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState firstObject, out TabletopObjectState secondObject, out TabletopObjectState thirdObject, out TabletopObjectState fourthObject);

            container.Reorder(2, 1);

            Assert.That(container.ObjectIds, Is.EqualTo(new[] { firstObject.Id, thirdObject.Id, secondObject.Id, fourthObject.Id }));
        }

        [Test]
        public void Reorder_WhenMovingToSameIndex_IsNoOp()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState firstObject, out TabletopObjectState secondObject, out TabletopObjectState thirdObject);

            container.Reorder(1, 1);

            Assert.That(container.ObjectIds, Is.EqualTo(new[] { firstObject.Id, secondObject.Id, thirdObject.Id }));
        }

        [Test]
        public void Reorder_PreservesMembershipExactlyOnce()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out _, out _, out _);

            container.Reorder(0, 2);

            Assert.That(container.ObjectIds, Has.Count.EqualTo(3));
            Assert.That(container.ObjectIds, Is.Unique);
        }

        [Test]
        public void Reorder_PreservesNonOrderContainerFields()
        {
            ContainerId containerId = ContainerId.New();
            SeatId ownerSeatId = SeatId.New();
            ContainerState container = CreateContainer(
                id: containerId,
                kind: ContainerKind.Hand,
                ownerSeatId: ownerSeatId,
                visibility: ObjectVisibility.OwnerOnly,
                capacity: 5);
            AddObjects(container, out _, out _, out _);

            container.Reorder(0, 2);

            Assert.That(container.Id, Is.EqualTo(containerId));
            Assert.That(container.Kind, Is.EqualTo(ContainerKind.Hand));
            Assert.That(container.OwnerSeatId, Is.EqualTo(ownerSeatId));
            Assert.That(container.Visibility, Is.EqualTo(ObjectVisibility.OwnerOnly));
            Assert.That(container.Capacity, Is.EqualTo(5));
        }

        [TestCase(-1, 0)]
        [TestCase(3, 0)]
        [TestCase(0, -1)]
        [TestCase(0, 3)]
        public void Reorder_WhenIndexIsInvalid_PreservesOrder(int fromIndex, int toIndex)
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState firstObject, out TabletopObjectState secondObject, out TabletopObjectState thirdObject);

            Assert.That(
                () => container.Reorder(fromIndex, toIndex),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(container.ObjectIds, Is.EqualTo(new[] { firstObject.Id, secondObject.Id, thirdObject.Id }));
        }

        [Test]
        public void TransferService_WhenUsedAfterOrderingChanges_StillTransfersBetweenContainers()
        {
            ContainerTransferService service = new ContainerTransferService();
            ContainerState source = CreateContainer();
            ContainerState destination = CreateContainer();
            AddObjects(source, out TabletopObjectState firstObject, out TabletopObjectState secondObject, out TabletopObjectState thirdObject);
            source.Reorder(0, 2);

            ContainerTransferResult result = service.MoveBetweenContainers(firstObject, source, destination);

            Assert.That(result, Is.EqualTo(ContainerTransferResult.Success(0)));
            Assert.That(source.ObjectIds, Is.EqualTo(new[] { secondObject.Id, thirdObject.Id }));
            Assert.That(destination.ObjectIds, Is.EqualTo(new[] { firstObject.Id }));
        }

        private static ContainerState CreateContainer(
            ContainerId? id = null,
            ContainerKind kind = ContainerKind.Generic,
            SeatId? ownerSeatId = null,
            ObjectVisibility visibility = ObjectVisibility.Public,
            int capacity = 0)
        {
            return new ContainerState(
                id ?? ContainerId.New(),
                kind,
                ownerSeatId ?? SeatId.Empty,
                visibility,
                capacity);
        }

        private static void AddObjects(
            ContainerState container,
            out TabletopObjectState firstObject,
            out TabletopObjectState secondObject,
            out TabletopObjectState thirdObject)
        {
            ContainerTransferService service = new ContainerTransferService();

            firstObject = CreateObject();
            secondObject = CreateObject();
            thirdObject = CreateObject();

            service.PlaceIntoContainer(firstObject, container);
            service.PlaceIntoContainer(secondObject, container);
            service.PlaceIntoContainer(thirdObject, container);
        }

        private static void AddObjects(
            ContainerState container,
            out TabletopObjectState firstObject,
            out TabletopObjectState secondObject,
            out TabletopObjectState thirdObject,
            out TabletopObjectState fourthObject)
        {
            ContainerTransferService service = new ContainerTransferService();

            firstObject = CreateObject();
            secondObject = CreateObject();
            thirdObject = CreateObject();
            fourthObject = CreateObject();

            service.PlaceIntoContainer(firstObject, container);
            service.PlaceIntoContainer(secondObject, container);
            service.PlaceIntoContainer(thirdObject, container);
            service.PlaceIntoContainer(fourthObject, container);
        }

        private static TabletopObjectState CreateObject()
        {
            return new TabletopObjectState(
                TabletopObjectId.New(),
                ObjectDefinitionId.New(),
                TabletopObjectKind.Card,
                TabletopPose.Default,
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);
        }
    }
}
