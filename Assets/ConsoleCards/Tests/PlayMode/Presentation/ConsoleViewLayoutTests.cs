using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class ConsoleViewLayoutTests
    {
        private const float Tolerance = 0.0001f;

        private readonly List<GameObject> createdGameObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdGameObjects.Count; i++)
            {
                if (createdGameObjects[i] != null)
                {
                    UnityObject.DestroyImmediate(createdGameObjects[i]);
                }
            }

            createdGameObjects.Clear();
        }

        [Test]
        public void ConsoleSlotView_WhenKindIsConsoleSlot_AcceptsExactStateInstance()
        {
            SlotFixture fixture = CreateSlotFixture(1);

            fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews);

            Assert.That(fixture.View.IsBound, Is.True);
            Assert.That(fixture.View.ContainerId, Is.EqualTo(fixture.Container.Id));
            Assert.That(fixture.View.ContainerState, Is.SameAs(fixture.Container));
            Assert.That(fixture.View.LayoutAnchor, Is.SameAs(fixture.Anchor));
            Assert.That(fixture.View.VisibleCardCount, Is.EqualTo(1));
        }

        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.Generic)]
        public void ConsoleSlotView_WhenKindIsWrong_Rejects(ContainerKind kind)
        {
            SlotFixture fixture = CreateSlotFixture(1, kind);

            Assert.Throws<ArgumentException>(
                () => fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews));
        }

        [Test]
        public void ConsoleSlotView_EmptyAndCapacityOneSlotsAreValid()
        {
            SlotFixture empty = CreateSlotFixture(0, capacity: 1);
            SlotFixture one = CreateSlotFixture(1, capacity: 1);

            empty.View.Bind(empty.Container, empty.Anchor, empty.Converter, empty.CardViews);
            one.View.Bind(one.Container, one.Anchor, one.Converter, one.CardViews);

            Assert.That(empty.View.VisibleCardCount, Is.EqualTo(0));
            AssertVector3(one.CardViews[0].transform.position, one.Anchor.position);
        }

        [Test]
        public void ConsoleSlotView_MultiMemberSlotUsesBottomToTopVerticalOrder()
        {
            SlotFixture fixture = CreateSlotFixture(3, capacity: 3);
            fixture.View.VerticalOffset = 0.1f;

            fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews);

            Assert.That(fixture.CardViews[1].transform.position.y, Is.GreaterThan(fixture.CardViews[0].transform.position.y));
            Assert.That(fixture.CardViews[2].transform.position.y, Is.GreaterThan(fixture.CardViews[1].transform.position.y));
        }

        [Test]
        public void ConsoleSlotView_DoesNotMutateRuntimeStateOrMetadata()
        {
            SlotFixture fixture = CreateSlotFixture(1, capacity: 2);
            SeatId owner = fixture.Container.OwnerSeatId;
            ObjectVisibility visibility = fixture.Container.Visibility;
            int capacity = fixture.Container.Capacity;
            TabletopPose cardPose = fixture.Cards[0].BaseState.Pose;

            fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews);
            fixture.View.ApplyAcceptedLayout();

            Assert.That(fixture.Container.OwnerSeatId, Is.EqualTo(owner));
            Assert.That(fixture.Container.Visibility, Is.EqualTo(visibility));
            Assert.That(fixture.Container.Capacity, Is.EqualTo(capacity));
            Assert.That(fixture.Cards[0].BaseState.Pose, Is.EqualTo(cardPose));
        }

        [Test]
        public void ConsoleSlotView_MissingOrDuplicateCardView_IsRejected()
        {
            SlotFixture missing = CreateSlotFixture(2);
            missing.CardViews.RemoveAt(1);
            SlotFixture duplicate = CreateSlotFixture(2);
            duplicate.CardViews[1] = duplicate.CardViews[0];

            Assert.Throws<KeyNotFoundException>(
                () => missing.View.Bind(missing.Container, missing.Anchor, missing.Converter, missing.CardViews));
            Assert.Throws<ArgumentException>(
                () => duplicate.View.Bind(duplicate.Container, duplicate.Anchor, duplicate.Converter, duplicate.CardViews));
        }

        [Test]
        public void ConsoleView_BindPreservesExactConsoleAndSlotInstances()
        {
            ConsoleFixture fixture = CreateConsoleFixture(3);

            fixture.BindSlots();
            fixture.ConsoleView.Bind(fixture.Console, fixture.ConsoleAnchor, fixture.SlotViews);

            Assert.That(fixture.ConsoleView.IsBound, Is.True);
            Assert.That(fixture.ConsoleView.ConsoleState, Is.SameAs(fixture.Console));
            Assert.That(fixture.ConsoleView.VisibleSlotCount, Is.EqualTo(3));
            Assert.That(fixture.SlotViews[0].ContainerState, Is.SameAs(fixture.SlotContainers[0]));
        }

        [Test]
        public void ConsoleView_SlotOrderFollowsConsoleStateAndRowIsCentered()
        {
            ConsoleFixture fixture = CreateConsoleFixture(3);
            fixture.BindSlots();
            fixture.ConsoleView.SlotSpacing = 2f;

            fixture.ConsoleView.Bind(fixture.Console, fixture.ConsoleAnchor, fixture.SlotViews);

            Assert.That(fixture.SlotViews[0].LayoutAnchor.position.x, Is.EqualTo(-2f).Within(Tolerance));
            Assert.That(fixture.SlotViews[1].LayoutAnchor.position.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(fixture.SlotViews[2].LayoutAnchor.position.x, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void ConsoleView_WhenSlotViewMissing_Rejects()
        {
            ConsoleFixture fixture = CreateConsoleFixture(2);
            fixture.BindSlots();
            fixture.SlotViews.RemoveAt(1);

            Assert.Throws<ArgumentException>(
                () => fixture.ConsoleView.Bind(fixture.Console, fixture.ConsoleAnchor, fixture.SlotViews));
        }

        [Test]
        public void ConsoleView_WhenSlotViewDuplicate_Rejects()
        {
            ConsoleFixture fixture = CreateConsoleFixture(2);
            fixture.BindSlots();
            fixture.SlotViews[1] = fixture.SlotViews[0];

            Assert.Throws<ArgumentException>(
                () => fixture.ConsoleView.Bind(fixture.Console, fixture.ConsoleAnchor, fixture.SlotViews));
        }

        [Test]
        public void ConsoleView_WhenSlotOrderMismatches_Rejects()
        {
            ConsoleFixture fixture = CreateConsoleFixture(2);
            fixture.BindSlots();
            ConsoleSlotView first = fixture.SlotViews[0];
            fixture.SlotViews[0] = fixture.SlotViews[1];
            fixture.SlotViews[1] = first;

            Assert.Throws<ArgumentException>(
                () => fixture.ConsoleView.Bind(fixture.Console, fixture.ConsoleAnchor, fixture.SlotViews));
        }

        [Test]
        public void ConsoleView_FailedBindPreservesPriorConfiguration()
        {
            ConsoleFixture fixture = CreateConsoleFixture(2);
            fixture.BindSlots();
            fixture.ConsoleView.Bind(fixture.Console, fixture.ConsoleAnchor, fixture.SlotViews);
            Vector3 firstSlotPosition = fixture.SlotViews[0].LayoutAnchor.position;
            ConsoleState previousConsole = fixture.ConsoleView.ConsoleState;
            List<ConsoleSlotView> invalidSlots = new List<ConsoleSlotView>(fixture.SlotViews) { fixture.SlotViews[0] };

            Assert.Throws<ArgumentException>(
                () => fixture.ConsoleView.Bind(fixture.Console, fixture.ConsoleAnchor, invalidSlots));

            Assert.That(fixture.ConsoleView.ConsoleState, Is.SameAs(previousConsole));
            AssertVector3(fixture.SlotViews[0].LayoutAnchor.position, firstSlotPosition);
        }

        [Test]
        public void ConsoleView_DoesNotMutateConsoleSlotsOrCardMembership()
        {
            ConsoleFixture fixture = CreateConsoleFixture(2, cardsPerSlot: 1);
            IReadOnlyList<ContainerId> slotOrder = new List<ContainerId>(fixture.Console.SlotContainerIds);
            TabletopObjectId cardId = fixture.Cards[0].BaseState.Id;
            ContainerId cardContainerId = fixture.Cards[0].BaseState.ContainerId;
            fixture.BindSlots();

            fixture.ConsoleView.Bind(fixture.Console, fixture.ConsoleAnchor, fixture.SlotViews);
            fixture.ConsoleView.ApplyAcceptedLayout();

            Assert.That(fixture.Console.SlotContainerIds, Is.EqualTo(slotOrder));
            Assert.That(fixture.Cards[0].BaseState.Id, Is.EqualTo(cardId));
            Assert.That(fixture.Cards[0].BaseState.ContainerId, Is.EqualTo(cardContainerId));
            Assert.That(fixture.SlotContainers[0].ObjectIds, Does.Contain(cardId));
        }

        [Test]
        public void ConsoleView_UnbindClearsConfigurationWithoutMutatingSlots()
        {
            ConsoleFixture fixture = CreateConsoleFixture(1, cardsPerSlot: 1);
            fixture.BindSlots();
            fixture.ConsoleView.Bind(fixture.Console, fixture.ConsoleAnchor, fixture.SlotViews);

            fixture.ConsoleView.Unbind();

            Assert.That(fixture.ConsoleView.IsBound, Is.False);
            Assert.That(fixture.SlotContainers[0].Count, Is.EqualTo(1));
            Assert.That(fixture.Cards[0].BaseState.ContainerId, Is.EqualTo(fixture.SlotContainers[0].Id));
        }

        private ConsoleFixture CreateConsoleFixture(int slotCount, int cardsPerSlot = 0)
        {
            SeatId seatId = SeatId.New();
            List<ContainerState> slotContainers = new List<ContainerState>();
            List<ConsoleSlotView> slotViews = new List<ConsoleSlotView>();
            List<Transform> slotAnchors = new List<Transform>();
            List<CardInstanceState> cards = new List<CardInstanceState>();
            List<List<CardView>> cardViewsBySlot = new List<List<CardView>>();
            ContainerTransferService transferService = new ContainerTransferService();

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                ContainerState slot = new ContainerState(
                    ContainerId.New(),
                    ContainerKind.ConsoleSlot,
                    seatId,
                    ObjectVisibility.OwnerOnly,
                    0);
                List<CardView> cardViews = new List<CardView>();
                for (int cardIndex = 0; cardIndex < cardsPerSlot; cardIndex++)
                {
                    int seed = 50 + (slotIndex * 10) + cardIndex;
                    CardInstanceState card = CreateCard(seed);
                    transferService.PlaceIntoContainer(card.BaseState, slot);
                    CardView cardView = CreateGameObject($"Slot{slotIndex}Card{cardIndex}").AddComponent<CardView>();
                    cardView.Bind(card, CreateConverter());
                    cards.Add(card);
                    cardViews.Add(cardView);
                }

                slotContainers.Add(slot);
                slotAnchors.Add(CreateGameObject($"Slot{slotIndex}Anchor").transform);
                slotViews.Add(CreateGameObject($"Slot{slotIndex}View").AddComponent<ConsoleSlotView>());
                cardViewsBySlot.Add(cardViews);
            }

            ConsoleState console = new ConsoleState(seatId, slotContainers.ConvertAll(container => container.Id));
            ConsoleView consoleView = CreateGameObject("ConsoleView").AddComponent<ConsoleView>();
            Transform consoleAnchor = CreateGameObject("ConsoleAnchor").transform;

            return new ConsoleFixture(
                console,
                consoleView,
                consoleAnchor,
                slotContainers,
                slotViews,
                slotAnchors,
                cards,
                cardViewsBySlot,
                CreateConverter());
        }

        private SlotFixture CreateSlotFixture(
            int cardCount,
            ContainerKind kind = ContainerKind.ConsoleSlot,
            int capacity = 0)
        {
            SeatId seatId = SeatId.New();
            ContainerState container = new ContainerState(
                ContainerId.New(),
                kind,
                seatId,
                ObjectVisibility.OwnerOnly,
                capacity);
            Transform anchor = CreateGameObject("SlotAnchor").transform;
            ConsoleSlotView view = CreateGameObject("ConsoleSlotView").AddComponent<ConsoleSlotView>();
            List<CardInstanceState> cards = new List<CardInstanceState>();
            List<CardView> cardViews = new List<CardView>();
            ContainerTransferService transferService = new ContainerTransferService();

            for (int i = 0; i < cardCount; i++)
            {
                CardInstanceState card = CreateCard(i + 40);
                transferService.PlaceIntoContainer(card.BaseState, container);
                CardView cardView = CreateGameObject($"Card{i}").AddComponent<CardView>();
                cardView.Bind(card, CreateConverter());
                cards.Add(card);
                cardViews.Add(cardView);
            }

            return new SlotFixture(container, anchor, view, cards, cardViews, CreateConverter());
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private static CardInstanceState CreateCard(int seed)
        {
            return new CardInstanceState(
                new TabletopObjectState(
                    new TabletopObjectId(GuidFromSeed(seed)),
                    new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                    TabletopObjectKind.Card,
                    new TabletopPose(new TableCoordinate(seed, seed + 1.0), 0f, 0, 0),
                    ContainerId.Empty,
                    PlayerId.Empty,
                    ObjectVisibility.OwnerOnly,
                    false),
                CardFace.FaceUp);
        }

        private static TabletopCoordinateConverter CreateConverter()
        {
            return new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        private sealed class SlotFixture
        {
            public SlotFixture(
                ContainerState container,
                Transform anchor,
                ConsoleSlotView view,
                List<CardInstanceState> cards,
                List<CardView> cardViews,
                TabletopCoordinateConverter converter)
            {
                Container = container;
                Anchor = anchor;
                View = view;
                Cards = cards;
                CardViews = cardViews;
                Converter = converter;
            }

            public ContainerState Container { get; }

            public Transform Anchor { get; }

            public ConsoleSlotView View { get; }

            public List<CardInstanceState> Cards { get; }

            public List<CardView> CardViews { get; }

            public TabletopCoordinateConverter Converter { get; }
        }

        private sealed class ConsoleFixture
        {
            public ConsoleFixture(
                ConsoleState console,
                ConsoleView consoleView,
                Transform consoleAnchor,
                List<ContainerState> slotContainers,
                List<ConsoleSlotView> slotViews,
                List<Transform> slotAnchors,
                List<CardInstanceState> cards,
                List<List<CardView>> cardViewsBySlot,
                TabletopCoordinateConverter converter)
            {
                Console = console;
                ConsoleView = consoleView;
                ConsoleAnchor = consoleAnchor;
                SlotContainers = slotContainers;
                SlotViews = slotViews;
                SlotAnchors = slotAnchors;
                Cards = cards;
                CardViewsBySlot = cardViewsBySlot;
                Converter = converter;
            }

            public ConsoleState Console { get; }

            public ConsoleView ConsoleView { get; }

            public Transform ConsoleAnchor { get; }

            public List<ContainerState> SlotContainers { get; }

            public List<ConsoleSlotView> SlotViews { get; }

            public List<Transform> SlotAnchors { get; }

            public List<CardInstanceState> Cards { get; }

            public List<List<CardView>> CardViewsBySlot { get; }

            public TabletopCoordinateConverter Converter { get; }

            public void BindSlots()
            {
                for (int i = 0; i < SlotViews.Count; i++)
                {
                    SlotViews[i].Bind(SlotContainers[i], SlotAnchors[i], Converter, CardViewsBySlot[i]);
                }
            }
        }
    }
}
