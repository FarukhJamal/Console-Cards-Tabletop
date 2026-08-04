using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopContainerDropTargetTests
    {
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
        public void StartsUnconfigured()
        {
            TabletopContainerDropTarget target = CreateDropTarget(out BoxCollider collider);

            Assert.That(target.IsConfigured, Is.False);
            Assert.That(target.ContainerView, Is.Null);
            Assert.That(target.ContainerId, Is.EqualTo(ContainerId.Empty));
            Assert.That(target.TargetCollider, Is.Null);
            Assert.That(collider.enabled, Is.True);
        }

        [TestCase(ContainerViewKind.Deck)]
        [TestCase(ContainerViewKind.Stack)]
        [TestCase(ContainerViewKind.DiscardPile)]
        [TestCase(ContainerViewKind.Hand)]
        [TestCase(ContainerViewKind.ConsoleSlot)]
        public void Configure_WithBoundContainerView_Accepts(ContainerViewKind viewKind)
        {
            BoundContainerViewFixture fixture = CreateBoundContainerView(viewKind);
            TabletopContainerDropTarget target = CreateDropTarget(out BoxCollider collider);

            target.Configure(fixture.ContainerView, collider);

            Assert.That(target.IsConfigured, Is.True);
            Assert.That(target.ContainerView, Is.SameAs(fixture.ContainerView));
            Assert.That(target.ContainerId, Is.EqualTo(fixture.Container.Id));
            Assert.That(target.TargetCollider, Is.SameAs(collider));
        }

        [Test]
        public void ConsoleView_IsNotAContainerDropTargetView()
        {
            CreateGameObject("ConsoleView").AddComponent<ConsoleView>();

            Assert.That(typeof(IContainerView).IsAssignableFrom(typeof(ConsoleView)), Is.False);
        }

        [Test]
        public void Configure_WhenViewIsNull_Rejects()
        {
            TabletopContainerDropTarget target = CreateDropTarget(out BoxCollider collider);

            Assert.Throws<ArgumentNullException>(() => target.Configure(null, collider));
        }

        [Test]
        public void Configure_WhenViewIsUnbound_Rejects()
        {
            DeckView view = CreateGameObject("UnboundDeck").AddComponent<DeckView>();
            TabletopContainerDropTarget target = CreateDropTarget(out BoxCollider collider);

            Assert.Throws<ArgumentException>(() => target.Configure(view, collider));
        }

        [Test]
        public void Configure_WhenColliderIsNull_Rejects()
        {
            BoundContainerViewFixture fixture = CreateBoundContainerView(ContainerViewKind.Deck);
            TabletopContainerDropTarget target = CreateDropTarget(out _);

            Assert.Throws<ArgumentNullException>(() => target.Configure(fixture.ContainerView, null));
        }

        [Test]
        public void Configure_WhenColliderIsOutsideOwnedHierarchy_Rejects()
        {
            BoundContainerViewFixture fixture = CreateBoundContainerView(ContainerViewKind.Deck);
            TabletopContainerDropTarget target = CreateDropTarget(out _);
            BoxCollider externalCollider = CreateGameObject("ExternalCollider").AddComponent<BoxCollider>();

            Assert.Throws<ArgumentException>(() => target.Configure(fixture.ContainerView, externalCollider));
        }

        [Test]
        public void Configure_WithChildCollider_Accepts()
        {
            BoundContainerViewFixture fixture = CreateBoundContainerView(ContainerViewKind.Stack);
            TabletopContainerDropTarget target = CreateDropTargetWithChildCollider(out BoxCollider childCollider);

            target.Configure(fixture.ContainerView, childCollider);

            Assert.That(target.TargetCollider, Is.SameAs(childCollider));
        }

        [Test]
        public void FailedReconfiguration_PreservesPriorConfiguration()
        {
            BoundContainerViewFixture first = CreateBoundContainerView(ContainerViewKind.Deck);
            BoundContainerViewFixture second = CreateBoundContainerView(ContainerViewKind.Hand);
            TabletopContainerDropTarget target = CreateDropTarget(out BoxCollider collider);
            target.Configure(first.ContainerView, collider);
            ContainerId previousContainerId = target.ContainerId;
            IContainerView previousView = target.ContainerView;

            Assert.Throws<ArgumentNullException>(() => target.Configure(second.ContainerView, null));

            Assert.That(target.IsConfigured, Is.True);
            Assert.That(target.ContainerId, Is.EqualTo(previousContainerId));
            Assert.That(target.ContainerView, Is.SameAs(previousView));
            Assert.That(target.TargetCollider, Is.SameAs(collider));
        }

        [Test]
        public void ClearConfiguration_RemovesAssociationOnly()
        {
            BoundContainerViewFixture fixture = CreateBoundContainerView(ContainerViewKind.DiscardPile);
            TabletopContainerDropTarget target = CreateDropTarget(out BoxCollider collider);
            target.Configure(fixture.ContainerView, collider);

            target.ClearConfiguration();

            Assert.That(target.IsConfigured, Is.False);
            Assert.That(target.ContainerView, Is.Null);
            Assert.That(target.ContainerId, Is.EqualTo(ContainerId.Empty));
            Assert.That(target.TargetCollider, Is.Null);
            Assert.That(collider.enabled, Is.True);
            Assert.That(fixture.Container.Count, Is.EqualTo(0));
        }

        [Test]
        public void Configure_DoesNotMutateRuntimeStateOrCollider()
        {
            BoundContainerViewFixture fixture = CreateBoundContainerView(ContainerViewKind.ConsoleSlot);
            TabletopContainerDropTarget target = CreateDropTarget(out BoxCollider collider);
            ContainerKind kind = fixture.Container.Kind;
            ObjectVisibility visibility = fixture.Container.Visibility;
            int capacity = fixture.Container.Capacity;
            bool colliderEnabled = collider.enabled;

            target.Configure(fixture.ContainerView, collider);

            Assert.That(fixture.Container.Kind, Is.EqualTo(kind));
            Assert.That(fixture.Container.Visibility, Is.EqualTo(visibility));
            Assert.That(fixture.Container.Capacity, Is.EqualTo(capacity));
            Assert.That(fixture.Container.Count, Is.EqualTo(0));
            Assert.That(collider.enabled, Is.EqualTo(colliderEnabled));
        }

        private TabletopContainerDropTarget CreateDropTarget(out BoxCollider collider)
        {
            GameObject gameObject = CreateGameObject("ContainerDropTarget");
            collider = gameObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
            return gameObject.AddComponent<TabletopContainerDropTarget>();
        }

        private TabletopContainerDropTarget CreateDropTargetWithChildCollider(out BoxCollider collider)
        {
            GameObject parent = CreateGameObject("ContainerDropTarget");
            TabletopContainerDropTarget target = parent.AddComponent<TabletopContainerDropTarget>();
            GameObject child = CreateGameObject("TargetColliderChild");
            child.transform.SetParent(parent.transform, false);
            collider = child.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
            return target;
        }

        private BoundContainerViewFixture CreateBoundContainerView(ContainerViewKind viewKind)
        {
            ContainerKind containerKind = ToContainerKind(viewKind);
            ContainerState container = new ContainerState(
                ContainerId.New(),
                containerKind,
                SeatId.Empty,
                ObjectVisibility.Public,
                0);
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
            GameObject gameObject = CreateGameObject($"{viewKind}View");

            switch (viewKind)
            {
                case ContainerViewKind.Deck:
                    DeckView deckView = gameObject.AddComponent<DeckView>();
                    deckView.Bind(
                        container,
                        new ContainerPlacementState(container.Id, CreatePose(0.0, 0.0)),
                        converter,
                        Array.Empty<CardView>());
                    return new BoundContainerViewFixture(container, deckView);

                case ContainerViewKind.Stack:
                    StackView stackView = gameObject.AddComponent<StackView>();
                    stackView.Bind(
                        container,
                        new ContainerPlacementState(container.Id, CreatePose(0.0, 0.0)),
                        converter,
                        Array.Empty<CardView>());
                    return new BoundContainerViewFixture(container, stackView);

                case ContainerViewKind.DiscardPile:
                    DiscardPileView discardPileView = gameObject.AddComponent<DiscardPileView>();
                    discardPileView.Bind(
                        container,
                        new ContainerPlacementState(container.Id, CreatePose(0.0, 0.0)),
                        converter,
                        Array.Empty<CardView>());
                    return new BoundContainerViewFixture(container, discardPileView);

                case ContainerViewKind.Hand:
                    HandView handView = gameObject.AddComponent<HandView>();
                    handView.Bind(container, CreateGameObject("HandAnchor").transform, converter, Array.Empty<CardView>());
                    return new BoundContainerViewFixture(container, handView);

                case ContainerViewKind.ConsoleSlot:
                    ConsoleSlotView slotView = gameObject.AddComponent<ConsoleSlotView>();
                    slotView.Bind(container, CreateGameObject("SlotAnchor").transform, converter, Array.Empty<CardView>());
                    return new BoundContainerViewFixture(container, slotView);

                default:
                    throw new ArgumentOutOfRangeException(nameof(viewKind), viewKind, "Unsupported test view kind.");
            }
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private static ContainerKind ToContainerKind(ContainerViewKind viewKind)
        {
            switch (viewKind)
            {
                case ContainerViewKind.Deck:
                    return ContainerKind.Deck;
                case ContainerViewKind.Stack:
                    return ContainerKind.Stack;
                case ContainerViewKind.DiscardPile:
                    return ContainerKind.DiscardPile;
                case ContainerViewKind.Hand:
                    return ContainerKind.Hand;
                case ContainerViewKind.ConsoleSlot:
                    return ContainerKind.ConsoleSlot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(viewKind), viewKind, "Unsupported test view kind.");
            }
        }

        private static TabletopPose CreatePose(double x, double y)
        {
            return new TabletopPose(new TableCoordinate(x, y), 0f, 0, 0);
        }

        public enum ContainerViewKind
        {
            Deck,
            Stack,
            DiscardPile,
            Hand,
            ConsoleSlot
        }

        private sealed class BoundContainerViewFixture
        {
            public BoundContainerViewFixture(ContainerState container, IContainerView containerView)
            {
                Container = container;
                ContainerView = containerView;
            }

            public ContainerState Container { get; }

            public IContainerView ContainerView { get; }
        }
    }
}
