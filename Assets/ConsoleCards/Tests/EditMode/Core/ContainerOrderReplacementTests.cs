using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class ContainerOrderReplacementTests
    {
        [Test]
        public void ReplaceOrder_WhenPermutationIsValid_AppliesExactSuppliedOrder()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState first, out TabletopObjectState second, out TabletopObjectState third);

            container.ReplaceOrder(new[] { third.Id, first.Id, second.Id });

            Assert.That(container.ObjectIds, Is.EqualTo(new[] { third.Id, first.Id, second.Id }));
        }

        [Test]
        public void ReplaceOrder_WhenContainerIsEmpty_AcceptsEmptyReplacement()
        {
            ContainerState container = CreateContainer();

            container.ReplaceOrder(Array.Empty<TabletopObjectId>());

            Assert.That(container.ObjectIds, Is.Empty);
        }

        [Test]
        public void ReplaceOrder_WhenContainerHasOneItem_AcceptsIdenticalReplacement()
        {
            ContainerState container = CreateContainer();
            TabletopObjectState onlyObject = AddObject(container);

            container.ReplaceOrder(new[] { onlyObject.Id });

            Assert.That(container.ObjectIds, Is.EqualTo(new[] { onlyObject.Id }));
        }

        [Test]
        public void ReplaceOrder_WhenReplacementIsNull_ThrowsArgumentNullException()
        {
            ContainerState container = CreateContainer();

            Assert.That(
                () => container.ReplaceOrder(null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void ReplaceOrder_WhenCountMismatches_ThrowsArgumentException()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState first, out _, out _);

            Assert.That(
                () => container.ReplaceOrder(new[] { first.Id }),
                Throws.ArgumentException);
        }

        [Test]
        public void ReplaceOrder_WhenObjectIdIsEmpty_ThrowsArgumentException()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState first, out _, out TabletopObjectState third);

            Assert.That(
                () => container.ReplaceOrder(new[] { first.Id, TabletopObjectId.Empty, third.Id }),
                Throws.ArgumentException);
        }

        [Test]
        public void ReplaceOrder_WhenObjectIdIsDuplicate_ThrowsArgumentException()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState first, out _, out TabletopObjectState third);

            Assert.That(
                () => container.ReplaceOrder(new[] { first.Id, first.Id, third.Id }),
                Throws.ArgumentException);
        }

        [Test]
        public void ReplaceOrder_WhenCurrentMemberIsMissing_ThrowsArgumentException()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState first, out _, out TabletopObjectState third);

            Assert.That(
                () => container.ReplaceOrder(new[] { first.Id, third.Id, TabletopObjectId.New() }),
                Throws.ArgumentException);
        }

        [Test]
        public void ReplaceOrder_WhenUnknownMemberIsIncluded_ThrowsArgumentException()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState first, out TabletopObjectState second, out _);

            Assert.That(
                () => container.ReplaceOrder(new[] { first.Id, second.Id, TabletopObjectId.New() }),
                Throws.ArgumentException);
        }

        [TestCase(ReplacementFailureScenario.CountMismatch)]
        [TestCase(ReplacementFailureScenario.EmptyId)]
        [TestCase(ReplacementFailureScenario.DuplicateId)]
        [TestCase(ReplacementFailureScenario.MissingCurrentMember)]
        [TestCase(ReplacementFailureScenario.UnknownMember)]
        public void ReplaceOrder_WhenFailureOccurs_PreservesOriginalOrder(ReplacementFailureScenario scenario)
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState first, out TabletopObjectState second, out TabletopObjectState third);
            TabletopObjectId[] originalOrder = { first.Id, second.Id, third.Id };
            TabletopObjectId[] replacement = CreateInvalidReplacement(scenario, first.Id, second.Id, third.Id);

            Assert.That(
                () => container.ReplaceOrder(replacement),
                Throws.Exception);

            Assert.That(container.ObjectIds, Is.EqualTo(originalOrder));
        }

        [Test]
        public void ReplaceOrder_PreservesNonOrderContainerFields()
        {
            ContainerId containerId = ContainerId.New();
            SeatId ownerSeatId = SeatId.New();
            ContainerState container = CreateContainer(
                id: containerId,
                kind: ContainerKind.Deck,
                ownerSeatId: ownerSeatId,
                visibility: ObjectVisibility.OwnerOnly,
                capacity: 6);
            AddObjects(container, out TabletopObjectState first, out TabletopObjectState second, out TabletopObjectState third);

            container.ReplaceOrder(new[] { second.Id, third.Id, first.Id });

            Assert.That(container.Id, Is.EqualTo(containerId));
            Assert.That(container.Kind, Is.EqualTo(ContainerKind.Deck));
            Assert.That(container.OwnerSeatId, Is.EqualTo(ownerSeatId));
            Assert.That(container.Visibility, Is.EqualTo(ObjectVisibility.OwnerOnly));
            Assert.That(container.Capacity, Is.EqualTo(6));
        }

        [Test]
        public void ReplaceOrder_PreservesMembershipSetExactly()
        {
            ContainerState container = CreateContainer();
            AddObjects(container, out TabletopObjectState first, out TabletopObjectState second, out TabletopObjectState third);

            container.ReplaceOrder(new[] { third.Id, first.Id, second.Id });

            Assert.That(container.ObjectIds, Is.EquivalentTo(new[] { first.Id, second.Id, third.Id }));
            Assert.That(container.ObjectIds, Is.Unique);
        }

        private static TabletopObjectId[] CreateInvalidReplacement(
            ReplacementFailureScenario scenario,
            TabletopObjectId first,
            TabletopObjectId second,
            TabletopObjectId third)
        {
            switch (scenario)
            {
                case ReplacementFailureScenario.CountMismatch:
                    return new[] { first, second };

                case ReplacementFailureScenario.EmptyId:
                    return new[] { first, TabletopObjectId.Empty, third };

                case ReplacementFailureScenario.DuplicateId:
                    return new[] { first, first, third };

                case ReplacementFailureScenario.MissingCurrentMember:
                    return new[] { first, third, TabletopObjectId.New() };

                case ReplacementFailureScenario.UnknownMember:
                    return new[] { first, second, TabletopObjectId.New() };

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported replacement failure scenario.");
            }
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
            out TabletopObjectState first,
            out TabletopObjectState second,
            out TabletopObjectState third)
        {
            first = AddObject(container);
            second = AddObject(container);
            third = AddObject(container);
        }

        private static TabletopObjectState AddObject(ContainerState container)
        {
            TabletopObjectState objectState = new TabletopObjectState(
                TabletopObjectId.New(),
                ObjectDefinitionId.New(),
                TabletopObjectKind.Card,
                TabletopPose.Default,
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);

            ContainerTransferService service = new ContainerTransferService();
            service.PlaceIntoContainer(objectState, container);
            return objectState;
        }

        public enum ReplacementFailureScenario
        {
            CountMismatch,
            EmptyId,
            DuplicateId,
            MissingCurrentMember,
            UnknownMember
        }
    }
}
