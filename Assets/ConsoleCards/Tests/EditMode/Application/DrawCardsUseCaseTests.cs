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
    public sealed class DrawCardsUseCaseTests
    {
        public enum DrawFailureScenario
        {
            NullCommand,
            MatchMismatch,
            RevisionConflict,
            SourceMissing,
            SourceGeneric,
            SourceStack,
            SourceHand,
            SourceDiscardPile,
            SourceConsoleSlot,
            DestinationMissing,
            InsufficientCards,
            DestinationCapacityExceeded,
            ObjectMissing,
            ObjectContainerMismatch,
            ObjectUserLocked,
            RevisionOverflow
        }

        [Test]
        public void Execute_WhenMatchStateIsNull_ReturnsInvalidMatchMissing()
        {
            DrawCardsUseCase useCase = new DrawCardsUseCase();

            DrawCardsResult result = useCase.Execute(
                null,
                CreateCommand(MatchId.New(), ContainerId.New(), ContainerId.New(), 1));

            AssertFailure(result, CommandResultStatus.Invalid, DrawCardsError.MatchMissing);
        }

        [TestCase(DrawFailureScenario.NullCommand, CommandResultStatus.Invalid, DrawCardsError.CommandMissing)]
        [TestCase(DrawFailureScenario.MatchMismatch, CommandResultStatus.Invalid, DrawCardsError.MatchMismatch)]
        [TestCase(DrawFailureScenario.RevisionConflict, CommandResultStatus.Conflict, DrawCardsError.RevisionConflict)]
        [TestCase(DrawFailureScenario.SourceMissing, CommandResultStatus.Rejected, DrawCardsError.SourceContainerMissing)]
        [TestCase(DrawFailureScenario.SourceGeneric, CommandResultStatus.Rejected, DrawCardsError.SourceContainerNotDeck)]
        [TestCase(DrawFailureScenario.SourceStack, CommandResultStatus.Rejected, DrawCardsError.SourceContainerNotDeck)]
        [TestCase(DrawFailureScenario.SourceHand, CommandResultStatus.Rejected, DrawCardsError.SourceContainerNotDeck)]
        [TestCase(DrawFailureScenario.SourceDiscardPile, CommandResultStatus.Rejected, DrawCardsError.SourceContainerNotDeck)]
        [TestCase(DrawFailureScenario.SourceConsoleSlot, CommandResultStatus.Rejected, DrawCardsError.SourceContainerNotDeck)]
        [TestCase(DrawFailureScenario.DestinationMissing, CommandResultStatus.Rejected, DrawCardsError.DestinationContainerMissing)]
        [TestCase(DrawFailureScenario.InsufficientCards, CommandResultStatus.Rejected, DrawCardsError.InsufficientCards)]
        [TestCase(DrawFailureScenario.DestinationCapacityExceeded, CommandResultStatus.Rejected, DrawCardsError.DestinationCapacityExceeded)]
        [TestCase(DrawFailureScenario.ObjectMissing, CommandResultStatus.Rejected, DrawCardsError.ObjectMissing)]
        [TestCase(DrawFailureScenario.ObjectContainerMismatch, CommandResultStatus.Rejected, DrawCardsError.ObjectContainerMismatch)]
        [TestCase(DrawFailureScenario.ObjectUserLocked, CommandResultStatus.Rejected, DrawCardsError.ObjectUserLocked)]
        [TestCase(DrawFailureScenario.RevisionOverflow, CommandResultStatus.Conflict, DrawCardsError.RevisionOverflow)]
        public void Execute_WhenValidationFails_ReturnsExpectedFailure(
            DrawFailureScenario scenario,
            CommandResultStatus expectedStatus,
            DrawCardsError expectedError)
        {
            FailureFixture failure = CreateFailureFixture(scenario);

            DrawCardsResult result = failure.Execute();

            AssertFailure(result, expectedStatus, expectedError);
        }

        [TestCase(DrawFailureScenario.NullCommand)]
        [TestCase(DrawFailureScenario.MatchMismatch)]
        [TestCase(DrawFailureScenario.RevisionConflict)]
        [TestCase(DrawFailureScenario.SourceMissing)]
        [TestCase(DrawFailureScenario.SourceGeneric)]
        [TestCase(DrawFailureScenario.SourceStack)]
        [TestCase(DrawFailureScenario.SourceHand)]
        [TestCase(DrawFailureScenario.SourceDiscardPile)]
        [TestCase(DrawFailureScenario.SourceConsoleSlot)]
        [TestCase(DrawFailureScenario.DestinationMissing)]
        [TestCase(DrawFailureScenario.InsufficientCards)]
        [TestCase(DrawFailureScenario.DestinationCapacityExceeded)]
        [TestCase(DrawFailureScenario.ObjectMissing)]
        [TestCase(DrawFailureScenario.ObjectContainerMismatch)]
        [TestCase(DrawFailureScenario.ObjectUserLocked)]
        [TestCase(DrawFailureScenario.RevisionOverflow)]
        public void Execute_WhenFailureOccurs_PreservesAggregateState(DrawFailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            AggregateSnapshot before = AggregateSnapshot.Capture(failure.Fixture);

            DrawCardsResult result = failure.Execute();

            Assert.That(result.Succeeded, Is.False);
            before.AssertMatches(failure.Fixture);
        }

        [Test]
        public void Execute_WhenDrawingOne_RemovesTopAndAppendsToDestination()
        {
            DrawFixture fixture = CreateFixture(deckCount: 4, destinationCount: 1);

            DrawCardsResult result = Execute(fixture, count: 1);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.SourceDeck.ObjectIds, Is.EqualTo(new[]
            {
                fixture.DeckCards[0].BaseState.Id,
                fixture.DeckCards[1].BaseState.Id,
                fixture.DeckCards[2].BaseState.Id
            }));
            Assert.That(fixture.Destination.ObjectIds, Is.EqualTo(new[]
            {
                fixture.DestinationCards[0].BaseState.Id,
                fixture.DeckCards[3].BaseState.Id
            }));
        }

        [Test]
        public void Execute_WhenDrawingTwo_AppendsInDrawSequence()
        {
            DrawFixture fixture = CreateFixture(deckCount: 4, destinationCount: 1);

            DrawCardsResult result = Execute(fixture, count: 2);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.SourceDeck.ObjectIds, Is.EqualTo(new[]
            {
                fixture.DeckCards[0].BaseState.Id,
                fixture.DeckCards[1].BaseState.Id
            }));
            Assert.That(fixture.Destination.ObjectIds, Is.EqualTo(new[]
            {
                fixture.DestinationCards[0].BaseState.Id,
                fixture.DeckCards[3].BaseState.Id,
                fixture.DeckCards[2].BaseState.Id
            }));
        }

        [Test]
        public void Execute_WhenDrawingCompleteDeck_AppendsTopToBottomInDrawSequence()
        {
            DrawFixture fixture = CreateFixture(deckCount: 4, destinationCount: 1);

            DrawCardsResult result = Execute(fixture, count: 4);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.SourceDeck.ObjectIds, Is.Empty);
            Assert.That(fixture.Destination.ObjectIds, Is.EqualTo(new[]
            {
                fixture.DestinationCards[0].BaseState.Id,
                fixture.DeckCards[3].BaseState.Id,
                fixture.DeckCards[2].BaseState.Id,
                fixture.DeckCards[1].BaseState.Id,
                fixture.DeckCards[0].BaseState.Id
            }));
        }

        [TestCase(ContainerKind.Generic)]
        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.ConsoleSlot)]
        public void Execute_AllowsStructurallyValidDestinationKinds(ContainerKind destinationKind)
        {
            int capacity = destinationKind == ContainerKind.ConsoleSlot ? 1 : 0;
            DrawFixture fixture = CreateFixture(
                destinationKind: destinationKind,
                deckCount: 1,
                destinationCount: 0,
                destinationCapacity: capacity);

            DrawCardsResult result = Execute(fixture, count: 1);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Destination.ObjectIds, Is.EqualTo(new[] { fixture.DeckCards[0].BaseState.Id }));
        }

        [Test]
        public void Execute_WhenDeckHasOneCard_DrawOneSucceeds()
        {
            DrawFixture fixture = CreateFixture(deckCount: 1, destinationCount: 0, revision: 6);

            DrawCardsResult result = Execute(fixture, count: 1);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.SourceDeck.ObjectIds, Is.Empty);
            Assert.That(fixture.Destination.ObjectIds, Is.EqualTo(new[] { fixture.DeckCards[0].BaseState.Id }));
            Assert.That(fixture.Match.Revision, Is.EqualTo(7));
        }

        [Test]
        public void Execute_WhenDeckHasOneCardAndCountExceedsAvailable_FailsAtomically()
        {
            DrawFixture fixture = CreateFixture(deckCount: 1, destinationCount: 0);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture);

            DrawCardsResult result = Execute(fixture, count: 2);

            AssertFailure(result, CommandResultStatus.Rejected, DrawCardsError.InsufficientCards);
            before.AssertMatches(fixture);
        }

        [Test]
        public void Execute_WhenSuccessful_AdvancesRevisionExactlyOnce()
        {
            DrawFixture fixture = CreateFixture(deckCount: 3, destinationCount: 0, revision: 20);

            DrawCardsResult result = Execute(fixture, count: 2);

            Assert.That(result.Revision, Is.EqualTo(21));
            Assert.That(fixture.Match.Revision, Is.EqualTo(21));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesIdentitiesAndOnlyMovesDrawnObjectContainers()
        {
            DrawFixture fixture = CreateFixture(deckCount: 4, destinationCount: 1);
            MatchState match = fixture.Match;
            ContainerState source = fixture.SourceDeck;
            ContainerState destination = fixture.Destination;
            CardInstanceState drawnTop = fixture.DeckCards[3];
            CardInstanceState drawnNext = fixture.DeckCards[2];
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture);

            DrawCardsResult result = Execute(fixture, count: 2);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Match, Is.SameAs(match));
            Assert.That(fixture.SourceDeck, Is.SameAs(source));
            Assert.That(fixture.Destination, Is.SameAs(destination));
            Assert.That(fixture.Match.Cards[drawnTop.BaseState.Id], Is.SameAs(drawnTop));
            Assert.That(fixture.Match.Cards[drawnNext.BaseState.Id], Is.SameAs(drawnNext));
            before.AssertMatchesAfterDraw(fixture, new[] { drawnTop.BaseState.Id, drawnNext.BaseState.Id });
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesPlacementStatesSeatsAndConsoles()
        {
            DrawFixture fixture = CreateFixture(deckCount: 3, destinationCount: 1);
            ContainerPlacementState placement = fixture.Placement;
            SeatState seat = fixture.Seat;

            DrawCardsResult result = Execute(fixture, count: 1);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Match.ContainerPlacements[placement.ContainerId], Is.SameAs(placement));
            Assert.That(fixture.Placement.Pose, Is.EqualTo(CreatePose(x: 2.5, y: -1.5, rotationDegrees: 45f, layer: 1, localOrder: 9)));
            Assert.That(fixture.Match.Seats[seat.Id], Is.SameAs(seat));
            Assert.That(fixture.Seat.Console.OwnerSeatId, Is.EqualTo(seat.Id));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesObjectNonContainerFields()
        {
            DrawFixture fixture = CreateFixture(deckCount: 3, destinationCount: 1);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture);

            DrawCardsResult result = Execute(fixture, count: 2);

            Assert.That(result.Succeeded, Is.True);
            before.AssertObjectNonContainerFieldsMatch(fixture);
        }

        [Test]
        public void StaticBoundary_UsesApplicationCoreOnlyAndAddsNoPresentationDrawCode()
        {
            Assembly applicationAssembly = typeof(DrawCardsUseCase).Assembly;
            string[] referencedAssemblyNames = applicationAssembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.That(referencedAssemblyNames, Does.Contain("ConsoleCards.Core"));
            Assert.That(referencedAssemblyNames.Any(name => name.StartsWith("UnityEngine", StringComparison.Ordinal)), Is.False);
            Assert.That(referencedAssemblyNames, Does.Not.Contain("ConsoleCards.Presentation"));
            Assert.That(applicationAssembly.GetType("ConsoleCards.Application.UseCases.MergeStacksUseCase"), Is.Null);
            Assert.That(applicationAssembly.GetType("ConsoleCards.Application.UseCases.SplitStackUseCase"), Is.Null);
        }

        private static DrawCardsResult Execute(DrawFixture fixture, int count)
        {
            DrawCardsCommand command = CreateCommand(
                fixture.Match.Id,
                fixture.SourceDeck.Id,
                fixture.Destination.Id,
                count,
                fixture.Match.Revision);
            DrawCardsUseCase useCase = new DrawCardsUseCase();
            return useCase.Execute(fixture.Match, command);
        }

        private static void AssertFailure(
            DrawCardsResult result,
            CommandResultStatus expectedStatus,
            DrawCardsError expectedError)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.Error, Is.EqualTo(expectedError));
            Assert.That(result.Revision, Is.EqualTo(-1));
        }

        private static FailureFixture CreateFailureFixture(DrawFailureScenario scenario)
        {
            switch (scenario)
            {
                case DrawFailureScenario.NullCommand:
                {
                    DrawFixture fixture = CreateFixture();
                    return new FailureFixture(fixture, null);
                }

                case DrawFailureScenario.MatchMismatch:
                {
                    DrawFixture fixture = CreateFixture();
                    DrawCardsCommand command = CreateCommand(
                        MatchId.New(),
                        fixture.SourceDeck.Id,
                        fixture.Destination.Id,
                        1);
                    return new FailureFixture(fixture, command);
                }

                case DrawFailureScenario.RevisionConflict:
                {
                    DrawFixture fixture = CreateFixture(revision: 4);
                    DrawCardsCommand command = CreateCommand(
                        fixture.Match.Id,
                        fixture.SourceDeck.Id,
                        fixture.Destination.Id,
                        1,
                        expectedRevision: 5);
                    return new FailureFixture(fixture, command);
                }

                case DrawFailureScenario.SourceMissing:
                {
                    DrawFixture fixture = CreateFixture();
                    DrawCardsCommand command = CreateCommand(
                        fixture.Match.Id,
                        ContainerId.New(),
                        fixture.Destination.Id,
                        1);
                    return new FailureFixture(fixture, command);
                }

                case DrawFailureScenario.SourceGeneric:
                    return CreateSourceKindFailureFixture(ContainerKind.Generic);

                case DrawFailureScenario.SourceStack:
                    return CreateSourceKindFailureFixture(ContainerKind.Stack);

                case DrawFailureScenario.SourceHand:
                    return CreateSourceKindFailureFixture(ContainerKind.Hand);

                case DrawFailureScenario.SourceDiscardPile:
                    return CreateSourceKindFailureFixture(ContainerKind.DiscardPile);

                case DrawFailureScenario.SourceConsoleSlot:
                    return CreateSourceKindFailureFixture(ContainerKind.ConsoleSlot);

                case DrawFailureScenario.DestinationMissing:
                {
                    DrawFixture fixture = CreateFixture();
                    DrawCardsCommand command = CreateCommand(
                        fixture.Match.Id,
                        fixture.SourceDeck.Id,
                        ContainerId.New(),
                        1);
                    return new FailureFixture(fixture, command);
                }

                case DrawFailureScenario.InsufficientCards:
                {
                    DrawFixture fixture = CreateFixture(deckCount: 2);
                    return new FailureFixture(fixture, CreateCommandForFixture(fixture, count: 3));
                }

                case DrawFailureScenario.DestinationCapacityExceeded:
                {
                    DrawFixture fixture = CreateFixture(deckCount: 3, destinationCount: 1, destinationCapacity: 2);
                    return new FailureFixture(fixture, CreateCommandForFixture(fixture, count: 2));
                }

                case DrawFailureScenario.ObjectMissing:
                {
                    DrawFixture fixture = CreateFixture(deckCount: 2);
                    AddUnmatchedTopObject(fixture);
                    return new FailureFixture(fixture, CreateCommandForFixture(fixture, count: 1));
                }

                case DrawFailureScenario.ObjectContainerMismatch:
                {
                    DrawFixture fixture = CreateFixture(deckCount: 2);
                    fixture.DeckCards[1].BaseState.SetContainer(fixture.Destination.Id);
                    return new FailureFixture(fixture, CreateCommandForFixture(fixture, count: 1));
                }

                case DrawFailureScenario.ObjectUserLocked:
                {
                    DrawFixture fixture = CreateFixture(deckCount: 2);
                    fixture.DeckCards[1].BaseState.SetUserLocked(true);
                    return new FailureFixture(fixture, CreateCommandForFixture(fixture, count: 1));
                }

                case DrawFailureScenario.RevisionOverflow:
                {
                    DrawFixture fixture = CreateFixture(revision: long.MaxValue);
                    return new FailureFixture(
                        fixture,
                        CreateCommandForFixture(fixture, count: 1, expectedRevision: long.MaxValue));
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported draw failure scenario.");
            }
        }

        private static FailureFixture CreateSourceKindFailureFixture(ContainerKind kind)
        {
            DrawFixture fixture = CreateFixture(sourceKind: kind);
            return new FailureFixture(fixture, CreateCommandForFixture(fixture, count: 1));
        }

        private static DrawCardsCommand CreateCommandForFixture(
            DrawFixture fixture,
            int count,
            long? expectedRevision = 0)
        {
            return CreateCommand(
                fixture.Match.Id,
                fixture.SourceDeck.Id,
                fixture.Destination.Id,
                count,
                expectedRevision);
        }

        private static DrawCardsCommand CreateCommand(
            MatchId matchId,
            ContainerId sourceDeckContainerId,
            ContainerId destinationContainerId,
            int count,
            long? expectedRevision = 0)
        {
            CommandContext context = new CommandContext(
                CommandId.New(),
                matchId,
                PlayerId.New(),
                expectedRevision);

            return new DrawCardsCommand(context, sourceDeckContainerId, destinationContainerId, count);
        }

        private static DrawFixture CreateFixture(
            ContainerKind sourceKind = ContainerKind.Deck,
            ContainerKind destinationKind = ContainerKind.Generic,
            int deckCount = 4,
            int destinationCount = 1,
            int destinationCapacity = 0,
            long revision = 0)
        {
            ContainerTransferService transferService = new ContainerTransferService();
            SeatId seatId = SeatId.New();
            ContainerState sourceDeck = CreateContainer(kind: sourceKind);
            SeatId destinationOwner = destinationKind == ContainerKind.Hand || destinationKind == ContainerKind.ConsoleSlot
                ? seatId
                : SeatId.Empty;
            ContainerState destination = CreateContainer(
                kind: destinationKind,
                ownerSeatId: destinationOwner,
                capacity: destinationCapacity);
            List<CardInstanceState> deckCards = new List<CardInstanceState>();
            List<CardInstanceState> destinationCards = new List<CardInstanceState>();

            for (int index = 0; index < deckCount; index++)
            {
                CardInstanceState card = CreateCard(face: index % 2 == 0 ? CardFace.FaceDown : CardFace.FaceUp);
                transferService.PlaceIntoContainer(card.BaseState, sourceDeck);
                deckCards.Add(card);
            }

            for (int index = 0; index < destinationCount; index++)
            {
                CardInstanceState card = CreateCard(face: CardFace.FaceUp);
                transferService.PlaceIntoContainer(card.BaseState, destination);
                destinationCards.Add(card);
            }

            ContainerState otherContainer = CreateContainer(kind: ContainerKind.Generic);
            CardInstanceState otherCard = CreateCard(face: CardFace.FaceUp);
            transferService.PlaceIntoContainer(otherCard.BaseState, otherContainer);

            ContainerState handContainer = destinationKind == ContainerKind.Hand
                ? destination
                : CreateContainer(kind: ContainerKind.Hand, ownerSeatId: seatId);
            ContainerState slotContainer = destinationKind == ContainerKind.ConsoleSlot
                ? destination
                : CreateContainer(kind: ContainerKind.ConsoleSlot, ownerSeatId: seatId);

            SeatState seat = new SeatState(
                seatId,
                CreatePose(x: -4.0, y: 4.0),
                handContainer.Id,
                new ConsoleState(seatId, new[] { slotContainer.Id }),
                PlayerId.Empty,
                SeatStatus.Vacant);

            List<ContainerState> containers = new List<ContainerState>
            {
                sourceDeck,
                destination,
                otherContainer
            };

            AddContainerIfMissing(containers, handContainer);
            AddContainerIfMissing(containers, slotContainer);

            ContainerState placementContainer = sourceKind == ContainerKind.Deck
                || sourceKind == ContainerKind.Stack
                || sourceKind == ContainerKind.DiscardPile
                    ? sourceDeck
                    : CreateContainer(kind: ContainerKind.Deck);
            AddContainerIfMissing(containers, placementContainer);

            ContainerPlacementState placement = new ContainerPlacementState(
                placementContainer.Id,
                CreatePose(x: 2.5, y: -1.5, rotationDegrees: 45f, layer: 1, localOrder: 9));

            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                deckCards.Concat(destinationCards).Concat(new[] { otherCard }).ToArray(),
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                containers,
                new[] { seat },
                new[] { placement });

            return new DrawFixture(
                match,
                sourceDeck,
                destination,
                otherContainer,
                placement,
                seat,
                deckCards.ToArray(),
                destinationCards.ToArray(),
                otherCard);
        }

        private static void AddContainerIfMissing(List<ContainerState> containers, ContainerState container)
        {
            if (!containers.Any(existing => existing.Id == container.Id))
            {
                containers.Add(container);
            }
        }

        private static void AddUnmatchedTopObject(DrawFixture fixture)
        {
            TabletopObjectState objectState = CreateCard().BaseState;
            ContainerTransferService transferService = new ContainerTransferService();
            transferService.PlaceIntoContainer(objectState, fixture.SourceDeck);
            fixture.ExtraObjects.Add(objectState);
        }

        private static ContainerState CreateContainer(
            ContainerKind kind,
            SeatId? ownerSeatId = null,
            int capacity = 0)
        {
            return new ContainerState(
                ContainerId.New(),
                kind,
                ownerSeatId ?? SeatId.Empty,
                ObjectVisibility.Public,
                capacity);
        }

        private static CardInstanceState CreateCard(CardFace face = CardFace.FaceDown)
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
                face);
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

        private sealed class DrawFixture
        {
            public DrawFixture(
                MatchState match,
                ContainerState sourceDeck,
                ContainerState destination,
                ContainerState otherContainer,
                ContainerPlacementState placement,
                SeatState seat,
                CardInstanceState[] deckCards,
                CardInstanceState[] destinationCards,
                CardInstanceState otherCard)
            {
                Match = match;
                SourceDeck = sourceDeck;
                Destination = destination;
                OtherContainer = otherContainer;
                Placement = placement;
                Seat = seat;
                DeckCards = deckCards;
                DestinationCards = destinationCards;
                OtherCard = otherCard;
                ExtraObjects = new List<TabletopObjectState>();
            }

            public MatchState Match { get; }

            public ContainerState SourceDeck { get; }

            public ContainerState Destination { get; }

            public ContainerState OtherContainer { get; }

            public ContainerPlacementState Placement { get; }

            public SeatState Seat { get; }

            public CardInstanceState[] DeckCards { get; }

            public CardInstanceState[] DestinationCards { get; }

            public CardInstanceState OtherCard { get; }

            public List<TabletopObjectState> ExtraObjects { get; }
        }

        private sealed class FailureFixture
        {
            public FailureFixture(DrawFixture fixture, DrawCardsCommand command)
            {
                Fixture = fixture;
                Command = command;
            }

            public DrawFixture Fixture { get; }

            private DrawCardsCommand Command { get; }

            public DrawCardsResult Execute()
            {
                DrawCardsUseCase useCase = new DrawCardsUseCase();
                return useCase.Execute(Fixture.Match, Command);
            }
        }

        private sealed class AggregateSnapshot
        {
            private AggregateSnapshot(
                long revision,
                TabletopObjectId[] sourceOrder,
                TabletopObjectId[] destinationOrder,
                TabletopObjectId[] otherOrder,
                TabletopPose placementPose,
                ContainerPlacementState placement,
                SeatState seat,
                ObjectSnapshot[] objectSnapshots,
                CardSnapshot[] cardSnapshots)
            {
                Revision = revision;
                SourceOrder = sourceOrder;
                DestinationOrder = destinationOrder;
                OtherOrder = otherOrder;
                PlacementPose = placementPose;
                Placement = placement;
                Seat = seat;
                ObjectSnapshots = objectSnapshots;
                CardSnapshots = cardSnapshots;
            }

            private long Revision { get; }

            private TabletopObjectId[] SourceOrder { get; }

            private TabletopObjectId[] DestinationOrder { get; }

            private TabletopObjectId[] OtherOrder { get; }

            private TabletopPose PlacementPose { get; }

            private ContainerPlacementState Placement { get; }

            private SeatState Seat { get; }

            private ObjectSnapshot[] ObjectSnapshots { get; }

            private CardSnapshot[] CardSnapshots { get; }

            public static AggregateSnapshot Capture(DrawFixture fixture)
            {
                TabletopObjectState[] objectStates = fixture.Match.Cards.Values
                    .Select(card => card.BaseState)
                    .Concat(fixture.ExtraObjects)
                    .ToArray();

                return new AggregateSnapshot(
                    fixture.Match.Revision,
                    fixture.SourceDeck.ObjectIds.ToArray(),
                    fixture.Destination.ObjectIds.ToArray(),
                    fixture.OtherContainer.ObjectIds.ToArray(),
                    fixture.Placement.Pose,
                    fixture.Placement,
                    fixture.Seat,
                    objectStates.Select(ObjectSnapshot.Capture).ToArray(),
                    fixture.Match.Cards.Values.Select(CardSnapshot.Capture).ToArray());
            }

            public void AssertMatches(DrawFixture fixture)
            {
                Assert.That(fixture.Match.Revision, Is.EqualTo(Revision));
                Assert.That(fixture.SourceDeck.ObjectIds, Is.EqualTo(SourceOrder));
                Assert.That(fixture.Destination.ObjectIds, Is.EqualTo(DestinationOrder));
                Assert.That(fixture.OtherContainer.ObjectIds, Is.EqualTo(OtherOrder));
                AssertCommonState(fixture);

                foreach (ObjectSnapshot snapshot in ObjectSnapshots)
                {
                    snapshot.AssertMatches();
                }
            }

            public void AssertMatchesAfterDraw(
                DrawFixture fixture,
                IReadOnlyCollection<TabletopObjectId> drawnObjectIds)
            {
                Assert.That(fixture.Match.Revision, Is.EqualTo(Revision + 1));
                Assert.That(fixture.OtherContainer.ObjectIds, Is.EqualTo(OtherOrder));
                AssertCommonState(fixture);

                foreach (ObjectSnapshot snapshot in ObjectSnapshots)
                {
                    if (drawnObjectIds.Contains(snapshot.Id))
                    {
                        snapshot.AssertMatchesExceptContainer(fixture.Destination.Id);
                    }
                    else
                    {
                        snapshot.AssertMatches();
                    }
                }
            }

            public void AssertObjectNonContainerFieldsMatch(DrawFixture fixture)
            {
                foreach (ObjectSnapshot snapshot in ObjectSnapshots)
                {
                    snapshot.AssertNonContainerFieldsMatch();
                }

                foreach (CardSnapshot snapshot in CardSnapshots)
                {
                    snapshot.AssertMatches(fixture.Match.Cards[snapshot.Id]);
                }
            }

            private void AssertCommonState(DrawFixture fixture)
            {
                Assert.That(fixture.Match.ContainerPlacements[Placement.ContainerId], Is.SameAs(Placement));
                Assert.That(fixture.Placement.Pose, Is.EqualTo(PlacementPose));
                Assert.That(fixture.Match.Seats[Seat.Id], Is.SameAs(Seat));
                Assert.That(fixture.Seat.Console.OwnerSeatId, Is.EqualTo(Seat.Id));
            }
        }

        private sealed class ObjectSnapshot
        {
            private readonly TabletopObjectState objectState;

            private ObjectSnapshot(
                TabletopObjectState objectState,
                TabletopObjectId id,
                ObjectDefinitionId definitionId,
                TabletopObjectKind kind,
                TabletopPose pose,
                ContainerId containerId,
                PlayerId ownerPlayerId,
                ObjectVisibility visibility,
                bool isUserLocked)
            {
                this.objectState = objectState;
                Id = id;
                DefinitionId = definitionId;
                Kind = kind;
                Pose = pose;
                ContainerId = containerId;
                OwnerPlayerId = ownerPlayerId;
                Visibility = visibility;
                IsUserLocked = isUserLocked;
            }

            public TabletopObjectId Id { get; }

            private ObjectDefinitionId DefinitionId { get; }

            private TabletopObjectKind Kind { get; }

            private TabletopPose Pose { get; }

            private ContainerId ContainerId { get; }

            private PlayerId OwnerPlayerId { get; }

            private ObjectVisibility Visibility { get; }

            private bool IsUserLocked { get; }

            public static ObjectSnapshot Capture(TabletopObjectState objectState)
            {
                return new ObjectSnapshot(
                    objectState,
                    objectState.Id,
                    objectState.DefinitionId,
                    objectState.Kind,
                    objectState.Pose,
                    objectState.ContainerId,
                    objectState.OwnerPlayerId,
                    objectState.Visibility,
                    objectState.IsUserLocked);
            }

            public void AssertMatches()
            {
                AssertNonContainerFieldsMatch();
                Assert.That(objectState.ContainerId, Is.EqualTo(ContainerId));
            }

            public void AssertMatchesExceptContainer(ContainerId expectedContainerId)
            {
                AssertNonContainerFieldsMatch();
                Assert.That(objectState.ContainerId, Is.EqualTo(expectedContainerId));
            }

            public void AssertNonContainerFieldsMatch()
            {
                Assert.That(objectState.Id, Is.EqualTo(Id));
                Assert.That(objectState.DefinitionId, Is.EqualTo(DefinitionId));
                Assert.That(objectState.Kind, Is.EqualTo(Kind));
                Assert.That(objectState.Pose, Is.EqualTo(Pose));
                Assert.That(objectState.OwnerPlayerId, Is.EqualTo(OwnerPlayerId));
                Assert.That(objectState.Visibility, Is.EqualTo(Visibility));
                Assert.That(objectState.IsUserLocked, Is.EqualTo(IsUserLocked));
            }
        }

        private sealed class CardSnapshot
        {
            private CardSnapshot(TabletopObjectId id, CardFace face)
            {
                Id = id;
                Face = face;
            }

            public TabletopObjectId Id { get; }

            private CardFace Face { get; }

            public static CardSnapshot Capture(CardInstanceState card)
            {
                return new CardSnapshot(card.BaseState.Id, card.Face);
            }

            public void AssertMatches(CardInstanceState card)
            {
                Assert.That(card.Face, Is.EqualTo(Face));
            }
        }
    }
}
