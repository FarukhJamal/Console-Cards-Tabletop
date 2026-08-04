using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class CardContainerLayoutProjectionTests
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
        public void BoundContainedCard_AcceptsContainerLayoutPose()
        {
            CardFixture fixture = CreateContainedCardFixture();
            TabletopPose layoutPose = CreatePose(4.0, 5.0, 35f);

            fixture.View.ApplyContainerLayoutPose(layoutPose);

            Assert.That(fixture.View.IsContainerLayoutApplied, Is.True);
            Assert.That(fixture.View.ContainerLayoutPose, Is.EqualTo(layoutPose));
            AssertWorldPose(fixture.View, layoutPose);
        }

        [Test]
        public void ApplyContainerLayoutPose_ChangesTransformOnly()
        {
            CardFixture fixture = CreateContainedCardFixture(face: CardFace.FaceDown);
            TabletopPose statePose = fixture.Card.BaseState.Pose;
            ContainerId containerId = fixture.Card.BaseState.ContainerId;
            CardFace face = fixture.Card.Face;

            fixture.View.ApplyContainerLayoutPose(CreatePose(-3.0, 7.0, -450f));

            Assert.That(fixture.Card.BaseState.Pose, Is.EqualTo(statePose));
            Assert.That(fixture.Card.BaseState.ContainerId, Is.EqualTo(containerId));
            Assert.That(fixture.Card.Face, Is.EqualTo(face));
            Assert.That(fixture.View.IsPreviewing, Is.False);
        }

        [Test]
        public void ApplyContainerLayoutPose_PreservesScale()
        {
            CardFixture fixture = CreateContainedCardFixture();
            Vector3 scale = new Vector3(2f, 3f, 4f);
            fixture.View.transform.localScale = scale;

            fixture.View.ApplyContainerLayoutPose(CreatePose(1.0, 2.0, 10f), 0.25f);

            AssertVector3(fixture.View.transform.localScale, scale);
        }

        [Test]
        public void ApplyContainerLayoutPose_ReplacesExistingLayoutPose()
        {
            CardFixture fixture = CreateContainedCardFixture();
            TabletopPose secondPose = CreatePose(6.0, -2.0, 80f);

            fixture.View.ApplyContainerLayoutPose(CreatePose(1.0, 1.0, 10f));
            fixture.View.ApplyContainerLayoutPose(secondPose);

            Assert.That(fixture.View.ContainerLayoutPose, Is.EqualTo(secondPose));
            AssertWorldPose(fixture.View, secondPose);
        }

        [TestCase(double.NaN, 1.0, 0f)]
        [TestCase(1.0, double.PositiveInfinity, 0f)]
        [TestCase(1.0, 2.0, float.NegativeInfinity)]
        public void ApplyContainerLayoutPose_WhenPoseIsNonFinite_RejectsAtomically(
            double x,
            double y,
            float rotation)
        {
            CardFixture fixture = CreateContainedCardFixture();
            fixture.View.ApplyContainerLayoutPose(CreatePose(1.0, 2.0, 10f));
            Vector3 position = fixture.View.transform.position;
            Quaternion rotationBefore = fixture.View.transform.rotation;
            TabletopPose layoutPose = fixture.View.ContainerLayoutPose;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => fixture.View.ApplyContainerLayoutPose(CreatePose(x, y, rotation)));

            Assert.That(fixture.View.ContainerLayoutPose, Is.EqualTo(layoutPose));
            AssertVector3(fixture.View.transform.position, position);
            Assert.That(Quaternion.Angle(rotationBefore, fixture.View.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ApplyContainerLayoutPose_WhenUnbound_ThrowsInvalidOperationException()
        {
            CardView view = CreateView("UnboundCard");

            Assert.Throws<InvalidOperationException>(() => view.ApplyContainerLayoutPose(CreatePose(1.0, 1.0, 0f)));
        }

        [Test]
        public void ApplyContainerLayoutPose_WhenCardIsOnTabletop_ThrowsInvalidOperationException()
        {
            CardView view = CreateView("TabletopCard");
            CardInstanceState card = CreateCard(10, CreatePose(0.0, 0.0, 0f));
            view.Bind(card, CreateConverter());

            Assert.Throws<InvalidOperationException>(() => view.ApplyContainerLayoutPose(CreatePose(1.0, 1.0, 0f)));
        }

        [Test]
        public void ClearContainerLayoutAndReconcile_ReappliesAcceptedCardPose()
        {
            CardFixture fixture = CreateContainedCardFixture();
            TabletopPose acceptedPose = fixture.Card.BaseState.Pose;
            fixture.View.ApplyContainerLayoutPose(CreatePose(5.0, 6.0, 45f));

            fixture.View.ClearContainerLayoutAndReconcile();

            Assert.That(fixture.View.IsContainerLayoutApplied, Is.False);
            Assert.That(fixture.View.ContainerLayoutPose, Is.EqualTo(TabletopPose.Default));
            AssertWorldPose(fixture.View, acceptedPose);
        }

        [Test]
        public void ClearContainerLayout_ClearsLayoutStateWithoutMovingTransform()
        {
            CardFixture fixture = CreateContainedCardFixture();
            fixture.View.ApplyContainerLayoutPose(CreatePose(5.0, 6.0, 45f));
            Vector3 position = fixture.View.transform.position;
            Quaternion rotation = fixture.View.transform.rotation;

            fixture.View.ClearContainerLayout();

            Assert.That(fixture.View.IsContainerLayoutApplied, Is.False);
            AssertVector3(fixture.View.transform.position, position);
            Assert.That(Quaternion.Angle(rotation, fixture.View.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Unbind_ClearsContainerLayoutStateSafely()
        {
            CardFixture fixture = CreateContainedCardFixture();
            fixture.View.ApplyContainerLayoutPose(CreatePose(5.0, 6.0, 45f));

            fixture.View.Unbind();

            Assert.That(fixture.View.IsContainerLayoutApplied, Is.False);
            Assert.That(fixture.View.ContainerLayoutPose, Is.EqualTo(TabletopPose.Default));
        }

        [Test]
        public void ApplyPreviewPose_RemainsSeparateFromContainerLayoutState()
        {
            CardFixture fixture = CreateContainedCardFixture();
            TabletopPose layoutPose = CreatePose(5.0, 6.0, 45f);
            TabletopPose previewPose = CreatePose(-5.0, -6.0, 90f);

            fixture.View.ApplyContainerLayoutPose(layoutPose);
            fixture.View.ApplyPreviewPose(previewPose);

            Assert.That(fixture.View.IsContainerLayoutApplied, Is.True);
            Assert.That(fixture.View.ContainerLayoutPose, Is.EqualTo(layoutPose));
            Assert.That(fixture.View.IsPreviewing, Is.True);
            AssertWorldPose(fixture.View, previewPose);
        }

        [Test]
        public void FacePresentation_RemainsAuthoritativeDuringContainerLayout()
        {
            CardFixture fixture = CreateContainedCardFixture(face: CardFace.FaceDown, configureFace: true);

            fixture.View.ApplyContainerLayoutPose(CreatePose(5.0, 6.0, 45f));

            Assert.That(fixture.View.DisplayedFace, Is.EqualTo(CardFace.FaceDown));
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceDown));
        }

        private CardFixture CreateContainedCardFixture(
            CardFace face = CardFace.FaceUp,
            bool configureFace = false)
        {
            ContainerState container = CreateContainer(ContainerKind.Deck);
            CardInstanceState card = CreateCard(1, CreatePose(2.0, 3.0, 15f), face);
            new ContainerTransferService().PlaceIntoContainer(card.BaseState, container);
            CardView view = CreateView("CardView");
            if (configureFace)
            {
                GameObject up = CreateChild("FaceUp", view.transform);
                GameObject down = CreateChild("FaceDown", view.transform);
                view.ConfigureFacePresentation(up, down);
            }

            view.Bind(card, CreateConverter());
            return new CardFixture(card, view, container);
        }

        private CardView CreateView(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject.AddComponent<CardView>();
        }

        private GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            createdGameObjects.Add(child);
            return child;
        }

        private static CardInstanceState CreateCard(int seed, TabletopPose pose, CardFace face = CardFace.FaceUp)
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
                face);
        }

        private static ContainerState CreateContainer(ContainerKind kind)
        {
            return new ContainerState(ContainerId.New(), kind, SeatId.Empty, ObjectVisibility.Public, 0);
        }

        private static TabletopCoordinateConverter CreateConverter()
        {
            return new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
        }

        private static TabletopPose CreatePose(double x, double y, float rotation)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotation, 0, 0);
        }

        private static void AssertWorldPose(CardView view, TabletopPose pose)
        {
            AssertVector3(view.transform.position, new Vector3((float)pose.Position.X, 0f, (float)pose.Position.Y));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, pose.RotationDegrees, 0f), view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
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

        private sealed class CardFixture
        {
            public CardFixture(CardInstanceState card, CardView view, ContainerState container)
            {
                Card = card;
                View = view;
                Container = container;
            }

            public CardInstanceState Card { get; }

            public CardView View { get; }

            public ContainerState Container { get; }
        }
    }
}
