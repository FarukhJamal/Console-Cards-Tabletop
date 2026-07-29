using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class MatchStateHandOwnershipTests
    {
        [Test]
        public void TryGetSeatHand_WhenSeatExists_ReturnsExactHandContainer()
        {
            HandFixture fixture = CreateValidFixture();

            bool resolved = fixture.Match.TryGetSeatHand(fixture.Seat.Id, out ContainerState handContainer);

            Assert.That(resolved, Is.True);
            Assert.That(handContainer, Is.SameAs(fixture.Hand));
            Assert.That(handContainer.Kind, Is.EqualTo(ContainerKind.Hand));
            Assert.That(handContainer.OwnerSeatId, Is.EqualTo(fixture.Seat.Id));
        }

        [Test]
        public void TryGetSeatHand_WhenSeatIsMissing_ReturnsFalseAndNullHand()
        {
            HandFixture fixture = CreateValidFixture();

            bool resolved = fixture.Match.TryGetSeatHand(SeatId.New(), out ContainerState handContainer);

            Assert.That(resolved, Is.False);
            Assert.That(handContainer, Is.Null);
        }

        [Test]
        public void TryGetSeatHand_WhenSeatIdIsEmpty_ReturnsFalseAndNullHand()
        {
            HandFixture fixture = CreateValidFixture();

            bool resolved = fixture.Match.TryGetSeatHand(SeatId.Empty, out ContainerState handContainer);

            Assert.That(resolved, Is.False);
            Assert.That(handContainer, Is.Null);
        }

        [Test]
        public void Constructor_WhenSeatHandIsMissing_RejectsMatch()
        {
            SeatId seatId = SeatId.New();
            ContainerState slot = CreateContainer(ContainerKind.ConsoleSlot, seatId, capacity: 1);
            SeatState seat = CreateSeat(seatId, ContainerId.New(), slot.Id);

            Assert.That(
                () => CreateMatch(Array.Empty<CardInstanceState>(), new[] { slot }, new[] { seat }),
                Throws.ArgumentException);
        }

        [TestCase(ContainerKind.Generic)]
        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.ConsoleSlot)]
        public void Constructor_WhenSeatReferencesNonHandContainer_RejectsMatch(ContainerKind handKind)
        {
            SeatId seatId = SeatId.New();
            ContainerState hand = CreateContainer(handKind, OwnerFor(handKind, seatId), capacity: handKind == ContainerKind.ConsoleSlot ? 1 : 0);
            ContainerState slot = CreateContainer(ContainerKind.ConsoleSlot, seatId, capacity: 1);
            SeatState seat = CreateSeat(seatId, hand.Id, slot.Id);

            Assert.That(
                () => CreateMatch(Array.Empty<CardInstanceState>(), new[] { hand, slot }, new[] { seat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenHandOwnerDoesNotMatchSeat_RejectsMatch()
        {
            SeatId seatId = SeatId.New();
            ContainerState hand = CreateContainer(ContainerKind.Hand, SeatId.New());
            ContainerState slot = CreateContainer(ContainerKind.ConsoleSlot, seatId, capacity: 1);
            SeatState seat = CreateSeat(seatId, hand.Id, slot.Id);

            Assert.That(
                () => CreateMatch(Array.Empty<CardInstanceState>(), new[] { hand, slot }, new[] { seat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenTwoSeatsShareHand_RejectsMatch()
        {
            SeatId firstSeatId = SeatId.New();
            SeatId secondSeatId = SeatId.New();
            ContainerState sharedHand = CreateContainer(ContainerKind.Hand, firstSeatId);
            ContainerState firstSlot = CreateContainer(ContainerKind.ConsoleSlot, firstSeatId, capacity: 1);
            ContainerState secondSlot = CreateContainer(ContainerKind.ConsoleSlot, secondSeatId, capacity: 1);
            SeatState firstSeat = CreateSeat(firstSeatId, sharedHand.Id, firstSlot.Id);
            SeatState secondSeat = CreateSeat(secondSeatId, sharedHand.Id, secondSlot.Id);

            Assert.That(
                () => CreateMatch(
                    Array.Empty<CardInstanceState>(),
                    new[] { sharedHand, firstSlot, secondSlot },
                    new[] { firstSeat, secondSeat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenHandHasPlacement_RejectsMatch()
        {
            HandFixture fixture = CreateValidFixture(createMatch: false);
            ContainerPlacementState handPlacement = new ContainerPlacementState(fixture.Hand.Id, CreatePose(x: 2.0, y: 3.0));

            Assert.That(
                () => CreateMatch(
                    fixture.Cards,
                    new[] { fixture.Hand, fixture.Slot },
                    new[] { fixture.Seat },
                    new[] { handPlacement }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenHandCardMembershipMatchesContainerId_AcceptsMatch()
        {
            HandFixture fixture = CreateValidFixture(handCardCount: 2);

            Assert.That(fixture.Match.Cards.Values.Select(card => card.BaseState.ContainerId), Is.All.EqualTo(fixture.Hand.Id));
            Assert.That(fixture.Hand.ObjectIds, Is.EqualTo(fixture.Cards.Select(card => card.BaseState.Id).ToArray()));
            Assert.That(fixture.Match.TryGetSeatHand(fixture.Seat.Id, out ContainerState handContainer), Is.True);
            Assert.That(handContainer, Is.SameAs(fixture.Hand));
        }

        [Test]
        public void Constructor_WhenHandCardContainerIdDoesNotMatchMembership_RejectsMatch()
        {
            SeatId seatId = SeatId.New();
            ContainerState hand = CreateContainer(ContainerKind.Hand, seatId);
            ContainerState slot = CreateContainer(ContainerKind.ConsoleSlot, seatId, capacity: 1);
            SeatState seat = CreateSeat(seatId, hand.Id, slot.Id);
            CardInstanceState card = CreateCard();
            new ContainerTransferService().PlaceIntoContainer(card.BaseState, hand);
            card.BaseState.SetContainer(ContainerId.New());
            ObjectSnapshot snapshot = ObjectSnapshot.Capture(card.BaseState);
            TabletopObjectId[] handOrder = hand.ObjectIds.ToArray();

            Assert.That(
                () => CreateMatch(new[] { card }, new[] { hand, slot }, new[] { seat }),
                Throws.ArgumentException);
            snapshot.AssertMatches(card.BaseState);
            Assert.That(hand.ObjectIds, Is.EqualTo(handOrder));
        }

        [Test]
        public void Constructor_WhenValid_PreservesSeatHandAndRevisionIdentity()
        {
            HandFixture fixture = CreateValidFixture(revision: 12);

            Assert.That(fixture.Match.Seats[fixture.Seat.Id], Is.SameAs(fixture.Seat));
            Assert.That(fixture.Match.Containers[fixture.Hand.Id], Is.SameAs(fixture.Hand));
            Assert.That(fixture.Match.Revision, Is.EqualTo(12));
        }

        [Test]
        public void HandStateBoundary_UsesContainerStateOnlyAndNoCoreUnityDependency()
        {
            Type[] coreTypes = typeof(MatchState).Assembly.GetTypes();
            string[] referencedAssemblies = typeof(MatchState).Assembly.GetReferencedAssemblies()
                .Select(name => name.Name)
                .ToArray();

            Assert.That(coreTypes.Any(type => type.Name == "HandState"), Is.False);
            Assert.That(typeof(ContainerState).GetProperty("IsPrivate"), Is.Null);
            Assert.That(referencedAssemblies, Does.Not.Contain("UnityEngine.CoreModule"));
        }

        [Test]
        public void HandVisibilityBoundary_RemainsMetadataOnly()
        {
            HandFixture fixture = CreateValidFixture(handVisibility: ObjectVisibility.OwnerOnly);

            Assert.That(fixture.Hand.OwnerSeatId, Is.EqualTo(fixture.Seat.Id));
            Assert.That(fixture.Hand.Visibility, Is.EqualTo(ObjectVisibility.OwnerOnly));
            Assert.That(fixture.Match.TryGetSeatHand(fixture.Seat.Id, out ContainerState handContainer), Is.True);
            Assert.That(handContainer.Visibility, Is.EqualTo(ObjectVisibility.OwnerOnly));
        }

        private static HandFixture CreateValidFixture(
            int handCardCount = 0,
            ObjectVisibility handVisibility = ObjectVisibility.OwnerOnly,
            long revision = 0,
            bool createMatch = true)
        {
            SeatId seatId = SeatId.New();
            ContainerState hand = CreateContainer(ContainerKind.Hand, seatId, visibility: handVisibility, capacity: 5);
            ContainerState slot = CreateContainer(ContainerKind.ConsoleSlot, seatId, capacity: 1);
            SeatState seat = CreateSeat(seatId, hand.Id, slot.Id);
            List<CardInstanceState> cards = new List<CardInstanceState>();
            ContainerTransferService transferService = new ContainerTransferService();

            for (int index = 0; index < handCardCount; index++)
            {
                CardInstanceState card = CreateCard();
                transferService.PlaceIntoContainer(card.BaseState, hand);
                cards.Add(card);
            }

            MatchState match = createMatch
                ? CreateMatch(cards.ToArray(), new[] { hand, slot }, new[] { seat }, revision: revision)
                : null;

            return new HandFixture(match, hand, slot, seat, cards.ToArray());
        }

        private static MatchState CreateMatch(
            CardInstanceState[] cards,
            ContainerState[] containers,
            SeatState[] seats,
            ContainerPlacementState[] placements = null,
            long revision = 0)
        {
            return new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                cards,
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                containers,
                seats,
                placements ?? Array.Empty<ContainerPlacementState>());
        }

        private static SeatState CreateSeat(SeatId seatId, ContainerId handContainerId, ContainerId slotContainerId)
        {
            return new SeatState(
                seatId,
                CreatePose(x: -4.0, y: 5.0),
                handContainerId,
                new ConsoleState(seatId, new[] { slotContainerId }),
                PlayerId.Empty,
                SeatStatus.Vacant);
        }

        private static ContainerState CreateContainer(
            ContainerKind kind,
            SeatId? ownerSeatId = null,
            ObjectVisibility visibility = ObjectVisibility.Public,
            int capacity = 0)
        {
            return new ContainerState(
                ContainerId.New(),
                kind,
                ownerSeatId ?? SeatId.Empty,
                visibility,
                capacity);
        }

        private static CardInstanceState CreateCard()
        {
            return new CardInstanceState(
                new TabletopObjectState(
                    TabletopObjectId.New(),
                    ObjectDefinitionId.New(),
                    TabletopObjectKind.Card,
                    CreatePose(x: 1.0, y: 2.0, rotationDegrees: 30f, layer: 2, localOrder: 3),
                    ContainerId.Empty,
                    PlayerId.New(),
                    ObjectVisibility.OwnerOnly,
                    false),
                CardFace.FaceDown);
        }

        private static SeatId OwnerFor(ContainerKind kind, SeatId seatId)
        {
            return kind == ContainerKind.Hand || kind == ContainerKind.ConsoleSlot
                ? seatId
                : SeatId.Empty;
        }

        private static TabletopPose CreatePose(
            double x = 0.0,
            double y = 0.0,
            float rotationDegrees = 0f,
            int layer = 0,
            int localOrder = 0)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotationDegrees, layer, localOrder);
        }

        private sealed class HandFixture
        {
            public HandFixture(
                MatchState match,
                ContainerState hand,
                ContainerState slot,
                SeatState seat,
                CardInstanceState[] cards)
            {
                Match = match;
                Hand = hand;
                Slot = slot;
                Seat = seat;
                Cards = cards;
            }

            public MatchState Match { get; }

            public ContainerState Hand { get; }

            public ContainerState Slot { get; }

            public SeatState Seat { get; }

            public CardInstanceState[] Cards { get; }
        }

        private sealed class ObjectSnapshot
        {
            private ObjectSnapshot(
                TabletopObjectId id,
                ObjectDefinitionId definitionId,
                TabletopObjectKind kind,
                TabletopPose pose,
                ContainerId containerId,
                PlayerId ownerPlayerId,
                ObjectVisibility visibility,
                bool isUserLocked)
            {
                Id = id;
                DefinitionId = definitionId;
                Kind = kind;
                Pose = pose;
                ContainerId = containerId;
                OwnerPlayerId = ownerPlayerId;
                Visibility = visibility;
                IsUserLocked = isUserLocked;
            }

            private TabletopObjectId Id { get; }

            private ObjectDefinitionId DefinitionId { get; }

            private TabletopObjectKind Kind { get; }

            private TabletopPose Pose { get; }

            private ContainerId ContainerId { get; }

            private PlayerId OwnerPlayerId { get; }

            private ObjectVisibility Visibility { get; }

            private bool IsUserLocked { get; }

            public static ObjectSnapshot Capture(TabletopObjectState state)
            {
                return new ObjectSnapshot(
                    state.Id,
                    state.DefinitionId,
                    state.Kind,
                    state.Pose,
                    state.ContainerId,
                    state.OwnerPlayerId,
                    state.Visibility,
                    state.IsUserLocked);
            }

            public void AssertMatches(TabletopObjectState state)
            {
                Assert.That(state.Id, Is.EqualTo(Id));
                Assert.That(state.DefinitionId, Is.EqualTo(DefinitionId));
                Assert.That(state.Kind, Is.EqualTo(Kind));
                Assert.That(state.Pose, Is.EqualTo(Pose));
                Assert.That(state.ContainerId, Is.EqualTo(ContainerId));
                Assert.That(state.OwnerPlayerId, Is.EqualTo(OwnerPlayerId));
                Assert.That(state.Visibility, Is.EqualTo(Visibility));
                Assert.That(state.IsUserLocked, Is.EqualTo(IsUserLocked));
            }
        }
    }
}
