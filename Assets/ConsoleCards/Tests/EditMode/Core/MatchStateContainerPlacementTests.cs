using System;
using System.Collections.Generic;
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
    public sealed class MatchStateContainerPlacementTests
    {
        [Test]
        public void ExistingConstructor_WhenNoPlacementsProvided_StillCreatesMatch()
        {
            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                0,
                Array.Empty<CardInstanceState>(),
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                Array.Empty<ContainerState>(),
                Array.Empty<SeatState>());

            Assert.That(match.ContainerPlacements, Is.Empty);
        }

        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.DiscardPile)]
        public void Constructor_WhenPlacementKindIsAllowed_AcceptsPlacement(ContainerKind kind)
        {
            ContainerState container = CreateContainer(kind: kind);
            ContainerPlacementState placement = CreatePlacement(container.Id);

            MatchState match = CreateMatch(
                containers: new[] { container },
                containerPlacements: new[] { placement });

            Assert.That(match.ContainerPlacements[container.Id], Is.SameAs(placement));
        }

        [Test]
        public void Constructor_WhenMultiplePlacementsAreValid_AcceptsAllPlacements()
        {
            ContainerState deck = CreateContainer(kind: ContainerKind.Deck);
            ContainerState stack = CreateContainer(kind: ContainerKind.Stack);
            ContainerState discard = CreateContainer(kind: ContainerKind.DiscardPile);
            ContainerPlacementState deckPlacement = CreatePlacement(deck.Id, x: 1);
            ContainerPlacementState stackPlacement = CreatePlacement(stack.Id, x: 2);
            ContainerPlacementState discardPlacement = CreatePlacement(discard.Id, x: 3);

            MatchState match = CreateMatch(
                containers: new[] { deck, stack, discard },
                containerPlacements: new[] { deckPlacement, stackPlacement, discardPlacement });

            Assert.That(match.ContainerPlacements, Has.Count.EqualTo(3));
            Assert.That(match.ContainerPlacements[deck.Id], Is.SameAs(deckPlacement));
            Assert.That(match.ContainerPlacements[stack.Id], Is.SameAs(stackPlacement));
            Assert.That(match.ContainerPlacements[discard.Id], Is.SameAs(discardPlacement));
        }

        [Test]
        public void TryGetContainerPlacement_WhenPlacementExists_ReturnsTrueAndPlacement()
        {
            ContainerState deck = CreateContainer(kind: ContainerKind.Deck);
            ContainerPlacementState placement = CreatePlacement(deck.Id);
            MatchState match = CreateMatch(
                containers: new[] { deck },
                containerPlacements: new[] { placement });

            bool result = match.TryGetContainerPlacement(deck.Id, out ContainerPlacementState foundPlacement);

            Assert.That(result, Is.True);
            Assert.That(foundPlacement, Is.SameAs(placement));
        }

        [Test]
        public void TryGetContainerPlacement_WhenPlacementIsMissing_ReturnsFalseAndNull()
        {
            MatchState match = CreateMatch();

            bool result = match.TryGetContainerPlacement(ContainerId.New(), out ContainerPlacementState foundPlacement);

            Assert.That(result, Is.False);
            Assert.That(foundPlacement, Is.Null);
        }

        [Test]
        public void ContainerPlacements_CannotBeMutatedExternally()
        {
            ContainerState deck = CreateContainer(kind: ContainerKind.Deck);
            ContainerPlacementState placement = CreatePlacement(deck.Id);
            MatchState match = CreateMatch(
                containers: new[] { deck },
                containerPlacements: new[] { placement });

            Assert.That(match.ContainerPlacements as Dictionary<ContainerId, ContainerPlacementState>, Is.Null);

            IDictionary<ContainerId, ContainerPlacementState> dictionaryView =
                match.ContainerPlacements as IDictionary<ContainerId, ContainerPlacementState>;
            Assert.That(dictionaryView, Is.Not.Null);
            Assert.That(
                () => dictionaryView.Add(ContainerId.New(), CreatePlacement(ContainerId.New())),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void Constructor_WhenPlacementCollectionIsNull_ThrowsArgumentNullException()
        {
            Assert.That(
                () => new MatchState(
                    MatchId.New(),
                    GameTemplateId.New(),
                    0,
                    Array.Empty<CardInstanceState>(),
                    Array.Empty<PawnState>(),
                    Array.Empty<TokenState>(),
                    Array.Empty<ContainerState>(),
                    Array.Empty<SeatState>(),
                    (IEnumerable<ContainerPlacementState>)null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_WhenPlacementItemIsNull_ThrowsArgumentException()
        {
            Assert.That(
                () => CreateMatch(containerPlacements: new ContainerPlacementState[] { null }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenPlacementIdIsDuplicate_ThrowsArgumentException()
        {
            ContainerState deck = CreateContainer(kind: ContainerKind.Deck);
            ContainerPlacementState firstPlacement = CreatePlacement(deck.Id);
            ContainerPlacementState secondPlacement = CreatePlacement(deck.Id, x: 2);

            Assert.That(
                () => CreateMatch(
                    containers: new[] { deck },
                    containerPlacements: new[] { firstPlacement, secondPlacement }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenDictionaryKeyDoesNotMatchPlacementId_ThrowsArgumentException()
        {
            ContainerState deck = CreateContainer(kind: ContainerKind.Deck);
            ContainerPlacementState placement = CreatePlacement(deck.Id);
            Dictionary<ContainerId, ContainerPlacementState> placements =
                new Dictionary<ContainerId, ContainerPlacementState>
                {
                    { ContainerId.New(), placement }
                };

            Assert.That(
                () => CreateMatch(
                    containers: new[] { deck },
                    containerPlacementDictionary: placements),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenDictionaryPlacementItemIsNull_ThrowsArgumentException()
        {
            ContainerId containerId = ContainerId.New();
            Dictionary<ContainerId, ContainerPlacementState> placements =
                new Dictionary<ContainerId, ContainerPlacementState>
                {
                    { containerId, null }
                };

            Assert.That(
                () => CreateMatch(containerPlacementDictionary: placements),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenPlacementReferencesMissingContainer_ThrowsArgumentException()
        {
            ContainerPlacementState placement = CreatePlacement(ContainerId.New());

            Assert.That(
                () => CreateMatch(containerPlacements: new[] { placement }),
                Throws.ArgumentException);
        }

        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.ConsoleSlot)]
        [TestCase(ContainerKind.Generic)]
        public void Constructor_WhenPlacementKindIsNotAllowed_ThrowsArgumentException(ContainerKind kind)
        {
            ContainerState container = CreateContainer(kind: kind);
            ContainerPlacementState placement = CreatePlacement(container.Id);

            Assert.That(
                () => CreateMatch(
                    containers: new[] { container },
                    containerPlacements: new[] { placement }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenConstructionFails_DoesNotMutateSuppliedStates()
        {
            ContainerState hand = CreateContainer(kind: ContainerKind.Hand);
            ContainerPlacementState placement = CreatePlacement(hand.Id, x: 7, y: 8, rotationDegrees: 9);
            TabletopPose originalPose = placement.Pose;

            Assert.That(
                () => CreateMatch(
                    containers: new[] { hand },
                    containerPlacements: new[] { placement }),
                Throws.ArgumentException);

            Assert.That(hand.Kind, Is.EqualTo(ContainerKind.Hand));
            Assert.That(hand.Count, Is.EqualTo(0));
            Assert.That(placement.Pose, Is.EqualTo(originalPose));
        }

        [Test]
        public void Constructor_WhenObjectAndContainerValidationFails_StillThrowsWithPlacementSupport()
        {
            ContainerState deck = CreateContainer(kind: ContainerKind.Deck);
            CardInstanceState card = CreateCard(containerId: deck.Id);
            ContainerPlacementState placement = CreatePlacement(deck.Id);

            Assert.That(
                () => CreateMatch(
                    cards: new[] { card },
                    containers: new[] { deck },
                    containerPlacements: new[] { placement }),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenSeatValidationFails_StillThrowsWithPlacementSupport()
        {
            ContainerState deck = CreateContainer(kind: ContainerKind.Deck);
            ContainerPlacementState placement = CreatePlacement(deck.Id);
            SeatId seatId = SeatId.New();
            SeatState seat = new SeatState(
                seatId,
                TabletopPose.Default,
                ContainerId.New(),
                new ConsoleState(seatId, Array.Empty<ContainerId>()),
                PlayerId.Empty,
                SeatStatus.Vacant);

            Assert.That(
                () => CreateMatch(
                    containers: new[] { deck },
                    seats: new[] { seat },
                    containerPlacements: new[] { placement }),
                Throws.ArgumentException);
        }

        private static MatchState CreateMatch(
            IEnumerable<CardInstanceState> cards = null,
            IEnumerable<ContainerState> containers = null,
            IEnumerable<SeatState> seats = null,
            IEnumerable<ContainerPlacementState> containerPlacements = null,
            IReadOnlyDictionary<ContainerId, ContainerPlacementState> containerPlacementDictionary = null)
        {
            if (containerPlacementDictionary != null)
            {
                return new MatchState(
                    MatchId.New(),
                    GameTemplateId.New(),
                    0,
                    cards ?? Array.Empty<CardInstanceState>(),
                    Array.Empty<PawnState>(),
                    Array.Empty<TokenState>(),
                    containers ?? Array.Empty<ContainerState>(),
                    seats ?? Array.Empty<SeatState>(),
                    containerPlacementDictionary);
            }

            return new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                0,
                cards ?? Array.Empty<CardInstanceState>(),
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                containers ?? Array.Empty<ContainerState>(),
                seats ?? Array.Empty<SeatState>(),
                containerPlacements ?? Array.Empty<ContainerPlacementState>());
        }

        private static ContainerState CreateContainer(
            ContainerKind kind,
            ContainerId? id = null)
        {
            return new ContainerState(
                id ?? ContainerId.New(),
                kind,
                SeatId.Empty,
                ObjectVisibility.Public,
                0);
        }

        private static ContainerPlacementState CreatePlacement(
            ContainerId containerId,
            double x = 1,
            double y = 2,
            float rotationDegrees = 3)
        {
            return new ContainerPlacementState(
                containerId,
                new TabletopPose(
                    new TableCoordinate(x, y),
                    rotationDegrees,
                    0,
                    0));
        }

        private static CardInstanceState CreateCard(ContainerId containerId)
        {
            return new CardInstanceState(
                new TabletopObjectState(
                    TabletopObjectId.New(),
                    ObjectDefinitionId.New(),
                    TabletopObjectKind.Card,
                    TabletopPose.Default,
                    containerId,
                    PlayerId.Empty,
                    ObjectVisibility.Public,
                    false),
                CardFace.FaceDown);
        }
    }
}
