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
    public sealed class ShuffleDeckUseCaseTests
    {
        public enum ShuffleFailureScenario
        {
            NullCommand,
            MatchMismatch,
            RevisionConflict,
            ContainerMissing,
            GenericContainer,
            StackContainer,
            HandContainer,
            DiscardPileContainer,
            ConsoleSlotContainer,
            RevisionOverflow
        }

        [Test]
        public void Execute_WhenMatchStateIsNull_ReturnsInvalidMatchMissing()
        {
            ShuffleDeckUseCase useCase = new ShuffleDeckUseCase();

            ShuffleDeckResult result = useCase.Execute(null, CreateCommand(MatchId.New(), ContainerId.New()));

            AssertFailure(result, CommandResultStatus.Invalid, ShuffleDeckError.MatchMissing);
        }

        [Test]
        public void Execute_WhenCommandIsNull_ReturnsInvalidCommandMissing()
        {
            ShuffleFixture fixture = CreateFixture();
            ShuffleDeckUseCase useCase = new ShuffleDeckUseCase();

            ShuffleDeckResult result = useCase.Execute(fixture.Match, null);

            AssertFailure(result, CommandResultStatus.Invalid, ShuffleDeckError.CommandMissing);
        }

        [Test]
        public void Execute_WhenMatchIdMismatches_ReturnsInvalidMatchMismatch()
        {
            ShuffleFixture fixture = CreateFixture();

            ShuffleDeckResult result = Execute(
                fixture,
                CreateCommand(MatchId.New(), fixture.TargetContainer.Id));

            AssertFailure(result, CommandResultStatus.Invalid, ShuffleDeckError.MatchMismatch);
        }

        [Test]
        public void Execute_WhenExpectedRevisionMismatches_ReturnsConflictRevisionConflict()
        {
            ShuffleFixture fixture = CreateFixture(revision: 4);

            ShuffleDeckResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, fixture.TargetContainer.Id, expectedRevision: 5));

            AssertFailure(result, CommandResultStatus.Conflict, ShuffleDeckError.RevisionConflict);
        }

        [Test]
        public void Execute_WhenContainerIsMissing_ReturnsRejectedContainerMissing()
        {
            ShuffleFixture fixture = CreateFixture();

            ShuffleDeckResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, ContainerId.New()));

            AssertFailure(result, CommandResultStatus.Rejected, ShuffleDeckError.ContainerMissing);
        }

        [TestCase(ContainerKind.Generic)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.ConsoleSlot)]
        public void Execute_WhenContainerIsNotDeck_ReturnsRejectedContainerNotDeck(ContainerKind kind)
        {
            ShuffleFixture fixture = CreateFixture(targetKind: kind);

            ShuffleDeckResult result = Execute(fixture);

            AssertFailure(result, CommandResultStatus.Rejected, ShuffleDeckError.ContainerNotDeck);
        }

        [Test]
        public void Execute_WhenRevisionIsLongMaxValue_ReturnsConflictRevisionOverflow()
        {
            ShuffleFixture fixture = CreateFixture(revision: long.MaxValue);

            ShuffleDeckResult result = Execute(fixture);

            AssertFailure(result, CommandResultStatus.Conflict, ShuffleDeckError.RevisionOverflow);
        }

        [TestCase(ShuffleFailureScenario.NullCommand)]
        [TestCase(ShuffleFailureScenario.MatchMismatch)]
        [TestCase(ShuffleFailureScenario.RevisionConflict)]
        [TestCase(ShuffleFailureScenario.ContainerMissing)]
        [TestCase(ShuffleFailureScenario.GenericContainer)]
        [TestCase(ShuffleFailureScenario.StackContainer)]
        [TestCase(ShuffleFailureScenario.HandContainer)]
        [TestCase(ShuffleFailureScenario.DiscardPileContainer)]
        [TestCase(ShuffleFailureScenario.ConsoleSlotContainer)]
        [TestCase(ShuffleFailureScenario.RevisionOverflow)]
        public void Execute_WhenFailureOccurs_PreservesAggregateState(ShuffleFailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            AggregateSnapshot before = AggregateSnapshot.Capture(failure.Fixture);

            ShuffleDeckResult result = failure.Execute();

            Assert.That(result.Succeeded, Is.False);
            before.AssertMatches(failure.Fixture);
        }

        [Test]
        public void Execute_WithSameOrderAndSameSeed_ProducesSamePermutationAcrossMatches()
        {
            ShuffleFixture first = CreateFixture();
            ShuffleFixture second = CreateFixture();

            int[] firstPermutation = ExecuteAndCapturePermutation(first, 77);
            int[] secondPermutation = ExecuteAndCapturePermutation(second, 77);

            Assert.That(secondPermutation, Is.EqualTo(firstPermutation));
        }

        [Test]
        public void Execute_WhenSameUseCaseInstanceIsReused_RemainsStateless()
        {
            ShuffleDeckUseCase useCase = new ShuffleDeckUseCase();
            ShuffleFixture first = CreateFixture();
            ShuffleFixture second = CreateFixture();

            int[] firstPermutation = ExecuteAndCapturePermutation(first, 91, useCase);
            int[] secondPermutation = ExecuteAndCapturePermutation(second, 91, useCase);

            Assert.That(secondPermutation, Is.EqualTo(firstPermutation));
        }

        [Test]
        public void Execute_WhenNewUseCaseInstancesAreUsed_ProducesSamePermutation()
        {
            ShuffleFixture first = CreateFixture();
            ShuffleFixture second = CreateFixture();

            int[] firstPermutation = ExecuteAndCapturePermutation(first, 33, new ShuffleDeckUseCase());
            int[] secondPermutation = ExecuteAndCapturePermutation(second, 33, new ShuffleDeckUseCase());

            Assert.That(secondPermutation, Is.EqualTo(firstPermutation));
        }

        [TestCase(0)]
        [TestCase(-19)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void Execute_WithBoundarySeed_IsDeterministic(int seed)
        {
            ShuffleFixture first = CreateFixture();
            ShuffleFixture second = CreateFixture();

            int[] firstPermutation = ExecuteAndCapturePermutation(first, seed);
            int[] secondPermutation = ExecuteAndCapturePermutation(second, seed);

            Assert.That(secondPermutation, Is.EqualTo(firstPermutation));
        }

        [Test]
        public void Execute_WithKnownOrderAndSeed_ProducesDocumentedPermutation()
        {
            ShuffleFixture fixture = CreateFixture(deckCount: 5);
            TabletopObjectId[] originalOrder = fixture.TargetContainer.ObjectIds.ToArray();

            Execute(fixture, seed: 123);

            Assert.That(
                fixture.TargetContainer.ObjectIds,
                Is.EqualTo(new[]
                {
                    originalOrder[4],
                    originalOrder[3],
                    originalOrder[0],
                    originalOrder[2],
                    originalOrder[1]
                }));
        }

        [Test]
        public void Execute_FromSameOriginalOrderAndSeed_ReproducesExactPermutation()
        {
            ShuffleFixture first = CreateFixture();
            ShuffleFixture second = CreateFixture();

            int[] firstPermutation = ExecuteAndCapturePermutation(first, 123);
            int[] secondPermutation = ExecuteAndCapturePermutation(second, 123);

            Assert.That(secondPermutation, Is.EqualTo(firstPermutation));
        }

        [Test]
        public void Execute_DifferentSeedsProduceAtLeastTwoDifferentPermutations()
        {
            HashSet<string> permutations = new HashSet<string>();

            for (int seed = -5; seed <= 5; seed++)
            {
                ShuffleFixture fixture = CreateFixture();
                permutations.Add(string.Join(",", ExecuteAndCapturePermutation(fixture, seed)));
            }

            Assert.That(permutations.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesAllIdsExactlyOnce()
        {
            ShuffleFixture fixture = CreateFixture();
            TabletopObjectId[] originalOrder = fixture.TargetContainer.ObjectIds.ToArray();

            Execute(fixture, seed: 321);

            Assert.That(fixture.TargetContainer.ObjectIds, Is.EquivalentTo(originalOrder));
            Assert.That(fixture.TargetContainer.ObjectIds, Is.Unique);
        }

        [Test]
        public void Execute_WhenDeckIsEmpty_AcceptsAndAdvancesRevisionOnce()
        {
            ShuffleFixture fixture = CreateFixture(deckCount: 0, revision: 10);

            ShuffleDeckResult result = Execute(fixture, seed: 7);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.TargetContainer.ObjectIds, Is.Empty);
            Assert.That(fixture.Match.Revision, Is.EqualTo(11));
            Assert.That(result.Revision, Is.EqualTo(11));
        }

        [Test]
        public void Execute_WhenDeckHasOneCard_AcceptsAndAdvancesRevisionOnce()
        {
            ShuffleFixture fixture = CreateFixture(deckCount: 1, revision: 10);
            TabletopObjectId onlyObjectId = fixture.TargetContainer.ObjectIds[0];

            ShuffleDeckResult result = Execute(fixture, seed: -5);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.TargetContainer.ObjectIds, Is.EqualTo(new[] { onlyObjectId }));
            Assert.That(fixture.Match.Revision, Is.EqualTo(11));
            Assert.That(result.Revision, Is.EqualTo(11));
        }

        [Test]
        public void Execute_WhenDeckHasMultipleCards_AcceptsAndAdvancesRevisionOnce()
        {
            ShuffleFixture fixture = CreateFixture(revision: 10);

            ShuffleDeckResult result = Execute(fixture, seed: 123);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Match.Revision, Is.EqualTo(11));
            Assert.That(result.Revision, Is.EqualTo(11));
        }

        [Test]
        public void Execute_WhenPermutationEqualsCurrentOrder_StillAcceptsAndAdvancesRevisionOnce()
        {
            ShuffleFixture fixture = CreateFixture(deckCount: 2, revision: 15);
            TabletopObjectId[] originalOrder = fixture.TargetContainer.ObjectIds.ToArray();

            ShuffleDeckResult result = Execute(fixture, seed: 0);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.TargetContainer.ObjectIds, Is.EqualTo(originalOrder));
            Assert.That(fixture.Match.Revision, Is.EqualTo(16));
            Assert.That(result.Revision, Is.EqualTo(16));
        }

        [Test]
        public void Execute_WhenSuccessful_OnlyTargetDeckOrderChanges()
        {
            ShuffleFixture fixture = CreateFixture();
            TabletopObjectId[] originalDeckOrder = fixture.TargetContainer.ObjectIds.ToArray();
            TabletopObjectId[] originalOtherOrder = fixture.OtherContainer.ObjectIds.ToArray();

            Execute(fixture, seed: 123);

            Assert.That(fixture.TargetContainer.ObjectIds, Is.Not.EqualTo(originalDeckOrder));
            Assert.That(fixture.OtherContainer.ObjectIds, Is.EqualTo(originalOtherOrder));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesPlacementObjectStatesSeatsAndConsoles()
        {
            ShuffleFixture fixture = CreateFixture();
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture);

            ShuffleDeckResult result = Execute(fixture, seed: 123);

            Assert.That(result.Succeeded, Is.True);
            before.AssertMatchesExceptTargetOrderAndRevision(fixture);
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesMatchAndContainerIdentity()
        {
            ShuffleFixture fixture = CreateFixture();
            MatchState match = fixture.Match;
            ContainerState deck = fixture.TargetContainer;

            Execute(fixture, seed: 123);

            Assert.That(fixture.Match, Is.SameAs(match));
            Assert.That(fixture.TargetContainer, Is.SameAs(deck));
            Assert.That(fixture.Match.Containers[deck.Id], Is.SameAs(deck));
        }

        [Test]
        public void StaticBoundary_UsesApplicationCoreOnlyAndNoBroadRandomService()
        {
            Assembly applicationAssembly = typeof(ShuffleDeckUseCase).Assembly;
            string[] referencedAssemblyNames = applicationAssembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();
            Type stableRandomType = applicationAssembly.GetType("ConsoleCards.Application.Random.StableShuffleRandom");

            Assert.That(referencedAssemblyNames, Does.Contain("ConsoleCards.Core"));
            Assert.That(referencedAssemblyNames.Any(name => name.StartsWith("UnityEngine", StringComparison.Ordinal)), Is.False);
            Assert.That(referencedAssemblyNames, Does.Not.Contain("ConsoleCards.Presentation"));
            Assert.That(stableRandomType, Is.Not.Null);
            Assert.That(stableRandomType.IsPublic, Is.False);
            Assert.That(
                stableRandomType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(field => !field.IsLiteral)
                    .ToArray(),
                Is.Empty);
        }

        private static ShuffleDeckResult Execute(
            ShuffleFixture fixture,
            ShuffleDeckCommand command = null,
            int seed = 123)
        {
            ShuffleDeckCommand actualCommand = command
                ?? CreateCommand(fixture.Match.Id, fixture.TargetContainer.Id, seed, fixture.Match.Revision);
            ShuffleDeckUseCase useCase = new ShuffleDeckUseCase();
            return useCase.Execute(fixture.Match, actualCommand);
        }

        private static int[] ExecuteAndCapturePermutation(
            ShuffleFixture fixture,
            int seed,
            ShuffleDeckUseCase useCase = null)
        {
            TabletopObjectId[] originalOrder = fixture.TargetContainer.ObjectIds.ToArray();
            ShuffleDeckCommand command = CreateCommand(fixture.Match.Id, fixture.TargetContainer.Id, seed, fixture.Match.Revision);
            ShuffleDeckResult result = (useCase ?? new ShuffleDeckUseCase()).Execute(fixture.Match, command);

            Assert.That(result.Succeeded, Is.True);
            return fixture.TargetContainer.ObjectIds
                .Select(objectId => Array.IndexOf(originalOrder, objectId))
                .ToArray();
        }

        private static void AssertFailure(
            ShuffleDeckResult result,
            CommandResultStatus expectedStatus,
            ShuffleDeckError expectedError)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.Error, Is.EqualTo(expectedError));
            Assert.That(result.Revision, Is.EqualTo(-1));
        }

        private static FailureFixture CreateFailureFixture(ShuffleFailureScenario scenario)
        {
            switch (scenario)
            {
                case ShuffleFailureScenario.NullCommand:
                {
                    ShuffleFixture fixture = CreateFixture();
                    return new FailureFixture(fixture, null);
                }

                case ShuffleFailureScenario.MatchMismatch:
                {
                    ShuffleFixture fixture = CreateFixture();
                    ShuffleDeckCommand command = CreateCommand(MatchId.New(), fixture.TargetContainer.Id);
                    return new FailureFixture(fixture, command);
                }

                case ShuffleFailureScenario.RevisionConflict:
                {
                    ShuffleFixture fixture = CreateFixture(revision: 3);
                    ShuffleDeckCommand command = CreateCommand(
                        fixture.Match.Id,
                        fixture.TargetContainer.Id,
                        expectedRevision: 4);
                    return new FailureFixture(fixture, command);
                }

                case ShuffleFailureScenario.ContainerMissing:
                {
                    ShuffleFixture fixture = CreateFixture();
                    ShuffleDeckCommand command = CreateCommand(fixture.Match.Id, ContainerId.New());
                    return new FailureFixture(fixture, command);
                }

                case ShuffleFailureScenario.GenericContainer:
                    return CreateNonDeckFailureFixture(ContainerKind.Generic);

                case ShuffleFailureScenario.StackContainer:
                    return CreateNonDeckFailureFixture(ContainerKind.Stack);

                case ShuffleFailureScenario.HandContainer:
                    return CreateNonDeckFailureFixture(ContainerKind.Hand);

                case ShuffleFailureScenario.DiscardPileContainer:
                    return CreateNonDeckFailureFixture(ContainerKind.DiscardPile);

                case ShuffleFailureScenario.ConsoleSlotContainer:
                    return CreateNonDeckFailureFixture(ContainerKind.ConsoleSlot);

                case ShuffleFailureScenario.RevisionOverflow:
                {
                    ShuffleFixture fixture = CreateFixture(revision: long.MaxValue);
                    ShuffleDeckCommand command = CreateCommand(
                        fixture.Match.Id,
                        fixture.TargetContainer.Id,
                        expectedRevision: long.MaxValue);
                    return new FailureFixture(fixture, command);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported shuffle failure scenario.");
            }
        }

        private static FailureFixture CreateNonDeckFailureFixture(ContainerKind kind)
        {
            ShuffleFixture fixture = CreateFixture(targetKind: kind);
            return new FailureFixture(fixture, CreateCommand(fixture.Match.Id, fixture.TargetContainer.Id));
        }

        private static ShuffleDeckCommand CreateCommand(
            MatchId matchId,
            ContainerId deckContainerId,
            int seed = 123,
            long? expectedRevision = 0)
        {
            CommandContext context = new CommandContext(
                CommandId.New(),
                matchId,
                PlayerId.New(),
                expectedRevision);

            return new ShuffleDeckCommand(context, deckContainerId, seed);
        }

        private static ShuffleFixture CreateFixture(
            ContainerKind targetKind = ContainerKind.Deck,
            int deckCount = 5,
            long revision = 0)
        {
            ContainerTransferService transferService = new ContainerTransferService();
            ContainerState targetContainer = CreateContainer(kind: targetKind);
            List<CardInstanceState> deckCards = new List<CardInstanceState>();

            for (int index = 0; index < deckCount; index++)
            {
                CardInstanceState card = CreateCard(face: index % 2 == 0 ? CardFace.FaceDown : CardFace.FaceUp);
                transferService.PlaceIntoContainer(card.BaseState, targetContainer);
                deckCards.Add(card);
            }

            ContainerState otherContainer = CreateContainer(kind: ContainerKind.Generic);
            CardInstanceState otherCard = CreateCard(face: CardFace.FaceUp);
            transferService.PlaceIntoContainer(otherCard.BaseState, otherContainer);

            SeatId seatId = SeatId.New();
            ContainerState handContainer = CreateContainer(kind: ContainerKind.Hand, ownerSeatId: seatId);
            ContainerState slotContainer = CreateContainer(kind: ContainerKind.ConsoleSlot, ownerSeatId: seatId);
            SeatState seat = new SeatState(
                seatId,
                CreatePose(x: -4.0, y: 4.0),
                handContainer.Id,
                new ConsoleState(seatId, new[] { slotContainer.Id }),
                PlayerId.Empty,
                SeatStatus.Vacant);

            List<ContainerState> containers = new List<ContainerState>
            {
                targetContainer,
                otherContainer,
                handContainer,
                slotContainer
            };

            ContainerState placementContainer = targetContainer;
            if (targetKind != ContainerKind.Deck)
            {
                placementContainer = CreateContainer(kind: ContainerKind.Deck);
                containers.Add(placementContainer);
            }

            ContainerPlacementState placement = new ContainerPlacementState(
                placementContainer.Id,
                CreatePose(x: 2.5, y: -1.5, rotationDegrees: 45f, layer: 1, localOrder: 9));

            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                deckCards.Concat(new[] { otherCard }).ToArray(),
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                containers,
                new[] { seat },
                new[] { placement });

            return new ShuffleFixture(
                match,
                targetContainer,
                otherContainer,
                placement,
                seat,
                deckCards.ToArray(),
                otherCard);
        }

        private static ContainerState CreateContainer(
            ContainerKind kind,
            SeatId? ownerSeatId = null)
        {
            return new ContainerState(
                ContainerId.New(),
                kind,
                ownerSeatId ?? SeatId.Empty,
                ObjectVisibility.Public,
                0);
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

        private sealed class ShuffleFixture
        {
            public ShuffleFixture(
                MatchState match,
                ContainerState targetContainer,
                ContainerState otherContainer,
                ContainerPlacementState placement,
                SeatState seat,
                CardInstanceState[] deckCards,
                CardInstanceState otherCard)
            {
                Match = match;
                TargetContainer = targetContainer;
                OtherContainer = otherContainer;
                Placement = placement;
                Seat = seat;
                DeckCards = deckCards;
                OtherCard = otherCard;
            }

            public MatchState Match { get; }

            public ContainerState TargetContainer { get; }

            public ContainerState OtherContainer { get; }

            public ContainerPlacementState Placement { get; }

            public SeatState Seat { get; }

            public CardInstanceState[] DeckCards { get; }

            public CardInstanceState OtherCard { get; }
        }

        private sealed class FailureFixture
        {
            public FailureFixture(ShuffleFixture fixture, ShuffleDeckCommand command)
            {
                Fixture = fixture;
                Command = command;
            }

            public ShuffleFixture Fixture { get; }

            public ShuffleDeckCommand Command { get; }

            public ShuffleDeckResult Execute()
            {
                ShuffleDeckUseCase useCase = new ShuffleDeckUseCase();
                return useCase.Execute(Fixture.Match, Command);
            }
        }

        private sealed class AggregateSnapshot
        {
            private AggregateSnapshot(
                long revision,
                TabletopObjectId[] targetOrder,
                TabletopObjectId[] otherOrder,
                TabletopPose placementPose,
                ContainerPlacementState placement,
                SeatState seat,
                CardSnapshot[] cards)
            {
                Revision = revision;
                TargetOrder = targetOrder;
                OtherOrder = otherOrder;
                PlacementPose = placementPose;
                Placement = placement;
                Seat = seat;
                Cards = cards;
            }

            private long Revision { get; }

            private TabletopObjectId[] TargetOrder { get; }

            private TabletopObjectId[] OtherOrder { get; }

            private TabletopPose PlacementPose { get; }

            private ContainerPlacementState Placement { get; }

            private SeatState Seat { get; }

            private CardSnapshot[] Cards { get; }

            public static AggregateSnapshot Capture(ShuffleFixture fixture)
            {
                return new AggregateSnapshot(
                    fixture.Match.Revision,
                    fixture.TargetContainer.ObjectIds.ToArray(),
                    fixture.OtherContainer.ObjectIds.ToArray(),
                    fixture.Placement.Pose,
                    fixture.Placement,
                    fixture.Seat,
                    fixture.DeckCards.Concat(new[] { fixture.OtherCard }).Select(CardSnapshot.Capture).ToArray());
            }

            public void AssertMatches(ShuffleFixture fixture)
            {
                Assert.That(fixture.Match.Revision, Is.EqualTo(Revision));
                Assert.That(fixture.TargetContainer.ObjectIds, Is.EqualTo(TargetOrder));
                Assert.That(fixture.OtherContainer.ObjectIds, Is.EqualTo(OtherOrder));
                AssertCommonState(fixture);
            }

            public void AssertMatchesExceptTargetOrderAndRevision(ShuffleFixture fixture)
            {
                Assert.That(fixture.Match.Revision, Is.EqualTo(Revision + 1));
                Assert.That(fixture.OtherContainer.ObjectIds, Is.EqualTo(OtherOrder));
                AssertCommonState(fixture);
            }

            private void AssertCommonState(ShuffleFixture fixture)
            {
                Assert.That(fixture.Match.ContainerPlacements[Placement.ContainerId], Is.SameAs(Placement));
                Assert.That(fixture.Placement.Pose, Is.EqualTo(PlacementPose));
                Assert.That(fixture.Match.Seats[Seat.Id], Is.SameAs(Seat));
                Assert.That(fixture.Seat.Console.OwnerSeatId, Is.EqualTo(Seat.Id));

                foreach (CardSnapshot card in Cards)
                {
                    card.AssertMatches(fixture.Match.Cards[card.Id]);
                }
            }
        }

        private sealed class CardSnapshot
        {
            private CardSnapshot(
                TabletopObjectId id,
                ObjectDefinitionId definitionId,
                TabletopPose pose,
                ContainerId containerId,
                PlayerId ownerPlayerId,
                ObjectVisibility visibility,
                bool isUserLocked,
                CardFace face)
            {
                Id = id;
                DefinitionId = definitionId;
                Pose = pose;
                ContainerId = containerId;
                OwnerPlayerId = ownerPlayerId;
                Visibility = visibility;
                IsUserLocked = isUserLocked;
                Face = face;
            }

            public TabletopObjectId Id { get; }

            private ObjectDefinitionId DefinitionId { get; }

            private TabletopPose Pose { get; }

            private ContainerId ContainerId { get; }

            private PlayerId OwnerPlayerId { get; }

            private ObjectVisibility Visibility { get; }

            private bool IsUserLocked { get; }

            private CardFace Face { get; }

            public static CardSnapshot Capture(CardInstanceState card)
            {
                TabletopObjectState baseState = card.BaseState;
                return new CardSnapshot(
                    baseState.Id,
                    baseState.DefinitionId,
                    baseState.Pose,
                    baseState.ContainerId,
                    baseState.OwnerPlayerId,
                    baseState.Visibility,
                    baseState.IsUserLocked,
                    card.Face);
            }

            public void AssertMatches(CardInstanceState card)
            {
                Assert.That(card.BaseState.Id, Is.EqualTo(Id));
                Assert.That(card.BaseState.DefinitionId, Is.EqualTo(DefinitionId));
                Assert.That(card.BaseState.Pose, Is.EqualTo(Pose));
                Assert.That(card.BaseState.ContainerId, Is.EqualTo(ContainerId));
                Assert.That(card.BaseState.OwnerPlayerId, Is.EqualTo(OwnerPlayerId));
                Assert.That(card.BaseState.Visibility, Is.EqualTo(Visibility));
                Assert.That(card.BaseState.IsUserLocked, Is.EqualTo(IsUserLocked));
                Assert.That(card.Face, Is.EqualTo(Face));
            }
        }
    }
}
