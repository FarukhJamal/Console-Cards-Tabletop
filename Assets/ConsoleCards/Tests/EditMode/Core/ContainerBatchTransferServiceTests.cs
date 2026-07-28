using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class ContainerBatchTransferServiceTests
    {
        [Test]
        public void TransferOrdered_WhenValid_AppendsExactTransferOrder()
        {
            TransferFixture fixture = CreateFixture();

            ContainerTransferResult result = Execute(fixture, fixture.SourceObjects[2], fixture.SourceObjects[1]);

            Assert.That(result, Is.EqualTo(ContainerTransferResult.Success(1)));
            Assert.That(fixture.Source.ObjectIds, Is.EqualTo(new[] { fixture.SourceObjects[0].Id }));
            Assert.That(fixture.Destination.ObjectIds, Is.EqualTo(new[]
            {
                fixture.DestinationObject.Id,
                fixture.SourceObjects[2].Id,
                fixture.SourceObjects[1].Id
            }));
            Assert.That(fixture.SourceObjects[2].ContainerId, Is.EqualTo(fixture.Destination.Id));
            Assert.That(fixture.SourceObjects[1].ContainerId, Is.EqualTo(fixture.Destination.Id));
        }

        [Test]
        public void TransferOrdered_WhenMultipleObjectsAreValid_SucceedsAtomically()
        {
            TransferFixture fixture = CreateFixture();

            ContainerTransferResult result = Execute(fixture, fixture.SourceObjects[2], fixture.SourceObjects[1], fixture.SourceObjects[0]);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Source.ObjectIds, Is.Empty);
            Assert.That(fixture.Destination.ObjectIds, Is.EqualTo(new[]
            {
                fixture.DestinationObject.Id,
                fixture.SourceObjects[2].Id,
                fixture.SourceObjects[1].Id,
                fixture.SourceObjects[0].Id
            }));
        }

        [Test]
        public void TransferOrdered_WhenObjectsDictionaryIsNull_ReturnsObjectStateRequired()
        {
            TransferFixture fixture = CreateFixture();
            ContainerBatchTransferService service = new ContainerBatchTransferService();

            ContainerTransferResult result = service.TransferOrdered(
                null,
                fixture.Source,
                fixture.Destination,
                new[] { fixture.SourceObjects[0].Id });

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.ObjectStateRequired));
        }

        [Test]
        public void TransferOrdered_WhenSourceIsNull_ReturnsSourceRequired()
        {
            TransferFixture fixture = CreateFixture();
            ContainerBatchTransferService service = new ContainerBatchTransferService();

            ContainerTransferResult result = service.TransferOrdered(
                fixture.Objects,
                null,
                fixture.Destination,
                new[] { fixture.SourceObjects[0].Id });

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.SourceRequired));
        }

        [Test]
        public void TransferOrdered_WhenDestinationIsNull_ReturnsDestinationRequired()
        {
            TransferFixture fixture = CreateFixture();
            ContainerBatchTransferService service = new ContainerBatchTransferService();

            ContainerTransferResult result = service.TransferOrdered(
                fixture.Objects,
                fixture.Source,
                null,
                new[] { fixture.SourceObjects[0].Id });

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.DestinationRequired));
        }

        [Test]
        public void TransferOrdered_WhenSourceAndDestinationMatch_ReturnsSameContainer()
        {
            TransferFixture fixture = CreateFixture();
            ContainerBatchTransferService service = new ContainerBatchTransferService();

            ContainerTransferResult result = service.TransferOrdered(
                fixture.Objects,
                fixture.Source,
                fixture.Source,
                new[] { fixture.SourceObjects[0].Id });

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.SameContainer));
        }

        [Test]
        public void TransferOrdered_WhenTransferListIsNull_ReturnsTransferListRequired()
        {
            TransferFixture fixture = CreateFixture();
            ContainerBatchTransferService service = new ContainerBatchTransferService();

            ContainerTransferResult result = service.TransferOrdered(
                fixture.Objects,
                fixture.Source,
                fixture.Destination,
                null);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.TransferListRequired));
        }

        [Test]
        public void TransferOrdered_WhenTransferListIsEmpty_ReturnsTransferListRequired()
        {
            TransferFixture fixture = CreateFixture();
            ContainerBatchTransferService service = new ContainerBatchTransferService();

            ContainerTransferResult result = service.TransferOrdered(
                fixture.Objects,
                fixture.Source,
                fixture.Destination,
                new TabletopObjectId[0]);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.TransferListRequired));
        }

        [Test]
        public void TransferOrdered_WhenTransferIdIsEmpty_ReturnsObjectIdEmpty()
        {
            TransferFixture fixture = CreateFixture();

            ContainerTransferResult result = Execute(fixture, TabletopObjectId.Empty);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.ObjectIdEmpty));
        }

        [Test]
        public void TransferOrdered_WhenTransferIdIsDuplicate_ReturnsDuplicateObjectId()
        {
            TransferFixture fixture = CreateFixture();

            ContainerTransferResult result = Execute(fixture, fixture.SourceObjects[0], fixture.SourceObjects[0]);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.DuplicateObjectId));
        }

        [Test]
        public void TransferOrdered_WhenSourceDoesNotContainObject_ReturnsSourceDoesNotContainObject()
        {
            TransferFixture fixture = CreateFixture();
            TabletopObjectState unknownObject = CreateObject(containerId: fixture.Source.Id);
            fixture.Objects.Add(unknownObject.Id, unknownObject);

            ContainerTransferResult result = Execute(fixture, unknownObject);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.SourceDoesNotContainObject));
        }

        [Test]
        public void TransferOrdered_WhenDestinationAlreadyContainsObject_ReturnsObjectAlreadyContained()
        {
            TransferFixture fixture = CreateFixture();
            PlaceDestinationObjectIntoSourceToo(fixture);

            ContainerTransferResult result = Execute(fixture, fixture.DestinationObject);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.ObjectAlreadyContained));
        }

        [Test]
        public void TransferOrdered_WhenObjectStateIsMissing_ReturnsObjectStateMissing()
        {
            TransferFixture fixture = CreateFixture();
            fixture.Objects.Remove(fixture.SourceObjects[0].Id);

            ContainerTransferResult result = Execute(fixture, fixture.SourceObjects[0]);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.ObjectStateMissing));
        }

        [Test]
        public void TransferOrdered_WhenObjectContainerMismatches_ReturnsSourceContainerMismatch()
        {
            TransferFixture fixture = CreateFixture();
            fixture.SourceObjects[0].SetContainer(ContainerId.New());

            ContainerTransferResult result = Execute(fixture, fixture.SourceObjects[0]);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.SourceContainerMismatch));
        }

        [Test]
        public void TransferOrdered_WhenDestinationCapacityWouldBeExceeded_ReturnsDestinationFull()
        {
            TransferFixture fixture = CreateFixture(destinationCapacity: 2);

            ContainerTransferResult result = Execute(fixture, fixture.SourceObjects[2], fixture.SourceObjects[1]);

            Assert.That(result.Error, Is.EqualTo(ContainerTransferError.DestinationFull));
        }

        [TestCase(BatchFailureScenario.EmptyId)]
        [TestCase(BatchFailureScenario.DuplicateId)]
        [TestCase(BatchFailureScenario.SourceMembershipMissing)]
        [TestCase(BatchFailureScenario.DestinationAlreadyContains)]
        [TestCase(BatchFailureScenario.ObjectStateMissing)]
        [TestCase(BatchFailureScenario.ObjectContainerMismatch)]
        [TestCase(BatchFailureScenario.CapacityExceeded)]
        [TestCase(BatchFailureScenario.LaterItemInvalid)]
        public void TransferOrdered_WhenFailureOccurs_PreservesAllState(BatchFailureScenario scenario)
        {
            BatchFailureFixture failure = CreateFailureFixture(scenario);
            BatchSnapshot before = BatchSnapshot.Capture(failure.Fixture);

            ContainerTransferResult result = failure.Execute();

            Assert.That(result.Succeeded, Is.False);
            before.AssertMatches(failure.Fixture);
        }

        [Test]
        public void TransferOrdered_WhenSuccessful_PreservesSourceDestinationAndObjectIdentity()
        {
            TransferFixture fixture = CreateFixture();
            ContainerState source = fixture.Source;
            ContainerState destination = fixture.Destination;
            TabletopObjectState transferredObject = fixture.SourceObjects[0];

            Execute(fixture, transferredObject);

            Assert.That(fixture.Source, Is.SameAs(source));
            Assert.That(fixture.Destination, Is.SameAs(destination));
            Assert.That(fixture.Objects[transferredObject.Id], Is.SameAs(transferredObject));
        }

        private static ContainerTransferResult Execute(
            TransferFixture fixture,
            params TabletopObjectState[] objects)
        {
            TabletopObjectId[] objectIds = new TabletopObjectId[objects.Length];
            for (int index = 0; index < objects.Length; index++)
            {
                objectIds[index] = objects[index].Id;
            }

            return Execute(fixture, objectIds);
        }

        private static ContainerTransferResult Execute(
            TransferFixture fixture,
            params TabletopObjectId[] objectIds)
        {
            ContainerBatchTransferService service = new ContainerBatchTransferService();
            return service.TransferOrdered(fixture.Objects, fixture.Source, fixture.Destination, objectIds);
        }

        private static BatchFailureFixture CreateFailureFixture(BatchFailureScenario scenario)
        {
            TransferFixture fixture = scenario == BatchFailureScenario.CapacityExceeded
                ? CreateFixture(destinationCapacity: 2)
                : CreateFixture();

            switch (scenario)
            {
                case BatchFailureScenario.EmptyId:
                    return new BatchFailureFixture(fixture, new[] { TabletopObjectId.Empty });

                case BatchFailureScenario.DuplicateId:
                    return new BatchFailureFixture(fixture, new[]
                    {
                        fixture.SourceObjects[2].Id,
                        fixture.SourceObjects[2].Id
                    });

                case BatchFailureScenario.SourceMembershipMissing:
                {
                    TabletopObjectState unknownObject = CreateObject(containerId: fixture.Source.Id);
                    fixture.Objects.Add(unknownObject.Id, unknownObject);
                    return new BatchFailureFixture(fixture, new[] { unknownObject.Id });
                }

                case BatchFailureScenario.DestinationAlreadyContains:
                    PlaceDestinationObjectIntoSourceToo(fixture);
                    return new BatchFailureFixture(fixture, new[] { fixture.DestinationObject.Id });

                case BatchFailureScenario.ObjectStateMissing:
                    fixture.Objects.Remove(fixture.SourceObjects[0].Id);
                    return new BatchFailureFixture(fixture, new[] { fixture.SourceObjects[0].Id });

                case BatchFailureScenario.ObjectContainerMismatch:
                    fixture.SourceObjects[0].SetContainer(ContainerId.New());
                    return new BatchFailureFixture(fixture, new[] { fixture.SourceObjects[0].Id });

                case BatchFailureScenario.CapacityExceeded:
                    return new BatchFailureFixture(fixture, new[]
                    {
                        fixture.SourceObjects[2].Id,
                        fixture.SourceObjects[1].Id
                    });

                case BatchFailureScenario.LaterItemInvalid:
                    fixture.SourceObjects[1].SetContainer(ContainerId.New());
                    return new BatchFailureFixture(fixture, new[]
                    {
                        fixture.SourceObjects[2].Id,
                        fixture.SourceObjects[1].Id
                    });

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported failure scenario.");
            }
        }

        private static TransferFixture CreateFixture(int destinationCapacity = 0)
        {
            ContainerTransferService transferService = new ContainerTransferService();
            ContainerState source = CreateContainer();
            ContainerState destination = CreateContainer(capacity: destinationCapacity);
            TabletopObjectState[] sourceObjects =
            {
                CreateObject(),
                CreateObject(),
                CreateObject()
            };
            TabletopObjectState destinationObject = CreateObject();
            Dictionary<TabletopObjectId, TabletopObjectState> objects =
                new Dictionary<TabletopObjectId, TabletopObjectState>();

            foreach (TabletopObjectState objectState in sourceObjects)
            {
                transferService.PlaceIntoContainer(objectState, source);
                objects.Add(objectState.Id, objectState);
            }

            transferService.PlaceIntoContainer(destinationObject, destination);
            objects.Add(destinationObject.Id, destinationObject);

            return new TransferFixture(source, destination, sourceObjects, destinationObject, objects);
        }

        private static void PlaceDestinationObjectIntoSourceToo(TransferFixture fixture)
        {
            fixture.DestinationObject.SetContainer(ContainerId.Empty);
            ContainerTransferService transferService = new ContainerTransferService();
            transferService.PlaceIntoContainer(fixture.DestinationObject, fixture.Source);
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

        private static TabletopObjectState CreateObject(ContainerId? containerId = null)
        {
            return new TabletopObjectState(
                TabletopObjectId.New(),
                ObjectDefinitionId.New(),
                TabletopObjectKind.Card,
                TabletopPose.Default,
                containerId ?? ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);
        }

        private sealed class TransferFixture
        {
            public TransferFixture(
                ContainerState source,
                ContainerState destination,
                TabletopObjectState[] sourceObjects,
                TabletopObjectState destinationObject,
                Dictionary<TabletopObjectId, TabletopObjectState> objects)
            {
                Source = source;
                Destination = destination;
                SourceObjects = sourceObjects;
                DestinationObject = destinationObject;
                Objects = objects;
            }

            public ContainerState Source { get; }

            public ContainerState Destination { get; }

            public TabletopObjectState[] SourceObjects { get; }

            public TabletopObjectState DestinationObject { get; }

            public Dictionary<TabletopObjectId, TabletopObjectState> Objects { get; }
        }

        private sealed class BatchFailureFixture
        {
            public BatchFailureFixture(TransferFixture fixture, IReadOnlyList<TabletopObjectId> transferOrder)
            {
                Fixture = fixture;
                TransferOrder = transferOrder;
            }

            public TransferFixture Fixture { get; }

            private IReadOnlyList<TabletopObjectId> TransferOrder { get; }

            public ContainerTransferResult Execute()
            {
                ContainerBatchTransferService service = new ContainerBatchTransferService();
                return service.TransferOrdered(Fixture.Objects, Fixture.Source, Fixture.Destination, TransferOrder);
            }
        }

        private sealed class BatchSnapshot
        {
            private BatchSnapshot(
                TabletopObjectId[] sourceOrder,
                TabletopObjectId[] destinationOrder,
                Dictionary<TabletopObjectId, ContainerId> objectContainerIds)
            {
                SourceOrder = sourceOrder;
                DestinationOrder = destinationOrder;
                ObjectContainerIds = objectContainerIds;
            }

            private TabletopObjectId[] SourceOrder { get; }

            private TabletopObjectId[] DestinationOrder { get; }

            private Dictionary<TabletopObjectId, ContainerId> ObjectContainerIds { get; }

            public static BatchSnapshot Capture(TransferFixture fixture)
            {
                Dictionary<TabletopObjectId, ContainerId> objectContainerIds =
                    new Dictionary<TabletopObjectId, ContainerId>();

                foreach (KeyValuePair<TabletopObjectId, TabletopObjectState> pair in fixture.Objects)
                {
                    objectContainerIds.Add(pair.Key, pair.Value.ContainerId);
                }

                return new BatchSnapshot(
                    fixture.Source.ObjectIds.ToArray(),
                    fixture.Destination.ObjectIds.ToArray(),
                    objectContainerIds);
            }

            public void AssertMatches(TransferFixture fixture)
            {
                Assert.That(fixture.Source.ObjectIds, Is.EqualTo(SourceOrder));
                Assert.That(fixture.Destination.ObjectIds, Is.EqualTo(DestinationOrder));

                foreach (KeyValuePair<TabletopObjectId, ContainerId> pair in ObjectContainerIds)
                {
                    Assert.That(fixture.Objects[pair.Key].ContainerId, Is.EqualTo(pair.Value));
                }
            }
        }

        public enum BatchFailureScenario
        {
            EmptyId,
            DuplicateId,
            SourceMembershipMissing,
            DestinationAlreadyContains,
            ObjectStateMissing,
            ObjectContainerMismatch,
            CapacityExceeded,
            LaterItemInvalid
        }
    }
}
