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
    public sealed class ReorderContainerUseCaseTests
    {
        public enum FailureScenario
        {
            NullCommand,
            MatchMismatch,
            RevisionConflict,
            ContainerMissing,
            InvalidFromIndex,
            InvalidToIndex,
            ObjectMissing,
            ObjectContainerMismatch,
            ObjectMembershipMissing,
            ObjectIndexMismatch,
            ObjectUserLocked,
            RevisionOverflow
        }

        [Test]
        public void Execute_WhenMatchStateIsNull_ReturnsInvalidMatchMissing()
        {
            ReorderContainerUseCase useCase = new ReorderContainerUseCase();

            ReorderContainerResult result = useCase.Execute(
                null,
                new ReorderContainerCommand(
                    CreateContext(MatchId.New()),
                    ContainerId.New(),
                    TabletopObjectId.New(),
                    fromIndex: 0,
                    toIndex: 1));

            AssertFailure(result, CommandResultStatus.Invalid, ReorderContainerError.MatchMissing);
        }

        [TestCase(FailureScenario.NullCommand, CommandResultStatus.Invalid, ReorderContainerError.CommandMissing)]
        [TestCase(FailureScenario.MatchMismatch, CommandResultStatus.Invalid, ReorderContainerError.MatchMismatch)]
        [TestCase(FailureScenario.RevisionConflict, CommandResultStatus.Conflict, ReorderContainerError.RevisionConflict)]
        [TestCase(FailureScenario.ContainerMissing, CommandResultStatus.Rejected, ReorderContainerError.ContainerMissing)]
        [TestCase(FailureScenario.InvalidFromIndex, CommandResultStatus.Invalid, ReorderContainerError.InvalidFromIndex)]
        [TestCase(FailureScenario.InvalidToIndex, CommandResultStatus.Invalid, ReorderContainerError.InvalidToIndex)]
        [TestCase(FailureScenario.ObjectMissing, CommandResultStatus.Rejected, ReorderContainerError.ObjectMissing)]
        [TestCase(FailureScenario.ObjectContainerMismatch, CommandResultStatus.Rejected, ReorderContainerError.ObjectContainerMismatch)]
        [TestCase(FailureScenario.ObjectMembershipMissing, CommandResultStatus.Rejected, ReorderContainerError.ObjectMembershipMissing)]
        [TestCase(FailureScenario.ObjectIndexMismatch, CommandResultStatus.Rejected, ReorderContainerError.ObjectIndexMismatch)]
        [TestCase(FailureScenario.ObjectUserLocked, CommandResultStatus.Rejected, ReorderContainerError.ObjectUserLocked)]
        [TestCase(FailureScenario.RevisionOverflow, CommandResultStatus.Conflict, ReorderContainerError.RevisionOverflow)]
        public void Execute_WhenValidationFails_ReturnsExpectedFailure(
            FailureScenario scenario,
            CommandResultStatus expectedStatus,
            ReorderContainerError expectedError)
        {
            FailureFixture failure = CreateFailureFixture(scenario);

            ReorderContainerResult result = failure.Execute();

            AssertFailure(result, expectedStatus, expectedError);
        }

        [TestCase(FailureScenario.NullCommand)]
        [TestCase(FailureScenario.MatchMismatch)]
        [TestCase(FailureScenario.RevisionConflict)]
        [TestCase(FailureScenario.ContainerMissing)]
        [TestCase(FailureScenario.InvalidFromIndex)]
        [TestCase(FailureScenario.InvalidToIndex)]
        [TestCase(FailureScenario.ObjectMissing)]
        [TestCase(FailureScenario.ObjectContainerMismatch)]
        [TestCase(FailureScenario.ObjectMembershipMissing)]
        [TestCase(FailureScenario.ObjectIndexMismatch)]
        [TestCase(FailureScenario.ObjectUserLocked)]
        [TestCase(FailureScenario.RevisionOverflow)]
        public void Execute_WhenValidationFails_PreservesAggregateState(FailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            AggregateSnapshot before = AggregateSnapshot.Capture(failure.Fixture.Match);

            ReorderContainerResult result = failure.Execute();

            Assert.That(result.Succeeded, Is.False);
            before.AssertMatches(failure.Fixture.Match);
        }

        [TestCase(0, 2, "B,C,A")]
        [TestCase(2, 0, "C,A,B")]
        public void Execute_WithThreeCards_UsesFinalDestinationIndex(
            int fromIndex,
            int toIndex,
            string expectedLabels)
        {
            ReorderFixture fixture = CreateFixture(ContainerKind.Hand, memberCount: 3, revision: 6);
            ReorderContainerCommand command = CreateCommand(
                fixture,
                fixture.Cards[fromIndex].BaseState.Id,
                fromIndex,
                toIndex);

            ReorderContainerResult result = new ReorderContainerUseCase().Execute(fixture.Match, command);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Container.ObjectIds, Is.EqualTo(IdsForLabels(fixture, expectedLabels)));
            Assert.That(fixture.Match.Revision, Is.EqualTo(7));
        }

        [TestCase(1, 3, "A,C,D,B")]
        [TestCase(3, 1, "A,D,B,C")]
        public void Execute_WithFourCards_UsesFinalDestinationIndex(
            int fromIndex,
            int toIndex,
            string expectedLabels)
        {
            ReorderFixture fixture = CreateFixture(ContainerKind.Deck, memberCount: 4, revision: 2);
            ReorderContainerCommand command = CreateCommand(
                fixture,
                fixture.Cards[fromIndex].BaseState.Id,
                fromIndex,
                toIndex);

            ReorderContainerResult result = new ReorderContainerUseCase().Execute(fixture.Match, command);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Container.ObjectIds, Is.EqualTo(IdsForLabels(fixture, expectedLabels)));
            Assert.That(fixture.Match.Revision, Is.EqualTo(3));
        }

        [Test]
        public void Execute_WhenSameIndex_IsAcceptedAndAdvancesRevision()
        {
            ReorderFixture fixture = CreateFixture(ContainerKind.Stack, memberCount: 3, revision: 10);
            TabletopObjectId[] originalOrder = fixture.Container.ObjectIds.ToArray();

            ReorderContainerResult result = new ReorderContainerUseCase().Execute(
                fixture.Match,
                CreateCommand(fixture, fixture.Cards[1].BaseState.Id, fromIndex: 1, toIndex: 1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(11));
            Assert.That(fixture.Match.Revision, Is.EqualTo(11));
            Assert.That(fixture.Container.ObjectIds, Is.EqualTo(originalOrder));
        }

        [TestCase(ContainerKind.Generic)]
        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.ConsoleSlot)]
        public void Execute_WhenContainerKindIsSupported_Reorders(ContainerKind kind)
        {
            ReorderFixture fixture = CreateFixture(kind, memberCount: 3, revision: 14);
            TabletopObjectId targetId = fixture.Cards[0].BaseState.Id;

            ReorderContainerResult result = new ReorderContainerUseCase().Execute(
                fixture.Match,
                CreateCommand(fixture, targetId, fromIndex: 0, toIndex: 2));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Container.ObjectIds.Last(), Is.EqualTo(targetId));
            Assert.That(fixture.Match.Revision, Is.EqualTo(15));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesStateInvariantsAndIdentity()
        {
            ReorderFixture fixture = CreateFixture(ContainerKind.DiscardPile, memberCount: 4, revision: 20);
            MatchState match = fixture.Match;
            ContainerState container = fixture.Container;
            CardInstanceState target = fixture.Cards[1];
            AggregateSnapshot before = AggregateSnapshot.Capture(match);

            ReorderContainerResult result = new ReorderContainerUseCase().Execute(
                match,
                CreateCommand(fixture, target.BaseState.Id, fromIndex: 1, toIndex: 3));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(match, Is.SameAs(fixture.Match));
            Assert.That(match.Containers[container.Id], Is.SameAs(container));
            Assert.That(match.Cards[target.BaseState.Id], Is.SameAs(target));
            Assert.That(container.ObjectIds, Is.Unique);
            Assert.That(container.ObjectIds.OrderBy(id => id.ToString()), Is.EqualTo(before.ContainerOrder(container.Id).OrderBy(id => id.ToString())));
            before.AssertNonOrderStateMatches(match);
        }

        [Test]
        public void StaticBoundary_UsesApplicationCoreOnlyAndAddsNoFutureReorderScope()
        {
            Assembly applicationAssembly = typeof(ReorderContainerUseCase).Assembly;
            string[] referencedAssemblyNames = applicationAssembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.That(referencedAssemblyNames, Does.Contain("ConsoleCards.Core"));
            Assert.That(referencedAssemblyNames.Any(name => name.StartsWith("UnityEngine", StringComparison.Ordinal)), Is.False);
            Assert.That(referencedAssemblyNames, Does.Not.Contain("ConsoleCards.Presentation"));
            Assert.That(applicationAssembly.GetType("ConsoleCards.Application.Commands.ReorderCardsCommand"), Is.Null);
        }

        private static void AssertFailure(
            ReorderContainerResult result,
            CommandResultStatus expectedStatus,
            ReorderContainerError expectedError)
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
                    return new FailureFixture(CreateFixture(ContainerKind.Hand), null);

                case FailureScenario.MatchMismatch:
                {
                    ReorderFixture fixture = CreateFixture(ContainerKind.Hand);
                    return new FailureFixture(
                        fixture,
                        new ReorderContainerCommand(
                            CreateContext(MatchId.New()),
                            fixture.Container.Id,
                            fixture.Cards[0].BaseState.Id,
                            fromIndex: 0,
                            toIndex: 1));
                }

                case FailureScenario.RevisionConflict:
                {
                    ReorderFixture fixture = CreateFixture(ContainerKind.Hand, revision: 3);
                    return new FailureFixture(
                        fixture,
                        CreateCommand(fixture, fixture.Cards[0].BaseState.Id, 0, 1, expectedRevision: 2));
                }

                case FailureScenario.ContainerMissing:
                {
                    ReorderFixture fixture = CreateFixture(ContainerKind.Hand);
                    return new FailureFixture(
                        fixture,
                        new ReorderContainerCommand(
                            CreateContext(fixture.Match.Id),
                            ContainerId.New(),
                            fixture.Cards[0].BaseState.Id,
                            fromIndex: 0,
                            toIndex: 1));
                }

                case FailureScenario.InvalidFromIndex:
                {
                    ReorderFixture fixture = CreateFixture(ContainerKind.Hand);
                    return new FailureFixture(
                        fixture,
                        CreateCommand(fixture, fixture.Cards[0].BaseState.Id, fixture.Container.Count, 1));
                }

                case FailureScenario.InvalidToIndex:
                {
                    ReorderFixture fixture = CreateFixture(ContainerKind.Hand);
                    return new FailureFixture(
                        fixture,
                        CreateCommand(fixture, fixture.Cards[0].BaseState.Id, 0, fixture.Container.Count));
                }

                case FailureScenario.ObjectMissing:
                {
                    ReorderFixture fixture = CreateFixture(ContainerKind.Hand);
                    return new FailureFixture(
                        fixture,
                        CreateCommand(fixture, TabletopObjectId.New(), 0, 1));
                }

                case FailureScenario.ObjectContainerMismatch:
                {
                    ReorderFixture fixture = CreateFixture(ContainerKind.Hand);
                    fixture.Cards[0].BaseState.SetContainer(fixture.OtherContainer.Id);
                    return new FailureFixture(
                        fixture,
                        CreateCommand(fixture, fixture.Cards[0].BaseState.Id, 0, 1));
                }

                case FailureScenario.ObjectMembershipMissing:
                {
                    ReorderFixture fixture = CreateFixture(ContainerKind.Hand);
                    fixture.Container.RemoveObjectForTest(fixture.Cards[0].BaseState.Id);
                    fixture.Cards[0].BaseState.SetContainer(fixture.Container.Id);
                    return new FailureFixture(
                        fixture,
                        CreateCommand(fixture, fixture.Cards[0].BaseState.Id, 0, 1));
                }

                case FailureScenario.ObjectIndexMismatch:
                {
                    ReorderFixture fixture = CreateFixture(ContainerKind.Hand);
                    return new FailureFixture(
                        fixture,
                        CreateCommand(fixture, fixture.Cards[1].BaseState.Id, 0, 2));
                }

                case FailureScenario.ObjectUserLocked:
                    return new FailureFixture(
                        CreateFixture(ContainerKind.Hand, isTargetLocked: true),
                        null,
                        useDefaultCommand: true);

                case FailureScenario.RevisionOverflow:
                {
                    ReorderFixture fixture = CreateFixture(ContainerKind.Hand, revision: long.MaxValue);
                    return new FailureFixture(
                        fixture,
                        CreateCommand(fixture, fixture.Cards[0].BaseState.Id, 0, 1, expectedRevision: long.MaxValue));
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown failure scenario.");
            }
        }

        private static ReorderContainerCommand CreateCommand(
            ReorderFixture fixture,
            TabletopObjectId objectId,
            int fromIndex,
            int toIndex,
            long? expectedRevision = null)
        {
            return new ReorderContainerCommand(
                CreateContext(fixture.Match.Id, expectedRevision ?? fixture.Match.Revision),
                fixture.Container.Id,
                objectId,
                fromIndex,
                toIndex);
        }

        private static TabletopObjectId[] IdsForLabels(ReorderFixture fixture, string labels)
        {
            return labels
                .Split(',')
                .Select(label => fixture.Cards[label[0] - 'A'].BaseState.Id)
                .ToArray();
        }

        private static ReorderFixture CreateFixture(
            ContainerKind kind,
            int memberCount = 3,
            long revision = 0,
            bool isTargetLocked = false)
        {
            SeatId seatId = SeatId.New();
            ContainerState container = CreateContainer(kind, OwnerFor(kind, seatId), kind == ContainerKind.ConsoleSlot ? 5 : 0);
            ContainerState otherContainer = CreateContainer(ContainerKind.Generic);
            ContainerState hand = kind == ContainerKind.Hand
                ? container
                : CreateContainer(ContainerKind.Hand, seatId);
            ContainerState slot = kind == ContainerKind.ConsoleSlot
                ? container
                : CreateContainer(ContainerKind.ConsoleSlot, seatId, 5);
            ContainerState placementContainer = CanHavePlacement(kind)
                ? container
                : CreateContainer(ContainerKind.Deck);
            ContainerTransferService transferService = new ContainerTransferService();
            List<CardInstanceState> cards = new List<CardInstanceState>();
            CardInstanceState otherCard = CreateCard();

            for (int index = 0; index < memberCount; index++)
            {
                CardInstanceState card = CreateCard(isUserLocked: index == 0 && isTargetLocked);
                transferService.PlaceIntoContainer(card.BaseState, container);
                cards.Add(card);
            }

            transferService.PlaceIntoContainer(otherCard.BaseState, otherContainer);

            List<ContainerState> containers = new List<ContainerState>
            {
                container,
                otherContainer
            };
            AddContainerIfMissing(containers, hand);
            AddContainerIfMissing(containers, slot);
            AddContainerIfMissing(containers, placementContainer);

            ContainerPlacementState placement = new ContainerPlacementState(
                placementContainer.Id,
                CreatePose(x: 4.0, y: -3.0, rotationDegrees: 15f));
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
                cards.Concat(new[] { otherCard }).ToArray(),
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                containers,
                new[] { seat },
                new[] { placement });

            return new ReorderFixture(match, container, otherContainer, placement, seat, cards.ToArray(), otherCard);
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

        private static CardInstanceState CreateCard(bool isUserLocked = false)
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
                CardFace.FaceDown);
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

        private static CommandContext CreateContext(MatchId matchId, long? expectedRevision = 0)
        {
            return new CommandContext(CommandId.New(), matchId, PlayerId.New(), expectedRevision);
        }

        private static SeatId OwnerFor(ContainerKind kind, SeatId seatId)
        {
            return kind == ContainerKind.Hand || kind == ContainerKind.ConsoleSlot ? seatId : SeatId.Empty;
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

            public FailureFixture(
                ReorderFixture fixture,
                ReorderContainerCommand command,
                bool useDefaultCommand = false)
            {
                Fixture = fixture;
                Command = command;
                this.useDefaultCommand = useDefaultCommand;
            }

            public ReorderFixture Fixture { get; }

            private ReorderContainerCommand Command { get; }

            public ReorderContainerResult Execute()
            {
                ReorderContainerUseCase useCase = new ReorderContainerUseCase();
                ReorderContainerCommand command = useDefaultCommand
                    ? Command ?? CreateCommand(Fixture, Fixture.Cards[0].BaseState.Id, 0, 1)
                    : Command;
                return useCase.Execute(Fixture.Match, command);
            }
        }

        private sealed class ReorderFixture
        {
            public ReorderFixture(
                MatchState match,
                ContainerState container,
                ContainerState otherContainer,
                ContainerPlacementState placement,
                SeatState seat,
                CardInstanceState[] cards,
                CardInstanceState otherCard)
            {
                Match = match;
                Container = container;
                OtherContainer = otherContainer;
                Placement = placement;
                Seat = seat;
                Cards = cards;
                OtherCard = otherCard;
            }

            public MatchState Match { get; }

            public ContainerState Container { get; }

            public ContainerState OtherContainer { get; }

            public ContainerPlacementState Placement { get; }

            public SeatState Seat { get; }

            public CardInstanceState[] Cards { get; }

            public CardInstanceState OtherCard { get; }
        }

        private sealed class AggregateSnapshot
        {
            private readonly IReadOnlyDictionary<ContainerId, TabletopObjectId[]> containerOrders;
            private readonly IReadOnlyDictionary<TabletopObjectId, ObjectSnapshot> objectSnapshots;
            private readonly IReadOnlyDictionary<TabletopObjectId, CardFace> cardFaces;
            private readonly IReadOnlyDictionary<ContainerId, TabletopPose> placementPoses;
            private readonly IReadOnlyDictionary<SeatId, SeatState> seats;
            private readonly long revision;

            private AggregateSnapshot(
                long revision,
                IReadOnlyDictionary<ContainerId, TabletopObjectId[]> containerOrders,
                IReadOnlyDictionary<TabletopObjectId, ObjectSnapshot> objectSnapshots,
                IReadOnlyDictionary<TabletopObjectId, CardFace> cardFaces,
                IReadOnlyDictionary<ContainerId, TabletopPose> placementPoses,
                IReadOnlyDictionary<SeatId, SeatState> seats)
            {
                this.revision = revision;
                this.containerOrders = containerOrders;
                this.objectSnapshots = objectSnapshots;
                this.cardFaces = cardFaces;
                this.placementPoses = placementPoses;
                this.seats = seats;
            }

            public static AggregateSnapshot Capture(MatchState match)
            {
                Dictionary<TabletopObjectId, ObjectSnapshot> objectSnapshots =
                    new Dictionary<TabletopObjectId, ObjectSnapshot>();

                foreach (CardInstanceState card in match.Cards.Values)
                {
                    objectSnapshots.Add(card.BaseState.Id, ObjectSnapshot.Capture(card.BaseState));
                }

                foreach (PawnState pawn in match.Pawns.Values)
                {
                    objectSnapshots.Add(pawn.BaseState.Id, ObjectSnapshot.Capture(pawn.BaseState));
                }

                foreach (TokenState token in match.Tokens.Values)
                {
                    objectSnapshots.Add(token.BaseState.Id, ObjectSnapshot.Capture(token.BaseState));
                }

                return new AggregateSnapshot(
                    match.Revision,
                    match.Containers.ToDictionary(pair => pair.Key, pair => pair.Value.ObjectIds.ToArray()),
                    objectSnapshots,
                    match.Cards.ToDictionary(pair => pair.Key, pair => pair.Value.Face),
                    match.ContainerPlacements.ToDictionary(pair => pair.Key, pair => pair.Value.Pose),
                    match.Seats.ToDictionary(pair => pair.Key, pair => pair.Value));
            }

            public TabletopObjectId[] ContainerOrder(ContainerId containerId)
            {
                return containerOrders[containerId];
            }

            public void AssertMatches(MatchState match)
            {
                Assert.That(match.Revision, Is.EqualTo(revision));

                foreach (KeyValuePair<ContainerId, TabletopObjectId[]> pair in containerOrders)
                {
                    Assert.That(match.Containers[pair.Key].ObjectIds, Is.EqualTo(pair.Value));
                }

                AssertNonOrderStateMatches(match);
            }

            public void AssertNonOrderStateMatches(MatchState match)
            {
                foreach (KeyValuePair<TabletopObjectId, ObjectSnapshot> pair in objectSnapshots)
                {
                    pair.Value.AssertMatches(match.GetObject(pair.Key));
                }

                foreach (KeyValuePair<TabletopObjectId, CardFace> pair in cardFaces)
                {
                    Assert.That(match.Cards[pair.Key].Face, Is.EqualTo(pair.Value));
                }

                foreach (KeyValuePair<ContainerId, TabletopPose> pair in placementPoses)
                {
                    Assert.That(match.ContainerPlacements[pair.Key].Pose, Is.EqualTo(pair.Value));
                }

                foreach (KeyValuePair<SeatId, SeatState> pair in seats)
                {
                    Assert.That(match.Seats[pair.Key], Is.SameAs(pair.Value));
                }
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

    internal static class ContainerStateReorderTestExtensions
    {
        public static void RemoveObjectForTest(this ContainerState container, TabletopObjectId objectId)
        {
            ContainerTransferService transferService = new ContainerTransferService();
            TabletopObjectState objectState = new TabletopObjectState(
                objectId,
                ObjectDefinitionId.New(),
                TabletopObjectKind.Card,
                TabletopPose.Default,
                container.Id,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);

            transferService.RemoveFromContainer(objectState, container);
        }
    }
}
