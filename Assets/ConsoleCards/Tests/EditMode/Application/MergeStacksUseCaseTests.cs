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
    public sealed class MergeStacksUseCaseTests
    {
        public enum FailureScenario
        {
            NullCommand,
            MatchMismatch,
            RevisionConflict,
            SameStack,
            SourceMissing,
            DestinationMissing,
            SourceGeneric,
            SourceDeck,
            SourceHand,
            SourceDiscardPile,
            SourceConsoleSlot,
            DestinationGeneric,
            DestinationDeck,
            DestinationHand,
            DestinationDiscardPile,
            DestinationConsoleSlot,
            EmptySource,
            DestinationCapacityExceeded,
            ObjectMissing,
            ObjectContainerMismatch,
            ObjectUserLocked,
            RevisionOverflow
        }

        [Test]
        public void Execute_WhenMatchStateIsNull_ReturnsInvalidMatchMissing()
        {
            MergeStacksUseCase useCase = new MergeStacksUseCase();

            MergeStacksResult result = useCase.Execute(
                null,
                new MergeStacksCommand(CreateContext(MatchId.New()), ContainerId.New(), ContainerId.New()));

            AssertFailure(result, CommandResultStatus.Invalid, MergeStacksError.MatchMissing);
        }

        [TestCase(FailureScenario.NullCommand, CommandResultStatus.Invalid, MergeStacksError.CommandMissing)]
        [TestCase(FailureScenario.MatchMismatch, CommandResultStatus.Invalid, MergeStacksError.MatchMismatch)]
        [TestCase(FailureScenario.RevisionConflict, CommandResultStatus.Conflict, MergeStacksError.RevisionConflict)]
        [TestCase(FailureScenario.SourceMissing, CommandResultStatus.Rejected, MergeStacksError.SourceStackMissing)]
        [TestCase(FailureScenario.DestinationMissing, CommandResultStatus.Rejected, MergeStacksError.DestinationStackMissing)]
        [TestCase(FailureScenario.SourceGeneric, CommandResultStatus.Rejected, MergeStacksError.SourceContainerNotStack)]
        [TestCase(FailureScenario.SourceDeck, CommandResultStatus.Rejected, MergeStacksError.SourceContainerNotStack)]
        [TestCase(FailureScenario.SourceHand, CommandResultStatus.Rejected, MergeStacksError.SourceContainerNotStack)]
        [TestCase(FailureScenario.SourceDiscardPile, CommandResultStatus.Rejected, MergeStacksError.SourceContainerNotStack)]
        [TestCase(FailureScenario.SourceConsoleSlot, CommandResultStatus.Rejected, MergeStacksError.SourceContainerNotStack)]
        [TestCase(FailureScenario.DestinationGeneric, CommandResultStatus.Rejected, MergeStacksError.DestinationContainerNotStack)]
        [TestCase(FailureScenario.DestinationDeck, CommandResultStatus.Rejected, MergeStacksError.DestinationContainerNotStack)]
        [TestCase(FailureScenario.DestinationHand, CommandResultStatus.Rejected, MergeStacksError.DestinationContainerNotStack)]
        [TestCase(FailureScenario.DestinationDiscardPile, CommandResultStatus.Rejected, MergeStacksError.DestinationContainerNotStack)]
        [TestCase(FailureScenario.DestinationConsoleSlot, CommandResultStatus.Rejected, MergeStacksError.DestinationContainerNotStack)]
        [TestCase(FailureScenario.EmptySource, CommandResultStatus.Rejected, MergeStacksError.SourceStackEmpty)]
        [TestCase(FailureScenario.DestinationCapacityExceeded, CommandResultStatus.Rejected, MergeStacksError.DestinationCapacityExceeded)]
        [TestCase(FailureScenario.ObjectMissing, CommandResultStatus.Rejected, MergeStacksError.ObjectMissing)]
        [TestCase(FailureScenario.ObjectContainerMismatch, CommandResultStatus.Rejected, MergeStacksError.ObjectContainerMismatch)]
        [TestCase(FailureScenario.ObjectUserLocked, CommandResultStatus.Rejected, MergeStacksError.ObjectUserLocked)]
        [TestCase(FailureScenario.RevisionOverflow, CommandResultStatus.Conflict, MergeStacksError.RevisionOverflow)]
        public void Execute_WhenValidationFails_ReturnsExpectedFailure(
            FailureScenario scenario,
            CommandResultStatus expectedStatus,
            MergeStacksError expectedError)
        {
            FailureFixture failure = CreateFailureFixture(scenario);

            MergeStacksResult result = failure.Execute();

            AssertFailure(result, expectedStatus, expectedError);
        }

        [TestCase(FailureScenario.NullCommand)]
        [TestCase(FailureScenario.MatchMismatch)]
        [TestCase(FailureScenario.RevisionConflict)]
        [TestCase(FailureScenario.SourceMissing)]
        [TestCase(FailureScenario.DestinationMissing)]
        [TestCase(FailureScenario.SourceGeneric)]
        [TestCase(FailureScenario.SourceDeck)]
        [TestCase(FailureScenario.SourceHand)]
        [TestCase(FailureScenario.SourceDiscardPile)]
        [TestCase(FailureScenario.SourceConsoleSlot)]
        [TestCase(FailureScenario.DestinationGeneric)]
        [TestCase(FailureScenario.DestinationDeck)]
        [TestCase(FailureScenario.DestinationHand)]
        [TestCase(FailureScenario.DestinationDiscardPile)]
        [TestCase(FailureScenario.DestinationConsoleSlot)]
        [TestCase(FailureScenario.EmptySource)]
        [TestCase(FailureScenario.DestinationCapacityExceeded)]
        [TestCase(FailureScenario.ObjectMissing)]
        [TestCase(FailureScenario.ObjectContainerMismatch)]
        [TestCase(FailureScenario.ObjectUserLocked)]
        [TestCase(FailureScenario.RevisionOverflow)]
        public void Execute_WhenValidationFails_PreservesAggregateState(FailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            AggregateSnapshot before = AggregateSnapshot.Capture(failure.Fixture);

            MergeStacksResult result = failure.Execute();

            Assert.That(result.Succeeded, Is.False);
            before.AssertMatches(failure.Fixture);
        }

        [Test]
        public void Execute_WhenDestinationHasMembers_AppendsSourceBottomToTop()
        {
            MergeFixture fixture = CreateFixture(destinationCount: 2, sourceCount: 2, revision: 5);

            MergeStacksResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Destination.ObjectIds, Is.EqualTo(new[]
            {
                fixture.DestinationCards[0].BaseState.Id,
                fixture.DestinationCards[1].BaseState.Id,
                fixture.SourceCards[0].BaseState.Id,
                fixture.SourceCards[1].BaseState.Id
            }));
            Assert.That(fixture.Match.Containers.ContainsKey(fixture.Source.Id), Is.False);
            Assert.That(fixture.Match.Revision, Is.EqualTo(6));
        }

        [Test]
        public void Execute_WhenDestinationIsEmpty_AppendsSourceOrder()
        {
            MergeFixture fixture = CreateFixture(destinationCount: 0, sourceCount: 2);

            MergeStacksResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Destination.ObjectIds, Is.EqualTo(new[]
            {
                fixture.SourceCards[0].BaseState.Id,
                fixture.SourceCards[1].BaseState.Id
            }));
            Assert.That(fixture.Match.Containers.ContainsKey(fixture.Source.Id), Is.False);
        }

        [Test]
        public void Execute_WhenSourceHasThreeCards_PreservesSourceInternalOrder()
        {
            MergeFixture fixture = CreateFixture(destinationCount: 1, sourceCount: 3);

            MergeStacksResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Destination.ObjectIds, Is.EqualTo(new[]
            {
                fixture.DestinationCards[0].BaseState.Id,
                fixture.SourceCards[0].BaseState.Id,
                fixture.SourceCards[1].BaseState.Id,
                fixture.SourceCards[2].BaseState.Id
            }));
        }

        [Test]
        public void Execute_WhenSuccessful_RemovesSourcePlacementAndPreservesDestinationPlacement()
        {
            MergeFixture fixture = CreateFixture(destinationCount: 1, sourceCount: 2);
            ContainerPlacementState sourcePlacement = fixture.SourcePlacement;
            ContainerPlacementState destinationPlacement = fixture.DestinationPlacement;

            MergeStacksResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Match.ContainerPlacements.ContainsKey(sourcePlacement.ContainerId), Is.False);
            Assert.That(fixture.Match.ContainerPlacements[destinationPlacement.ContainerId], Is.SameAs(destinationPlacement));
            Assert.That(fixture.Match.ContainerPlacements[destinationPlacement.ContainerId].Pose, Is.EqualTo(destinationPlacement.Pose));
        }

        [Test]
        public void Execute_WhenSourcePlacementIsMissing_StillMerges()
        {
            MergeFixture fixture = CreateFixture(destinationCount: 1, sourceCount: 1, includeSourcePlacement: false);

            MergeStacksResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Match.Containers.ContainsKey(fixture.Source.Id), Is.False);
            Assert.That(fixture.Match.ContainerPlacements.ContainsKey(fixture.Source.Id), Is.False);
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesIdentitiesAndObjectNonContainerFields()
        {
            MergeFixture fixture = CreateFixture(destinationCount: 2, sourceCount: 2, revision: 20);
            MatchState match = fixture.Match;
            ContainerState destination = fixture.Destination;
            ContainerPlacementState destinationPlacement = fixture.DestinationPlacement;
            CardInstanceState moved = fixture.SourceCards[0];
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture);

            MergeStacksResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Match, Is.SameAs(match));
            Assert.That(fixture.Match.Containers[fixture.Destination.Id], Is.SameAs(destination));
            Assert.That(fixture.Match.ContainerPlacements[fixture.Destination.Id], Is.SameAs(destinationPlacement));
            Assert.That(fixture.Match.Cards[moved.BaseState.Id], Is.SameAs(moved));
            before.AssertMatchesAfterMerge(fixture);
        }

        [Test]
        public void Execute_WhenSuccessful_HasNoDuplicateMembershipAndLosesNoSourceMember()
        {
            MergeFixture fixture = CreateFixture(destinationCount: 1, sourceCount: 3);
            TabletopObjectId[] movedIds = fixture.SourceCards.Select(card => card.BaseState.Id).ToArray();

            MergeStacksResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Destination.ObjectIds, Is.Unique);
            CollectionAssert.IsSubsetOf(movedIds, fixture.Destination.ObjectIds);
            foreach (TabletopObjectId objectId in movedIds)
            {
                Assert.That(fixture.Match.GetObject(objectId).ContainerId, Is.EqualTo(fixture.Destination.Id));
            }
        }

        [Test]
        public void StaticBoundary_UsesApplicationCoreOnlyAndAddsNoFutureStackScope()
        {
            Assembly applicationAssembly = typeof(MergeStacksUseCase).Assembly;
            string[] referencedAssemblyNames = applicationAssembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.That(referencedAssemblyNames, Does.Contain("ConsoleCards.Core"));
            Assert.That(referencedAssemblyNames.Any(name => name.StartsWith("UnityEngine", StringComparison.Ordinal)), Is.False);
            Assert.That(referencedAssemblyNames, Does.Not.Contain("ConsoleCards.Presentation"));
            Assert.That(applicationAssembly.GetType("ConsoleCards.Application.UseCases.SplitStackUseCase"), Is.Null);
            Assert.That(applicationAssembly.GetType("ConsoleCards.Application.Commands.MergeStacksPreviewCommand"), Is.Null);
        }

        private static MergeStacksResult Execute(MergeFixture fixture, MergeStacksCommand command = null)
        {
            MergeStacksCommand actualCommand = command ?? CreateCommand(fixture);
            return new MergeStacksUseCase().Execute(fixture.Match, actualCommand);
        }

        private static void AssertFailure(
            MergeStacksResult result,
            CommandResultStatus expectedStatus,
            MergeStacksError expectedError)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.Error, Is.EqualTo(expectedError));
            Assert.That(result.Revision, Is.EqualTo(-1));
        }

        private static FailureFixture CreateFailureFixture(FailureScenario scenario)
        {
            switch (scenario)
            {
                case FailureScenario.NullCommand:
                    return new FailureFixture(CreateFixture(), null);

                case FailureScenario.MatchMismatch:
                {
                    MergeFixture fixture = CreateFixture();
                    return new FailureFixture(
                        fixture,
                        new MergeStacksCommand(CreateContext(MatchId.New()), fixture.Source.Id, fixture.Destination.Id));
                }

                case FailureScenario.RevisionConflict:
                {
                    MergeFixture fixture = CreateFixture(revision: 3);
                    return new FailureFixture(fixture, CreateCommand(fixture, expectedRevision: 4));
                }

                case FailureScenario.SourceMissing:
                {
                    MergeFixture fixture = CreateFixture();
                    return new FailureFixture(
                        fixture,
                        new MergeStacksCommand(CreateContext(fixture.Match.Id), ContainerId.New(), fixture.Destination.Id));
                }

                case FailureScenario.DestinationMissing:
                {
                    MergeFixture fixture = CreateFixture();
                    return new FailureFixture(
                        fixture,
                        new MergeStacksCommand(CreateContext(fixture.Match.Id), fixture.Source.Id, ContainerId.New()));
                }

                case FailureScenario.SourceGeneric:
                    return CreateKindFailureFixture(sourceKind: ContainerKind.Generic, destinationKind: ContainerKind.Stack);
                case FailureScenario.SourceDeck:
                    return CreateKindFailureFixture(sourceKind: ContainerKind.Deck, destinationKind: ContainerKind.Stack);
                case FailureScenario.SourceHand:
                    return CreateKindFailureFixture(sourceKind: ContainerKind.Hand, destinationKind: ContainerKind.Stack);
                case FailureScenario.SourceDiscardPile:
                    return CreateKindFailureFixture(sourceKind: ContainerKind.DiscardPile, destinationKind: ContainerKind.Stack);
                case FailureScenario.SourceConsoleSlot:
                    return CreateKindFailureFixture(sourceKind: ContainerKind.ConsoleSlot, destinationKind: ContainerKind.Stack);
                case FailureScenario.DestinationGeneric:
                    return CreateKindFailureFixture(sourceKind: ContainerKind.Stack, destinationKind: ContainerKind.Generic);
                case FailureScenario.DestinationDeck:
                    return CreateKindFailureFixture(sourceKind: ContainerKind.Stack, destinationKind: ContainerKind.Deck);
                case FailureScenario.DestinationHand:
                    return CreateKindFailureFixture(sourceKind: ContainerKind.Stack, destinationKind: ContainerKind.Hand);
                case FailureScenario.DestinationDiscardPile:
                    return CreateKindFailureFixture(sourceKind: ContainerKind.Stack, destinationKind: ContainerKind.DiscardPile);
                case FailureScenario.DestinationConsoleSlot:
                    return CreateKindFailureFixture(sourceKind: ContainerKind.Stack, destinationKind: ContainerKind.ConsoleSlot);

                case FailureScenario.EmptySource:
                    return new FailureFixture(CreateFixture(sourceCount: 0), null, useDefaultCommand: true);

                case FailureScenario.DestinationCapacityExceeded:
                    return new FailureFixture(CreateFixture(destinationCount: 1, sourceCount: 2, destinationCapacity: 2), null, useDefaultCommand: true);

                case FailureScenario.ObjectMissing:
                {
                    MergeFixture fixture = CreateFixture(sourceCount: 1);
                    CardInstanceState extraCard = CreateCard();
                    new ContainerTransferService().PlaceIntoContainer(extraCard.BaseState, fixture.Source);
                    fixture.ExtraObjects.Add(extraCard.BaseState);
                    return new FailureFixture(fixture, null, useDefaultCommand: true);
                }

                case FailureScenario.ObjectContainerMismatch:
                {
                    MergeFixture fixture = CreateFixture(sourceCount: 2);
                    fixture.SourceCards[1].BaseState.SetContainer(fixture.Destination.Id);
                    return new FailureFixture(fixture, null, useDefaultCommand: true);
                }

                case FailureScenario.ObjectUserLocked:
                    return new FailureFixture(CreateFixture(sourceCount: 2, isSourceTopLocked: true), null, useDefaultCommand: true);

                case FailureScenario.RevisionOverflow:
                {
                    MergeFixture fixture = CreateFixture(revision: long.MaxValue);
                    return new FailureFixture(fixture, CreateCommand(fixture, expectedRevision: long.MaxValue));
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported merge failure scenario.");
            }
        }

        private static FailureFixture CreateKindFailureFixture(ContainerKind sourceKind, ContainerKind destinationKind)
        {
            MergeFixture fixture = CreateFixture(sourceKind: sourceKind, destinationKind: destinationKind);
            return new FailureFixture(fixture, null, useDefaultCommand: true);
        }

        private static MergeStacksCommand CreateCommand(MergeFixture fixture, long? expectedRevision = null)
        {
            return new MergeStacksCommand(
                CreateContext(fixture.Match.Id, expectedRevision ?? fixture.Match.Revision),
                fixture.Source.Id,
                fixture.Destination.Id);
        }

        private static CommandContext CreateContext(MatchId matchId, long? expectedRevision = 0)
        {
            return new CommandContext(CommandId.New(), matchId, PlayerId.New(), expectedRevision);
        }

        private static MergeFixture CreateFixture(
            ContainerKind sourceKind = ContainerKind.Stack,
            ContainerKind destinationKind = ContainerKind.Stack,
            int destinationCount = 2,
            int sourceCount = 2,
            int destinationCapacity = 0,
            long revision = 0,
            bool includeSourcePlacement = true,
            bool isSourceTopLocked = false)
        {
            SeatId seatId = SeatId.New();
            ContainerTransferService transferService = new ContainerTransferService();
            ContainerState source = CreateContainer(sourceKind, OwnerFor(sourceKind, seatId), CapacityFor(sourceKind, sourceCount));
            ContainerState destination = CreateContainer(destinationKind, OwnerFor(destinationKind, seatId), destinationCapacity);
            ContainerState hand = FirstKind(ContainerKind.Hand, source, destination)
                ?? CreateContainer(ContainerKind.Hand, seatId);
            ContainerState slot = FirstKind(ContainerKind.ConsoleSlot, source, destination)
                ?? CreateContainer(ContainerKind.ConsoleSlot, seatId, 5);
            ContainerState other = CreateContainer(ContainerKind.Generic);
            List<CardInstanceState> sourceCards = new List<CardInstanceState>();
            List<CardInstanceState> destinationCards = new List<CardInstanceState>();
            CardInstanceState otherCard = CreateCard(face: CardFace.FaceUp);

            for (int index = 0; index < sourceCount; index++)
            {
                CardInstanceState card = CreateCard(
                    face: index % 2 == 0 ? CardFace.FaceDown : CardFace.FaceUp,
                    isUserLocked: isSourceTopLocked && index == sourceCount - 1);
                transferService.PlaceIntoContainer(card.BaseState, source);
                sourceCards.Add(card);
            }

            for (int index = 0; index < destinationCount; index++)
            {
                CardInstanceState card = CreateCard(face: CardFace.FaceUp);
                transferService.PlaceIntoContainer(card.BaseState, destination);
                destinationCards.Add(card);
            }

            transferService.PlaceIntoContainer(otherCard.BaseState, other);

            List<ContainerState> containers = new List<ContainerState>
            {
                source,
                destination,
                other
            };
            AddContainerIfMissing(containers, hand);
            AddContainerIfMissing(containers, slot);

            List<ContainerPlacementState> placements = new List<ContainerPlacementState>();
            ContainerPlacementState sourcePlacement = null;
            if (CanHavePlacement(source.Kind) && includeSourcePlacement)
            {
                sourcePlacement = new ContainerPlacementState(
                    source.Id,
                    CreatePose(x: -2.0, y: 1.5, rotationDegrees: 10f));
                placements.Add(sourcePlacement);
            }

            ContainerPlacementState destinationPlacement = null;
            if (CanHavePlacement(destination.Kind))
            {
                destinationPlacement = new ContainerPlacementState(
                    destination.Id,
                    CreatePose(x: 3.0, y: -4.0, rotationDegrees: 20f));
                placements.Add(destinationPlacement);
            }

            SeatState seat = new SeatState(
                seatId,
                CreatePose(x: -5.0, y: 5.0),
                hand.Id,
                new ConsoleState(seatId, new[] { slot.Id }),
                PlayerId.Empty,
                SeatStatus.Vacant);

            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                sourceCards.Concat(destinationCards).Concat(new[] { otherCard }).ToArray(),
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                containers,
                new[] { seat },
                placements);

            return new MergeFixture(
                match,
                source,
                destination,
                other,
                sourcePlacement,
                destinationPlacement,
                seat,
                sourceCards.ToArray(),
                destinationCards.ToArray(),
                otherCard);
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

        private static TabletopPose CreatePose(
            double x = 0.0,
            double y = 0.0,
            float rotationDegrees = 0f,
            int layer = 0,
            int localOrder = 0)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotationDegrees, layer, localOrder);
        }

        private static SeatId OwnerFor(ContainerKind kind, SeatId seatId)
        {
            return kind == ContainerKind.Hand || kind == ContainerKind.ConsoleSlot ? seatId : SeatId.Empty;
        }

        private static int CapacityFor(ContainerKind kind, int count)
        {
            return kind == ContainerKind.ConsoleSlot ? Math.Max(count, 1) : 0;
        }

        private static ContainerState FirstKind(ContainerKind kind, params ContainerState[] containers)
        {
            return containers.FirstOrDefault(container => container.Kind == kind);
        }

        private static bool CanHavePlacement(ContainerKind kind)
        {
            return kind == ContainerKind.Deck
                || kind == ContainerKind.Stack
                || kind == ContainerKind.DiscardPile;
        }

        private static void AddContainerIfMissing(List<ContainerState> containers, ContainerState container)
        {
            if (containers.All(existing => existing.Id != container.Id))
            {
                containers.Add(container);
            }
        }

        private sealed class FailureFixture
        {
            private readonly bool useDefaultCommand;

            public FailureFixture(MergeFixture fixture, MergeStacksCommand command, bool useDefaultCommand = false)
            {
                Fixture = fixture;
                Command = command;
                this.useDefaultCommand = useDefaultCommand;
            }

            public MergeFixture Fixture { get; }

            private MergeStacksCommand Command { get; }

            public MergeStacksResult Execute()
            {
                MergeStacksCommand command = useDefaultCommand ? Command ?? CreateCommand(Fixture) : Command;
                return new MergeStacksUseCase().Execute(Fixture.Match, command);
            }
        }

        private sealed class MergeFixture
        {
            public MergeFixture(
                MatchState match,
                ContainerState source,
                ContainerState destination,
                ContainerState other,
                ContainerPlacementState sourcePlacement,
                ContainerPlacementState destinationPlacement,
                SeatState seat,
                CardInstanceState[] sourceCards,
                CardInstanceState[] destinationCards,
                CardInstanceState otherCard)
            {
                Match = match;
                Source = source;
                Destination = destination;
                Other = other;
                SourcePlacement = sourcePlacement;
                DestinationPlacement = destinationPlacement;
                Seat = seat;
                SourceCards = sourceCards;
                DestinationCards = destinationCards;
                OtherCard = otherCard;
                ExtraObjects = new List<TabletopObjectState>();
            }

            public MatchState Match { get; }

            public ContainerState Source { get; }

            public ContainerState Destination { get; }

            public ContainerState Other { get; }

            public ContainerPlacementState SourcePlacement { get; }

            public ContainerPlacementState DestinationPlacement { get; }

            public SeatState Seat { get; }

            public CardInstanceState[] SourceCards { get; }

            public CardInstanceState[] DestinationCards { get; }

            public CardInstanceState OtherCard { get; }

            public List<TabletopObjectState> ExtraObjects { get; }
        }

        private sealed class AggregateSnapshot
        {
            private readonly IReadOnlyDictionary<ContainerId, TabletopObjectId[]> containerOrders;
            private readonly IReadOnlyDictionary<TabletopObjectId, ObjectSnapshot> objectSnapshots;
            private readonly IReadOnlyDictionary<TabletopObjectId, CardFace> cardFaces;
            private readonly IReadOnlyDictionary<ContainerId, TabletopPose> placementPoses;
            private readonly IReadOnlyDictionary<ContainerId, ContainerPlacementState> placementInstances;
            private readonly IReadOnlyDictionary<ContainerId, ContainerState> containerInstances;
            private readonly IReadOnlyDictionary<SeatId, SeatState> seatInstances;
            private readonly long revision;

            private AggregateSnapshot(
                long revision,
                IReadOnlyDictionary<ContainerId, TabletopObjectId[]> containerOrders,
                IReadOnlyDictionary<TabletopObjectId, ObjectSnapshot> objectSnapshots,
                IReadOnlyDictionary<TabletopObjectId, CardFace> cardFaces,
                IReadOnlyDictionary<ContainerId, TabletopPose> placementPoses,
                IReadOnlyDictionary<ContainerId, ContainerPlacementState> placementInstances,
                IReadOnlyDictionary<ContainerId, ContainerState> containerInstances,
                IReadOnlyDictionary<SeatId, SeatState> seatInstances)
            {
                this.revision = revision;
                this.containerOrders = containerOrders;
                this.objectSnapshots = objectSnapshots;
                this.cardFaces = cardFaces;
                this.placementPoses = placementPoses;
                this.placementInstances = placementInstances;
                this.containerInstances = containerInstances;
                this.seatInstances = seatInstances;
            }

            public static AggregateSnapshot Capture(MergeFixture fixture)
            {
                Dictionary<TabletopObjectId, ObjectSnapshot> objectSnapshots =
                    new Dictionary<TabletopObjectId, ObjectSnapshot>();

                foreach (CardInstanceState card in fixture.Match.Cards.Values)
                {
                    objectSnapshots.Add(card.BaseState.Id, ObjectSnapshot.Capture(card.BaseState));
                }

                foreach (TabletopObjectState extraObject in fixture.ExtraObjects)
                {
                    objectSnapshots.Add(extraObject.Id, ObjectSnapshot.Capture(extraObject));
                }

                return new AggregateSnapshot(
                    fixture.Match.Revision,
                    fixture.Match.Containers.ToDictionary(pair => pair.Key, pair => pair.Value.ObjectIds.ToArray()),
                    objectSnapshots,
                    fixture.Match.Cards.ToDictionary(pair => pair.Key, pair => pair.Value.Face),
                    fixture.Match.ContainerPlacements.ToDictionary(pair => pair.Key, pair => pair.Value.Pose),
                    fixture.Match.ContainerPlacements.ToDictionary(pair => pair.Key, pair => pair.Value),
                    fixture.Match.Containers.ToDictionary(pair => pair.Key, pair => pair.Value),
                    fixture.Match.Seats.ToDictionary(pair => pair.Key, pair => pair.Value));
            }

            public void AssertMatches(MergeFixture fixture)
            {
                Assert.That(fixture.Match.Revision, Is.EqualTo(revision));
                Assert.That(fixture.Match.Containers.Keys, Is.EquivalentTo(containerOrders.Keys));
                Assert.That(fixture.Match.ContainerPlacements.Keys, Is.EquivalentTo(placementPoses.Keys));

                foreach (KeyValuePair<ContainerId, TabletopObjectId[]> pair in containerOrders)
                {
                    Assert.That(fixture.Match.Containers[pair.Key].ObjectIds, Is.EqualTo(pair.Value));
                    Assert.That(fixture.Match.Containers[pair.Key], Is.SameAs(containerInstances[pair.Key]));
                }

                foreach (KeyValuePair<TabletopObjectId, ObjectSnapshot> pair in objectSnapshots)
                {
                    pair.Value.AssertMatches();
                }

                foreach (KeyValuePair<TabletopObjectId, CardFace> pair in cardFaces)
                {
                    Assert.That(fixture.Match.Cards[pair.Key].Face, Is.EqualTo(pair.Value));
                }

                foreach (KeyValuePair<ContainerId, TabletopPose> pair in placementPoses)
                {
                    Assert.That(fixture.Match.ContainerPlacements[pair.Key].Pose, Is.EqualTo(pair.Value));
                    Assert.That(fixture.Match.ContainerPlacements[pair.Key], Is.SameAs(placementInstances[pair.Key]));
                }

                foreach (KeyValuePair<SeatId, SeatState> pair in seatInstances)
                {
                    Assert.That(fixture.Match.Seats[pair.Key], Is.SameAs(pair.Value));
                }
            }

            public void AssertMatchesAfterMerge(MergeFixture fixture)
            {
                Assert.That(fixture.Match.Revision, Is.EqualTo(revision + 1));
                Assert.That(fixture.Match.Containers.ContainsKey(fixture.Source.Id), Is.False);
                Assert.That(fixture.Match.ContainerPlacements.ContainsKey(fixture.Source.Id), Is.False);

                foreach (KeyValuePair<TabletopObjectId, ObjectSnapshot> pair in objectSnapshots)
                {
                    if (fixture.SourceCards.Any(card => card.BaseState.Id == pair.Key))
                    {
                        pair.Value.AssertMatchesExceptContainer(fixture.Destination.Id);
                    }
                    else
                    {
                        pair.Value.AssertMatches();
                    }
                }

                foreach (KeyValuePair<TabletopObjectId, CardFace> pair in cardFaces)
                {
                    Assert.That(fixture.Match.Cards[pair.Key].Face, Is.EqualTo(pair.Value));
                }

                foreach (KeyValuePair<ContainerId, TabletopPose> pair in placementPoses)
                {
                    if (pair.Key == fixture.Source.Id)
                    {
                        continue;
                    }

                    Assert.That(fixture.Match.ContainerPlacements[pair.Key].Pose, Is.EqualTo(pair.Value));
                    Assert.That(fixture.Match.ContainerPlacements[pair.Key], Is.SameAs(placementInstances[pair.Key]));
                }

                foreach (KeyValuePair<SeatId, SeatState> pair in seatInstances)
                {
                    Assert.That(fixture.Match.Seats[pair.Key], Is.SameAs(pair.Value));
                }
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
                    state,
                    state.Id,
                    state.DefinitionId,
                    state.Kind,
                    state.Pose,
                    state.ContainerId,
                    state.OwnerPlayerId,
                    state.Visibility,
                    state.IsUserLocked);
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

            private void AssertNonContainerFieldsMatch()
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
    }
}
