using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class HandContainerOperationIntegrationTests
    {
        public enum HandTransferPath
        {
            TabletopToHand,
            DeckToHand,
            StackToHand,
            HandToTabletop,
            HandToDiscardPile,
            HandToConsoleSlot,
            HandToAnotherHand
        }

        [Test]
        public void DrawCards_WhenDrawingOneFromDeckToEmptyHand_AppendsTopCardToHand()
        {
            OperationFixture fixture = CreateOperationFixture(deckCount: 2, handCount: 0, handCapacity: 5, revision: 3);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);
            CardInstanceState drawnCard = fixture.DeckCards[1];

            DrawCardsResult result = ExecuteDraw(fixture, count: 1);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(4));
            Assert.That(fixture.Match.Revision, Is.EqualTo(4));
            Assert.That(fixture.Deck.ObjectIds, Is.EqualTo(new[] { fixture.DeckCards[0].BaseState.Id }));
            Assert.That(fixture.Hand.ObjectIds, Is.EqualTo(new[] { drawnCard.BaseState.Id }));
            Assert.That(drawnCard.BaseState.ContainerId, Is.EqualTo(fixture.Hand.Id));
            AssertHandMetadata(fixture.Hand, fixture.Seat.Id, ObjectVisibility.OwnerOnly, 5);
            before.AssertUnchangedExceptDraw(
                fixture.Match,
                fixture.Deck.Id,
                fixture.Hand.Id,
                new[] { drawnCard.BaseState.Id },
                fixture.Hand.Id);
        }

        [Test]
        public void DrawCards_WhenDrawingMultipleToHand_AppendsInApprovedDrawSequence()
        {
            OperationFixture fixture = CreateOperationFixture(deckCount: 4, handCount: 1, handCapacity: 3, revision: 7);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);

            DrawCardsResult result = ExecuteDraw(fixture, count: 2);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(8));
            Assert.That(fixture.Deck.ObjectIds, Is.EqualTo(new[]
            {
                fixture.DeckCards[0].BaseState.Id,
                fixture.DeckCards[1].BaseState.Id
            }));
            Assert.That(fixture.Hand.ObjectIds, Is.EqualTo(new[]
            {
                fixture.HandCards[0].BaseState.Id,
                fixture.DeckCards[3].BaseState.Id,
                fixture.DeckCards[2].BaseState.Id
            }));
            AssertHandMetadata(fixture.Hand, fixture.Seat.Id, ObjectVisibility.OwnerOnly, 3);
            before.AssertUnchangedExceptDraw(
                fixture.Match,
                fixture.Deck.Id,
                fixture.Hand.Id,
                new[] { fixture.DeckCards[3].BaseState.Id, fixture.DeckCards[2].BaseState.Id },
                fixture.Hand.Id);
        }

        [Test]
        public void DrawCards_WhenHandCapacityRejectsCompleteDraw_PreservesDeckHandCardsAndRevision()
        {
            OperationFixture fixture = CreateOperationFixture(deckCount: 4, handCount: 1, handCapacity: 2, revision: 5);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);

            DrawCardsResult result = ExecuteDraw(fixture, count: 2);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Rejected));
            Assert.That(result.Error, Is.EqualTo(DrawCardsError.DestinationCapacityExceeded));
            before.AssertMatches(fixture.Match);
        }

        [TestCase(HandTransferPath.TabletopToHand)]
        [TestCase(HandTransferPath.DeckToHand)]
        [TestCase(HandTransferPath.StackToHand)]
        [TestCase(HandTransferPath.HandToTabletop)]
        [TestCase(HandTransferPath.HandToDiscardPile)]
        [TestCase(HandTransferPath.HandToConsoleSlot)]
        [TestCase(HandTransferPath.HandToAnotherHand)]
        public void TransferCard_WhenPathInvolvesHand_UsesStructuralRulesAndPreservesHandBoundary(
            HandTransferPath path)
        {
            TransferFixture fixture = CreateTransferFixture(path);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);
            TabletopPose originalPose = fixture.TargetCard.BaseState.Pose;
            TabletopObjectId[] originalSourceOrder = fixture.Source == null
                ? Array.Empty<TabletopObjectId>()
                : fixture.Source.ObjectIds.ToArray();
            TabletopObjectId[] originalDestinationOrder = fixture.Destination == null
                ? Array.Empty<TabletopObjectId>()
                : fixture.Destination.ObjectIds.ToArray();

            TransferCardResult result = ExecuteTransfer(fixture);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(1));
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            if (fixture.Source != null)
            {
                Assert.That(
                    fixture.Source.ObjectIds,
                    Is.EqualTo(originalSourceOrder.Where(id => id != fixture.TargetCard.BaseState.Id).ToArray()));
            }

            if (fixture.Destination == null)
            {
                Assert.That(fixture.TargetCard.BaseState.ContainerId, Is.EqualTo(ContainerId.Empty));
                Assert.That(fixture.TargetCard.BaseState.Pose, Is.EqualTo(fixture.TargetPose));
            }
            else
            {
                Assert.That(
                    fixture.Destination.ObjectIds,
                    Is.EqualTo(originalDestinationOrder.Concat(new[] { fixture.TargetCard.BaseState.Id }).ToArray()));
                Assert.That(fixture.TargetCard.BaseState.ContainerId, Is.EqualTo(fixture.Destination.Id));
                Assert.That(fixture.TargetCard.BaseState.Pose, Is.EqualTo(originalPose));
            }

            Assert.That(fixture.Match.Containers[fixture.Hand.Id], Is.SameAs(fixture.Hand));
            Assert.That(fixture.Match.Seats[fixture.Seat.Id], Is.SameAs(fixture.Seat));
            AssertHandMetadata(fixture.Hand, fixture.Seat.Id, ObjectVisibility.OwnerOnly, 5);
            before.AssertUnchangedExceptTransfer(
                fixture.Match,
                fixture.TargetCard.BaseState.Id,
                fixture.Source == null ? ContainerId.Empty : fixture.Source.Id,
                fixture.Destination == null ? ContainerId.Empty : fixture.Destination.Id,
                fixture.Destination == null ? fixture.TargetPose : originalPose);
        }

        [Test]
        public void TransferCard_WhenTabletopToFullHandFails_PreservesHandAndCardState()
        {
            TransferFixture fixture = CreateTransferFixture(
                HandTransferPath.TabletopToHand,
                handCapacity: 1,
                destinationExistingCount: 1);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);

            TransferCardResult result = ExecuteTransfer(fixture);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Rejected));
            Assert.That(result.Error, Is.EqualTo(TransferCardError.DestinationCapacityExceeded));
            before.AssertMatches(fixture.Match);
        }

        [TestCase(0, 3, "B,C,D,A")]
        [TestCase(3, 0, "D,A,B,C")]
        [TestCase(1, 3, "A,C,D,B")]
        [TestCase(2, 1, "A,C,B,D")]
        public void ReorderContainer_WhenHandIsReordered_UsesApprovedIndexSemantics(
            int fromIndex,
            int toIndex,
            string expectedLabels)
        {
            OperationFixture fixture = CreateOperationFixture(handCount: 4, handCapacity: 5, revision: 10);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);
            ReorderContainerCommand command = CreateReorderCommand(
                fixture,
                fixture.HandCards[fromIndex].BaseState.Id,
                fromIndex,
                toIndex);

            ReorderContainerResult result = new ReorderContainerUseCase().Execute(fixture.Match, command);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(11));
            Assert.That(fixture.Hand.ObjectIds, Is.EqualTo(IdsForLabels(fixture, expectedLabels)));
            AssertHandMetadata(fixture.Hand, fixture.Seat.Id, ObjectVisibility.OwnerOnly, 5);
            before.AssertUnchangedExceptReorder(fixture.Match, fixture.Hand.Id);
        }

        [Test]
        public void ReorderContainer_WhenSameHandIndex_IsAcceptedAndAdvancesRevision()
        {
            OperationFixture fixture = CreateOperationFixture(handCount: 3, handCapacity: 5, revision: 2);
            TabletopObjectId[] originalOrder = fixture.Hand.ObjectIds.ToArray();

            ReorderContainerResult result = new ReorderContainerUseCase().Execute(
                fixture.Match,
                CreateReorderCommand(fixture, fixture.HandCards[1].BaseState.Id, 1, 1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(3));
            Assert.That(fixture.Hand.ObjectIds, Is.EqualTo(originalOrder));
            AssertHandMetadata(fixture.Hand, fixture.Seat.Id, ObjectVisibility.OwnerOnly, 5);
        }

        [Test]
        public void ReorderContainer_WhenHandObjectIdDoesNotMatchFromIndex_RejectsStaleIndex()
        {
            OperationFixture fixture = CreateOperationFixture(handCount: 3, handCapacity: 5, revision: 0);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);

            ReorderContainerResult result = new ReorderContainerUseCase().Execute(
                fixture.Match,
                CreateReorderCommand(fixture, fixture.HandCards[2].BaseState.Id, fromIndex: 0, toIndex: 1));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(ReorderContainerError.ObjectIndexMismatch));
            before.AssertMatches(fixture.Match);
        }

        [Test]
        public void ReorderContainer_WhenHandCardIsUserLocked_RejectsAndPreservesState()
        {
            OperationFixture fixture = CreateOperationFixture(handCount: 3, handCapacity: 5, lockedHandIndex: 0);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);

            ReorderContainerResult result = new ReorderContainerUseCase().Execute(
                fixture.Match,
                CreateReorderCommand(fixture, fixture.HandCards[0].BaseState.Id, fromIndex: 0, toIndex: 2));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(ReorderContainerError.ObjectUserLocked));
            before.AssertMatches(fixture.Match);
        }

        private static DrawCardsResult ExecuteDraw(OperationFixture fixture, int count)
        {
            DrawCardsCommand command = new DrawCardsCommand(
                CreateContext(fixture.Match.Id, fixture.Match.Revision),
                fixture.Deck.Id,
                fixture.Hand.Id,
                count);

            return new DrawCardsUseCase().Execute(fixture.Match, command);
        }

        private static TransferCardResult ExecuteTransfer(TransferFixture fixture)
        {
            TransferCardCommand command = fixture.Destination == null
                ? TransferCardCommand.ToTabletop(
                    CreateContext(fixture.Match.Id, fixture.Match.Revision),
                    fixture.TargetCard.BaseState.Id,
                    fixture.Source == null ? ContainerId.Empty : fixture.Source.Id,
                    fixture.TargetPose)
                : TransferCardCommand.ToContainer(
                    CreateContext(fixture.Match.Id, fixture.Match.Revision),
                    fixture.TargetCard.BaseState.Id,
                    fixture.Source == null ? ContainerId.Empty : fixture.Source.Id,
                    fixture.Destination.Id);

            return new TransferCardUseCase().Execute(fixture.Match, command);
        }

        private static ReorderContainerCommand CreateReorderCommand(
            OperationFixture fixture,
            TabletopObjectId objectId,
            int fromIndex,
            int toIndex)
        {
            return new ReorderContainerCommand(
                CreateContext(fixture.Match.Id, fixture.Match.Revision),
                fixture.Hand.Id,
                objectId,
                fromIndex,
                toIndex);
        }

        private static OperationFixture CreateOperationFixture(
            int deckCount = 4,
            int handCount = 0,
            int handCapacity = 5,
            int lockedHandIndex = -1,
            long revision = 0)
        {
            SeatId seatId = SeatId.New();
            SeatId otherSeatId = SeatId.New();
            ContainerState deck = CreateContainer(ContainerKind.Deck);
            ContainerState hand = CreateContainer(ContainerKind.Hand, seatId, ObjectVisibility.OwnerOnly, handCapacity);
            ContainerState stack = CreateContainer(ContainerKind.Stack);
            ContainerState discard = CreateContainer(ContainerKind.DiscardPile);
            ContainerState slot = CreateContainer(ContainerKind.ConsoleSlot, seatId, ObjectVisibility.Public, capacity: 1);
            ContainerState otherHand = CreateContainer(ContainerKind.Hand, otherSeatId, ObjectVisibility.OwnerOnly, capacity: 5);
            ContainerState otherSlot = CreateContainer(ContainerKind.ConsoleSlot, otherSeatId, ObjectVisibility.Public, capacity: 1);
            ContainerTransferService transferService = new ContainerTransferService();
            List<CardInstanceState> cards = new List<CardInstanceState>();
            List<CardInstanceState> deckCards = new List<CardInstanceState>();
            List<CardInstanceState> handCards = new List<CardInstanceState>();

            for (int index = 0; index < deckCount; index++)
            {
                CardInstanceState card = CreateCard(face: index % 2 == 0 ? CardFace.FaceDown : CardFace.FaceUp);
                transferService.PlaceIntoContainer(card.BaseState, deck);
                deckCards.Add(card);
                cards.Add(card);
            }

            for (int index = 0; index < handCount; index++)
            {
                CardInstanceState card = CreateCard(
                    face: CardFace.FaceUp,
                    isUserLocked: index == lockedHandIndex);
                transferService.PlaceIntoContainer(card.BaseState, hand);
                handCards.Add(card);
                cards.Add(card);
            }

            SeatState seat = CreateSeat(seatId, hand.Id, slot.Id);
            SeatState otherSeat = CreateSeat(otherSeatId, otherHand.Id, otherSlot.Id);
            ContainerPlacementState deckPlacement = new ContainerPlacementState(deck.Id, CreatePose(x: 3.0, y: -2.0));
            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                cards,
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                new[] { deck, hand, stack, discard, slot, otherHand, otherSlot },
                new[] { seat, otherSeat },
                new[] { deckPlacement });

            return new OperationFixture(
                match,
                deck,
                hand,
                stack,
                discard,
                slot,
                otherHand,
                seat,
                deckCards.ToArray(),
                handCards.ToArray());
        }

        private static TransferFixture CreateTransferFixture(
            HandTransferPath path,
            int handCapacity = 5,
            int destinationExistingCount = 1)
        {
            SeatId seatId = SeatId.New();
            SeatId otherSeatId = SeatId.New();
            ContainerState deck = CreateContainer(ContainerKind.Deck);
            ContainerState stack = CreateContainer(ContainerKind.Stack);
            ContainerState hand = CreateContainer(ContainerKind.Hand, seatId, ObjectVisibility.OwnerOnly, handCapacity);
            ContainerState discard = CreateContainer(ContainerKind.DiscardPile);
            ContainerState slot = CreateContainer(ContainerKind.ConsoleSlot, seatId, ObjectVisibility.Public, capacity: 1);
            ContainerState otherHand = CreateContainer(ContainerKind.Hand, otherSeatId, ObjectVisibility.OwnerOnly, capacity: 5);
            ContainerState otherSlot = CreateContainer(ContainerKind.ConsoleSlot, otherSeatId, ObjectVisibility.Public, capacity: 1);
            ContainerState source = ResolveSource(path, deck, stack, hand);
            ContainerState destination = ResolveDestination(path, hand, discard, slot, otherHand);
            ContainerTransferService transferService = new ContainerTransferService();
            List<CardInstanceState> cards = new List<CardInstanceState>();
            CardInstanceState targetCard = CreateCard(face: CardFace.FaceUp);

            if (source == null)
            {
                cards.Add(targetCard);
            }
            else
            {
                CardInstanceState sourceLower = CreateCard();
                CardInstanceState sourceUpper = CreateCard();
                transferService.PlaceIntoContainer(sourceLower.BaseState, source);
                transferService.PlaceIntoContainer(targetCard.BaseState, source);
                transferService.PlaceIntoContainer(sourceUpper.BaseState, source);
                cards.Add(sourceLower);
                cards.Add(targetCard);
                cards.Add(sourceUpper);
            }

            if (destination != null && destination != source && destination.Kind != ContainerKind.ConsoleSlot)
            {
                for (int index = 0; index < destinationExistingCount; index++)
                {
                    CardInstanceState destinationCard = CreateCard(face: CardFace.FaceDown);
                    transferService.PlaceIntoContainer(destinationCard.BaseState, destination);
                    cards.Add(destinationCard);
                }
            }

            SeatState seat = CreateSeat(seatId, hand.Id, slot.Id);
            SeatState otherSeat = CreateSeat(otherSeatId, otherHand.Id, otherSlot.Id);
            ContainerPlacementState deckPlacement = new ContainerPlacementState(deck.Id, CreatePose(x: -2.0, y: 2.0));
            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                0,
                cards,
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                new[] { deck, stack, hand, discard, slot, otherHand, otherSlot },
                new[] { seat, otherSeat },
                new[] { deckPlacement });

            return new TransferFixture(
                match,
                source,
                destination,
                hand,
                seat,
                targetCard,
                CreatePose(x: -8.0, y: 9.0, rotationDegrees: -725f, layer: 4, localOrder: 12));
        }

        private static ContainerState ResolveSource(
            HandTransferPath path,
            ContainerState deck,
            ContainerState stack,
            ContainerState hand)
        {
            switch (path)
            {
                case HandTransferPath.TabletopToHand:
                    return null;
                case HandTransferPath.DeckToHand:
                    return deck;
                case HandTransferPath.StackToHand:
                    return stack;
                case HandTransferPath.HandToTabletop:
                case HandTransferPath.HandToDiscardPile:
                case HandTransferPath.HandToConsoleSlot:
                case HandTransferPath.HandToAnotherHand:
                    return hand;
                default:
                    throw new ArgumentOutOfRangeException(nameof(path), path, "Unsupported Hand transfer path.");
            }
        }

        private static ContainerState ResolveDestination(
            HandTransferPath path,
            ContainerState hand,
            ContainerState discard,
            ContainerState slot,
            ContainerState otherHand)
        {
            switch (path)
            {
                case HandTransferPath.TabletopToHand:
                case HandTransferPath.DeckToHand:
                case HandTransferPath.StackToHand:
                    return hand;
                case HandTransferPath.HandToTabletop:
                    return null;
                case HandTransferPath.HandToDiscardPile:
                    return discard;
                case HandTransferPath.HandToConsoleSlot:
                    return slot;
                case HandTransferPath.HandToAnotherHand:
                    return otherHand;
                default:
                    throw new ArgumentOutOfRangeException(nameof(path), path, "Unsupported Hand transfer path.");
            }
        }

        private static CommandContext CreateContext(MatchId matchId, long? expectedRevision = 0)
        {
            return new CommandContext(CommandId.New(), matchId, PlayerId.New(), expectedRevision);
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

        private static SeatState CreateSeat(SeatId seatId, ContainerId handContainerId, ContainerId slotContainerId)
        {
            return new SeatState(
                seatId,
                CreatePose(x: -4.0, y: 4.0),
                handContainerId,
                new ConsoleState(seatId, new[] { slotContainerId }),
                PlayerId.Empty,
                SeatStatus.Vacant);
        }

        private static CardInstanceState CreateCard(
            CardFace face = CardFace.FaceDown,
            bool isUserLocked = false)
        {
            return new CardInstanceState(
                new TabletopObjectState(
                    TabletopObjectId.New(),
                    ObjectDefinitionId.New(),
                    TabletopObjectKind.Card,
                    CreatePose(x: 1.0, y: 2.0, rotationDegrees: 30f, layer: 2, localOrder: 3),
                    ContainerId.Empty,
                    PlayerId.New(),
                    ObjectVisibility.SeatOnly,
                    isUserLocked),
                face);
        }

        private static TabletopObjectId[] IdsForLabels(OperationFixture fixture, string labels)
        {
            return labels
                .Split(',')
                .Select(label => fixture.HandCards[label[0] - 'A'].BaseState.Id)
                .ToArray();
        }

        private static void AssertHandMetadata(
            ContainerState hand,
            SeatId seatId,
            ObjectVisibility visibility,
            int capacity)
        {
            Assert.That(hand.Kind, Is.EqualTo(ContainerKind.Hand));
            Assert.That(hand.OwnerSeatId, Is.EqualTo(seatId));
            Assert.That(hand.Visibility, Is.EqualTo(visibility));
            Assert.That(hand.Capacity, Is.EqualTo(capacity));
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

        private sealed class OperationFixture
        {
            public OperationFixture(
                MatchState match,
                ContainerState deck,
                ContainerState hand,
                ContainerState stack,
                ContainerState discard,
                ContainerState slot,
                ContainerState otherHand,
                SeatState seat,
                CardInstanceState[] deckCards,
                CardInstanceState[] handCards)
            {
                Match = match;
                Deck = deck;
                Hand = hand;
                Stack = stack;
                Discard = discard;
                Slot = slot;
                OtherHand = otherHand;
                Seat = seat;
                DeckCards = deckCards;
                HandCards = handCards;
            }

            public MatchState Match { get; }

            public ContainerState Deck { get; }

            public ContainerState Hand { get; }

            public ContainerState Stack { get; }

            public ContainerState Discard { get; }

            public ContainerState Slot { get; }

            public ContainerState OtherHand { get; }

            public SeatState Seat { get; }

            public CardInstanceState[] DeckCards { get; }

            public CardInstanceState[] HandCards { get; }
        }

        private sealed class TransferFixture
        {
            public TransferFixture(
                MatchState match,
                ContainerState source,
                ContainerState destination,
                ContainerState hand,
                SeatState seat,
                CardInstanceState targetCard,
                TabletopPose targetPose)
            {
                Match = match;
                Source = source;
                Destination = destination;
                Hand = hand;
                Seat = seat;
                TargetCard = targetCard;
                TargetPose = targetPose;
            }

            public MatchState Match { get; }

            public ContainerState Source { get; }

            public ContainerState Destination { get; }

            public ContainerState Hand { get; }

            public SeatState Seat { get; }

            public CardInstanceState TargetCard { get; }

            public TabletopPose TargetPose { get; }
        }

        private sealed class AggregateSnapshot
        {
            private readonly long revision;
            private readonly IReadOnlyDictionary<ContainerId, TabletopObjectId[]> containerOrders;
            private readonly IReadOnlyDictionary<ContainerId, ContainerSnapshot> containerSnapshots;
            private readonly IReadOnlyDictionary<TabletopObjectId, ObjectSnapshot> objectSnapshots;
            private readonly IReadOnlyDictionary<TabletopObjectId, CardFace> cardFaces;
            private readonly IReadOnlyDictionary<ContainerId, TabletopPose> placementPoses;
            private readonly IReadOnlyDictionary<ContainerId, ContainerPlacementState> placements;
            private readonly IReadOnlyDictionary<SeatId, SeatState> seats;

            private AggregateSnapshot(
                long revision,
                IReadOnlyDictionary<ContainerId, TabletopObjectId[]> containerOrders,
                IReadOnlyDictionary<ContainerId, ContainerSnapshot> containerSnapshots,
                IReadOnlyDictionary<TabletopObjectId, ObjectSnapshot> objectSnapshots,
                IReadOnlyDictionary<TabletopObjectId, CardFace> cardFaces,
                IReadOnlyDictionary<ContainerId, TabletopPose> placementPoses,
                IReadOnlyDictionary<ContainerId, ContainerPlacementState> placements,
                IReadOnlyDictionary<SeatId, SeatState> seats)
            {
                this.revision = revision;
                this.containerOrders = containerOrders;
                this.containerSnapshots = containerSnapshots;
                this.objectSnapshots = objectSnapshots;
                this.cardFaces = cardFaces;
                this.placementPoses = placementPoses;
                this.placements = placements;
                this.seats = seats;
            }

            public static AggregateSnapshot Capture(MatchState match)
            {
                return new AggregateSnapshot(
                    match.Revision,
                    match.Containers.ToDictionary(pair => pair.Key, pair => pair.Value.ObjectIds.ToArray()),
                    match.Containers.ToDictionary(pair => pair.Key, pair => ContainerSnapshot.Capture(pair.Value)),
                    match.Cards.ToDictionary(pair => pair.Key, pair => ObjectSnapshot.Capture(pair.Value.BaseState)),
                    match.Cards.ToDictionary(pair => pair.Key, pair => pair.Value.Face),
                    match.ContainerPlacements.ToDictionary(pair => pair.Key, pair => pair.Value.Pose),
                    match.ContainerPlacements.ToDictionary(pair => pair.Key, pair => pair.Value),
                    match.Seats.ToDictionary(pair => pair.Key, pair => pair.Value));
            }

            public void AssertMatches(MatchState match)
            {
                Assert.That(match.Revision, Is.EqualTo(revision));
                AssertContainerOrders(match);
                AssertContainerMetadata(match);
                AssertObjectState(match);
                AssertPlacementsAndSeats(match);
            }

            public void AssertUnchangedExceptDraw(
                MatchState match,
                ContainerId sourceId,
                ContainerId destinationId,
                IReadOnlyCollection<TabletopObjectId> drawnObjectIds,
                ContainerId drawnDestinationId)
            {
                Assert.That(match.Revision, Is.EqualTo(revision + 1));
                AssertContainerOrders(match, sourceId, destinationId);
                AssertContainerMetadata(match);
                AssertObjectState(match, drawnObjectIds, drawnDestinationId);
                AssertPlacementsAndSeats(match);
            }

            public void AssertUnchangedExceptTransfer(
                MatchState match,
                TabletopObjectId transferredObjectId,
                ContainerId sourceId,
                ContainerId destinationId,
                TabletopPose expectedTransferredPose)
            {
                Assert.That(match.Revision, Is.EqualTo(revision + 1));
                AssertContainerOrders(match, sourceId, destinationId);
                AssertContainerMetadata(match);
                AssertObjectState(
                    match,
                    new[] { transferredObjectId },
                    destinationId,
                    expectedTransferredPose);
                AssertPlacementsAndSeats(match);
            }

            public void AssertUnchangedExceptReorder(MatchState match, ContainerId reorderedContainerId)
            {
                Assert.That(match.Revision, Is.EqualTo(revision + 1));
                AssertContainerOrders(match, reorderedContainerId);
                AssertContainerMetadata(match);
                AssertObjectState(match);
                AssertPlacementsAndSeats(match);
            }

            private void AssertContainerOrders(MatchState match, params ContainerId[] changedContainerIds)
            {
                HashSet<ContainerId> changed = new HashSet<ContainerId>(changedContainerIds);

                foreach (KeyValuePair<ContainerId, TabletopObjectId[]> pair in containerOrders)
                {
                    if (!changed.Contains(pair.Key))
                    {
                        Assert.That(match.Containers[pair.Key].ObjectIds, Is.EqualTo(pair.Value));
                    }
                }
            }

            private void AssertContainerMetadata(MatchState match)
            {
                foreach (KeyValuePair<ContainerId, ContainerSnapshot> pair in containerSnapshots)
                {
                    pair.Value.AssertMatches(match.Containers[pair.Key]);
                }
            }

            private void AssertObjectState(
                MatchState match,
                IReadOnlyCollection<TabletopObjectId> changedContainerObjectIds = null,
                ContainerId changedContainerId = default(ContainerId),
                TabletopPose? changedPose = null)
            {
                HashSet<TabletopObjectId> changedIds = changedContainerObjectIds == null
                    ? new HashSet<TabletopObjectId>()
                    : new HashSet<TabletopObjectId>(changedContainerObjectIds);

                foreach (KeyValuePair<TabletopObjectId, ObjectSnapshot> pair in objectSnapshots)
                {
                    TabletopObjectState objectState = match.GetObject(pair.Key);
                    if (changedIds.Contains(pair.Key))
                    {
                        pair.Value.AssertMatchesExceptLocation(objectState, changedContainerId, changedPose ?? pair.Value.Pose);
                    }
                    else
                    {
                        pair.Value.AssertMatches(objectState);
                    }
                }

                foreach (KeyValuePair<TabletopObjectId, CardFace> pair in cardFaces)
                {
                    Assert.That(match.Cards[pair.Key].Face, Is.EqualTo(pair.Value));
                }
            }

            private void AssertPlacementsAndSeats(MatchState match)
            {
                foreach (KeyValuePair<ContainerId, TabletopPose> pair in placementPoses)
                {
                    Assert.That(match.ContainerPlacements[pair.Key].Pose, Is.EqualTo(pair.Value));
                    Assert.That(match.ContainerPlacements[pair.Key], Is.SameAs(placements[pair.Key]));
                }

                foreach (KeyValuePair<SeatId, SeatState> pair in seats)
                {
                    Assert.That(match.Seats[pair.Key], Is.SameAs(pair.Value));
                }
            }
        }

        private sealed class ContainerSnapshot
        {
            private ContainerSnapshot(
                ContainerId id,
                ContainerKind kind,
                SeatId ownerSeatId,
                ObjectVisibility visibility,
                int capacity)
            {
                Id = id;
                Kind = kind;
                OwnerSeatId = ownerSeatId;
                Visibility = visibility;
                Capacity = capacity;
            }

            private ContainerId Id { get; }

            private ContainerKind Kind { get; }

            private SeatId OwnerSeatId { get; }

            private ObjectVisibility Visibility { get; }

            private int Capacity { get; }

            public static ContainerSnapshot Capture(ContainerState container)
            {
                return new ContainerSnapshot(
                    container.Id,
                    container.Kind,
                    container.OwnerSeatId,
                    container.Visibility,
                    container.Capacity);
            }

            public void AssertMatches(ContainerState container)
            {
                Assert.That(container.Id, Is.EqualTo(Id));
                Assert.That(container.Kind, Is.EqualTo(Kind));
                Assert.That(container.OwnerSeatId, Is.EqualTo(OwnerSeatId));
                Assert.That(container.Visibility, Is.EqualTo(Visibility));
                Assert.That(container.Capacity, Is.EqualTo(Capacity));
            }
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

            public TabletopPose Pose { get; }

            private TabletopObjectId Id { get; }

            private ObjectDefinitionId DefinitionId { get; }

            private TabletopObjectKind Kind { get; }

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
                AssertMatchesExceptLocation(state, ContainerId, Pose);
            }

            public void AssertMatchesExceptLocation(
                TabletopObjectState state,
                ContainerId expectedContainerId,
                TabletopPose expectedPose)
            {
                Assert.That(state.Id, Is.EqualTo(Id));
                Assert.That(state.DefinitionId, Is.EqualTo(DefinitionId));
                Assert.That(state.Kind, Is.EqualTo(Kind));
                Assert.That(state.Pose, Is.EqualTo(expectedPose));
                Assert.That(state.ContainerId, Is.EqualTo(expectedContainerId));
                Assert.That(state.OwnerPlayerId, Is.EqualTo(OwnerPlayerId));
                Assert.That(state.Visibility, Is.EqualTo(Visibility));
                Assert.That(state.IsUserLocked, Is.EqualTo(IsUserLocked));
            }
        }
    }
}
