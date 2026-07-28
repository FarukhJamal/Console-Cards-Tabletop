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
    public sealed class MatchStateContainerRemovalTests
    {
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.DiscardPile)]
        public void RemoveEmptyContainer_WhenContainerIsRemovable_RemovesAndReturnsExactInstance(ContainerKind kind)
        {
            ContainerState container = CreateContainer(kind);
            ContainerPlacementState placement = CreatePlacement(container.Id);
            MatchState match = CreateMatch(new[] { container }, new[] { placement });

            ContainerState removed = match.RemoveEmptyContainer(container.Id);

            Assert.That(removed, Is.SameAs(container));
            Assert.That(match.Containers.ContainsKey(container.Id), Is.False);
            Assert.That(match.ContainerPlacements.ContainsKey(container.Id), Is.False);
        }

        [Test]
        public void RemoveEmptyContainer_WhenPlacementIsMissing_RemovesContainer()
        {
            ContainerState stack = CreateContainer(ContainerKind.Stack);
            MatchState match = CreateMatch(new[] { stack });

            ContainerState removed = match.RemoveEmptyContainer(stack.Id);

            Assert.That(removed, Is.SameAs(stack));
            Assert.That(match.Containers.ContainsKey(stack.Id), Is.False);
            Assert.That(match.ContainerPlacements, Is.Empty);
        }

        [Test]
        public void RemoveEmptyContainer_WhenIdIsEmpty_ThrowsArgumentException()
        {
            MatchState match = CreateMatch();

            Assert.That(() => match.RemoveEmptyContainer(ContainerId.Empty), Throws.ArgumentException);
        }

        [Test]
        public void RemoveEmptyContainer_WhenContainerIsMissing_ThrowsKeyNotFoundException()
        {
            MatchState match = CreateMatch();

            Assert.That(() => match.RemoveEmptyContainer(ContainerId.New()), Throws.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void RemoveEmptyContainer_WhenContainerIsNonEmpty_ThrowsInvalidOperationException()
        {
            ContainerState stack = CreateContainer(ContainerKind.Stack);
            CardInstanceState card = CreateCard();
            new ContainerTransferService().PlaceIntoContainer(card.BaseState, stack);
            MatchState match = CreateMatch(new[] { stack }, cards: new[] { card });

            Assert.That(() => match.RemoveEmptyContainer(stack.Id), Throws.TypeOf<InvalidOperationException>());
            Assert.That(match.Containers[stack.Id], Is.SameAs(stack));
            Assert.That(match.Containers[stack.Id].ObjectIds, Is.EqualTo(new[] { card.BaseState.Id }));
        }

        [Test]
        public void RemoveEmptyContainer_WhenSeatHandIsReferenced_ThrowsInvalidOperationException()
        {
            MatchState match = CreateSeatMatch(out ContainerState hand, out _);

            Assert.That(() => match.RemoveEmptyContainer(hand.Id), Throws.TypeOf<InvalidOperationException>());
            Assert.That(match.Containers[hand.Id], Is.SameAs(hand));
        }

        [Test]
        public void RemoveEmptyContainer_WhenConsoleSlotIsReferenced_ThrowsInvalidOperationException()
        {
            MatchState match = CreateSeatMatch(out _, out ContainerState slot);

            Assert.That(() => match.RemoveEmptyContainer(slot.Id), Throws.TypeOf<InvalidOperationException>());
            Assert.That(match.Containers[slot.Id], Is.SameAs(slot));
        }

        [Test]
        public void RemoveEmptyContainer_WhenFailureOccurs_PreservesContainersPlacementsAndRevision()
        {
            ContainerState stack = CreateContainer(ContainerKind.Stack);
            ContainerPlacementState placement = CreatePlacement(stack.Id);
            MatchState match = CreateMatch(new[] { stack }, new[] { placement }, revision: 6);

            Assert.That(() => match.RemoveEmptyContainer(ContainerId.New()), Throws.TypeOf<KeyNotFoundException>());

            Assert.That(match.Revision, Is.EqualTo(6));
            Assert.That(match.Containers[stack.Id], Is.SameAs(stack));
            Assert.That(match.ContainerPlacements[stack.Id], Is.SameAs(placement));
        }

        [Test]
        public void RemoveEmptyContainer_WhenSuccessful_PreservesOtherStateIdentities()
        {
            ContainerState removedStack = CreateContainer(ContainerKind.Stack);
            ContainerState remainingStack = CreateContainer(ContainerKind.Stack);
            ContainerPlacementState remainingPlacement = CreatePlacement(remainingStack.Id);
            CardInstanceState card = CreateCard();
            new ContainerTransferService().PlaceIntoContainer(card.BaseState, remainingStack);
            MatchState match = CreateSeatMatch(
                out ContainerState hand,
                out ContainerState slot,
                additionalContainers: new[] { removedStack, remainingStack },
                placements: new[] { remainingPlacement },
                cards: new[] { card },
                revision: 10);
            SeatState seat = match.Seats[hand.OwnerSeatId];

            match.RemoveEmptyContainer(removedStack.Id);

            Assert.That(match.Revision, Is.EqualTo(10));
            Assert.That(match.Containers[remainingStack.Id], Is.SameAs(remainingStack));
            Assert.That(match.ContainerPlacements[remainingStack.Id], Is.SameAs(remainingPlacement));
            Assert.That(match.Cards[card.BaseState.Id], Is.SameAs(card));
            Assert.That(match.Seats[seat.Id], Is.SameAs(seat));
            Assert.That(match.Containers[hand.Id], Is.SameAs(hand));
            Assert.That(match.Containers[slot.Id], Is.SameAs(slot));
        }

        private static MatchState CreateMatch(
            IEnumerable<ContainerState> containers = null,
            IEnumerable<ContainerPlacementState> placements = null,
            IEnumerable<CardInstanceState> cards = null,
            long revision = 0)
        {
            return new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                cards ?? Array.Empty<CardInstanceState>(),
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                containers ?? Array.Empty<ContainerState>(),
                Array.Empty<SeatState>(),
                placements ?? Array.Empty<ContainerPlacementState>());
        }

        private static MatchState CreateSeatMatch(
            out ContainerState hand,
            out ContainerState slot,
            IEnumerable<ContainerState> additionalContainers = null,
            IEnumerable<ContainerPlacementState> placements = null,
            IEnumerable<CardInstanceState> cards = null,
            long revision = 0)
        {
            SeatId seatId = SeatId.New();
            hand = CreateContainer(ContainerKind.Hand, seatId);
            slot = CreateContainer(ContainerKind.ConsoleSlot, seatId);
            List<ContainerState> containers = new List<ContainerState> { hand, slot };

            if (additionalContainers != null)
            {
                containers.AddRange(additionalContainers);
            }

            SeatState seat = new SeatState(
                seatId,
                CreatePose(),
                hand.Id,
                new ConsoleState(seatId, new[] { slot.Id }),
                PlayerId.Empty,
                SeatStatus.Vacant);

            return new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                cards ?? Array.Empty<CardInstanceState>(),
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                containers,
                new[] { seat },
                placements ?? Array.Empty<ContainerPlacementState>());
        }

        private static ContainerState CreateContainer(ContainerKind kind, SeatId? ownerSeatId = null)
        {
            return new ContainerState(
                ContainerId.New(),
                kind,
                ownerSeatId ?? SeatId.Empty,
                ObjectVisibility.Public,
                0);
        }

        private static ContainerPlacementState CreatePlacement(ContainerId containerId)
        {
            return new ContainerPlacementState(containerId, CreatePose(x: 1.0, y: 2.0, rotationDegrees: 3f));
        }

        private static CardInstanceState CreateCard()
        {
            return new CardInstanceState(
                new TabletopObjectState(
                    TabletopObjectId.New(),
                    ObjectDefinitionId.New(),
                    TabletopObjectKind.Card,
                    CreatePose(x: 4.0, y: 5.0, rotationDegrees: 6f),
                    ContainerId.Empty,
                    PlayerId.Empty,
                    ObjectVisibility.Public,
                    false),
                CardFace.FaceDown);
        }

        private static TabletopPose CreatePose(
            double x = 0.0,
            double y = 0.0,
            float rotationDegrees = 0f)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotationDegrees, 0, 0);
        }
    }
}
