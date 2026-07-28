using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class MatchStateContainerAdditionTests
    {
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.DiscardPile)]
        public void AddEmptyPlacedContainer_WhenKindIsAllowed_AddsExactInstances(ContainerKind kind)
        {
            MatchState match = CreateSeatMatch(out ContainerState hand, out ContainerState slot);
            ContainerState container = CreateContainer(kind);
            ContainerPlacementState placement = CreatePlacement(container.Id);

            match.AddEmptyPlacedContainer(container, placement);

            Assert.That(match.Containers[container.Id], Is.SameAs(container));
            Assert.That(match.ContainerPlacements[container.Id], Is.SameAs(placement));
            Assert.That(match.Containers[hand.Id], Is.SameAs(hand));
            Assert.That(match.Containers[slot.Id], Is.SameAs(slot));
        }

        [Test]
        public void AddEmptyPlacedContainer_WhenContainerIsNull_ThrowsArgumentNullException()
        {
            MatchState match = CreateMatch();

            Assert.That(
                () => match.AddEmptyPlacedContainer(null, CreatePlacement(ContainerId.New())),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void AddEmptyPlacedContainer_WhenPlacementIsNull_ThrowsArgumentNullException()
        {
            MatchState match = CreateMatch();

            Assert.That(
                () => match.AddEmptyPlacedContainer(CreateContainer(ContainerKind.Stack), null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void AddEmptyPlacedContainer_WhenContainerIdIsEmpty_ThrowsArgumentException()
        {
            MatchState match = CreateMatch();
            ContainerState container = new EmptyIdContainerBuilder().Create();

            Assert.That(
                () => match.AddEmptyPlacedContainer(container, CreatePlacement(ContainerId.New())),
                Throws.ArgumentException);
        }

        [Test]
        public void AddEmptyPlacedContainer_WhenPlacementIdDiffers_ThrowsArgumentException()
        {
            MatchState match = CreateMatch();
            ContainerState container = CreateContainer(ContainerKind.Stack);
            ContainerPlacementState placement = CreatePlacement(ContainerId.New());

            Assert.That(
                () => match.AddEmptyPlacedContainer(container, placement),
                Throws.ArgumentException);
        }

        [Test]
        public void AddEmptyPlacedContainer_WhenContainerAlreadyExists_ThrowsArgumentException()
        {
            ContainerState existing = CreateContainer(ContainerKind.Stack);
            MatchState match = CreateMatch(new[] { existing });
            ContainerPlacementState placement = CreatePlacement(existing.Id);

            Assert.That(
                () => match.AddEmptyPlacedContainer(existing, placement),
                Throws.ArgumentException);
        }

        [Test]
        public void AddEmptyPlacedContainer_WhenPlacementAlreadyExists_ThrowsArgumentException()
        {
            ContainerState existing = CreateContainer(ContainerKind.Stack);
            ContainerPlacementState existingPlacement = CreatePlacement(existing.Id);
            MatchState match = CreateMatch(new[] { existing }, new[] { existingPlacement });
            ContainerState duplicate = CreateContainer(ContainerKind.Stack, id: existing.Id);
            ContainerPlacementState duplicatePlacement = CreatePlacement(existing.Id);

            Assert.That(
                () => match.AddEmptyPlacedContainer(duplicate, duplicatePlacement),
                Throws.ArgumentException);
        }

        [Test]
        public void AddEmptyPlacedContainer_WhenContainerIsNonEmpty_ThrowsInvalidOperationException()
        {
            MatchState match = CreateMatch();
            ContainerState stack = CreateContainer(ContainerKind.Stack);
            CardInstanceState card = CreateCard();
            new ContainerTransferService().PlaceIntoContainer(card.BaseState, stack);

            Assert.That(
                () => match.AddEmptyPlacedContainer(stack, CreatePlacement(stack.Id)),
                Throws.TypeOf<InvalidOperationException>());
        }

        [TestCase(ContainerKind.Generic)]
        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.ConsoleSlot)]
        public void AddEmptyPlacedContainer_WhenKindIsRejected_ThrowsArgumentException(ContainerKind kind)
        {
            SeatId seatId = SeatId.New();
            MatchState match = CreateMatch();
            ContainerState container = CreateContainer(
                kind,
                ownerSeatId: kind == ContainerKind.Hand || kind == ContainerKind.ConsoleSlot ? seatId : SeatId.Empty);

            Assert.That(
                () => match.AddEmptyPlacedContainer(container, CreatePlacement(container.Id)),
                Throws.ArgumentException);
        }

        [Test]
        public void AddEmptyPlacedContainer_WhenFailureOccurs_PreservesDictionariesAndRevision()
        {
            ContainerState existing = CreateContainer(ContainerKind.Stack);
            ContainerPlacementState existingPlacement = CreatePlacement(existing.Id);
            MatchState match = CreateSeatMatch(
                out ContainerState hand,
                out ContainerState slot,
                additionalContainers: new[] { existing },
                placements: new[] { existingPlacement },
                revision: 9);
            ContainerState rejected = CreateContainer(ContainerKind.Generic);
            ContainerPlacementState rejectedPlacement = CreatePlacement(rejected.Id);

            Assert.That(
                () => match.AddEmptyPlacedContainer(rejected, rejectedPlacement),
                Throws.ArgumentException);

            Assert.That(match.Revision, Is.EqualTo(9));
            Assert.That(match.Containers.Keys, Is.EquivalentTo(new[] { hand.Id, slot.Id, existing.Id }));
            Assert.That(match.ContainerPlacements.Keys, Is.EquivalentTo(new[] { existing.Id }));
            Assert.That(match.Containers[existing.Id], Is.SameAs(existing));
            Assert.That(match.ContainerPlacements[existing.Id], Is.SameAs(existingPlacement));
        }

        [Test]
        public void AddEmptyPlacedContainer_WhenSuccessful_PreservesOtherStateIdentities()
        {
            MatchState match = CreateSeatMatch(out ContainerState hand, out ContainerState slot);
            SeatState seat = match.Seats[hand.OwnerSeatId];
            ContainerState stack = CreateContainer(ContainerKind.Stack);
            ContainerPlacementState placement = CreatePlacement(stack.Id);

            match.AddEmptyPlacedContainer(stack, placement);

            Assert.That(match.Revision, Is.Zero);
            Assert.That(match.Containers[hand.Id], Is.SameAs(hand));
            Assert.That(match.Containers[slot.Id], Is.SameAs(slot));
            Assert.That(match.ContainerPlacements[stack.Id], Is.SameAs(placement));
            Assert.That(match.Seats[seat.Id], Is.SameAs(seat));
            Assert.That(match.Objects().Count(), Is.Zero);
        }

        private static MatchState CreateMatch(
            IEnumerable<ContainerState> containers = null,
            IEnumerable<ContainerPlacementState> placements = null,
            long revision = 0)
        {
            return new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                Array.Empty<CardInstanceState>(),
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
                Array.Empty<CardInstanceState>(),
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                containers,
                new[] { seat },
                placements ?? Array.Empty<ContainerPlacementState>());
        }

        private static ContainerState CreateContainer(
            ContainerKind kind,
            SeatId? ownerSeatId = null,
            ContainerId? id = null)
        {
            return new ContainerState(
                id ?? ContainerId.New(),
                kind,
                ownerSeatId ?? SeatId.Empty,
                ObjectVisibility.Public,
                0);
        }

        private static ContainerPlacementState CreatePlacement(ContainerId containerId)
        {
            return new ContainerPlacementState(containerId, CreatePose(x: 1.0, y: 2.0, rotationDegrees: -450f));
        }

        private static CardInstanceState CreateCard()
        {
            return new CardInstanceState(
                new TabletopObjectState(
                    TabletopObjectId.New(),
                    ObjectDefinitionId.New(),
                    TabletopObjectKind.Card,
                    CreatePose(),
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

        private sealed class EmptyIdContainerBuilder
        {
            public ContainerState Create()
            {
                return (ContainerState)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ContainerState));
            }
        }
    }

    internal static class MatchStateObjectTestExtensions
    {
        public static IEnumerable<TabletopObjectState> Objects(this MatchState match)
        {
            return match.Cards.Values.Select(card => card.BaseState)
                .Concat(match.Pawns.Values.Select(pawn => pawn.BaseState))
                .Concat(match.Tokens.Values.Select(token => token.BaseState));
        }
    }
}
