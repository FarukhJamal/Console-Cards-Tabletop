using System;
using System.Collections.Generic;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// P7 (s162): whether a player still on the floor can still reach a usable exit. Host-only and pure (the cartridge
    /// gives it the authoritative facts; nothing here is sent to a guest).
    /// <para>
    /// The verdict is <see cref="TrapFloorEscapeVerdict.NoEscape"/> only when that is certain under the rules as they
    /// are written: the pawn's walkable area (tiles joined side by side, holes excluded) holds no usable exit, holds no
    /// face-down tile (a face-down tile may still hold a Spring, Crane, Ability or Friend that moves a pawn), and no
    /// player still on the floor holds a card that could move a pawn (an Ability or a Friend: Rush, Lasso and the like).
    /// Holes are only ever added, never removed, so such an area can only shrink. Anything less certain is
    /// <see cref="TrapFloorEscapeVerdict.Undetermined"/> with the reason; it never becomes a loss on its own.
    /// </para>
    /// </summary>
    public enum TrapFloorEscapeVerdict
    {
        CanReachExit = 0,
        NoEscape = 1,
        Undetermined = 2,
    }

    public readonly struct TrapFloorEscapeOutlook
    {
        public TrapFloorEscapeOutlook(TrapFloorEscapeVerdict verdict, string reason)
        {
            Verdict = verdict;
            Reason = reason ?? string.Empty;
        }

        public TrapFloorEscapeVerdict Verdict { get; }
        public string Reason { get; }

        public static TrapFloorEscapeOutlook Evaluate(
            int columns,
            int rows,
            TrapFloorCoordinate pawn,
            Func<TrapFloorCoordinate, bool> isHole,
            Func<TrapFloorCoordinate, bool> isUsableExit,
            Func<TrapFloorCoordinate, bool> isFaceDown,
            bool anyPlayerOnTheFloorHoldsAMoveCard)
        {
            if (isHole == null || isUsableExit == null || isFaceDown == null) throw new ArgumentNullException();
            if (columns < 1 || rows < 1) throw new ArgumentOutOfRangeException(nameof(columns));
            if (pawn.X < 1 || pawn.Y < 1 || pawn.X > columns || pawn.Y > rows || isHole(pawn))
                return new TrapFloorEscapeOutlook(TrapFloorEscapeVerdict.Undetermined, "the pawn is not on a standing tile");

            HashSet<TrapFloorCoordinate> seen = new HashSet<TrapFloorCoordinate> { pawn };
            Queue<TrapFloorCoordinate> open = new Queue<TrapFloorCoordinate>();
            open.Enqueue(pawn);
            bool faceDownInReach = false;
            while (open.Count > 0)
            {
                TrapFloorCoordinate at = open.Dequeue();
                if (isUsableExit(at)) return new TrapFloorEscapeOutlook(TrapFloorEscapeVerdict.CanReachExit, $"exit reachable at {at}");
                if (isFaceDown(at)) faceDownInReach = true;
                foreach (TrapFloorCoordinate next in Neighbours(at, columns, rows))
                {
                    if (isHole(next) || !seen.Add(next)) continue;
                    open.Enqueue(next);
                }
            }

            if (faceDownInReach)
                return new TrapFloorEscapeOutlook(TrapFloorEscapeVerdict.Undetermined,
                    "no exit in walking reach, but a face-down tile in reach may still move the pawn");
            if (anyPlayerOnTheFloorHoldsAMoveCard)
                return new TrapFloorEscapeOutlook(TrapFloorEscapeVerdict.Undetermined,
                    "no exit in walking reach, but a player still holds an Ability or Friend that may move a pawn");
            return new TrapFloorEscapeOutlook(TrapFloorEscapeVerdict.NoEscape,
                $"no exit in walking reach of {pawn}, every tile in reach is face up, and no card in play can move a pawn");
        }

        private static IEnumerable<TrapFloorCoordinate> Neighbours(TrapFloorCoordinate at, int columns, int rows)
        {
            if (at.X > 1) yield return new TrapFloorCoordinate(at.X - 1, at.Y);
            if (at.X < columns) yield return new TrapFloorCoordinate(at.X + 1, at.Y);
            if (at.Y > 1) yield return new TrapFloorCoordinate(at.X, at.Y - 1);
            if (at.Y < rows) yield return new TrapFloorCoordinate(at.X, at.Y + 1);
        }
    }

    /// <summary>
    /// P7 (s162), owner rule 2026-11-08: every floor holds exactly one Grand Exit and it never collapses. A floor
    /// without one, with more than one, or with it collapsed is a software defect, not a gameplay loss.
    /// </summary>
    public enum TrapFloorGrandExitInvariant
    {
        Holds = 0,
        Missing = 1,
        Duplicated = 2,
        Collapsed = 3,
    }

    public static class TrapFloorGrandExitCheck
    {
        public static TrapFloorGrandExitInvariant Check(
            IEnumerable<TrapFloorCoordinate> grandExitTiles,
            Func<TrapFloorCoordinate, bool> isHole)
        {
            if (grandExitTiles == null || isHole == null) throw new ArgumentNullException();
            int count = 0;
            bool collapsed = false;
            foreach (TrapFloorCoordinate at in grandExitTiles)
            {
                count++;
                if (isHole(at)) collapsed = true;
            }

            if (count == 0) return TrapFloorGrandExitInvariant.Missing;
            if (count > 1) return TrapFloorGrandExitInvariant.Duplicated;
            return collapsed ? TrapFloorGrandExitInvariant.Collapsed : TrapFloorGrandExitInvariant.Holds;
        }
    }
}
