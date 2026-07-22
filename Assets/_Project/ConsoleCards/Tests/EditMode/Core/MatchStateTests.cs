using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class MatchStateTests
    {
        [Test]
        public void Constructor_WhenEmptyMatchIsValid_AcceptsValue()
        {
            MatchState match = CreateMatch();

            Assert.That(match.ObjectCount, Is.EqualTo(0));
            Assert.That(match.Cards, Is.Empty);
            Assert.That(match.Pawns, Is.Empty);
            Assert.That(match.Tokens, Is.Empty);
            Assert.That(match.Containers, Is.Empty);
            Assert.That(match.Seats, Is.Empty);
        }

        [Test]
        public void Constructor_WhenMatchIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => CreateMatch(id: MatchId.Empty),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenGameTemplateIdIsEmpty_AcceptsValue()
        {
            MatchState match = CreateMatch(gameTemplateId: GameTemplateId.Empty);

            Assert.That(match.GameTemplateId, Is.EqualTo(GameTemplateId.Empty));
        }

        [Test]
        public void Constructor_WhenRevisionIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => CreateMatch(revision: -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WhenCollectionArgumentIsNull_ThrowsArgumentNullException()
        {
            Assert.That(
                () => new MatchState(
                    MatchId.New(),
                    GameTemplateId.New(),
                    0,
                    null,
                    Array.Empty<PawnState>(),
                    Array.Empty<TokenState>(),
                    Array.Empty<ContainerState>(),
                    Array.Empty<SeatState>()),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new MatchState(
                    MatchId.New(),
                    GameTemplateId.New(),
                    0,
                    Array.Empty<CardInstanceState>(),
                    null,
                    Array.Empty<TokenState>(),
                    Array.Empty<ContainerState>(),
                    Array.Empty<SeatState>()),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new MatchState(
                    MatchId.New(),
                    GameTemplateId.New(),
                    0,
                    Array.Empty<CardInstanceState>(),
                    Array.Empty<PawnState>(),
                    null,
                    Array.Empty<ContainerState>(),
                    Array.Empty<SeatState>()),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new MatchState(
                    MatchId.New(),
                    GameTemplateId.New(),
                    0,
                    Array.Empty<CardInstanceState>(),
                    Array.Empty<PawnState>(),
                    Array.Empty<TokenState>(),
                    null,
                    Array.Empty<SeatState>()),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new MatchState(
                    MatchId.New(),
                    GameTemplateId.New(),
                    0,
                    Array.Empty<CardInstanceState>(),
                    Array.Empty<PawnState>(),
                    Array.Empty<TokenState>(),
                    Array.Empty<ContainerState>(),
                    null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_WhenCollectionItemIsNull_ThrowsArgumentException()
        {
            Assert.That(
                () => CreateMatch(cards: new CardInstanceState[] { null }),
                Throws.ArgumentException);
            Assert.That(
                () => CreateMatch(pawns: new PawnState[] { null }),
                Throws.ArgumentException);
            Assert.That(
                () => CreateMatch(tokens: new TokenState[] { null }),
                Throws.ArgumentException);
            Assert.That(
                () => CreateMatch(containers: new ContainerState[] { null }),
                Throws.ArgumentException);
            Assert.That(
                () => CreateMatch(seats: new SeatState[] { null }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenCardIdIsDuplicate_ThrowsArgumentException()
        {
            TabletopObjectId objectId = TabletopObjectId.New();

            Assert.That(
                () => CreateMatch(cards: new[] { CreateCard(id: objectId), CreateCard(id: objectId) }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenObjectIdIsDuplicateAcrossCategories_ThrowsArgumentException()
        {
            TabletopObjectId objectId = TabletopObjectId.New();

            Assert.That(
                () => CreateMatch(
                    cards: new[] { CreateCard(id: objectId) },
                    pawns: new[] { CreatePawn(id: objectId) }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenContainerIdIsDuplicate_ThrowsArgumentException()
        {
            ContainerId containerId = ContainerId.New();

            Assert.That(
                () => CreateMatch(containers: new[]
                {
                    CreateContainer(id: containerId),
                    CreateContainer(id: containerId)
                }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenSeatIdIsDuplicate_ThrowsArgumentException()
        {
            SeatId seatId = SeatId.New();
            SeatState firstSeat = CreateSeat(seatId, ContainerId.New(), Array.Empty<ContainerId>());
            SeatState secondSeat = CreateSeat(seatId, ContainerId.New(), Array.Empty<ContainerId>());

            Assert.That(
                () => CreateMatch(seats: new[] { firstSeat, secondSeat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenObjectReferencesMissingContainer_ThrowsArgumentException()
        {
            CardInstanceState card = CreateCard(containerId: ContainerId.New());

            Assert.That(
                () => CreateMatch(cards: new[] { card }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenContainerReferencesMissingObject_ThrowsArgumentException()
        {
            ContainerState container = CreateContainer();
            CardInstanceState card = CreateCard();
            PlaceIntoContainer(card, container);

            Assert.That(
                () => CreateMatch(containers: new[] { container }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenObjectAndContainerMembershipMismatch_ThrowsArgumentException()
        {
            ContainerState firstContainer = CreateContainer();
            ContainerState secondContainer = CreateContainer();
            CardInstanceState card = CreateCard();
            PlaceIntoContainer(card, firstContainer);
            card.BaseState.SetContainer(secondContainer.Id);

            Assert.That(
                () => CreateMatch(
                    cards: new[] { card },
                    containers: new[] { firstContainer, secondContainer }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenSeatReferencesMissingHandContainer_ThrowsArgumentException()
        {
            SeatFixture fixture = CreateSeatFixture();

            Assert.That(
                () => CreateMatch(seats: new[] { fixture.Seat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenSeatHandContainerHasWrongKind_ThrowsArgumentException()
        {
            SeatFixture fixture = CreateSeatFixture();
            ContainerState wrongKindHand = CreateContainer(
                id: fixture.HandContainer.Id,
                kind: ContainerKind.Generic,
                ownerSeatId: fixture.Seat.Id);

            Assert.That(
                () => CreateMatch(
                    containers: new[] { wrongKindHand },
                    seats: new[] { fixture.Seat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenSeatHandContainerHasWrongOwner_ThrowsArgumentException()
        {
            SeatFixture fixture = CreateSeatFixture();
            ContainerState wrongOwnerHand = CreateContainer(
                id: fixture.HandContainer.Id,
                kind: ContainerKind.Hand,
                ownerSeatId: SeatId.New());

            Assert.That(
                () => CreateMatch(
                    containers: new[] { wrongOwnerHand },
                    seats: new[] { fixture.Seat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenConsoleSlotContainerIsMissing_ThrowsArgumentException()
        {
            SeatFixture fixture = CreateSeatFixture(slotCount: 1);

            Assert.That(
                () => CreateMatch(
                    containers: new[] { fixture.HandContainer },
                    seats: new[] { fixture.Seat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenConsoleSlotContainerHasWrongKind_ThrowsArgumentException()
        {
            SeatFixture fixture = CreateSeatFixture(slotCount: 1);
            ContainerState wrongKindSlot = CreateContainer(
                id: fixture.SlotContainers[0].Id,
                kind: ContainerKind.Generic,
                ownerSeatId: fixture.Seat.Id);

            Assert.That(
                () => CreateMatch(
                    containers: new[] { fixture.HandContainer, wrongKindSlot },
                    seats: new[] { fixture.Seat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenConsoleSlotContainerHasWrongOwner_ThrowsArgumentException()
        {
            SeatFixture fixture = CreateSeatFixture(slotCount: 1);
            ContainerState wrongOwnerSlot = CreateContainer(
                id: fixture.SlotContainers[0].Id,
                kind: ContainerKind.ConsoleSlot,
                ownerSeatId: SeatId.New());

            Assert.That(
                () => CreateMatch(
                    containers: new[] { fixture.HandContainer, wrongOwnerSlot },
                    seats: new[] { fixture.Seat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenMatchIsValid_ExposesAllTypedCollections()
        {
            CardInstanceState card = CreateCard();
            PawnState pawn = CreatePawn();
            TokenState token = CreateToken();
            ContainerState container = CreateContainer();
            SeatFixture fixture = CreateSeatFixture(slotCount: 1);

            MatchState match = CreateMatch(
                cards: new[] { card },
                pawns: new[] { pawn },
                tokens: new[] { token },
                containers: new[] { container, fixture.HandContainer, fixture.SlotContainers[0] },
                seats: new[] { fixture.Seat });

            Assert.That(match.Cards[card.BaseState.Id], Is.SameAs(card));
            Assert.That(match.Pawns[pawn.BaseState.Id], Is.SameAs(pawn));
            Assert.That(match.Tokens[token.BaseState.Id], Is.SameAs(token));
            Assert.That(match.Containers[container.Id], Is.SameAs(container));
            Assert.That(match.Seats[fixture.Seat.Id], Is.SameAs(fixture.Seat));
        }

        [Test]
        public void ObjectCount_IncludesCardsPawnsAndTokens()
        {
            MatchState match = CreateMatch(
                cards: new[] { CreateCard() },
                pawns: new[] { CreatePawn() },
                tokens: new[] { CreateToken() });

            Assert.That(match.ObjectCount, Is.EqualTo(3));
        }

        [Test]
        public void ContainsObject_WorksAcrossAllCategories()
        {
            CardInstanceState card = CreateCard();
            PawnState pawn = CreatePawn();
            TokenState token = CreateToken();
            MatchState match = CreateMatch(
                cards: new[] { card },
                pawns: new[] { pawn },
                tokens: new[] { token });

            Assert.That(match.ContainsObject(card.BaseState.Id), Is.True);
            Assert.That(match.ContainsObject(pawn.BaseState.Id), Is.True);
            Assert.That(match.ContainsObject(token.BaseState.Id), Is.True);
            Assert.That(match.ContainsObject(TabletopObjectId.New()), Is.False);
            Assert.That(match.ContainsObject(TabletopObjectId.Empty), Is.False);
        }

        [Test]
        public void GetObject_ReturnsEachExplicitObjectBaseState()
        {
            CardInstanceState card = CreateCard();
            PawnState pawn = CreatePawn();
            TokenState token = CreateToken();
            MatchState match = CreateMatch(
                cards: new[] { card },
                pawns: new[] { pawn },
                tokens: new[] { token });

            Assert.That(match.GetObject(card.BaseState.Id), Is.SameAs(card.BaseState));
            Assert.That(match.GetObject(pawn.BaseState.Id), Is.SameAs(pawn.BaseState));
            Assert.That(match.GetObject(token.BaseState.Id), Is.SameAs(token.BaseState));
        }

        [Test]
        public void GetObject_WhenMissing_ThrowsKeyNotFoundException()
        {
            MatchState match = CreateMatch();

            Assert.That(
                () => match.GetObject(TabletopObjectId.New()),
                Throws.TypeOf<KeyNotFoundException>());
            Assert.That(
                () => match.GetObject(TabletopObjectId.Empty),
                Throws.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void GetContainer_WhenMissing_ThrowsKeyNotFoundException()
        {
            MatchState match = CreateMatch();

            Assert.That(
                () => match.GetContainer(ContainerId.New()),
                Throws.TypeOf<KeyNotFoundException>());
            Assert.That(
                () => match.GetContainer(ContainerId.Empty),
                Throws.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void GetSeat_WhenMissing_ThrowsKeyNotFoundException()
        {
            MatchState match = CreateMatch();

            Assert.That(
                () => match.GetSeat(SeatId.New()),
                Throws.TypeOf<KeyNotFoundException>());
            Assert.That(
                () => match.GetSeat(SeatId.Empty),
                Throws.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void ReturnedDictionaries_CannotMutateInternalState()
        {
            CardInstanceState card = CreateCard();
            ContainerState container = CreateContainer();
            SeatFixture fixture = CreateSeatFixture();
            MatchState match = CreateMatch(
                cards: new[] { card },
                containers: new[] { container, fixture.HandContainer },
                seats: new[] { fixture.Seat });

            Assert.That(match.Cards as Dictionary<TabletopObjectId, CardInstanceState>, Is.Null);
            Assert.That(match.Containers as Dictionary<ContainerId, ContainerState>, Is.Null);
            Assert.That(match.Seats as Dictionary<SeatId, SeatState>, Is.Null);

            IDictionary<TabletopObjectId, CardInstanceState> cardDictionaryView =
                match.Cards as IDictionary<TabletopObjectId, CardInstanceState>;
            Assert.That(cardDictionaryView, Is.Not.Null);
            Assert.That(
                () => cardDictionaryView.Add(TabletopObjectId.New(), CreateCard()),
                Throws.TypeOf<NotSupportedException>());

            IDictionary<ContainerId, ContainerState> containerDictionaryView =
                match.Containers as IDictionary<ContainerId, ContainerState>;
            Assert.That(containerDictionaryView, Is.Not.Null);
            Assert.That(
                () => containerDictionaryView.Add(ContainerId.New(), CreateContainer()),
                Throws.TypeOf<NotSupportedException>());

            IDictionary<SeatId, SeatState> seatDictionaryView =
                match.Seats as IDictionary<SeatId, SeatState>;
            Assert.That(seatDictionaryView, Is.Not.Null);
            Assert.That(
                () => seatDictionaryView.Add(SeatId.New(), CreateSeatFixture().Seat),
                Throws.TypeOf<NotSupportedException>());

            Assert.That(match.Cards, Has.Count.EqualTo(1));
            Assert.That(match.Cards[card.BaseState.Id], Is.SameAs(card));
            Assert.That(match.Containers, Has.Count.EqualTo(2));
            Assert.That(match.Seats, Has.Count.EqualTo(1));
        }

        [Test]
        public void AdvanceRevision_IncrementsAndReturnsNewValue()
        {
            MatchState match = CreateMatch(revision: 3);

            long revision = match.AdvanceRevision();

            Assert.That(revision, Is.EqualTo(4));
            Assert.That(match.Revision, Is.EqualTo(4));
        }

        [Test]
        public void AdvanceRevision_WhenRevisionOverflows_ThrowsOverflowExceptionWithoutWrapping()
        {
            MatchState match = CreateMatch(revision: long.MaxValue);

            Assert.That(
                () => match.AdvanceRevision(),
                Throws.TypeOf<OverflowException>());
            Assert.That(match.Revision, Is.EqualTo(long.MaxValue));
        }

        private static MatchState CreateMatch(
            MatchId? id = null,
            GameTemplateId? gameTemplateId = null,
            long revision = 0,
            IEnumerable<CardInstanceState> cards = null,
            IEnumerable<PawnState> pawns = null,
            IEnumerable<TokenState> tokens = null,
            IEnumerable<ContainerState> containers = null,
            IEnumerable<SeatState> seats = null)
        {
            return new MatchState(
                id ?? MatchId.New(),
                gameTemplateId ?? GameTemplateId.New(),
                revision,
                cards ?? Array.Empty<CardInstanceState>(),
                pawns ?? Array.Empty<PawnState>(),
                tokens ?? Array.Empty<TokenState>(),
                containers ?? Array.Empty<ContainerState>(),
                seats ?? Array.Empty<SeatState>());
        }

        private static CardInstanceState CreateCard(
            TabletopObjectId? id = null,
            ContainerId? containerId = null)
        {
            return new CardInstanceState(
                CreateObjectState(TabletopObjectKind.Card, id, containerId),
                CardFace.FaceDown);
        }

        private static PawnState CreatePawn(
            TabletopObjectId? id = null,
            ContainerId? containerId = null)
        {
            return new PawnState(CreateObjectState(TabletopObjectKind.Pawn, id, containerId));
        }

        private static TokenState CreateToken(
            TabletopObjectId? id = null,
            ContainerId? containerId = null)
        {
            return new TokenState(CreateObjectState(TabletopObjectKind.Token, id, containerId));
        }

        private static TabletopObjectState CreateObjectState(
            TabletopObjectKind kind,
            TabletopObjectId? id = null,
            ContainerId? containerId = null)
        {
            return new TabletopObjectState(
                id ?? TabletopObjectId.New(),
                ObjectDefinitionId.New(),
                kind,
                TabletopPose.Default,
                containerId ?? ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);
        }

        private static ContainerState CreateContainer(
            ContainerId? id = null,
            ContainerKind kind = ContainerKind.Generic,
            SeatId? ownerSeatId = null)
        {
            return new ContainerState(
                id ?? ContainerId.New(),
                kind,
                ownerSeatId ?? SeatId.Empty,
                ObjectVisibility.Public,
                0);
        }

        private static SeatFixture CreateSeatFixture(int slotCount = 0)
        {
            SeatId seatId = SeatId.New();
            ContainerState handContainer = CreateContainer(
                kind: ContainerKind.Hand,
                ownerSeatId: seatId);
            List<ContainerState> slotContainers = new List<ContainerState>();
            List<ContainerId> slotContainerIds = new List<ContainerId>();

            for (int index = 0; index < slotCount; index++)
            {
                ContainerState slotContainer = CreateContainer(
                    kind: ContainerKind.ConsoleSlot,
                    ownerSeatId: seatId);
                slotContainers.Add(slotContainer);
                slotContainerIds.Add(slotContainer.Id);
            }

            return new SeatFixture(
                CreateSeat(seatId, handContainer.Id, slotContainerIds),
                handContainer,
                slotContainers.ToArray());
        }

        private static SeatState CreateSeat(
            SeatId seatId,
            ContainerId handContainerId,
            IEnumerable<ContainerId> slotContainerIds)
        {
            return new SeatState(
                seatId,
                TabletopPose.Default,
                handContainerId,
                new ConsoleState(seatId, slotContainerIds),
                PlayerId.Empty,
                SeatStatus.Vacant);
        }

        private static void PlaceIntoContainer(CardInstanceState card, ContainerState container)
        {
            ContainerTransferService service = new ContainerTransferService();
            service.PlaceIntoContainer(card.BaseState, container);
        }

        private sealed class SeatFixture
        {
            public SeatFixture(SeatState seat, ContainerState handContainer, ContainerState[] slotContainers)
            {
                Seat = seat;
                HandContainer = handContainer;
                SlotContainers = slotContainers;
            }

            public SeatState Seat { get; }

            public ContainerState HandContainer { get; }

            public ContainerState[] SlotContainers { get; }
        }
    }
}
