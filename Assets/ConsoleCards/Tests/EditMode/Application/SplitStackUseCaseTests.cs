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
    public sealed class SplitStackUseCaseTests
    {
        public enum FailureScenario
        {
            NullCommand,
            MatchMismatch,
            RevisionConflict,
            SameStack,
            SourceMissing,
            SourceGeneric,
            SourceDeck,
            SourceHand,
            SourceDiscardPile,
            SourceConsoleSlot,
            SourceCountZero,
            SourceCountOne,
            FirstMovedIndexEqualToCount,
            FirstMovedIndexGreaterThanCount,
            NewStackAlreadyExists,
            MissingMovedMemberObject,
            MovedMemberContainerMismatch,
            MovedMemberUserLocked,
            RevisionOverflow
        }

        [Test]
        public void Execute_WhenMatchStateIsNull_ReturnsInvalidMatchMissing()
        {
            SplitStackUseCase useCase = new SplitStackUseCase();

            SplitStackResult result = useCase.Execute(
                null,
                new SplitStackCommand(
                    CreateContext(MatchId.New()),
                    ContainerId.New(),
                    ContainerId.New(),
                    new StackSplitSpecification(1),
                    CreatePose()));

            AssertFailure(result, CommandResultStatus.Invalid, SplitStackError.MatchMissing);
        }

        [TestCase(FailureScenario.NullCommand, CommandResultStatus.Invalid, SplitStackError.CommandMissing)]
        [TestCase(FailureScenario.MatchMismatch, CommandResultStatus.Invalid, SplitStackError.MatchMismatch)]
        [TestCase(FailureScenario.RevisionConflict, CommandResultStatus.Conflict, SplitStackError.RevisionConflict)]
        [TestCase(FailureScenario.SameStack, CommandResultStatus.Invalid, SplitStackError.SameStack)]
        [TestCase(FailureScenario.SourceMissing, CommandResultStatus.Rejected, SplitStackError.SourceStackMissing)]
        [TestCase(FailureScenario.SourceGeneric, CommandResultStatus.Rejected, SplitStackError.SourceContainerNotStack)]
        [TestCase(FailureScenario.SourceDeck, CommandResultStatus.Rejected, SplitStackError.SourceContainerNotStack)]
        [TestCase(FailureScenario.SourceHand, CommandResultStatus.Rejected, SplitStackError.SourceContainerNotStack)]
        [TestCase(FailureScenario.SourceDiscardPile, CommandResultStatus.Rejected, SplitStackError.SourceContainerNotStack)]
        [TestCase(FailureScenario.SourceConsoleSlot, CommandResultStatus.Rejected, SplitStackError.SourceContainerNotStack)]
        [TestCase(FailureScenario.SourceCountZero, CommandResultStatus.Rejected, SplitStackError.SourceStackTooSmall)]
        [TestCase(FailureScenario.SourceCountOne, CommandResultStatus.Rejected, SplitStackError.SourceStackTooSmall)]
        [TestCase(FailureScenario.FirstMovedIndexEqualToCount, CommandResultStatus.Invalid, SplitStackError.InvalidSplitIndex)]
        [TestCase(FailureScenario.FirstMovedIndexGreaterThanCount, CommandResultStatus.Invalid, SplitStackError.InvalidSplitIndex)]
        [TestCase(FailureScenario.NewStackAlreadyExists, CommandResultStatus.Rejected, SplitStackError.NewStackAlreadyExists)]
        [TestCase(FailureScenario.MissingMovedMemberObject, CommandResultStatus.Rejected, SplitStackError.ObjectMissing)]
        [TestCase(FailureScenario.MovedMemberContainerMismatch, CommandResultStatus.Rejected, SplitStackError.ObjectContainerMismatch)]
        [TestCase(FailureScenario.MovedMemberUserLocked, CommandResultStatus.Rejected, SplitStackError.ObjectUserLocked)]
        [TestCase(FailureScenario.RevisionOverflow, CommandResultStatus.Conflict, SplitStackError.RevisionOverflow)]
        public void Execute_WhenValidationFails_ReturnsExpectedFailure(
            FailureScenario scenario,
            CommandResultStatus expectedStatus,
            SplitStackError expectedError)
        {
            FailureFixture failure = CreateFailureFixture(scenario);

            SplitStackResult result = failure.Execute();

            AssertFailure(result, expectedStatus, expectedError);
        }

        [TestCase(FailureScenario.NullCommand)]
        [TestCase(FailureScenario.MatchMismatch)]
        [TestCase(FailureScenario.RevisionConflict)]
        [TestCase(FailureScenario.SourceMissing)]
        [TestCase(FailureScenario.SourceGeneric)]
        [TestCase(FailureScenario.SourceDeck)]
        [TestCase(FailureScenario.SourceHand)]
        [TestCase(FailureScenario.SourceDiscardPile)]
        [TestCase(FailureScenario.SourceConsoleSlot)]
        [TestCase(FailureScenario.SourceCountZero)]
        [TestCase(FailureScenario.SourceCountOne)]
        [TestCase(FailureScenario.FirstMovedIndexEqualToCount)]
        [TestCase(FailureScenario.FirstMovedIndexGreaterThanCount)]
        [TestCase(FailureScenario.NewStackAlreadyExists)]
        [TestCase(FailureScenario.MissingMovedMemberObject)]
        [TestCase(FailureScenario.MovedMemberContainerMismatch)]
        [TestCase(FailureScenario.MovedMemberUserLocked)]
        [TestCase(FailureScenario.RevisionOverflow)]
        public void Execute_WhenValidationFails_PreservesAggregateState(FailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            AggregateSnapshot before = AggregateSnapshot.Capture(failure.Fixture);

            SplitStackResult result = failure.Execute();

            Assert.That(result.Succeeded, Is.False);
            before.AssertMatches(failure.Fixture);
        }

        [TestCase(1, "A", "B,C,D")]
        [TestCase(2, "A,B", "C,D")]
        [TestCase(3, "A,B,C", "D")]
        public void Execute_WithFourCardStack_UsesFirstMovedIndexAsUpperRangeStart(
            int firstMovedIndex,
            string expectedSourceLabels,
            string expectedNewStackLabels)
        {
            SplitFixture fixture = CreateFixture(sourceCount: 4, revision: 5);
            SplitStackCommand command = CreateCommand(fixture, firstMovedIndex);

            SplitStackResult result = Execute(fixture, command);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Source.ObjectIds, Is.EqualTo(IdsForLabels(fixture, expectedSourceLabels)));
            Assert.That(fixture.Match.Containers[fixture.NewStackId].ObjectIds, Is.EqualTo(IdsForLabels(fixture, expectedNewStackLabels)));
            Assert.That(fixture.Match.Revision, Is.EqualTo(6));
        }

        [Test]
        public void Execute_WithTwoCardStack_SplitsIntoOneAndOne()
        {
            SplitFixture fixture = CreateFixture(sourceCount: 2);

            SplitStackResult result = Execute(fixture, CreateCommand(fixture, firstMovedIndex: 1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Source.ObjectIds, Is.EqualTo(IdsForLabels(fixture, "A")));
            Assert.That(fixture.Match.Containers[fixture.NewStackId].ObjectIds, Is.EqualTo(IdsForLabels(fixture, "B")));
        }

        [Test]
        public void Execute_WhenSuccessful_InheritsStackMetadataAndCreatesPlacement()
        {
            SeatId sourceOwner = SeatId.New();
            TabletopPose newPose = CreatePose(x: 9.0, y: -4.0, rotationDegrees: -450f, layer: 2, localOrder: 7);
            SplitFixture fixture = CreateFixture(
                sourceCount: 3,
                sourceOwnerSeatId: sourceOwner,
                sourceVisibility: ObjectVisibility.OwnerOnly,
                sourceCapacity: 7,
                newStackPose: newPose);

            SplitStackResult result = Execute(fixture, CreateCommand(fixture, firstMovedIndex: 1, newStackPose: newPose));

            ContainerState newStack = fixture.Match.Containers[fixture.NewStackId];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(newStack.Kind, Is.EqualTo(ContainerKind.Stack));
            Assert.That(newStack.Capacity, Is.EqualTo(7));
            Assert.That(newStack.OwnerSeatId, Is.EqualTo(sourceOwner));
            Assert.That(newStack.Visibility, Is.EqualTo(ObjectVisibility.OwnerOnly));
            Assert.That(fixture.Match.ContainerPlacements[fixture.NewStackId].Pose, Is.EqualTo(newPose));
        }

        [Test]
        public void Execute_WhenSourcePlacementExists_PreservesSourcePlacementInstance()
        {
            SplitFixture fixture = CreateFixture(sourceCount: 3);
            ContainerPlacementState sourcePlacement = fixture.SourcePlacement;

            SplitStackResult result = Execute(fixture, CreateCommand(fixture, firstMovedIndex: 2));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Match.ContainerPlacements[fixture.Source.Id], Is.SameAs(sourcePlacement));
        }

        [Test]
        public void Execute_WhenSourcePlacementIsMissing_StillSplitsWithoutCreatingSourcePlacement()
        {
            SplitFixture fixture = CreateFixture(sourceCount: 3, includeSourcePlacement: false);

            SplitStackResult result = Execute(fixture, CreateCommand(fixture, firstMovedIndex: 1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Match.ContainerPlacements.ContainsKey(fixture.Source.Id), Is.False);
            Assert.That(fixture.Match.ContainerPlacements.ContainsKey(fixture.NewStackId), Is.True);
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesIdentitiesAndObjectNonContainerFields()
        {
            SplitFixture fixture = CreateFixture(sourceCount: 4, revision: 20);
            MatchState match = fixture.Match;
            ContainerState source = fixture.Source;
            ContainerPlacementState sourcePlacement = fixture.SourcePlacement;
            CardInstanceState moved = fixture.SourceCards[2];
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture);

            SplitStackResult result = Execute(fixture, CreateCommand(fixture, firstMovedIndex: 2));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(match, Is.SameAs(fixture.Match));
            Assert.That(match.Containers[source.Id], Is.SameAs(source));
            Assert.That(match.ContainerPlacements[source.Id], Is.SameAs(sourcePlacement));
            Assert.That(match.Cards[moved.BaseState.Id], Is.SameAs(moved));
            before.AssertMatchesAfterSplit(fixture, firstMovedIndex: 2);
        }

        [Test]
        public void Execute_WhenSuccessful_HasNoDuplicateMembershipAndLosesNoMember()
        {
            SplitFixture fixture = CreateFixture(sourceCount: 4);
            TabletopObjectId[] originalIds = fixture.SourceCards.Select(card => card.BaseState.Id).ToArray();

            SplitStackResult result = Execute(fixture, CreateCommand(fixture, firstMovedIndex: 2));

            Assert.That(result.Succeeded, Is.True);
            TabletopObjectId[] combined = fixture.Source.ObjectIds
                .Concat(fixture.Match.Containers[fixture.NewStackId].ObjectIds)
                .ToArray();
            Assert.That(combined, Is.Unique);
            Assert.That(combined, Is.EquivalentTo(originalIds));
        }

        [Test]
        public void Execute_WhenLaterMovedMemberIsInvalid_AddsNoStackAndPerformsNoPartialTransfer()
        {
            SplitFixture fixture = CreateFixture(sourceCount: 3);
            fixture.SourceCards[2].BaseState.SetContainer(fixture.Other.Id);
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture);

            SplitStackResult result = Execute(fixture, CreateCommand(fixture, firstMovedIndex: 1));

            AssertFailure(result, CommandResultStatus.Rejected, SplitStackError.ObjectContainerMismatch);
            before.AssertMatches(fixture);
            Assert.That(fixture.Match.Containers.ContainsKey(fixture.NewStackId), Is.False);
            Assert.That(fixture.Match.ContainerPlacements.ContainsKey(fixture.NewStackId), Is.False);
        }

        [Test]
        public void StaticBoundary_UsesApplicationCoreOnlyAndAddsNoFutureSplitScope()
        {
            Assembly applicationAssembly = typeof(SplitStackUseCase).Assembly;
            string[] referencedAssemblyNames = applicationAssembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.That(referencedAssemblyNames, Does.Contain("ConsoleCards.Core"));
            Assert.That(referencedAssemblyNames.Any(name => name.StartsWith("UnityEngine", StringComparison.Ordinal)), Is.False);
            Assert.That(referencedAssemblyNames, Does.Not.Contain("ConsoleCards.Presentation"));
            Assert.That(applicationAssembly.GetType("ConsoleCards.Application.Commands.SplitStackPreviewCommand"), Is.Null);
            Assert.That(applicationAssembly.GetType("ConsoleCards.Application.UseCases.SplitStackPreviewUseCase"), Is.Null);
        }

        private static SplitStackResult Execute(SplitFixture fixture, SplitStackCommand command)
        {
            return new SplitStackUseCase().Execute(fixture.Match, command);
        }

        private static void AssertFailure(
            SplitStackResult result,
            CommandResultStatus expectedStatus,
            SplitStackError expectedError)
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
                    SplitFixture fixture = CreateFixture();
                    return new FailureFixture(
                        fixture,
                        new SplitStackCommand(
                            CreateContext(MatchId.New()),
                            fixture.Source.Id,
                            fixture.NewStackId,
                            new StackSplitSpecification(1),
                            fixture.NewStackPose));
                }

                case FailureScenario.RevisionConflict:
                {
                    SplitFixture fixture = CreateFixture(revision: 3);
                    return new FailureFixture(fixture, CreateCommand(fixture, firstMovedIndex: 1, expectedRevision: 2));
                }

                case FailureScenario.SameStack:
                {
                    SplitFixture fixture = CreateFixture();
                    return new FailureFixture(
                        fixture,
                        CreateMalformedCommand(
                            CreateContext(fixture.Match.Id),
                            fixture.Source.Id,
                            fixture.Source.Id,
                            new StackSplitSpecification(1),
                            fixture.NewStackPose));
                }

                case FailureScenario.SourceMissing:
                {
                    SplitFixture fixture = CreateFixture();
                    return new FailureFixture(
                        fixture,
                        new SplitStackCommand(
                            CreateContext(fixture.Match.Id),
                            ContainerId.New(),
                            fixture.NewStackId,
                            new StackSplitSpecification(1),
                            fixture.NewStackPose));
                }

                case FailureScenario.SourceGeneric:
                    return CreateSourceKindFailureFixture(ContainerKind.Generic);
                case FailureScenario.SourceDeck:
                    return CreateSourceKindFailureFixture(ContainerKind.Deck);
                case FailureScenario.SourceHand:
                    return CreateSourceKindFailureFixture(ContainerKind.Hand);
                case FailureScenario.SourceDiscardPile:
                    return CreateSourceKindFailureFixture(ContainerKind.DiscardPile);
                case FailureScenario.SourceConsoleSlot:
                    return CreateSourceKindFailureFixture(ContainerKind.ConsoleSlot);

                case FailureScenario.SourceCountZero:
                    return new FailureFixture(CreateFixture(sourceCount: 0), null, useDefaultCommand: true);

                case FailureScenario.SourceCountOne:
                    return new FailureFixture(CreateFixture(sourceCount: 1), null, useDefaultCommand: true);

                case FailureScenario.FirstMovedIndexEqualToCount:
                {
                    SplitFixture fixture = CreateFixture(sourceCount: 3);
                    return new FailureFixture(fixture, CreateCommand(fixture, firstMovedIndex: 3));
                }

                case FailureScenario.FirstMovedIndexGreaterThanCount:
                {
                    SplitFixture fixture = CreateFixture(sourceCount: 3);
                    return new FailureFixture(fixture, CreateCommand(fixture, firstMovedIndex: 4));
                }

                case FailureScenario.NewStackAlreadyExists:
                {
                    SplitFixture fixture = CreateFixture(sourceCount: 3);
                    return new FailureFixture(
                        fixture,
                        new SplitStackCommand(
                            CreateContext(fixture.Match.Id),
                            fixture.Source.Id,
                            fixture.Other.Id,
                            new StackSplitSpecification(1),
                            fixture.NewStackPose));
                }

                case FailureScenario.MissingMovedMemberObject:
                {
                    SplitFixture fixture = CreateFixture(sourceCount: 1);
                    CardInstanceState extraCard = CreateCard();
                    new ContainerTransferService().PlaceIntoContainer(extraCard.BaseState, fixture.Source);
                    fixture.ExtraObjects.Add(extraCard.BaseState);
                    return new FailureFixture(fixture, CreateCommand(fixture, firstMovedIndex: 1));
                }

                case FailureScenario.MovedMemberContainerMismatch:
                {
                    SplitFixture fixture = CreateFixture(sourceCount: 3);
                    fixture.SourceCards[1].BaseState.SetContainer(fixture.Other.Id);
                    return new FailureFixture(fixture, CreateCommand(fixture, firstMovedIndex: 1));
                }

                case FailureScenario.MovedMemberUserLocked:
                    return new FailureFixture(CreateFixture(sourceCount: 3, lockedMovedIndex: 1), null, useDefaultCommand: true);

                case FailureScenario.RevisionOverflow:
                {
                    SplitFixture fixture = CreateFixture(sourceCount: 3, revision: long.MaxValue);
                    return new FailureFixture(
                        fixture,
                        CreateCommand(fixture, firstMovedIndex: 1, expectedRevision: long.MaxValue));
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported split failure scenario.");
            }
        }

        private static FailureFixture CreateSourceKindFailureFixture(ContainerKind sourceKind)
        {
            return new FailureFixture(CreateFixture(sourceKind: sourceKind), null, useDefaultCommand: true);
        }

        private static SplitStackCommand CreateCommand(
            SplitFixture fixture,
            int firstMovedIndex,
            long? expectedRevision = null,
            TabletopPose? newStackPose = null)
        {
            return new SplitStackCommand(
                CreateContext(fixture.Match.Id, expectedRevision ?? fixture.Match.Revision),
                fixture.Source.Id,
                fixture.NewStackId,
                new StackSplitSpecification(firstMovedIndex),
                newStackPose ?? fixture.NewStackPose);
        }

        private static CommandContext CreateContext(MatchId matchId, long? expectedRevision = 0)
        {
            return new CommandContext(CommandId.New(), matchId, PlayerId.New(), expectedRevision);
        }

        private static SplitStackCommand CreateMalformedCommand(
            CommandContext context,
            ContainerId sourceStackContainerId,
            ContainerId newStackContainerId,
            StackSplitSpecification splitSpecification,
            TabletopPose newStackPose)
        {
            SplitStackCommand command =
                (SplitStackCommand)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SplitStackCommand));

            SetAutoProperty(command, nameof(SplitStackCommand.Context), context);
            SetAutoProperty(command, nameof(SplitStackCommand.SourceStackContainerId), sourceStackContainerId);
            SetAutoProperty(command, nameof(SplitStackCommand.NewStackContainerId), newStackContainerId);
            SetAutoProperty(command, nameof(SplitStackCommand.SplitSpecification), splitSpecification);
            SetAutoProperty(command, nameof(SplitStackCommand.NewStackPose), newStackPose);

            return command;
        }

        private static void SetAutoProperty<TValue>(SplitStackCommand command, string propertyName, TValue value)
        {
            FieldInfo field = typeof(SplitStackCommand).GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            field.SetValue(command, value);
        }

        private static TabletopObjectId[] IdsForLabels(SplitFixture fixture, string labels)
        {
            if (string.IsNullOrEmpty(labels))
            {
                return Array.Empty<TabletopObjectId>();
            }

            return labels
                .Split(',')
                .Select(label => fixture.SourceCards[label[0] - 'A'].BaseState.Id)
                .ToArray();
        }

        private static SplitFixture CreateFixture(
            ContainerKind sourceKind = ContainerKind.Stack,
            int sourceCount = 4,
            long revision = 0,
            bool includeSourcePlacement = true,
            int lockedMovedIndex = -1,
            SeatId? sourceOwnerSeatId = null,
            ObjectVisibility sourceVisibility = ObjectVisibility.Public,
            int sourceCapacity = 0,
            TabletopPose? newStackPose = null)
        {
            SeatId seatId = SeatId.New();
            ContainerTransferService transferService = new ContainerTransferService();
            ContainerState source = CreateContainer(
                sourceKind,
                OwnerFor(sourceKind, seatId, sourceOwnerSeatId),
                CapacityFor(sourceKind, sourceCount, sourceCapacity),
                sourceVisibility);
            ContainerState hand = sourceKind == ContainerKind.Hand
                ? source
                : CreateContainer(ContainerKind.Hand, seatId);
            ContainerState slot = sourceKind == ContainerKind.ConsoleSlot
                ? source
                : CreateContainer(ContainerKind.ConsoleSlot, seatId, 5);
            ContainerState other = CreateContainer(ContainerKind.Stack);
            List<CardInstanceState> sourceCards = new List<CardInstanceState>();
            CardInstanceState otherCard = CreateCard(face: CardFace.FaceUp);

            for (int index = 0; index < sourceCount; index++)
            {
                CardInstanceState card = CreateCard(
                    face: index % 2 == 0 ? CardFace.FaceDown : CardFace.FaceUp,
                    isUserLocked: index == lockedMovedIndex);
                transferService.PlaceIntoContainer(card.BaseState, source);
                sourceCards.Add(card);
            }

            transferService.PlaceIntoContainer(otherCard.BaseState, other);

            List<ContainerState> containers = new List<ContainerState>
            {
                source,
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

            ContainerPlacementState otherPlacement = new ContainerPlacementState(
                other.Id,
                CreatePose(x: 3.0, y: -4.0, rotationDegrees: 20f));
            placements.Add(otherPlacement);

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
                sourceCards.Concat(new[] { otherCard }).ToArray(),
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                containers,
                new[] { seat },
                placements);

            return new SplitFixture(
                match,
                source,
                other,
                sourcePlacement,
                otherPlacement,
                seat,
                sourceCards.ToArray(),
                otherCard,
                ContainerId.New(),
                newStackPose ?? CreatePose(x: 6.0, y: 7.0, rotationDegrees: 450f));
        }

        private static ContainerState CreateContainer(
            ContainerKind kind,
            SeatId? ownerSeatId = null,
            int capacity = 0,
            ObjectVisibility visibility = ObjectVisibility.Public)
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

        private static TabletopPose CreatePose(
            double x = 0.0,
            double y = 0.0,
            float rotationDegrees = 0f,
            int layer = 0,
            int localOrder = 0)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotationDegrees, layer, localOrder);
        }

        private static SeatId OwnerFor(ContainerKind kind, SeatId seatId, SeatId? sourceOwnerSeatId)
        {
            if (kind == ContainerKind.Hand || kind == ContainerKind.ConsoleSlot)
            {
                return seatId;
            }

            return sourceOwnerSeatId ?? SeatId.Empty;
        }

        private static int CapacityFor(ContainerKind kind, int sourceCount, int requestedCapacity)
        {
            if (kind == ContainerKind.ConsoleSlot)
            {
                return Math.Max(sourceCount, 1);
            }

            return requestedCapacity;
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
                SplitFixture fixture,
                SplitStackCommand command,
                bool useDefaultCommand = false)
            {
                Fixture = fixture;
                Command = command;
                this.useDefaultCommand = useDefaultCommand;
            }

            public SplitFixture Fixture { get; }

            private SplitStackCommand Command { get; }

            public SplitStackResult Execute()
            {
                SplitStackCommand command = useDefaultCommand
                    ? Command ?? CreateCommand(Fixture, firstMovedIndex: 1)
                    : Command;
                return new SplitStackUseCase().Execute(Fixture.Match, command);
            }
        }

        private sealed class SplitFixture
        {
            public SplitFixture(
                MatchState match,
                ContainerState source,
                ContainerState other,
                ContainerPlacementState sourcePlacement,
                ContainerPlacementState otherPlacement,
                SeatState seat,
                CardInstanceState[] sourceCards,
                CardInstanceState otherCard,
                ContainerId newStackId,
                TabletopPose newStackPose)
            {
                Match = match;
                Source = source;
                Other = other;
                SourcePlacement = sourcePlacement;
                OtherPlacement = otherPlacement;
                Seat = seat;
                SourceCards = sourceCards;
                OtherCard = otherCard;
                NewStackId = newStackId;
                NewStackPose = newStackPose;
                ExtraObjects = new List<TabletopObjectState>();
            }

            public MatchState Match { get; }

            public ContainerState Source { get; }

            public ContainerState Other { get; }

            public ContainerPlacementState SourcePlacement { get; }

            public ContainerPlacementState OtherPlacement { get; }

            public SeatState Seat { get; }

            public CardInstanceState[] SourceCards { get; }

            public CardInstanceState OtherCard { get; }

            public ContainerId NewStackId { get; }

            public TabletopPose NewStackPose { get; }

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

            public static AggregateSnapshot Capture(SplitFixture fixture)
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

            public void AssertMatches(SplitFixture fixture)
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

            public void AssertMatchesAfterSplit(SplitFixture fixture, int firstMovedIndex)
            {
                Assert.That(fixture.Match.Revision, Is.EqualTo(revision + 1));
                Assert.That(fixture.Match.Containers[fixture.Source.Id], Is.SameAs(containerInstances[fixture.Source.Id]));
                Assert.That(fixture.Match.ContainerPlacements[fixture.Source.Id], Is.SameAs(placementInstances[fixture.Source.Id]));
                Assert.That(fixture.Match.Containers.ContainsKey(fixture.NewStackId), Is.True);
                Assert.That(fixture.Match.ContainerPlacements.ContainsKey(fixture.NewStackId), Is.True);

                TabletopObjectId[] originalSourceOrder = containerOrders[fixture.Source.Id];
                TabletopObjectId[] remaining = originalSourceOrder.Take(firstMovedIndex).ToArray();
                TabletopObjectId[] moved = originalSourceOrder.Skip(firstMovedIndex).ToArray();

                Assert.That(fixture.Source.ObjectIds, Is.EqualTo(remaining));
                Assert.That(fixture.Match.Containers[fixture.NewStackId].ObjectIds, Is.EqualTo(moved));

                foreach (KeyValuePair<TabletopObjectId, ObjectSnapshot> pair in objectSnapshots)
                {
                    if (moved.Contains(pair.Key))
                    {
                        pair.Value.AssertMatchesExceptContainer(fixture.NewStackId);
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
