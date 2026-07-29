using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    public sealed class ConsoleSlotOperationIntegrationTests
    {
        public enum SlotTransferPath
        {
            TabletopToConsoleSlot,
            DeckToConsoleSlot,
            StackToConsoleSlot,
            HandToConsoleSlot,
            DiscardPileToConsoleSlot,
            ConsoleSlotToTabletop,
            ConsoleSlotToHand,
            ConsoleSlotToStack,
            ConsoleSlotToDiscardPile,
            ConsoleSlotToAnotherConsoleSlot
        }

        [TestCase(SlotTransferPath.TabletopToConsoleSlot)]
        [TestCase(SlotTransferPath.DeckToConsoleSlot)]
        [TestCase(SlotTransferPath.StackToConsoleSlot)]
        [TestCase(SlotTransferPath.HandToConsoleSlot)]
        [TestCase(SlotTransferPath.DiscardPileToConsoleSlot)]
        [TestCase(SlotTransferPath.ConsoleSlotToTabletop)]
        [TestCase(SlotTransferPath.ConsoleSlotToHand)]
        [TestCase(SlotTransferPath.ConsoleSlotToStack)]
        [TestCase(SlotTransferPath.ConsoleSlotToDiscardPile)]
        [TestCase(SlotTransferPath.ConsoleSlotToAnotherConsoleSlot)]
        public void TransferCard_WhenPathInvolvesConsoleSlot_UsesStructuralRulesAndPreservesSlotBoundary(
            SlotTransferPath path)
        {
            TransferFixture fixture = CreateTransferFixture(path, slotCapacity: 4, destinationExistingCount: 1);
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

            Assert.That(fixture.Match.TryGetSeatConsole(fixture.Seat.Id, out ConsoleState console), Is.True);
            Assert.That(console, Is.SameAs(fixture.Console));
            Assert.That(fixture.Match.TryGetConsoleSlot(fixture.Seat.Id, 0, out ContainerState firstSlot), Is.True);
            Assert.That(firstSlot, Is.SameAs(fixture.PrimarySlot));
            Assert.That(fixture.Match.TryGetConsoleSlot(fixture.Seat.Id, 1, out ContainerState secondSlot), Is.True);
            Assert.That(secondSlot, Is.SameAs(fixture.OtherSlot));
            AssertSlotMetadata(fixture.PrimarySlot, fixture.Seat.Id, ObjectVisibility.SeatOnly, 4);
            before.AssertUnchangedExceptTransfer(
                fixture.Match,
                fixture.TargetCard.BaseState.Id,
                fixture.Source?.Id ?? ContainerId.Empty,
                fixture.Destination?.Id ?? ContainerId.Empty,
                fixture.Destination == null ? fixture.TargetPose : originalPose);
        }

        [Test]
        public void TransferCard_WhenConsoleSlotIsFull_FailsAtomically()
        {
            TransferFixture fixture = CreateTransferFixture(
                SlotTransferPath.TabletopToConsoleSlot,
                slotCapacity: 1,
                destinationExistingCount: 1);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);

            TransferCardResult result = ExecuteTransfer(fixture);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Rejected));
            Assert.That(result.Error, Is.EqualTo(TransferCardError.DestinationCapacityExceeded));
            before.AssertMatches(fixture.Match);
        }

        [Test]
        public void DrawCards_WhenDrawingOneToEmptyCapacityOneSlot_Succeeds()
        {
            DrawFixture fixture = CreateDrawFixture(deckCount: 1, slotCount: 0, slotCapacity: 1, revision: 2);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);

            DrawCardsResult result = ExecuteDraw(fixture, count: 1);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(3));
            Assert.That(fixture.Deck.ObjectIds, Is.Empty);
            Assert.That(fixture.PrimarySlot.ObjectIds, Is.EqualTo(new[] { fixture.DeckCards[0].BaseState.Id }));
            AssertSlotMetadata(fixture.PrimarySlot, fixture.Seat.Id, ObjectVisibility.SeatOnly, 1);
            before.AssertUnchangedExceptDraw(
                fixture.Match,
                fixture.Deck.Id,
                fixture.PrimarySlot.Id,
                new[] { fixture.DeckCards[0].BaseState.Id },
                fixture.PrimarySlot.Id);
        }

        [Test]
        public void DrawCards_WhenDrawingOneToFullCapacityOneSlot_FailsAtomically()
        {
            DrawFixture fixture = CreateDrawFixture(deckCount: 1, slotCount: 1, slotCapacity: 1);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);

            DrawCardsResult result = ExecuteDraw(fixture, count: 1);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Rejected));
            Assert.That(result.Error, Is.EqualTo(DrawCardsError.DestinationCapacityExceeded));
            before.AssertMatches(fixture.Match);
        }

        [Test]
        public void DrawCards_WhenDrawingMultipleToSlotWithCapacity_AppendsInDrawSequence()
        {
            DrawFixture fixture = CreateDrawFixture(deckCount: 4, slotCount: 1, slotCapacity: 3, revision: 5);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);

            DrawCardsResult result = ExecuteDraw(fixture, count: 2);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(6));
            Assert.That(fixture.Deck.ObjectIds, Is.EqualTo(new[]
            {
                fixture.DeckCards[0].BaseState.Id,
                fixture.DeckCards[1].BaseState.Id
            }));
            Assert.That(fixture.PrimarySlot.ObjectIds, Is.EqualTo(new[]
            {
                fixture.SlotCards[0].BaseState.Id,
                fixture.DeckCards[3].BaseState.Id,
                fixture.DeckCards[2].BaseState.Id
            }));
            AssertSlotMetadata(fixture.PrimarySlot, fixture.Seat.Id, ObjectVisibility.SeatOnly, 3);
            before.AssertUnchangedExceptDraw(
                fixture.Match,
                fixture.Deck.Id,
                fixture.PrimarySlot.Id,
                new[] { fixture.DeckCards[3].BaseState.Id, fixture.DeckCards[2].BaseState.Id },
                fixture.PrimarySlot.Id);
        }

        [Test]
        public void DrawCards_WhenFullCountDoesNotFitSlot_FailsAtomically()
        {
            DrawFixture fixture = CreateDrawFixture(deckCount: 4, slotCount: 1, slotCapacity: 2);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);

            DrawCardsResult result = ExecuteDraw(fixture, count: 2);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(DrawCardsError.DestinationCapacityExceeded));
            before.AssertMatches(fixture.Match);
        }

        [TestCase(0, 3, "B,C,D,A")]
        [TestCase(3, 0, "D,A,B,C")]
        [TestCase(1, 1, "A,B,C,D")]
        public void ReorderContainer_WhenConsoleSlotAllowsMultipleMembers_UsesApprovedIndexSemantics(
            int fromIndex,
            int toIndex,
            string expectedLabels)
        {
            ReorderFixture fixture = CreateReorderFixture(slotCount: 4, slotCapacity: 5, revision: 7);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);

            ReorderContainerResult result = new ReorderContainerUseCase().Execute(
                fixture.Match,
                CreateReorderCommand(fixture, fixture.SlotCards[fromIndex].BaseState.Id, fromIndex, toIndex));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(8));
            Assert.That(fixture.PrimarySlot.ObjectIds, Is.EqualTo(IdsForLabels(fixture, expectedLabels)));
            AssertSlotMetadata(fixture.PrimarySlot, fixture.Seat.Id, ObjectVisibility.SeatOnly, 5);
            before.AssertUnchangedExceptReorder(fixture.Match, fixture.PrimarySlot.Id);
        }

        [Test]
        public void ReorderContainer_WhenConsoleSlotIndexIsStale_FailsAtomically()
        {
            ReorderFixture fixture = CreateReorderFixture(slotCount: 3, slotCapacity: 5);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);

            ReorderContainerResult result = new ReorderContainerUseCase().Execute(
                fixture.Match,
                CreateReorderCommand(fixture, fixture.SlotCards[2].BaseState.Id, fromIndex: 0, toIndex: 1));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(ReorderContainerError.ObjectIndexMismatch));
            before.AssertMatches(fixture.Match);
        }

        [Test]
        public void ReorderContainer_WhenConsoleSlotCardIsUserLocked_FailsAtomically()
        {
            ReorderFixture fixture = CreateReorderFixture(slotCount: 3, slotCapacity: 5, lockedSlotIndex: 0);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture.Match);

            ReorderContainerResult result = new ReorderContainerUseCase().Execute(
                fixture.Match,
                CreateReorderCommand(fixture, fixture.SlotCards[0].BaseState.Id, fromIndex: 0, toIndex: 2));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(ReorderContainerError.ObjectUserLocked));
            before.AssertMatches(fixture.Match);
        }

        [Test]
        public void StaticBoundary_UsesApplicationCoreOnlyAndAddsNoSlotSpecificCommand()
        {
            Assembly applicationAssembly = typeof(TransferCardUseCase).Assembly;
            string[] referencedAssemblyNames = applicationAssembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.That(referencedAssemblyNames, Does.Contain("ConsoleCards.Core"));
            Assert.That(referencedAssemblyNames.Any(name => name.StartsWith("UnityEngine", StringComparison.Ordinal)), Is.False);
            Assert.That(referencedAssemblyNames, Does.Not.Contain("ConsoleCards.Presentation"));
            Assert.That(applicationAssembly.GetType("ConsoleCards.Application.Commands.DrawToSlotCommand"), Is.Null);
            Assert.That(applicationAssembly.GetType("ConsoleCards.Application.Commands.TransferToConsoleSlotCommand"), Is.Null);
        }

        private static TransferCardResult ExecuteTransfer(TransferFixture fixture)
        {
            TransferCardCommand command = fixture.Destination == null
                ? TransferCardCommand.ToTabletop(
                    CreateContext(fixture.Match.Id, fixture.Match.Revision),
                    fixture.TargetCard.BaseState.Id,
                    fixture.Source?.Id ?? ContainerId.Empty,
                    fixture.TargetPose)
                : TransferCardCommand.ToContainer(
                    CreateContext(fixture.Match.Id, fixture.Match.Revision),
                    fixture.TargetCard.BaseState.Id,
                    fixture.Source?.Id ?? ContainerId.Empty,
                    fixture.Destination.Id);

            return new TransferCardUseCase().Execute(fixture.Match, command);
        }

        private static DrawCardsResult ExecuteDraw(DrawFixture fixture, int count)
        {
            DrawCardsCommand command = new DrawCardsCommand(
                CreateContext(fixture.Match.Id, fixture.Match.Revision),
                fixture.Deck.Id,
                fixture.PrimarySlot.Id,
                count);

            return new DrawCardsUseCase().Execute(fixture.Match, command);
        }

        private static ReorderContainerCommand CreateReorderCommand(
            ReorderFixture fixture,
            TabletopObjectId objectId,
            int fromIndex,
            int toIndex)
        {
            return new ReorderContainerCommand(
                CreateContext(fixture.Match.Id, fixture.Match.Revision),
                fixture.PrimarySlot.Id,
                objectId,
                fromIndex,
                toIndex);
        }

        private static TransferFixture CreateTransferFixture(
            SlotTransferPath path,
            int slotCapacity,
            int destinationExistingCount)
        {
            SeatId seatId = SeatId.New();
            ContainerState deck = CreateContainer(ContainerKind.Deck);
            ContainerState stack = CreateContainer(ContainerKind.Stack);
            ContainerState hand = CreateContainer(ContainerKind.Hand, seatId, ObjectVisibility.OwnerOnly, capacity: 5);
            ContainerState discard = CreateContainer(ContainerKind.DiscardPile);
            ContainerState primarySlot = CreateContainer(ContainerKind.ConsoleSlot, seatId, ObjectVisibility.SeatOnly, slotCapacity);
            ContainerState otherSlot = CreateContainer(ContainerKind.ConsoleSlot, seatId, ObjectVisibility.SeatOnly, slotCapacity);
            ContainerState source = ResolveSource(path, deck, stack, hand, discard, primarySlot);
            ContainerState destination = ResolveDestination(path, hand, stack, discard, primarySlot, otherSlot);
            ContainerTransferService transferService = new ContainerTransferService();
            List<CardInstanceState> cards = new List<CardInstanceState>();
            CardInstanceState targetCard = CreateCard(face: CardFace.FaceUp);

            if (source == null)
            {
                cards.Add(targetCard);
            }
            else
            {
                CardInstanceState lower = CreateCard();
                CardInstanceState upper = CreateCard();
                transferService.PlaceIntoContainer(lower.BaseState, source);
                transferService.PlaceIntoContainer(targetCard.BaseState, source);
                transferService.PlaceIntoContainer(upper.BaseState, source);
                cards.Add(lower);
                cards.Add(targetCard);
                cards.Add(upper);
            }

            if (destination != null && destination != source)
            {
                for (int index = 0; index < destinationExistingCount; index++)
                {
                    CardInstanceState destinationCard = CreateCard(face: CardFace.FaceDown);
                    transferService.PlaceIntoContainer(destinationCard.BaseState, destination);
                    cards.Add(destinationCard);
                }
            }

            SeatState seat = CreateSeat(seatId, hand.Id, new[] { primarySlot.Id, otherSlot.Id });
            ContainerPlacementState deckPlacement = new ContainerPlacementState(deck.Id, CreatePose(x: 2.0, y: -2.0));
            MatchState match = CreateMatch(
                cards,
                new[] { deck, stack, hand, discard, primarySlot, otherSlot },
                new[] { seat },
                new[] { deckPlacement });

            return new TransferFixture(
                match,
                source,
                destination,
                primarySlot,
                otherSlot,
                seat,
                seat.Console,
                targetCard,
                CreatePose(x: -9.0, y: 8.0, rotationDegrees: -725f, layer: 4, localOrder: 12));
        }

        private static DrawFixture CreateDrawFixture(
            int deckCount,
            int slotCount,
            int slotCapacity,
            long revision = 0)
        {
            SeatId seatId = SeatId.New();
            ContainerState deck = CreateContainer(ContainerKind.Deck);
            ContainerState hand = CreateContainer(ContainerKind.Hand, seatId, ObjectVisibility.OwnerOnly, capacity: 5);
            ContainerState primarySlot = CreateContainer(ContainerKind.ConsoleSlot, seatId, ObjectVisibility.SeatOnly, slotCapacity);
            ContainerState otherSlot = CreateContainer(ContainerKind.ConsoleSlot, seatId, ObjectVisibility.SeatOnly, capacity: 1);
            ContainerTransferService transferService = new ContainerTransferService();
            List<CardInstanceState> cards = new List<CardInstanceState>();
            List<CardInstanceState> deckCards = new List<CardInstanceState>();
            List<CardInstanceState> slotCards = new List<CardInstanceState>();

            for (int index = 0; index < deckCount; index++)
            {
                CardInstanceState card = CreateCard(face: index % 2 == 0 ? CardFace.FaceDown : CardFace.FaceUp);
                transferService.PlaceIntoContainer(card.BaseState, deck);
                deckCards.Add(card);
                cards.Add(card);
            }

            for (int index = 0; index < slotCount; index++)
            {
                CardInstanceState card = CreateCard(face: CardFace.FaceUp);
                transferService.PlaceIntoContainer(card.BaseState, primarySlot);
                slotCards.Add(card);
                cards.Add(card);
            }

            SeatState seat = CreateSeat(seatId, hand.Id, new[] { primarySlot.Id, otherSlot.Id });
            ContainerPlacementState deckPlacement = new ContainerPlacementState(deck.Id, CreatePose(x: 3.0, y: -1.0));
            MatchState match = CreateMatch(
                cards,
                new[] { deck, hand, primarySlot, otherSlot },
                new[] { seat },
                new[] { deckPlacement },
                revision);

            return new DrawFixture(
                match,
                deck,
                primarySlot,
                otherSlot,
                seat,
                seat.Console,
                deckCards.ToArray(),
                slotCards.ToArray());
        }

        private static ReorderFixture CreateReorderFixture(
            int slotCount,
            int slotCapacity,
            int lockedSlotIndex = -1,
            long revision = 0)
        {
            SeatId seatId = SeatId.New();
            ContainerState hand = CreateContainer(ContainerKind.Hand, seatId, ObjectVisibility.OwnerOnly, capacity: 5);
            ContainerState primarySlot = CreateContainer(ContainerKind.ConsoleSlot, seatId, ObjectVisibility.SeatOnly, slotCapacity);
            ContainerState otherSlot = CreateContainer(ContainerKind.ConsoleSlot, seatId, ObjectVisibility.SeatOnly, capacity: 1);
            ContainerTransferService transferService = new ContainerTransferService();
            List<CardInstanceState> cards = new List<CardInstanceState>();
            List<CardInstanceState> slotCards = new List<CardInstanceState>();

            for (int index = 0; index < slotCount; index++)
            {
                CardInstanceState card = CreateCard(
                    face: CardFace.FaceUp,
                    isUserLocked: index == lockedSlotIndex);
                transferService.PlaceIntoContainer(card.BaseState, primarySlot);
                slotCards.Add(card);
                cards.Add(card);
            }

            SeatState seat = CreateSeat(seatId, hand.Id, new[] { primarySlot.Id, otherSlot.Id });
            MatchState match = CreateMatch(
                cards,
                new[] { hand, primarySlot, otherSlot },
                new[] { seat },
                revision: revision);

            return new ReorderFixture(match, primarySlot, otherSlot, seat, seat.Console, slotCards.ToArray());
        }

        private static ContainerState ResolveSource(
            SlotTransferPath path,
            ContainerState deck,
            ContainerState stack,
            ContainerState hand,
            ContainerState discard,
            ContainerState primarySlot)
        {
            switch (path)
            {
                case SlotTransferPath.TabletopToConsoleSlot:
                    return null;
                case SlotTransferPath.DeckToConsoleSlot:
                    return deck;
                case SlotTransferPath.StackToConsoleSlot:
                    return stack;
                case SlotTransferPath.HandToConsoleSlot:
                    return hand;
                case SlotTransferPath.DiscardPileToConsoleSlot:
                    return discard;
                case SlotTransferPath.ConsoleSlotToTabletop:
                case SlotTransferPath.ConsoleSlotToHand:
                case SlotTransferPath.ConsoleSlotToStack:
                case SlotTransferPath.ConsoleSlotToDiscardPile:
                case SlotTransferPath.ConsoleSlotToAnotherConsoleSlot:
                    return primarySlot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(path), path, "Unsupported Console Slot transfer path.");
            }
        }

        private static ContainerState ResolveDestination(
            SlotTransferPath path,
            ContainerState hand,
            ContainerState stack,
            ContainerState discard,
            ContainerState primarySlot,
            ContainerState otherSlot)
        {
            switch (path)
            {
                case SlotTransferPath.TabletopToConsoleSlot:
                case SlotTransferPath.DeckToConsoleSlot:
                case SlotTransferPath.StackToConsoleSlot:
                case SlotTransferPath.HandToConsoleSlot:
                case SlotTransferPath.DiscardPileToConsoleSlot:
                    return primarySlot;
                case SlotTransferPath.ConsoleSlotToTabletop:
                    return null;
                case SlotTransferPath.ConsoleSlotToHand:
                    return hand;
                case SlotTransferPath.ConsoleSlotToStack:
                    return stack;
                case SlotTransferPath.ConsoleSlotToDiscardPile:
                    return discard;
                case SlotTransferPath.ConsoleSlotToAnotherConsoleSlot:
                    return otherSlot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(path), path, "Unsupported Console Slot transfer path.");
            }
        }

        private static MatchState CreateMatch(
            IEnumerable<CardInstanceState> cards,
            IEnumerable<ContainerState> containers,
            IEnumerable<SeatState> seats,
            IEnumerable<ContainerPlacementState> placements = null,
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
                CreatePose(x: -4.0, y: 4.0),
                handContainerId,
                new ConsoleState(seatId, slotContainerIds),
                PlayerId.Empty,
                SeatStatus.Vacant);
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
                    ObjectVisibility.OwnerOnly,
                    isUserLocked),
                face);
        }

        private static TabletopObjectId[] IdsForLabels(ReorderFixture fixture, string labels)
        {
            return labels
                .Split(',')
                .Select(label => fixture.SlotCards[label[0] - 'A'].BaseState.Id)
                .ToArray();
        }

        private static void AssertSlotMetadata(
            ContainerState slot,
            SeatId seatId,
            ObjectVisibility visibility,
            int capacity)
        {
            Assert.That(slot.Kind, Is.EqualTo(ContainerKind.ConsoleSlot));
            Assert.That(slot.OwnerSeatId, Is.EqualTo(seatId));
            Assert.That(slot.Visibility, Is.EqualTo(visibility));
            Assert.That(slot.Capacity, Is.EqualTo(capacity));
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

        private sealed class TransferFixture
        {
            public TransferFixture(
                MatchState match,
                ContainerState source,
                ContainerState destination,
                ContainerState primarySlot,
                ContainerState otherSlot,
                SeatState seat,
                ConsoleState console,
                CardInstanceState targetCard,
                TabletopPose targetPose)
            {
                Match = match;
                Source = source;
                Destination = destination;
                PrimarySlot = primarySlot;
                OtherSlot = otherSlot;
                Seat = seat;
                Console = console;
                TargetCard = targetCard;
                TargetPose = targetPose;
            }

            public MatchState Match { get; }

            public ContainerState Source { get; }

            public ContainerState Destination { get; }

            public ContainerState PrimarySlot { get; }

            public ContainerState OtherSlot { get; }

            public SeatState Seat { get; }

            public ConsoleState Console { get; }

            public CardInstanceState TargetCard { get; }

            public TabletopPose TargetPose { get; }
        }

        private sealed class DrawFixture
        {
            public DrawFixture(
                MatchState match,
                ContainerState deck,
                ContainerState primarySlot,
                ContainerState otherSlot,
                SeatState seat,
                ConsoleState console,
                CardInstanceState[] deckCards,
                CardInstanceState[] slotCards)
            {
                Match = match;
                Deck = deck;
                PrimarySlot = primarySlot;
                OtherSlot = otherSlot;
                Seat = seat;
                Console = console;
                DeckCards = deckCards;
                SlotCards = slotCards;
            }

            public MatchState Match { get; }

            public ContainerState Deck { get; }

            public ContainerState PrimarySlot { get; }

            public ContainerState OtherSlot { get; }

            public SeatState Seat { get; }

            public ConsoleState Console { get; }

            public CardInstanceState[] DeckCards { get; }

            public CardInstanceState[] SlotCards { get; }
        }

        private sealed class ReorderFixture
        {
            public ReorderFixture(
                MatchState match,
                ContainerState primarySlot,
                ContainerState otherSlot,
                SeatState seat,
                ConsoleState console,
                CardInstanceState[] slotCards)
            {
                Match = match;
                PrimarySlot = primarySlot;
                OtherSlot = otherSlot;
                Seat = seat;
                Console = console;
                SlotCards = slotCards;
            }

            public MatchState Match { get; }

            public ContainerState PrimarySlot { get; }

            public ContainerState OtherSlot { get; }

            public SeatState Seat { get; }

            public ConsoleState Console { get; }

            public CardInstanceState[] SlotCards { get; }
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
                IReadOnlyCollection<TabletopObjectId> changedObjectIds = null,
                ContainerId changedContainerId = default(ContainerId),
                TabletopPose? changedPose = null)
            {
                HashSet<TabletopObjectId> changed = changedObjectIds == null
                    ? new HashSet<TabletopObjectId>()
                    : new HashSet<TabletopObjectId>(changedObjectIds);

                foreach (KeyValuePair<TabletopObjectId, ObjectSnapshot> pair in objectSnapshots)
                {
                    TabletopObjectState state = match.GetObject(pair.Key);
                    if (changed.Contains(pair.Key))
                    {
                        pair.Value.AssertMatchesExceptLocation(
                            state,
                            changedContainerId,
                            changedPose ?? pair.Value.Pose);
                    }
                    else
                    {
                        pair.Value.AssertMatches(state);
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
                    Assert.That(match.Seats[pair.Key].Console, Is.SameAs(pair.Value.Console));
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
