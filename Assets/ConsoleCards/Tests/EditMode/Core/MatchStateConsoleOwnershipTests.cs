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
    public sealed class MatchStateConsoleOwnershipTests
    {
        [Test]
        public void TryGetSeatConsole_WhenSeatExists_ReturnsExactConsoleInstance()
        {
            ConsoleFixture fixture = CreateValidFixture(slotCount: 2);

            bool resolved = fixture.Match.TryGetSeatConsole(fixture.Seat.Id, out ConsoleState console);

            Assert.That(resolved, Is.True);
            Assert.That(console, Is.SameAs(fixture.Console));
            Assert.That(console.OwnerSeatId, Is.EqualTo(fixture.Seat.Id));
        }

        [Test]
        public void TryGetSeatConsole_WhenSeatIsMissing_ReturnsFalseAndNullConsole()
        {
            ConsoleFixture fixture = CreateValidFixture();

            bool resolved = fixture.Match.TryGetSeatConsole(SeatId.New(), out ConsoleState console);

            Assert.That(resolved, Is.False);
            Assert.That(console, Is.Null);
        }

        [Test]
        public void TryGetSeatConsole_WhenSeatIdIsEmpty_ReturnsFalseAndNullConsole()
        {
            ConsoleFixture fixture = CreateValidFixture();

            bool resolved = fixture.Match.TryGetSeatConsole(SeatId.Empty, out ConsoleState console);

            Assert.That(resolved, Is.False);
            Assert.That(console, Is.Null);
        }

        [Test]
        public void TryGetConsoleSlot_WhenSlotExists_ReturnsExactOrderedSlotContainer()
        {
            ConsoleFixture fixture = CreateValidFixture(slotCount: 2);

            bool firstResolved = fixture.Match.TryGetConsoleSlot(fixture.Seat.Id, 0, out ContainerState firstSlot);
            bool secondResolved = fixture.Match.TryGetConsoleSlot(fixture.Seat.Id, 1, out ContainerState secondSlot);

            Assert.That(firstResolved, Is.True);
            Assert.That(secondResolved, Is.True);
            Assert.That(firstSlot, Is.SameAs(fixture.Slots[0]));
            Assert.That(secondSlot, Is.SameAs(fixture.Slots[1]));
            Assert.That(fixture.Console.SlotContainerIds, Is.EqualTo(fixture.Slots.Select(slot => slot.Id).ToArray()));
        }

        [Test]
        public void TryGetConsoleSlot_WhenConsoleSeatIsMissing_ReturnsFalseAndNullSlot()
        {
            ConsoleFixture fixture = CreateValidFixture();

            bool resolved = fixture.Match.TryGetConsoleSlot(SeatId.New(), 0, out ContainerState slot);

            Assert.That(resolved, Is.False);
            Assert.That(slot, Is.Null);
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void TryGetConsoleSlot_WhenSlotIndexIsInvalid_ReturnsFalseAndNullSlot(int slotIndex)
        {
            ConsoleFixture fixture = CreateValidFixture(slotCount: 2);

            bool resolved = fixture.Match.TryGetConsoleSlot(fixture.Seat.Id, slotIndex, out ContainerState slot);

            Assert.That(resolved, Is.False);
            Assert.That(slot, Is.Null);
        }

        [Test]
        public void SeatState_WhenConsoleIsMissing_RejectsSeatBeforeMatchConstruction()
        {
            SeatId seatId = SeatId.New();

            Assert.That(
                () => new SeatState(
                    seatId,
                    CreatePose(),
                    ContainerId.New(),
                    null,
                    PlayerId.Empty,
                    SeatStatus.Vacant),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_WhenConsoleSlotIsMissing_RejectsMatch()
        {
            SeatId seatId = SeatId.New();
            ContainerState hand = CreateContainer(ContainerKind.Hand, seatId);
            ContainerId missingSlotId = ContainerId.New();
            SeatState seat = CreateSeat(seatId, hand.Id, new[] { missingSlotId });

            Assert.That(
                () => CreateMatch(Array.Empty<CardInstanceState>(), new[] { hand }, new[] { seat }),
                Throws.ArgumentException);
        }

        [TestCase(ContainerKind.Generic)]
        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.DiscardPile)]
        public void Constructor_WhenConsoleReferencesNonSlotContainer_RejectsMatch(ContainerKind slotKind)
        {
            SeatId seatId = SeatId.New();
            ContainerState hand = CreateContainer(ContainerKind.Hand, seatId);
            ContainerState slot = CreateContainer(slotKind, OwnerFor(slotKind, seatId));
            SeatState seat = CreateSeat(seatId, hand.Id, new[] { slot.Id });

            Assert.That(
                () => CreateMatch(Array.Empty<CardInstanceState>(), new[] { hand, slot }, new[] { seat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenConsoleSlotOwnerDoesNotMatchSeat_RejectsMatch()
        {
            SeatId seatId = SeatId.New();
            ContainerState hand = CreateContainer(ContainerKind.Hand, seatId);
            ContainerState slot = CreateContainer(ContainerKind.ConsoleSlot, SeatId.New());
            SeatState seat = CreateSeat(seatId, hand.Id, new[] { slot.Id });

            Assert.That(
                () => CreateMatch(Array.Empty<CardInstanceState>(), new[] { hand, slot }, new[] { seat }),
                Throws.ArgumentException);
        }

        [Test]
        public void ConsoleState_WhenSlotIdsAreDuplicate_RejectsConsole()
        {
            ContainerId slotId = ContainerId.New();

            Assert.That(
                () => new ConsoleState(SeatId.New(), new[] { slotId, slotId }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenTwoConsolesShareOneSlot_RejectsMatch()
        {
            SeatId firstSeatId = SeatId.New();
            SeatId secondSeatId = SeatId.New();
            ContainerState firstHand = CreateContainer(ContainerKind.Hand, firstSeatId);
            ContainerState secondHand = CreateContainer(ContainerKind.Hand, secondSeatId);
            ContainerState sharedSlot = CreateContainer(ContainerKind.ConsoleSlot, firstSeatId);
            SeatState firstSeat = CreateSeat(firstSeatId, firstHand.Id, new[] { sharedSlot.Id });
            SeatState secondSeat = CreateSeat(secondSeatId, secondHand.Id, new[] { sharedSlot.Id });

            Assert.That(
                () => CreateMatch(
                    Array.Empty<CardInstanceState>(),
                    new[] { firstHand, secondHand, sharedSlot },
                    new[] { firstSeat, secondSeat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenHandContainerIsUsedAsConsoleSlot_RejectsMatch()
        {
            SeatId seatId = SeatId.New();
            ContainerState seatHand = CreateContainer(ContainerKind.Hand, seatId);
            ContainerState handUsedAsSlot = CreateContainer(ContainerKind.Hand, seatId);
            SeatState seat = CreateSeat(seatId, seatHand.Id, new[] { handUsedAsSlot.Id });

            Assert.That(
                () => CreateMatch(
                    Array.Empty<CardInstanceState>(),
                    new[] { seatHand, handUsedAsSlot },
                    new[] { seat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenConsoleSlotContainerIsUsedAsSeatHand_RejectsMatch()
        {
            SeatId seatId = SeatId.New();
            ContainerState slotUsedAsHand = CreateContainer(ContainerKind.ConsoleSlot, seatId);
            ContainerState validSlot = CreateContainer(ContainerKind.ConsoleSlot, seatId);
            SeatState seat = CreateSeat(seatId, slotUsedAsHand.Id, new[] { validSlot.Id });

            Assert.That(
                () => CreateMatch(
                    Array.Empty<CardInstanceState>(),
                    new[] { slotUsedAsHand, validSlot },
                    new[] { seat }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenConsoleSlotHasPlacement_RejectsMatch()
        {
            ConsoleFixture fixture = CreateValidFixture(createMatch: false);
            ContainerPlacementState slotPlacement = new ContainerPlacementState(
                fixture.Slots[0].Id,
                CreatePose(x: 3.0, y: -2.0));

            Assert.That(
                () => CreateMatch(
                    fixture.Cards,
                    new[] { fixture.Hand }.Concat(fixture.Slots).ToArray(),
                    new[] { fixture.Seat },
                    new[] { slotPlacement }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenSlotCardMembershipMatchesContainerId_AcceptsMatch()
        {
            ConsoleFixture fixture = CreateValidFixture(slotCount: 2, slotCardCount: 2);

            Assert.That(fixture.Match.Cards.Values.Select(card => card.BaseState.ContainerId), Is.All.EqualTo(fixture.Slots[0].Id));
            Assert.That(fixture.Slots[0].ObjectIds, Is.EqualTo(fixture.Cards.Select(card => card.BaseState.Id).ToArray()));
            Assert.That(fixture.Match.TryGetConsoleSlot(fixture.Seat.Id, 0, out ContainerState slot), Is.True);
            Assert.That(slot, Is.SameAs(fixture.Slots[0]));
        }

        [Test]
        public void Constructor_WhenSlotCardContainerIdDoesNotMatchMembership_RejectsMatchAndPreservesState()
        {
            ConsoleFixture fixture = CreateValidFixture(slotCount: 1, createMatch: false);
            CardInstanceState card = CreateCard();
            new ContainerTransferService().PlaceIntoContainer(card.BaseState, fixture.Slots[0]);
            card.BaseState.SetContainer(ContainerId.New());
            ObjectSnapshot objectSnapshot = ObjectSnapshot.Capture(card.BaseState);
            TabletopObjectId[] slotOrder = fixture.Slots[0].ObjectIds.ToArray();

            Assert.That(
                () => CreateMatch(
                    new[] { card },
                    new[] { fixture.Hand }.Concat(fixture.Slots).ToArray(),
                    new[] { fixture.Seat }),
                Throws.ArgumentException);

            objectSnapshot.AssertMatches(card.BaseState);
            Assert.That(fixture.Slots[0].ObjectIds, Is.EqualTo(slotOrder));
        }

        [Test]
        public void Constructor_WhenValid_PreservesSeatConsoleSlotIdentitiesAndRevision()
        {
            ConsoleFixture fixture = CreateValidFixture(slotCount: 2, revision: 8);

            Assert.That(fixture.Match.Revision, Is.EqualTo(8));
            Assert.That(fixture.Match.Seats[fixture.Seat.Id], Is.SameAs(fixture.Seat));
            Assert.That(fixture.Match.Seats[fixture.Seat.Id].Console, Is.SameAs(fixture.Console));
            Assert.That(fixture.Match.Containers[fixture.Slots[0].Id], Is.SameAs(fixture.Slots[0]));
            Assert.That(fixture.Match.Containers[fixture.Slots[1].Id], Is.SameAs(fixture.Slots[1]));
        }

        [Test]
        public void RemoveEmptyContainer_WhenConsoleSlotIsReferenced_RemainsProtectedAndResolvable()
        {
            ConsoleFixture fixture = CreateValidFixture(slotCount: 1, revision: 11);

            Assert.That(
                () => fixture.Match.RemoveEmptyContainer(fixture.Slots[0].Id),
                Throws.TypeOf<InvalidOperationException>());

            Assert.That(fixture.Match.Revision, Is.EqualTo(11));
            Assert.That(fixture.Match.Seats[fixture.Seat.Id], Is.SameAs(fixture.Seat));
            Assert.That(fixture.Match.TryGetSeatConsole(fixture.Seat.Id, out ConsoleState console), Is.True);
            Assert.That(console, Is.SameAs(fixture.Console));
            Assert.That(fixture.Match.TryGetConsoleSlot(fixture.Seat.Id, 0, out ContainerState slot), Is.True);
            Assert.That(slot, Is.SameAs(fixture.Slots[0]));
        }

        [Test]
        public void ConsoleSlotBoundary_UsesContainerStateOnlyAndNoCoreUnityDependency()
        {
            Type[] coreTypes = typeof(MatchState).Assembly.GetTypes();
            string[] referencedAssemblies = typeof(MatchState).Assembly.GetReferencedAssemblies()
                .Select(name => name.Name)
                .ToArray();

            Assert.That(coreTypes.Any(type => type.Name == "ConsoleSlotState"), Is.False);
            Assert.That(typeof(ContainerState).GetProperty("IsOccupied"), Is.Null);
            Assert.That(referencedAssemblies, Does.Not.Contain("UnityEngine.CoreModule"));
        }

        private static ConsoleFixture CreateValidFixture(
            int slotCount = 1,
            int slotCardCount = 0,
            long revision = 0,
            bool createMatch = true)
        {
            SeatId seatId = SeatId.New();
            ContainerState hand = CreateContainer(ContainerKind.Hand, seatId, ObjectVisibility.OwnerOnly, capacity: 5);
            List<ContainerState> slots = new List<ContainerState>();
            List<ContainerId> slotIds = new List<ContainerId>();
            List<CardInstanceState> cards = new List<CardInstanceState>();
            ContainerTransferService transferService = new ContainerTransferService();

            for (int index = 0; index < slotCount; index++)
            {
                ContainerState slot = CreateContainer(
                    ContainerKind.ConsoleSlot,
                    seatId,
                    ObjectVisibility.SeatOnly,
                    capacity: 4);
                slots.Add(slot);
                slotIds.Add(slot.Id);
            }

            for (int index = 0; index < slotCardCount; index++)
            {
                CardInstanceState card = CreateCard();
                transferService.PlaceIntoContainer(card.BaseState, slots[0]);
                cards.Add(card);
            }

            SeatState seat = CreateSeat(seatId, hand.Id, slotIds);
            MatchState match = createMatch
                ? CreateMatch(cards.ToArray(), new[] { hand }.Concat(slots).ToArray(), new[] { seat }, revision: revision)
                : null;

            return new ConsoleFixture(match, hand, slots.ToArray(), seat, seat.Console, cards.ToArray());
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

        private static SeatState CreateSeat(
            SeatId seatId,
            ContainerId handContainerId,
            IEnumerable<ContainerId> slotContainerIds)
        {
            return new SeatState(
                seatId,
                CreatePose(x: -5.0, y: 4.0),
                handContainerId,
                new ConsoleState(seatId, slotContainerIds),
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

        private sealed class ConsoleFixture
        {
            public ConsoleFixture(
                MatchState match,
                ContainerState hand,
                ContainerState[] slots,
                SeatState seat,
                ConsoleState console,
                CardInstanceState[] cards)
            {
                Match = match;
                Hand = hand;
                Slots = slots;
                Seat = seat;
                Console = console;
                Cards = cards;
            }

            public MatchState Match { get; }

            public ContainerState Hand { get; }

            public ContainerState[] Slots { get; }

            public SeatState Seat { get; }

            public ConsoleState Console { get; }

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
