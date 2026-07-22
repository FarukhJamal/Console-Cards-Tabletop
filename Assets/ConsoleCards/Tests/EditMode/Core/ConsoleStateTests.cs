using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class ConsoleStateTests
    {
        [Test]
        public void Constructor_StoresOwnerAndOrderedSlots()
        {
            SeatId ownerSeatId = SeatId.New();
            ContainerId firstSlot = ContainerId.New();
            ContainerId secondSlot = ContainerId.New();

            ConsoleState state = new ConsoleState(ownerSeatId, new[] { firstSlot, secondSlot });

            Assert.That(state.OwnerSeatId, Is.EqualTo(ownerSeatId));
            Assert.That(state.SlotCount, Is.EqualTo(2));
            Assert.That(state.SlotContainerIds, Is.EqualTo(new[] { firstSlot, secondSlot }));
        }

        [Test]
        public void Constructor_WhenOwnerIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new ConsoleState(SeatId.Empty, Array.Empty<ContainerId>()),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenSlotSequenceIsNull_ThrowsArgumentNullException()
        {
            Assert.That(
                () => new ConsoleState(SeatId.New(), null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_WhenSlotIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new ConsoleState(SeatId.New(), new[] { ContainerId.Empty }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenSlotIdIsDuplicate_ThrowsArgumentException()
        {
            ContainerId slotContainerId = ContainerId.New();

            Assert.That(
                () => new ConsoleState(SeatId.New(), new[] { slotContainerId, slotContainerId }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenZeroSlots_AcceptsValue()
        {
            ConsoleState state = new ConsoleState(SeatId.New(), Array.Empty<ContainerId>());

            Assert.That(state.SlotCount, Is.EqualTo(0));
            Assert.That(state.SlotContainerIds, Is.Empty);
        }

        [Test]
        public void ContainsSlot_ReportsCurrentSlots()
        {
            ContainerId slotContainerId = ContainerId.New();
            ConsoleState state = new ConsoleState(SeatId.New(), new[] { slotContainerId });

            Assert.That(state.ContainsSlot(slotContainerId), Is.True);
            Assert.That(state.ContainsSlot(ContainerId.New()), Is.False);
        }

        [Test]
        public void SlotContainerIds_CannotMutateInternalState()
        {
            ContainerId firstSlot = ContainerId.New();
            List<ContainerId> suppliedSlots = new List<ContainerId> { firstSlot };
            ConsoleState state = new ConsoleState(SeatId.New(), suppliedSlots);
            suppliedSlots.Add(ContainerId.New());

            Assert.That(state.SlotContainerIds, Is.EqualTo(new[] { firstSlot }));
            Assert.That(state.SlotContainerIds as List<ContainerId>, Is.Null);

            IList<ContainerId> listView = state.SlotContainerIds as IList<ContainerId>;
            Assert.That(listView, Is.Not.Null);
            Assert.That(
                () => listView.Add(ContainerId.New()),
                Throws.TypeOf<NotSupportedException>());
        }
    }
}
