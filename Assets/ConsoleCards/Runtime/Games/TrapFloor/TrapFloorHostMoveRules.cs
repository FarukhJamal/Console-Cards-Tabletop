using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Games.TrapFloor
{
    public enum TrapFloorHostMoveVerdict
    {
        Allowed = 0,
        /// <summary>Another seat's pawn.</summary>
        NotYourPawn = 1,
        /// <summary>The pawn's player is out (eliminated or escaped) for this floor.</summary>
        PlayerIsOut = 2,
        /// <summary>A floor turn, or another player's turn.</summary>
        NotYourTurn = 3,
        /// <summary>Onto a collapsed tile or an empty cell.</summary>
        OntoHole = 4,
        /// <summary>Not along one straight line from where the turn started (or a hole in the way).</summary>
        NotStraight = 5,
        /// <summary>Further than this turn allows.</summary>
        TooFar = 6,
        /// <summary>A Sticky turn: the pawn stays.</summary>
        Stuck = 7,
        /// <summary>Blind: the direction has not been rolled, or this is not that direction.</summary>
        Blind = 8,
        /// <summary>An Ability or a Trap shows the tiles to go to, and this is not one.</summary>
        NotALitTile = 9,
    }

    /// <summary>The host's own facts for one pawn move (never what the sender wrote, apart from where to).</summary>
    public sealed class TrapFloorHostMoveFacts
    {
        /// <summary>The room plays Full (the host's Assist level). False: Manual / Reminders, nothing is refused here.</summary>
        public bool FullRules;
        public PlayerId Sender;
        /// <summary>The player whose pawn it is (empty: not a player's pawn, not checked).</summary>
        public PlayerId PawnOwner;
        public bool PawnOwnerOut;
        public bool IsPlayerTurn;
        public PlayerId ActivePlayer;
        /// <summary>Where the pawn stands on the host's table now (null: off the floor).</summary>
        public TrapFloorCoordinate? Current;
        /// <summary>Where it stood when this turn started (null: it came from off the floor).</summary>
        public TrapFloorCoordinate? TurnStart;
        /// <summary>Where the command puts it (null: off the floor).</summary>
        public TrapFloorCoordinate? Target;
        public int Columns;
        public int Rows;
        /// <summary>On the floor and not a hole, on the host's table.</summary>
        public Func<int, int, bool> Walkable;
        public int Allowance;
        public bool Stuck;
        public bool BlindUnrolled;
        public TrapFloorBlindMovementDirection? BlindDirection;
        /// <summary>Tiles an Ability or a Trap lights for this player (Dodge, Rush, Spring, Crane), or null.</summary>
        public ICollection<TrapFloorCoordinate> LitTiles;
        /// <summary>A guided move was already made this turn (as on the player's own computer: no count after it).</summary>
        public bool GuidedMoveDone;
        /// <summary>The rules send the pawn somewhere this turn on their own (Spring / Crane, back to the start after a
        /// Key or a prize): its target is not a hand move.</summary>
        public bool RuleMoveExpected;
    }

    /// <summary>
    /// AUTH-MOVE (owner, 2026-11-06: "FULL: use the existing game's defined rules to validate movement on the host"): the
    /// host's check of a pawn move sent by a joined computer, in Full only. The same rules the player's own computer
    /// shows as "Move not allowed" (doc 18 §15.2, H3, RULE-HOLE), decided on the host's table. Manual and Reminders are
    /// not checked here (tabletop freedom). Moves that keep the pawn on the same tile are never refused; a move off the floor (a fall) is
    /// allowed for the turn player's own pawn only. Pure, so it is unit tested.
    /// </summary>
    public static class TrapFloorHostMoveRules
    {
        public static TrapFloorHostMoveVerdict Check(TrapFloorHostMoveFacts f)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
            if (!f.FullRules || f.PawnOwner.IsEmpty) return TrapFloorHostMoveVerdict.Allowed; // tabletop freedom
            if (Nullable.Equals(f.Target, f.Current)) return TrapFloorHostMoveVerdict.Allowed; // settling in place
            if (f.Sender != f.PawnOwner) return TrapFloorHostMoveVerdict.NotYourPawn;
            if (f.PawnOwnerOut) return TrapFloorHostMoveVerdict.PlayerIsOut;
            if (!f.IsPlayerTurn || f.ActivePlayer != f.Sender) return TrapFloorHostMoveVerdict.NotYourTurn;
            // P8A (2026-11-08, live: a stray click after right-click → Move put a pawn on the board's edge and the host
            // took it): stepping off the floor is still a deliberate fall, but only for the player's own pawn, on their
            // own turn, while they may still move (not Stuck, not already moved by an Ability or Trap).
            if (!f.Target.HasValue)
            {
                if (f.Stuck) return TrapFloorHostMoveVerdict.Stuck;
                if (f.GuidedMoveDone) return TrapFloorHostMoveVerdict.NotALitTile;
                return TrapFloorHostMoveVerdict.Allowed;
            }

            TrapFloorCoordinate to = f.Target.Value;
            if (f.Walkable == null || !f.Walkable(to.X, to.Y)) return TrapFloorHostMoveVerdict.OntoHole;
            if (f.LitTiles != null && f.LitTiles.Count > 0)
                return f.LitTiles.Contains(to) ? TrapFloorHostMoveVerdict.Allowed : TrapFloorHostMoveVerdict.NotALitTile;
            if (f.GuidedMoveDone || f.RuleMoveExpected) return TrapFloorHostMoveVerdict.Allowed;
            if (!f.TurnStart.HasValue || to.Equals(f.TurnStart.Value)) return TrapFloorHostMoveVerdict.Allowed;
            if (f.Stuck) return TrapFloorHostMoveVerdict.Stuck;
            if (f.BlindUnrolled) return TrapFloorHostMoveVerdict.Blind;

            int steps = StepsAlongLine(f, f.TurnStart.Value, to);
            if (steps < 0)
            {
                if (f.BlindDirection.HasValue && StepsAlongLine(f, f.TurnStart.Value, to, ignoreBlind: true) >= 0)
                    return TrapFloorHostMoveVerdict.Blind;
                return TrapFloorHostMoveVerdict.NotStraight;
            }

            return steps > Math.Max(0, f.Allowance) ? TrapFloorHostMoveVerdict.TooFar : TrapFloorHostMoveVerdict.Allowed;
        }

        /// <summary>The player-facing reason, in the words the player's own computer uses.</summary>
        public static string Describe(TrapFloorHostMoveVerdict verdict, int allowance)
        {
            switch (verdict)
            {
                case TrapFloorHostMoveVerdict.NotYourPawn: return "that pawn is another player's.";
                case TrapFloorHostMoveVerdict.PlayerIsOut: return "that player is out for this floor.";
                case TrapFloorHostMoveVerdict.NotYourTurn: return "it is not your turn to move.";
                case TrapFloorHostMoveVerdict.OntoHole: return "that tile is a hole (collapsed floor cannot be stood on).";
                case TrapFloorHostMoveVerdict.NotStraight: return "moves go in one straight line (up, down, left or right), with no turning or crossing holes.";
                case TrapFloorHostMoveVerdict.TooFar: return $"that is further than this turn allows ({allowance}).";
                case TrapFloorHostMoveVerdict.Stuck: return "STUCK: this turn you stay where you are.";
                case TrapFloorHostMoveVerdict.Blind: return "BLIND: roll the Blind direction, then move only that way.";
                case TrapFloorHostMoveVerdict.NotALitTile: return "the Ability or Trap moves your pawn to a lit tile only.";
                default: return null;
            }
        }

        // Steps from the start to the tile along one straight line (up, down, left, right) over walkable tiles; -1 if not.
        private static int StepsAlongLine(TrapFloorHostMoveFacts f, TrapFloorCoordinate start, TrapFloorCoordinate to, bool ignoreBlind = false)
        {
            if (start.X != to.X && start.Y != to.Y) return -1;
            int dx = Math.Sign(to.X - start.X);
            int dy = Math.Sign(to.Y - start.Y);
            if (!ignoreBlind && f.BlindDirection.HasValue && DirectionOf(dx, dy) != f.BlindDirection.Value) return -1;
            int x = start.X, y = start.Y, steps = 0;
            while (x != to.X || y != to.Y)
            {
                x += dx;
                y += dy;
                steps++;
                if (!f.Walkable(x, y)) return -1;
            }

            return steps;
        }

        private static TrapFloorBlindMovementDirection DirectionOf(int dx, int dy) =>
            dx > 0 ? TrapFloorBlindMovementDirection.Right
            : dx < 0 ? TrapFloorBlindMovementDirection.Left
            : dy > 0 ? TrapFloorBlindMovementDirection.Up
            : TrapFloorBlindMovementDirection.Down;
    }
}
