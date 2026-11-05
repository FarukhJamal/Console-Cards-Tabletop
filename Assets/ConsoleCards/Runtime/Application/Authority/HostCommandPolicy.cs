using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
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
        public HostCommandVerdict Check(MatchState match, PlayerId sender, object command, Func<ContainerId, bool> isGuardedDeck)
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

        private void Remember(CommandId id)
        {
            seen.Add(id);
            order.Enqueue(id);
            while (order.Count > Remembered) seen.Remove(order.Dequeue());
        }
    }
}
