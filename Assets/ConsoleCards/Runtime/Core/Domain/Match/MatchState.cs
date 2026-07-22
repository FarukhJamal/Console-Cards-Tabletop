using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.Match
{
    public sealed class MatchState
    {
        private readonly Dictionary<TabletopObjectId, CardInstanceState> cards;
        private readonly Dictionary<TabletopObjectId, PawnState> pawns;
        private readonly Dictionary<TabletopObjectId, TokenState> tokens;
        private readonly Dictionary<ContainerId, ContainerState> containers;
        private readonly Dictionary<SeatId, SeatState> seats;
        private readonly ReadOnlyDictionary<TabletopObjectId, CardInstanceState> readOnlyCards;
        private readonly ReadOnlyDictionary<TabletopObjectId, PawnState> readOnlyPawns;
        private readonly ReadOnlyDictionary<TabletopObjectId, TokenState> readOnlyTokens;
        private readonly ReadOnlyDictionary<ContainerId, ContainerState> readOnlyContainers;
        private readonly ReadOnlyDictionary<SeatId, SeatState> readOnlySeats;

        public MatchState(
            MatchId id,
            GameTemplateId gameTemplateId,
            long revision,
            IEnumerable<CardInstanceState> cards,
            IEnumerable<PawnState> pawns,
            IEnumerable<TokenState> tokens,
            IEnumerable<ContainerState> containers,
            IEnumerable<SeatState> seats)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("Match ID cannot be empty.", nameof(id));
            }

            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision), "Revision cannot be below zero.");
            }

            Id = id;
            GameTemplateId = gameTemplateId;
            Revision = revision;

            HashSet<TabletopObjectId> seenObjectIds = new HashSet<TabletopObjectId>();
            this.cards = CopyCards(cards, seenObjectIds);
            this.pawns = CopyPawns(pawns, seenObjectIds);
            this.tokens = CopyTokens(tokens, seenObjectIds);
            this.containers = CopyContainers(containers);
            this.seats = CopySeats(seats);

            ValidateObjectContainerConsistency();
            ValidateSeatConsistency();

            readOnlyCards = new ReadOnlyDictionary<TabletopObjectId, CardInstanceState>(this.cards);
            readOnlyPawns = new ReadOnlyDictionary<TabletopObjectId, PawnState>(this.pawns);
            readOnlyTokens = new ReadOnlyDictionary<TabletopObjectId, TokenState>(this.tokens);
            readOnlyContainers = new ReadOnlyDictionary<ContainerId, ContainerState>(this.containers);
            readOnlySeats = new ReadOnlyDictionary<SeatId, SeatState>(this.seats);
        }

        public MatchId Id { get; }

        public GameTemplateId GameTemplateId { get; }

        public long Revision { get; private set; }

        public int ObjectCount => cards.Count + pawns.Count + tokens.Count;

        public IReadOnlyDictionary<TabletopObjectId, CardInstanceState> Cards => readOnlyCards;

        public IReadOnlyDictionary<TabletopObjectId, PawnState> Pawns => readOnlyPawns;

        public IReadOnlyDictionary<TabletopObjectId, TokenState> Tokens => readOnlyTokens;

        public IReadOnlyDictionary<ContainerId, ContainerState> Containers => readOnlyContainers;

        public IReadOnlyDictionary<SeatId, SeatState> Seats => readOnlySeats;

        public bool ContainsObject(TabletopObjectId objectId)
        {
            return cards.ContainsKey(objectId)
                || pawns.ContainsKey(objectId)
                || tokens.ContainsKey(objectId);
        }

        public TabletopObjectState GetObject(TabletopObjectId objectId)
        {
            if (cards.TryGetValue(objectId, out CardInstanceState card))
            {
                return card.BaseState;
            }

            if (pawns.TryGetValue(objectId, out PawnState pawn))
            {
                return pawn.BaseState;
            }

            if (tokens.TryGetValue(objectId, out TokenState token))
            {
                return token.BaseState;
            }

            throw new KeyNotFoundException("Tabletop object was not found.");
        }

        public ContainerState GetContainer(ContainerId containerId)
        {
            if (containers.TryGetValue(containerId, out ContainerState container))
            {
                return container;
            }

            throw new KeyNotFoundException("Container was not found.");
        }

        public SeatState GetSeat(SeatId seatId)
        {
            if (seats.TryGetValue(seatId, out SeatState seat))
            {
                return seat;
            }

            throw new KeyNotFoundException("Seat was not found.");
        }

        public long AdvanceRevision()
        {
            Revision = checked(Revision + 1);
            return Revision;
        }

        private static Dictionary<TabletopObjectId, CardInstanceState> CopyCards(
            IEnumerable<CardInstanceState> cards,
            HashSet<TabletopObjectId> seenObjectIds)
        {
            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards));
            }

            Dictionary<TabletopObjectId, CardInstanceState> copiedCards = new Dictionary<TabletopObjectId, CardInstanceState>();

            foreach (CardInstanceState card in cards)
            {
                if (card == null)
                {
                    throw new ArgumentException("Cards cannot contain null items.", nameof(cards));
                }

                AddObjectId(card.BaseState.Id, seenObjectIds, nameof(cards));
                copiedCards.Add(card.BaseState.Id, card);
            }

            return copiedCards;
        }

        private static Dictionary<TabletopObjectId, PawnState> CopyPawns(
            IEnumerable<PawnState> pawns,
            HashSet<TabletopObjectId> seenObjectIds)
        {
            if (pawns == null)
            {
                throw new ArgumentNullException(nameof(pawns));
            }

            Dictionary<TabletopObjectId, PawnState> copiedPawns = new Dictionary<TabletopObjectId, PawnState>();

            foreach (PawnState pawn in pawns)
            {
                if (pawn == null)
                {
                    throw new ArgumentException("Pawns cannot contain null items.", nameof(pawns));
                }

                AddObjectId(pawn.BaseState.Id, seenObjectIds, nameof(pawns));
                copiedPawns.Add(pawn.BaseState.Id, pawn);
            }

            return copiedPawns;
        }

        private static Dictionary<TabletopObjectId, TokenState> CopyTokens(
            IEnumerable<TokenState> tokens,
            HashSet<TabletopObjectId> seenObjectIds)
        {
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            Dictionary<TabletopObjectId, TokenState> copiedTokens = new Dictionary<TabletopObjectId, TokenState>();

            foreach (TokenState token in tokens)
            {
                if (token == null)
                {
                    throw new ArgumentException("Tokens cannot contain null items.", nameof(tokens));
                }

                AddObjectId(token.BaseState.Id, seenObjectIds, nameof(tokens));
                copiedTokens.Add(token.BaseState.Id, token);
            }

            return copiedTokens;
        }

        private static Dictionary<ContainerId, ContainerState> CopyContainers(IEnumerable<ContainerState> containers)
        {
            if (containers == null)
            {
                throw new ArgumentNullException(nameof(containers));
            }

            Dictionary<ContainerId, ContainerState> copiedContainers = new Dictionary<ContainerId, ContainerState>();

            foreach (ContainerState container in containers)
            {
                if (container == null)
                {
                    throw new ArgumentException("Containers cannot contain null items.", nameof(containers));
                }

                if (copiedContainers.ContainsKey(container.Id))
                {
                    throw new ArgumentException("Containers cannot contain duplicate Container IDs.", nameof(containers));
                }

                copiedContainers.Add(container.Id, container);
            }

            return copiedContainers;
        }

        private static Dictionary<SeatId, SeatState> CopySeats(IEnumerable<SeatState> seats)
        {
            if (seats == null)
            {
                throw new ArgumentNullException(nameof(seats));
            }

            Dictionary<SeatId, SeatState> copiedSeats = new Dictionary<SeatId, SeatState>();

            foreach (SeatState seat in seats)
            {
                if (seat == null)
                {
                    throw new ArgumentException("Seats cannot contain null items.", nameof(seats));
                }

                if (copiedSeats.ContainsKey(seat.Id))
                {
                    throw new ArgumentException("Seats cannot contain duplicate Seat IDs.", nameof(seats));
                }

                copiedSeats.Add(seat.Id, seat);
            }

            return copiedSeats;
        }

        private static void AddObjectId(
            TabletopObjectId objectId,
            HashSet<TabletopObjectId> seenObjectIds,
            string parameterName)
        {
            if (!seenObjectIds.Add(objectId))
            {
                throw new ArgumentException("Object collections cannot contain duplicate Tabletop Object IDs.", parameterName);
            }
        }

        private void ValidateObjectContainerConsistency()
        {
            Dictionary<TabletopObjectId, TabletopObjectState> objectStates = CreateObjectStateLookup();

            foreach (TabletopObjectState objectState in objectStates.Values)
            {
                if (objectState.ContainerId.IsEmpty)
                {
                    continue;
                }

                if (!containers.TryGetValue(objectState.ContainerId, out ContainerState container))
                {
                    throw new ArgumentException("Object references a missing Container.", nameof(containers));
                }

                if (CountContainerMembership(container, objectState.Id) != 1)
                {
                    throw new ArgumentException("Object Container must contain the object ID exactly once.", nameof(containers));
                }
            }

            foreach (ContainerState container in containers.Values)
            {
                foreach (TabletopObjectId objectId in container.ObjectIds)
                {
                    if (!objectStates.TryGetValue(objectId, out TabletopObjectState objectState))
                    {
                        throw new ArgumentException("Container references a missing object.", nameof(containers));
                    }

                    if (objectState.ContainerId != container.Id)
                    {
                        throw new ArgumentException("Container membership does not match the object's Container ID.", nameof(containers));
                    }
                }
            }
        }

        private void ValidateSeatConsistency()
        {
            foreach (SeatState seat in seats.Values)
            {
                if (!containers.TryGetValue(seat.HandContainerId, out ContainerState handContainer))
                {
                    throw new ArgumentException("Seat references a missing Hand Container.", nameof(seats));
                }

                if (handContainer.Kind != ContainerKind.Hand)
                {
                    throw new ArgumentException("Seat Hand Container must have Kind Hand.", nameof(seats));
                }

                if (handContainer.OwnerSeatId != seat.Id)
                {
                    throw new ArgumentException("Seat Hand Container owner must match the Seat ID.", nameof(seats));
                }

                foreach (ContainerId slotContainerId in seat.Console.SlotContainerIds)
                {
                    if (!containers.TryGetValue(slotContainerId, out ContainerState slotContainer))
                    {
                        throw new ArgumentException("Console slot references a missing Container.", nameof(seats));
                    }

                    if (slotContainer.Kind != ContainerKind.ConsoleSlot)
                    {
                        throw new ArgumentException("Console slot Container must have Kind ConsoleSlot.", nameof(seats));
                    }

                    if (slotContainer.OwnerSeatId != seat.Id)
                    {
                        throw new ArgumentException("Console slot Container owner must match the Seat ID.", nameof(seats));
                    }
                }
            }
        }

        private Dictionary<TabletopObjectId, TabletopObjectState> CreateObjectStateLookup()
        {
            Dictionary<TabletopObjectId, TabletopObjectState> objectStates = new Dictionary<TabletopObjectId, TabletopObjectState>();

            foreach (CardInstanceState card in cards.Values)
            {
                objectStates.Add(card.BaseState.Id, card.BaseState);
            }

            foreach (PawnState pawn in pawns.Values)
            {
                objectStates.Add(pawn.BaseState.Id, pawn.BaseState);
            }

            foreach (TokenState token in tokens.Values)
            {
                objectStates.Add(token.BaseState.Id, token.BaseState);
            }

            return objectStates;
        }

        private static int CountContainerMembership(ContainerState container, TabletopObjectId objectId)
        {
            int count = 0;

            foreach (TabletopObjectId containedObjectId in container.ObjectIds)
            {
                if (containedObjectId == objectId)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
