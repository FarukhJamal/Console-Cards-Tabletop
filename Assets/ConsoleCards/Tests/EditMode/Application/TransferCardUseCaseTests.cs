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
    public sealed class TransferCardUseCaseTests
    {
        public enum TransferFailureScenario
        {
            NullCommand,
            MatchMismatch,
            RevisionConflict,
            ObjectMissing,
            Pawn,
            Token,
            UserLockedCard,
            SourceContainerMissing,
            SourceContainerMismatch,
            SourceMembershipMissing,
            ObjectFoundInUnexpectedContainer,
            DestinationMissing,
            DestinationFull,
            DestinationAlreadyContainsCard,
            RevisionOverflow
        }

        [Test]
        public void Execute_WhenMatchStateIsNull_ReturnsInvalidMatchMissing()
        {
            TransferCardUseCase useCase = new TransferCardUseCase();

            TransferCardResult result = useCase.Execute(
                null,
                TransferCardCommand.ToContainer(
                    CreateContext(MatchId.New()),
                    TabletopObjectId.New(),
                    ContainerId.Empty,
                    ContainerId.New()));

            AssertFailure(result, CommandResultStatus.Invalid, TransferCardError.MatchMissing);
        }

        [TestCase(TransferFailureScenario.NullCommand, CommandResultStatus.Invalid, TransferCardError.CommandMissing)]
        [TestCase(TransferFailureScenario.MatchMismatch, CommandResultStatus.Invalid, TransferCardError.MatchMismatch)]
        [TestCase(TransferFailureScenario.RevisionConflict, CommandResultStatus.Conflict, TransferCardError.RevisionConflict)]
        [TestCase(TransferFailureScenario.ObjectMissing, CommandResultStatus.Rejected, TransferCardError.ObjectMissing)]
        [TestCase(TransferFailureScenario.Pawn, CommandResultStatus.Rejected, TransferCardError.ObjectNotCard)]
        [TestCase(TransferFailureScenario.Token, CommandResultStatus.Rejected, TransferCardError.ObjectNotCard)]
        [TestCase(TransferFailureScenario.UserLockedCard, CommandResultStatus.Rejected, TransferCardError.ObjectUserLocked)]
        [TestCase(TransferFailureScenario.SourceContainerMissing, CommandResultStatus.Rejected, TransferCardError.SourceContainerMissing)]
        [TestCase(TransferFailureScenario.SourceContainerMismatch, CommandResultStatus.Rejected, TransferCardError.SourceContainerMismatch)]
        [TestCase(TransferFailureScenario.SourceMembershipMissing, CommandResultStatus.Rejected, TransferCardError.SourceMembershipMissing)]
        [TestCase(TransferFailureScenario.ObjectFoundInUnexpectedContainer, CommandResultStatus.Rejected, TransferCardError.ObjectFoundInUnexpectedContainer)]
        [TestCase(TransferFailureScenario.DestinationMissing, CommandResultStatus.Rejected, TransferCardError.DestinationContainerMissing)]
        [TestCase(TransferFailureScenario.DestinationFull, CommandResultStatus.Rejected, TransferCardError.DestinationCapacityExceeded)]
        [TestCase(TransferFailureScenario.DestinationAlreadyContainsCard, CommandResultStatus.Rejected, TransferCardError.DestinationAlreadyContainsObject)]
        [TestCase(TransferFailureScenario.RevisionOverflow, CommandResultStatus.Conflict, TransferCardError.RevisionOverflow)]
        public void Execute_WhenValidationFails_ReturnsExpectedFailure(
            TransferFailureScenario scenario,
            CommandResultStatus expectedStatus,
            TransferCardError expectedError)
        {
            FailureFixture failure = CreateFailureFixture(scenario);

            TransferCardResult result = failure.Execute();

            AssertFailure(result, expectedStatus, expectedError);
        }

        [TestCase(TransferFailureScenario.NullCommand)]
        [TestCase(TransferFailureScenario.MatchMismatch)]
        [TestCase(TransferFailureScenario.RevisionConflict)]
        [TestCase(TransferFailureScenario.ObjectMissing)]
        [TestCase(TransferFailureScenario.Pawn)]
        [TestCase(TransferFailureScenario.Token)]
        [TestCase(TransferFailureScenario.UserLockedCard)]
        [TestCase(TransferFailureScenario.SourceContainerMissing)]
        [TestCase(TransferFailureScenario.SourceContainerMismatch)]
        [TestCase(TransferFailureScenario.SourceMembershipMissing)]
        [TestCase(TransferFailureScenario.ObjectFoundInUnexpectedContainer)]
        [TestCase(TransferFailureScenario.DestinationMissing)]
        [TestCase(TransferFailureScenario.DestinationFull)]
        [TestCase(TransferFailureScenario.DestinationAlreadyContainsCard)]
        [TestCase(TransferFailureScenario.RevisionOverflow)]
        public void Execute_WhenValidationFails_PreservesAggregateState(TransferFailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            AggregateSnapshot before = AggregateSnapshot.Capture(failure.Fixture);

            TransferCardResult result = failure.Execute();

            Assert.That(result.Succeeded, Is.False);
            before.AssertMatches(failure.Fixture);
        }

        [TestCase(ContainerKind.Generic)]
        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.ConsoleSlot)]
        public void Execute_WhenTabletopToContainerIsValid_AppendsCardAndPreservesPose(ContainerKind destinationKind)
        {
            TransferFixture fixture = CreateFixture(
                sourceKind: null,
                destinationKind: destinationKind,
                destinationCapacity: destinationKind == ContainerKind.ConsoleSlot ? 1 : 0,
                destinationCount: 0,
                revision: 9);
            TabletopPose originalPose = fixture.TargetCard.BaseState.Pose;

            TransferCardResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Destination.ObjectIds.Last(), Is.EqualTo(fixture.TargetCard.BaseState.Id));
            Assert.That(fixture.TargetCard.BaseState.ContainerId, Is.EqualTo(fixture.Destination.Id));
            Assert.That(fixture.TargetCard.BaseState.Pose, Is.EqualTo(originalPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(10));
            Assert.That(fixture.Match.Cards[fixture.TargetCard.BaseState.Id], Is.SameAs(fixture.TargetCard));
            Assert.That(fixture.Match.Containers[fixture.Destination.Id], Is.SameAs(fixture.Destination));
        }

        [TestCase(ContainerKind.Generic)]
        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.ConsoleSlot)]
        public void Execute_WhenContainerToTabletopIsValid_RemovesCardAndAppliesTargetPose(ContainerKind sourceKind)
        {
            TabletopPose targetPose = CreatePose(x: -8.5, y: 6.25, rotationDegrees: -725f, layer: 3, localOrder: 11);
            TransferFixture fixture = CreateFixture(
                sourceKind: sourceKind,
                destinationKind: null,
                revision: 4);
            TabletopObjectId[] otherSourceMembers = fixture.Source.ObjectIds
                .Where(id => id != fixture.TargetCard.BaseState.Id)
                .ToArray();

            TransferCardResult result = Execute(fixture, targetPose: targetPose);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Source.Contains(fixture.TargetCard.BaseState.Id), Is.False);
            Assert.That(fixture.Source.ObjectIds, Is.EqualTo(otherSourceMembers));
            Assert.That(fixture.TargetCard.BaseState.ContainerId, Is.EqualTo(ContainerId.Empty));
            Assert.That(fixture.TargetCard.BaseState.Pose, Is.EqualTo(targetPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(5));
            Assert.That(fixture.Match.Cards[fixture.TargetCard.BaseState.Id], Is.SameAs(fixture.TargetCard));
        }

        [TestCase(ContainerKind.Deck, ContainerKind.Hand)]
        [TestCase(ContainerKind.Hand, ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.Stack, ContainerKind.ConsoleSlot)]
        [TestCase(ContainerKind.ConsoleSlot, ContainerKind.Stack)]
        [TestCase(ContainerKind.Generic, ContainerKind.Deck)]
        public void Execute_WhenContainerToContainerIsValid_RemovesFromSourceAndAppendsToDestination(
            ContainerKind sourceKind,
            ContainerKind destinationKind)
        {
            TransferFixture fixture = CreateFixture(
                sourceKind: sourceKind,
                destinationKind: destinationKind,
                destinationCapacity: destinationKind == ContainerKind.ConsoleSlot ? 2 : 0,
                destinationCount: destinationKind == ContainerKind.ConsoleSlot ? 0 : 1,
                revision: 12);
            TabletopPose originalPose = fixture.TargetCard.BaseState.Pose;
            TabletopObjectId[] originalSourceWithoutTarget = fixture.Source.ObjectIds
                .Where(id => id != fixture.TargetCard.BaseState.Id)
                .ToArray();
            TabletopObjectId[] originalDestination = fixture.Destination.ObjectIds.ToArray();

            TransferCardResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Source.ObjectIds, Is.EqualTo(originalSourceWithoutTarget));
            Assert.That(fixture.Destination.ObjectIds, Is.EqualTo(
                originalDestination.Concat(new[] { fixture.TargetCard.BaseState.Id }).ToArray()));
            Assert.That(fixture.TargetCard.BaseState.ContainerId, Is.EqualTo(fixture.Destination.Id));
            Assert.That(fixture.TargetCard.BaseState.Pose, Is.EqualTo(originalPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(13));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesNonLocationFieldsAndAggregateIdentity()
        {
            TransferFixture fixture = CreateFixture(
                sourceKind: ContainerKind.Deck,
                destinationKind: ContainerKind.Hand);
            MatchState match = fixture.Match;
            ContainerState source = fixture.Source;
            ContainerState destination = fixture.Destination;
            CardInstanceState target = fixture.TargetCard;
            AggregateSnapshot before = AggregateSnapshot.Capture(fixture);

            TransferCardResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Match, Is.SameAs(match));
            Assert.That(fixture.Source, Is.SameAs(source));
            Assert.That(fixture.Destination, Is.SameAs(destination));
            Assert.That(fixture.Match.Cards[target.BaseState.Id], Is.SameAs(target));
            before.AssertNonLocationFieldsMatch(fixture);
            AssertConstructivelyConsistent(fixture);
        }

        [Test]
        public void StaticBoundary_UsesApplicationCoreOnlyAndAddsNoPresentationTransferCode()
        {
            Assembly applicationAssembly = typeof(TransferCardUseCase).Assembly;
            string[] referencedAssemblyNames = applicationAssembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.That(referencedAssemblyNames, Does.Contain("ConsoleCards.Core"));
            Assert.That(referencedAssemblyNames.Any(name => name.StartsWith("UnityEngine", StringComparison.Ordinal)), Is.False);
            Assert.That(referencedAssemblyNames, Does.Not.Contain("ConsoleCards.Presentation"));
            Assert.That(applicationAssembly.GetType("ConsoleCards.Application.Commands.TransferCardsCommand"), Is.Null);
        }

        private static TransferCardResult Execute(
            TransferFixture fixture,
            TransferCardCommand command = null,
            TabletopPose? targetPose = null)
        {
            TransferCardCommand actualCommand = command ?? CreateCommandForFixture(fixture, targetPose);
            TransferCardUseCase useCase = new TransferCardUseCase();
            return useCase.Execute(fixture.Match, actualCommand);
        }

        private static TransferCardCommand CreateCommandForFixture(
            TransferFixture fixture,
            TabletopPose? targetPose = null)
        {
            return new TransferCardCommand(
                CreateContext(fixture.Match.Id, fixture.Match.Revision),
                fixture.TargetCard.BaseState.Id,
                fixture.Source?.Id ?? ContainerId.Empty,
                fixture.Destination?.Id ?? ContainerId.Empty,
                fixture.Destination == null ? targetPose ?? CreatePose(x: 9.0, y: 9.0) : (TabletopPose?)null);
        }

        private static void AssertFailure(
            TransferCardResult result,
            CommandResultStatus expectedStatus,
            TransferCardError expectedError)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.Error, Is.EqualTo(expectedError));
            Assert.That(result.Revision, Is.EqualTo(-1));
        }

        private static FailureFixture CreateFailureFixture(TransferFailureScenario scenario)
        {
            switch (scenario)
            {
                case TransferFailureScenario.NullCommand:
                {
                    TransferFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
                    return new FailureFixture(fixture, null);
                }

                case TransferFailureScenario.MatchMismatch:
                {
                    TransferFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
                    TransferCardCommand command = new TransferCardCommand(
                        CreateContext(MatchId.New()),
                        fixture.TargetCard.BaseState.Id,
                        fixture.Source.Id,
                        fixture.Destination.Id,
                        null);
                    return new FailureFixture(fixture, command);
                }

                case TransferFailureScenario.RevisionConflict:
                {
                    TransferFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand, revision: 2);
                    TransferCardCommand command = new TransferCardCommand(
                        CreateContext(fixture.Match.Id, 3),
                        fixture.TargetCard.BaseState.Id,
                        fixture.Source.Id,
                        fixture.Destination.Id,
                        null);
                    return new FailureFixture(fixture, command);
                }

                case TransferFailureScenario.ObjectMissing:
                {
                    TransferFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
                    TransferCardCommand command = new TransferCardCommand(
                        CreateContext(fixture.Match.Id),
                        TabletopObjectId.New(),
                        fixture.Source.Id,
                        fixture.Destination.Id,
                        null);
                    return new FailureFixture(fixture, command);
                }

                case TransferFailureScenario.Pawn:
                    return CreateNonCardFailureFixture(TabletopObjectKind.Pawn);

                case TransferFailureScenario.Token:
                    return CreateNonCardFailureFixture(TabletopObjectKind.Token);

                case TransferFailureScenario.UserLockedCard:
                    return new FailureFixture(
                        CreateFixture(ContainerKind.Deck, ContainerKind.Hand, isUserLocked: true),
                        null,
                        useDefaultCommand: true);

                case TransferFailureScenario.SourceContainerMissing:
                {
                    TransferFixture fixture = CreateFixture(sourceKind: null, destinationKind: ContainerKind.Hand);
                    ContainerId missingSourceId = ContainerId.New();
                    fixture.TargetCard.BaseState.SetContainer(missingSourceId);
                    TransferCardCommand command = new TransferCardCommand(
                        CreateContext(fixture.Match.Id),
                        fixture.TargetCard.BaseState.Id,
                        missingSourceId,
                        fixture.Destination.Id,
                        null);
                    return new FailureFixture(fixture, command);
                }

                case TransferFailureScenario.SourceContainerMismatch:
                {
                    TransferFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
                    TransferCardCommand command = new TransferCardCommand(
                        CreateContext(fixture.Match.Id),
                        fixture.TargetCard.BaseState.Id,
                        fixture.OtherContainer.Id,
                        fixture.Destination.Id,
                        null);
                    return new FailureFixture(fixture, command);
                }

                case TransferFailureScenario.SourceMembershipMissing:
                {
                    TransferFixture fixture = CreateSourceMembershipMissingFixture();
                    ContainerState source = fixture.Source;
                    fixture.TargetCard.BaseState.SetContainer(source.Id);
                    TransferCardCommand command = new TransferCardCommand(
                        CreateContext(fixture.Match.Id),
                        fixture.TargetCard.BaseState.Id,
                        source.Id,
                        fixture.Destination.Id,
                        null);
                    return new FailureFixture(fixture, command);
                }

                case TransferFailureScenario.ObjectFoundInUnexpectedContainer:
                {
                    TransferFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
                    fixture.TargetCard.BaseState.SetContainer(ContainerId.Empty);
                    new ContainerTransferService().PlaceIntoContainer(
                        fixture.TargetCard.BaseState,
                        fixture.OtherContainer);
                    fixture.TargetCard.BaseState.SetContainer(fixture.Source.Id);
                    return new FailureFixture(fixture, null, useDefaultCommand: true);
                }

                case TransferFailureScenario.DestinationMissing:
                {
                    TransferFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
                    TransferCardCommand command = new TransferCardCommand(
                        CreateContext(fixture.Match.Id),
                        fixture.TargetCard.BaseState.Id,
                        fixture.Source.Id,
                        ContainerId.New(),
                        null);
                    return new FailureFixture(fixture, command);
                }

                case TransferFailureScenario.DestinationFull:
                    return new FailureFixture(
                        CreateFixture(
                            ContainerKind.Deck,
                            ContainerKind.Hand,
                            destinationCapacity: 1,
                            destinationCount: 1),
                        null,
                        useDefaultCommand: true);

                case TransferFailureScenario.DestinationAlreadyContainsCard:
                {
                    TransferFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
                    fixture.TargetCard.BaseState.SetContainer(ContainerId.Empty);
                    new ContainerTransferService().PlaceIntoContainer(
                        fixture.TargetCard.BaseState,
                        fixture.Destination);
                    fixture.TargetCard.BaseState.SetContainer(fixture.Source.Id);
                    return new FailureFixture(fixture, null, useDefaultCommand: true);
                }

                case TransferFailureScenario.RevisionOverflow:
                    return new FailureFixture(
                        CreateFixture(ContainerKind.Deck, ContainerKind.Hand, revision: long.MaxValue),
                        null,
                        useDefaultCommand: true);

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported transfer failure scenario.");
            }
        }

        private static TransferFixture CreateSourceMembershipMissingFixture()
        {
            SeatId seatId = SeatId.New();
            ContainerState source = CreateContainer(ContainerKind.Deck);
            ContainerState destination = CreateContainer(ContainerKind.Hand, seatId);
            ContainerState slot = CreateContainer(ContainerKind.ConsoleSlot, seatId, 1);
            ContainerState other = CreateContainer(ContainerKind.Generic);
            CardInstanceState targetCard = CreateCard();
            CardInstanceState otherCard = CreateCard();
            new ContainerTransferService().PlaceIntoContainer(otherCard.BaseState, other);
            ContainerPlacementState placement = new ContainerPlacementState(source.Id, CreatePose(x: 1.0, y: -1.0));
            SeatState seat = new SeatState(
                seatId,
                TabletopPose.Default,
                destination.Id,
                new ConsoleState(seatId, new[] { slot.Id }),
                PlayerId.Empty,
                SeatStatus.Vacant);
            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                0,
                new[] { targetCard, otherCard },
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                new[] { source, destination, slot, other },
                new[] { seat },
                new[] { placement });

            return new TransferFixture(
                match,
                source,
                destination,
                other,
                placement,
                seat,
                targetCard,
                Array.Empty<CardInstanceState>(),
                new[] { otherCard },
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>());
        }

        private static FailureFixture CreateNonCardFailureFixture(TabletopObjectKind kind)
        {
            TabletopObjectState objectState = CreateObject(kind);
            ContainerState destination = CreateContainer(ContainerKind.Hand, ownerSeatId: SeatId.New());
            MatchState match = kind == TabletopObjectKind.Pawn
                ? CreateMatch(pawns: new[] { new PawnState(objectState) }, containers: new[] { destination })
                : CreateMatch(tokens: new[] { new TokenState(objectState) }, containers: new[] { destination });
            TransferFixture fixture = new TransferFixture(
                match,
                null,
                destination,
                CreateContainer(ContainerKind.Generic),
                null,
                null,
                new CardInstanceState(CreateObject(TabletopObjectKind.Card), CardFace.FaceDown),
                Array.Empty<CardInstanceState>(),
                Array.Empty<CardInstanceState>(),
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>());
            TransferCardCommand command = new TransferCardCommand(
                CreateContext(match.Id),
                objectState.Id,
                ContainerId.Empty,
                destination.Id,
                null);
            return new FailureFixture(fixture, command);
        }

        private static TransferFixture CreateFixture(
            ContainerKind? sourceKind,
            ContainerKind? destinationKind,
            int destinationCapacity = 0,
            int destinationCount = 1,
            long revision = 0,
            bool isUserLocked = false)
        {
            SeatId seatId = SeatId.New();
            ContainerTransferService transferService = new ContainerTransferService();
            ContainerState source = sourceKind.HasValue
                ? CreateContainer(sourceKind.Value, CreateOwner(sourceKind.Value, seatId), CapacityFor(sourceKind.Value))
                : null;
            ContainerState destination = destinationKind.HasValue
                ? CreateContainer(destinationKind.Value, CreateOwner(destinationKind.Value, seatId), destinationCapacity)
                : null;
            ContainerState other = CreateContainer(ContainerKind.Generic);
            ContainerState hand = FirstKind(ContainerKind.Hand, source, destination)
                ?? CreateContainer(ContainerKind.Hand, seatId);
            ContainerState slot = FirstKind(ContainerKind.ConsoleSlot, source, destination)
                ?? CreateContainer(ContainerKind.ConsoleSlot, seatId, 1);
            CardInstanceState targetCard = CreateCard(isUserLocked: isUserLocked);
            List<CardInstanceState> destinationCards = new List<CardInstanceState>();
            CardInstanceState otherCard = CreateCard();

            if (source != null)
            {
                transferService.PlaceIntoContainer(targetCard.BaseState, source);
            }

            if (destination != null)
            {
                for (int index = 0; index < destinationCount; index++)
                {
                    CardInstanceState destinationCard = CreateCard(face: CardFace.FaceUp);
                    transferService.PlaceIntoContainer(destinationCard.BaseState, destination);
                    destinationCards.Add(destinationCard);
                }
            }

            transferService.PlaceIntoContainer(otherCard.BaseState, other);

            List<ContainerState> containers = new List<ContainerState>();
            AddContainerIfNotNull(containers, source);
            AddContainerIfNotNull(containers, destination);
            AddContainerIfNotNull(containers, other);
            AddContainerIfNotNull(containers, hand);
            AddContainerIfNotNull(containers, slot);

            ContainerState placementContainer = containers.FirstOrDefault(container =>
                container.Kind == ContainerKind.Deck
                || container.Kind == ContainerKind.Stack
                || container.Kind == ContainerKind.DiscardPile)
                ?? CreateContainer(ContainerKind.Deck);
            AddContainerIfNotNull(containers, placementContainer);

            ContainerPlacementState placement = new ContainerPlacementState(
                placementContainer.Id,
                CreatePose(x: 2.0, y: -2.0, rotationDegrees: 45f));
            SeatState seat = new SeatState(
                seatId,
                CreatePose(x: -5.0, y: 5.0),
                hand.Id,
                new ConsoleState(seatId, new[] { slot.Id }),
                PlayerId.Empty,
                SeatStatus.Vacant);

            CardInstanceState[] cards = new[] { targetCard, otherCard }
                .Concat(destinationCards)
                .ToArray();
            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                cards,
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                containers,
                new[] { seat },
                new[] { placement });

            return new TransferFixture(
                match,
                source,
                destination,
                other,
                placement,
                seat,
                targetCard,
                destinationCards.ToArray(),
                new[] { otherCard },
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>());
        }

        private static MatchState CreateMatch(
            CardInstanceState[] cards = null,
            PawnState[] pawns = null,
            TokenState[] tokens = null,
            ContainerState[] containers = null)
        {
            ContainerState hand = CreateContainer(ContainerKind.Hand, SeatId.New());
            ContainerState slot = CreateContainer(ContainerKind.ConsoleSlot, hand.OwnerSeatId, 1);
            SeatState seat = new SeatState(
                hand.OwnerSeatId,
                TabletopPose.Default,
                hand.Id,
                new ConsoleState(hand.OwnerSeatId, new[] { slot.Id }),
                PlayerId.Empty,
                SeatStatus.Vacant);
            List<ContainerState> allContainers = new List<ContainerState> { hand, slot };

            if (containers != null)
            {
                foreach (ContainerState container in containers)
                {
                    AddContainerIfNotNull(allContainers, container);
                }
            }

            return new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                0,
                cards ?? Array.Empty<CardInstanceState>(),
                pawns ?? Array.Empty<PawnState>(),
                tokens ?? Array.Empty<TokenState>(),
                allContainers,
                new[] { seat });
        }

        private static SeatId CreateOwner(ContainerKind kind, SeatId seatId)
        {
            return kind == ContainerKind.Hand || kind == ContainerKind.ConsoleSlot ? seatId : SeatId.Empty;
        }

        private static int CapacityFor(ContainerKind kind)
        {
            return kind == ContainerKind.ConsoleSlot ? 2 : 0;
        }

        private static ContainerState FirstKind(
            ContainerKind kind,
            params ContainerState[] containers)
        {
            return containers.FirstOrDefault(container => container != null && container.Kind == kind);
        }

        private static void AddContainerIfNotNull(List<ContainerState> containers, ContainerState container)
        {
            if (container != null && containers.All(existing => existing.Id != container.Id))
            {
                containers.Add(container);
            }
        }

        private static void AssertConstructivelyConsistent(TransferFixture fixture)
        {
            foreach (CardInstanceState card in fixture.Match.Cards.Values)
            {
                int membershipCount = fixture.Match.Containers.Values.Count(container => container.Contains(card.BaseState.Id));
                if (card.BaseState.ContainerId.IsEmpty)
                {
                    Assert.That(membershipCount, Is.EqualTo(0));
                }
                else
                {
                    Assert.That(membershipCount, Is.EqualTo(1));
                    Assert.That(fixture.Match.Containers[card.BaseState.ContainerId].Contains(card.BaseState.Id), Is.True);
                }
            }
        }

        private static CommandContext CreateContext(MatchId matchId, long? expectedRevision = 0)
        {
            return new CommandContext(CommandId.New(), matchId, PlayerId.New(), expectedRevision);
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
            return new CardInstanceState(CreateObject(TabletopObjectKind.Card, isUserLocked: isUserLocked), face);
        }

        private static TabletopObjectState CreateObject(
            TabletopObjectKind kind,
            bool isUserLocked = false)
        {
            return new TabletopObjectState(
                TabletopObjectId.New(),
                ObjectDefinitionId.New(),
                kind,
                CreatePose(x: 1.0, y: 2.0, rotationDegrees: 30f, layer: 2, localOrder: 3),
                ContainerId.Empty,
                PlayerId.New(),
                ObjectVisibility.OwnerOnly,
                isUserLocked);
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

        private sealed class FailureFixture
        {
            private readonly bool useDefaultCommand;

            public FailureFixture(
                TransferFixture fixture,
                TransferCardCommand command,
                bool useDefaultCommand = false)
            {
                Fixture = fixture;
                Command = command;
                this.useDefaultCommand = useDefaultCommand;
            }

            public TransferFixture Fixture { get; }

            private TransferCardCommand Command { get; }

            public TransferCardResult Execute()
            {
                TransferCardUseCase useCase = new TransferCardUseCase();
                TransferCardCommand command = useDefaultCommand ? Command ?? CreateCommandForFixture(Fixture) : Command;
                return useCase.Execute(Fixture.Match, command);
            }
        }

        private sealed class TransferFixture
        {
            public TransferFixture(
                MatchState match,
                ContainerState source,
                ContainerState destination,
                ContainerState otherContainer,
                ContainerPlacementState placement,
                SeatState seat,
                CardInstanceState targetCard,
                CardInstanceState[] destinationCards,
                CardInstanceState[] otherCards,
                PawnState[] pawns,
                TokenState[] tokens)
            {
                Match = match;
                Source = source;
                Destination = destination;
                OtherContainer = otherContainer;
                Placement = placement;
                Seat = seat;
                TargetCard = targetCard;
                DestinationCards = destinationCards;
                OtherCards = otherCards;
                Pawns = pawns;
                Tokens = tokens;
            }

            public MatchState Match { get; }

            public ContainerState Source { get; }

            public ContainerState Destination { get; }

            public ContainerState OtherContainer { get; }

            public ContainerPlacementState Placement { get; }

            public SeatState Seat { get; }

            public CardInstanceState TargetCard { get; }

            public CardInstanceState[] DestinationCards { get; }

            public CardInstanceState[] OtherCards { get; }

            public PawnState[] Pawns { get; }

            public TokenState[] Tokens { get; }

        }

        private sealed class AggregateSnapshot
        {
            private readonly IReadOnlyDictionary<ContainerId, TabletopObjectId[]> containerOrders;
            private readonly IReadOnlyDictionary<TabletopObjectId, ObjectSnapshot> objectSnapshots;
            private readonly IReadOnlyDictionary<TabletopObjectId, CardFace> cardFaces;
            private readonly IReadOnlyDictionary<ContainerId, TabletopPose> placementPoses;
            private readonly ContainerPlacementState placement;
            private readonly SeatState seat;
            private readonly long revision;

            private AggregateSnapshot(
                long revision,
                IReadOnlyDictionary<ContainerId, TabletopObjectId[]> containerOrders,
                IReadOnlyDictionary<TabletopObjectId, ObjectSnapshot> objectSnapshots,
                IReadOnlyDictionary<TabletopObjectId, CardFace> cardFaces,
                IReadOnlyDictionary<ContainerId, TabletopPose> placementPoses,
                ContainerPlacementState placement,
                SeatState seat)
            {
                this.revision = revision;
                this.containerOrders = containerOrders;
                this.objectSnapshots = objectSnapshots;
                this.cardFaces = cardFaces;
                this.placementPoses = placementPoses;
                this.placement = placement;
                this.seat = seat;
            }

            public static AggregateSnapshot Capture(TransferFixture fixture)
            {
                Dictionary<TabletopObjectId, ObjectSnapshot> objectSnapshots =
                    new Dictionary<TabletopObjectId, ObjectSnapshot>();

                foreach (CardInstanceState card in fixture.Match.Cards.Values)
                {
                    objectSnapshots.Add(card.BaseState.Id, ObjectSnapshot.Capture(card.BaseState));
                }

                foreach (PawnState pawn in fixture.Match.Pawns.Values)
                {
                    objectSnapshots.Add(pawn.BaseState.Id, ObjectSnapshot.Capture(pawn.BaseState));
                }

                foreach (TokenState token in fixture.Match.Tokens.Values)
                {
                    objectSnapshots.Add(token.BaseState.Id, ObjectSnapshot.Capture(token.BaseState));
                }

                return new AggregateSnapshot(
                    fixture.Match.Revision,
                    fixture.Match.Containers.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value.ObjectIds.ToArray()),
                    objectSnapshots,
                    fixture.Match.Cards.ToDictionary(pair => pair.Key, pair => pair.Value.Face),
                    fixture.Match.ContainerPlacements.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value.Pose),
                    fixture.Placement,
                    fixture.Seat);
            }

            public void AssertMatches(TransferFixture fixture)
            {
                Assert.That(fixture.Match.Revision, Is.EqualTo(revision));

                foreach (KeyValuePair<ContainerId, TabletopObjectId[]> pair in containerOrders)
                {
                    Assert.That(fixture.Match.Containers[pair.Key].ObjectIds, Is.EqualTo(pair.Value));
                }

                foreach (KeyValuePair<TabletopObjectId, ObjectSnapshot> pair in objectSnapshots)
                {
                    pair.Value.AssertMatches(fixture.Match.GetObject(pair.Key));
                }

                foreach (KeyValuePair<TabletopObjectId, CardFace> pair in cardFaces)
                {
                    Assert.That(fixture.Match.Cards[pair.Key].Face, Is.EqualTo(pair.Value));
                }

                foreach (KeyValuePair<ContainerId, TabletopPose> pair in placementPoses)
                {
                    Assert.That(fixture.Match.ContainerPlacements[pair.Key].Pose, Is.EqualTo(pair.Value));
                }

                if (placement != null)
                {
                    Assert.That(fixture.Match.ContainerPlacements[placement.ContainerId], Is.SameAs(placement));
                }

                if (seat != null)
                {
                    Assert.That(fixture.Match.Seats[seat.Id], Is.SameAs(seat));
                }
            }

            public void AssertNonLocationFieldsMatch(TransferFixture fixture)
            {
                foreach (KeyValuePair<TabletopObjectId, ObjectSnapshot> pair in objectSnapshots)
                {
                    pair.Value.AssertNonLocationFieldsMatch(fixture.Match.GetObject(pair.Key));
                }

                foreach (KeyValuePair<TabletopObjectId, CardFace> pair in cardFaces)
                {
                    Assert.That(fixture.Match.Cards[pair.Key].Face, Is.EqualTo(pair.Value));
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
                AssertNonLocationFieldsMatch(state);
                Assert.That(state.Pose, Is.EqualTo(Pose));
                Assert.That(state.ContainerId, Is.EqualTo(ContainerId));
            }

            public void AssertNonLocationFieldsMatch(TabletopObjectState state)
            {
                Assert.That(state.Id, Is.EqualTo(Id));
                Assert.That(state.DefinitionId, Is.EqualTo(DefinitionId));
                Assert.That(state.Kind, Is.EqualTo(Kind));
                Assert.That(state.OwnerPlayerId, Is.EqualTo(OwnerPlayerId));
                Assert.That(state.Visibility, Is.EqualTo(Visibility));
                Assert.That(state.IsUserLocked, Is.EqualTo(IsUserLocked));
            }
        }
    }
}
