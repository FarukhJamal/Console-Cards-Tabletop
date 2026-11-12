using System.Collections.Generic;
using ConsoleCards.Core.Domain.Match;

namespace ConsoleCards.Application.Authority
{
    /// <summary>
    /// P8A STAGE-A PHY-3 (owner, 2026-11-13): the table's record hash that the periodic table check compares between
    /// the host and a joined computer (moved here from OnlinePlayPresenter.Table so tests use the very same hash).
    /// Every piece, container, seat and placement goes in, written field by field with <see cref="TableWire"/>, and
    /// the per-item hashes are summed, so the order of a dictionary does not matter. Two computers that ran the same
    /// commands on the same table get the same number; a change made on one computer only does not.
    /// </summary>
    public static class TableStateHash
    {
        public static ulong Of(MatchState match)
        {
            if (match == null) return 0UL;
            return Sum(match.Cards) * 3UL
                + Sum(match.Pawns) * 5UL
                + Sum(match.Tokens) * 7UL
                + Sum(match.Dice) * 11UL
                + Sum(match.Containers) * 13UL
                + Sum(match.ContainerPlacements) * 17UL
                + Sum(match.Seats) * 19UL
                + Sum(match.PlacedConsoles) * 23UL
                + Sum(match.PlacedBoards) * 29UL
                + Sum(match.PlayAreas) * 31UL;
        }

        private static ulong Sum<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> items)
        {
            ulong sum = (ulong)items.Count;
            foreach (KeyValuePair<TKey, TValue> item in items)
            {
                byte[] bytes = TableWire.Write(item.Value, false);
                ulong hash = TableWire.Hash(bytes, 0, bytes.Length);
                hash ^= hash >> 33;
                hash *= 0xFF51AFD7ED558CCDUL;
                hash ^= hash >> 33;
                sum += hash;
            }

            return sum;
        }
    }
}
