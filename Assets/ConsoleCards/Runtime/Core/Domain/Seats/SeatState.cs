using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.Seats
{
    public sealed class SeatState
    {
        public SeatState(
            SeatId id,
            TabletopPose tablePose,
            ContainerId handContainerId,
            ConsoleState console,
            PlayerId occupantPlayerId,
            SeatStatus status)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("Seat ID cannot be empty.", nameof(id));
            }

            if (handContainerId.IsEmpty)
            {
                throw new ArgumentException("Hand Container ID cannot be empty.", nameof(handContainerId));
            }

            if (console == null)
            {
                throw new ArgumentNullException(nameof(console));
            }

            if (console.OwnerSeatId != id)
            {
                throw new ArgumentException("Console owner Seat ID must match the Seat ID.", nameof(console));
            }

            ValidateOccupantStatus(occupantPlayerId, status);

            Id = id;
            TablePose = tablePose;
            HandContainerId = handContainerId;
            Console = console;
            OccupantPlayerId = occupantPlayerId;
            Status = status;
        }

        public SeatId Id { get; }

        public TabletopPose TablePose { get; private set; }

        public ContainerId HandContainerId { get; }

        public ConsoleState Console { get; }

        public PlayerId OccupantPlayerId { get; private set; }

        public SeatStatus Status { get; private set; }

        public void SetTablePose(TabletopPose tablePose)
        {
            TablePose = tablePose;
        }

        public void AssignPlayer(PlayerId playerId)
        {
            if (playerId.IsEmpty)
            {
                throw new ArgumentException("Player ID cannot be empty.", nameof(playerId));
            }

            OccupantPlayerId = playerId;
            Status = SeatStatus.Occupied;
        }

        public void ReserveFor(PlayerId playerId)
        {
            if (playerId.IsEmpty)
            {
                throw new ArgumentException("Player ID cannot be empty.", nameof(playerId));
            }

            OccupantPlayerId = playerId;
            Status = SeatStatus.Reserved;
        }

        public void MarkTemporarilyDisconnected()
        {
            if (Status != SeatStatus.Occupied || OccupantPlayerId.IsEmpty)
            {
                throw new InvalidOperationException("Only an occupied Seat with an occupant can be marked temporarily disconnected.");
            }

            Status = SeatStatus.TemporarilyDisconnected;
        }

        public void RestoreConnection()
        {
            if (Status != SeatStatus.TemporarilyDisconnected || OccupantPlayerId.IsEmpty)
            {
                throw new InvalidOperationException("Only a temporarily disconnected Seat with an occupant can restore connection.");
            }

            Status = SeatStatus.Occupied;
        }

        public void ClearOccupant()
        {
            OccupantPlayerId = PlayerId.Empty;
            Status = SeatStatus.Vacant;
        }

        private static void ValidateOccupantStatus(PlayerId occupantPlayerId, SeatStatus status)
        {
            if (status == SeatStatus.Vacant)
            {
                if (!occupantPlayerId.IsEmpty)
                {
                    throw new ArgumentException("Vacant Seats cannot have an occupant.", nameof(occupantPlayerId));
                }

                return;
            }

            if (occupantPlayerId.IsEmpty)
            {
                throw new ArgumentException("Non-vacant Seats require an occupant.", nameof(occupantPlayerId));
            }
        }
    }
}
