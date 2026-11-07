using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.GameTemplates.Privacy
{
    /// <summary>
    /// PRIVACY-P6B (s151): an operation the host checked, with the real ids it names. The host then runs it with the
    /// table's own command (TransferCardCommand, FlipCardCommand, ShuffleDeckCommand, ReorderContainerCommand,
    /// DrawCardsCommand): this class decides who may ask; the game's own use cases decide what is a legal move.
    /// </summary>
    public sealed class PrivateResolvedOperation
    {
        public PrivateOperation Operation;
        public TabletopObjectId RealCard;
        public int FromIndex;
    }

    /// <summary>
    /// PRIVACY-P6B: who may ask for a card or deck operation on the private path, the same for every seat (the host's
    /// own seat included). The rules here are about privacy and ownership only, so a Free table keeps its tabletop
    /// freedom: a player may act on its own hand, its own Console, its own seat's piles and the shared piles and
    /// table; never on another seat's hand, Console or piles, never on a card by another recipient's reference or by a
    /// hidden card's real id. In Full the game's own turn applies (IPrivateSeats.IsTurnOf).
    /// </summary>
    public static class PrivateOperationPolicy
    {
        public static PrivateRequestVerdict Check(MatchState match, IPrivateSeats seats, PrivateTableHost host, PlayerId sender, PrivateOperation op, out PrivateResolvedOperation resolved)
        {
            resolved = null;
            if (op == null) return PrivateRequestVerdict.UnknownReference;
            if (!seats.IsSeated(sender)) return PrivateRequestVerdict.NotSeated;
            if (op.Kind != PrivateOperationKind.Undo && !seats.IsTurnOf(sender)) return PrivateRequestVerdict.NotYourTurn;
            if (!host.AcceptRequest(op.RequestId)) return PrivateRequestVerdict.Duplicate;
            SeatId seat = SeatOf(match, sender);
            resolved = new PrivateResolvedOperation { Operation = op, FromIndex = -1 };

            switch (op.Kind)
            {
                case PrivateOperationKind.Transfer:
                {
                    PrivateRequestVerdict card = ResolveCard(match, host, seats, sender, seat, op.Card, out TabletopObjectId real, out ContainerId at);
                    if (card != PrivateRequestVerdict.Accepted) return card;
                    if (at != op.Container) return PrivateRequestVerdict.InvalidSource; // the card has moved since the sender saw it
                    if (op.HasPose) { resolved.RealCard = real; return PrivateRequestVerdict.Accepted; }
                    if (!match.Containers.TryGetValue(op.Destination, out ContainerState to) || !MayUseHere(match, seats, host, sender, seat, to)) return PrivateRequestVerdict.InvalidDestination;
                    if (to.Kind == ContainerKind.Hand && to.Id != seats.HandOf(sender)) return PrivateRequestVerdict.InvalidDestination;
                    resolved.RealCard = real;
                    return PrivateRequestVerdict.Accepted;
                }

                case PrivateOperationKind.Flip:
                {
                    PrivateRequestVerdict card = ResolveCard(match, host, seats, sender, seat, op.Card, out TabletopObjectId real, out _);
                    if (card != PrivateRequestVerdict.Accepted) return card;
                    resolved.RealCard = real;
                    return PrivateRequestVerdict.Accepted;
                }

                case PrivateOperationKind.Shuffle:
                    if (!match.Containers.TryGetValue(op.Container, out ContainerState deck) || deck.Kind != ContainerKind.Deck) return PrivateRequestVerdict.InvalidSource;
                    return MayUseHere(match, seats, host, sender, seat, deck) ? PrivateRequestVerdict.Accepted : PrivateRequestVerdict.WrongSeat;

                case PrivateOperationKind.DrawCount:
                    if (!match.Containers.TryGetValue(op.Container, out ContainerState source) || source.Kind != ContainerKind.Deck) return PrivateRequestVerdict.InvalidSource;
                    if (!MayUseHere(match, seats, host, sender, seat, source)) return PrivateRequestVerdict.WrongSeat;
                    if (op.Destination != seats.HandOf(sender)) return PrivateRequestVerdict.InvalidDestination; // draws go into one's own hand
                    if (source.Count < op.A) return PrivateRequestVerdict.EmptyDeck;
                    int limit = seats.DrawLimit(sender);
                    if (limit >= 0 && match.Containers[op.Destination].Count + op.A > limit) return PrivateRequestVerdict.RuleRefused;
                    return PrivateRequestVerdict.Accepted;

                case PrivateOperationKind.Reorder:
                {
                    if (!match.Containers.TryGetValue(op.Container, out ContainerState container) || !MayUseHere(match, seats, host, sender, seat, container)) return PrivateRequestVerdict.InvalidSource;
                    RecipientSnapshotProjection.Classify(match, sender, out _, out HashSet<ContainerId> hiddenOrder);
                    if (hiddenOrder.Contains(container.Id)) return PrivateRequestVerdict.InvalidSource; // an order the sender may not see
                    PrivateRequestVerdict card = ResolveCard(match, host, seats, sender, seat, op.Card, out TabletopObjectId real, out ContainerId at);
                    if (card != PrivateRequestVerdict.Accepted) return card;
                    if (at != container.Id) return PrivateRequestVerdict.InvalidSource;
                    int from = container.IndexOf(real);
                    if (from != op.A || op.B >= container.Count) return PrivateRequestVerdict.InvalidDestination; // stale or out of range
                    resolved.RealCard = real;
                    resolved.FromIndex = from;
                    return PrivateRequestVerdict.Accepted;
                }

                default:
                    return PrivateRequestVerdict.UnknownReference;
            }
        }

        /// <summary>A container the sender may act on: its own hand, its own Console, its own seat's piles, or a shared public one.</summary>
        public static bool MayUse(MatchState match, IPrivateSeats seats, PlayerId sender, SeatId seat, ContainerState container)
        {
            if (container == null || container.Visibility == ObjectVisibility.AuthorityOnly) return false;
            if (container.Kind == ContainerKind.Hand) return container.Id == seats.HandOf(sender);
            if (seats.IsOwnConsoleSlot(sender, container.Id)) return true;
            if (!container.OwnerSeatId.IsEmpty) return container.OwnerSeatId == seat;
            if (IsAnyConsoleSlot(match, container.Id)) return false; // another seat's Console
            return true;
        }

        /// <summary>
        /// PRIVACY-P6B: a pile the public layout names as another seat's (its Controller deck or action area) is refused
        /// even when the table gives it no owner seat. A pile the layout names for this seat too (Trap Floor's one shared
        /// Controller deck) stays usable.
        /// </summary>
        public static bool MayUseHere(MatchState match, IPrivateSeats seats, PrivateTableHost host, PlayerId sender, SeatId seat, ContainerState container) =>
            container != null && !IsOtherSeatsPile(host?.Layout, seat, container.Id) && MayUse(match, seats, sender, seat, container);

        public static bool IsOtherSeatsPile(RecipientPublicLayout layout, SeatId seat, ContainerId id)
        {
            if (layout == null || id.IsEmpty) return false;
            bool others = false;
            foreach (RecipientSeatLayout entry in layout.Seats)
            {
                if (entry == null || (entry.ControllerDeck != id && entry.ActionArea != id)) continue;
                if (entry.Seat == seat) return false; // also this seat's own (a shared pile)
                others = true;
            }

            return others;
        }

        private static bool IsAnyConsoleSlot(MatchState match, ContainerId id)
        {
            foreach (KeyValuePair<SeatId, SeatState> pair in match.Seats)
                if (pair.Value.Console != null && pair.Value.Console.ContainsSlot(id)) return true;
            return false;
        }

        private static PrivateRequestVerdict ResolveCard(MatchState match, PrivateTableHost host, IPrivateSeats seats, PlayerId sender, SeatId seat, TabletopObjectId reference, out TabletopObjectId real, out ContainerId at)
        {
            at = ContainerId.Empty;
            ReferenceResolution resolution = host.Resolve(sender, reference, out real);
            if (resolution == ReferenceResolution.Unknown) return PrivateRequestVerdict.UnknownReference;
            if (resolution == ReferenceResolution.HiddenRealId) return PrivateRequestVerdict.HiddenRealId;
            if (!match.Cards.TryGetValue(real, out CardInstanceState card)) return PrivateRequestVerdict.InvalidSource;
            at = card.BaseState.ContainerId;
            if (at.IsEmpty) return PrivateRequestVerdict.Accepted; // a card loose on the table
            return match.Containers.TryGetValue(at, out ContainerState container) && MayUseHere(match, seats, host, sender, seat, container)
                ? PrivateRequestVerdict.Accepted
                : PrivateRequestVerdict.InvalidSource;
        }

        public static SeatId SeatOf(MatchState match, PlayerId player)
        {
            if (player.IsEmpty) return SeatId.Empty;
            foreach (KeyValuePair<SeatId, SeatState> pair in match.Seats)
                if (pair.Value.OccupantPlayerId == player) return pair.Key;
            return SeatId.Empty;
        }
    }
}
