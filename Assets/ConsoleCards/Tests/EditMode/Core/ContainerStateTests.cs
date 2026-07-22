using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class ContainerStateTests
    {
        [Test]
        public void Constructor_StoresSuppliedValues()
        {
            ContainerId id = ContainerId.New();
            SeatId ownerSeatId = SeatId.New();

            ContainerState state = new ContainerState(
                id,
                ContainerKind.Hand,
                ownerSeatId,
                ObjectVisibility.OwnerOnly,
                5);

            Assert.That(state.Id, Is.EqualTo(id));
            Assert.That(state.Kind, Is.EqualTo(ContainerKind.Hand));
            Assert.That(state.OwnerSeatId, Is.EqualTo(ownerSeatId));
            Assert.That(state.Visibility, Is.EqualTo(ObjectVisibility.OwnerOnly));
            Assert.That(state.Capacity, Is.EqualTo(5));
        }

        [Test]
        public void Constructor_WhenIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => CreateContainer(id: ContainerId.Empty),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenOwnerSeatIdIsEmpty_AcceptsValue()
        {
            ContainerState state = CreateContainer(ownerSeatId: SeatId.Empty);

            Assert.That(state.OwnerSeatId, Is.EqualTo(SeatId.Empty));
        }

        [Test]
        public void Constructor_WhenCapacityIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => CreateContainer(capacity: -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void CapacityZero_IsUnlimited()
        {
            ContainerState state = CreateContainer(capacity: 0);
            ContainerTransferService service = new ContainerTransferService();

            service.PlaceIntoContainer(CreateObject(), state);
            service.PlaceIntoContainer(CreateObject(), state);

            Assert.That(state.Count, Is.EqualTo(2));
            Assert.That(state.IsFull, Is.False);
        }

        [Test]
        public void Constructor_StartsEmpty()
        {
            ContainerState state = CreateContainer(capacity: 1);

            Assert.That(state.Count, Is.EqualTo(0));
            Assert.That(state.IsFull, Is.False);
            Assert.That(state.ObjectIds, Is.Empty);
        }

        [Test]
        public void FixedCapacityContainer_BecomesFullAtCapacity()
        {
            ContainerState state = CreateContainer(capacity: 1);
            ContainerTransferService service = new ContainerTransferService();

            service.PlaceIntoContainer(CreateObject(), state);

            Assert.That(state.Count, Is.EqualTo(1));
            Assert.That(state.IsFull, Is.True);
        }

        [Test]
        public void ObjectIds_CannotBeMutatedExternally()
        {
            ContainerState state = CreateContainer();
            ContainerTransferService service = new ContainerTransferService();
            TabletopObjectState objectState = CreateObject();
            service.PlaceIntoContainer(objectState, state);

            Assert.That(state.ObjectIds as List<TabletopObjectId>, Is.Null);

            IList<TabletopObjectId> listView = state.ObjectIds as IList<TabletopObjectId>;
            Assert.That(listView, Is.Not.Null);
            Assert.That(
                () => listView.Add(TabletopObjectId.New()),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(state.ObjectIds, Is.EqualTo(new[] { objectState.Id }));
        }

        [Test]
        public void ContainsAndIndexOf_ReportCurrentMembership()
        {
            ContainerState state = CreateContainer();
            ContainerTransferService service = new ContainerTransferService();
            TabletopObjectState firstObject = CreateObject();
            TabletopObjectState secondObject = CreateObject();

            service.PlaceIntoContainer(firstObject, state);
            service.PlaceIntoContainer(secondObject, state);

            Assert.That(state.Contains(firstObject.Id), Is.True);
            Assert.That(state.Contains(TabletopObjectId.New()), Is.False);
            Assert.That(state.IndexOf(firstObject.Id), Is.EqualTo(0));
            Assert.That(state.IndexOf(secondObject.Id), Is.EqualTo(1));
            Assert.That(state.IndexOf(TabletopObjectId.New()), Is.EqualTo(-1));
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
                ownerSeatId ?? SeatId.New(),
                visibility,
                capacity);
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
