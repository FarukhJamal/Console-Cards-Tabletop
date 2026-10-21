using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.PlayerLayouts
{
    /// <summary>
    /// Default placement rule (PLACE-1, owner 2026-10-21) for every game: when fewer players sit at a table than its
    /// layout has seats, the seats used face the board and are spread out round the table. Seat 0 is always used;
    /// each next seat is the one farthest (by angle round the layout centre) from the seats already chosen, ties to
    /// the lowest index. On the standard 4-seat layout 2 players sit opposite (seats 0 and 2), 3 sit on three sides
    /// (0, 1, 2) and 4 use every side. The chosen seats keep their table order so turns still go round the table.
    /// Event-time only (called when a table is dealt).
    /// </summary>
    public static class PlayerLayoutSeatRules
    {
        /// <summary>The layout's seat indices to use for <paramref name="count"/> players, in seat order.</summary>
        public static IReadOnlyList<int> ChooseSeats(PlayerLayoutDefinition layout, int count)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (count < PlayerLayoutDefinition.MinimumSeatCount || count > layout.OccupiedSeatCount)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            TableCoordinate[] positions = new TableCoordinate[layout.OccupiedSeatCount];
            for (int i = 0; i < positions.Length; i++) positions[i] = layout.Seats[i].PlayerZonePose.Position;
            return ChooseSpread(positions, count);
        }

        /// <summary>
        /// PLACE-2: the same rule for any places round a table (for example the Consoles of a game a player made):
        /// the indices of <paramref name="count"/> of <paramref name="positions"/>, the first always, then each next
        /// the farthest round the middle of all of them from those already chosen, returned in their own order.
        /// </summary>
        public static IReadOnlyList<int> ChooseSpread(IReadOnlyList<TableCoordinate> positions, int count)
        {
            if (positions == null)
            {
                throw new ArgumentNullException(nameof(positions));
            }

            int total = positions.Count;
            if (count < 1 || count > total)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            // The middle of the places: the board sits there on every built-in layout.
            double centreX = 0d;
            double centreY = 0d;
            for (int i = 0; i < total; i++)
            {
                centreX += positions[i].X;
                centreY += positions[i].Y;
            }

            centreX /= total;
            centreY /= total;
            double[] angles = new double[total];
            for (int i = 0; i < total; i++)
            {
                angles[i] = Math.Atan2(positions[i].Y - centreY, positions[i].X - centreX);
            }

            bool[] chosen = new bool[total];
            chosen[0] = true;
            for (int picked = 1; picked < count; picked++)
            {
                int best = -1;
                double bestGap = -1d;
                for (int candidate = 0; candidate < total; candidate++)
                {
                    if (chosen[candidate])
                    {
                        continue;
                    }

                    double gap = double.MaxValue;
                    for (int other = 0; other < total; other++)
                    {
                        if (chosen[other])
                        {
                            gap = Math.Min(gap, AngleBetween(angles[candidate], angles[other]));
                        }
                    }

                    // A strictly larger gap wins (with a small tolerance), so ties go to the lowest index.
                    if (gap > bestGap + 0.01d)
                    {
                        best = candidate;
                        bestGap = gap;
                    }
                }

                chosen[best] = true;
            }

            List<int> seats = new List<int>(count);
            for (int i = 0; i < total; i++)
            {
                if (chosen[i])
                {
                    seats.Add(i);
                }
            }

            return seats;
        }

        /// <summary>
        /// The layout cut down to <paramref name="count"/> seats chosen by <see cref="ChooseSeats"/>, re-indexed from 0
        /// in table order, keeping the table bounds. Returns the layout itself when every seat is used.
        /// </summary>
        public static PlayerLayoutDefinition ForPlayerCount(PlayerLayoutDefinition layout, int count)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (count == layout.OccupiedSeatCount)
            {
                return layout;
            }

            IReadOnlyList<int> chosen = ChooseSeats(layout, count);
            List<PlayerSeatLayoutEntry> seats = new List<PlayerSeatLayoutEntry>(chosen.Count);
            byte[] idBytes = layout.Id.Value.ToByteArray();
            for (int i = 0; i < chosen.Count; i++)
            {
                PlayerSeatLayoutEntry source = layout.Seats[chosen[i]];
                seats.Add(new PlayerSeatLayoutEntry(
                    i,
                    source.PlayerZonePose,
                    source.HandAnchorPose,
                    source.ConsoleAnchorPose,
                    source.FacingRotationDegrees));
                idBytes[15 - (i % 8)] ^= (byte)(0x11 * (chosen[i] + 1));
            }

            // Deterministic id: the base id mixed with the count and the chosen seats.
            idBytes[0] ^= (byte)(0x5A + count);
            return new PlayerLayoutDefinition(
                new PlayerLayoutId(new Guid(idBytes)),
                layout.Name + " (" + count + " players)",
                seats,
                layout.TableBounds);
        }

        private static double AngleBetween(double a, double b)
        {
            double difference = Math.Abs(a - b) % (2d * Math.PI);
            return difference > Math.PI ? (2d * Math.PI) - difference : difference;
        }
    }
}
