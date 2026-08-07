using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.PlayerLayouts
{
    public sealed class PlayerLayoutDefinition
    {
        public const int MinimumSeatCount = 1;
        public const int MaximumSeatCount = 8;

        private readonly ReadOnlyCollection<PlayerSeatLayoutEntry> seats;

        public PlayerLayoutDefinition(
            PlayerLayoutId id,
            string name,
            IEnumerable<PlayerSeatLayoutEntry> seats)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("Player Layout ID cannot be empty.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Player Layout name cannot be empty.", nameof(name));
            }

            if (seats == null)
            {
                throw new ArgumentNullException(nameof(seats));
            }

            List<PlayerSeatLayoutEntry> copiedSeats = new List<PlayerSeatLayoutEntry>();
            HashSet<int> seenSeatIndices = new HashSet<int>();
            foreach (PlayerSeatLayoutEntry seat in seats)
            {
                if (seat == null)
                {
                    throw new ArgumentException("Player Layout seats cannot contain null entries.", nameof(seats));
                }

                if (!seenSeatIndices.Add(seat.SeatIndex))
                {
                    throw new ArgumentException("Player Layout seats cannot contain duplicate Seat indices.", nameof(seats));
                }

                copiedSeats.Add(seat);
            }

            if (copiedSeats.Count < MinimumSeatCount || copiedSeats.Count > MaximumSeatCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seats),
                    $"Player Layouts must contain between {MinimumSeatCount} and {MaximumSeatCount} occupied Seats.");
            }

            copiedSeats.Sort((left, right) => left.SeatIndex.CompareTo(right.SeatIndex));
            for (int i = 0; i < copiedSeats.Count; i++)
            {
                if (copiedSeats[i].SeatIndex != i)
                {
                    throw new ArgumentException(
                        "Player Layout Seat indices must be contiguous and begin at zero.",
                        nameof(seats));
                }
            }

            Id = id;
            Name = name;
            this.seats = new ReadOnlyCollection<PlayerSeatLayoutEntry>(copiedSeats);
        }

        public PlayerLayoutId Id { get; }

        public string Name { get; }

        public int OccupiedSeatCount => seats.Count;

        public IReadOnlyList<PlayerSeatLayoutEntry> Seats => seats;

        public bool TryGetSeat(int seatIndex, out PlayerSeatLayoutEntry seat)
        {
            if (seatIndex >= 0 && seatIndex < seats.Count)
            {
                seat = seats[seatIndex];
                return true;
            }

            seat = null;
            return false;
        }
    }
}
