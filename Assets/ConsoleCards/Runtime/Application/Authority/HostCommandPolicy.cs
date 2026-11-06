using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Authority
{
    public enum HostCommandVerdict
    {
        Allowed = 0,
        /// <summary>This command id already ran on the host: it is not run or sent again.</summary>
        Duplicate = 1,
        /// <summary>A draw into a Hand that belongs to another seat (or to no one).</summary>
        DrawIntoAnotherHand = 2,
        MissingCommandId = 3,
        /// <summary>
        /// The game's own host-side check refused it against the host's state (AUTH-PRIZE: the taker's pawn is not on
        /// the prize's tile in the host's table, whatever position the request names).
        /// </summary>
        RefusedByGame = 4,
        /// <summary>
        /// AUTH-POS (2026-11-06): a move or a physics resting place that is not a number or lies far off any table. The
        /// network reader builds commands without their constructors' checks, so the host checks again.
        /// </summary>
        InvalidPosition = 5,
    }

    /// <summary>
    /// AUTH-DRAW / AUTH-DUP (owner approval, 2026-11-06): checks the host makes on a command from a joined computer before
    /// replaying it, on top of the sender check (the command's player must be the connection's seat) and the use case's
    /// own checks. Pure, so it is unit tested.
    /// <list type="bullet">
    /// <item>A command id runs once on the host. A copy sent again (a repeat or a replay attack) changes nothing.</item>
    /// <item>A draw from a guarded deck (a game's Controller deck) goes into the sender's own Hand, or into a pile that is
    /// no one's Hand. Other piles keep the table's freedom (a Manual deal into every hand). A draw names a count, not
    /// cards: the host takes the deck's top cards, so the sender cannot pick them. Secrecy note: every seated computer
    /// still holds the deck order (T1); this path adds no new disclosure.</item>
    /// </list>
    /// </summary>
    public sealed class HostCommandPolicy
    {
        public const int Remembered = 2048;

        private readonly HashSet<CommandId> seen = new HashSet<CommandId>();
        private readonly Queue<CommandId> order = new Queue<CommandId>();

        /// <param name="isGuardedDeck">Decks whose draws must go into the sender's own Hand (a game's Controller decks).
        /// Draws from other piles keep the table's freedom (a Manual deal into every hand).</param>
        /// <param name="gameAllows">Optional: the game's check of a command against the host's own state (false:
        /// refused). Runs before the command id is remembered, so a refused command can be sent again once it fits.</param>
        public HostCommandVerdict Check(
            MatchState match,
            PlayerId sender,
            object command,
            Func<ContainerId, bool> isGuardedDeck,
            Func<object, PlayerId, bool> gameAllows = null)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            CommandId id = command is ITabletopCommand tabletop ? tabletop.Context.Id : CommandId.Empty;
            if (command is ITabletopCommand && id.IsEmpty) return HostCommandVerdict.MissingCommandId;
            if (!id.IsEmpty && seen.Contains(id)) return HostCommandVerdict.Duplicate;
            if (command is DrawCardsCommand draw
                && isGuardedDeck != null
                && isGuardedDeck(draw.SourceDeckContainerId)
                && IsDrawIntoAnotherHand(match, sender, draw.DestinationContainerId))
                return HostCommandVerdict.DrawIntoAnotherHand;
            if (!HasSanePosition(command)) return HostCommandVerdict.InvalidPosition;
            if (gameAllows != null && !gameAllows(command, sender)) return HostCommandVerdict.RefusedByGame;
            if (!id.IsEmpty) Remember(id);
            return HostCommandVerdict.Allowed;
        }

        public void Clear()
        {
            seen.Clear();
            order.Clear();
        }

        /// <summary>True when the destination is a Hand that is not the sender's own.</summary>
        public static bool IsDrawIntoAnotherHand(MatchState match, PlayerId sender, ContainerId destination)
        {
            if (match == null || !match.Containers.TryGetValue(destination, out ContainerState container)) return false; // the use case refuses it
            if (container.Kind != ContainerKind.Hand) return false;
            if (container.OwnerSeatId.IsEmpty || !match.Seats.TryGetValue(container.OwnerSeatId, out SeatState seat)) return true;
            return sender.IsEmpty || seat.OccupantPlayerId != sender;
        }

        /// <summary>Further than this from the table's centre (in table units) is nowhere on any table.</summary>
        public const double MaxDistance = 10000d;

        /// <summary>AUTH-POS: false for a move or a resting place that is not finite or lies far off any table.</summary>
        public static bool HasSanePosition(object command)
        {
            if (command is MoveObjectCommand move)
                return Sane(move.TargetPose.Position.X) && Sane(move.TargetPose.Position.Y) && Finite(move.TargetPose.RotationDegrees);
            if (command is CommitPhysicalObjectCommand commit)
            {
                if (commit.State == null) return true; // the use case refuses it
                return Sane(commit.State.Position.X) && Sane(commit.State.Position.Y) && Sane(commit.State.Position.Z)
                    && Finite(commit.State.Rotation.X) && Finite(commit.State.Rotation.Y)
                    && Finite(commit.State.Rotation.Z) && Finite(commit.State.Rotation.W);
            }

            return true;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool Sane(double value) => Finite(value) && Math.Abs(value) <= MaxDistance;

        private void Remember(CommandId id)
        {
            seen.Add(id);
            order.Enqueue(id);
            while (order.Count > Remembered) seen.Remove(order.Dequeue());
        }
    }
}
