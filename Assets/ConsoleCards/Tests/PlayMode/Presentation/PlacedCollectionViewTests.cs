using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
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
    public sealed class PlacedCollectionViewTests
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

        [TestCase(PlacedCollectionKind.Deck)]
        [TestCase(PlacedCollectionKind.Stack)]
        [TestCase(PlacedCollectionKind.DiscardPile)]
        public void Bind_WhenKindIsCorrect_AcceptsExactStateInstances(PlacedCollectionKind viewKind)
        {
            PlacedFixture fixture = CreatePlacedFixture(viewKind, 3);

            fixture.Bind();

            Assert.That(fixture.IsBound, Is.True);
            Assert.That(fixture.ContainerId, Is.EqualTo(fixture.Container.Id));
            Assert.That(fixture.ContainerState, Is.SameAs(fixture.Container));
            Assert.That(fixture.PlacementState, Is.SameAs(fixture.Placement));
            Assert.That(fixture.VisibleCardCount, Is.EqualTo(3));
        }

        [TestCase(PlacedCollectionKind.Deck, ContainerKind.Stack)]
        [TestCase(PlacedCollectionKind.Stack, ContainerKind.Deck)]
        [TestCase(PlacedCollectionKind.DiscardPile, ContainerKind.Hand)]
        public void Bind_WhenKindIsWrong_Rejects(PlacedCollectionKind viewKind, ContainerKind containerKind)
        {
            PlacedFixture fixture = CreatePlacedFixture(viewKind, 1, containerKindOverride: containerKind);

            Assert.Throws<ArgumentException>(() => fixture.Bind());
        }

        [TestCase(PlacedCollectionKind.Deck)]
        [TestCase(PlacedCollectionKind.Stack)]
        [TestCase(PlacedCollectionKind.DiscardPile)]
        public void Bind_WhenPlacementIdMismatches_Rejects(PlacedCollectionKind viewKind)
        {
            PlacedFixture fixture = CreatePlacedFixture(viewKind, 1, placementIdOverride: ContainerId.New());

            Assert.Throws<ArgumentException>(() => fixture.Bind());
        }

        [TestCase(PlacedCollectionKind.Deck)]
        [TestCase(PlacedCollectionKind.Stack)]
        [TestCase(PlacedCollectionKind.DiscardPile)]
        public void Bind_WhenCardViewMissing_Rejects(PlacedCollectionKind viewKind)
        {
            PlacedFixture fixture = CreatePlacedFixture(viewKind, 2);
            fixture.CardViews.RemoveAt(1);

            Assert.Throws<KeyNotFoundException>(() => fixture.Bind());
        }

        [TestCase(PlacedCollectionKind.Deck)]
        [TestCase(PlacedCollectionKind.Stack)]
        [TestCase(PlacedCollectionKind.DiscardPile)]
        public void Bind_WhenCardViewIdsDuplicate_Rejects(PlacedCollectionKind viewKind)
        {
            PlacedFixture fixture = CreatePlacedFixture(viewKind, 2);
            fixture.CardViews[1] = fixture.CardViews[0];

            Assert.Throws<ArgumentException>(() => fixture.Bind());
        }

        [TestCase(PlacedCollectionKind.Deck)]
        [TestCase(PlacedCollectionKind.Stack)]
        [TestCase(PlacedCollectionKind.DiscardPile)]
        public void Bind_WhenObjectContainerMismatches_Rejects(PlacedCollectionKind viewKind)
        {
            PlacedFixture fixture = CreatePlacedFixture(viewKind, 1);
            fixture.Cards[0].BaseState.SetContainer(ContainerId.New());

            Assert.Throws<ArgumentException>(() => fixture.Bind());
        }

        [TestCase(PlacedCollectionKind.Deck)]
        [TestCase(PlacedCollectionKind.Stack)]
        [TestCase(PlacedCollectionKind.DiscardPile)]
        public void EmptyContainer_IsValid(PlacedCollectionKind viewKind)
        {
            PlacedFixture fixture = CreatePlacedFixture(viewKind, 0);

            fixture.Bind();

            Assert.That(fixture.VisibleCardCount, Is.EqualTo(0));
            Assert.That(fixture.IsBound, Is.True);
        }

        [TestCase(PlacedCollectionKind.Deck)]
        [TestCase(PlacedCollectionKind.Stack)]
        [TestCase(PlacedCollectionKind.DiscardPile)]
        public void ApplyAcceptedLayout_IsDeterministicAndDoesNotMutateRuntimeState(PlacedCollectionKind viewKind)
        {
            PlacedFixture fixture = CreatePlacedFixture(viewKind, 3);
            IReadOnlyList<TabletopObjectId> orderBefore = new List<TabletopObjectId>(fixture.Container.ObjectIds);
            TabletopPose placementPose = fixture.Placement.Pose;
            TabletopPose cardPose = fixture.Cards[0].BaseState.Pose;
            CardFace face = fixture.Cards[0].Face;
            fixture.Bind();
            Vector3 firstPosition = fixture.CardViews[0].transform.position;
            Quaternion firstRotation = fixture.CardViews[0].transform.rotation;

            fixture.ApplyAcceptedLayout();

            AssertVector3(fixture.CardViews[0].transform.position, firstPosition);
            Assert.That(Quaternion.Angle(firstRotation, fixture.CardViews[0].transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(fixture.Container.ObjectIds, Is.EqualTo(orderBefore));
            Assert.That(fixture.Placement.Pose, Is.EqualTo(placementPose));
            Assert.That(fixture.Cards[0].BaseState.Pose, Is.EqualTo(cardPose));
            Assert.That(fixture.Cards[0].Face, Is.EqualTo(face));
        }

        [Test]
        public void DeckView_IncreasingIndexIncreasesVerticalPositionAndTopCardIsHighest()
        {
            PlacedFixture fixture = CreatePlacedFixture(PlacedCollectionKind.Deck, 3);
            fixture.DeckView.CardThicknessOffset = 0.1f;

            fixture.Bind();

            Assert.That(fixture.CardViews[1].transform.position.y, Is.GreaterThan(fixture.CardViews[0].transform.position.y));
            Assert.That(fixture.CardViews[2].transform.position.y, Is.GreaterThan(fixture.CardViews[1].transform.position.y));
            Assert.That(fixture.CardViews[2].transform.position.y, Is.EqualTo(0.2f).Within(Tolerance));
            Assert.That(fixture.CardViews[0].transform.position.x, Is.EqualTo(fixture.CardViews[2].transform.position.x).Within(Tolerance));
            Assert.That(fixture.CardViews[0].transform.position.z, Is.EqualTo(fixture.CardViews[2].transform.position.z).Within(Tolerance));
        }

        [Test]
        public void StackView_IncreasingIndexAppliesVerticalAndTableOffset()
        {
            PlacedFixture fixture = CreatePlacedFixture(PlacedCollectionKind.Stack, 3);
            fixture.StackView.VerticalOffset = 0.1f;
            fixture.StackView.TableOffsetPerCard = 0.25f;

            fixture.Bind();

            Assert.That(fixture.CardViews[1].transform.position.x, Is.GreaterThan(fixture.CardViews[0].transform.position.x));
            Assert.That(fixture.CardViews[1].transform.position.z, Is.GreaterThan(fixture.CardViews[0].transform.position.z));
            Assert.That(fixture.CardViews[1].transform.position.y, Is.GreaterThan(fixture.CardViews[0].transform.position.y));
        }

        [Test]
        public void DiscardPileView_AppliesDeterministicDiagonalAndVerticalOffset()
        {
            PlacedFixture fixture = CreatePlacedFixture(PlacedCollectionKind.DiscardPile, 3);
            fixture.DiscardPileView.VerticalOffset = 0.1f;
            fixture.DiscardPileView.DiagonalTableOffsetPerCard = 0.25f;

            fixture.Bind();

            Assert.That(fixture.CardViews[2].transform.position.x, Is.GreaterThan(fixture.CardViews[1].transform.position.x));
            Assert.That(fixture.CardViews[2].transform.position.z, Is.LessThan(fixture.CardViews[1].transform.position.z));
            Assert.That(fixture.CardViews[2].transform.position.y, Is.GreaterThan(fixture.CardViews[1].transform.position.y));
        }

        [TestCase(PlacedCollectionKind.Deck)]
        [TestCase(PlacedCollectionKind.Stack)]
        [TestCase(PlacedCollectionKind.DiscardPile)]
        public void ReorderThenApplyAcceptedLayout_ChangesOnlyVisualOrder(PlacedCollectionKind viewKind)
        {
            PlacedFixture fixture = CreatePlacedFixture(viewKind, 3);
            fixture.Bind();
            Vector3 previousTopPosition = fixture.CardViews[2].transform.position;
            fixture.Container.Reorder(0, 2);

            fixture.ApplyAcceptedLayout();

            AssertVector3(fixture.CardViews[0].transform.position, previousTopPosition);
            Assert.That(fixture.Cards[0].BaseState.ContainerId, Is.EqualTo(fixture.Container.Id));
            Assert.That(fixture.Cards[0].BaseState.Pose, Is.EqualTo(CreatePose(10.0, 11.0, 0f)));
        }

        [TestCase(PlacedCollectionKind.Deck)]
        [TestCase(PlacedCollectionKind.Stack)]
        [TestCase(PlacedCollectionKind.DiscardPile)]
        public void FailedBind_PreservesPreviousBindingAndTransforms(PlacedCollectionKind viewKind)
        {
            PlacedFixture fixture = CreatePlacedFixture(viewKind, 1);
            fixture.Bind();
            Vector3 position = fixture.CardViews[0].transform.position;
            ContainerState boundContainer = fixture.ContainerState;
            ContainerPlacementState boundPlacement = fixture.PlacementState;
            ContainerPlacementState mismatchedPlacement = new ContainerPlacementState(ContainerId.New(), fixture.Placement.Pose);

            Assert.Throws<ArgumentException>(() => fixture.BindWithPlacement(mismatchedPlacement));

            Assert.That(fixture.ContainerState, Is.SameAs(boundContainer));
            Assert.That(fixture.PlacementState, Is.SameAs(boundPlacement));
            AssertVector3(fixture.CardViews[0].transform.position, position);
        }

        [TestCase(PlacedCollectionKind.Deck)]
        [TestCase(PlacedCollectionKind.Stack)]
        [TestCase(PlacedCollectionKind.DiscardPile)]
        public void Unbind_DoesNotMutateRuntimeState(PlacedCollectionKind viewKind)
        {
            PlacedFixture fixture = CreatePlacedFixture(viewKind, 1);
            TabletopPose pose = fixture.Cards[0].BaseState.Pose;
            fixture.Bind();

            fixture.Unbind();

            Assert.That(fixture.Cards[0].BaseState.Pose, Is.EqualTo(pose));
            Assert.That(fixture.Cards[0].BaseState.ContainerId, Is.EqualTo(fixture.Container.Id));
            Assert.That(fixture.ViewGameObject.GetComponent<DeckView>()?.IsBound ?? false, Is.False);
        }

        private PlacedFixture CreatePlacedFixture(
            PlacedCollectionKind viewKind,
            int cardCount,
            ContainerKind? containerKindOverride = null,
            ContainerId? placementIdOverride = null)
        {
            ContainerKind kind = containerKindOverride ?? ToContainerKind(viewKind);
            ContainerState container = new ContainerState(ContainerId.New(), kind, SeatId.Empty, ObjectVisibility.Public, 0);
            ContainerPlacementState placement = new ContainerPlacementState(
                placementIdOverride ?? container.Id,
                CreatePose(1.0, 2.0, 30f));
            List<CardInstanceState> cards = new List<CardInstanceState>();
            List<CardView> views = new List<CardView>();
            ContainerTransferService transferService = new ContainerTransferService();

            for (int i = 0; i < cardCount; i++)
            {
                CardInstanceState card = CreateCard(i + 10, CreatePose(10.0 + i, 11.0 + i, 0f));
                transferService.PlaceIntoContainer(card.BaseState, container);
                CardView view = CreateCardView($"Card{i}", card);
                cards.Add(card);
                views.Add(view);
            }

            GameObject viewGameObject = CreateGameObject($"{viewKind}View");
            DeckView deckView = null;
            StackView stackView = null;
            DiscardPileView discardPileView = null;
            switch (viewKind)
            {
                case PlacedCollectionKind.Deck:
                    deckView = viewGameObject.AddComponent<DeckView>();
                    break;
                case PlacedCollectionKind.Stack:
                    stackView = viewGameObject.AddComponent<StackView>();
                    break;
                case PlacedCollectionKind.DiscardPile:
                    discardPileView = viewGameObject.AddComponent<DiscardPileView>();
                    break;
            }

            return new PlacedFixture(
                viewKind,
                viewGameObject,
                container,
                placement,
                cards,
                views,
                deckView,
                stackView,
                discardPileView,
                CreateConverter());
        }

        private CardView CreateCardView(string name, CardInstanceState card)
        {
            CardView view = CreateGameObject(name).AddComponent<CardView>();
            view.Bind(card, CreateConverter());
            return view;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private static ContainerKind ToContainerKind(PlacedCollectionKind viewKind)
        {
            switch (viewKind)
            {
                case PlacedCollectionKind.Deck:
                    return ContainerKind.Deck;
                case PlacedCollectionKind.Stack:
                    return ContainerKind.Stack;
                case PlacedCollectionKind.DiscardPile:
                    return ContainerKind.DiscardPile;
                default:
                    throw new ArgumentOutOfRangeException(nameof(viewKind), viewKind, "Unsupported placed collection kind.");
            }
        }

        private static CardInstanceState CreateCard(int seed, TabletopPose pose)
        {
            return new CardInstanceState(
                new TabletopObjectState(
                    new TabletopObjectId(GuidFromSeed(seed)),
                    new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                    TabletopObjectKind.Card,
                    pose,
                    ContainerId.Empty,
                    PlayerId.Empty,
                    ObjectVisibility.Public,
                    false),
                CardFace.FaceUp);
        }

        private static TabletopCoordinateConverter CreateConverter()
        {
            return new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
        }

        private static TabletopPose CreatePose(double x, double y, float rotation)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotation, 0, 0);
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

        public enum PlacedCollectionKind
        {
            Deck,
            Stack,
            DiscardPile
        }

        private sealed class PlacedFixture
        {
            public PlacedFixture(
                PlacedCollectionKind viewKind,
                GameObject viewGameObject,
                ContainerState container,
                ContainerPlacementState placement,
                List<CardInstanceState> cards,
                List<CardView> cardViews,
                DeckView deckView,
                StackView stackView,
                DiscardPileView discardPileView,
                TabletopCoordinateConverter converter)
            {
                ViewKind = viewKind;
                ViewGameObject = viewGameObject;
                Container = container;
                Placement = placement;
                Cards = cards;
                CardViews = cardViews;
                DeckView = deckView;
                StackView = stackView;
                DiscardPileView = discardPileView;
                Converter = converter;
            }

            public PlacedCollectionKind ViewKind { get; }

            public GameObject ViewGameObject { get; }

            public ContainerState Container { get; }

            public ContainerPlacementState Placement { get; }

            public List<CardInstanceState> Cards { get; }

            public List<CardView> CardViews { get; }

            public DeckView DeckView { get; }

            public StackView StackView { get; }

            public DiscardPileView DiscardPileView { get; }

            public TabletopCoordinateConverter Converter { get; }

            public bool IsBound => DeckView?.IsBound ?? StackView?.IsBound ?? DiscardPileView.IsBound;

            public ContainerId ContainerId => DeckView?.ContainerId ?? StackView?.ContainerId ?? DiscardPileView.ContainerId;

            public ContainerState ContainerState => DeckView?.ContainerState ?? StackView?.ContainerState ?? DiscardPileView.ContainerState;

            public ContainerPlacementState PlacementState => DeckView?.PlacementState ?? StackView?.PlacementState ?? DiscardPileView.PlacementState;

            public int VisibleCardCount => DeckView?.VisibleCardCount ?? StackView?.VisibleCardCount ?? DiscardPileView.VisibleCardCount;

            public void Bind()
            {
                BindWithPlacement(Placement);
            }

            public void BindWithPlacement(ContainerPlacementState placement)
            {
                if (DeckView != null)
                {
                    DeckView.Bind(Container, placement, Converter, CardViews);
                    return;
                }

                if (StackView != null)
                {
                    StackView.Bind(Container, placement, Converter, CardViews);
                    return;
                }

                DiscardPileView.Bind(Container, placement, Converter, CardViews);
            }

            public void ApplyAcceptedLayout()
            {
                if (DeckView != null)
                {
                    DeckView.ApplyAcceptedLayout();
                    return;
                }

                if (StackView != null)
                {
                    StackView.ApplyAcceptedLayout();
                    return;
                }

                DiscardPileView.ApplyAcceptedLayout();
            }

            public void Unbind()
            {
                if (DeckView != null)
                {
                    DeckView.Unbind();
                    return;
                }

                if (StackView != null)
                {
                    StackView.Unbind();
                    return;
                }

                DiscardPileView.Unbind();
            }
        }
    }
}
