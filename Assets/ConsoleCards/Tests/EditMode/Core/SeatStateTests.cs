using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class SeatStateTests
    {
        [Test]
        public void Constructor_StoresAllValues()
        {
            SeatId id = SeatId.New();
            TabletopPose pose = new TabletopPose(new TableCoordinate(2, 3), 45, 1, 2);
            ContainerId handContainerId = ContainerId.New();
            ConsoleState console = new ConsoleState(id, Array.Empty<ContainerId>());
            PlayerId occupantPlayerId = PlayerId.New();

            SeatState state = new SeatState(
                id,
                pose,
                handContainerId,
                console,
                occupantPlayerId,
                SeatStatus.Occupied);

            Assert.That(state.Id, Is.EqualTo(id));
            Assert.That(state.TablePose, Is.EqualTo(pose));
            Assert.That(state.HandContainerId, Is.EqualTo(handContainerId));
            Assert.That(state.Console, Is.SameAs(console));
            Assert.That(state.OccupantPlayerId, Is.EqualTo(occupantPlayerId));
            Assert.That(state.Status, Is.EqualTo(SeatStatus.Occupied));
        }

        [Test]
        public void Constructor_WhenSeatIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => CreateSeat(id: SeatId.Empty),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenHandContainerIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => CreateSeat(handContainerId: ContainerId.Empty),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenConsoleIsNull_ThrowsArgumentNullException()
        {
            SeatId seatId = SeatId.New();

            Assert.That(
                () => new SeatState(
                    seatId,
                    TabletopPose.Default,
                    ContainerId.New(),
                    null,
                    PlayerId.Empty,
                    SeatStatus.Vacant),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_WhenConsoleOwnerMismatches_ThrowsArgumentException()
        {
            ConsoleState console = new ConsoleState(SeatId.New(), Array.Empty<ContainerId>());

            Assert.That(
                () => CreateSeat(console: console),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenVacantWithEmptyOccupant_AcceptsValue()
        {
            SeatState state = CreateSeat(occupantPlayerId: PlayerId.Empty, status: SeatStatus.Vacant);

            Assert.That(state.Status, Is.EqualTo(SeatStatus.Vacant));
            Assert.That(state.OccupantPlayerId, Is.EqualTo(PlayerId.Empty));
        }

        [Test]
        public void Constructor_WhenVacantWithOccupant_ThrowsArgumentException()
        {
            Assert.That(
                () => CreateSeat(occupantPlayerId: PlayerId.New(), status: SeatStatus.Vacant),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenNonVacantWithEmptyOccupant_ThrowsArgumentException()
        {
            Assert.That(
                () => CreateSeat(occupantPlayerId: PlayerId.Empty, status: SeatStatus.Reserved),
                Throws.ArgumentException);
            Assert.That(
                () => CreateSeat(occupantPlayerId: PlayerId.Empty, status: SeatStatus.Occupied),
                Throws.ArgumentException);
            Assert.That(
                () => CreateSeat(occupantPlayerId: PlayerId.Empty, status: SeatStatus.TemporarilyDisconnected),
                Throws.ArgumentException);
        }

        [Test]
        public void AssignPlayer_SetsOccupied()
        {
            SeatState state = CreateSeat();
            PlayerId playerId = PlayerId.New();

            state.AssignPlayer(playerId);

            Assert.That(state.OccupantPlayerId, Is.EqualTo(playerId));
            Assert.That(state.Status, Is.EqualTo(SeatStatus.Occupied));
        }

        [Test]
        public void ReserveFor_SetsReserved()
        {
            SeatState state = CreateSeat();
            PlayerId playerId = PlayerId.New();

            state.ReserveFor(playerId);

            Assert.That(state.OccupantPlayerId, Is.EqualTo(playerId));
            Assert.That(state.Status, Is.EqualTo(SeatStatus.Reserved));
        }

        [Test]
        public void MarkTemporarilyDisconnected_FromOccupied_Succeeds()
        {
            PlayerId playerId = PlayerId.New();
            SeatState state = CreateSeat(occupantPlayerId: playerId, status: SeatStatus.Occupied);

            state.MarkTemporarilyDisconnected();

            Assert.That(state.OccupantPlayerId, Is.EqualTo(playerId));
            Assert.That(state.Status, Is.EqualTo(SeatStatus.TemporarilyDisconnected));
        }

        [Test]
        public void MarkTemporarilyDisconnected_WhenStateIsInvalid_ThrowsInvalidOperationException()
        {
            Assert.That(
                () => CreateSeat().MarkTemporarilyDisconnected(),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => CreateSeat(occupantPlayerId: PlayerId.New(), status: SeatStatus.Reserved).MarkTemporarilyDisconnected(),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void RestoreConnection_FromTemporarilyDisconnected_Succeeds()
        {
            PlayerId playerId = PlayerId.New();
            SeatState state = CreateSeat(occupantPlayerId: playerId, status: SeatStatus.TemporarilyDisconnected);

            state.RestoreConnection();

            Assert.That(state.OccupantPlayerId, Is.EqualTo(playerId));
            Assert.That(state.Status, Is.EqualTo(SeatStatus.Occupied));
        }

        [Test]
        public void RestoreConnection_WhenStateIsInvalid_ThrowsInvalidOperationException()
        {
            Assert.That(
                () => CreateSeat().RestoreConnection(),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => CreateSeat(occupantPlayerId: PlayerId.New(), status: SeatStatus.Occupied).RestoreConnection(),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void ClearOccupant_SetsVacantAndEmptyPlayerId()
        {
            SeatState state = CreateSeat(occupantPlayerId: PlayerId.New(), status: SeatStatus.Occupied);

            state.ClearOccupant();

            Assert.That(state.OccupantPlayerId, Is.EqualTo(PlayerId.Empty));
            Assert.That(state.Status, Is.EqualTo(SeatStatus.Vacant));
        }

        [Test]
        public void SetTablePose_UpdatesPose()
        {
            SeatState state = CreateSeat();
            TabletopPose pose = new TabletopPose(new TableCoordinate(7, 8), 90, 2, 3);

            state.SetTablePose(pose);

            Assert.That(state.TablePose, Is.EqualTo(pose));
        }

        private static SeatState CreateSeat(
            SeatId? id = null,
            ContainerId? handContainerId = null,
            ConsoleState console = null,
            PlayerId? occupantPlayerId = null,
            SeatStatus status = SeatStatus.Vacant)
        {
            SeatId seatId = id ?? SeatId.New();

            return new SeatState(
                seatId,
                TabletopPose.Default,
                handContainerId ?? ContainerId.New(),
                console ?? new ConsoleState(seatId, Array.Empty<ContainerId>()),
                occupantPlayerId ?? PlayerId.Empty,
                status);
        }
    }
}
