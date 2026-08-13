using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.Match
{
    public sealed class MatchState
    {
        private readonly Dictionary<TabletopObjectId, CardInstanceState> cards;
        private readonly Dictionary<TabletopObjectId, PawnState> pawns;
        private readonly Dictionary<TabletopObjectId, TokenState> tokens;
        private readonly Dictionary<TabletopObjectId, DieState> dice;
        private readonly Dictionary<ContainerId, ContainerState> containers;
        private readonly Dictionary<ContainerId, ContainerPlacementState> containerPlacements;
        private readonly Dictionary<SeatId, SeatState> seats;
        private readonly Dictionary<ConsoleId, PlacedConsoleState> placedConsoles;
        private readonly Dictionary<PlayAreaId, PlayAreaState> playAreas;
        private readonly ReadOnlyDictionary<TabletopObjectId, CardInstanceState> readOnlyCards;
        private readonly ReadOnlyDictionary<TabletopObjectId, PawnState> readOnlyPawns;
        private readonly ReadOnlyDictionary<TabletopObjectId, TokenState> readOnlyTokens;
        private readonly ReadOnlyDictionary<TabletopObjectId, DieState> readOnlyDice;
        private readonly ReadOnlyDictionary<ContainerId, ContainerState> readOnlyContainers;
        private readonly ReadOnlyDictionary<ContainerId, ContainerPlacementState> readOnlyContainerPlacements;
        private readonly ReadOnlyDictionary<SeatId, SeatState> readOnlySeats;
        private readonly ReadOnlyDictionary<ConsoleId, PlacedConsoleState> readOnlyPlacedConsoles;
        private readonly ReadOnlyDictionary<PlayAreaId, PlayAreaState> readOnlyPlayAreas;

        public MatchState(
            MatchId id,
            GameTemplateId gameTemplateId,
            long revision,
            IEnumerable<CardInstanceState> cards,
            IEnumerable<PawnState> pawns,
            IEnumerable<TokenState> tokens,
            IEnumerable<ContainerState> containers,
            IEnumerable<SeatState> seats)
            : this(
                id,
                gameTemplateId,
                revision,
                cards,
                pawns,
                tokens,
                containers,
                seats,
                Array.Empty<ContainerPlacementState>())
        {
        }

        public MatchState(
            MatchId id,
            GameTemplateId gameTemplateId,
            long revision,
            IEnumerable<CardInstanceState> cards,
            IEnumerable<PawnState> pawns,
            IEnumerable<TokenState> tokens,
            IEnumerable<ContainerState> containers,
            IEnumerable<SeatState> seats,
            IReadOnlyDictionary<ContainerId, ContainerPlacementState> containerPlacements)
            : this(
                id,
                gameTemplateId,
                revision,
                cards,
                pawns,
                tokens,
                containers,
                seats,
                CopyContainerPlacements(containerPlacements).Values)
        {
        }

        public MatchState(
            MatchId id,
            GameTemplateId gameTemplateId,
            long revision,
            IEnumerable<CardInstanceState> cards,
            IEnumerable<PawnState> pawns,
            IEnumerable<TokenState> tokens,
            IEnumerable<ContainerState> containers,
            IEnumerable<SeatState> seats,
            IEnumerable<ContainerPlacementState> containerPlacements)
            : this(
                id,
                gameTemplateId,
                revision,
                cards,
                pawns,
                tokens,
                containers,
                seats,
                containerPlacements,
                Array.Empty<PlayAreaState>())
        {
        }

        public MatchState(
            MatchId id,
            GameTemplateId gameTemplateId,
            long revision,
            IEnumerable<CardInstanceState> cards,
            IEnumerable<PawnState> pawns,
            IEnumerable<TokenState> tokens,
            IEnumerable<ContainerState> containers,
            IEnumerable<SeatState> seats,
            IEnumerable<ContainerPlacementState> containerPlacements,
            IEnumerable<PlayAreaState> playAreas)
            : this(
                id,
                gameTemplateId,
                revision,
                cards,
                pawns,
                tokens,
                containers,
                seats,
                containerPlacements,
                playAreas,
                Array.Empty<DieState>())
        {
        }

        public MatchState(
            MatchId id,
            GameTemplateId gameTemplateId,
            long revision,
            IEnumerable<CardInstanceState> cards,
            IEnumerable<PawnState> pawns,
            IEnumerable<TokenState> tokens,
            IEnumerable<ContainerState> containers,
            IEnumerable<SeatState> seats,
            IEnumerable<ContainerPlacementState> containerPlacements,
            IEnumerable<PlayAreaState> playAreas,
            IEnumerable<DieState> dice)
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
            this.dice = CopyDice(dice, seenObjectIds);
            this.containers = CopyContainers(containers);
            this.containerPlacements = CopyContainerPlacements(containerPlacements);
            this.seats = CopySeats(seats);
            placedConsoles = new Dictionary<ConsoleId, PlacedConsoleState>();
            this.playAreas = CopyPlayAreas(playAreas);

            ValidateObjectContainerConsistency();
            ValidateContainerPlacementConsistency();
            ValidateSeatConsistency();

            readOnlyCards = new ReadOnlyDictionary<TabletopObjectId, CardInstanceState>(this.cards);
            readOnlyPawns = new ReadOnlyDictionary<TabletopObjectId, PawnState>(this.pawns);
            readOnlyTokens = new ReadOnlyDictionary<TabletopObjectId, TokenState>(this.tokens);
            readOnlyDice = new ReadOnlyDictionary<TabletopObjectId, DieState>(this.dice);
            readOnlyContainers = new ReadOnlyDictionary<ContainerId, ContainerState>(this.containers);
            readOnlyContainerPlacements = new ReadOnlyDictionary<ContainerId, ContainerPlacementState>(this.containerPlacements);
            readOnlySeats = new ReadOnlyDictionary<SeatId, SeatState>(this.seats);
            readOnlyPlacedConsoles = new ReadOnlyDictionary<ConsoleId, PlacedConsoleState>(placedConsoles);
            readOnlyPlayAreas = new ReadOnlyDictionary<PlayAreaId, PlayAreaState>(this.playAreas);
        }

        public MatchId Id { get; }

        public GameTemplateId GameTemplateId { get; }

        public long Revision { get; private set; }

        public int ObjectCount => cards.Count + pawns.Count + tokens.Count + dice.Count;

        public IReadOnlyDictionary<TabletopObjectId, CardInstanceState> Cards => readOnlyCards;

        public IReadOnlyDictionary<TabletopObjectId, PawnState> Pawns => readOnlyPawns;

        public IReadOnlyDictionary<TabletopObjectId, TokenState> Tokens => readOnlyTokens;

        public IReadOnlyDictionary<TabletopObjectId, DieState> Dice => readOnlyDice;

        public IReadOnlyDictionary<ContainerId, ContainerState> Containers => readOnlyContainers;

        public IReadOnlyDictionary<ContainerId, ContainerPlacementState> ContainerPlacements => readOnlyContainerPlacements;

        public IReadOnlyDictionary<SeatId, SeatState> Seats => readOnlySeats;

        public IReadOnlyDictionary<ConsoleId, PlacedConsoleState> PlacedConsoles => readOnlyPlacedConsoles;

        public IReadOnlyDictionary<PlayAreaId, PlayAreaState> PlayAreas => readOnlyPlayAreas;

        public bool ContainsObject(TabletopObjectId objectId)
        {
            return cards.ContainsKey(objectId)
                || pawns.ContainsKey(objectId)
                || tokens.ContainsKey(objectId)
                || dice.ContainsKey(objectId);
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

            if (dice.TryGetValue(objectId, out DieState die))
            {
                return die.BaseState;
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

        public bool TryGetContainerPlacement(
            ContainerId containerId,
            out ContainerPlacementState placement)
        {
            if (containerPlacements.TryGetValue(containerId, out placement))
            {
                return true;
            }

            placement = null;
            return false;
        }

        public bool TryGetSeatHand(SeatId seatId, out ContainerState handContainer)
        {
            if (!seats.TryGetValue(seatId, out SeatState seat))
            {
                handContainer = null;
                return false;
            }

            if (!containers.TryGetValue(seat.HandContainerId, out handContainer))
            {
                handContainer = null;
                return false;
            }

            if (handContainer.Kind != ContainerKind.Hand || handContainer.OwnerSeatId != seat.Id)
            {
                handContainer = null;
                return false;
            }

            return true;
        }

        public bool TryGetSeatConsole(SeatId seatId, out ConsoleState console)
        {
            if (!seats.TryGetValue(seatId, out SeatState seat))
            {
                console = null;
                return false;
            }

            if (seat.Console.OwnerSeatId != seat.Id)
            {
                console = null;
                return false;
            }

            console = seat.Console;
            return true;
        }

        public bool TryGetConsoleSlot(
            SeatId seatId,
            int slotIndex,
            out ContainerState slotContainer)
        {
            if (slotIndex < 0)
            {
                slotContainer = null;
                return false;
            }

            if (!TryGetSeatConsole(seatId, out ConsoleState console))
            {
                slotContainer = null;
                return false;
            }

            if (slotIndex >= console.SlotCount)
            {
                slotContainer = null;
                return false;
            }

            ContainerId slotContainerId = console.SlotContainerIds[slotIndex];
            if (!containers.TryGetValue(slotContainerId, out slotContainer))
            {
                slotContainer = null;
                return false;
            }

            if (slotContainer.Kind != ContainerKind.ConsoleSlot
                || slotContainer.OwnerSeatId != console.OwnerSeatId)
            {
                slotContainer = null;
                return false;
            }

            return true;
        }

        public void AddEmptyPlacedContainer(
            ContainerState container,
            ContainerPlacementState placement)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            if (placement == null)
            {
                throw new ArgumentNullException(nameof(placement));
            }

            if (container.Id.IsEmpty)
            {
                throw new ArgumentException("Container ID cannot be empty.", nameof(container));
            }

            if (placement.ContainerId != container.Id)
            {
                throw new ArgumentException("Container placement ID must match the Container ID.", nameof(placement));
            }

            if (containers.ContainsKey(container.Id))
            {
                throw new ArgumentException("Container ID already exists in the Match.", nameof(container));
            }

            if (containerPlacements.ContainsKey(placement.ContainerId))
            {
                throw new ArgumentException("Container placement ID already exists in the Match.", nameof(placement));
            }

            if (container.Count != 0)
            {
                throw new InvalidOperationException("Only empty Containers can be added through this operation.");
            }

            if (!CanHavePlacement(container.Kind))
            {
                throw new ArgumentException("Only Deck, Stack, and DiscardPile Containers can be added with placement.", nameof(container));
            }

            bool containerAdded = false;
            bool placementAdded = false;

            try
            {
                containers.Add(container.Id, container);
                containerAdded = true;
                containerPlacements.Add(placement.ContainerId, placement);
                placementAdded = true;
            }
            catch
            {
                if (placementAdded)
                {
                    containerPlacements.Remove(placement.ContainerId);
                }

                if (containerAdded)
                {
                    containers.Remove(container.Id);
                }

                throw;
            }
        }

        public ContainerState RemoveEmptyContainer(ContainerId containerId)
        {
            if (containerId.IsEmpty)
            {
                throw new ArgumentException("Container ID cannot be empty.", nameof(containerId));
            }

            if (!containers.TryGetValue(containerId, out ContainerState container))
            {
                throw new KeyNotFoundException("Container was not found.");
            }

            if (container.Count != 0)
            {
                throw new InvalidOperationException("Only empty Containers can be removed.");
            }

            if (IsReferencedBySeatHand(containerId))
            {
                throw new InvalidOperationException("Seat Hand Containers cannot be removed.");
            }

            if (IsReferencedByConsoleSlot(containerId))
            {
                throw new InvalidOperationException("Console Slot Containers cannot be removed.");
            }

            containers.Remove(containerId);
            containerPlacements.Remove(containerId);
            return container;
        }

        public SeatState GetSeat(SeatId seatId)
        {
            if (seats.TryGetValue(seatId, out SeatState seat))
            {
                return seat;
            }

            throw new KeyNotFoundException("Seat was not found.");
        }

        public void AddUncontainedCard(CardInstanceState card)
        {
            AddUncontainedObject(card?.BaseState, card, cards, nameof(card));
        }

        public void AddUncontainedCards(IReadOnlyList<CardInstanceState> cardsToAdd)
        {
            if (cardsToAdd == null)
            {
                throw new ArgumentNullException(nameof(cardsToAdd));
            }

            HashSet<TabletopObjectId> incomingIds = new HashSet<TabletopObjectId>();
            for (int i = 0; i < cardsToAdd.Count; i++)
            {
                CardInstanceState card = cardsToAdd[i];
                if (card == null || card.BaseState == null)
                {
                    throw new ArgumentException("Card batch cannot contain null entries.", nameof(cardsToAdd));
                }

                if (!card.BaseState.ContainerId.IsEmpty)
                {
                    throw new InvalidOperationException("Only uncontained Cards can be added through this operation.");
                }

                if (!incomingIds.Add(card.BaseState.Id) || ContainsObject(card.BaseState.Id))
                {
                    throw new ArgumentException("Card batch contains an existing or duplicate Object ID.", nameof(cardsToAdd));
                }
            }

            List<TabletopObjectId> addedIds = new List<TabletopObjectId>();
            try
            {
                for (int i = 0; i < cardsToAdd.Count; i++)
                {
                    CardInstanceState card = cardsToAdd[i];
                    cards.Add(card.BaseState.Id, card);
                    addedIds.Add(card.BaseState.Id);
                }
            }
            catch
            {
                for (int i = 0; i < addedIds.Count; i++)
                {
                    cards.Remove(addedIds[i]);
                }

                throw;
            }
        }

        public void AddCardsToEmptyContainer(
            ContainerId containerId,
            IReadOnlyList<CardInstanceState> cardsToAdd)
        {
            if (!containers.TryGetValue(containerId, out ContainerState destination))
            {
                throw new KeyNotFoundException("Destination Container was not found.");
            }

            if (destination.Count != 0)
            {
                throw new InvalidOperationException("Cards can only be populated into an empty Container.");
            }

            if (cardsToAdd == null)
            {
                throw new ArgumentNullException(nameof(cardsToAdd));
            }

            if (destination.Capacity > 0 && cardsToAdd.Count > destination.Capacity)
            {
                throw new InvalidOperationException("Card batch exceeds Container capacity.");
            }

            HashSet<TabletopObjectId> incomingIds = new HashSet<TabletopObjectId>();
            for (int i = 0; i < cardsToAdd.Count; i++)
            {
                CardInstanceState card = cardsToAdd[i];
                if (card == null || card.BaseState == null)
                {
                    throw new ArgumentException("Card batch cannot contain null entries.", nameof(cardsToAdd));
                }

                if (card.BaseState.ContainerId != containerId)
                {
                    throw new InvalidOperationException("Every populated Card must reference the destination Container.");
                }

                if (!incomingIds.Add(card.BaseState.Id) || ContainsObject(card.BaseState.Id))
                {
                    throw new ArgumentException("Card batch contains an existing or duplicate Object ID.", nameof(cardsToAdd));
                }
            }

            List<TabletopObjectId> addedIds = new List<TabletopObjectId>();
            try
            {
                for (int i = 0; i < cardsToAdd.Count; i++)
                {
                    CardInstanceState card = cardsToAdd[i];
                    cards.Add(card.BaseState.Id, card);
                    addedIds.Add(card.BaseState.Id);
                    destination.InsertObject(card.BaseState.Id, destination.Count);
                }
            }
            catch
            {
                for (int i = addedIds.Count - 1; i >= 0; i--)
                {
                    TabletopObjectId objectId = addedIds[i];
                    if (destination.Contains(objectId))
                    {
                        destination.RemoveObject(objectId);
                    }

                    cards.Remove(objectId);
                }

                throw;
            }
        }

        public void AddPlacedConsole(
            PlacedConsoleState placedConsole,
            IReadOnlyList<ContainerState> slotContainers)
        {
            if (placedConsole == null)
            {
                throw new ArgumentNullException(nameof(placedConsole));
            }

            if (slotContainers == null)
            {
                throw new ArgumentNullException(nameof(slotContainers));
            }

            if (placedConsoles.ContainsKey(placedConsole.Id))
            {
                throw new ArgumentException("Console ID already exists in the Match.", nameof(placedConsole));
            }

            if (slotContainers.Count != placedConsole.Console.SlotCount)
            {
                throw new ArgumentException("Placed Console Slot count must match its Console state.", nameof(slotContainers));
            }

            HashSet<ContainerId> incomingIds = new HashSet<ContainerId>();
            for (int i = 0; i < slotContainers.Count; i++)
            {
                ContainerState slot = slotContainers[i];
                if (slot == null
                    || slot.Id != placedConsole.Console.SlotContainerIds[i]
                    || slot.Kind != ContainerKind.ConsoleSlot
                    || !slot.OwnerSeatId.IsEmpty
                    || slot.Count != 0
                    || containers.ContainsKey(slot.Id)
                    || !incomingIds.Add(slot.Id))
                {
                    throw new ArgumentException("Placed Console requires new, unowned, empty Console Slot Containers in Console order.", nameof(slotContainers));
                }
            }

            List<ContainerId> addedSlotIds = new List<ContainerId>();
            try
            {
                for (int i = 0; i < slotContainers.Count; i++)
                {
                    ContainerState slot = slotContainers[i];
                    containers.Add(slot.Id, slot);
                    addedSlotIds.Add(slot.Id);
                }

                placedConsoles.Add(placedConsole.Id, placedConsole);
            }
            catch
            {
                placedConsoles.Remove(placedConsole.Id);
                for (int i = 0; i < addedSlotIds.Count; i++)
                {
                    containers.Remove(addedSlotIds[i]);
                }

                throw;
            }
        }

        public void AddUncontainedPawn(PawnState pawn)
        {
            AddUncontainedObject(pawn?.BaseState, pawn, pawns, nameof(pawn));
        }

        public void AddUncontainedToken(TokenState token)
        {
            AddUncontainedObject(token?.BaseState, token, tokens, nameof(token));
        }

        public void AddUncontainedDie(DieState die)
        {
            AddUncontainedObject(die?.BaseState, die, dice, nameof(die));
        }

        public PlayAreaState GetPlayArea(PlayAreaId playAreaId)
        {
            if (playAreas.TryGetValue(playAreaId, out PlayAreaState playArea))
            {
                return playArea;
            }

            throw new KeyNotFoundException("Play Area was not found.");
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

        private static Dictionary<TabletopObjectId, DieState> CopyDice(
            IEnumerable<DieState> dice,
            HashSet<TabletopObjectId> seenObjectIds)
        {
            if (dice == null)
            {
                throw new ArgumentNullException(nameof(dice));
            }

            Dictionary<TabletopObjectId, DieState> copiedDice = new Dictionary<TabletopObjectId, DieState>();
            foreach (DieState die in dice)
            {
                if (die == null)
                {
                    throw new ArgumentException("Dice cannot contain null items.", nameof(dice));
                }

                AddObjectId(die.BaseState.Id, seenObjectIds, nameof(dice));
                copiedDice.Add(die.BaseState.Id, die);
            }

            return copiedDice;
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

        private static Dictionary<ContainerId, ContainerPlacementState> CopyContainerPlacements(
            IEnumerable<ContainerPlacementState> containerPlacements)
        {
            if (containerPlacements == null)
            {
                throw new ArgumentNullException(nameof(containerPlacements));
            }

            Dictionary<ContainerId, ContainerPlacementState> copiedPlacements =
                new Dictionary<ContainerId, ContainerPlacementState>();

            foreach (ContainerPlacementState placement in containerPlacements)
            {
                if (placement == null)
                {
                    throw new ArgumentException("Container placements cannot contain null items.", nameof(containerPlacements));
                }

                if (copiedPlacements.ContainsKey(placement.ContainerId))
                {
                    throw new ArgumentException("Container placements cannot contain duplicate Container IDs.", nameof(containerPlacements));
                }

                copiedPlacements.Add(placement.ContainerId, placement);
            }

            return copiedPlacements;
        }

        private static Dictionary<ContainerId, ContainerPlacementState> CopyContainerPlacements(
            IReadOnlyDictionary<ContainerId, ContainerPlacementState> containerPlacements)
        {
            if (containerPlacements == null)
            {
                throw new ArgumentNullException(nameof(containerPlacements));
            }

            Dictionary<ContainerId, ContainerPlacementState> copiedPlacements =
                new Dictionary<ContainerId, ContainerPlacementState>();

            foreach (KeyValuePair<ContainerId, ContainerPlacementState> pair in containerPlacements)
            {
                if (pair.Value == null)
                {
                    throw new ArgumentException("Container placements cannot contain null items.", nameof(containerPlacements));
                }

                if (pair.Key != pair.Value.ContainerId)
                {
                    throw new ArgumentException("Container placement dictionary key must match the placement Container ID.", nameof(containerPlacements));
                }

                if (copiedPlacements.ContainsKey(pair.Value.ContainerId))
                {
                    throw new ArgumentException("Container placements cannot contain duplicate Container IDs.", nameof(containerPlacements));
                }

                copiedPlacements.Add(pair.Value.ContainerId, pair.Value);
            }

            return copiedPlacements;
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

        private static Dictionary<PlayAreaId, PlayAreaState> CopyPlayAreas(IEnumerable<PlayAreaState> playAreas)
        {
            if (playAreas == null)
            {
                throw new ArgumentNullException(nameof(playAreas));
            }

            Dictionary<PlayAreaId, PlayAreaState> copiedPlayAreas = new Dictionary<PlayAreaId, PlayAreaState>();

            foreach (PlayAreaState playArea in playAreas)
            {
                if (playArea == null)
                {
                    throw new ArgumentException("Play Areas cannot contain null items.", nameof(playAreas));
                }

                if (copiedPlayAreas.ContainsKey(playArea.Id))
                {
                    throw new ArgumentException("Play Areas cannot contain duplicate Play Area IDs.", nameof(playAreas));
                }

                copiedPlayAreas.Add(playArea.Id, playArea);
            }

            return copiedPlayAreas;
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

        private void ValidateContainerPlacementConsistency()
        {
            foreach (ContainerPlacementState placement in containerPlacements.Values)
            {
                if (!containers.TryGetValue(placement.ContainerId, out ContainerState container))
                {
                    throw new ArgumentException("Container placement references a missing Container.", nameof(containerPlacements));
                }

                if (!CanHavePlacement(container.Kind))
                {
                    throw new ArgumentException("Container placement is only valid for Deck, Stack, and DiscardPile Containers.", nameof(containerPlacements));
                }
            }
        }

        private static bool CanHavePlacement(ContainerKind kind)
        {
            return kind == ContainerKind.Deck
                || kind == ContainerKind.Stack
                || kind == ContainerKind.DiscardPile;
        }

        private bool IsReferencedBySeatHand(ContainerId containerId)
        {
            foreach (SeatState seat in seats.Values)
            {
                if (seat.HandContainerId == containerId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsReferencedByConsoleSlot(ContainerId containerId)
        {
            foreach (SeatState seat in seats.Values)
            {
                if (seat.Console.ContainsSlot(containerId))
                {
                    return true;
                }
            }

            foreach (PlacedConsoleState placedConsole in placedConsoles.Values)
            {
                if (placedConsole.Console.ContainsSlot(containerId))
                {
                    return true;
                }
            }

            return false;
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

            foreach (DieState die in dice.Values)
            {
                objectStates.Add(die.BaseState.Id, die.BaseState);
            }

            return objectStates;
        }

        private void AddUncontainedObject<TState>(
            TabletopObjectState baseState,
            TState state,
            IDictionary<TabletopObjectId, TState> destination,
            string parameterName)
            where TState : class
        {
            if (state == null || baseState == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (!baseState.ContainerId.IsEmpty)
            {
                throw new InvalidOperationException("Only uncontained Tabletop Objects can be added through this operation.");
            }

            if (ContainsObject(baseState.Id))
            {
                throw new ArgumentException("Tabletop Object ID already exists in the Match.", parameterName);
            }

            destination.Add(baseState.Id, state);
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
