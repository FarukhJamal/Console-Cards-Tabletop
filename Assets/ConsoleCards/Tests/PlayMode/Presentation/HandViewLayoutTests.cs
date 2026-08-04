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
    public sealed class HandViewLayoutTests
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
        public void Bind_WhenKindIsHand_AcceptsExactStateInstance()
        {
            HandFixture fixture = CreateHandFixture(2);

            fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews);

            Assert.That(fixture.View.IsBound, Is.True);
            Assert.That(fixture.View.ContainerId, Is.EqualTo(fixture.Container.Id));
            Assert.That(fixture.View.ContainerState, Is.SameAs(fixture.Container));
            Assert.That(fixture.View.LayoutAnchor, Is.SameAs(fixture.Anchor));
            Assert.That(fixture.View.VisibleCardCount, Is.EqualTo(2));
        }

        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.ConsoleSlot)]
        [TestCase(ContainerKind.Generic)]
        public void Bind_WhenKindIsNotHand_Rejects(ContainerKind kind)
        {
            HandFixture fixture = CreateHandFixture(1, kind);

            Assert.Throws<ArgumentException>(
                () => fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews));
        }

        [Test]
        public void EmptyHand_IsValid()
        {
            HandFixture fixture = CreateHandFixture(0);

            fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews);

            Assert.That(fixture.View.IsBound, Is.True);
            Assert.That(fixture.View.VisibleCardCount, Is.EqualTo(0));
        }

        [Test]
        public void OneCardHand_IsCenteredWithZeroFan()
        {
            HandFixture fixture = CreateHandFixture(1);
            fixture.View.HorizontalSpacing = 0.75f;
            fixture.View.FanAngleDegrees = 10f;

            fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews);

            AssertVector3(fixture.CardViews[0].transform.position, fixture.Anchor.position);
            Assert.That(Quaternion.Angle(fixture.Anchor.rotation, fixture.CardViews[0].transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void MultipleCards_AreCenteredLeftToRightWithSymmetricFan()
        {
            HandFixture fixture = CreateHandFixture(3);
            fixture.View.HorizontalSpacing = 1f;
            fixture.View.FanAngleDegrees = 10f;

            fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews);

            Assert.That(fixture.CardViews[0].transform.position.x, Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(fixture.CardViews[1].transform.position.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(fixture.CardViews[2].transform.position.x, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, -10f, 0f), fixture.CardViews[0].transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 0f, 0f), fixture.CardViews[1].transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 10f, 0f), fixture.CardViews[2].transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void RuntimeStateAndHandMetadataRemainUnchanged()
        {
            HandFixture fixture = CreateHandFixture(2);
            TabletopPose cardPose = fixture.Cards[0].BaseState.Pose;
            SeatId owner = fixture.Container.OwnerSeatId;
            ObjectVisibility visibility = fixture.Container.Visibility;
            int capacity = fixture.Container.Capacity;

            fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews);
            fixture.View.ApplyAcceptedLayout();

            Assert.That(fixture.Cards[0].BaseState.Pose, Is.EqualTo(cardPose));
            Assert.That(fixture.Container.OwnerSeatId, Is.EqualTo(owner));
            Assert.That(fixture.Container.Visibility, Is.EqualTo(visibility));
            Assert.That(fixture.Container.Capacity, Is.EqualTo(capacity));
        }

        [Test]
        public void ReorderRefresh_ChangesVisualOrdering()
        {
            HandFixture fixture = CreateHandFixture(3);
            fixture.View.HorizontalSpacing = 1f;
            fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews);
            Vector3 previousRightPosition = fixture.CardViews[2].transform.position;

            fixture.Container.Reorder(0, 2);
            fixture.View.ApplyAcceptedLayout();

            AssertVector3(fixture.CardViews[0].transform.position, previousRightPosition);
        }

        [Test]
        public void StaleCardNoLongerInContainer_StopsBeingLayoutOwnedOnRefresh()
        {
            HandFixture fixture = CreateHandFixture(2);
            fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews);
            new ContainerTransferService().RemoveFromContainer(fixture.Cards[0].BaseState, fixture.Container);

            fixture.View.ApplyAcceptedLayout();

            Assert.That(fixture.CardViews[0].IsContainerLayoutApplied, Is.False);
            Assert.That(fixture.CardViews[1].IsContainerLayoutApplied, Is.True);
        }

        [Test]
        public void MissingOrDuplicateCardView_IsRejected()
        {
            HandFixture missingFixture = CreateHandFixture(2);
            missingFixture.CardViews.RemoveAt(1);
            HandFixture duplicateFixture = CreateHandFixture(2);
            duplicateFixture.CardViews[1] = duplicateFixture.CardViews[0];

            Assert.Throws<KeyNotFoundException>(
                () => missingFixture.View.Bind(
                    missingFixture.Container,
                    missingFixture.Anchor,
                    missingFixture.Converter,
                    missingFixture.CardViews));
            Assert.Throws<ArgumentException>(
                () => duplicateFixture.View.Bind(
                    duplicateFixture.Container,
                    duplicateFixture.Anchor,
                    duplicateFixture.Converter,
                    duplicateFixture.CardViews));
        }

        [Test]
        public void InvalidConfigRejectedAndFailedBindPreservesPreviousState()
        {
            HandFixture fixture = CreateHandFixture(1);
            fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews);
            Vector3 position = fixture.CardViews[0].transform.position;

            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.View.HorizontalSpacing = float.NaN);
            Assert.Throws<ArgumentNullException>(
                () => fixture.View.Bind(fixture.Container, null, fixture.Converter, fixture.CardViews));

            Assert.That(fixture.View.IsBound, Is.True);
            AssertVector3(fixture.CardViews[0].transform.position, position);
        }

        [Test]
        public void NoPlacementStateIsRequired()
        {
            HandFixture fixture = CreateHandFixture(1);

            fixture.View.Bind(fixture.Container, fixture.Anchor, fixture.Converter, fixture.CardViews);

            Assert.That(fixture.View.ContainerState, Is.SameAs(fixture.Container));
        }

        private HandFixture CreateHandFixture(int cardCount, ContainerKind kind = ContainerKind.Hand)
        {
            SeatId ownerSeatId = SeatId.New();
            ContainerState container = new ContainerState(
                ContainerId.New(),
                kind,
                ownerSeatId,
                ObjectVisibility.OwnerOnly,
                5);
            Transform anchor = CreateGameObject("HandAnchor").transform;
            HandView view = CreateGameObject("HandView").AddComponent<HandView>();
            List<CardInstanceState> cards = new List<CardInstanceState>();
            List<CardView> cardViews = new List<CardView>();
            ContainerTransferService transferService = new ContainerTransferService();

            for (int i = 0; i < cardCount; i++)
            {
                CardInstanceState card = CreateCard(i + 20);
                transferService.PlaceIntoContainer(card.BaseState, container);
                CardView cardView = CreateGameObject($"Card{i}").AddComponent<CardView>();
                cardView.Bind(card, CreateConverter());
                cards.Add(card);
                cardViews.Add(cardView);
            }

            return new HandFixture(container, anchor, view, cards, cardViews, CreateConverter());
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

        private sealed class HandFixture
        {
            public HandFixture(
                ContainerState container,
                Transform anchor,
                HandView view,
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

            public HandView View { get; }

            public List<CardInstanceState> Cards { get; }

            public List<CardView> CardViews { get; }

            public TabletopCoordinateConverter Converter { get; }
        }
    }
}
